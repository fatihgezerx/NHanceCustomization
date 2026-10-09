using System;
using System.Reflection;
using NHance.Assets.Scripts;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace NHanceLook
{
    /// <summary>
    /// A small 3D view of a look: a copy of the look's preview character, dressed with it, in a private preview
    /// scene. Left or right drag turns it, the middle button pans, the wheel zooms towards the pointer, the buttons
    /// jump to the whole character, the head, the body or the legs. Nothing is added to the open scene.
    /// </summary>
    internal sealed class NHanceLookPreview : IDisposable
    {
        private const float Height = 380f;
        private const float DragSpeed = 0.8f;
        private const float Pitch = 8f;
        private const float FieldOfView = 30f;
        private const float MinZoom = 0.05f;
        private const float MaxZoom = 2.5f;
        private const float WheelStep = 1.06f;
        private const float ButtonStep = 1.25f;

        // Equipment fits an item's bones to the character in Start, which does not run outside play mode.
        private static readonly MethodInfo EquipmentStart =
            typeof(Equipment).GetMethod("Start", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

        // Not public API: it lets the preview leave the open scene as clean as it found it.
        private static readonly MethodInfo ClearSceneDirtiness =
            typeof(UnityEditor.SceneManagement.EditorSceneManager).GetMethod(
                "ClearSceneDirtiness", BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic, null, new[] { typeof(Scene) }, null);

        private PreviewRenderUtility _utility;
        private GameObject _instance;
        private Bounds _bounds;
        private float _baseDistance = 3f;
        private int _builtHash;
        private float _yaw = 160f;
        private float _zoom = 1f;
        private Vector3 _pan;

        /// <summary>Draws the preview in the Inspector; <paramref name="repaint"/> is called while it is moved.</summary>
        public void Draw(NHanceLookData look, Action repaint)
        {
            var rect = GUILayoutUtility.GetRect(10f, Height, GUILayout.ExpandWidth(true));
            var id = GUIUtility.GetControlID(FocusType.Passive);

            if (look.PreviewBase == null)
            {
                EditorGUI.HelpBox(rect, "Set a Preview Base: a character prefab with an NHAvatar set up for this race and gender.", MessageType.Info);
                Release();
                return;
            }

            var hash = HashOf(look);
            if (_instance == null || hash != _builtHash)
            {
                Rebuild(look, hash);
            }

            if (_instance == null)
            {
                return;
            }

            if (Event.current.type == EventType.Repaint)
            {
                Render(rect);
            }

            // The buttons are drawn over the picture and, being first in line, take their own clicks.
            DrawButtons(rect, repaint);
            HandleInput(rect, id, repaint);
        }

        public void Dispose()
        {
            Release();
            if (_utility != null)
            {
                _utility.Cleanup();
                _utility = null;
            }
        }

        private void DrawButtons(Rect rect, Action repaint)
        {
            const float w = 42f;
            const float h = 18f;
            var x = rect.x + 6f;
            var y = rect.y + 6f;

            if (GUI.Button(new Rect(x, y, w, h), new GUIContent("Full", "The whole character"), EditorStyles.miniButton))
            {
                View(0.5f, 1.15f);
                repaint?.Invoke();
            }

            if (GUI.Button(new Rect(x + w + 2f, y, w, h), new GUIContent("Head", "Zoom to the head"), EditorStyles.miniButton))
            {
                View(0.9f, 0.28f);
                repaint?.Invoke();
            }

            if (GUI.Button(new Rect(x + (w + 2f) * 2f, y, w, h), new GUIContent("Body", "Zoom to the chest and arms"), EditorStyles.miniButton))
            {
                View(0.68f, 0.5f);
                repaint?.Invoke();
            }

            if (GUI.Button(new Rect(x + (w + 2f) * 3f, y, w, h), new GUIContent("Legs", "Zoom to the legs and boots"), EditorStyles.miniButton))
            {
                View(0.22f, 0.45f);
                repaint?.Invoke();
            }

            var square = 22f;
            var right = rect.xMax - 6f - square;
            if (GUI.Button(new Rect(right - square - 2f, y, square, h), new GUIContent("+", "Zoom in"), EditorStyles.miniButton))
            {
                ZoomBy(1f / ButtonStep, Vector2.zero, 1f);
                repaint?.Invoke();
            }

            if (GUI.Button(new Rect(right, y, square, h), new GUIContent("-", "Zoom out"), EditorStyles.miniButton))
            {
                ZoomBy(ButtonStep, Vector2.zero, 1f);
                repaint?.Invoke();
            }
        }

        private void HandleInput(Rect rect, int id, Action repaint)
        {
            var e = Event.current;
            switch (e.GetTypeForControl(id))
            {
                case EventType.MouseDown:
                    if (rect.Contains(e.mousePosition) && e.button <= 2)
                    {
                        GUIUtility.hotControl = id;
                        e.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (GUIUtility.hotControl == id)
                    {
                        if (e.button == 2)
                        {
                            Pan(e.delta, rect);
                        }
                        else
                        {
                            _yaw -= e.delta.x * DragSpeed;
                        }

                        e.Use();
                        repaint?.Invoke();
                    }

                    break;
                case EventType.MouseUp:
                    if (GUIUtility.hotControl == id)
                    {
                        GUIUtility.hotControl = 0;
                        e.Use();
                    }

                    break;
                case EventType.ScrollWheel:
                    if (rect.Contains(e.mousePosition))
                    {
                        // The point under the pointer stays under it: the view zooms towards where the mouse is.
                        var ndc = new Vector2(
                            (e.mousePosition.x - rect.center.x) / (rect.width * 0.5f),
                            -(e.mousePosition.y - rect.center.y) / (rect.height * 0.5f));
                        ZoomBy(Mathf.Pow(WheelStep, e.delta.y), ndc, rect.width / rect.height);
                        e.Use();
                        repaint?.Invoke();
                    }

                    break;
            }
        }

        private Quaternion Rotation => Quaternion.Euler(Pitch, _yaw, 0f);

        private float Distance => _baseDistance * _zoom;

        // Multiplies the distance by factor; the point at ndc (-1..1 over the picture) stays where it is on the screen.
        private void ZoomBy(float factor, Vector2 ndc, float aspect)
        {
            var zoom = Mathf.Clamp(_zoom * factor, MinZoom, MaxZoom);
            var applied = zoom / _zoom;
            var halfHeight = Distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            var rotation = Rotation;
            _pan += (rotation * Vector3.right * (ndc.x * aspect) + rotation * Vector3.up * ndc.y) * (halfHeight * (1f - applied));
            _zoom = zoom;
            ClampPan();
        }

        // Moves the view with the mouse: what is under the pointer follows it.
        private void Pan(Vector2 delta, Rect rect)
        {
            var worldPerPixel = 2f * Distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) / rect.height;
            var rotation = Rotation;
            _pan += (-(rotation * Vector3.right) * delta.x + rotation * Vector3.up * delta.y) * worldPerPixel;
            ClampPan();
        }

        // Looks at the character from the height centerFraction (0 feet, 1 top of the head) so visibleFraction of its height fills the picture.
        private void View(float centerFraction, float visibleFraction)
        {
            var height = Mathf.Max(_bounds.size.y, 0.1f);
            _pan = new Vector3(0f, _bounds.min.y + height * centerFraction - _bounds.center.y, 0f);
            var visible = height * visibleFraction;
            _zoom = Mathf.Clamp(visible / (2f * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * _baseDistance), MinZoom, MaxZoom);
        }

        private void ClampPan()
        {
            var limit = _bounds.extents * 1.2f;
            _pan = new Vector3(
                Mathf.Clamp(_pan.x, -limit.x, limit.x),
                Mathf.Clamp(_pan.y, -limit.y, limit.y),
                Mathf.Clamp(_pan.z, -limit.z, limit.z));
        }

        private void Render(Rect rect)
        {
            var camera = _utility.camera;
            var rotation = Rotation;
            var center = _bounds.center + _pan;

            camera.transform.position = center + rotation * new Vector3(0f, 0f, -Distance);
            camera.transform.rotation = rotation;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = Distance + _bounds.extents.magnitude * 4f;
            camera.fieldOfView = FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.17f, 0.18f, 1f);

            _utility.lights[0].transform.rotation = Quaternion.Euler(35f, _yaw + 25f, 0f);
            _utility.lights[0].intensity = 1.3f;
            _utility.lights[1].transform.rotation = Quaternion.Euler(-20f, _yaw + 160f, 0f);
            _utility.lights[1].intensity = 0.7f;

            _utility.BeginPreview(rect, GUIStyle.none);
            _utility.Render(true);
            var texture = _utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }

        private void Rebuild(NHanceLookData look, int hash)
        {
            Release();
            _builtHash = hash;
            _utility ??= new PreviewRenderUtility();

            _instance = _utility.InstantiatePrefabInScene(look.PreviewBase.gameObject);
            var avatar = _instance.GetComponent<NHAvatar>();
            if (avatar == null)
            {
                avatar = _instance.GetComponentInChildren<NHAvatar>(true);
            }

            if (avatar != null)
            {
                // The avatar builds the items with PrefabUtility, which creates them in the open scene first;
                // leave that scene as clean as it was.
                var scene = SceneManager.GetActiveScene();
                var wasDirty = scene.isDirty;
                NHanceLookRules.Apply(avatar, look);
                if (!wasDirty && scene.isDirty)
                {
                    ClearSceneDirtiness?.Invoke(null, new object[] { scene });
                }

                FitEquipment();
            }

            var firstTime = _bounds.size == Vector3.zero;
            _bounds = MeasureBounds();
            _baseDistance = Mathf.Max(_bounds.extents.magnitude, 0.5f) * 2.6f;
            if (firstTime)
            {
                View(0.5f, 1.15f);
            }
        }

        private void FitEquipment()
        {
            if (EquipmentStart == null)
            {
                return;
            }

            foreach (var equipment in _instance.GetComponentsInChildren<Equipment>(true))
            {
                EquipmentStart.Invoke(equipment, null);
            }
        }

        private Bounds MeasureBounds()
        {
            var bounds = new Bounds(_instance.transform.position + Vector3.up, Vector3.one);
            var first = true;
            foreach (var renderer in _instance.GetComponentsInChildren<Renderer>())
            {
                if (!renderer.enabled)
                {
                    continue;
                }

                if (first)
                {
                    bounds = renderer.bounds;
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private void Release()
        {
            if (_instance != null)
            {
                UnityEngine.Object.DestroyImmediate(_instance);
                _instance = null;
            }
        }

        // Changes whenever something the preview shows changes.
        private static int HashOf(NHanceLookData look)
        {
            unchecked
            {
                var hash = (int)look.Gender * 397 ^ look.PreviewBase.GetInstanceID();
                for (var i = 0; i < look.Slots.Count; i++)
                {
                    var slot = look.Slots[i];
                    hash = hash * 31 + (int)slot.Type;
                    hash = hash * 31 + (slot.Item != null ? slot.Item.GetInstanceID() : 0);
                }

                return hash;
            }
        }
    }
}
