KINGS OF SIEGE — CHEST POPUPS / DEVELOPER PNG PACK

Includes both approved views: chest selection, opening and reward reveal.
All component PNGs are RGBA. Full-screen Reference PNGs are flattened mockups,
not layered source files. Buttons, panel and card backs are regenerated standalone
versions of the approved design; they are not pixel-exact source-layer exports.
Original unit card art and existing chest animation frames are preserved.
Separate sample text PNGs use DejaVu Sans Bold, a substitute font. Use your game
font and native text components for localization, timers, names and quantities.
Power card stack is a placeholder visual, not final individual power artwork.

ASSEMBLY
Render the existing home scene, blur it in-engine, add Screen_Dim above it,
then place Popup_Empty above the overlay. The home screenshot is a reference,
not a replacement for the interactive scene. Use panel/button 9-slicing so
corners keep their shape. Start with 45px panel insets, 35px button insets;
adjust these to your engine import scale. Keep art aspect ratios.
Selection: title + close, three equal columns, name, chest, clock + total
unlock hours, status. Put one OPEN CHEST button in the shared footer.
Silver 4h; Gold 8h; Master 24h. Example timer 02:35:10 is not game state.
Select a ready chest to enable Open. Timer completion changes Unlocking to
Ready. Waiting indicates a chest not started; wire start/unlock behavior to
your game's queue rules. Do not start an additional timer unintentionally.

OPEN / REVEAL
Use the selected chest's Animation_Frames 01..12 at roughly 12 fps.
01 closed; 08 open without glow; 12 open with light. Each frame is 362x362;
keep a fixed pivot and do not auto-trim individual animation frames.
These generated frames are visual drafts; check motion consistency in-engine.
Hold last frame, animate Card_Back upward, then reveal the actual rewards.
Animate Selected_Blue_Glow / Chest_Gold_Glow opacity and Sparkle scale separately.
Render reward cards in one horizontal row, apply Card_Name_Fade below dynamic
names and quantities. Final Collect commits the displayed reward once and closes
or returns to chest selection. Do not grant twice if the button is tapped again.

SAMPLE REWARDS
120 coins + Swordsman x1 + Archer x1 + 2 power cards demonstrate the Silver
reward layout. Production rewards must come from game state, not these labels.
Keep reward grants idempotent and authoritative in your game backend.

OPEN Preview.html for a simple local asset assembly demo. Reference/ contains
the approved visual targets. The demo is a layout aid, not production game code.
