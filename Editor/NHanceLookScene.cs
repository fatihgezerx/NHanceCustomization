using System.Reflection;
using NHance.Assets.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NHanceCustomization
{
    /// <summary>
    /// The dressed copy of a look's preview character inside a preview scene, shared by both previews (the Veneer one and the IMGUI fallback):
    /// it builds the copy, dresses it with the look, fits the equipment, measures it, and rebuilds only when something the preview shows
    /// changed. It owns the character instance, not the <c>PreviewRenderUtility</c> (the preview passes its own). Nothing is added to the
    /// open scene, and the open scene is left as clean as it was found.
    /// </summary>
    internal sealed class NHanceLookScene
    {
        /// <summary>What a preview says while the look has no Preview Base.</summary>
        internal const string MissingBaseText = "Set a Preview Base: a character prefab with an NHAvatar set up for this race and gender.";

        // Equipment fits an item's bones to the character in Start, which does not run outside play mode.
        private static readonly MethodInfo EquipmentStart =
            typeof(Equipment).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Not public API: it lets the preview leave the open scene as clean as it found it.
        private static readonly MethodInfo ClearSceneDirtiness =
            typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod(
                "ClearSceneDirtiness", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);

        private static readonly object[] SceneArgument = new object[1];

        private GameObject instance;
        private int builtHash;
        private Bounds bounds;

        /// <summary>True while a dressed character exists.</summary>
        public bool HasModel => instance != null;

        /// <summary>World bounds of the character (of its enabled renderers).</summary>
        public Bounds Bounds => bounds;

        /// <summary>
        /// Brings the character up to date with <paramref name="look"/>: builds it in <paramref name="utility"/>'s scene when it is missing or
        /// when the look changed, removes it when the look has no Preview Base. Returns true when the character or its bounds changed.
        /// </summary>
        public bool Sync(PreviewRenderUtility utility, NHanceLookData look)
        {
            if (look == null || look.PreviewBase == null)
            {
                bool had = instance != null;
                Release();
                return had;
            }

            int hash = HashOf(look);
            if (instance != null && hash == builtHash)
            {
                return false;
            }

            Release();
            builtHash = hash;
            instance = utility.InstantiatePrefabInScene(look.PreviewBase.gameObject);
            var avatar = instance.GetComponent<NHAvatar>();
            if (avatar == null)
            {
                avatar = instance.GetComponentInChildren<NHAvatar>(true);
            }

            if (avatar != null)
            {
                // The avatar builds the items with PrefabUtility, which creates them in the open scene first;
                // leave that scene as clean as it was.
                Scene scene = SceneManager.GetActiveScene();
                bool wasDirty = scene.isDirty;
                NHanceLookRules.Apply(avatar, look);
                if (!wasDirty && scene.isDirty && ClearSceneDirtiness != null)
                {
                    SceneArgument[0] = scene;
                    ClearSceneDirtiness.Invoke(null, SceneArgument);
                }

                FitEquipment();
            }

            bounds = MeasureBounds();
            return true;
        }

        /// <summary>Destroys the character (the scene and the utility stay).</summary>
        public void Release()
        {
            if (instance != null)
            {
                Object.DestroyImmediate(instance);
                instance = null;
            }
        }

        private void FitEquipment()
        {
            if (EquipmentStart == null)
            {
                return;
            }

            foreach (Equipment equipment in instance.GetComponentsInChildren<Equipment>(true))
            {
                EquipmentStart.Invoke(equipment, null);
            }
        }

        private Bounds MeasureBounds()
        {
            var measured = new Bounds(instance.transform.position + Vector3.up, Vector3.one);
            bool first = true;
            foreach (Renderer renderer in instance.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (first)
                {
                    measured = renderer.bounds;
                    first = false;
                }
                else
                {
                    measured.Encapsulate(renderer.bounds);
                }
            }

            return measured;
        }

        // Changes whenever something the preview shows changes.
        private static int HashOf(NHanceLookData look)
        {
            unchecked
            {
                int hash = (int)look.Gender * 397 ^ look.PreviewBase.GetInstanceID();
                for (int i = 0; i < look.Slots.Count; i++)
                {
                    NHanceLookSlot slot = look.Slots[i];
                    hash = hash * 31 + (int)slot.Type;
                    hash = hash * 31 + (slot.Item != null ? slot.Item.GetInstanceID() : 0);
                }

                return hash;
            }
        }
    }
}
