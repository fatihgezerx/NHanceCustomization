#if HAS_VENEER
using System;
using Veneer;

namespace NHanceCustomization
{
    /// <summary>
    /// The design of the <see cref="NHanceLookData"/> Inspector that N-Hance Customization ships: the 3D preview under the header and
    /// three tabs (Items, Character, Random) with their cards, fields, notes and the system's own items (state pill, preview, base
    /// check, Style picker, Random and Clear, slot rows, Refresh items). With Veneer installed a project can copy it in the Inspector
    /// Designer and change the copy; this default is never saved as an asset.
    /// </summary>
    internal sealed class NHanceLookDataDefaultLayout : IVeneerDefaultLayoutProvider
    {
        private const string OptionalChanceTooltip =
            "How often Random fills a piece a character may go without: cape, gloves, belt, shoulders, helmet, beard...";

        private const string HelmetsTooltip =
            "Off: Random never gives a helmet, so the character keeps its hair (N-Hance hides the hair under a helmet).";

        /// <inheritdoc />
        public Type TargetType => typeof(NHanceLookData);

        /// <inheritdoc />
        public VeneerLayout CreateLayout()
        {
            // The header and the preview sit outside the tabs; their entries live in the first card (the engine draws them in their slots).
            VeneerLayout layout = new VeneerLayoutBuilder(typeof(NHanceLookData), "Character look", "Race, gender and outfit")
                .Page("Items", "layers")
                .Card("Randomize", "bolt")
                .Custom(NHanceLookDataItems.StateId)
                .Custom(NHanceLookDataItems.PreviewId)
                .Custom(NHanceLookDataItems.MismatchId)
                .Custom(NHanceLookDataItems.StyleId)
                .Custom(NHanceLookDataItems.ActionsId)
                .Card("Skin", "layers")
                .Custom(NHanceLookDataItems.SkinSlotsId)
                .Card("Head", "user")
                .Custom(NHanceLookDataItems.HeadSlotsId)
                .Card("Armor", "shield")
                .Custom(NHanceLookDataItems.ArmorSlotsId)
                .Page("Character", "user")
                .Card("Character", "user")
                .Field("Gender", NHanceLookDataItems.GenderLabel, NHanceLookDataItems.GenderTooltip)
                .Field("PreviewBase", NHanceLookDataItems.BaseLabel, NHanceLookDataItems.BaseTooltip)
                .Note("The look follows the character it is previewed on: choosing a Preview Base of another race or gender switches the look to it and empties its slots.")
                .Page("Random", "bolt")
                .Card("Random rules", "bolt")
                .Slider("OptionalChance", "Optional chance", "Never", "Always")
                .Field("RollHelmets", "Random helmets", HelmetsTooltip, null, VeneerWidget.Toggle)
                .Icon("shield")
                .Note("Random never rolls the Pants item nor the additional skins (BodyAdditional, HeadAdditional, HeadScars): pick those by hand.")
                .Card("Items in the project", "refresh")
                .Note("The Inspector finds the item prefabs once and keeps the list. Press Refresh items after you add, remove or fix item prefabs.")
                .Custom(NHanceLookDataItems.RefreshId)
                .Build();

            VeneerLayoutEntry chance = layout.FindEntry("OptionalChance", out _, out _);
            if (chance != null)
            {
                chance.Tooltip = OptionalChanceTooltip;
            }

            // The slots and the style are drawn by the slot rows and the Style picker; the old Inspector never showed them as fields.
            layout.HiddenPaths.Add("Slots");
            layout.HiddenPaths.Add("Style");

            // The look of the system (accent, style sheet). UseThemeLogo stays off on purpose: the Inspector's builder already carries the
            // brand and shows its logo in the header, and the renderer would put a second logo next to it.
            layout.ThemeBrand = NHanceBrand.Value.Name;
            return layout;
        }

        /// <inheritdoc />
        public void RegisterItems()
        {
            NHanceLookDataItems.Register();
        }
    }
}
#endif
