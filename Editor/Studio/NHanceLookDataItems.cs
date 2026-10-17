#if HAS_VENEER
using System;
using System.Collections.Generic;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using Veneer;
using UnityEditor;
using UnityEditor.UIElements;
using UnityEngine;
using UnityEngine.UIElements;

namespace NHanceCustomization
{
    /// <summary>
    /// The pieces of the <see cref="NHanceLookData"/> Inspector that are not plain fields, registered with Veneer so the default layout
    /// (and a user's copy of it) can place them: the state pill, the 3D preview, the base-mismatch warning, the Style picker, the Random
    /// and Clear buttons, the slot rows of the Skin, Head and Armor groups and the Refresh items button, plus the widgets of Race &amp;
    /// Gender and Preview base (they empty the slots when the gender changes). They can be moved or hidden in the Inspector Designer,
    /// not changed. Every change of the look goes through <see cref="NHanceLookDataSession.Change"/>.
    /// </summary>
    internal static class NHanceLookDataItems
    {
        /// <summary>Id of the header pill (header badge).</summary>
        public const string StateId = "nhance.state";

        /// <summary>Id of the 3D preview card (after the header).</summary>
        public const string PreviewId = "nhance.preview";

        /// <summary>Id of the base-mismatch warning (after the header).</summary>
        public const string MismatchId = "nhance.mismatch";

        /// <summary>Id of the Style picker row (card item).</summary>
        public const string StyleId = "nhance.style";

        /// <summary>Id of the Random and Clear buttons (card item).</summary>
        public const string ActionsId = "nhance.actions";

        /// <summary>Id of the Skin slot rows (card item).</summary>
        public const string SkinSlotsId = "nhance.slots.skin";

        /// <summary>Id of the Head slot rows (card item).</summary>
        public const string HeadSlotsId = "nhance.slots.head";

        /// <summary>Id of the Armor slot rows (card item).</summary>
        public const string ArmorSlotsId = "nhance.slots.armor";

        /// <summary>Id of the Refresh items button (card item).</summary>
        public const string RefreshId = "nhance.refresh-items";

        /// <summary>Default label of the gender widget.</summary>
        public const string GenderLabel = "Race & Gender";

        /// <summary>Default tooltip of the gender widget.</summary>
        public const string GenderTooltip = "Only items made for this race and gender are offered and rolled. Changing it empties the slots and the style.";

        /// <summary>Default label of the preview base widget.</summary>
        public const string BaseLabel = "Preview base";

        /// <summary>Default tooltip of the preview base widget.</summary>
        public const string BaseTooltip = "A character prefab with an NHAvatar set up for this race and gender; the preview dresses a copy of it.";

        private const string AllStyles = "All";
        private const string StyleLabel = "Style";
        private const string StyleTooltip = "The clothing style Random builds the outfit from. All picks a style at random each time.";
        private const string CardClass = "vn-card";

        private const int SkinGroup = 0;
        private const int HeadGroup = 1;
        private const int ArmorGroup = 2;

        /// <summary>Registers the custom items and the widgets of <see cref="NHanceLookData"/>. Safe to call again (items are replaced).</summary>
        public static void Register()
        {
            Type type = typeof(NHanceLookData);

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StateId,
                Label = "State",
                Icon = "check-circle",
                Description = "A pill in the header: the number of items in the look, or Base mismatch.",
                Slot = VeneerCustomItemSlot.HeaderBadge,
                Build = BuildState,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = PreviewId,
                Label = "3D preview",
                Icon = "eye",
                Description = "The Preview card under the header: the look on a copy of the Preview Base character. Drag to turn, wheel to zoom.",
                Slot = VeneerCustomItemSlot.AfterHeader,
                Build = BuildPreview,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = MismatchId,
                Label = "Base mismatch check",
                Icon = "warning",
                Description = "A warning while the Preview Base is set up for another gender than the look.",
                Slot = VeneerCustomItemSlot.AfterHeader,
                Build = BuildMismatch,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = StyleId,
                Label = "Style",
                Icon = "layers",
                Description = "The clothing style Random builds the outfit from (a menu of the styles for the look's gender).",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildStyle,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = ActionsId,
                Label = "Random and Clear buttons",
                Icon = "bolt",
                Description = "Random rolls a new outfit (locked slots are kept); Clear empties every slot that is not locked.",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildActions,
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = SkinSlotsId,
                Label = "Skin slots",
                Icon = "layers",
                Description = "One row per skin slot: item box (drop or click), picker, roll, clear and lock.",
                Slot = VeneerCustomItemSlot.Item,
                Build = context => BuildSlots(context, SkinGroup),
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = HeadSlotsId,
                Label = "Head slots",
                Icon = "user",
                Description = "One row per head slot: item box (drop or click), picker, roll, clear and lock.",
                Slot = VeneerCustomItemSlot.Item,
                Build = context => BuildSlots(context, HeadGroup),
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = ArmorSlotsId,
                Label = "Armor slots",
                Icon = "shield",
                Description = "One row per armor slot: item box (drop or click), picker, roll, clear and lock.",
                Slot = VeneerCustomItemSlot.Item,
                Build = context => BuildSlots(context, ArmorGroup),
            });

