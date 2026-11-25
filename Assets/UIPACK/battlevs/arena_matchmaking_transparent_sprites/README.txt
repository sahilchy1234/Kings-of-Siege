# Arena Matchmaking — Individually Cropped Transparent Sprites

This pack contains individually exported PNG sprite files cropped from the generated transparent sprite atlas.

## Contents
- Panels: ornate matchmaking panel, wide status panel, compact info panel.
- Banners: arena header, ranked ribbon, player ribbons, faction pennants.
- Frames: player/rival shields and portrait rings.
- Avatars: blue commander, red commander, rival silhouette.
- Buttons: cancel-search, gold, blue and dark button styles.
- Icons: crown, trophy, laurel, swords, settings, close, spinner, signal and bottom navigation icons.
- Backgrounds: castle arena and night search/radar strips.
- Effects: glow orbs, particles and radar rings.
- Source: original transparent atlas for reference.

## Unity import
1. Extract this ZIP into your Unity project's Assets folder, for example:
   Assets/Game/UI/Matchmaking/Sprites/
2. Select the PNG files in Unity.
3. Set Texture Type to `Sprite (2D and UI)`.
4. Set Sprite Mode to `Single`.
5. Enable Alpha Is Transparency.
6. Use `Image` components for UI elements.
7. Use 9-slicing for scalable panels and buttons where appropriate.
8. Use TextMeshPro for editable text such as player names, rank, timer and button labels.

## Important notes
- Each file is a separate PNG with an alpha channel.
- The source atlas is included for reference.
- Some generated artwork has decorative lettering embedded in the banner/button artwork; use TMP overlays or cover the embedded lettering if you need dynamic localization.
- These are raster sprites, not vector artwork or Unity prefabs. You will still need to assemble the Canvas hierarchy and configure anchors, layout groups and transitions.
