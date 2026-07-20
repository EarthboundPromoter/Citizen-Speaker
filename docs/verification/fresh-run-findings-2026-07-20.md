# Fresh-run findings — 2026-07-20 (inaugural new-game run, slot 1)

Owner ruling: fixes PARKED as discovered; one fix pass after session close.
**FIX PASS EXECUTED same day (owner go): F1, F2+F6, F3, F4, F7, F8, F9, F10
built, 0-warning build, DEPLOYED to plugins (game closed). All await live
verification next launch. F4 ships blind on sprite naming — it logs every die
sprite name (`[Tutorial] die sprite:`) for calibration; prompt-gap fill logs
mismatches. F7's unrecognized-multi-sign wording ("plus 2, X") and the
clock-callout dedupe direction are provisional owner-review items. F5 (energy
channel) and D1 (selection-sync) remain design questions — not built.
Each entry: mechanism + evidence + specced fix. Checklist:
fresh-run-test-checklist.md. Session context: run is on SLOT 1 (owner confirmed
the overwrite warning deliberately); OPERATOR save backed up pre-overwrite at
`logs/savebackup_2026-07-20_pre-freshrun/` (restore = copy back while game closed).

## Parked fixes (mechanism known, build in fix pass)

**F1 — Map table speaks story-gated clocks (ghost clocks).** Empty Container row
read "Clock 0 of 8; 0 of 24; 0 of 12, negative cycle clock" ×3 — all three are
story-gated groups (Hunted / Maywick / Terminal Flux Clock) the game has never
rendered. Bridge-confirmed inside the location (f~23699 window): Actions root
ACTIVE, End Cycle Action ACTIVE, all three clock groups inactive; the adjacent
Action Switch gate FSMs are ACTIVE and their output is the clock group's local
`activeSelf`. Location table's Clocks tab correctly said "No clocks here." —
surfaces contradicted. Fix: `StationAtlas.ClockCell` interior fallback skips
clock groups whose own `activeSelf` is false; `DialString` prefers the
locally-active size variant over first-valued (kills the arbitrary 8/24/12 variant
pick too). Shipyard started-but-unbillboarded case keeps reading (its group rests
locally active). SECOND SYMPTOM (f62623–f63283 vs f40858): Dragos's Yard renders TWO clocks
(BACK IN BUSINESS 0/8 positive; A DEBT CALLED IN 0/8 negative cycle) but the
map row spoke one "0 of 8, negative clock" — matching neither (wrong polarity
vs one, missing the cycle flag vs the other): the fallback's pick can lie
about polarity/kind, not just invent empties. Post-fix the cell should join
BOTH rendered dials. Same fix covers it.

**F2 — Conversation-teardown focus pinball (auto-read of unrelated action cards).**
After the Dragos intro conversation, focus events spoke "CONTINUE button,
disabled", then HULL DISSECTION and MANUAL SALVAGE (Dragos's Yard cards, full
describe + "disabled"), then "Location: Empty Container" (log f35510–f35545,
~0.3s). Mechanism: conversation end story-unlocked the yard cards; newly
activated selectables transiently claimed selection during teardown before the
game parked focus at the current location. Fix: conversation-end settle window —
suppress focus announcements through the teardown beat, announce only the settled
target (same family as camera-flight mute + strip-steal suppression).

**F3 — Station census channel still live though retracted (BL-16).** "1 location
added. 3 nodes removed." fired on leaving Empty Container (f32906) — the
camera/zone-local artifact family, exactly what BL-16 retracted as unsound. The
retraction was verification-status only; the announcer still speaks. Fix: mute
the station census channel until the BL-16 redesign (cloud census unaffected —
different, verified instrument).

