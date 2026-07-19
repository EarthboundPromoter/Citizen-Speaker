# Bug ledger (persistent)

Durable, cross-session bug list — unlike the per-session triage queues, this file
persists and carries status. Add evidence pointers (log snapshot + frame) with every
entry; close entries with the fix commit or the ruling that resolved them.

Log snapshots referenced below live in `logs/` at the repo root (gitignored, kept
on disk): `LogOutput_run1_2026-07-19.log`, `LogOutput_run2_2026-07-19.log`,
`LogOutput_pre-launch_2026-07-19.log` (tail of session 5).

## OPEN

**BL-1 — Modifier misread (HIGH).** The color-heuristic modifier readout announced
HULL DISSECTION as "ENGINEER 0, risky" while the rendered highlight sat on the +1
cell and Lua ground truth agreed (MACHINIST ENGR = +1). Two occurrences, run 2
(f10396 area, log lines 390/49; second at f11413). The heuristic picks the wrong
cell under some render states — exactly the failure the W4 "modifier readout → Lua
skill value" migration kills. Lua source is verified and allowlisted; promote that
W4 item. (Found in passive log readthrough 2026-07-19; owner confirmed.)
FIX DEPLOYED `6a310b5` (build-queue Q1): bucket read from the Lua skill variable
via the game's own FloatSwitch mapping; heuristic + calibration dump deleted.
Awaiting live verification (acceptance: build-queue-acceptance-2026-07-19.md).

**BL-2 — "Y button" label + cloud post-leave anchor.** After a cloud node leave,
the game parks selection on the Scan Button, whose spoken label resolves to the
gamepad glyph child text "Y button"; arrows dead-end there (Top UI, off the marker
field). Run 2 t72–77, owner dump f2702. Two parts: (a) Describe needs the
prompt-glyph transcode (W4 tutorial-glyph item, now with a concrete case) so glyph
children never become labels; (b) consider whether cloud post-leave needs a
mod-side re-anchor row (focus-model addition — owner ruling required). Note: the
deployed camera-flight mute changes the shape here — if the settle announce lands
on the Scan Button it will still say "Y button" until (a) lands.

**BL-3 — Trigger-driven tutorials invisible (silent tutorial).** The Character /
Hacking / Breakdown Tutorial Triggers activate a `$TUTORIAL OBJECT` panel
(corpus + live /fsm read; e.g. Breakdown → `Tutorial System/Breakdown Tutorial`).
One such panel rendered ~run-1 f7900 with no auto-read, no Tutorial mode, no
input-pause, and a dismissal that produced zero observable events. Root cause of
the detection miss not yet pinned (panel IS a Tutorial System child — alpha or
text-timing suspected). Breakdown trigger is ARMED on the slot-2 test save
(Tutorial Done=0, watches BREAKDOWN_CYCLE) — it should re-fire; dissect the panel
live while up. Fix direction (pending live probe + owner ruling): FsmSignals on
the triggers' Tutorial state as clock, panel rendered text as content, Complete
state for dismissal.

**BL-4 — Inventory Up/Down swap intermittently bounces; label speaks intent.**
REVISED 2026-07-19 (real-save session): not the empty-DATA case — on the OPERATOR
save (data items present) most swap presses bounced but at least one succeeded
(f4789 announced "ITEMS.", provable only with the strip resting on DATA).
Mechanism: a frame race in the panel handoff. The outgoing panel's deactivate
kills selection; if the incoming panel's cursor doesn't claim selection the same
frame, the strip's every-frame name-watchdog (selection != "Item Cursor" →
Deactivate) slams it back. Both panels' cursors share the "Item Cursor" name, so
the watchdog passes whenever ANY cursor holds selection — the race is purely
claim-timing. Dispatch is on-contract (the game's own Swapper sends the identical
event), so the game's controller swap likely races identically — candidate GAME
bug. Evidence: run 1 f1998/f2089 (both bounced, empty DATA); real-save run
f4207/f4672/f4723/f4778 (bounces) + f4789 (success). Mod fix still owed either
way: announce from the settled state's rendered tab label (FsmSignals clock)
instead of pre-announcing intent — awaiting owner ruling.

