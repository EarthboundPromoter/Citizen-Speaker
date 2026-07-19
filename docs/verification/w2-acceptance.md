# W2 modality layer — acceptance predictions (written pre-launch, 2026-07-18)

Per the W5 model. Built against input-model.md; every signal the ModeModel reads
is corpus-verified or live-proven (citations in ModeModel.cs). All wording
provisional until owner calibration.

## Flagged for owner review (scope derivations, not explicit rulings)

1. **Toggles stay live during dice allocation** (I/U/J, not S). Derivation: the
   game designed ForceUnslotDice for exactly the window-opens-mid-allocation
   case, so refusing those keys would be stricter than the game. Owner may
   prefer refusals there anyway — say so and the table changes.
2. **Station toggles absent in cloud mode** (I/U/J refused, S live as the
   designed exit). Derivation: scan-entry deactivates Character UI, Locations,
   1_Action Groups wholesale (brief E modal map) — their targets are gone.
   Cloud Backspace deliberately refuses (the node CloseAction wiring is
   unverified live; ledgered for W4 cloud coverage, not wired dark).

## Predicted behavior, next launch

1. **Nothing regresses silently at load:** scene announcements, tutorial,
   notifications, outcome pipeline, dice announcements, window watchers, cycle
   summary — all untouched code paths.
2. **New keys:** V speaks "Cycle N. Energy X of 5. Condition C, band. Cryo K."
   (Lua-backed, values matching HUD); D speaks "N dice: a, b, c." (+ "M
   spent." when true); L speaks mode (+ zone via Location Controller dial +
   focused marker on station); Z repeats last utterance; R rereads the last
   dialogue line; F1 speaks only the current mode's keys.
3. **Removed keys:** Tab, C, F2, grave, brackets do nothing (Tab's function is
   served by native arrows between action slots — brief E adjacency).
4. **Backspace:** dice allocation = designed Back (retract-or-cancel, FSM
   resolves depth); character window / drive log = designed toggle close;
   inventory cursor = designed Deactivate; pause = designed Pause Back mapping
   (Pause states → Unpause, Sure?/OPTIONS → Back); station/action view =
   Leave Button. Watch: drive-log close via its designed event is NEW wiring
   (template-identical to character window, but its open-branch is a ledgered
   live-open) — if it misbehaves, that's the known ambiguity surfacing.
5. **Refusals:** a scoped-out key answers "<Mode name>. <Thing> not available
   here." — e.g. U inside the drive log; V/D/K/L at the title answer with the
   mode name.
6. **Empty Enter** on the station map with nothing selected re-anchors to the
   nearest marker (UI selector Reset — the game's own Confirm backstop
   mechanism) instead of doing nothing.
7. **Listening modes** (cycle transition, autoplay): game-facing keys quiet,
   speech/queries answer.

## Known-stale-by-design (W4 items, not regressions)

- K remains the flat clock dump; the navigable K index with Right-for-narrative
  is W4 feature work.
- "Die slotted." still rides the session-3 heuristic (the real-state hook via
  FsmSignals is W4); the Watchers' Gamepad-Dice-Slot "Slotted" poll is dead
  code against a nonexistent state name (G#1) — remove during W4 migration.
- N tree, Alt dialogue buffer, wake-readout alignment: unbuilt (W4).

## Hardening batch (post-run, 2026-07-19) — predictions for next launch

Three live findings from the first W2 runs, fixed per owner green-light:

1. **Event-armed cycle gate.** New game / save load: NO "Cycle complete" summary,
   no focus suppression, no CycleTransition mode during boot (the old build did
   all three). A real End Cycle press: `[Cycle] pipeline armed` in the log, mode
   goes CycleTransition, summary speaks on return to Idle. The intro's own
   cycle-tutorial press counts as real — it should announce.
2. **Boot-sweep silence.** After "Main menu." / "On station.", NO game-driven
   focus announcements (no "PRESS TO START button", no "UPGRADE button,
   disabled", no marker names) until the first keypress or click —
   `[Focus] scene settled` marks the release. Dialogue auto-read and the
   "Continue" sole-choice announcement are unaffected (different channels).
   Intro consequence: Enter now fires on the opening continue (the misdetected
   listening mode was what swallowed it).
3. **Conversation events.** `[Dialogue] conversation lifecycle events
   subscribed` at load; dialogue mode now flips on the framework's own
   started/ended events — including non-input triggers (the cargo-container
   on-leave dialogue is the known test case). Any `[Dialogue] DIVERGENCE`
   warning = event flag and old poll disagree — report it; the poll gets
   dropped after one clean session.

## Failure signatures

- A mode misdetected (e.g. refusals firing on the station map) → log
  `[Mode]` lines and the KeyScope table are the first suspects.
- V numbers disagreeing with HUD → would contradict the closed single-store
  verdict; escalate immediately.
- Double-handling (a key both acting and refusing) → dispatch ordering bug.
