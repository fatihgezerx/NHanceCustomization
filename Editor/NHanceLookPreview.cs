#if !HAS_VENEER
using System;
using UnityEditor;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>
    /// The IMGUI fallback 3D view of a look (used while Veneer is not installed; with it <see cref="NHanceLookPreviewSource"/> feeds the kit's
    /// own preview control): a copy of the look's preview character, dressed with it, in a private preview scene. Left or right drag turns
    /// it, the middle button pans, the wheel zooms towards the pointer, the + and - buttons zoom to the middle, the R button resets, and the
    /// buttons jump to the whole character, the head, the body or the legs. The scene itself is <see cref="NHanceLookScene"/>.
    /// </summary>
    internal sealed class NHanceLookPreview : IDisposable
    {
        internal const float Height = 380f;
        private const float DragSpeed = 0.8f;
        private const float Pitch = 8f;
        private const float InitialYaw = 160f;
        private const float FieldOfView = 30f;
        private const float MinZoom = 0.05f;
        private const float MaxZoom = 2.5f;
        private const float WheelStep = 1.06f;
        private const float ButtonStep = 1.25f;

        private readonly NHanceLookScene scene = new NHanceLookScene();

        private PreviewRenderUtility utility;
        private float baseDistance = 3f;
        private bool placed;
        private float yaw = InitialYaw;
        private float zoom = 1f;
        private Vector3 pan;

        private Bounds Bounds => scene.Bounds;

        /// <summary>Draws the preview in the Inspector; <paramref name="repaint"/> is called while it is moved.</summary>
        public void Draw(NHanceLookData look, Action repaint)
        {
            Rect rect = GUILayoutUtility.GetRect(10f, Height, GUILayout.ExpandWidth(true));
            int id = GUIUtility.GetControlID(FocusType.Passive);

            if (look.PreviewBase == null)
            {
                EditorGUI.HelpBox(rect, NHanceLookScene.MissingBaseText, MessageType.Info);
                scene.Release();
                return;
            }

            utility ??= new PreviewRenderUtility();
            if (scene.Sync(utility, look) && scene.HasModel)
            {
                Fit();
            }

            if (!scene.HasModel)
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
            scene.Release();
            if (utility != null)
            {
                utility.Cleanup();
                utility = null;
            }
        }

        private void DrawButtons(Rect rect, Action repaint)
        {
            const float w = 42f;
            const float h = 18f;
            float x = rect.x + 6f;
            float y = rect.y + 6f;

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

            const float square = 22f;
            float right = rect.xMax - 6f - square;
            if (GUI.Button(new Rect(right, y, square, h), new GUIContent("R", "Reset the view: turn and zoom back to the start"), EditorStyles.miniButton))
            {
                yaw = InitialYaw;
                View(0.5f, 1.15f);
                repaint?.Invoke();
            }

            if (GUI.Button(new Rect(right - square - 2f, y, square, h), new GUIContent("-", "Zoom out"), EditorStyles.miniButton))
            {
                ZoomBy(ButtonStep, Vector2.zero, 1f);
                repaint?.Invoke();
            }

            if (GUI.Button(new Rect(right - (square + 2f) * 2f, y, square, h), new GUIContent("+", "Zoom in"), EditorStyles.miniButton))
            {
                ZoomBy(1f / ButtonStep, Vector2.zero, 1f);
                repaint?.Invoke();
            }
        }

        private void HandleInput(Rect rect, int id, Action repaint)
        {
            Event e = Event.current;
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
                            yaw -= e.delta.x * DragSpeed;
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

        private Quaternion Rotation => Quaternion.Euler(Pitch, yaw, 0f);

        private float Distance => baseDistance * zoom;

        // Multiplies the distance by factor; the point at ndc (-1..1 over the picture) stays where it is on the screen.
        private void ZoomBy(float factor, Vector2 ndc, float aspect)
        {
            float next = Mathf.Clamp(zoom * factor, MinZoom, MaxZoom);
            float applied = next / zoom;
            float halfHeight = Distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad);
            Quaternion rotation = Rotation;
            pan += (rotation * Vector3.right * (ndc.x * aspect) + rotation * Vector3.up * ndc.y) * (halfHeight * (1f - applied));
            zoom = next;
            ClampPan();
        }

        // Moves the view with the mouse: what is under the pointer follows it.
        private void Pan(Vector2 delta, Rect rect)
        {
            float worldPerPixel = 2f * Distance * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) / rect.height;
            Quaternion rotation = Rotation;
            pan += (-(rotation * Vector3.right) * delta.x + rotation * Vector3.up * delta.y) * worldPerPixel;
            ClampPan();
        }

        // Looks at the character from the height centerFraction (0 feet, 1 top of the head) so visibleFraction of its height fills the picture.
        private void View(float centerFraction, float visibleFraction)
        {
            float height = Mathf.Max(Bounds.size.y, 0.1f);
            pan = new Vector3(0f, Bounds.min.y + height * centerFraction - Bounds.center.y, 0f);
            float visible = height * visibleFraction;
            zoom = Mathf.Clamp(visible / (2f * Mathf.Tan(FieldOfView * 0.5f * Mathf.Deg2Rad) * baseDistance), MinZoom, MaxZoom);
        }

        private void ClampPan()
        {
            Vector3 limit = Bounds.extents * 1.2f;
            pan = new Vector3(
                Mathf.Clamp(pan.x, -limit.x, limit.x),
                Mathf.Clamp(pan.y, -limit.y, limit.y),
                Mathf.Clamp(pan.z, -limit.z, limit.z));
        }

        // The character was (re)built: the distance follows its size; the first time the view shows all of it.
        private void Fit()
        {
            baseDistance = Mathf.Max(Bounds.extents.magnitude, 0.5f) * 2.6f;
            if (!placed)
            {
                placed = true;
                View(0.5f, 1.15f);
            }
        }

        private void Render(Rect rect)
        {
            Camera camera = utility.camera;
            Quaternion rotation = Rotation;
            Vector3 center = Bounds.center + pan;

            camera.transform.position = center + rotation * new Vector3(0f, 0f, -Distance);
            camera.transform.rotation = rotation;
            camera.nearClipPlane = 0.02f;
            camera.farClipPlane = Distance + Bounds.extents.magnitude * 4f;
            camera.fieldOfView = FieldOfView;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.17f, 0.17f, 0.18f, 1f);

            utility.lights[0].transform.rotation = Quaternion.Euler(35f, yaw + 25f, 0f);
            utility.lights[0].intensity = 1.3f;
            utility.lights[1].transform.rotation = Quaternion.Euler(-20f, yaw + 160f, 0f);
            utility.lights[1].intensity = 0.7f;

            utility.BeginPreview(rect, GUIStyle.none);
            utility.Render(true);
            Texture texture = utility.EndPreview();
            GUI.DrawTexture(rect, texture, ScaleMode.StretchToFill, false);
        }
    }
}
#endif
