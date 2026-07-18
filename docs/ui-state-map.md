# Citizen Sleeper UI state map

Draft 1 — assembled 2026-07-18 from static extraction of `level1` (UnityPy + typetree
generation against the game's Managed DLLs) cross-checked against live bridge
observation during test session 2. Items marked **UNVERIFIED** are serialized structure
we have not yet watched run; everything else was either observed live or read from
serialized wiring (button persistent calls, FSM state/event/variable strings).

Purpose: the factual basis for the mod's moment-modality layer. The mod must never
assume what the game expects; it derives the current mode from the anchors below and
announces mode transitions and per-mode affordances.

## 1. Input mode layer

Owner: scene root `Gamepad Manager` (FSM states: Switch or PC, Check Last State;
events Mouse/Gamepad; reads `Rewired.ControllerType`, hides/locks cursor).
Global FSM bool `Gamepad` tracks the result. The game has no keyboard UI input;
"gamepad mode" is what the mod piggybacks on. Mode can flip at runtime on input
device change (**UNVERIFIED** how that interacts with the mod's synthetic events).

## 2. Global gates (PlayMaker globals + controller FSM variables)

- `IntroComplete` — gates Drive System, Focus Rotator (GATED RIM / RIMGATE states),
  pause-menu autosave display. The intro withholds whole subsystems until done.
- `CycleCanEnd` — variable on the `Intro Sequence` FSM. The intro decides when the
  End Cycle button becomes usable. Almost certainly the gate that silently swallowed
  activation during session 2 (**UNVERIFIED** — confirm live: read End Cycle
  `Button.IsInteractable()` while intro is mid-script).
- `SlottedDiceGlobal` — read by the Leave Button FSM (leaving gated while dice slotted).
- `Player_Condition`, `Player_Energy`, `Player_Class`, `Player_UpgradePoints` —
  PlayMaker GLOBALS (underscore naming — this is why the earlier globals dump
  "came back negative"; we searched the wrong names). The devs' own DEBUG MENU FSM
  writes them (Condition/Energy/CRYO/All6s setters).
- Cycle Controller FSM locals: `Player Energy`, `Player Condition`, `Cycle Count`,
  `Starving`, `Die Condition`, `LightCycle`, `Intro Complete?` — a second readable
  copy with spaces in names; relation of copies **UNVERIFIED** (which is
  authoritative mid-cycle).

## 3. Modal layers, highest priority first

The mod's mode tracker should evaluate these top-down; first live layer wins.

1. **Pause** — root `PAUSE` FSM (Idle → Pause → Sure?/Options/Exit → Unpause;
   performs "DIalogue UI Unfocus"/"Dialogue UI focus" on the way in/out).
   Anchor: `PAUSE/Pause Canvas` active. Observed live.
2. **Breakdown** — `Letterbox Canvas/Breakdown UI` (static art + WIPER; logic lives
   in Cycle Controller's `Breakdown?` branch). Anchor: Breakdown UI visible.
   **UNVERIFIED** (never reached).
3. **Tutorial modal** — `Letterbox Canvas/Tutorial System` (Intro/Character/Hacking/
   Breakdown tutorial triggers + Input Pauser FSM). Anchor: tutorial canvas effective
   alpha > 0. Observed live. Note session 2 evidence: a tutorial CAN be up while
   station input stays live (owner dismissed the modal-gating hypothesis) — Input
   Pauser's actual scope **UNVERIFIED**.
4. **Dialogue / conversation** — `CS Dialogue Manager` panel (Dialogue System for
   Unity + Ink via PlaymakerInkProxy). Sub-states observed live: subtitle flow with
   sole Continue; response menu (choice list). The Intro Sequence FSM drives
   conversations during the intro (states: Text Intro, Response Menu, End Intro
   Conversation, Camera Transition; vars: Dragos State, DragosSceneCount, Conversation).
   Anchor: Dialogue Panel active.
5. **Dice allocation** — `Top UI/Dice UI/Dice Gamepad System` FSM
   (Setup/Off/Activate/Active/Click/Slotted/Back/Reselector/RefocusUI/ForceUnslotDice).
   Anchor: any `Dice Cursor 1..5` active, or Dice Gamepad System FSM in Active.
   Confirmed inactive outside allocation (session 2). Flow **UNVERIFIED** live.
6. **Scan mode** — `Top UI/Scan Button` FSM (Normal Idle ↔ Scan Mode Transition ↔
   Scan Idle; also gated by Intro Check). Anchor: Scan Button FSM state.
   **UNVERIFIED** live.
7. **Character screen** — `Character UI/Character UI Button` FSM (Open/Close/Back,
   "Character Screen", flags UpgradeAvailable) + `Character Window` (class portrait,
   upgrade tracker, skill list). Anchor: Character Window visible. **UNVERIFIED**.
8. **Inventory strip** — `Bottom UI/Inventory` FSM (Setup/Item/Data/Swap/
   "D to I"/"I to D"/Activate/Deactivate; Rewired action "Inventory Toggle").
   Two sub-modes: ITEM and DATA. Anchor: Inventory FSM state. Observed live
   (I key, session 2) — semi-modal: station focus persisted alongside.
9. **Station free-roam** — default. Sub-structure: location action groups
   (`1_Action Groups`, one child per location, gated via alpha/interactable/
   blocksRaycasts by the Action Groups FSM) vs hacking action groups
   (`2_Hacking Action Groups`, ~40 network-node groups, all inactive until a
   hack starts — **UNVERIFIED**). World zones: Hub/Rim/Greenway
   (Location Controller FSM: Filter/Rim/Greenway/Hub/transits; Focus Rotator
   gates Rim behind IntroComplete).

## 4. Selection machinery (why foreign focus gets fought)

- `UI Reselector` (scene root, inactive by default): FSM Idle/Pressed/Check Selected/
  Reset Selection — pressing Confirm with nothing valid selected resets selection.
- Per-button "Gamepad Checker" FSMs (e.g. PAUSE/RESUME, dice slot buttons) that
  re-select their owner ("Set Selected", "Reselect") when conditions change.
- `RefocusUI` event in Dice Gamepad System.
These are the mechanisms behind the established core input principle: route arrows
through the game's own move handlers; never set selection to arbitrary objects.

## 5. Read targets for announcements (game-rendered data)

- Condition: `Top UI/Energy UI/Condition System` FSM — reads `Player_Condition`,
  renders text (MOOD/DECLINING states, "Declining (20+)") + bar fill. The HUD text
  observed in session 2's screenshot ("FLICKERING") comes from this system.
- Energy: `Top UI/Energy UI/Energy Bar System` FSM — reads `Player_Energy`, bar only
  (no text observed — announcement should derive number from the global).
- Cycle count, starving, die condition: Cycle Controller FSM locals.
- Dice: `Top UI/Dice UI/Dice Slot 1..5` + existing per-die FSM `DiceValue` reads.
- Drives/trackers: `Drive System` (intro-gated), `Top UI/Ghost Trackers`
  (Hunter/Killer/Gardener stages via C_* globals). **UNVERIFIED**.
- Perks: `Perks Manager` FSM (skill-action-performed events; INTUIT_PERKS save check
  on REROLL DICE).

## 6. End Cycle activation (resolved session 2, static + live)

`Dice Slot Button` (per home location; only current home's is active) carries a plain
uGUI Button whose single persistent onClick call is `SendEvent("EndCycle")` targeted
at the Cycle Controller FSM. Scene-wide, only Cycle Controller consumes `EndCycle`.
Plain click/submit IS the sanctioned modality — but interactability is gated during
the intro (see `CycleCanEnd`). The five home variants (Empty Container, Repaired
Unit, Hypha Dorm, Briar Base Camp, Capsule 0451) all wire identically.

## 6b. Dice allocation mechanics (static extraction, session 2 afternoon)

Two action archetypes exist, confirmed by component census:

- **Dice actions** (Manual Salvage etc.): carry `Gamepad Dice Slot` + `Action Controller`
  (+ mouse drag targets). No `Dice Slot Button`.
- **Plain-activation actions** (End Cycle homes only): carry `Dice Slot Button`
  (onClick → SendEvent, section 6). No gamepad dice machinery.

The gamepad allocation chain, per serialized FSM states:

1. `Gamepad Dice Slot` is a standard uGUI **Button** (component census: Image, Button
   whose onClick persistent call is `SendEvent("Click")` to its own FSM, an
   event-relay component firing `Activate`/`Deactivate` on focus, the FSM, an
   Animator). Its FSM: Focus → Idle → **Click** → Set Slot Pos → Select Dice →
   (waits) → **Slotted** → Reset / Unslot Die / Refocus. On Click it coordinates
   with the Leave Button and sends **Activate** to Dice Gamepad System. Therefore
   plain uGUI submit (the mod's existing Enter) is the sanctioned entry into
   allocation — same convention as every other button in the game.
2. `Dice Gamepad System` FSM: Setup/Off → **Activate** → Active. In Active it
   activates the five `Dice Cursor N` objects and routes Back/Click/Slotted/
   Reselector/RefocusUI events. ForceUnslotDice resets allocation.
3. `Dice Cursor N` FSM (per die): Check Die is On → Idle → **Click** → Check if Used
   → (Used / Used Animation dead-end for spent dice) → Select Dice → **Jump to Slot**
   → Slot Die (uses `Selected Slot Position` var) → fires Slotted back to the slot.
4. `Action Controller` FSM (per action, ~150KB blob — the outcome engine): consumes
   DiceSlotted/SlottedDice, sets `SlottedDiceGlobal`, resolves outcomes, ticks
   positive/negative clocks, sets ActionComplete, renders outcome text
   (CSUI_ACTION_RELATIONSHIP_INPUT_DICE table entries).

Mouse-mode allocation appears to be pointer-DRAG based (cursor FSMs carry
Gamepad Drag / DragReset states; dice are dragged to slots) — **keyboard cannot
synthesize this**; the gamepad chain is the keyboard-drivable path.

**The picker has a NATIVE uGUI focus model.** Dice Cursor N component census: Image +
standard uGUI Button (Normal/Highlighted/Pressed/Selected/Disabled transitions,
onClick → SendEvent) + FSM + Animator. The cursor FSM performs
`UiSetSelectedGameObject` — the game moves EventSystem selection onto cursors when
the picker opens. D-pad = uGUI navigation between cursor Buttons; A = uGUI submit.
Consequence: the mod's existing arrows (moveHandler on current selection) and Enter
(submit) drive allocation natively; NO new input machinery is needed — only
announcements (mode transition, per-die focus description, Slotted/Back feedback,
gated-control diagnostics). **UNVERIFIED** live; validate at next session.

## 6c. Focus-model doctrine (owner-set, session 2 afternoon)

Three tiers, in order of preference:
1. **Native selection exists** → ride it. Arrows/Enter through the game's own
   navigation and submit; mod only announces.
2. **Native selectables exist but nothing is selected** (dead focus, e.g. arriving
   at a location) → an engage key sets selection to the game's OWN anchor — the same
   target the game's `UiSetSelectedGameObject` calls use. Never invent selection
   targets; the reselector machinery (section 4) fights foreign selections.
3. **No native selectables** (static overlays: tutorials, reviews) → mod-side
   review walker (TutorialReview / CharacterSelect pattern).

Numeric direct-pick idioms are rejected for anything the game gives a focus model.

## 7. Dev-side helpers found in scene (not for player builds)

- `DEBUG MENU` (Letterbox Canvas, inactive): setters for CRYO/100CRYO, All6s dice,
  Condition, Energy, Reroll, Upgrade, ALLITEMS — writes the Player_* globals.
- `DEBUG Intro Skipper` (scene root, inactive) — candidate for producing a clean
  post-intro test state without replaying the intro (**UNVERIFIED** what it sets;
  inspect before use — it may not set every flag a real playthrough would).
- `DEBUG Selection Tracker`, `DEBUG - R Tracker`, `DEBUG Capture`, `FPS`.

## 8. Open validation tasks (next live session)

1. Read End Cycle `Button.IsInteractable()` during intro; confirm `CycleCanEnd` gating
   and its set-point in the Intro Sequence FSM.
2. Trace one full sanctioned end-cycle (post-intro) end to end; capture the event
   order for the mode tracker's cycle-transition suppression window (report 3).
3. Exercise Dice Gamepad System live: Activate → Cursor → Click → Slotted (report 6).
4. Determine Input Pauser's actual gating scope (tutorial layer).
5. Verify whether the broken screen (session 2 close) reproduces on the suspect save,
   and whether DEBUG Intro Skipper yields a usable clean state.
6. Confirm Player_* globals are readable via FsmVariables.GlobalVariables at runtime
   (underscore names).
