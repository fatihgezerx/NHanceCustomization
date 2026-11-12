#if HAS_VENEER
using System.Collections.Generic;
using Veneer;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>
    /// What the kit's <see cref="VeneerModelPreview"/> shows for a look: the dressed copy of its preview character
    /// (<see cref="NHanceLookScene"/>, the same code the IMGUI fallback uses), the four views Full, Head, Body and Legs, and the hint
    /// while the look has no Preview Base. The kit knows nothing of N-Hance; this class is the only bridge.
    /// </summary>
    internal sealed class NHanceLookPreviewSource : VeneerModelPreviewScene
    {
        // Same places as the IMGUI fallback: height of the point looked at (0 feet, 1 top of the head) and the part of the height shown.
        private static readonly VeneerModelFocus[] Views =
        {
            new VeneerModelFocus("Full", string.Empty, "The whole character", 0.5f, 1.15f),
            new VeneerModelFocus("Head", string.Empty, "Zoom to the head", 0.9f, 0.28f),
            new VeneerModelFocus("Body", string.Empty, "Zoom to the chest and arms", 0.68f, 0.5f),
            new VeneerModelFocus("Legs", string.Empty, "Zoom to the legs and boots", 0.22f, 0.45f),
        };

        private readonly NHanceLookData look;
        private readonly NHanceLookScene scene = new NHanceLookScene();

        /// <summary>Makes the source of <paramref name="look"/>; nothing is built until the control syncs it.</summary>
        public NHanceLookPreviewSource(NHanceLookData look)
        {
            this.look = look;
        }

        /// <inheritdoc />
        public override bool HasModel => scene.HasModel;

        /// <inheritdoc />
        public override string EmptyHint => look != null && look.PreviewBase == null ? NHanceLookScene.MissingBaseText : null;

        /// <inheritdoc />
        public override Bounds Bounds => scene.Bounds;

        /// <inheritdoc />
        public override IReadOnlyList<VeneerModelFocus> Focuses => Views;

        /// <inheritdoc />
        public override bool Sync()
        {
            // The utility is only needed (and created) once there is a character to put in its scene.
            if (look != null && look.PreviewBase != null)
            {
                return scene.Sync(Utility, look);
            }

            return scene.Sync(null, look);
        }

        /// <inheritdoc />
        protected override void ReleaseModel()
        {
            scene.Release();
        }
    }
}
#endif
