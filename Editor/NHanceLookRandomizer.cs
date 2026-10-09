using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using Random = UnityEngine.Random;

namespace NHanceCustomization
{
    /// <summary>
    /// Fills the unlocked slots of a look with random items that fit its race and gender. The general slots (skin, hair,
    /// eyes...) are rolled one by one; the outfit (chest, boots, cape, gloves...) comes from ONE clothing style and ONE
    /// color, the way N-Hance's own presets are made, so the pieces always belong together.
    /// </summary>
    internal static class NHanceLookRandomizer
    {
        // General slots a character always has; every other general slot is filled by chance (NHanceLookData.OptionalChance).
        private static readonly HashSet<ItemTypeEnum> Required = new HashSet<ItemTypeEnum>
        {
            ItemTypeEnum.Skin, ItemTypeEnum.HeadSkin, ItemTypeEnum.Eyes, ItemTypeEnum.UnderwearSkin, ItemTypeEnum.Hair
        };

        // Never rolled. The Pants item hides the character's legs and brings a short mesh of its own, which leaves a gap
        // under a long chest piece; N-Hance's presets leave it out and put the trousers on the legs with the style's PantsSkin.
        // The additional skins (tattoos, scars) are a taste, picked by hand.
        private static readonly HashSet<ItemTypeEnum> NotRolled = new HashSet<ItemTypeEnum>
        {
            ItemTypeEnum.Pants, ItemTypeEnum.BodyAdditional, ItemTypeEnum.HeadAdditional, ItemTypeEnum.HeadScars
        };

        // Slots that take the color of the hair (Hu_F_Hair_01_Bd goes with Hu_F_Brows_Bd).
        private static readonly HashSet<ItemTypeEnum> FollowsHair = new HashSet<ItemTypeEnum>
        {
            ItemTypeEnum.Beard, ItemTypeEnum.Brows, ItemTypeEnum.FemBrows, ItemTypeEnum.BeardStyle, ItemTypeEnum.BrowStyle,
            ItemTypeEnum.OrF_Brows, ItemTypeEnum.DwM_Beard, ItemTypeEnum.DwM_Moustache, ItemTypeEnum.DwM_Brows, ItemTypeEnum.DwF_Brows
        };

        // A helmet is rarer than the other optional pieces: it hides the hair.
        private const float HelmetFactor = 0.5f;

        // Skin, head skin and underwear are numbered the same way (Hu_F_Body_01, Hu_F_Head_01_A, Hu_F_Cloth_01).
        private static readonly Regex ToneToken = new Regex(@"_(\d\d)(?:_|$)", RegexOptions.Compiled);

        /// <summary>Rolls every slot that is not locked.</summary>
        public static void Roll(NHanceLookData look)
        {
            RollGeneral(look);
            RollOutfit(look);

            var hairLocked = look.Find(ItemTypeEnum.Hair)?.Locked == true;
            ResolveHeadgear(look, hairLocked ? ItemTypeEnum.Hair : ItemTypeEnum.Helmet);
            WarnMissing(look);
        }

        /// <summary>Rolls one slot again, always with an item when the project has one that fits.</summary>
        public static void RollSlot(NHanceLookData look, ItemTypeEnum type)
        {
            if (NHanceItemCatalog.IsOutfit(type))
            {
                // Stay with the outfit the look already has.
                var info = look.Get(ItemTypeEnum.Chest) != null ? NHanceItemCatalog.Parse(look.Get(ItemTypeEnum.Chest).name) : default;
                var style = !string.IsNullOrEmpty(look.Style) ? look.Style : info.Style;
                look.Set(type, PickPiece(look, type, style, info.Color));
            }
            else
            {
                look.Set(type, PickGeneral(look, type, Tone(look.Get(ItemTypeEnum.Skin)), Color(look.Get(ItemTypeEnum.Hair)), true));
            }

            if (type == ItemTypeEnum.Hair || type == ItemTypeEnum.Helmet)
            {
                ResolveHeadgear(look, type);
            }
        }

