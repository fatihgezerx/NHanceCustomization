# Changelog

## [Unreleased]

### Editor
- The Character Look Inspector is now drawn from a Veneer default layout (`NHanceLookDataDefaultLayout`, items in `NHanceLookDataItems`),
  customisable in the Inspector Designer. Same tabs, cards and behaviour; the header title is the fixed text "Character look" (it was the
  asset name), the two hints under the Character and Random cards now sit inside those cards, and Undo / Redo changes are picked up by a
  200 ms poll instead of an Undo callback. The item picker is unchanged.
- New studio-look Character Look Inspector (Veneer): header card with an item-count / base-mismatch pill, the 3D preview
  (the existing IMGUI preview in an `IMGUIContainer`, always visible), and tabs **Items** (Style, Random, Clear, Skin / Head / Armor
  slot cards with item box, picker, roll, clear and lock), **Character** (Race & Gender, Preview Base) and **Random** (optional chance,
  random helmets, Refresh items). Undo, dirty flag, drag-and-drop of items and the reset rules of race / gender / preview base are unchanged.
  A multi-selection shows a hint instead of the editor (as before, only one look can be edited at a time).
- The item picker is now a `VeneerWindow` (title bar, brand, search field, size slider, count pill); its thumbnail grid stays an IMGUI area.
  It opens centred on the main window instead of at the mouse.
- Without the kit the previous IMGUI Inspector and picker are compiled instead (`#if !HAS_VENEER`); with it only the studio ones.
- New `Editor/Setup` (`NHanceCustomization.Setup`, `DependencyGuard`): detects the optional Veneer, sets `HAS_VENEER`
  and `HAS_NHANCE_CUSTOMIZATION`, and offers to download the kit.
- Brand: lime accent, `NHanceBrand` and `Editor/Brand/NHanceBrand.uss`.
