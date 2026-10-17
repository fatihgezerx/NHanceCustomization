#if HAS_GORE_LAB
using System.Collections.Generic;
using System.Linq;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using UnityEngine;

namespace NHanceCustomization
{
    // What the GoreLab's studio needs to show a character dressed with this look as it looks in the game: the skins on the naked body, the skin
    // materials on the clothes and the naked parts the clothes hide. The studio puts the clothes on by tying their bones to the skeleton only; the
    // rest is what NHAvatar.Compile does, repeated here from the avatar's public data (the avatar's own code is not changed).
    public sealed partial class NHanceLookData : GoreLab.IGoreLookPreview
    {
        /// <summary>Puts the look's skins on the naked body of <paramref name="character"/>, then does for every worn item what the avatar does when it is dressed.</summary>
        public void PreparePreview(GameObject character, IReadOnlyList<GameObject> items, ICollection<Renderer> hidden)
        {
            var avatar = character != null ? character.GetComponentInChildren<NHAvatar>(true) : null;
            if (avatar == null)
            {
                return;
            }

            // The skins (body, chest, gloves, pants, head, eyes...): set on the avatar, which gives them to the naked parts (nothing is instantiated).
            var skins = NHanceLookRules.SkinTypes(Gender);
            for (var i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Item != null && skins.Contains(Slots[i].Type))
                {
                    avatar.SetItem(Slots[i].Item);
                }
            }

            avatar.Clean();

            for (var i = 0; i < items.Count; i++)
            {
                var item = items[i] != null ? items[i].GetComponent<NHItem>() : null;
                if (item == null || IsSkin(item.Type))
                {
                    continue;
                }

                HideBodyParts(avatar, item, hidden);
                SkinTheItem(avatar, item, items[i]);
                AddMappedMaterials(avatar, item);
            }
        }

        private static bool IsSkin(ItemTypeEnum type)
        {
            return type == ItemTypeEnum.Skin || type == ItemTypeEnum.HeadSkin || type == ItemTypeEnum.Eyes || type == ItemTypeEnum.FemBrows;
        }

        // The naked parts an item replaces are not shown.
        private static void HideBodyParts(NHAvatar avatar, NHItem item, ICollection<Renderer> hidden)
        {
            var replaced = item.Wrappers.Where(w => w.Enabled).Select(w => w.Type).ToList();
            foreach (var part in avatar.PartsMap.Where(p => p.Target != null))
            {
                if (replaced.Contains(part.Type) && part.Renderer != null)
                {
                    hidden.Add(part.Renderer);
                }
            }
        }

        // An item that copies the skin gets the body (or head) skin materials in place of the plain skin materials its meshes were made with.
        private static void SkinTheItem(NHAvatar avatar, NHItem item, GameObject root)
        {
            if (!item.CopySkinMaterial)
            {
                return;
            }

            var body = BodyMaterials(avatar);
            if (body.Count == 0)
            {
                return;
            }

            foreach (var renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var current = renderer.sharedMaterials;
                if (current.Any(material => material != null && avatar.BodyMaterialsToReplace.Any(replace => replace != null && replace.name == material.name)))
                {
                    var result = new List<Material>(body);
                    result.AddRange(current.Where(material => material != null && !avatar.BodyMaterialsToReplace.Any(replace => replace != null && replace.name == material.name)));
                    renderer.sharedMaterials = result.ToArray();
                }

                current = renderer.sharedMaterials;
                if (current.Any(material => material != null && avatar.HeadMaterialsToReplace.Any(replace => replace != null && replace.name == material.name)))
                {
                    var result = new List<Material>(avatar.HeadMaterial);
                    result.AddRange(current.Where(material => material != null && !avatar.HeadMaterialsToReplace.Any(replace => replace != null && replace.name == material.name)));
                    renderer.sharedMaterials = result.ToArray();
                }
            }
        }

        // The materials an item lists for body parts are added after the materials those parts already have.
        private static void AddMappedMaterials(NHAvatar avatar, NHItem item)
        {
            foreach (var mapper in item.Mappers)
            {
                var types = mapper.Wrappers.Where(w => w.Enabled).Select(w => w.Type).ToList();
                if (types.Count == 0)
                {
                    continue;
                }

                foreach (var part in avatar.PartsMap.Where(p => p.Target != null))
                {
                    if (types.Contains(part.Type) && part.Renderer != null)
                    {
                        var materials = part.Renderer.sharedMaterials.ToList();
                        materials.AddRange(mapper.Materials);
                        part.Renderer.sharedMaterials = materials.Where(material => material != null).ToArray();
                    }
                }
            }
        }

        // The body skin materials of the avatar: the skin, then the additional, underwear, chest, gloves and pants skins (the avatar's own order).
        private static List<Material> BodyMaterials(NHAvatar avatar)
        {
            var result = new List<Material>();
            var skin = avatar.Items[ItemTypeEnum.Skin];
            result.AddRange(skin != null
                ? skin.Mappers.SelectMany(m => m.Materials).Where(m => m != null)
                : avatar.DefaultBodyMaterial.Where(m => m != null));

            var layers = new[] { ItemTypeEnum.BodyAdditional, ItemTypeEnum.UnderwearSkin, ItemTypeEnum.ChestSkin, ItemTypeEnum.GlovesSkin, ItemTypeEnum.PantsSkin };
            foreach (var type in layers)
            {
                var layer = avatar.Items[type];
                if (layer != null)
                {
                    result.AddRange(layer.Mappers.SelectMany(m => m.Materials).Where(m => m != null));
                }
            }

            return result;
        }
    }
}
#endif
