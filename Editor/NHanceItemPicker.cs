using System;
using System.Collections.Generic;
using System.Linq;
using NHance.Assets.Scripts.Enums;
using NHance.Assets.Scripts.Items;
using UnityEditor;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>
    /// Picks an item of one slot from a grid of thumbnails. A normal window: it can be resized and docked, the
    /// thumbnails grow with a slider and the search box filters by name. A click chooses the item at once (the
    /// Inspector behind it shows the result), a double click or Escape closes the window.
    /// </summary>
    internal sealed class NHanceItemPicker : EditorWindow
    {
        private const float Gap = 6f;
        private const float LabelHeight = 30f;
        private const float DefaultCell = 120f;

        private readonly Dictionary<NHItem, Texture> _thumbnails = new Dictionary<NHItem, Texture>();
        private List<NHItem> _items = new List<NHItem>();
        private List<NHItem> _shown = new List<NHItem>();
        private NHItem _current;
        private Action<NHItem> _onPick;
        private string _search = string.Empty;
        private float _cell = DefaultCell;
        private Vector2 _scroll;
        private bool _placed;

        /// <summary>Opens the picker for <paramref name="type"/>; <paramref name="onPick"/> is called with each item the user clicks.</summary>
        public static void Open(ItemTypeEnum type, Gender gender, NHItem current, Action<NHItem> onPick)
        {
            var window = GetWindow<NHanceItemPicker>(false, $"Select {type}");
            window.minSize = new Vector2(320f, 260f);
            window._items = NHanceItemCatalog.For(type, gender, true);
            window._current = current;
            window._onPick = onPick;
            window._search = string.Empty;
            window._scroll = Vector2.zero;
            window._thumbnails.Clear();
            window.Filter();
            AssetPreview.SetPreviewTextureCacheSize(Mathf.Max(64, window._items.Count * 2));

            if (!window._placed)
            {
                window._placed = true;
                var mouse = GUIUtility.GUIToScreenPoint(Event.current != null ? Event.current.mousePosition : Vector2.zero);
                window.position = new Rect(mouse.x - 40f, mouse.y, 720f, 560f);
            }

            window.Show();
            window.Focus();
        }

        private void OnDisable() => _onPick = null;

        private void OnGUI()
        {
            DrawToolbar();
            DrawGrid();

            var e = Event.current;
            if (e.type == EventType.KeyDown && e.keyCode == KeyCode.Escape)
            {
                Close();
            }
        }

        private void DrawToolbar()
        {
            using (new EditorGUILayout.HorizontalScope(EditorStyles.toolbar))
            {
                EditorGUI.BeginChangeCheck();
                _search = GUILayout.TextField(_search, EditorStyles.toolbarSearchField, GUILayout.MinWidth(80f));
                if (EditorGUI.EndChangeCheck())
                {
                    Filter();
                }

                GUILayout.Label($"{_shown.Count} / {_items.Count}", EditorStyles.miniLabel, GUILayout.Width(64f));
                GUILayout.Label("Size", EditorStyles.miniLabel, GUILayout.Width(28f));
                _cell = GUILayout.HorizontalSlider(_cell, 64f, 240f, GUILayout.Width(110f));
            }
        }

        private void DrawGrid()
        {
            if (_shown.Count == 0)
            {
                EditorGUILayout.HelpBox(
                    _items.Count == 0 ? "The project has no items of this kind for this race and gender." : "No item matches the search.",
                    MessageType.Info);
                return;
            }

            var viewHeight = position.height - EditorStyles.toolbar.fixedHeight;
            var cellWidth = _cell + Gap;
            var cellHeight = _cell + LabelHeight + Gap;
            var columns = Mathf.Max(1, Mathf.FloorToInt((position.width - 18f) / cellWidth));
            var rows = Mathf.CeilToInt(_shown.Count / (float)columns);

            _scroll = GUILayout.BeginScrollView(_scroll);
            var area = GUILayoutUtility.GetRect(columns * cellWidth, rows * cellHeight);

            // Only the rows in view are drawn and ask for a thumbnail.
            var firstRow = Mathf.Max(0, Mathf.FloorToInt(_scroll.y / cellHeight));
            var lastRow = Mathf.Min(rows - 1, Mathf.FloorToInt((_scroll.y + viewHeight) / cellHeight));
            var loading = false;
            for (var row = firstRow; row <= lastRow; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var index = row * columns + column;
                    if (index >= _shown.Count)
                    {
                        break;
                    }

                    var rect = new Rect(area.x + column * cellWidth + Gap * 0.5f, area.y + row * cellHeight + Gap * 0.5f, _cell, _cell + LabelHeight);
                    loading |= DrawCell(rect, _shown[index]);
                }
            }

            GUILayout.EndScrollView();

            // Thumbnails are made in the background: keep drawing until they are all there.
            if (loading)
            {
                Repaint();
            }
        }

        // Returns true while the thumbnail is still being made.
        private bool DrawCell(Rect rect, NHItem item)
        {
            var e = Event.current;
            var selected = item == _current;

            if (e.type == EventType.Repaint)
            {
                if (selected)
                {
                    EditorGUI.DrawRect(rect, new Color(0.24f, 0.49f, 0.9f, 0.55f));
                }
                else if (rect.Contains(e.mousePosition))
                {
                    EditorGUI.DrawRect(rect, new Color(1f, 1f, 1f, 0.08f));
                }
            }

            var picture = new Rect(rect.x + 2f, rect.y + 2f, rect.width - 4f, rect.width - 4f);
            var texture = Thumbnail(item, out var pending);
            if (texture != null && e.type == EventType.Repaint)
            {
                GUI.DrawTexture(picture, texture, ScaleMode.ScaleToFit, true);
            }

            var label = new Rect(rect.x, rect.y + rect.width, rect.width, LabelHeight);
            var broken = NHanceItemCatalog.IsBroken(item);
            GUI.Label(
                label,
                new GUIContent(broken ? item.name + "\n(missing material)" : item.name, broken ? item.name + ": a material is missing in the project, the character would have holes" : item.name),
                CellLabel);

            if (e.type == EventType.MouseDown && e.button == 0 && rect.Contains(e.mousePosition))
            {
                _current = item;
                _onPick?.Invoke(item);
                if (e.clickCount >= 2)
                {
                    Close();
                }

                e.Use();
                Repaint();
            }

            return pending;
        }

        private static GUIStyle _cellLabel;

        private static GUIStyle CellLabel => _cellLabel ??= new GUIStyle(EditorStyles.miniLabel)
        {
            alignment = TextAnchor.UpperCenter,
            wordWrap = true,
            clipping = TextClipping.Clip
        };

        // A mesh item shows the prefab; a skin, eyes or brows item (no mesh, only materials) shows its first material.
        private Texture Thumbnail(NHItem item, out bool pending)
        {
            pending = false;
            if (_thumbnails.TryGetValue(item, out var cached))
            {
                return cached;
            }

            UnityEngine.Object subject = item.gameObject;
            if (item.GetComponentInChildren<Renderer>(true) == null)
            {
                var material = item.Mappers.SelectMany(m => m.Materials).FirstOrDefault(m => m != null);
                if (material != null)
                {
                    subject = material;
                }
            }

            var preview = AssetPreview.GetAssetPreview(subject);
            if (preview == null)
            {
                pending = AssetPreview.IsLoadingAssetPreview(subject.GetInstanceID());
                return AssetPreview.GetMiniThumbnail(subject);
            }

            _thumbnails[item] = preview;
            return preview;
        }

        private void Filter()
        {
            _shown = string.IsNullOrWhiteSpace(_search)
                ? _items
                : _items.Where(i => i.name.IndexOf(_search.Trim(), StringComparison.OrdinalIgnoreCase) >= 0).ToList();
        }
    }
}
