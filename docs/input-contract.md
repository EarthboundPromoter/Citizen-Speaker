# Input contract — the game's complete sanctioned input surface

Source: FSM corpus (verification brief F, INPUT SURFACE — exhaustive for the station
scene, 617 call sites). The game listens for exactly **22 Rewired actions**; nothing
else reaches game logic. Binding rule (build-plan W1): **every mod key must map to
one action's designed effect, or be a pure-read query.** Anything else is an
invented input schema — the class of bug this contract exists to kill.

This document is the ground-truth input to the W1→W2 keymap design session
(build-plan checkpoint). It has two parts: the contract itself, and the current
provisional binds audited against it.

## 1. The 22 actions

### Gameplay actions (bound to real hardware, watched by real FSMs)

- **`Back`** — universal cancel/dismiss/deselect: leaves dice allocation (Dice
  Gamepad System's own `Back`), deactivates item cursors, fires Leave Button,
  second trigger for the character-window toggle, dismisses skill detail.
  468 call sites — the most-watched action in the game.
- **`Inventory Toggle`** — opens/closes the inventory strip; also backs out of an
  item-cursor focus.
- **`Character Screen`** — character window open/close toggle (Character UI
  Button's own `Open` event, both directions).
- **`Drive Log`** — drive log open/close toggle; same button-FSM template as
  Character Screen.
- **`Scan Toggle`** — fires the Scan Button's `Click` from its idle/holding states.
- **`Pause`** — PAUSE FSM `Idle`→`Pause`, and unpause from paused states.
- **`Pause Back`** — pause-menu-scoped cancel (distinct from gameplay `Back`):
  backs out of Sure?/OPTIONS, unpauses.
- **`REROLL`** — fires `Reroll` on REROLL DICE (all its On states).
- **`Confirm`** — watched only by scene-root UI Reselector (`Idle`→`Pressed`), the
  "confirm pressed with nothing valid selected" recovery watcher. Actual
  activation is uGUI submit, not this action.

### Axes and scroll

- **`Scroll Axis`** — camera pan/zoom (Focus Rotator), rim jump-up/down
  double-press variants.
- **`Keyboard Scroll Axis`** — PC-only variant of the same Focus states.
- **`Selection Axis Vertical`** — device probe (Gamepad Manager), inventory item
  swapper, drive-log scroll area.
- **`Selection Axis Horizontal`** — device probe only.
- **`Rotate View`** — device probe only.
- **`Scroll Window`** — dialogue panel scrollbar.

### Device detection

- **`AnyButton/Joystick`** — Gamepad Manager mode switch to gamepad; credits skip.
- **`AnyButton/Mouse`** — Gamepad Manager mode switch to mouse.

### Dev-only (excluded from the sanctioned surface)

`DEBUG MENU`, `DEBUG BG Toggle`, `DEBUG Station Toggle`, `DEBUG Lights Toggle`,
`DEBUG UI Toggle`.

### Absences (as informative as presences)

- **No action opens cloud/hacking view** — entry is a uGUI `Clicked` on the node
  itself; keyboard entry necessarily goes through node-click emulation.
- **No per-window close action** distinct from `Back`/the window's own toggle.
- **No map action** — location navigation is the camera-zone system, not a window.
- Arrow navigation is not a Rewired action either: controller navigation is the
  input module driving uGUI move events over the designer-authored Selectable graph.
  The designed equivalents for the mod are `ExecuteEvents.moveHandler` (arrows) and
  submit (Enter) on the current selection.

## 2. Current provisional binds audited against the contract

All binds below are provisional input to the keymap design session, not constraints
on it (build-plan W1→W2 checkpoint).

### Mapped to a designed effect — conforming

- **Arrows / Enter** — uGUI move/submit on current selection (the designed
  navigation machinery).
- **Backspace** — `Back`: picker cancel via Dice Gamepad System's own `Back`
  event; character-window close via its designed toggle event; Leave Button
  otherwise.
- **I** — `Inventory Toggle` effect, via clicking the ITEM/DATA buttons.
- **U** — `Character Screen` effect, via clicking Character UI Button.
- **J** — `Drive Log` effect, via clicking Drive Log Button.
- **S** — `Scan Toggle` effect, via clicking Scan Button.
- **Shift+R** — `REROLL` effect, via clicking REROLL DICE.
- **Esc** — `Pause` (native — the game itself handles it; the mod adds nothing).
- **Number keys** — response pick = native click on the rendered response button
  (designed activation of a designed target).
- **T** — re-selects the tutorial continue button (designed selection machinery;
  Enter fires it natively).

### Pure-read queries and speech control — conforming by category

C / D / K (status, dice, clocks), Space (describe focused), F1 (help), F2 (repeat
dialogue), R (repeat speech), brackets (history), grave (stop speech). No game
input is synthesized.

### Mod-added navigation aids — selection-only, no game action exists

Tab (action cycling), L (world cycling), review-cursor arrows (character select /
tutorial / character window). These move EventSystem selection or a mod-owned
cursor; they synthesize no Rewired action. Sanctioned so long as selection respects
the Checker-variant table (brief E).

### Open items for the keymap session

- `Pause Back` is unmapped — inside the pause menu, Backspace currently falls
  through to generic click-path fallbacks ("Back Button"/"Close Button" name
  guesses) instead of the designed pause-scoped cancel.
- Key collisions to revisit: R (repeat speech) vs Shift+R (reroll); S (scan) sits
  next to arrow-adjacent WASD muscle memory; letter queries (C/D/K/T/I/U/J/L)
  are all unshifted single letters — audit against future per-mode scoping.
- I clicks ITEM/DATA buttons rather than firing the strip's own numbered-state
  toggle; per-mode behavior (open vs close vs back-out-of-cursor) should follow
  the `Inventory Toggle` designed semantics once the modality layer knows the mode.
- Cloud view: no action exists — decide the keyboard entry idiom (node-click
  emulation via L-cycle + Enter is the current de facto path).
- `Confirm`'s reselection-recovery role suggests the mod's "Enter with nothing
  selected" behavior should mirror it — currently undefined.
