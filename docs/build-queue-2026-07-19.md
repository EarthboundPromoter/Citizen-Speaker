# Build queue — prepared 2026-07-19 session close

Implementation instructions for the held builds, in recommended order. Evidence
citations live in docs/bug-ledger.md (BL-n); log snapshots in logs/. Every item
rides already-verified signals unless marked otherwise. W5 discipline: each item
lists its acceptance predictions — write them into the session's acceptance notes
before the verification launch.

## Q1 — BL-1: modifier readout → Lua (BUILD FIRST)

Why first: actively announces wrong skill modifiers in live play (three+ hits
today: INTERFACE +1 read as "0" on both station and cloud cards).

- File: `mod/src/UI/Describe.cs`. Replace `SiblingModifier`'s color-outlier
  heuristic: in `CollectSkillLine`, after matching the skill word, read the value
  from `Substrate/LuaStore` (existing verified, allowlisted getters — skills
  END/ENG/ENGR/INT/INTU, bucket −1/0/+1/+2) and speak that bucket. Map rendered
  skill word → Lua skill getter.
- Delete the heuristic + the `[Describe] modifier row` calibration dump (its job
  is done); `SingleColorOutlier`/`ColorsClose` become dead code — remove.
- Both station action cards and cloud node cards flow through this path — one fix.
- Acceptance: OPERATOR save announces "INTERFACE +1" on data/interface cards
  (Lua INT=+1); slot-2 MACHINIST announces "ENGINEER +1" on Hull Dissection
  (Lua ENGR=+1); zero `[Describe] modifier row` lines in the log.

## Q2 — BL-12 + BL-13: cloud package (die demand + outcome pipeline)

All content sources confirmed by live captures (ledger BL-13 entry).

