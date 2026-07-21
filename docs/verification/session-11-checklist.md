# Session 11 live checklist — D3/D4 permanent nav, reroll, title flow, perk surface

Builds under test: `adc8ec9` (D3 permanent tables + D4 stacked location grid) and
`fde6be9` (reroll hook, TitleFlow, PerkWatch, perk cells), plus the still-unverified
run-3 batch `9fde676` (F11–F14). Deployed DLL hash-verified against `fde6be9`.
Save: slot 1 fresh MACHINIST (cycle 4+, cloud unlocked, no perks bought yet).

Play-flow order. Quoted lines are the expected utterances (wording provisional —
calibration notes welcome on any of them).

## 1. Boot and title

1. During the company crawl: the mod-load line only ("Citizen Sleeper Access
   loaded. Press F1 for commands."). **No "Main menu." over the crawl.**
2. When the crawl ends and the landing fades in: "Main menu. Press Enter to
   start."
3. Press Enter immediately, even without touching arrows — it must start
   (selection-less Enter clicks the landing button). Menu buttons then announce
   on focus.
4. Optional, later: quit to title mid-session — expect just "Main menu." (that
   path skips splash and landing).

## 2. Station — D3 permanent table

5. After load: "On station.", then (~0.6 s settle) "Station table. <zone>.
   <row>" with no key pressed.
6. Arrows walk rows/columns, slash tabs, Space full row, Enter commits — as
   before. **N does nothing.** Backspace speaks the leave refusal, closes
   nothing.
7. Excursion test: open inventory (I), move around, close (I). Return is
   SILENT; first arrow proves the row held. Repeat quickly with U and J.
8. F1: leads with the table grammar ("Up and Down: rows. Left and Right:
   columns. Slash: next tab. Space: full row. Enter: activate."), no "N: map
   table", ends "Control X: native navigation."
9. Ctrl+X: "Native navigation." — native arrows walk markers WITH focus
   announcements, Space describes focus. Ctrl+X again: "Station table. …"
   re-anchored to the camera. F1 while native ends "Control X: table
   navigation."
10. Shift+R at station (no Intuit perks): "Reroll requires the second Intuit
    perk."

## 3. Location — D4 stacked grid

11. Enter a location with clocks. Down-arrow through the action cards; crossing
    into clocks announces "Clock cards. <clock row>"; crossing back announces
    "Action cards. <action row>".
12. Column resets at the boundary; Left/Right speak per-section headers
    (actions: Name, Skill, Risk, Takes, Cost, Predicted, Narrative; clocks:
    Name, Progress, Narrative).
13. Slash at a location: nothing (freed). Enter on a clock row: nothing
    (display-only). Enter on an action row: commits as before.
14. Excursion at a location (I open/close): row position held (previously it
    reset).
15. No "Predicted" spoken anywhere yet (perk not bought — the cell is
    render-gated).

## 4. Run-3 batch carryovers (deployed last session, still unverified)

16. F11: any full clock speaks "complete", never "9 of 8".
17. F12: character window purchase — the Confirm? modal is announced with
    "Enter to confirm, Backspace to cancel."; Backspace speaks "Cancelled.";
    a real purchase composes points + perk + skill-after.
18. F13: no tutorial voice mid cycle-end; tutorials speak when presented.
19. F14: a cryo purchase (Order Fungus style) announces its outcome.

## 5. Cloud — D3 permanent table + card table (§11 first exercise)

20. Enter the cloud (O): settled entry (~1.2 s) "Cloud table. <row>". Census
    stays silent if nothing changed since the baseline.
21. Field browse: arrows move rows with camera sync; Space full row; Demand
    speaks pre-entry ("Matches die N or M" style).
22. Enter a node: flight quiet, settle speaks the CARD row (Name, Demand,
    Takes, Narrative). Left/Right walk the card columns; Up/Down repeat the
    row.
23. Enter on the card: die picker opens and owns the keys; cancel or place —
    returning to the card is silent.
24. Backspace inside a node: leaves; pull-back settle speaks the field row.
    Backspace at field level: exits the cloud.
25. Ctrl+X round trip works in the cloud too.
26. After a hack outcome: "Revealed: …" if reveal edges fire; placing dice via
    the picker must NOT re-trigger the entry census.

## 6. Deferred until perks are bought (future rides)

- Intuit 1: Predicted cells populate (if icon-only, the log flags
  "[Describe] PREDICTIVE renders but carries no text" — decode pass then).
- Intuit 2: Shift+R success — "Rerolled." + new pool ~1 s later; second use
  same cycle → "Reroll already used this cycle."
- Engage 2: cryo costs speak the discounted number + "discounted".
- Endure 2: dice brief appends "Perk: Hard to Kill." at the lowest condition
  tier.
- Proc perks (Thrill Seeker / Efficient Extractor / Transfer Intercept /
  Icebreaker): spoken on proc; watch for double-speak with EnergyWatch on
  Thrill Seeker.

## Standing observe items (unchanged)

F4 unseen-tutorial transcodes (dice-odds sprite log armed), F7b quoted clock
names, energy "6 of 5" boost wording if heard, ENGAGE-refusal-with-points
(likely F12-explained), drive-log alpha divergence samples.
