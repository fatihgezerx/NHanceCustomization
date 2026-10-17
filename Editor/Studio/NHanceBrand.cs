#if HAS_VENEER
using Veneer;
using UnityEngine;

namespace NHanceCustomization
{
    /// <summary>The N-Hance Customization brand for the Veneer look: lime accent, logo and banner from <c>Editor/Brand</c>.</summary>
    internal static class NHanceBrand
    {
        /// <summary>The accent class the brand style sheet answers to (the exact lime, instead of a kit preset).</summary>
        internal const string AccentClass = "nhance-accent";

        /// <summary>The cached brand instance; use it for every Inspector and window of the system.</summary>
        internal static readonly VeneerBrand Value = VeneerBrand.FromAssets(
            name: "N-Hance Customization",
            subtitle: "Character looks and outfits",
            accent: new Color32(0x7C, 0xB3, 0x42, 255),
            logoPath: "Assets/Scripts/NHanceCustomization/Editor/Brand/logo.png",
            bannerPath: "Assets/Scripts/NHanceCustomization/Editor/Brand/banner.png",
            iconFolder: null,
            styleSheetPath: "Assets/Scripts/NHanceCustomization/Editor/Brand/NHanceBrand.uss",
            accentClass: AccentClass);
    }
}
#endif