**F4 — Tutorial dice-odds transcode (OWNER-DIRECTED).** The ACTIONS 1/2 tutorial
("New Action Tutorial 1") renders the die-face → outcome-odds table as Die
image groups each holding their odds text ("100% CHANCE POSITIVE" etc.); the
die faces are Image children (Marker/FILL/Frame — one group even nests two Die
images for a value range), so the spoken read lists odds with no die values —
the player can't learn which values map to which odds. Bridge hierarchy dump
captured live (panel id 21960) — structure in this entry's session log.
Fix direction: at announce time the mod reads each Die group's face value
(Image sprite name or pip/marker count — mod-side read, the bridge dump doesn't
carry sprite names) and interleaves "Die N[–M]: <odds>". Same panel also shows
the glyph-gap machinery: "Gamepad Text" with A Prompt / DPAD prompt positioner
children — the dropped "select it with [] and []" glyphs; transcode to keyboard
equivalents per the keyboard-portability rule. This promotes the W4 glyph
transcode with a concrete, owner-directed case.

**Contract watch (CLOSED, verified live): "1/2" page indicators are cosmetic.**
ACTIONS 2/2 arrived as its own sequential panel on the owner's Enter advance
(f56229) and auto-announced in full — the corpus single-page/sequential model
and the Left/Right seal both hold.

**F5 — Energy live channel: BUILT + DEPLOYED (owner ruling, same day —
segments-first).** Corpus: Energy Bar System = Energy Getter → Checker →
Setter 0..6 (setter index IS the rendered box count, 7 authored levels) +
Condition Minus (starving path, rendered Starving marker). EnergyWatch mirrors
ConditionWatch: "Energy falling/rising: N." on level change only (silent boot
baseline), "Starving. Energy empty." edge announce (re-arms on recovery).
Every level change logs `[EnergyWatch] level N (Lua E)` — threshold/max
calibration next launch ("of 6" wording deferred until the pairs confirm).
UNIFICATION PASS BUILT + DEPLOYED (run-2 close, owner-calibrated): energy =
5 boxes @ 20 pts (Setter pairs 2↔40/3↔60/5↔100; Setter 6 = boost overcharge,
flagged if heard); condition = 20 boxes @ 5 pts (art-tick render,
owner-counted; chunk confirmed 65→60). All stat reads segments-first:
cycle string + V ("Energy 3 of 5. Condition 12 of 20, flickering."),
MetersBrief, EnergyWatch ("of 5"), ConditionWatch band announces (box count
rides with band), F7 effect "now" values (sign counts = box deltas,
live-confirmed -- ENERGY = 2 boxes). Cryo stays numeric. Acceptance: next
cycle end should speak "Condition falling: 11 of 20, fading." at 55.
ORIGINAL DIRECTIVE (superseded by the build above):
segments-first across ALL stat reads. Condition has NO box states (continuous
bar floats; five 20-point band states render the band word) but the CONDITION
tutorial says "depletes by one segment each cycle" — segment count lives in
the bar GRAPHIC. Next launch: count tick objects in the bar hierarchy via
bridge (+ observe one cycle's delta; likely 5 points → 20 segments), then one
wording pass converts cycle string / V query / F7 "now" values / band
announces to "N of M[, band]" form. Band always rides with the count (owner).

**F6 — Die-picker open speaks every die (BL-6 confirmed, frame-batched).** Picker
open at Dragos's Yard: ALL FOUR dice fired focus events on one frame (f64067 —
"value 5, die 1 of 4" ... "value 2, die 4 of 4"), then the picker prompt, then
the queued stragglers drained. Owner: should read only the hovered die. Same
transient-focus family as F2. Fix: collapse same-frame (or short-window,
no-user-input) focus bursts to the settled final event — one idiom can serve
F2, F6, and the tutorial-open chatter. New hover wording itself verified
correct in the same lines (§4 wording PASS).