- New subscriber (Watchers or a new `CloudOutcomes` class): `FsmSignals.Subscribe`
  on owner name **"Hacking Slots Controller"** — states observed live:
  `Slot1 → Active → Hacking → Outcome Animation → Complete` (+ `Node Complete`).
  On `Outcome Animation`/`Complete` entry: resolve the node action root (the
  controller's ancestor `* Action` transform), read the rendered OUTCOMES family
  (`OUTCOMES/*/Outcome Type` + `Effect N` texts — same template the station
  outcome reader parses; reuse that reader), announce queued.
- Sequence Complete: on `Complete` entry, if `Action Elements/Sequence Complete
  Button` is active+interactable, announce from rendered text (card renders
  "COMPLETE SEQUENCE" on `Input Border/Relationship`; after the press it becomes
  "DATA EXTRACTED" — announce that transition too). The press itself is the
  user's Enter (native flow, already works).
- Sequence steps: `Elements Slot N/Outline (1)/Text` = step name ("ACCESS
  PROTOCOLS"), `Outline (2)/Text` = "LOCKED"/"UNLOCKED" — include in the node
  card's detailed describe (Space).
- Die demand (BL-12): graphics-only. FIRST decode (corpus, offline): variables of
  `.../Gamepad Dice Slot 1` (census line ~906) and `.../Hacking Dice Slot 1`
  (~4889) — expect a DiceValue-style float naming the demanded value. If found,
  append "matches die N" to the node card describe; if not found, bridge-probe a
  live node before shipping (invariant 6 — do NOT ship unverified).
- Wording note: cipher/data commits currently announce "Die slotted." (die-commit
  watcher on `Slot Item`/`Select Dice 2`) — leave for owner calibration, flag at
  session.
- Acceptance: activating a node hack produces an outcome announcement within the
  resolve animation; the collect affordance is announced when it appears; the
  collect press announces "DATA EXTRACTED"; node describe includes the demand.

## Q3 — BL-7 + BL-8: variant-swap family (census + completion narrative)

Root cause shared: the game swaps a location's canvas (and action group) to a
story-variant object; identity keyed on GameObjects breaks.

- BL-7 (`mod/src/Game/NodeCensus.cs`): census identity = rendered location name
  (the `* Canvas` owner-name parse Describe already uses), not the marker/canvas
  object. A diff where the same name exists before and after = variant swap = NO
  announcement. Acceptance: zero "1 location added. 1 node removed." false
  positives across a session (it fired identically 3× today, all false); a
  genuine reveal still announces.
- BL-8 (outcome watcher): when an Action Controller outcome state fires, resolve
  the card by action NAME with a short (2–3 frame) deferral so a mid-flow variant
  swap can't tear the read down; if the group swapped, re-find the same-named
  action in the active variant. Consider (owner ruling on wording) an on-swap
  re-read of changed card descriptions — the swap is exactly when the cards'
  rendered text changes meaning. Acceptance: a clock-completing outcome announces
  tier + narrative (today's WINTER LIGHT 8/8 completion was fully silent).

## Q4 — BL-4: inventory swap package (CONFIRM DESIGN WITH OWNER FIRST)

Proposed, not yet owner-approved: announce-on-settle + one-shot handoff.

- Announce-on-settle: on Up/Down, send `Swap` (unchanged); subscribe FsmSignals
  "Inventory" resting states (`Item`/`Data` + numbered families — WindowState
  already maps open/closed sets) and announce the settled panel's rendered tab
  label (ITEM Button/DATA Button child texts). Settle-back = bare repeat of the
  current label (dead-end idiom, owner-ruled earlier).
- One-shot handoff (the reliability fix): after sending Swap, once, ~0.12s later,
  `SetSelectedGameObject` onto the incoming panel's first active `Item Cursor`
  (under `DATA Inventory UI`/`ITEM Inventory UI`). Never repeated, never held —
  it supplies the selection claim the game's own race loses (ledger BL-4:
  watchdog closes the strip when no cursor holds selection at handoff; the
  game's controller swapper has the identical race).
- Acceptance: ten consecutive Up/Down presses toggle panels deterministically
  with correct labels; on the empty-DATA slot-2 save, behavior is consistent and
  honestly announced either way.

## Q5 — smaller approved/queued items

- BL-9 (character window): announce the rendered `Points Av` value on change
  while the window is open (read after any review Activate), voice purchase
  confirmations from the row's rendered state (FG marker appears), and voice
  refusals when a tier click is game-refused (tracker renders 1/2/3-point cost
  labels). All render-paired.
- Strip-steal extension: FocusPatch strip-steal recovery currently gates on
  `Mode.ActionView`; extend to `Mode.Station` (recovery = `ReAnchor(Station)`,
  the UI-selector Reset). Evidence: L query read "DATA button" at station map
  today.
- BL-2 narrow fix: in Describe, never use a `Gamepad Prompt`/glyph child's text
  as an element label ("Y button" incidents; full glyph transcode stays W4).
- BL-5/6: save-slot label ordering + die-picker readout stacking — owner
  calibration conversations, not code-first.

## Standing stakeout (no build until evidence)

- BL-3 silent tutorial: Breakdown trigger armed on BOTH saves (`Tutorial Done=0`,
  watches `BREAKDOWN_CYCLE`; real save has had breakdown-range condition). When
  a tutorial renders without announcing: F3, then bridge `/hierarchy` +
  `/texts` + `/modstate` WHILE IT IS UP. Fix design (FsmSignals on trigger
  `Tutorial` states as clock, panel rendered text as content, `Complete` for
  dismissal) ships only after that dissection.

## Environment notes for a cold start

- Repo `C:\Users\IATPFNJ624\CitizenSleeperAccess`; build `dotnet build -c
  Release` per plugin dir; deploy = copy DLL to game `BepInEx\plugins` (direct
  when game closed; deploy-on-exit watcher pattern when running). Snapshot
  `BepInEx/LogOutput.log` before any relaunch.
- Diagnostics built today (all live): F3 incident dump; `[Speech:...]
  [mode fN tN]` stamps; mod-side input/nav ring; bridge `/modstate`, `/watch`
  frame numbers + `ui.press` events. Monitor speech via `[Speech:` filtered
  tails; frame number joins mod log ↔ bridge `/watch`.
- Working tree has ~2 days of uncommitted work and origin is ~59+ commits
  behind — commit/push is a standing owner decision, not part of this queue.
