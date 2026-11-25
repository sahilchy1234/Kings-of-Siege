KINGS OF SIEGE — STORE PNG ELEMENTS

Separate PNG assets for the shop screen. No unit portraits or power cards.
The generic Cards navigation icon and empty card-size panel are retained.

Backgrounds: full-screen blurred blue background.
Panels: offer/card/coin panels and faded square variants.
Buttons: empty green buy button, plus button, plus glyph, down chevron.
Navigation: shop, cards, home, rankings icons; bottom bar and selected-tab panel.
Currency: single coin and gem.
Products: silver chest, gold chest, coin stack, coin bag.
Heading_Icons: daily-offers calendar.
Overlays: dark fade and divider.
Labels: static headings, product labels and navigation labels.
Numbers: digits, comma and price/balance examples from the reference.

All visual assets are PNG. Background is intentionally opaque; sprites/panels have alpha transparency. The panel variants have different opacity levels. Existing artwork may include a soft transparent glow.

Reference layout: 941 x 1672. Three columns around x=38,335,624. Offer/card/coin rows start around y=237,654,1130. Keep navigation fixed and content scrollable.
Unity: import as Sprite (2D and UI), enable alpha transparency. Use Sliced for panels/buttons (start with 32px panel borders, 18px button borders). Maintain aspect ratio for artwork. Use the background as a full-screen image. Layer icons and text above panels and buttons.
Labels/numbers are optional PNG recreations. Live TextMeshPro text is preferable for localization and changing prices. Typography and newly reconstructed pieces are visual approximations, not original layered source exports. Reuse the coin stack at different sizes for smaller coin packs.

The new calendar and coin bag were recreated with built-in image generation using the shop reference: isolate each named icon, preserve blue/gold toy-like 3D style, remove UI and text, transparent background. The blue background was likewise reconstructed with all foreground UI removed.