            VeneerCustomItems.Register(type, new VeneerCustomItem
            {
                Id = RefreshId,
                Label = "Refresh items button",
                Icon = "refresh",
                Description = "Looks the project up again for item prefabs (the Inspector keeps the list until then).",
                Slot = VeneerCustomItemSlot.Item,
                Build = BuildRefresh,
            });

            VeneerCustomItems.RegisterWidget(type, "Gender", BuildGender);
            VeneerCustomItems.RegisterWidget(type, "PreviewBase", BuildPreviewBase);
        }

        // ------------------------------------------------------------------ header, preview, hints

        private static VisualElement BuildState(VeneerCustomItemContext context)
        {
            var status = new NHanceLookDataStatus(context.SerializedObject.targetObject as NHanceLookData);
            Label pill = VeneerStyle.Pill(string.Empty, "idle");
            Action refresh = () =>
            {
                (string text, string kind) state = status.PillState();
                VeneerStyle.SetPill(pill, state.kind, state.text);
            };

            // The first run of Poll is on the first tick: show the right state at once.
            refresh();
            context.Poll(refresh);
            return pill;
        }

        private static VisualElement BuildPreview(VeneerCustomItemContext context)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            VeneerInspectorBuilder b = context.Builder;
            VisualElement view = session.CreatePreview();
            session.AddRefresher(view.MarkDirtyRepaint);
            b.Card("Preview", "eye", () => b.Add(view));
            return null;
        }

        private static VisualElement BuildMismatch(VeneerCustomItemContext context)
        {
            var status = new NHanceLookDataStatus(context.SerializedObject.targetObject as NHanceLookData);
            context.Builder.ConditionalHint(status.MismatchText, "warn", true);
            return null;
        }

        // ------------------------------------------------------------------ style, buttons

        private static VisualElement BuildStyle(VeneerCustomItemContext context)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            string tooltip = TooltipOf(context, StyleTooltip);
            Button styleButton = null;
            styleButton = VeneerStyle.Button(StyleText(session.Look), "chevron-down", tooltip, () => ShowStyleMenu(session, styleButton), false, context.Brand);
            styleButton.style.flexGrow = 1f;
            Label styleText = styleButton.Q<Label>(className: "vn-button__label");
            session.AddRefresher(() =>
            {
                if (styleText != null)
                {
                    styleText.text = StyleText(session.Look);
                }
            });

            return VeneerStyle.FieldRow(LabelOf(context, StyleLabel), styleButton, tooltip, out Label _);
        }

        private static VisualElement BuildActions(VeneerCustomItemContext context)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            VeneerInspectorBuilder b = context.Builder;
            using (b.Into(b.Row()))
            {
                Button random = b.Button("Random", "bolt", () => session.Change("Randomize look", () => NHanceLookRandomizer.Roll(session.Look)),
                    "Roll a new outfit from one style and one color. Locked slots are kept.", true);
                random.style.flexGrow = 1f;
                Button clear = b.Button("Clear", "trash", () => session.Change("Clear look", () => ClearUnlocked(session.Look)),
                    "Empty every slot that is not locked.");
                clear.style.flexGrow = 1f;
            }

            return null;
        }

        private static VisualElement BuildRefresh(VeneerCustomItemContext context)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            context.Builder.Button("Refresh items", "refresh", () =>
            {
                NHanceItemCatalog.Refresh();
                session.RefreshAll();
            }, "Look the project up again for item prefabs");
            return null;
        }

        private static string StyleText(NHanceLookData look)
        {
            return string.IsNullOrEmpty(look.Style) ? AllStyles : look.Style;
        }

        private static void ClearUnlocked(NHanceLookData look)
        {
            foreach (NHanceLookSlot slot in look.Slots)
            {
                if (!slot.Locked)
                {
                    slot.Item = null;
                }
            }
        }

        private static void ShowStyleMenu(NHanceLookDataSession session, Button anchor)
        {
            NHanceLookData look = session.Look;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(AllStyles), string.IsNullOrEmpty(look.Style), () => SetStyle(session, string.Empty));
            menu.AddSeparator(string.Empty);
            foreach (string style in NHanceItemCatalog.StylesFor(look.Gender))
            {
                string chosen = style;
                menu.AddItem(new GUIContent(chosen), look.Style == chosen, () => SetStyle(session, chosen));
            }

            menu.DropDown(anchor.worldBound);
        }

        private static void SetStyle(NHanceLookDataSession session, string style)
        {
            session.Change("Change style", () => session.Look.Style = style);
        }

        // ------------------------------------------------------------------ gender and preview base widgets

        private static VisualElement BuildGender(VeneerCustomItemContext context, SerializedProperty property)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            var field = new EnumField(session.Look.Gender);
            field.RegisterValueChangedCallback(evt => OnGenderChanged(session, (Gender)evt.newValue));
            session.AddRefresher(() => field.SetValueWithoutNotify(session.Look.Gender));
            return VeneerStyle.FieldRow(LabelOf(context, GenderLabel), field, TooltipOf(context, GenderTooltip), out Label _);
        }

        private static VisualElement BuildPreviewBase(VeneerCustomItemContext context, SerializedProperty property)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            var field = new ObjectField { objectType = typeof(NHAvatar), allowSceneObjects = false };
            field.SetValueWithoutNotify(session.Look.PreviewBase);
            field.RegisterValueChangedCallback(evt => OnBaseChanged(session, evt.newValue as NHAvatar));
            session.AddRefresher(() => field.SetValueWithoutNotify(session.Look.PreviewBase));
            return VeneerStyle.FieldRow(LabelOf(context, BaseLabel), field, TooltipOf(context, BaseTooltip), out Label _);
        }

        private static void OnGenderChanged(NHanceLookDataSession session, Gender gender)
        {
            NHanceLookData look = session.Look;
            if (gender == look.Gender)
            {
                return;
            }

            // Items made for another race or gender do not fit: start over.
            session.Change("Change race and gender", () =>
            {
                look.Gender = gender;
                look.Slots.Clear();
                look.Style = string.Empty;
            });
        }

        private static void OnBaseChanged(NHanceLookDataSession session, NHAvatar previewBase)
        {
            NHanceLookData look = session.Look;

            // The look follows the character it is previewed on: items made for another race or gender do not fit its body.
            session.Change("Change preview base", () =>
            {
                look.PreviewBase = previewBase;
                if (previewBase != null && previewBase.Gender != look.Gender)
                {
                    look.Gender = previewBase.Gender;
                    look.Slots.Clear();
                    look.Style = string.Empty;
                }
            });
        }

        // The layout entry's own label or tooltip wins over the default text (a user's copy of the layout may retitle the row).
        private static string LabelOf(VeneerCustomItemContext context, string fallback)
        {
            return context.Entry != null && !string.IsNullOrEmpty(context.Entry.Label) ? context.Entry.Label : fallback;
        }

        private static string TooltipOf(VeneerCustomItemContext context, string fallback)
        {
            return context.Entry != null && !string.IsNullOrEmpty(context.Entry.Tooltip) ? context.Entry.Tooltip : fallback;
        }

        // ------------------------------------------------------------------ slot rows

        private static VisualElement BuildSlots(VeneerCustomItemContext context, int group)
        {
            NHanceLookDataSession session = NHanceLookDataSession.For(context);
            if (session == null)
            {
                return null;
            }

            var host = new VisualElement();
            VeneerBrand brand = context.Brand;
            bool hidCard = false;

            // The card of a group that has no slot for this gender is hidden (the host is on its card only after the first tick).
            Action rebuild = () =>
            {
                List<ItemTypeEnum> types = TypesOf(session.Look, group);
                host.Clear();
                foreach (ItemTypeEnum slotType in types)
                {
                    host.Add(BuildSlotRow(session, brand, slotType));
                }

                VisualElement card = CardOf(host);
                if (card != null && (types.Count == 0 || hidCard))
                {
                    VeneerStyle.SetDisplay(card, types.Count != 0);
                    hidCard = types.Count == 0;
                }
            };

            session.AddRefresher(rebuild);
            rebuild();
            return host;
        }

        // The card the element sits in; null while it is not parented yet.
        private static VisualElement CardOf(VisualElement element)
        {
            for (VisualElement parent = element.parent; parent != null; parent = parent.parent)
            {
                if (parent.ClassListContains(CardClass))
                {
                    return parent;
                }
            }

            return null;
        }

        private static List<ItemTypeEnum> TypesOf(NHanceLookData look, int group)
        {
            switch (group)
            {
                case SkinGroup:
                    return NHanceLookRules.SkinTypes(look.Gender);
                case HeadGroup:
                    return NHanceLookRules.ItemTypes(look.Gender, ItemCategoryString.Head);
                default:
                    return NHanceLookRules.ItemTypes(look.Gender, ItemCategoryString.Armor);
            }
        }

        private static VisualElement BuildSlotRow(NHanceLookDataSession session, VeneerBrand brand, ItemTypeEnum type)
        {
            NHanceLookData look = session.Look;
            NHanceLookSlot slot = look.Find(type);
            NHItem item = slot?.Item;
            bool locked = slot != null && slot.Locked;

            var row = new VisualElement();
            row.AddToClassList("nh-slot");

            var label = new Label(type.ToString());
            label.AddToClassList("nh-slot__label");
            row.Add(label);

            row.Add(BuildItemBox(session, type, item));

            row.Add(VeneerStyle.IconButton("search", "Choose from a list of items", () => OpenPicker(session, type), brand));
            row.Add(VeneerStyle.IconButton("refresh", "Roll this slot again",
                () => session.Change("Roll look item", () => NHanceLookRandomizer.RollSlot(look, type)), brand));
            row.Add(VeneerStyle.IconButton("trash", "Empty this slot",
                () => session.Change("Clear look item", () => look.Set(type, null)), brand));

            Button lockButton = VeneerStyle.IconButton(locked ? "lock" : "unlock",
                locked ? "Locked: Random keeps this slot. Click to unlock" : "Unlocked. Click to lock: Random keeps a locked slot",
                () => session.Change("Lock look item", () =>
                {
                    NHanceLookSlot entry = look.Find(type);
                    if (entry == null)
                    {
                        look.Set(type, null);
                        entry = look.Find(type);
                    }

                    entry.Locked = !locked;
                }), brand);
            lockButton.EnableInClassList("vn-icon-button--on", locked);
            row.Add(lockButton);
            return row;
        }

        // The chosen item as a flat box: a click shows the prefab in the Project window, and an item of this slot can be dropped on it.
        private static VisualElement BuildItemBox(NHanceLookDataSession session, ItemTypeEnum type, NHItem item)
        {
            NHanceLookData look = session.Look;
            var box = new VisualElement();
            box.AddToClassList("nh-item");

            var text = new Label();
            text.AddToClassList("nh-item__text");
            text.pickingMode = PickingMode.Ignore;
            box.Add(text);

            if (item == null)
            {
                text.text = "None";
                box.AddToClassList("nh-item--empty");
                box.tooltip = "Drop an item of this slot here, or use the magnifier.";
            }
            else
            {
                bool broken = NHanceItemCatalog.IsBroken(item);
                text.text = broken ? item.name + "   (a material is missing: holes in the character)" : item.name;
                box.tooltip = "Click to show the prefab in the Project window.";
                if (broken)
                {
                    box.AddToClassList("nh-item--broken");
                }
            }

            box.RegisterCallback<ClickEvent>(_ =>
            {
                NHItem current = look.Get(type);
                if (current != null)
                {
                    EditorGUIUtility.PingObject(current);
                }
            });

            box.RegisterCallback<DragUpdatedEvent>(evt =>
            {
                NHItem candidate = DraggedItem(type);
                DragAndDrop.visualMode = candidate != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                box.EnableInClassList("nh-item--drop", candidate != null);
                evt.StopPropagation();
            });
            box.RegisterCallback<DragLeaveEvent>(_ => box.RemoveFromClassList("nh-item--drop"));
            box.RegisterCallback<DragExitedEvent>(_ => box.RemoveFromClassList("nh-item--drop"));
            box.RegisterCallback<DragPerformEvent>(evt =>
            {
                box.RemoveFromClassList("nh-item--drop");
                NHItem candidate = DraggedItem(type);
                if (candidate == null)
                {
                    return;
                }

                DragAndDrop.AcceptDrag();
                evt.StopPropagation();
                session.Change("Change look item", () => look.Set(type, candidate));
            });
            return box;
        }

        private static void OpenPicker(NHanceLookDataSession session, ItemTypeEnum type)
        {
            NHanceLookData look = session.Look;
            NHanceItemPicker.Open(type, look.Gender, look.Get(type),
                chosen => session.Change("Change look item", () => look.Set(type, chosen)));
        }

        private static NHItem DraggedItem(ItemTypeEnum type)
        {
            foreach (UnityEngine.Object reference in DragAndDrop.objectReferences)
            {
                var candidate = reference as NHItem;
                if (candidate == null && reference is GameObject go)
                {
                    candidate = go.GetComponent<NHItem>();
                }

                if (candidate != null && candidate.Type == type)
                {
                    return candidate;
                }
            }

            return null;
        }
    }
}
#endif
