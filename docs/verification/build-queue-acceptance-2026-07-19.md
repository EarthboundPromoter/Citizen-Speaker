# Acceptance predictions — build-queue session 2026-07-19 (post-queue builds)

W5 discipline: predictions written before the verification launch. Two segregated
sections so a failure attributes cleanly: (A) the session-6 deployed batch that was
already awaiting verification, (B) the new builds from this session (commits
`6a310b5`, `91fc4bc`, `103a2aa`, on top of checkpoint `06e6a8f`/`dab058b`/`81316fd`).

## A. Carried over — session-6 batch, still unverified

Per the bug ledger's deployed section; predictions unchanged from that session:

1. Field-level Backspace → Scan Button click (cloud, Leave inactive).
2. ActionView strip-steal re-anchor firing live (die-picker cancel).
3. Restored perk rows (all 5 skills' perk axis after the name-filter regression fix).
4. BL-10 first listen: item cursor speaks "name, amount N", Space adds description.
5. Camera-flight mute side effect: cloud↔station mode flicker gone during flights.
6. Item-action commit wording check ("Die slotted." on item slots — BL-11).

## B. New builds this session

### Q1 — BL-1 modifier → Lua (`6a310b5`)

- OPERATOR save: data/interface cards announce "INTERFACE +1" (Lua INT = +1),
  including the cards that misread "0" in the session-6 logs.
- Slot-2 MACHINIST: Hull Dissection announces "ENGINEER +1" (Lua ENGR = +1).
- Zero `[Describe] modifier row` lines anywhere in the log (dump deleted).
- Regression guard: skill line still reads "SKILL bucket" on both station and
  cloud cards; a missing Lua variable speaks the skill word alone (no bucket).

### Slot-ins (`91fc4bc`)

- Cycle-end string dice tail is now the bare-values D form: "... N dice: v, v, v."
  (was "die 1, value 1, ..." verbose).
- No `[Dialogue] DIVERGENCE` lines can appear (diagnostic removed after running
  clean across all six session-6 snapshots). Regression guard: dialogue mode
  switching unchanged (events were already the truth source).

### Q2 — BL-12/13 cloud package (`103a2aa`)

- Committing a die and activating a node hack produces, after the resolve
  animation: "<node name>: <rendered tier> . <effect lines> . <narrative>"
  (e.g. "Havenage Agent: NEUTRAL OUTCOME. plus HAVENAGE DATA."). Two clocks fire
  (Outcome Animation + Complete); identical content must announce ONCE.
- When the Sequence Complete Button comes up: "<rendered label> button."
  (expect "COMPLETE SEQUENCE button."). If it never comes up: silence plus a
  `[Cloud]` log line, never a guess.
- After the collect press: the re-rendered label speaks once (expect
  "DATA EXTRACTED."). Clock = rendered-label change, so mouse or Enter both work.
- Node card describe now ends with the demand: "Matches die N" at skill bucket 0;
  "Matches die N or M" / "Matches die N, M or K" with +1/+2 skill (glyph count =
  Potential Dice). Cross-check against the rendered glyphs with sighted spot-check.
- Space (detailed) on a node adds rendered sequence steps: "ACCESS PROTOCOLS,
  LOCKED" (then UNLOCKED after that step clears).
- Station regression guard (FindActionRoot extension): station action cards
  describe exactly as before; no new "* Actions"-group children mislabeled as
  actions (watch for odd DescribeAction fallbacks in the log).

### Q3 — BL-7/8 variant-swap family (`c817a7a`)

- Zero false "1 location added. 1 node removed." across a session with variant
  swaps (it fired 3× today, all false); a genuine reveal still announces with
  correct counts. Baseline log line shows a nonzero location count (the 0-location
  early baseline is gone).
- A clock-completing outcome (the WINTER LIGHT case) announces tier AND the
  completion narrative even though the game swaps the action group mid-flow;
  non-completing outcomes announce exactly as before (same wording, all tiers).
- No doubled outcome announcements (the poll's announce path is removed; the
  FsmSignals clock is the only announcer).
- Watch: outcome announcements during load/boot walks would be new (the poll's
  first-seen suppression is gone; state ENTRY should not fire on boot, but this
  is the prediction to check).
- Held for owner ruling: on-swap re-read of changed card descriptions.

### Q5 — smaller items (`1b9a1bc`)

- BL-9: buying a perk or rung speaks "Purchased." + the rendered points line;
  a game-refused click (insufficient points) speaks "No change." + the points
  line (was fully silent). The main UPGRADE button stays untracked. Wording
  provisional.
- Strip-steal at the station map now recovers (suppress + UI-selector Reset);
  L query never reads "DATA button" after a picker cancel at station.
- BL-2 narrow: the Scan Button (and anything else with a Gamepad Prompt child)
  never speaks as "Y button" — its real label or object name is used. Full glyph
  transcode remains W4.

### Wording calibration flags (owner, at leisure — not failures)

- "Matches die N, M or K" phrasing — provisional.
- Collect result speaks the bare rendered label ("DATA EXTRACTED.") — provisional.
- Cipher/data die commits still announce "Die slotted." (BL-11/BL-12 family,
  standing flag).