**F7 — Outcome effect lines compose STATE, not raw markup (OWNER RULING,
2026-07-20 mid-run).** Current read: "plus plus BACK IN BUSINESS. plus 15 CRYO."
(f67576). Ruled composition:
  - Clock effects: "<clock name> now x/y" (post-tick value — ClockValue read the
    clock-callout channel already does).
  - Value resources (cryo, items): amount alone is fine ("plus 15 CRYO" stays).
  - Energy and condition: delta AND resulting state — amount + present value,
    condition also gets its band label (sources: Lua Player_Energy, condition +
    $Breaking band word).
  Fix-pass design note: with clocks spoken as "now x/y" in the effect line, the
  separate post-roll clock callout ("BACK IN BUSINESS, 2 of 8 segments...")
  becomes redundant for clocks the card already covered — dedupe (callout only
  for clocks NOT in the outcome, e.g. cycle-clock self-ticks) or merge; owner
  call at fix time.

**F8 — Drives cell misses tracked-drive pips: case-sensitive name filter.**
Owner caught Bright Market reading "Drives: No tracked drive." while SURVIVE's
yellow marker rendered there. Bridge: the pip object is
`Billboard Elements/SURVIVE Pip` (capital P), active; DriveCell filters children
on case-sensitive `Contains(" pip")` (StationAtlas.cs:204) so " Pip" never
matches. Fix: case-insensitive compare; audit the Tracked Drives tab's pip walk
for the same assumption. (Real-save S8 evidence presumably passed on
lowercase-named pips or a different path — verify both casings post-fix.)
Camera-independence (owner question): read is designed camera-independent —
persistent canvas + FindDeep(includeInactive) + dormant-FSM Quest Name var +
QuestLog API (data-side). Post-fix check: read the Drives cell for a
pip-carrying location from a far camera position; if empty, the Quest Name is
event-populated (Text Setup timing family), not authored — then read the pip
child's own name prefix ("SURVIVE Pip") as fallback.

