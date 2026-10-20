# A cleaner version of the cartoon UI

**Status: NOT applied to the project.** Look at the pictures first; if you like it, copy the three files in `files\Assets\Scripts\Hud\` over the project (they replace `HudTheme.cs`, `MenuKit.cs` and `HudWidgets.cs`; nothing else changes, every public call of `HudTheme` and `MenuKit` keeps working, so no other file needs a touch). The old versions are in `DevTools\QaBackups\old_toon_ui_2026-10-17`.

## What "cleaner" means here

You tried a flat dark look and said it was "not our vibe", and you asked for the cartoon UI "a little bit cleaner looking". So this keeps everything that makes it the cartoon game: the purple card, the bright candy buttons, ink outlines, bold outlined lettering, pill chips, the pop and sparkle animations. It only draws them with a lighter hand:

* **Thinner ink lines everywhere** (about a fifth lighter), a **smaller and fainter drop shadow**, a **slimmer cream sticker rim** round the cards, and a **quieter gloss line** (shorter, fainter, only on tall plates). The two-tone "cel" shading of a plate is a little softer.
* **Lettering with a thin, even outline** instead of a thick one with a shadow. Big lettering keeps a round outline; small lettering gets a crisp one-pixel outline. White lettering on a coloured button or ribbon is edged in a dark shade of **that colour** instead of near-black, which keeps the candy feel and looks softer.
* **Fewer competing colours.** Every section title on a page used to be a different candy chip (blue, orange, green, red...). Now they are one quiet tag colour with a small dot in the section's own colour, so the buttons are the only bright things on a page.
* **One size of lettering in a row.** The pick-one pills of a row ("DOCK WORKER 1 .. 15", "PUBLIC / CODE ONLY / FRIENDS ONLY") all use the largest size at which the longest label fits, instead of each label shrinking by itself (at 960 px wide "DOCK WORKER 1" was cut off at both ends; it fits now).
* **A calmer disabled button:** a muted card tone with soft grey lettering and no outline, instead of a glowing grey slab.
* **Readable quota bar.** The label on the HUD's quota bar ("QUOTA $2,580 / $4,300") used to be white across a yellow fill, which could not be read. It is white on the dark part of the bar and dark ink on the bright part, cut where the fill ends. The docks' solid yellow bar ("NEXT QUOTA ...") has ink lettering. Red lettering on the purple card (the pack's weight when it is nearly full, the empty magazine) takes a lighter red; the bar and the bullets keep the strong one.

Not touched: layout, sizes of the screens, game rules, networking, behaviour, animations, the font, the palette (the candy colours are the same). Drawing still happens only on the Repaint event and stays allocation-free (the outline uses the same few `GUI.Label` passes as before: 8 for big text, 4 for the rest).

## The dials (taste)

All the weights are in one block, "the finish", near the top of `HudTheme.cs`, and every plate in the game goes through it:

| constant | now | before (what the old drawing code did) | what it does |
| --- | --- | --- | --- |
| `OutlineK` | 0.78 | 1.0 | the ink outline, as a share of the width a caller asks for |
| `ShadowK`, `ShadowA` | 0.55, 0.30 | 1.0, 0.38 | the hard drop shadow: depth and opacity |
| `RimK`, `RimA` | 0.65, 0.55 | 1.0, 0.62 | the cream sticker rim: width and opacity |
| `ShadeAmount` | 0.18 | 0.24 | how much darker the lower half of a plate is |
| `GlossA` | 0.20 | 0.32 | opacity of the gloss line |

Put them back to the "before" column and the cards are heavy again; lower them further (for example 0.6 / 0.4 / 0.5 / 0.12 / 0.10) for an even flatter, lighter look. The pictures `alt_lighter_*.png` show that second setting for three screens, so you can compare without building anything.

## What changed, file by file

**`Scripts/Hud/HudTheme.cs`** (the core: palette, plates, text)
* New block "the finish" (the constants above). `Body`, `Pill`, `Bar`, `KeyCap` and the text outline read it.
* `Body` draws the shadow and the rim as one shape that follows the plate, and takes an optional `litShare` (how much of the plate is the lit upper half). `Fill` puts the gloss line only on plates at least 30 px tall, half as long, thinner and fainter.
* New colours `Track` (the dark trough of a bar, a slider, a text field; it was a hex inside `Bar`) and `Tag` (the quiet fill of section tags), and `Edge(plate)` (a dark shade of a plate's own colour, for white lettering on it).
* Text: a thin even outline (8 passes of 2-5 px for text from 34 px up, 4 diagonal passes of 1 px, 2 px from 28 px up, for the rest). The old code used 2-6 px rings plus a faint shadow for big text and a single shadow for small text. `Text` and `TextPx` get an optional `edge` colour (default ink).
* New `FitPx(texts, px, width)`: the size at which the longest of several labels fits.
* New `BarLabel(rect, fraction, text, size, outline)`: the knock-out label for progress bars.

**`Scripts/Hud/MenuKit.cs`** (menus: Sheet, Ribbon, Button, Pick, Swatch, Slider, ToggleRow, TextBox, scroll bar)
* `Sheet` is drawn by `HudTheme.Body` (its shadow, rim and outline follow the finish) with a radius of 22 instead of 24. `Ribbon` is a little smaller (30 px lettering, 46 px tall) and its lettering is edged in the ribbon's own dark shade.
* `Section` titles: one quiet tag with a dot in the section's colour, and a thinner rule. (The `ink` argument stays so the call sites do not change; it is no longer used.)
* `Button`: lettering edged in a dark shade of the button's colour, thinner outline and shadow, grows 2 px on hover (3 before), and the disabled look described above.
* `Pick` / `PickRow`: thinner outline and shadow, a slimmer cream halo on the selected pill, no gloss line on the swatch, one lettering size across the row.
* `Slider` (a slimmer track and knob), `ToggleRow` (a slimmer switch with a lettering edge in its own colour), `TextBox` (thinner outline and focus ring), the scroll bar (a thinner track and thumb), the small status chips (no outline).

**`Scripts/Hud/HudWidgets.cs`** (the in-run HUD)
* The status card's quota bar uses `BarLabel`; the docks' bar has ink lettering.
* The pack card's weight and the ammunition count use a lighter red for the lettering.

Nothing else was edited. (The session that made this also had to fix script references in its scratch copy to be able to take pictures: that helper, `ZzUiRepair.cs`, is not part of the delivery.)

## The pictures

`before_<screen>.png` is the game as it is now, `after_<screen>.png` the same scene with the three files above, taken by the project's own picture scenarios (the Editor's Game view, 1920 x 888; the `_960`, `_1280` and `_3840` ones are the same page at other sizes). `pairs\pair_<screen>.png` puts the two side by side. Screens: the lobby (main, play, customize, settings, how to play, lobby, invite, status, browse), the Esc menu and the keybinds, the in-run HUD (status card, banners, pack, ammunition, pinned, backup), the inventory, the shop, the security computer's tabs.

## How it was checked

* The three files compile with 0 errors in a scratch copy of the project (`C:\bqui`), brought up to date with the project at 2026-10-18 01:00 (the lobby music slider, the unlimited ammunition in the docks and the rest of the night's work are in it).
* The picture scenarios `Menus`, `MenusLobby`, `Ui`, `InvUi`, `DemoUi` and `ComputerUi` ran on the untouched files (`before_*`) and on the three new files (`after_*`): no failed check in either set, except one in `MenusLobby` ("the lobby song is loaded and plays"), which fails the same way before and after because the scratch copy has no `LobbyMusic.wav` (it is not part of this change).
* `alt_lighter_*.png` and `pairs\alt_pair_*.png`: the same screens with the dials lowered to 0.6 / 0.4 and 0.25 / 0.5 and 0.45 / 0.14 / 0.10 (a flatter, lighter look) next to the delivered one. If you prefer it, change the five numbers in `HudTheme.cs` after applying the files.
* Not checked: a BUILT player with the new files (the pictures are from the Editor's Game view), and the screens that only appear with two or more players (the party invite pop-up, the lobby list with friends).