**BL-5 — Save-slot label token scramble (minor).** Slot labels read in layout
order: "Save slot 2, CLASS, CYCLE, 3, MACHINIST" (run 2 f724). Wording/order
composition fix; owner calibration.

**BL-6 — Die-picker open stacks die readouts (minor).** Picker open queues
"Die 1, value 5 / Die 2, value 4 / Die 3, value 1" focus lines before the picker
prompt (run 2 f10963). Chatter-shape question for owner calibration.

**BL-7 — Node census false "1 location added. 1 node removed." on canvas-variant
swaps.** Real-save run announced it twice (f2754, f19725) with identical wording;
bridge check at the second showed the enabled location set unchanged (the four
known markers — owner-confirmed no new location). Markers currently live under
"...Canvas 2" objects where earlier events referenced the un-numbered canvases:
the game swaps a location's canvas for its story variant, and the census diff
appears to key on the canvas/marker object rather than the deduped location name
(the session-5 story-variants dedup ruling isn't holding). Fix: dedup census
identity by rendered location name, not canvas object. False positives also
erode trust in the census channel generally.

**BL-8 — Outcome + completion narrative lost on clock-completing actions.** When
an action outcome completes a clock, the game swaps the whole action group to its
story-variant canvas (live: Dragos's Yard Actions → "Dragos's Yard Actions 2" at
WINTER LIGHT 8/8, f~26600). The outcome watcher's card was torn down mid-flow, so
neither the outcome tier nor the completion narrative was announced (owner-flagged
live); the variant's new card texts rendered unspoken. Same canvas-variant
machinery as BL-7 — fix them together: identity + announcements keyed on rendered
names/labels across variant swaps, plus an on-swap re-read of changed card
descriptions (rendered text, so render-honest). Non-completing outcomes on the
same card announced fine all session (all three tiers verified).

**BL-9 — Character-window upgrades: purchases and refusals are silent.** Real-save
run f42724–42829: Enter on "Upgrade Button Perk 2" spent the owner's 1 upgrade
point with no announcement; two subsequent Enters on a modifier-tier button
failed on zero points, also silently ("upgrade fails for an unknown reason" —
owner report; rendered Points Av read 0 while the window's opening announcement
had said 1). Needed: announce point-count changes (rendered Points Av), speak a
purchase confirmation from the row's rendered state, and voice refusals when a
tier button click is game-refused (tier costs render as 1/2/3-point labels on
the Upgrade Tracker — speakable). All render-paired.

**BL-10 — Inventory slots announce as bare "Item Cursor"; contents undiscoverable.**
Owner couldn't find a newly acquired item (Stablizer vial) though it rendered in
the ITEM panel (slot Amount 1, alongside Cryo 80 / Scrap 1 / Shipmind Fragment 1).
Cursors carry no text; the game renders the hovered item's identity in the
Inventory Display (Item Name + Item Description) and each slot renders an Amount.
Fix: on cursor focus, speak Inventory Display's Item Name + slot Amount (Space =
add Description) — all render-paired. BUILT + DEPLOYED 2026-07-19 (owner go):
Describe Item Cursor case + 0.15s freshness deferral in FocusPatch (the display
populates a beat after the cursor moves); covers BOTH panels (shared display and
cursor naming). Awaiting live verification.

**BL-11 (REVISED) — Item use: keyboard route probably already works; verify +
wording.** Items are actionable (dice-style slotting FSMs: Drag / Gamepad Drag /
Check for Slot / Slotted / Used — Stablizer decode). Gamepad flow decoded from
Self Repair's slot (corpus): a single CLICK on the action's Gamepad Dice Slot
does everything — reads the held count from the Lua INV_* variable, compares to
Item Cost, then positions the item and sends it Gamepad Drag itself. No picker,
no drag gesture on controller — so the mod's existing Enter-on-slot click path
should already drive item use. Needs: (a) live verification on an item action
(e.g. Self Repair consumes Scrap Components), (b) wording check — the flow's
commit state is named "Slot Item", the same family the die-commit announcer
watches, so a successful item slot may announce "Die slotted."; (c) insufficient
count bounces silently (Reset + refocus) — refusal feedback wanted, cost is
renderable from the slot's Item Cost pairing.

