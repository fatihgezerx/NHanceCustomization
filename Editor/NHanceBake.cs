using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Items;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NHanceCustomization
{
    /// <summary>What one <see cref="NHanceBake.Bake"/> did.</summary>
    public sealed class NHanceBakeReport
    {
        /// <summary>Equipment items whose renderers now use the character's skeleton.</summary>
        public int ItemsBaked;

        /// <summary>Skinned renderers moved onto the character's skeleton.</summary>
        public int RenderersRebound;

        /// <summary>Transforms of the items' own skeleton copies that were deleted.</summary>
        public int TransformsRemoved;

        /// <summary>Skeleton-copy transforms that were kept because a component or a reference still uses them.</summary>
        public int TransformsKept;

        /// <summary>Equipment items that could not be baked here (inherited from a base prefab, or broken).</summary>
        public int ItemsSkipped;

        /// <summary>One line per skipped item or other notice.</summary>
        public readonly List<string> Notes = new List<string>();

        /// <summary>Adds another report's numbers to this one.</summary>
        public void Add(NHanceBakeReport other)
        {
            ItemsBaked += other.ItemsBaked;
            RenderersRebound += other.RenderersRebound;
            TransformsRemoved += other.TransformsRemoved;
            TransformsKept += other.TransformsKept;
            ItemsSkipped += other.ItemsSkipped;
            Notes.AddRange(other.Notes);
        }

        public override string ToString()
        {
            var text = new StringBuilder();
            text.Append($"{ItemsBaked} item(s) baked, {RenderersRebound} renderer(s) moved onto the character's skeleton, ")
                .Append($"{TransformsRemoved} skeleton-copy transform(s) removed, {TransformsKept} kept (still used), {ItemsSkipped} item(s) skipped.");
            foreach (string note in Notes)
            {
                text.Append("\n  ").Append(note);
            }

            return text.ToString();
        }
    }

    /// <summary>
    /// Turns a dressed N-Hance character into a lean one. Every equipment item N-Hance builds brings a full copy of the skeleton and an
    /// <see cref="Equipment"/> component that moves the item's renderers onto the character's bones in <c>Start</c>, each time the character
    /// is spawned, and leaves the copy behind (a few hundred unused transforms per item). Baking does that move once, in the editor, exactly
    /// the way <see cref="Equipment"/> would (same bone lookup, same <c>updateWhenOffscreen</c>), then deletes the copies and the component.
    /// The character looks and animates the same; it spawns faster and carries far fewer transforms.
    /// <para>Nothing that is still used is deleted: a copy transform that holds a component, or that any component of the character refers to
    /// (a rig or IK component's setup pose, say), is kept together with its parents, and such references are never redirected.</para>
    /// </summary>
    public static class NHanceBake
    {
        private const string MenuRoot = "Tools/N-Hance Customization/";

        // Not public API: lets a bake leave the open scene as clean as it found it (N-Hance creates the items there first).
        private static readonly MethodInfo DirtyClear =
            typeof(EditorSceneManager).GetMethod("ClearSceneDirtiness", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);

        /// <summary>
        /// Bakes every <see cref="Equipment"/> item under <paramref name="root"/> in place. With <paramref name="stripNHanceComponents"/> the
        /// <see cref="NHAvatar"/> and <see cref="NHItem"/> components go too (the result can no longer be re-dressed; dress the look again instead).
        /// Items that belong to a nested prefab instance are skipped and listed: bake the prefab they come from.
        /// </summary>
        public static NHanceBakeReport Bake(GameObject root, bool stripNHanceComponents)
        {
            var report = new NHanceBakeReport();
            if (root == null)
            {
                return report;
            }

            var baked = new List<Equipment>();
            foreach (Equipment equipment in root.GetComponentsInChildren<Equipment>(true))
            {
                if (PrefabUtility.IsPartOfPrefabInstance(equipment))
                {
                    GameObject source = PrefabUtility.GetCorrespondingObjectFromOriginalSource(equipment.gameObject);
                    report.ItemsSkipped++;
                    report.Notes.Add($"'{equipment.name}' comes from {(source != null ? AssetDatabase.GetAssetPath(source) : "a nested prefab")}: bake that prefab.");
                    continue;
                }

                if (TryRebind(equipment, report))
                {
                    baked.Add(equipment);
                }
            }

            if (baked.Count > 0)
            {
                HashSet<Transform> used = UsedTransforms(root);
                foreach (Equipment equipment in baked)
                {
                    Prune(equipment.transform, used, report);
                }

                foreach (Equipment equipment in baked)
                {
                    Object.DestroyImmediate(equipment, true);
                }
            }

            if (stripNHanceComponents)
            {
                foreach (NHAvatar avatar in root.GetComponentsInChildren<NHAvatar>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(avatar))
                    {
                        Object.DestroyImmediate(avatar, true);
                    }
                }

                foreach (NHItem item in root.GetComponentsInChildren<NHItem>(true))
                {
                    if (!PrefabUtility.IsPartOfPrefabInstance(item))
                    {
                        Object.DestroyImmediate(item, true);
                    }
                }
            }

            return report;
        }

        /// <summary>
        /// Bakes prefab assets in place (same files, same GUIDs, so every reference to them stays). Base prefabs are baked before their
        /// variants. N-Hance components other than <see cref="Equipment"/> are kept.
        /// </summary>
        public static NHanceBakeReport BakePrefabs(IEnumerable<string> prefabPaths)
        {
            var total = new NHanceBakeReport();
            foreach (string path in BasesFirst(prefabPaths))
            {
                GameObject contents = PrefabUtility.LoadPrefabContents(path);
                try
                {
                    NHanceBakeReport report = Bake(contents, false);
                    if (report.ItemsBaked > 0)
                    {
                        PrefabUtility.SaveAsPrefabAsset(contents, path);
                    }

                    report.Notes.Insert(0, $"{path}: {report.ItemsBaked} baked, {report.TransformsRemoved} transforms removed");
                    total.Add(report);
                }
                finally
                {
                    PrefabUtility.UnloadPrefabContents(contents);
                }
            }

            return total;
        }

        /// <summary>
        /// Dresses a copy of <paramref name="look"/>'s Preview Base with the look, bakes it, and saves it as a new prefab at
        /// <paramref name="prefabPath"/> (overwritten if it exists). The prefab is a plain prefab, not a variant of the Preview Base.
        /// </summary>
        public static NHanceBakeReport BakeLookToPrefab(NHanceLookData look, string prefabPath, bool stripNHanceComponents = true)
        {
            if (look == null || look.PreviewBase == null)
            {
                throw new System.ArgumentException("The look needs a Preview Base (a character prefab with an NHAvatar).");
            }

            Scene preview = EditorSceneManager.NewPreviewScene();
            Scene active = SceneManager.GetActiveScene();
            bool wasDirty = active.isDirty;
            try
            {
                var instance = (GameObject)PrefabUtility.InstantiatePrefab(look.PreviewBase.gameObject, preview);
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                instance.name = look.name;

                NHAvatar avatar = instance.GetComponentInChildren<NHAvatar>(true);
                NHanceLookRules.Apply(avatar, look);

                // N-Hance builds the items as prefab instances (in the open scene, then moves them under the character): make them plain objects.
                foreach (NHItem item in instance.GetComponentsInChildren<NHItem>(true))
                {
                    if (item != null && PrefabUtility.IsOutermostPrefabInstanceRoot(item.gameObject))
                    {
                        PrefabUtility.UnpackPrefabInstance(item.gameObject, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    }
                }

                NHanceBakeReport report = Bake(instance, stripNHanceComponents);

                // A Preview Base copied from another project can carry components whose scripts this project does not have; Unity
                // refuses to save a prefab with a missing script.
                int missing = 0;
                foreach (Transform t in instance.GetComponentsInChildren<Transform>(true))
                {
                    missing += GameObjectUtility.RemoveMonoBehavioursWithMissingScript(t.gameObject);
                }

                if (missing > 0)
                {
                    report.Notes.Add($"{missing} component(s) with a missing script removed from the copy of the Preview Base.");
                }

                string folder = Path.GetDirectoryName(prefabPath);
                if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
                {
                    Directory.CreateDirectory(folder);
                    AssetDatabase.Refresh();
                }

                PrefabUtility.SaveAsPrefabAsset(instance, prefabPath);
                report.Notes.Insert(0, $"{look.name} -> {prefabPath}");
                return report;
            }
            finally
            {
                EditorSceneManager.ClosePreviewScene(preview);
                if (!wasDirty && active.isDirty && DirtyClear != null)
                {
                    DirtyClear.Invoke(null, new object[] { active });
                }
            }
        }

        // ---- menus --------------------------------------------------------------------------------------------------------------

        [MenuItem(MenuRoot + "Bake Selected Prefabs")]
        private static void BakeSelectedPrefabs()
        {
            string[] paths = Selection.objects.Select(AssetDatabase.GetAssetPath).Where(p => p.EndsWith(".prefab")).Distinct().ToArray();
            NHanceBakeReport report = BakePrefabs(paths);
            Debug.Log("[NHanceCustomization] Bake: " + report);
            EditorUtility.DisplayDialog("Bake N-Hance prefabs", report.ToString(), "OK");
        }

        [MenuItem(MenuRoot + "Bake Selected Prefabs", true)]
        private static bool CanBakeSelectedPrefabs() => Selection.objects.Any(o => AssetDatabase.GetAssetPath(o).EndsWith(".prefab"));

        [MenuItem("CONTEXT/NHanceLookData/Bake to Prefab...")]
        private static void BakeLookFromContext(MenuCommand command) => BakeLookWithDialog(command.context as NHanceLookData);

        [MenuItem("Assets/N-Hance/Bake Look to Prefab...")]
        private static void BakeLookFromAssets() => BakeLookWithDialog(Selection.activeObject as NHanceLookData);

        [MenuItem("Assets/N-Hance/Bake Look to Prefab...", true)]
        private static bool CanBakeLookFromAssets() => Selection.activeObject is NHanceLookData;

        private static void BakeLookWithDialog(NHanceLookData look)
        {
            if (look == null)
            {
                return;
            }

            if (look.PreviewBase == null)
            {
                EditorUtility.DisplayDialog("Bake to Prefab", NHanceLookScene.MissingBaseText, "OK");
                return;
            }

            string folder = Path.GetDirectoryName(AssetDatabase.GetAssetPath(look));
            string path = EditorUtility.SaveFilePanelInProject("Bake to Prefab", look.name, "prefab", "Where to save the baked character", folder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            NHanceBakeReport report = BakeLookToPrefab(look, path);
            Debug.Log("[NHanceCustomization] Bake: " + report, AssetDatabase.LoadAssetAtPath<GameObject>(path));
        }

        // ---- baking -------------------------------------------------------------------------------------------------------------

        // Equipment.Start, once: the same bone map, the same renderers, the same result. Returns false (and changes nothing) when Start
        // would fail at run time (no target, a missing bone reference); such an item is left as it is.
        private static bool TryRebind(Equipment equipment, NHanceBakeReport report)
        {
            Transform target = equipment.Target;
            if (target == null)
            {
                report.ItemsSkipped++;
                report.Notes.Add($"'{equipment.name}' has no Target: left as it is.");
                return false;
            }

            var boneMap = new Dictionary<string, Transform>();
            MapBones(boneMap, target);

            List<SkinnedMeshRenderer> renderers = RenderersStartWouldFit(equipment);
            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                if (renderer.bones.Any(b => b == null) || renderer.rootBone == null)
                {
                    report.ItemsSkipped++;
                    report.Notes.Add($"'{equipment.name}/{renderer.name}' has a missing bone: left as it is.");
                    return false;
                }
            }

            foreach (SkinnedMeshRenderer renderer in renderers)
            {
                Transform[] bones = renderer.bones;
                var mapped = new Transform[bones.Length];
                for (int i = 0; i < bones.Length; i++)
                {
                    if (!boneMap.TryGetValue(bones[i].name, out mapped[i]))
                    {
                        report.Notes.Add($"'{equipment.name}/{renderer.name}': no bone '{bones[i].name}' on the character (N-Hance leaves it empty too).");
                    }
                }

                boneMap.TryGetValue(renderer.rootBone.name, out Transform rootBone);
                renderer.bones = mapped;
                renderer.rootBone = rootBone;
                renderer.updateWhenOffscreen = true;
                report.RenderersRebound++;
            }

            report.ItemsBaked++;
            return true;
        }

        // Equipment.GetAllSkinnedMeshRenderers: depth-first from the target, first name wins, items' own subtrees are skipped.
        private static void MapBones(Dictionary<string, Transform> map, Transform target)
        {
            if (!map.ContainsKey(target.name))
            {
                map.Add(target.name, target);
            }

            foreach (Transform child in target)
            {
                if (child.GetComponent<NHItem>() == null)
                {
                    MapBones(map, child);
                }
            }
        }

        // GetComponents<SkinnedMeshRenderer>() plus GetComponentsInChildren<SkinnedMeshRenderer>() as Start sees them: renderers on inactive
        // children are not fitted (they keep the copy's bones, so those bones are kept too).
        private static List<SkinnedMeshRenderer> RenderersStartWouldFit(Equipment equipment)
        {
            var result = new List<SkinnedMeshRenderer>(equipment.GetComponents<SkinnedMeshRenderer>());
            foreach (SkinnedMeshRenderer renderer in equipment.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.transform != equipment.transform && ActiveUpTo(renderer.transform, equipment.transform))
                {
                    result.Add(renderer);
                }
            }

            return result;
        }

        private static bool ActiveUpTo(Transform t, Transform stop)
        {
            for (Transform c = t; c != null && c != stop; c = c.parent)
            {
                if (!c.gameObject.activeSelf)
                {
                    return false;
                }
            }

            return true;
        }

        // Every transform something in the hierarchy still points at: skinned bones and root bones, and any object reference of any component.
        private static HashSet<Transform> UsedTransforms(GameObject root)
        {
            var used = new HashSet<Transform>();
            foreach (Component component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null || component is Transform || component is Equipment)
                {
                    continue;
                }

                if (component is SkinnedMeshRenderer skinned)
                {
                    foreach (Transform bone in skinned.bones)
                    {
                        if (bone != null)
                        {
                            used.Add(bone);
                        }
                    }

                    if (skinned.rootBone != null)
                    {
                        used.Add(skinned.rootBone);
                    }

                    continue;
                }

                var serialized = new SerializedObject(component);
                SerializedProperty property = serialized.GetIterator();
                while (property.Next(true))
                {
                    if (property.propertyType != SerializedPropertyType.ObjectReference)
                    {
                        continue;
                    }

                    Object value = property.objectReferenceValue;
                    if (value is Component referenced)
                    {
                        used.Add(referenced.transform);
                    }
                    else if (value is GameObject go)
                    {
                        used.Add(go.transform);
                    }
                }
            }

            return used;
        }

        // Deletes every subtree under the item that nothing uses and that holds nothing but transforms.
        private static void Prune(Transform item, HashSet<Transform> used, NHanceBakeReport report)
        {
            var children = new List<Transform>();
            foreach (Transform child in item)
            {
                children.Add(child);
            }

            foreach (Transform child in children)
            {
                if (IsNeeded(child, used))
                {
                    if (child.GetComponents<Component>().Length == 1)
                    {
                        report.TransformsKept++;
                    }

                    Prune(child, used, report);
                }
                else
                {
                    report.TransformsRemoved += child.GetComponentsInChildren<Transform>(true).Length;
                    Object.DestroyImmediate(child.gameObject, true);
                }
            }
        }

        private static bool IsNeeded(Transform t, HashSet<Transform> used)
        {
            foreach (Transform c in t.GetComponentsInChildren<Transform>(true))
            {
                if (used.Contains(c) || c.GetComponents<Component>().Length > 1)
                {
                    return true;
                }
            }

            return false;
        }

        private static IEnumerable<string> BasesFirst(IEnumerable<string> paths)
        {
            var list = paths.Distinct().ToList();
            int Depth(string path)
            {
                int depth = 0;
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                while (go != null && PrefabUtility.GetPrefabAssetType(go) == PrefabAssetType.Variant)
                {
                    go = PrefabUtility.GetCorrespondingObjectFromSource(go);
                    depth++;
                }

                return depth;
            }

            return list.OrderBy(Depth).ThenBy(p => p);
        }
    }
}
