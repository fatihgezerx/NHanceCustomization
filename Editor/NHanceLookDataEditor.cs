using System.Collections.Generic;
using NHance.Assets.Scripts;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace NHanceLook
{
    /// <summary>The Inspector of a look: race and gender, style, a 3D preview, the Random button and one row per slot.</summary>
    [CustomEditor(typeof(NHanceLookData))]
    internal sealed class NHanceLookDataEditor : Editor
    {
        // Every button of a slot row has the same size, as high as the item box.
        private const float RowHeight = 20f;
        private const float ButtonWidth = 24f;

        private NHanceLookPreview _preview;

        private void OnEnable() => _preview = new NHanceLookPreview();

        private void OnDisable()
        {
            _preview?.Dispose();
            _preview = null;
        }

        public override void OnInspectorGUI()
        {
            var look = (NHanceLookData)target;

            DrawSettings(look);
            _preview.Draw(look, Repaint);
            DrawButtons(look);

            DrawGroup(look, "Skin", NHanceLookRules.SkinTypes(look.Gender));
            DrawGroup(look, "Head", NHanceLookRules.ItemTypes(look.Gender, ItemCategoryString.Head));
            DrawGroup(look, "Armor", NHanceLookRules.ItemTypes(look.Gender, ItemCategoryString.Armor));
        }

        private void DrawSettings(NHanceLookData look)
        {
            EditorGUI.BeginChangeCheck();
            var gender = (Gender)EditorGUILayout.EnumPopup("Race & Gender", look.Gender);
            if (EditorGUI.EndChangeCheck() && gender != look.Gender)
            {
                // Items made for another race or gender do not fit: start over.
                Change(look, "Change race and gender", () =>
                {
                    look.Gender = gender;
                    look.Slots.Clear();
                    look.Style = string.Empty;
                });
            }

            EditorGUI.BeginChangeCheck();
            var previewBase = (NHAvatar)EditorGUILayout.ObjectField("Preview Base", look.PreviewBase, typeof(NHAvatar), false);
            if (EditorGUI.EndChangeCheck())
            {
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

            if (look.PreviewBase != null && look.PreviewBase.Gender != look.Gender)
            {
                EditorGUILayout.HelpBox(
                    $"The Preview Base is set up as {look.PreviewBase.Gender} but this look is {look.Gender}: its items will not fit the body. Change Race & Gender, or use another Preview Base.",
                    MessageType.Warning);
            }

            DrawStyle(look);

            EditorGUI.BeginChangeCheck();
            var chance = EditorGUILayout.Slider(new GUIContent("Optional Chance", "How often Random fills a piece a character may go without: cape, gloves, belt, shoulders, helmet, beard..."), look.OptionalChance, 0f, 1f);
            if (EditorGUI.EndChangeCheck())
            {
                Change(look, "Change optional chance", () => look.OptionalChance = chance);
            }

            EditorGUI.BeginChangeCheck();
            var helmets = EditorGUILayout.Toggle(new GUIContent("Random Helmets", "Off: Random never gives a helmet, so the character keeps its hair (N-Hance hides the hair under a helmet)."), look.RollHelmets);
            if (EditorGUI.EndChangeCheck())
            {
                Change(look, "Change random helmets", () => look.RollHelmets = helmets);
            }
        }

        private void DrawStyle(NHanceLookData look)
        {
            var rect = EditorGUILayout.GetControlRect();
            rect = EditorGUI.PrefixLabel(rect, new GUIContent("Style", "The clothing style Random builds the outfit from. All picks a style at random each time."));
            var label = string.IsNullOrEmpty(look.Style) ? "All" : look.Style;
            if (EditorGUI.DropdownButton(rect, new GUIContent(label), FocusType.Keyboard))
            {
                var menu = new GenericMenu();
                menu.AddItem(new GUIContent("All"), string.IsNullOrEmpty(look.Style), () => SetStyle(look, string.Empty));
                menu.AddSeparator(string.Empty);
                foreach (var style in NHanceItemCatalog.StylesFor(look.Gender))
                {
                    var chosen = style;
                    menu.AddItem(new GUIContent(chosen), look.Style == chosen, () => SetStyle(look, chosen));
                }

                menu.DropDown(rect);
            }
        }

        private void SetStyle(NHanceLookData look, string style)
        {
            Change(look, "Change style", () => look.Style = style);
            Repaint();
        }

        private void DrawButtons(NHanceLookData look)
        {
            EditorGUILayout.Space(4f);
            using (new EditorGUILayout.HorizontalScope())
            {
                if (GUILayout.Button("Random", GUILayout.Height(26f)))
                {
                    Change(look, "Randomize look", () => NHanceLookRandomizer.Roll(look));
                }

                if (GUILayout.Button("Clear", GUILayout.Height(26f)))
                {
                    Change(look, "Clear look", () =>
                    {
                        foreach (var slot in look.Slots)
                        {
                            if (!slot.Locked)
                            {
                                slot.Item = null;
                            }
                        }
                    });
                }

                if (GUILayout.Button(new GUIContent("Refresh Items", "Look the project up again for item prefabs"), GUILayout.Height(26f), GUILayout.Width(100f)))
                {
                    NHanceItemCatalog.Refresh();
                }
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawGroup(NHanceLookData look, string title, List<ItemTypeEnum> types)
        {
            if (types.Count == 0)
            {
                return;
            }

            EditorGUILayout.LabelField(title, EditorStyles.boldLabel);
            foreach (var type in types)
            {
                DrawSlot(look, type);
            }

            EditorGUILayout.Space(6f);
        }

        private void DrawSlot(NHanceLookData look, ItemTypeEnum type)
        {
            var slot = look.Find(type);
            var item = slot?.Item;
            var locked = slot != null && slot.Locked;

            using (new EditorGUILayout.HorizontalScope())
            {
                EditorGUILayout.LabelField(type.ToString(), GUILayout.Width(110f));

                var dropped = ItemField(item, type);
                if (dropped != null)
                {
                    Change(look, "Change look item", () => look.Set(type, dropped));
                }

                if (RowButton(NHanceLookIcons.Search))
                {
                    NHanceItemPicker.Open(type, look.Gender, item, chosen =>
                    {
                        Change(look, "Change look item", () => look.Set(type, chosen));
                        if (this != null)
                        {
                            Repaint();
                        }
                    });
                }

                if (RowButton(NHanceLookIcons.Reroll))
                {
                    Change(look, "Roll look item", () => NHanceLookRandomizer.RollSlot(look, type));
                }

                if (RowButton(NHanceLookIcons.Trash))
                {
                    Change(look, "Clear look item", () => look.Set(type, null));
                }

                EditorGUI.BeginChangeCheck();
                var lockNow = GUILayout.Toggle(
                    locked, locked ? NHanceLookIcons.Locked : NHanceLookIcons.Unlocked, GUI.skin.button,
                    GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight));
                if (EditorGUI.EndChangeCheck())
                {
                    Change(look, "Lock look item", () =>
                    {
                        var target = look.Find(type);
                        if (target == null)
                        {
                            look.Set(type, null);
                            target = look.Find(type);
                        }

                        target.Locked = lockNow;
                    });
                }
            }
        }

        // The chosen item as a flat box (no picker circle: the magnifier next to it opens the picker). A click shows the
        // prefab in the Project window, and an item of this slot can be dropped on it. Returns the dropped item, else null.
        private static bool RowButton(GUIContent content)
        {
            return GUILayout.Button(content, GUILayout.Width(ButtonWidth), GUILayout.Height(RowHeight));
        }

        private static NHItem ItemField(NHItem item, ItemTypeEnum type)
        {
            var rect = GUILayoutUtility.GetRect(0f, RowHeight, GUILayout.ExpandWidth(true));
            var e = Event.current;

            if (e.type == EventType.Repaint)
            {
                var previous = GUI.contentColor;
                if (item == null)
                {
                    GUI.contentColor = new Color(previous.r, previous.g, previous.b, 0.55f);
                }

                var broken = NHanceItemCatalog.IsBroken(item);
                if (broken)
                {
                    GUI.contentColor = new Color(1f, 0.55f, 0.35f);
                }

                var text = item == null ? "None" : broken ? item.name + "   (a material is missing: holes in the character)" : item.name;
                EditorStyles.objectField.Draw(rect, new GUIContent(text), false, false, false, false);
                GUI.contentColor = previous;
            }

            switch (e.type)
            {
                case EventType.MouseDown:
                    if (e.button == 0 && item != null && rect.Contains(e.mousePosition))
                    {
                        EditorGUIUtility.PingObject(item);
                        e.Use();
                    }

                    break;
                case EventType.DragUpdated:
                case EventType.DragPerform:
                    if (rect.Contains(e.mousePosition))
                    {
                        var candidate = DraggedItem(type);
                        DragAndDrop.visualMode = candidate != null ? DragAndDropVisualMode.Link : DragAndDropVisualMode.Rejected;
                        if (e.type == EventType.DragPerform && candidate != null)
                        {
                            DragAndDrop.AcceptDrag();
                            e.Use();
                            return candidate;
                        }

                        e.Use();
                    }

                    break;
            }

            return null;
        }

        private static NHItem DraggedItem(ItemTypeEnum type)
        {
            foreach (var reference in DragAndDrop.objectReferences)
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

        private static void Change(NHanceLookData look, string undoName, System.Action change)
        {
            Undo.RecordObject(look, undoName);
            change();
            EditorUtility.SetDirty(look);
        }
    }

    /// <summary>The small icons of the slot rows, taken from Unity's own set (falling back to a letter when one is missing).</summary>
    internal static class NHanceLookIcons
    {
        public static GUIContent Search => Icon("Choose from a list of items", "S", "d_Search Icon", "Search Icon");

        public static GUIContent Reroll => Icon("Roll this slot again", "R", "d_Refresh", "Refresh");

        public static GUIContent Trash => Icon("Empty this slot", "X", "d_TreeEditor.Trash", "TreeEditor.Trash");

        public static GUIContent Locked => Icon("Locked: Random keeps this slot. Click to unlock", "L", "d_LockIcon-On", "LockIcon-On");

        public static GUIContent Unlocked => Icon("Unlocked. Click to lock: Random keeps a locked slot", "U", "d_LockIcon", "LockIcon");

        private static GUIContent Icon(string tooltip, string fallback, params string[] names)
        {
            foreach (var name in names)
            {
                var texture = EditorGUIUtility.FindTexture(name);
                if (texture != null)
                {
                    return new GUIContent(texture, tooltip);
                }
            }

            return new GUIContent(fallback, tooltip);
        }
    }
}
