KINGS OF SIEGE - HOME SCREEN PNG ASSETS

20 separate PNG assets plus the complete home-screen reference.
Generated as separate matching assets from the approved mockup; these are not original layered source files or pixel-exact slices.

Background: opaque portrait castle courtyard.
Characters: full-body blue king and circular profile avatar.
Icons: shop, cards, home, rankings, missions, rewards, coin, gem, settings, swords, blank level shield, gold league shield.
Panels: blank battle button, league panel, navy navigation/header bar, shortcut button, selected tab.

All assets except background use alpha transparency. Original canvas margins are preserved. Use asset_dimensions.json for sizes.

UNITY IMPLEMENTATION
Import UI PNGs as Sprite (2D and UI), alpha transparency enabled. Preserve aspect ratio. Set texture max sizes and compression for target devices; originals are high-resolution masters. Use a portrait Canvas Scaler with safe-area handling.
Layer order: background, king, panel backings, icons, live text. Anchor header to top, navigation to bottom, Battle button above navigation. The navy bar can serve as header and navigation backing; use a suitable 9-slice in Sprite Editor. Configure slice borders after inspecting sprite padding, not the full canvas edge.

TEXT MUST BE ADDED IN ENGINE
Tabaja; player level; currency totals; league name and division; trophies; Missions; Rewards; BATTLE; Ranked match; Shop; Cards; Home; Rankings. Use TextMeshPro so labels, localization and values remain editable. Font files are not included.
No battle deck belongs on this home screen. Cards tab opens deck management.

These are static raster assets, not 3D models or editable vector files.
