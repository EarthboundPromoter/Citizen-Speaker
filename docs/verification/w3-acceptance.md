# W3 focus/modality build — acceptance predictions (written pre-launch, 2026-07-19)

Per the W5 model. Built against focus-model.md (all rows owner-walked this
session) and verification/H-focus-assessment.md. Deploys on next game exit;
predictions apply to the launch after that. All wording provisional.

## What changed

ModeModel affordance precedence + event-driven window truth (WindowState),
FocusModel universal re-anchor, inventory designed idiom, cycle-end totals
string, node census announcements, options-menu review, warning-menu and QR
announcements, U/J live inside inventory. NOT in this build (awaiting the keymap
table eval): S and D rehoming, drive-log tab-swap key, N tree, drive-log review,
map-parity clock/drive detail on Space.

## Predicted behavior, next launch

1. **Fresh game, drive log never opened: no false Drive Log mode.** L on the
   station says "Station map..."; I toggles the inventory; U opens the character
   window with no prior refusal. The whole incident-7 cascade (stuck mode,
   wandering arrows, refuse-then-act mysteries) is structurally gone — window
   truth comes from the FSMs' own states plus the quest-log window's public
   isOpen, which are false at boot by construction.
2. **Wake dialogue (or any scene-beat conversation inside a cycle transition) is
   advanceable.** Mode flips to Dialogue on the framework's conversationStarted
   even mid-transition; Enter advances; R rereads; when it ends the transition
   resumes listening. Incident 9 unreproducible.
3. **Cycle end speaks totals:** "Cycle ended. Cycle N. Energy X... Condition Y,
   BAND..." then the dice line — absolute values only, no deltas. Then, at the
   first moment of full station control (after any wake dialogue), any node
   changes: "2 locations added." / "1 node removed." No announcement when
   nothing changed. Boot/baseline is silent.
4. **Dialogue focus loss is recoverable:** if focus is lost mid-conversation,
   Enter (empty selection) reselects the Continue Button — the variant-C anchor
   the game never restores on its own. Same universal re-anchor: Tutorial →
   dismiss button, DiceAllocation → current dice cursor, CharacterWindow →
   Upgrade Button, Station/ActionView → UI selector Reset (unchanged).
5. **Inventory rides its designed machinery:** I sends the strip FSM's own
   Activate/Deactivate (no more ITEM/DATA button clicks); mode announcements...
   NOTE: open/close have no dedicated announcement yet (the strip never had one);
   the mode name answers L and refusals. Up/Down swaps panels with the tab-swap
   SFX and speaks the rendered panel label (ITEM/DATA); Left/Right move natively
   between item cursors; U and J now work from inside the strip (the opening
   panel closes it — game choreography, listen for the sequence); Backspace
   closes designed.
6. **Options menu (pause) is fully speakable:** on open, "Options. Up and Down
   for settings..."; Up/Down speaks "SCENE TEXT SIZE, DEFAULT" style rows (row
   label + accent-colored current value); Left/Right clicks the neighboring
   value — the game's tab SFX and recolor are the confirmation, we speak the new
   value label; Enter on BACK closes. First open logs label colors per row
   ([Options] lines) — calibration data for the accent threshold.
7. **Pause warning menus** speak their rendered body + "QUIT or CANCEL." on open.
8. **Title QR (Update News)** speaks its rendered text + "CLOSE button." when
   opened (title visit needed to verify; title options menu path is still
   uncaptured — if the options review stays silent on the title screen, that's
   the known gap, log-collect the path).
9. **Divergence diagnostics:** `[WindowState] DIVERGENCE` lines mean alpha and
   event truth disagree — report, don't panic; alpha is no longer consulted for
   mode. `[NodeCensus] baseline` logs on each station load.

## Live fix during run 3 (deployed `f29b73e`) — prediction for next launch

**Container-exit trap dead.** Run 3 finding: `Autoplay Waiting` is a scheduling
flag with a designed leak (every character canvas's `Autoplay Wait` state sets it
true; the `Check Variables -> Off` exit never clears it) — it stranded the mode
in "Scene playing" while the game held the container's interactable slots and
waited for the player to LEAVE (our refusal blocked the exact Leave being waited
for). Autoplay listening now yields whenever the game holds an interactable
selection (selection identity, the game's own idiom); genuine cutscene lockouts
remain covered by the Input Pauser honor guard. Predicted: in the container
post-cycle beat, L says "At a location...", arrows walk slots, Backspace fires
the Leave Button. Watch for the reverse failure: keys acting during a true
cinematic that holds a stale selection — if seen, capture what was selected.

## Failure signatures

- Mode stuck in a window mode after closing it → a close-state name we didn't
  subscribe (Character UI Button close route beyond Reset/Gamepad UI, or
  inventory state family we missed) — the FsmSignals trace config
  (Debug.TraceFsmSignals) identifies it in one open/close cycle.
- I does nothing → FindFsm("Inventory", "Bottom UI") missed; log will show no
  strip events.
- Options review misreads current values → color threshold; the [Options]
  calibration lines carry the real numbers to tune against.
- Node census over/under-counts → Normalize() variant-dedup wrong for some
  canvas naming pattern; [NodeCensus] logs carry the counts.

## Re-run list (W2 remainder, now unblocked)

Tutorial text (passed once this session), die retraction verbatim, drive-log
close-key attribution, dialogue R/numbers pass, Z + dead keys, empty-Enter
re-anchor (now per-mode), real cycle-end summary (new string), cloud interior,
conversation-events divergence (one clean session → drop the poll).
