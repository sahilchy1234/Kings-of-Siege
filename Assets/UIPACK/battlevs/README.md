# Kings of Siege — countdown UI assets

20 individual transparent PNG assets, plus the source atlas and pixel-coordinate manifest.
These are regenerated assets matching the approved concept, not pixel-exact extractions or vector artwork. Native asset sizes are approximately 300 px; avoid enlarging beyond intended UI size.

## Contents
- 2 framed knight avatars (red and blue)
- 5 league shields: Starter/wood, Silver, Gold, Legend, Champion
- 2 blank horizontal nameplates (silver/red and gold/blue)
- 2 blank level medallions (silver and gold)
- 5 white battle silhouettes
- 4 countdown graphics: 1, 2, 3, START!

## Assembly
Place the league shield overlapping the left end of a blank nameplate, and a level medallion overlapping its right end. Render the player name as live white bold text in the plate and the numeric level as live dark text in the medallion. Example: Marcos / 12 / Silver; Tabaja / 15 / Gold. Text is intentionally not baked into these assets.

Backgrounds and divider should be native UI shapes, requiring no image: coral red #FF4148 above, blue #1760FF below. Suggested diagonal endpoints: left 54% of screen height, right 46%. Add a thin white line and distribute white battle icons along it, leaving the center clear for the countdown.

The blank plates can use nine-slice scaling with corner/frame regions preserved. Keep aspect ratios of avatars, shields, medallions and icons. PNG padding is retained to protect soft edges; trim using your engine sprite editor if desired. Atlas coordinates are top-left origin in pixels (not a uniform grid).

Use alpha blending, no background removal. White icons are easiest to inspect against a colored surface. Configure textures as UI sprites and inspect at target phone resolution. No animation or particle effects are required. Start with individual PNGs; optionally pack into an engine atlas.

Only silver/red and gold/blue nameplates are included because those are the approved screen variants; all five standalone league shields are included. Extra plate colors can be authored by your developer if needed.
