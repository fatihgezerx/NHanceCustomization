using System;
using System.Collections.Generic;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using UnityEngine;

namespace NHanceLook
{
    /// <summary>One choice of a look: which item sits in an item type slot (hair, chest, skin...).</summary>
    [Serializable]
    public class NHanceLookSlot
    {
        /// <summary>The N-Hance item type this choice is for.</summary>
        public ItemTypeEnum Type;

        /// <summary>The item prefab; null means nothing in this slot.</summary>
        public NHItem Item;

        /// <summary>Kept as it is by the Random button of the editor.</summary>
        public bool Locked;
    }

    /// <summary>
    /// How one N-Hance character looks: its race and gender and one item per slot. It only holds the choices;
    /// <see cref="NHanceLookApplier"/> puts them on a character. Weapons and shields are not part of a look.
    /// </summary>
    [CreateAssetMenu(fileName = "NewLook", menuName = "N-Hance/Character Look")]
    public sealed class NHanceLookData : ScriptableObject
    {
        /// <summary>Race and gender; only items that fit it are offered and rolled.</summary>
        public Gender Gender;

        /// <summary>The choices, one per item type at most.</summary>
        public List<NHanceLookSlot> Slots = new List<NHanceLookSlot>();

        /// <summary>The clothing style Random picks the outfit from ("Barbarian", "Peasant"...); empty = a random style each time.</summary>
        public string Style = string.Empty;

        /// <summary>Whether Random may give a helmet. Off by default: a helmet hides the hair, and a character should always have hair.</summary>
        public bool RollHelmets;

        /// <summary>The chance (0-1) that the Random button fills a slot that a character may go without (cape, gloves, beard...).</summary>
        [Range(0f, 1f)] public float OptionalChance = 0.4f;

#if UNITY_EDITOR
        /// <summary>A character prefab with an NHAvatar set up for this race and gender; the preview dresses a copy of it.</summary>
        public NHAvatar PreviewBase;
#endif

        /// <summary>The item in <paramref name="type"/>'s slot, or null.</summary>
        public NHItem Get(ItemTypeEnum type)
        {
            var slot = Find(type);
            return slot?.Item;
        }

        /// <summary>The slot of <paramref name="type"/>, or null when there is none.</summary>
        public NHanceLookSlot Find(ItemTypeEnum type)
        {
            for (var i = 0; i < Slots.Count; i++)
            {
                if (Slots[i].Type == type)
                {
                    return Slots[i];
                }
            }

            return null;
        }

        /// <summary>Puts <paramref name="item"/> (null to empty it) in <paramref name="type"/>'s slot.</summary>
        public void Set(ItemTypeEnum type, NHItem item)
        {
            var slot = Find(type);
            if (slot == null)
            {
                slot = new NHanceLookSlot { Type = type };
                Slots.Add(slot);
            }

            slot.Item = item;
        }
    }
}