        private static void RollGeneral(NHanceLookData look)
        {
            // Skin and hair first: the head skin, the underwear and the brows follow them.
            var types = NHanceLookRules.TypesFor(look.Gender)
                .Where(t => !NHanceItemCatalog.IsOutfit(t) && !NotRolled.Contains(t))
                .ToList();
            var order = new List<ItemTypeEnum>(types.Count);
            if (types.Contains(ItemTypeEnum.Skin)) order.Add(ItemTypeEnum.Skin);
            if (types.Contains(ItemTypeEnum.Hair)) order.Add(ItemTypeEnum.Hair);
            order.AddRange(types.Where(t => t != ItemTypeEnum.Skin && t != ItemTypeEnum.Hair));

            string tone = null;
            string hairColor = null;
            foreach (var type in order)
            {
                var slot = look.Find(type);
                if (slot == null || !slot.Locked)
                {
                    look.Set(type, PickGeneral(look, type, tone, hairColor, false));
                }

                if (type == ItemTypeEnum.Skin) tone = Tone(look.Get(type));
                if (type == ItemTypeEnum.Hair) hairColor = Color(look.Get(type));
            }
        }

        private static void RollOutfit(NHanceLookData look)
        {
            if (!ChooseOutfit(look, out var style, out var color))
            {
                // The project has no outfit of that style for this race and gender: no half outfit either.
                foreach (var type in NHanceItemCatalog.OutfitTypes)
                {
                    if (look.Find(type)?.Locked != true)
                    {
                        look.Set(type, null);
                    }
                }

                return;
            }

            foreach (var type in NHanceItemCatalog.OutfitTypes)
            {
                var slot = look.Find(type);
                if (slot != null && slot.Locked)
                {
                    continue;
                }

                var piece = PickPiece(look, type, style, color);
                if (piece != null && !IsWanted(look, type, style))
                {
                    piece = null;
                }

                look.Set(type, piece);
            }
        }

        // Which style and color the outfit is made of: a locked chest piece decides, else the look's Style (or a random one).
        private static bool ChooseOutfit(NHanceLookData look, out string style, out string color)
        {
            style = null;
            color = null;

            var chest = look.Find(ItemTypeEnum.Chest);
            if (chest != null && chest.Locked && chest.Item != null)
            {
                var info = NHanceItemCatalog.Parse(chest.Item.name);
                style = info.Style;
                color = info.Color;
                return true;
            }

            var chests = NHanceItemCatalog.For(ItemTypeEnum.Chest, look.Gender)
                .Where(i => NHanceItemCatalog.Parse(i.name).Style != null)
                .ToList();
            if (!string.IsNullOrEmpty(look.Style))
            {
                chests = chests.Where(i => NHanceItemCatalog.Parse(i.name).Style == look.Style).ToList();
            }

            if (chests.Count == 0)
            {
                return false;
            }

            // A style is picked among the styles, not among the pieces, so a style with many chest pieces is not favored.
            var styles = chests.Select(i => NHanceItemCatalog.Parse(i.name).Style).Distinct().ToList();
            var chosen = styles[Random.Range(0, styles.Count)];
            style = chosen;

            var colors = chests
                .Where(i => NHanceItemCatalog.Parse(i.name).Style == chosen)
                .Select(i => NHanceItemCatalog.Parse(i.name).Color)
                .Where(c => c != null)
                .Distinct()
                .ToList();
            color = colors.Count > 0 ? colors[Random.Range(0, colors.Count)] : null;
            return true;
        }

