# Changelog

## [Unreleased]

### Bake
- New **Bake** (`NHanceBake`): every equipment item N-Hance builds carries its own copy of the skeleton and an `Equipment` component that moves the item's renderers onto the character's bones in `Start`, each time the character spawns. Baking does that move once in the editor, exactly like `Equipment` (same bone lookup, same `updateWhenOffscreen`), then deletes the skeleton copies and the component. Same look and animation, far fewer transforms, no work at spawn. Copy transforms that still hold a component or that something refers to are kept, and references are never redirected.
- *Tools > N-Hance Customization > Bake Selected Prefabs* bakes prefabs in place (same GUIDs; base prefabs before their variants; items inherited from a base prefab are left to that prefab). `NHanceBake.BakePrefabs(paths)` from code.
- *Bake to Prefab...* on a Character Look (Inspector context menu, or *Assets > N-Hance > Bake Look to Prefab...*): dresses a copy of the Preview Base, bakes it and saves a plain prefab without N-Hance components. `NHanceBake.BakeLookToPrefab(look, path)` from code.
- *Tools > N-Hance Customization > Create Look From Selected Character* (`NHanceLookFromCharacter`): writes an already dressed character down as a Character Look. It reads an `NHAvatar` when there is one; otherwise it finds the items by name and the skin slots (skin, head skin, eyes, brows) by their materials on the body.

### Setup
- "Not now" on the optional-dependency question (Veneer) is remembered per project across editor sessions instead of only for the current session. *Tools > N-Hance Customization > Install Optional Dependencies...* asks again on demand.

### Runtime
- `NHanceLookApplier` no longer needs Combat System: its `IFighterAppearance` part is compiled only with `HAS_COMBAT_SYSTEM` (set by Combat System itself). Without it the applier dresses the character in `Start`.

### Editor
- The Style picker of the Veneer Inspector is the kit's animated dropdown (`VeneerDropdown`) instead of Unity's popup menu; it refills when the gender or the item list changes. The IMGUI fallback keeps its popup.
- The Veneer Inspector's 3D preview is the kit's `VeneerModelPreview` (UI Toolkit): kit zoom in / zoom out / reset buttons and a segmented Full / Head / Body / Legs group, the camera glides to a view (Motion setting), keyboard control, redrawn only when something changes. New `NHanceLookPreviewSource` (the Veneer bridge). The scene code (character copy, dressing, equipment fit, bounds, rebuild hash, leaving the open scene clean) moved to `NHanceLookScene`, shared with the IMGUI fallback `NHanceLookPreview`, which is now compiled only without Veneer and gained a reset (R) button.
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
