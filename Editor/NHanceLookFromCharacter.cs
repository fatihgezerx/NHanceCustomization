using System.Collections.Generic;
using System.IO;
using System.Linq;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>
    /// Writes down an already dressed character as a <see cref="NHanceLookData"/>, so it can be edited and baked again.
    /// <list type="bullet">
    /// <item>A character with an <see cref="NHAvatar"/>: its items are read as they are.</item>
    /// <item>A character without one (dressed earlier and saved as a plain prefab): every equipment item is found by name (N-Hance names an
    /// item after its prefab), and the skin slots that leave no object behind (skin, head skin, eyes, brows) are found by their materials on
    /// the body. Weapons are not part of a look.</item>
    /// </list>
    /// Items are looked up among the project's item prefabs, so they have to be in the project.
    /// </summary>
    public static class NHanceLookFromCharacter
    {
        /// <summary>
        /// A new, unsaved look describing <paramref name="character"/>. <paramref name="notes"/> lists what could not be decided
        /// (an item prefab that is missing from the project, a skin slot with no match).
        /// </summary>
        public static NHanceLookData Create(GameObject character, NHAvatar previewBase, out List<string> notes)
        {
            notes = new List<string>();
            var look = ScriptableObject.CreateInstance<NHanceLookData>();
            look.name = character.name;
#if UNITY_EDITOR
            look.PreviewBase = previewBase;
#endif

            NHAvatar avatar = character.GetComponentInChildren<NHAvatar>(true);
            if (avatar != null)
            {
                look.Gender = avatar.Gender;
                foreach (ItemTypeEnum type in NHanceLookRules.TypesFor(avatar.Gender))
                {
                    look.Set(type, avatar.Items[type]);
                }

                return look;
            }

            IReadOnlyList<NHItem> catalog = NHanceItemCatalog.All;
            var byName = new Dictionary<string, NHItem>();
            foreach (NHItem item in catalog)
            {
                if (item != null && !byName.ContainsKey(item.name))
                {
                    byName.Add(item.name, item);
                }
            }

            look.Gender = GuessGender(character, previewBase);
            List<ItemTypeEnum> slots = NHanceLookRules.TypesFor(look.Gender);

            // Equipment items: an object named after an item prefab.
            var itemRoots = new List<Transform>();
            foreach (Transform t in character.GetComponentsInChildren<Transform>(true))
            {
                if (t == character.transform || !byName.TryGetValue(t.name, out NHItem item) || !NHanceLookRules.IsLookType(item.Type))
                {
                    continue;
                }

                if (look.Get(item.Type) != null)
                {
                    notes.Add($"two items for {item.Type}: kept '{look.Get(item.Type).name}', ignored '{t.name}'");
                    continue;
                }

                look.Set(item.Type, item);
                itemRoots.Add(t);
            }

            // Skin slots without an object: the item whose materials are all on the body (the most of them when several fit).
            var bodyMaterials = new HashSet<Material>();
            foreach (Renderer renderer in character.GetComponentsInChildren<Renderer>(true))
            {
                if (itemRoots.Any(r => renderer.transform.IsChildOf(r)))
                {
                    continue;
                }

                foreach (Material material in renderer.sharedMaterials)
                {
                    if (material != null)
                    {
                        bodyMaterials.Add(material);
                    }
                }
            }

            string prefix = NHanceLookRules.PrefixFor(look.Gender);
            foreach (ItemTypeEnum type in NHanceLookRules.SkinTypes(look.Gender))
            {
                if (look.Get(type) != null)
                {
                    continue;
                }

                NHItem best = null;
                int bestCount = 0;
                foreach (NHItem candidate in catalog)
                {
                    if (candidate == null || candidate.Type != type || !FitsGender(candidate.name, prefix))
                    {
                        continue;
                    }

                    var materials = candidate.Mappers.SelectMany(m => m.Materials).Where(m => m != null).Distinct().ToList();
                    if (materials.Count > bestCount && materials.All(bodyMaterials.Contains))
                    {
                        best = candidate;
                        bestCount = materials.Count;
                    }
                }

                look.Set(type, best);
                if (best == null)
                {
                    notes.Add($"{type}: no item prefab matches the body's materials (left empty: N-Hance uses the default)");
                }
            }

            foreach (ItemTypeEnum type in slots)
            {
                if (look.Find(type) == null)
                {
                    look.Set(type, null);
                }
            }

            return look;
        }

        /// <summary><see cref="Create"/>, saved at <paramref name="assetPath"/> (replaced if it exists).</summary>
        public static NHanceLookData CreateAsset(GameObject character, NHAvatar previewBase, string assetPath, out List<string> notes)
        {
            NHanceLookData look = Create(character, previewBase, out notes);
            string folder = Path.GetDirectoryName(assetPath);
            if (!string.IsNullOrEmpty(folder) && !AssetDatabase.IsValidFolder(folder))
            {
                Directory.CreateDirectory(folder);
                AssetDatabase.Refresh();
            }

            if (AssetDatabase.LoadAssetAtPath<NHanceLookData>(assetPath) != null)
            {
                AssetDatabase.DeleteAsset(assetPath);
            }

            look.name = Path.GetFileNameWithoutExtension(assetPath);
            AssetDatabase.CreateAsset(look, assetPath);
            AssetDatabase.SaveAssets();
            return look;
        }

        [MenuItem("Tools/N-Hance Customization/Create Look From Selected Character")]
        private static void CreateFromSelection()
        {
            GameObject character = Selection.activeGameObject;
            string folder = AssetDatabase.GetAssetPath(character);
            folder = string.IsNullOrEmpty(folder) ? "Assets" : Path.GetDirectoryName(folder);
            string path = EditorUtility.SaveFilePanelInProject("Create Look", character.name + "Look", "asset", "Where to save the look", folder);
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            NHanceLookData look = CreateAsset(character, null, path, out List<string> notes);
            Debug.Log($"[NHanceCustomization] Look '{path}' created" + (notes.Count > 0 ? ":\n  " + string.Join("\n  ", notes) : "."), look);
            Selection.activeObject = look;
        }

        [MenuItem("Tools/N-Hance Customization/Create Look From Selected Character", true)]
        private static bool CanCreateFromSelection() => Selection.activeGameObject != null;

        private static bool FitsGender(string itemName, string prefix)
        {
            // "Hu_M_..." items fit one race and gender; names without a race prefix are shared.
            bool hasRace = itemName.Length > 4 && itemName[2] == '_' && itemName[4] == '_' && (itemName.StartsWith("Hu") || itemName.StartsWith("Or") || itemName.StartsWith("Dw"));
            return !hasRace || itemName.StartsWith(prefix + "_");
        }

        private static Gender GuessGender(GameObject character, NHAvatar previewBase)
        {
            if (previewBase != null)
            {
                return previewBase.Gender;
            }

            var votes = new Dictionary<Gender, int>();
            foreach (Gender gender in new[] { Gender.HumanMale, Gender.HumanFemale, Gender.OrcMale, Gender.OrcFemale, Gender.DwarfMale, Gender.DwarfFemale })
            {
                string prefix = NHanceLookRules.PrefixFor(gender) + "_";
                votes[gender] = character.GetComponentsInChildren<Transform>(true).Count(t => t.name.StartsWith(prefix));
                foreach (Animator animator in character.GetComponentsInChildren<Animator>(true))
                {
                    if (animator.avatar != null && animator.avatar.name.Contains(NHanceLookRules.PrefixFor(gender) + "_"))
                    {
                        votes[gender] += 100;
                    }
                }
            }

            return votes.OrderByDescending(v => v.Value).First().Key;
        }
    }
}