**BL-12 — Cloud data actions: demanded die not announced.** Data-action cards
render the die value to match as a glyph on the card's right (per the game's own
DATA ACTIONS tutorial); the mod speaks the card name and skill modifier but not
the demand — the gating fact for the action (owner-flagged live 2026-07-19).
Long-standing W4 "cloud die-match transcode" item (triage 19b), promoted here
because it compounds with BL-1 (modifier sometimes misread) — the card currently
conveys neither fact reliably. Transcode source: the slot's rendered die glyph
(pips/value render route needs the corpus check; likely same DiceValue pairing as
tray dice). Also noted: cipher/gate commits announce as "Die slotted." (BL-11
wording family), and gate outcomes are structural (node reveal), not outcome
cards — no narrative is being missed there.
FIX DEPLOYED `103a2aa` (build-queue Q2): corpus decode found the pairing — the
Hacking Dice Slot FSM's Required Roll/+1/+2 floats drive the rendered glyphs
(FloatSwitch → Dice Value states), Potential Dice = glyph count, Slotted 1–3
accept exactly those values. Describe appends "Matches die ...". Awaiting live
verification.

**BL-13 — Cloud node outcome pipeline entirely silent.** Cloud nodes run a
different controller template than station actions (Hacking Slots Controller:
Slot1 → Active → Hacking → Outcome Animation → Complete — corpus + live), so the
station outcome watcher never fires there. Live-decoded sequence (Node Æ32,
2026-07-19): die commit (heard) → Dice Slot Button press = activation (silent) →
hack resolution (silent) → Sequence Complete Button appears (unannounced) →
owner's press collects the result (silent; owner-flagged "did something I
couldn't hear"). Zero ui.text events through the whole window — results render
via card effect elements outside the set-text hooks. Fix: FsmSignals on Hacking
Slots Controller states as clocks; content sources CONFIRMED by live /texts
capture mid-hack (Havenage Agent node, 2026-07-19): cards carry a standard
OUTCOMES family (Outcome Type + Effect lines, e.g. "NEUTRAL OUTCOME" /
"+ HAVENAGE DATA") — same template the station outcome reader already parses —
plus rendered sequence steps with lock state ("ACCESS PROTOCOLS" / "LOCKED"),
MATCH/INPUT labels, and "COMPLETE SEQUENCE". Only the demanded die VALUE is
graphics-only (needs the DiceValue-style FSM pairing — the BL-12 half). Announce
the Sequence Complete Button's appearance (required press). Companion to BL-12 —
together they are the W4 cloud-coverage build, now fully specced.
FIX DEPLOYED `103a2aa` (build-queue Q2): CloudOutcomes subscriber — controller
Outcome Animation/Complete clocks → rendered OUTCOMES read (tier + shared
station effect/narrative reader); collect button announced from rendered label,
post-press re-render spoken via scoped label watch; sequence steps in Space
describe. Awaiting live verification.

## DEPLOYED, AWAITING LIVE VERIFICATION (2026-07-19 batch)

- Cloud node exit: Backspace in Cloud clicks Leave Button (verified live run 2) →
  extended to field level (Leave inactive → Scan Button click). Field half unverified.
- Dial-first cloud mode: Scan Button dial outranks the leaking `Hacking?` global
  (stale-flag incident run 2 t300+); transitional states fall back to the flag;
  divergence logged. Run-2 state sides validated from log (`Disable` in-cloud,
  `Disable 2`/`Off` station-side).
- Camera-flight focus mute + settle announce (cloud markers only, Hacking UI
  scoped, 3s timeout backstop).
- Strip-steal suppression + ActionView re-anchor (die-picker cancel handed focus
  to DATA Button; RefocusUI recovery, rate-limited, never same-frame).
- Expected side effect to confirm: cloud↔station mode flicker during flights gone
  (was transitional-dial fallthrough).

## RESOLVED

- Options menu value changes speak bare numbers — owner pinned as-is (2026-07-19).
- Cloud node trap (no keyboard exit; quit-to-menu escape, 45s lost) — fixed by the
  Cloud Cancel patch, verified live run 2 (three node leaves via Backspace).
- Stale "In the cloud" refusals at station — root-caused (Hacking? leak), fix in
  deployed batch.
- F3 dump t176 (run 1) — owner: late press, no underlying issue; dropped.