**F9 — Location table drops action cards with trailing-space names (owner-caught:
ASK FOR DIRECTIONS unreachable at Bright Market).** The object is authored
`"Ask for Directions Action "` (trailing space); GetActionPanels
(GameQueries.cs:183) matches EndsWith(" Action") WITHOUT TrimEnd → row never
lists. Map ActionCount trims (StationAtlas.cs:241) → map said "2 actions" vs
table's 1 row: the very discrepancy noticed one turn earlier. Known corpus
authoring quirk ("Interface Breakdown ", "The Flotilla Actions "). Fix: sweep
ALL name matches to TrimEnd first (GetActionPanels, GetClockPanels, and audit
remaining EndsWith/Contains sites — grep shows newer code trims, older doesn't).
Card highlight the owner saw = the Active Frame (native selection resting on
the card — see F10's render map), unrelated to the miss. OWNER RULING: trim fix
covers F9.

**D1 — PARKED DESIGN (owner, post-session): selection-sync on table row nav for
action cards.** Idea: location-table row moves drive native selection (Active
Frame follows the table cursor, analog of the map table's camera-sync).
Tradeoffs noted in-session: pro — sighted-view coherence, game-native hover
state; con — selection side effects during browse (Active Frame FSM churn,
focus-event chatter needing a mute, strip-watchdog interactions; current
design commits selection only on Enter). Not part of the F9 repair.

**F10 — Card reads omit the CRITICAL type facet (owner question exposed it).**
Cards render a type badge pair (Repeatable Action / Critical Action elements,
exactly one active — ACTIONS 2/2 tutorial defines them; critical = one-time-only
roll). Describe/table rows speak name/skill/risk/Takes but never type; critical
only surfaces after use via the resting-state label. Decision-relevant omission
(e.g. Bright Market's ASK FOR DIRECTIONS is DANGER + CRITICAL). RENDER TRUTH
(bridge, id 66346): the badge is an Image chip with a localized TMP child
(object name "Relationship") rendering the literal text "CRITICAL ACTION" —
a game-sanctioned label. Fix: speak the active badge's own rendered text in
card reads (position after the risk word); repeatable badge silent as default
(owner may reverse). No live watcher needed — type is authored per-card;
browse-time read suffices.
CARD RENDER↔DATA MAP (owner-requested, live-verified at Bright Market):
Action Name=name; Action Skill Display+Skill Lock glyph=skill/bucket/lock;
SAFE|RISKY|DANGER one-active icon=risk; Repeatable|Critical one-active
("CRITICAL ACTION" text plate)=type; DICE SYMBOL+percentages=die+odds;
Description=narrative; OUTCOMES(P/N/N+PREDICTIVE)=resolution cards;
Active Frame=NATIVE SELECTION HIGHLIGHT (FSM On iff game Selected == this
card's Gamepad Dice Slot — transient, not card data; this was the "highlight"
the owner saw on ASK FOR DIRECTIONS); Outline/Input Border/BG/Marker=chrome.

## Wording/polish samples (park in polish queue)

- **P1** Overwrite warning read runs body + both button labels in one utterance:
  "THIS WILL ERASE ... CONTINUE?, CONTINUE, BACK" (f860).
- **P2** Map table Name cell on character rows stacks headers: "Name: Character:
  DRAGOS, new..." (f7582).
- **P3** Location table empty cells speak bare headers: "Skill:", "Risk:",
  "Takes:" (f23699–f23758) — the queued terse-emptiness pass, live samples.
- **P4** Cost cell speaks raw pipe markup: "Cost: PER CYCLE | - - ENERGY | -
  CONDITION" (f23770) — transcode nit.
- **P5** Disabled row speaks contradictory affordance: "END CYCLE. Enter to
  activate. Action card disabled." (f32801) — suppress "Enter to activate" when
  disabled, or order reason first.
- **P6** Tutorial glyph drop (known W4): INTRODUCTION body read "Select locations
  with and .." — sprite glyphs vanish (f12473).
- **P7** → promoted to F7 (owner ruling mid-run).

## Verified this run (checklist ticks, details in session log)

- §1 overwrite warning body spoken (was silent) — PASS; class select
  review/change/start — PASS. BL-5 slot-label sample not yet taken.
- §2 tutorials: auto-announce (INTRODUCTION + CONDITION/DICE/ENERGY post-cycle,
  queued cleanly behind cycle string), R reread in Tutorial mode, block stepping,
  boundary repeats — PASS so far. Late-fill trigger panel (BL-3 breakdown case)
  still unheard.
- §3 dialogue: full Dragos intro clean — auto-read, choice counts, numbered
  picks. PASS (first pass).
- §5 location table: END CYCLE full row, Clocks tab honest empty state, column
  walk geometry. Takes variants / lock reasons / bounces / post-roll callouts
  still pending.
- §9 map table: rows, column walk with spoken empties, Enter commit, close/reopen.
  PASS so far (see F1).
- §6 STATION TIERED OUTCOME — the priority carry-over — VERIFIED (f67571–f67588):
  "HULL DISSECTION: positive outcome." + card content + BACK IN BUSINESS clock
  callout + CLOCKS tutorial, all queued in order. ActionOutcomes works on the
  station event path. BL-1 MACHINIST case also heard ("ENGINEER +1" on Hull
  Dissection, f63841).
- §4 hover wording "value X, die a of b" + placement narration (settle →
  narrative + "Enter to start.") — VERIFIED (f64067, f67309).
- §12 cycle-end totals string cycle 1: "Cycle ended. Cycle 1. Energy 40.
  Condition 65, flickering. 4 dice: 5, 2, 5, 2." — bare dice tail correct.

## Watches (no action yet)

- `[WindowState] DIVERGENCE` drive log alpha-vs-truth edge race — TWO samples:
  boot (alpha=True truth=False) and drive-log reopen f78457 (alpha=False
  truth=True). Window resolved correctly both times; cosmetic so far. If a
  misread mode ever accompanies one, promote to fix.
- F1 addendum: full-map sweeps (f77898–f79015) show zero-progress clocks with
  varied sizes on never-visited locations (Shipyard 0/8, Bright Market 0/3,
  Dock B-2 0/4, Rotunda ×2, Dock C-4 0/8) — can't tell rendered-vs-ghost by
  ear; after the activeSelf filter lands, sweep the map once and diff the
  clock cells against this run's reads to see which were real.
- BL-15 / BL-19 / BL-2 glyph guard / strip-steal recovery: nothing yet.
