#if HAS_VENEER
using System;
using System.Collections.Generic;
using System.Linq;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using Veneer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NHanceCustomization
{
    /// <summary>
    /// Picks an item of one slot from a grid of thumbnails, in the studio window shell (title bar, brand, toolbar). A normal window:
    /// it can be resized and docked, the thumbnails grow with a slider and the search box filters by name. A click chooses the item
    /// at once (the Inspector behind it shows the result), a double click or Escape closes the window. The thumbnail grid itself is
    /// still an <c>IMGUIContainer</c>: it draws only the rows in view and asks <c>AssetPreview</c> for pictures that arrive later,
    /// which a UI Toolkit list would have to virtualise by hand. This class replaces the plain IMGUI picker while the kit is installed.
    /// </summary>
    internal sealed class NHanceItemPicker : VeneerWindow
    {
        private const float Gap = 6f;
        private const float LabelHeight = 30f;
        private const float DefaultCell = 120f;

        private readonly Dictionary<NHItem, Texture> thumbnails = new Dictionary<NHItem, Texture>();
        private List<NHItem> items = new List<NHItem>();
        private List<NHItem> shown = new List<NHItem>();
        private NHItem current;
        private Action<NHItem> onPick;
        private string search = string.Empty;
        private string heading = "Select item";
        private float cell = DefaultCell;
        private Vector2 scroll;
        private bool placed;

        private IMGUIContainer grid;
        private Label countLabel;
        private VisualElement noMatch;
        private Label noMatchTitle;
        private Label noMatchText;

        private static GUIStyle cellLabel;

        /// <inheritdoc />
        protected override VeneerBrand Brand => NHanceBrand.Value;

        /// <inheritdoc />
        protected override string WindowTitle => heading;

        private static GUIStyle CellLabel => cellLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        /// <summary>Opens the picker for <paramref name="type"/>; <paramref name="onPick"/> is called with each item the user clicks.</summary>
        public static void Open(ItemTypeEnum type, Gender gender, NHItem current, Action<NHItem> onPick)
        {
            var window = GetWindow<NHanceItemPicker>(false, $"Select {type}");
            window.minSize = new Vector2(320f, 260f);
            window.heading = $"Select {type}";
            window.items = NHanceItemCatalog.For(type, gender, true);
            window.current = current;
            window.onPick = onPick;
            window.search = string.Empty;
            window.scroll = Vector2.zero;
            window.thumbnails.Clear();
            window.shown = window.items;
            AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(64, window.items.Count * 2));

            if (!window.placed)
            {
                window.placed = true;
                Rect main = EditorGUIUtility.GetMainWindowPosition();
                window.position = new Rect(main.center.x - 360f, main.center.y - 280f, 720f, 560f);
            }

            // Rebuild so the title bar and the toolbar show the new slot.
            window.CreateGUI();
            window.Show();
            window.Focus();
        }

        private void OnDisable() => onPick = null;

        /// <inheritdoc />
        protected override void BuildContent(VisualElement content)
        {
            if (onPick == null)
            {
                // After a script reload the window has lost whom it was picking for.
                ShowEmptyState("search", "Nothing to pick", "Press the magnifier next to a slot of a Character Look to choose an item.");
                return;
            }

            RootElement.focusable = true;
            RootElement.RegisterCallback<KeyDownEvent>(OnKeyDown, TrickleDown.TrickleDown);

            VisualElement bar = VeneerStyle.Toolbar();
            VisualElement searchField = VeneerStyle.SearchField(text =>
            {
                search = text ?? string.Empty;
                Filter();
            }, out TextField _, "Search items", Brand);
            searchField.style.flexGrow = 1f;
            bar.Add(searchField);

            countLabel = VeneerStyle.MutedLabel(string.Empty);
            bar.Add(countLabel);
            bar.Add(VeneerStyle.MutedLabel("Size"));
            var size = new Slider(64f, 240f) { value = cell };
            size.style.width = 110f;
            size.RegisterValueChangedCallback(evt =>
            {
                cell = evt.newValue;
                grid?.MarkDirtyRepaint();
            });
            bar.Add(size);
            content.Add(bar);

            noMatch = VeneerStyle.EmptyState("search", string.Empty, string.Empty, out noMatchTitle, out noMatchText, Brand);
            content.Add(noMatch);

            grid = new IMGUIContainer(DrawGrid);
            grid.style.flexGrow = 1f;
            content.Add(grid);

            Refresh();
        }

        private void OnKeyDown(KeyDownEvent evt)
        {
            if (evt.keyCode == KeyCode.Escape)
            {
                evt.StopPropagation();
                Close();
            }
        }

        private void Filter()
        {
            shown = string.IsNullOrWhiteSpace(search)
                ? items
                : items.Where(i => i.name.IndexOf(search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
            Refresh();
        }

        private void Refresh()
        {
            if (grid == null || countLabel == null)
            {
                return;
            }

            countLabel.text = $"{shown.Count} / {items.Count}";
            SetPill($"{shown.Count} items", shown.Count == 0 ? "idle" : "info");

            bool none = shown.Count == 0;
            VeneerStyle.SetDisplay(noMatch, none);
            VeneerStyle.SetDisplay(grid, !none);
            if (none)
            {
                noMatchTitle.text = items.Count == 0 ? "No items" : "No match";
                noMatchText.text = items.Count == 0
                    ? "The project has no items of this kind for this race and gender."
                    : "No item matches the search.";
            }

            grid.MarkDirtyRepaint();
        }

        private void DrawGrid()
        {
            if (grid == null || shown.Count == 0)
            {
                return;
            }

            Event e = Event.current;
            float viewHeight = grid.contentRect.height;
            float cellWidth = cell + Gap;
            float cellHeight = cell + LabelHeight + Gap;
            int columns = Mathf.Max(1, Mathf.FloorToInt((grid.contentRect.width - 18f) / cellWidth));
            int rows = Mathf.CeilToInt(shown.Count / (float)columns);

            scroll = GUILayout.BeginScrollView(scroll);
            Rect area = GUILayoutUtility.GetRect(columns * cellWidth, rows * cellHeight);

            // Only the rows in view are drawn and ask for a thumbnail.
            int firstRow = Mathf.Max(0, Mathf.FloorToInt(scroll.y / cellHeight));
            int lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((scroll.y + viewHeight) / cellHeight));
            bool loading = false;
            for (int row = firstRow; row <= lastRow; row++)
            {
                for (int column = 0; column < columns; column++)
                {
                    int index = row * columns + column;
                    if (index >= shown.Count)
                    {
                        break;
                    }

                    var rect = new Rect(area.x + column * cellWidth + Gap * 0.5f, area.y + row * cellHeight + Gap * 0.5f, cell, cell + LabelHeight);
                    loading |= DrawCell(rect, shown[index]);
                }
            }

            GUILayout.EndScrollView();

            // Thumbnails are made in the background: keep drawing until they are all there.
            if (loading)
            {
                grid.MarkDirtyRepaint();
            }

            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                e.Use();
                Close();
            }
        }

        // Returns true while the thumbnail is still being made.
        private bool DrawCell(Rect rect, NHItem item)
        {
            Event e = Event.current;
            bool selected = item == current;

            if (e.type == EventType.Repaint)
            {
                Color accent = Brand.Accent;
                if (selected)
                {
                    EditorGUI.DrawRect(rect, new Color(accent.r, accent.g, accent.b, 0.45f));
                }
                else if (rect.Contains(e.mousePosition))
                {
                    EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.08f));
                }
            }

            var picture = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.width - 4f);
            Texture texture = Thumbnail(item, out bool pending);
            if (texture != null && e.type == EventType.Repaint)
            {
                GUI.DrawTexture(picture, texture, ScaleMode.ScaleToFit, true);
            }

            var label = new Rect(rect.x, rect.y + rect.width, rect.width, LabelHeight);
            bool broken = NHanceItemCatalog.IsBroken(item);
            GUI.Label(
                label,
                new GUIContent(broken ? item.name + "\n(missing material)" : item.name, broken ? item.name + ": a material is missing in the project, the character would have holes" : item.name),
                CellLabel);

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                current = item;
                onPick?.Invoke(item);
                bool doubleClick = e.clickCount >= 2;
                e.Use();
                if (doubleClick)
                {
                    Close();
                    return false;
                }

                grid.MarkDirtyRepaint();
            }

            return pending;
        }

        // A mesh item shows the prefab; a skin, eyes or brows item (no mesh, only materials) shows its first material.
        private Texture Thumbnail(NHItem item, out bool pending)
        {
            pending = false;
            if (thumbnails.TryGetValue(item, out Texture cached))
            {
                return cached;
            }

            UnityEngine.Object subject = item.gameObject;
            if (item.GetComponentInChildren<Renderer>(true) == null)
            {
                Material material = item.Mappers.SelectMany(m => m.Materials).FirstOrDefault(m => m != null);
                if (material != null)
                {
                    subject = material;
                }
            }

            Texture2D preview = AssetPreview.GetAssetPreview(subject);
            if (preview == null)
            {
                pending = AssetPreview.IsLoadingAssetPreview(subject.GetInstanceID());
                return AssetPreview.GetMiniThumbnail(subject);
            }

            thumbnails[item] = preview;
            return preview;
        }
    }
}
#endif
