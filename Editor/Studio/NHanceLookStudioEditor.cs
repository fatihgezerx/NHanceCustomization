#if HAS_VENEER
using System;
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
    /// The studio-look Inspector of <see cref="NHanceLookData"/> (replaces the old IMGUI one, which stays as the fallback while
    /// Veneer is missing). A header card with an item-count pill, the 3D preview (still the IMGUI
    /// <see cref="NHanceLookPreview"/>, hosted in an <c>IMGUIContainer</c>: render texture, drag, wheel and view buttons have no
    /// UI Toolkit equivalent) and three tabs: <b>Items</b> (Style, Random, Clear and one row per slot), <b>Character</b> (race and
    /// gender, preview base) and <b>Random</b> (optional chance, helmets, refresh items). Everything the look changes goes through
    /// <see cref="Change"/>: one Undo step, dirty flag, and a refresh of the rows and the preview.
    /// </summary>
    [CustomEditor(typeof(NHanceLookData))]
    internal sealed class NHanceLookDataEditor : VeneerInspector<NHanceLookData>
    {
        private const string AllStyles = "All";

        private NHanceLookPreview preview;
        private IMGUIContainer previewView;
        private VisualElement slotsHost;
        private Button styleButton;
        private Label styleLabel;
        private EnumField genderField;
        private ObjectField baseField;

        // Cached so the 200 ms pill and hint providers allocate nothing while nothing changes.
        private int cachedFilled = -1;
        private string cachedFilledText = string.Empty;
        private string cachedMismatch;
        private Gender cachedMismatchBase;
        private Gender cachedMismatchLook;

        /// <inheritdoc />
        protected override VeneerBrand Brand => NHanceBrand.Value;

        private void OnEnable() => preview = new NHanceLookPreview();

        private void OnDisable()
        {
            Undo.undoRedoPerformed -= OnUndoRedo;
            preview?.Dispose();
            preview = null;
        }

        /// <inheritdoc />
        protected override void Build(VeneerInspectorBuilder b)
        {
            b.Header(Target.name, "Character look", null);
            b.StatePill(PillState);

            if (TargetCount > 1)
            {
                b.Hint("Select a single Character Look to edit it: the preview and the slot rows work on one asset.", "info");
                return;
            }

            NHanceLookData look = Target;

            // Undo and Redo change the asset behind the rows' back: rebuild them. Only while the Inspector is on a panel.
            b.Root.RegisterCallback<AttachToPanelEvent>(_ =>
            {
                Undo.undoRedoPerformed -= OnUndoRedo;
                Undo.undoRedoPerformed += OnUndoRedo;
            });
            b.Root.RegisterCallback<DetachFromPanelEvent>(_ => Undo.undoRedoPerformed -= OnUndoRedo);

            b.Card("Preview", "eye", () =>
            {
                previewView = new IMGUIContainer(DrawPreview);
                previewView.AddToClassList("nh-preview");
                previewView.style.height = NHanceLookPreview.Height;
                b.Add(previewView);
            });

            b.ConditionalHint(MismatchText, "warn", true);

            VisualElement[] tabs = b.Tabs("NHanceCustomization.LookTab", ("Items", "layers"), ("Character", "user"), ("Random", "bolt"));

            using (b.Into(tabs[0]))
            {
                b.Card("Randomize", "bolt", () =>
                {
                    styleButton = VeneerStyle.Button(StyleText(look), "chevron-down",
                        "The clothing style Random builds the outfit from. All picks a style at random each time.", ShowStyleMenu, false, Brand);
                    styleButton.style.flexGrow = 1f;
                    styleLabel = styleButton.Q<Label>(className: "vn-button__label");
                    b.Add(VeneerStyle.FieldRow("Style", styleButton,
                        "The clothing style Random builds the outfit from. All picks a style at random each time.", out Label _));

                    using (b.Into(b.Row()))
                    {
                        Button random = b.Button("Random", "bolt", () => Change(look, "Randomize look", () => NHanceLookRandomizer.Roll(look)),
                            "Roll a new outfit from one style and one color. Locked slots are kept.", true);
                        random.style.flexGrow = 1f;
                        Button clear = b.Button("Clear", "trash", () => Change(look, "Clear look", () => ClearUnlocked(look)),
                            "Empty every slot that is not locked.");
                        clear.style.flexGrow = 1f;
                    }
                });

                slotsHost = b.Add(new VisualElement());
                RebuildSlots();
            }

            using (b.Into(tabs[1]))
            {
                b.Card("Character", "user", () =>
                {
                    genderField = new EnumField(look.Gender);
                    genderField.RegisterValueChangedCallback(OnGenderChanged);
                    b.Add(VeneerStyle.FieldRow("Race & Gender", genderField,
                        "Only items made for this race and gender are offered and rolled. Changing it empties the slots and the style.", out Label _));

                    baseField = new ObjectField { objectType = typeof(NHAvatar), allowSceneObjects = false };
                    baseField.SetValueWithoutNotify(look.PreviewBase);
                    baseField.RegisterValueChangedCallback(OnBaseChanged);
                    b.Add(VeneerStyle.FieldRow("Preview base", baseField,
                        "A character prefab with an NHAvatar set up for this race and gender; the preview dresses a copy of it.", out Label _));
                });
                b.Hint("The look follows the character it is previewed on: choosing a Preview Base of another race or gender switches the look to it and empties its slots.", "info");
            }

            using (b.Into(tabs[2]))
            {
                b.Card("Random rules", "bolt", () =>
                {
                    VisualElement chance = b.Slider("OptionalChance", "Optional chance", "Never", "Always");
                    chance.tooltip = "How often Random fills a piece a character may go without: cape, gloves, belt, shoulders, helmet, beard...";
                    VisualElement helmets = b.Chip("RollHelmets", "Random helmets", "shield");
                    helmets.tooltip = "Off: Random never gives a helmet, so the character keeps its hair (N-Hance hides the hair under a helmet).";
                });
                b.Hint("Random never rolls the Pants item nor the additional skins (BodyAdditional, HeadAdditional, HeadScars): pick those by hand.", "info");

                b.Card("Items in the project", "refresh", () =>
                {
                    b.Hint("The Inspector finds the item prefabs once and keeps the list. Press Refresh items after you add, remove or fix item prefabs.", "info");
                    b.Button("Refresh items", "refresh", () =>
                    {
                        NHanceItemCatalog.Refresh();
                        RefreshAll();
                    }, "Look the project up again for item prefabs");
                });
            }
        }

        // ------------------------------------------------------------------ state

        private (string text, string kind) PillState()
        {
            var look = target as NHanceLookData;
            if (look == null)
            {
                return (string.Empty, "idle");
            }

            if (look.PreviewBase != null && look.PreviewBase.Gender != look.Gender)
            {
                return ("Base mismatch", "warn");
            }

            int filled = 0;
            for (int i = 0; i < look.Slots.Count; i++)
            {
                if (look.Slots[i].Item != null)
                {
                    filled++;
                }
            }

            if (filled != cachedFilled)
            {
                cachedFilled = filled;
                cachedFilledText = filled == 1 ? "1 item" : filled + " items";
            }

            return (cachedFilledText, filled == 0 ? "idle" : "ok");
        }

        private string MismatchText()
        {
            var look = target as NHanceLookData;
            if (look == null || look.PreviewBase == null || look.PreviewBase.Gender == look.Gender)
            {
                return null;
            }

            Gender previewGender = look.PreviewBase.Gender;
            if (cachedMismatch == null || cachedMismatchBase != previewGender || cachedMismatchLook != look.Gender)
            {
                cachedMismatchBase = previewGender;
                cachedMismatchLook = look.Gender;
                cachedMismatch = $"The Preview Base is set up as {previewGender} but this look is {look.Gender}: its items will not fit the body. " +
                                 "Change Race & Gender, or use another Preview Base.";
            }

            return cachedMismatch;
        }

        private static string StyleText(NHanceLookData look) => string.IsNullOrEmpty(look.Style) ? AllStyles : look.Style;

        private void DrawPreview()
        {
            if (preview == null || target == null)
            {
                return;
            }

            preview.Draw((NHanceLookData)target, RepaintPreview);
        }

        private void RepaintPreview() => previewView?.MarkDirtyRepaint();

        private void OnUndoRedo()
        {
            if (this == null || target == null)
            {
                return;
            }

            serializedObject.Update();
            RefreshAll();
        }

        // ------------------------------------------------------------------ changes

        // Every edit of the look: one Undo step, the dirty flag, then the rows and the preview follow. The change may come late
        // (the picker window calls back), so the look is passed in and the UI is only refreshed while this Inspector still shows it.
        private void Change(NHanceLookData look, string undoName, Action change)
        {
            Undo.RecordObject(look, undoName);
            change();
            EditorUtility.SetDirty(look);

            if (this == null || target != look)
            {
                return;
            }

            serializedObject.Update();
            RefreshAll();
        }

        private void RefreshAll()
        {
            if (slotsHost == null)
            {
                return;
            }

            var look = (NHanceLookData)target;
            RebuildSlots();
            if (styleLabel != null)
            {
                styleLabel.text = StyleText(look);
            }

            genderField?.SetValueWithoutNotify(look.Gender);
            baseField?.SetValueWithoutNotify(look.PreviewBase);
            previewView?.MarkDirtyRepaint();
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

        private void OnGenderChanged(ChangeEvent<Enum> evt)
        {
            var look = (NHanceLookData)target;
            var gender = (Gender)evt.newValue;
            if (gender == look.Gender)
            {
                return;
            }

            // Items made for another race or gender do not fit: start over.
            Change(look, "Change race and gender", () =>
            {
                look.Gender = gender;
                look.Slots.Clear();
                look.Style = string.Empty;
            });
        }

        private void OnBaseChanged(ChangeEvent<UnityEngine.Object> evt)
        {
            var look = (NHanceLookData)target;
            var previewBase = evt.newValue as NHAvatar;

            // The look follows the character it is previewed on: items made for another race or gender do not fit its body.
            Change(look, "Change preview base", () =>
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

        private void ShowStyleMenu()
        {
            var look = (NHanceLookData)target;
            var menu = new GenericMenu();
            menu.AddItem(new GUIContent(AllStyles), string.IsNullOrEmpty(look.Style), () => SetStyle(look, string.Empty));
            menu.AddSeparator(string.Empty);
            foreach (string style in NHanceItemCatalog.StylesFor(look.Gender))
            {
                string chosen = style;
                menu.AddItem(new GUIContent(chosen), look.Style == chosen, () => SetStyle(look, chosen));
            }

            menu.DropDown(styleButton.worldBound);
        }

        private void SetStyle(NHanceLookData look, string style) => Change(look, "Change style", () => look.Style = style);

        // ------------------------------------------------------------------ slot rows

        private void RebuildSlots()
        {
            if (slotsHost == null)
            {
                return;
            }

            var look = (NHanceLookData)target;
            slotsHost.Clear();
            AddGroup(look, "Skin", "layers", NHanceLookRules.SkinTypes(look.Gender));
            AddGroup(look, "Head", "user", NHanceLookRules.ItemTypes(look.Gender, ItemCategoryString.Head));
            AddGroup(look, "Armor", "shield", NHanceLookRules.ItemTypes(look.Gender, ItemCategoryString.Armor));
        }

        private void AddGroup(NHanceLookData look, string title, string icon, System.Collections.Generic.List<ItemTypeEnum> types)
        {
            if (types.Count == 0)
            {
                return;
            }

            VisualElement card = VeneerStyle.Card(title, icon, Brand);
            foreach (ItemTypeEnum type in types)
            {
                card.Add(BuildSlotRow(look, type));
            }

            slotsHost.Add(card);
        }

        private VisualElement BuildSlotRow(NHanceLookData look, ItemTypeEnum type)
        {
            NHanceLookSlot slot = look.Find(type);
            NHItem item = slot?.Item;
            bool locked = slot != null && slot.Locked;

            var row = new VisualElement();
            row.AddToClassList("nh-slot");

            var label = new Label(type.ToString());
            label.AddToClassList("nh-slot__label");
            row.Add(label);

            row.Add(BuildItemBox(look, type, item));

            row.Add(VeneerStyle.IconButton("search", "Choose from a list of items", () => OpenPicker(look, type), Brand));
            row.Add(VeneerStyle.IconButton("refresh", "Roll this slot again",
                () => Change(look, "Roll look item", () => NHanceLookRandomizer.RollSlot(look, type)), Brand));
            row.Add(VeneerStyle.IconButton("trash", "Empty this slot",
                () => Change(look, "Clear look item", () => look.Set(type, null)), Brand));

            Button lockButton = VeneerStyle.IconButton(locked ? "lock" : "unlock",
                locked ? "Locked: Random keeps this slot. Click to unlock" : "Unlocked. Click to lock: Random keeps a locked slot",
                () => Change(look, "Lock look item", () =>
                {
                    NHanceLookSlot entry = look.Find(type);
                    if (entry == null)
                    {
                        look.Set(type, null);
                        entry = look.Find(type);
                    }

                    entry.Locked = !locked;
                }), Brand);
            lockButton.EnableInClassList("vn-icon-button--on", locked);
            row.Add(lockButton);
            return row;
        }

        // The chosen item as a flat box: a click shows the prefab in the Project window, and an item of this slot can be dropped on it.
        private VisualElement BuildItemBox(NHanceLookData look, ItemTypeEnum type, NHItem item)
        {
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
                Change(look, "Change look item", () => look.Set(type, candidate));
            });
            return box;
        }

        private void OpenPicker(NHanceLookData look, ItemTypeEnum type)
        {
            NHanceItemPicker.Open(type, look.Gender, look.Get(type),
                chosen => Change(look, "Change look item", () => look.Set(type, chosen)));
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
