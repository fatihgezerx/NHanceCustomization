using System;
using System.Collections.Generic;
using System.Linq;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using NHance.Assets.Scripts.Utils;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>What an item prefab's name says: race, gender, clothing style and color ("Hu_F_Chest_Peasant_01_Rd").</summary>
    internal readonly struct NHanceItemInfo
    {
        /// <summary>"Hu", "Or" or "Dw"; null when the name carries none.</summary>
        public readonly string Race;

        /// <summary>'M', 'F' or '\0' when the name does not say (or says unisex).</summary>
        public readonly char Gender;

        /// <summary>The clothing style ("Barbarian", "Peasant"); null for an item that belongs to no style (hair, skin, eyes).</summary>
        public readonly string Style;

        /// <summary>The last part of the name ("Bl", "Rd", "Bk"); null when there is none.</summary>
        public readonly string Color;

        public NHanceItemInfo(string race, char gender, string style, string color)
        {
            Race = race;
            Gender = gender;
            Style = style;
            Color = color;
        }
    }

    /// <summary>
    /// The item prefabs of the project, found once and shared by the Inspector, the Random button and the item picker,
    /// and the naming convention of the N-Hance packs read off their names.
    /// </summary>
    internal static class NHanceItemCatalog
    {
        /// <summary>The slots that make up an outfit: they all come from one style. The rest of a look (skin, hair, eyes...) is general.</summary>
        public static readonly ItemTypeEnum[] OutfitTypes =
        {
            ItemTypeEnum.Chest, ItemTypeEnum.Shoulders, ItemTypeEnum.Cape, ItemTypeEnum.Belt, ItemTypeEnum.Gloves,
            ItemTypeEnum.Helmet, ItemTypeEnum.Boots, ItemTypeEnum.ChestSkin, ItemTypeEnum.GlovesSkin, ItemTypeEnum.PantsSkin
        };

        private static List<NHItem> _all;
        private static readonly Dictionary<NHItem, bool> Broken = new Dictionary<NHItem, bool>();

        /// <summary>Every item prefab of the project. Slow the first time: it is kept until <see cref="Refresh"/>.</summary>
        public static IReadOnlyList<NHItem> All => _all ??= UtilsAssets.LoadPrefabsContaining<NHItem>();

        /// <summary>Forgets the list, so the next use looks the project up again.</summary>
        public static void Refresh()
        {
            _all = null;
            Broken.Clear();
        }

        /// <summary>
        /// Whether a material of <paramref name="item"/> is missing (deleted, or never part of the package). Such an item
        /// leaves holes in the character - a skin without its material makes the arms and hands disappear - so Random never picks it.
        /// </summary>
        public static bool IsBroken(NHItem item)
        {
            if (item == null)
            {
                return false;
            }

            if (Broken.TryGetValue(item, out var broken))
            {
                return broken;
            }

            broken = item.Mappers.Any(m => m.Materials.Any(material => material == null))
                     || item.GetComponentsInChildren<Renderer>(true).Any(r => r.sharedMaterials.Any(material => material == null));
            Broken[item] = broken;
            return broken;
        }

        /// <summary>Whether <paramref name="type"/> is part of an outfit.</summary>
        public static bool IsOutfit(ItemTypeEnum type) => Array.IndexOf(OutfitTypes, type) >= 0;

        /// <summary>Reads the race, gender, style and color off an item name.</summary>
        public static NHanceItemInfo Parse(string name)
        {
            var tokens = name.Split('_');
            var start = 0;
            string race = null;
            var gender = '\0';
            if (tokens.Length >= 2 && IsRace(tokens[0]) && IsGender(tokens[1]))
            {
                race = tokens[0];
                gender = tokens[1][0];
                start = 2;
            }

            // After the slot word (Chest, Pants, GlovesSkin...) what is left is the style, numbers, a gender letter and a color.
            var rest = new List<string>();
            for (var i = start + 1; i < tokens.Length; i++)
            {
                var token = tokens[i];
                if (token.Length == 0 || IsNumber(token))
                {
                    continue;
                }

                if (token == "M" || token == "F")
                {
                    gender = token[0];
                    continue;
                }

                if (token == "U")
                {
                    continue;
                }

                rest.Add(token);
            }

            var color = rest.Count > 0 ? rest[rest.Count - 1] : null;
            var style = rest.Count >= 2 ? rest[0] : null;
            return new NHanceItemInfo(race, gender, style, color);
        }

        /// <summary>Whether <paramref name="item"/> was made for <paramref name="gender"/>'s race and gender.</summary>
        public static bool Fits(NHItem item, Gender gender)
        {
            var descriptor = item.Type.TypeDescriptor();
            var prefix = NHanceLookRules.PrefixFor(gender);
            if (descriptor != null && descriptor.IsCanBeFilteredByCharacterPrefix && !string.IsNullOrEmpty(prefix))
            {
                return item.name.StartsWith(prefix, StringComparison.OrdinalIgnoreCase);
            }

            // Overlays and eyes carry no prefix: they are shared, unless the name names a race or a gender.
            var info = Parse(item.name);
            if (info.Race != null && prefix.Length >= 2 && !string.Equals(info.Race, prefix.Substring(0, 2), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return info.Gender == '\0' || prefix.Length < 4 || info.Gender == prefix[3];
        }

        /// <summary>The items of <paramref name="type"/> that fit <paramref name="gender"/>, by name; the ones with a missing material only on request.</summary>
        public static List<NHItem> For(ItemTypeEnum type, Gender gender, bool includeBroken = false)
        {
            return All
                .Where(i => i != null && i.Type == type && Fits(i, gender) && (includeBroken || !IsBroken(i)))
                .OrderBy(i => i.name, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        /// <summary>The clothing styles the project has outfits of for <paramref name="gender"/>.</summary>
        public static List<string> StylesFor(Gender gender)
        {
            return OutfitTypes
                .SelectMany(t => For(t, gender))
                .Select(i => Parse(i.name).Style)
                .Where(s => s != null)
                .Distinct()
                .OrderBy(s => s, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        private static bool IsRace(string token) => token == "Hu" || token == "Or" || token == "Dw";

        private static bool IsGender(string token) => token == "M" || token == "F";

        private static bool IsNumber(string token)
        {
            for (var i = 0; i < token.Length; i++)
            {
                if (!char.IsDigit(token[i]))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
