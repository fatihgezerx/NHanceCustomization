#if HAS_VENEER
using Veneer;
using UnityEditor;

namespace NHanceCustomization
{
    /// <summary>
    /// The studio-look Inspector of <see cref="NHanceLookData"/> (replaces the old IMGUI one, which stays as the fallback while Veneer
    /// is missing), drawn from a Veneer layout: the project's own copy made in the Inspector Designer when there is one, otherwise
    /// <see cref="NHanceLookDataDefaultLayout"/> (the 3D preview under the header, then the tabs Items, Character and Random). The
    /// state pill, the preview, the Style picker, the slot rows and the buttons are custom items (<see cref="NHanceLookDataItems"/>).
    /// Only a selection of several looks is built here: a header, the state pill and a hint.
    /// </summary>
    [CustomEditor(typeof(NHanceLookData))]
    internal sealed class NHanceLookDataEditor : VeneerLayoutDrivenInspector<NHanceLookData>
    {
        /// <inheritdoc />
        protected override VeneerBrand Brand => NHanceBrand.Value;

        /// <inheritdoc />
        protected override bool BuildMultiSelection(VeneerInspectorBuilder b)
        {
            b.Header(Target.name, "Character look", null);
            b.StatePill(new NHanceLookDataStatus(Target).PillState);
            b.Hint("Select a single Character Look to edit it: the preview and the slot rows work on one asset.", "info");
            return true;
        }
    }
}
#endif
