#if HAS_VENEER
using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Veneer;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace NHanceCustomization
{
    /// <summary>
    /// What the custom items and widgets of one open <see cref="NHanceLookData"/> Inspector share: the look, the 3D preview, the
    /// <see cref="Change"/> path (one Undo step, dirty flag, then every piece refreshes) and a poll that notices changes made behind the
    /// pieces' back (Undo, Redo, another window). One session lives per Inspector build; it is found through the builder with a weak
    /// table, so nothing is kept after the Inspector is gone.
    /// </summary>
    internal sealed class NHanceLookDataSession
    {
        private static readonly ConditionalWeakTable<VeneerInspectorBuilder, NHanceLookDataSession> Sessions =
            new ConditionalWeakTable<VeneerInspectorBuilder, NHanceLookDataSession>();

        private readonly SerializedObject serializedObject;
        private readonly VisualElement root;
        private readonly List<Action> refreshers = new List<Action>(8);

        private NHanceLookPreview preview;
        private IMGUIContainer previewView;
        private int signature;
        private bool firstTick = true;

        private NHanceLookDataSession(NHanceLookData look, SerializedObject serializedObject, VisualElement root)
        {
            Look = look;
            this.serializedObject = serializedObject;
            this.root = root;
            signature = Signature();
        }

        /// <summary>The look the Inspector edits (the first selected object).</summary>
        public NHanceLookData Look { get; }

        /// <summary>The session of the Inspector that is building <paramref name="context"/>'s item; made on first use. Null when the target is not a look.</summary>
        public static NHanceLookDataSession For(VeneerCustomItemContext context)
        {
            VeneerInspectorBuilder builder = context.Builder;
            if (Sessions.TryGetValue(builder, out NHanceLookDataSession existing))
            {
                return existing;
            }

            var look = context.SerializedObject.targetObject as NHanceLookData;
            if (look == null)
            {
                return null;
            }

            var session = new NHanceLookDataSession(look, context.SerializedObject, builder.Root);
            Sessions.Add(builder, session);
            builder.Root.RegisterCallback<DetachFromPanelEvent>(session.OnDetached);
            context.Poll(session.Tick);
            return session;
        }

        /// <summary>Adds a piece that redraws itself from the look; it is called after every change and on the first tick (the pieces are on a panel by then).</summary>
        public void AddRefresher(Action refresher)
        {
            refreshers.Add(refresher);
        }

        /// <summary>
        /// Every edit of the look goes through here: one Undo step, the dirty flag, then the rows and the preview follow. The change may
        /// come late (the picker window calls back), so the pieces are only refreshed while this Inspector is still on a panel.
        /// </summary>
        public void Change(string undoName, Action change)
        {
            if (Look == null)
            {
                return;
            }

            Undo.RecordObject(Look, undoName);
            change();
            EditorUtility.SetDirty(Look);
            if (root.panel != null)
            {
                RefreshAll();
            }
        }

        /// <summary>Re-reads the serialized data and redraws every piece.</summary>
        public void RefreshAll()
        {
            if (Look == null)
            {
                return;
            }

            serializedObject.Update();
            signature = Signature();
            for (int i = 0; i < refreshers.Count; i++)
            {
                refreshers[i]();
            }
        }

        /// <summary>Makes the 3D preview (an IMGUI container: render texture, drag, wheel and view buttons have no UI Toolkit equivalent).</summary>
        public VisualElement CreatePreview()
        {
            previewView = new IMGUIContainer(DrawPreview);
            previewView.AddToClassList("nh-preview");
            previewView.style.height = NHanceLookPreview.Height;
            return previewView;
        }

        private void DrawPreview()
        {
            if (Look == null)
            {
                return;
            }

            if (preview == null)
            {
                preview = new NHanceLookPreview();
            }

            preview.Draw(Look, RepaintPreview);
        }

        private void RepaintPreview()
        {
            if (previewView != null)
            {
                previewView.MarkDirtyRepaint();
            }
        }

        // The preview owns a render utility and a scene: release them when the Inspector leaves the panel (it is made again when drawn).
        private void OnDetached(DetachFromPanelEvent evt)
        {
            if (preview != null)
            {
                preview.Dispose();
                preview = null;
            }
        }

        // Undo, Redo or another window changed the look: the pieces redraw. Nothing is allocated while nothing changes.
        private void Tick()
        {
            if (Look == null)
            {
                return;
            }

            int current = Signature();
            if (firstTick || current != signature)
            {
                firstTick = false;
                RefreshAll();
            }
        }

        // What the pieces show: gender, style, preview base and every slot's type, item and lock.
        private int Signature()
        {
            unchecked
            {
                int hash = (int)Look.Gender;
                hash = hash * 397 + (Look.Style != null ? Look.Style.GetHashCode() : 0);
                hash = hash * 397 + (Look.PreviewBase != null ? Look.PreviewBase.GetInstanceID() : 0);
                hash = hash * 397 + Look.Slots.Count;
                for (int i = 0; i < Look.Slots.Count; i++)
                {
                    NHanceLookSlot slot = Look.Slots[i];
                    hash = hash * 397 + (int)slot.Type;
                    hash = hash * 397 + (slot.Item != null ? slot.Item.GetInstanceID() : 0);
                    hash = hash * 397 + (slot.Locked ? 1 : 0);
                }

                return hash;
            }
        }
    }
}
#endif
