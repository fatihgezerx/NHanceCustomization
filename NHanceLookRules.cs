using System.Collections.Generic;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Utils;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>What belongs to a look, and how a look is put on an N-Hance avatar.</summary>
    public static class NHanceLookRules
    {
        private static readonly ItemTypeEnum[] Types = BuildLookTypes();

        /// <summary>Every item type that is part of a look: all of them except the weapon ones (weapon, shield, quiver).</summary>
        public static IReadOnlyList<ItemTypeEnum> LookTypes => Types;

        /// <summary>Whether <paramref name="type"/> is part of a look (weapons are left to the combat loadout).</summary>
        public static bool IsLookType(ItemTypeEnum type)
        {
            for (var i = 0; i < Types.Length; i++)
            {
                if (Types[i] == type)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The name prefix of the items made for <paramref name="gender"/> ("Hu_M_..." for a human man).</summary>
        public static string PrefixFor(Gender gender)
        {
            switch (gender)
            {
                case Gender.HumanMale: return "Hu_M";
                case Gender.HumanFemale: return "Hu_F";
                case Gender.OrcMale: return "Or_M";
                case Gender.OrcFemale: return "Or_F";
                case Gender.DwarfMale: return "Dw_M";
                case Gender.DwarfFemale: return "Dw_F";
                default: return string.Empty;
            }
        }

        /// <summary>The skin slots a look of <paramref name="gender"/> has (skin, head skin, eyes, underwear...), in N-Hance's order.</summary>
        public static List<ItemTypeEnum> SkinTypes(Gender gender)
        {
            return new ItemTypeQuery()
                .WithCategory(ItemCategory.Skin)
                .WithGender(gender)
                .WithOrder(OrderType.Ascending)
                .Build();
        }

        /// <summary>The item slots of one N-Hance category (<see cref="ItemCategoryString.Head"/> or <see cref="ItemCategoryString.Armor"/>) that fit <paramref name="gender"/>.</summary>
        public static List<ItemTypeEnum> ItemTypes(Gender gender, string category)
        {
            return new ItemTypeQuery()
                .WithCategory(ItemCategory.Item)
                .WithFilter(category)
                .WithGender(gender)
                .WithOrder(OrderType.Ascending)
                .Build();
        }

        /// <summary>Every slot a look of <paramref name="gender"/> has: skin, then head, then armor.</summary>
        public static List<ItemTypeEnum> TypesFor(Gender gender)
        {
            var result = SkinTypes(gender);
            result.AddRange(ItemTypes(gender, ItemCategoryString.Head));
            result.AddRange(ItemTypes(gender, ItemCategoryString.Armor));
            return result;
        }

        /// <summary>
        /// Puts <paramref name="look"/> on <paramref name="avatar"/>: every look slot gets the look's item or is emptied,
        /// then the avatar is rebuilt. Safe to call again. Weapons the avatar holds are not touched.
        /// </summary>
        public static void Apply(NHAvatar avatar, NHanceLookData look)
        {
            if (avatar == null || look == null)
            {
                return;
            }

            if (avatar.Gender != look.Gender)
            {
                Debug.LogWarning($"[NHanceCustomization] '{look.name}' is a {look.Gender} look but '{avatar.name}' is set up as {avatar.Gender}.", avatar);
            }

            for (var i = 0; i < Types.Length; i++)
            {
                if (look.Get(Types[i]) == null)
                {
                    avatar.Items.Remove(Types[i]);
                }
            }

            for (var i = 0; i < look.Slots.Count; i++)
            {
                var slot = look.Slots[i];
                if (slot.Item != null && IsLookType(slot.Type))
                {
                    avatar.SetItem(slot.Item);
                }
            }

            avatar.Clean().Compile();
        }

        private static ItemTypeEnum[] BuildLookTypes()
        {
            var result = new List<ItemTypeEnum>();
            foreach (ItemTypeEnum type in System.Enum.GetValues(typeof(ItemTypeEnum)))
            {
                var descriptor = type.TypeDescriptor();
                if (descriptor != null && descriptor.Category == ItemCategoryString.Weapon)
                {
                    continue;
                }

                result.Add(type);
            }

            return result.ToArray();
        }
    }
}