        // The chest and the boots are always worn; the style's skin overlays too; the rest by chance.
        private static bool IsWanted(NHanceLookData look, ItemTypeEnum type, string style)
        {
            switch (type)
            {
                case ItemTypeEnum.Chest:
                case ItemTypeEnum.Boots:
                case ItemTypeEnum.ChestSkin:
                case ItemTypeEnum.PantsSkin:
                    return true;
                case ItemTypeEnum.GlovesSkin:
                    // The overlay belongs to the gloves; a style without gloves wears it alone.
                    return look.Get(ItemTypeEnum.Gloves) != null || !HasPiece(look, ItemTypeEnum.Gloves, style);
                case ItemTypeEnum.Helmet:
                    // N-Hance takes the hair off a character that wears a helmet, so Random only gives one when asked to.
                    return look.RollHelmets && Random.value < look.OptionalChance * HelmetFactor;
                default:
                    return Random.value < look.OptionalChance;
            }
        }

        private static bool HasPiece(NHanceLookData look, ItemTypeEnum type, string style)
        {
            return NHanceItemCatalog.For(type, look.Gender).Any(i => InStyle(i, style));
        }

        // An item of the style, or one that belongs to no style.
        private static bool InStyle(NHItem item, string style)
        {
            var itemStyle = NHanceItemCatalog.Parse(item.name).Style;
            return itemStyle == null || style == null || itemStyle == style;
        }

        private static NHItem PickPiece(NHanceLookData look, ItemTypeEnum type, string style, string color)
        {
            var list = NHanceItemCatalog.For(type, look.Gender).Where(i => InStyle(i, style)).ToList();
            if (list.Count == 0)
            {
                return null;
            }

            if (color != null)
            {
                Narrow(ref list, i => NHanceItemCatalog.Parse(i.name).Color == color);
            }

            return list[Random.Range(0, list.Count)];
        }

        private static NHItem PickGeneral(NHanceLookData look, ItemTypeEnum type, string tone, string hairColor, bool force)
        {
            var list = NHanceItemCatalog.For(type, look.Gender);
            if (list.Count == 0)
            {
                return null;
            }

            if (!force && !Required.Contains(type) && Random.value >= look.OptionalChance)
            {
                return null;
            }

            if ((type == ItemTypeEnum.HeadSkin || type == ItemTypeEnum.UnderwearSkin) && tone != null)
            {
                Narrow(ref list, i => Tone(i) == tone);
            }
            else if (FollowsHair.Contains(type) && hairColor != null)
            {
                Narrow(ref list, i => Color(i) == hairColor);
            }

            return list[Random.Range(0, list.Count)];
        }

        // A required slot with no item in the project (the asset package may not include them for a race and gender) stays empty: say so.
        private static void WarnMissing(NHanceLookData look)
        {
            var missing = NHanceLookRules.TypesFor(look.Gender)
                .Where(t => Required.Contains(t) && look.Get(t) == null && NHanceItemCatalog.For(t, look.Gender).Count == 0)
                .ToList();
            if (missing.Count > 0)
            {
                UnityEngine.Debug.LogWarning(
                    $"[NHanceCustomization] '{look.name}' ({look.Gender}): the project has no {string.Join(", ", missing)} items for this race and gender, so those slots stay empty.", look);
            }
        }

        // Keeps only the matching items; when none match, every item stays.
        private static void Narrow(ref List<NHItem> list, System.Func<NHItem, bool> match)
        {
            var narrowed = list.Where(match).ToList();
            if (narrowed.Count > 0)
            {
                list = narrowed;
            }
        }

        // A helmet and hair exclude each other (the avatar drops the older one): keep one, so what is saved is what is shown.
        private static void ResolveHeadgear(NHanceLookData look, ItemTypeEnum keep)
        {
            if (look.Get(ItemTypeEnum.Hair) == null || look.Get(ItemTypeEnum.Helmet) == null)
            {
                return;
            }

            look.Set(keep == ItemTypeEnum.Hair ? ItemTypeEnum.Helmet : ItemTypeEnum.Hair, null);
        }

        private static string Tone(NHItem item)
        {
            if (item == null)
            {
                return null;
            }

            var match = ToneToken.Match(item.name);
            return match.Success ? match.Groups[1].Value : null;
        }

        private static string Color(NHItem item) => item == null ? null : NHanceItemCatalog.Parse(item.name).Color;
    }
}
