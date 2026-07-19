# F — Input surface and mode machinery (FSM corpus verification)

Source: `tools/analysis/corpus/fsm-census.jsonl` (station scene, 4,948 FSMs, one
observation-only game-side dump — full state/action/transition graphs, not the
ASCII-run fallback earlier verification docs (B, C) had to use) and
`fsm-census-title.jsonl` (title/main-menu scene, 29 FSMs). Streamed with one-off
Python scripts in the scratchpad (not repo) — every finding below is a direct
read of a `states[].actions[].params` or `states[].transitions` value, cited by
FSM path + state name. Nothing here is live-observed; it supersedes B/C's
ASCII-extraction guesses wherever the two disagree (flagged explicitly below).

No narrative prose is quoted anywhere in this document — only structural
identifiers (FSM paths, state/event/variable names, localization keys).

## INPUT SURFACE

Every `Rewired*` action in the corpus, 617 call sites, exactly **22 distinct
Rewired action names** — this is the complete list of inputs the game itself
listens for. Anything the mod wants to synthesize or rebind should map onto one
of these.

Gameplay-facing actions (bound to real hardware, watched by real FSMs):

- **`Back`** — 468 call sites, by far the most-watched action, present on
  ~285 distinct FSM paths. It is the universal "cancel/dismiss/deselect"
  signal: every `Gamepad Dice Slot`/`Dice Cursor N` uses it to fire
  `DragReset`/`Reset` during allocation; `Dice Gamepad System` itself uses it
  (`Active`→event `Back`, `Slotted`→event `Back`) to leave allocation; every
  `Item Cursor` under `Inventory` (`ITEM Inventory UI`/`DATA Inventory UI`) uses
  it to fire `Deactivate`; `Leave Button` uses it to fire `Leave` from `Idle`/
  `Highlight`/`Continue`/`Highlight 2`; `Character UI Button`'s `Open` state
  watches it (`actionName: Back`) and re-fires the FSM's own `Open` event —
  i.e. `Back` is a second physical trigger for the character-window
  open/close toggle, alongside the dedicated `Character Screen` action (see
  Lifecycles). `Character Window/SKILL List/<SKILL>` rows watch it in their
  `Confirm?` sub-states to fire `Back` (skill-detail dismiss).
- **`Inventory Toggle`** — 56 sites. Watched by `Bottom UI/Inventory` itself
  (fires `Activate`/`Deactivate` depending on which numbered sub-state it's
  in) and by every `Item Cursor` under both `ITEM Inventory UI` and
  `DATA Inventory UI` (`Off`→`Activate`, `On`→`Deactivate` — the same button
  also backs out of an item-cursor focus).
- **`Character Screen`** — 2 sites, both on `Character UI Button`
  (`Idle`/`Open` states, both firing the FSM's own `Open` event) — the
  primary open/close toggle button for the character window (see Lifecycles).
- **`Drive Log`** — 2 sites, both on `Drive System/Drive Log Button`
  (`Idle`/`Open`, both firing the FSM's own `Open` event) — identical toggle
  idiom to `Character Screen`/`Character UI Button` (see Lifecycles).
- **`Scan Toggle`** — 3 sites, all on `Top UI/Scan Button`
  (`Holding`/`Normal Idle`/`Scan Idle`, all firing `Click`).
- **`Pause`** — 5 sites on `PAUSE` (`Idle`→`Pause`, `Pause`/`Pause 3`→
  `Unpause`, `OPTIONS`/`OPTIONS 2`→`Back`).
- **`Pause Back`** — 7 sites, also on `PAUSE` (`Pause`/`Pause 3`→`Unpause`,
  `Sure?`/`Sure? 2`/`Sure? 3`/`OPTIONS`→`Back`) — a second, pause-menu-scoped
  cancel action, distinct from the gameplay `Back` above (this is the
  Input-Pauser/`PAUSE`-remap target name documented in report C — confirmed
  here as a real, separately-bound Rewired action, not just a remap label).
- **`REROLL`** — 6 sites, all on `Top UI/Dice UI/REROLL DICE`
  (`On`..`On 6`, all firing `Reroll`).
- **`Confirm`** — 1 site, on scene-root `UI Reselector`
  (`Idle`→`Pressed`) — the "press confirm with nothing valid selected"
  watcher documented in `ui-state-map.md` section 4.
- **Axes/scroll**: `Scroll Axis` (17 sites — `Focus Rotator/Focus/Station
  Orbital Control` camera pan/zoom, plus `Focus Rotator/Focus`'s
  `GATED RIM`/`RIM` double-press-down variants firing `Jump Up`/`Jump Down`),
  `Keyboard Scroll Axis` (5 sites, same `Focus Rotator/Focus` states, PC-only
  variant), `Selection Axis Vertical` (7 sites — `Gamepad Manager` device-type
  probe, `Inventory/Gamepad Data / Item Swapper` `Idle`→`Swap`, and the
  Drive Log's own scroll area `Scroll Area` states `Scrolling`/`Wait for Up`/
  `Wait for Up 2`→`Scroll Down`/`Scroll Up`/`Ready`), `Selection Axis
  Horizontal` (1 site, `Gamepad Manager` probe), `Rotate View` (1 site,
  `Gamepad Manager` probe), `Scroll Window` (2 sites, dialogue panel's
  `Scrollbar` `Scrolling`/`Scrolling 2` states — title-scene copy too).
- **`AnyButton/Joystick`** and **`AnyButton/Mouse`** (`RewiredGetAnyButtonDown`,
  device-agnostic) — `Gamepad Manager` uses both to detect a controller/mouse
  switch (`Mouse`→event `Gamepad` or `Mouse`); `PAUSE` and the Tutorial
  System's `Input Pauser` use `RewiredPlayerSetControllerMapsEnabled` (not a
  button read, a controller-map toggle — this is the axis/button-name remap
  mechanism report C already fully mapped) on entering/leaving their paused
  states; `Ending Controller/End Credits` uses `AnyButton` to fire
  `SKip On` (credits skip).
- **Dev-only** (not player-facing, excluded from the mod's sanctioned-input
  surface): `DEBUG MENU` (`Letterbox Canvas/DEBUG MENU`, fires `Debug`/
  `Activate`), `DEBUG BG Toggle`/`DEBUG Station Toggle`/`DEBUG Lights Toggle`/
  `DEBUG UI Toggle` (all on scene-root `DEBUG TOGGLES`).

This list is exhaustive for the station scene — there is no Rewired action
referenced anywhere in the corpus that isn't in the 22 above. Absence is as
informative as presence: there is no separate "map" input, no dedicated
cloud/hacking-view toggle action (see Lifecycles — that surface is entered by
a uGUI click event, not a bound button), and no per-window "close" action
distinct from `Back`/the window's own open-toggle action.

## MODE MACHINERY

For each flag: every `SetBoolValue`/`SetFsmBool` write found via `$FlagName`
references, grouped by the FSM template that performs the write (many are
one-per-location-instance templates, not one central manager).

- **`Gamepad`** — 3 write sites, all on scene-root `Gamepad Manager`:
  state `Gamepad`→`True` (transition `Mouse -> Mouse`), state `Mouse`→`False`
  (transition `Gamepad -> Scene Active?`), state `Scene Active?`→`True`
  (transitions `Gamepad -> Gamepad`, `Mouse -> Mouse`). Single owner, matches
  report C exactly. 1,074 read sites (`BoolTest`) scene-wide confirm it as the
  most-consulted flag in the game.
- **`CycleCanEnd`** — 7 write sites: `Cycle Controller` (`Refresh End Cycle
  Bool`→`False`, transition `FINISHED -> Garbage Truck Truck Truck`),
  `Leave Button` (`Leave Action`→`True`, `Transit`→`True`), `Intro Sequence`
  (`Dragos State`→`True`, `Cycle Tutorial`→`True`, `Idle`→`True`), `DEBUG -
  Global Variable Setter` (`Player (DEBUG)`→`True`). Reader side: all 5 home
  `End Cycle Action` instances (Empty Container, Hypha Dorm, Capsule 0451,
  Briar Base Camp, Repaired Unit) sit in a state literally named `Off` and
  `BoolTest` this flag, transitioning to state `On` only when true — **this
  resolves `ui-state-map.md`'s open validation task 1 structurally**: the
  gate is real, single-sourced from `Cycle Controller`/`Intro Sequence`, and
  each home's End Cycle button's own `Off`→`On` state pair is the
  interactability switch. Still not live-confirmed (that needs watching
  `Button.IsInteractable()` during an actual intro run), but the mechanism
  is no longer inferred, it's read directly.
- **`Hacking?`** — 100 write sites, template-shaped: the shared `Location
  Button` leaf FSM (49 node instances under `.../Hacking UI/...`) writes
  `True` in state `Camera Transition` (transition `FINISHED -> Active`) and
  `False` in state `UI Camera On` (transition `FINISHED -> Idle`); `Leave
  Button` writes `False` in `Leave Action` and `Transit`. No central
  "Hacking Manager" exists — confirmed structurally now with the exact
  entry/exit event names (see Lifecycles).
- **`Action View?`** — 762 hits, 632 writes, same `Location Button` template
  (227 node instances this time — every node writes both `Hacking?` on the
  49-node hacking subset and `Action View?` on the full 227-node set,
  consistent with `Action View?` being the broader "an action/cloud-node
  panel currently has focus" signal that `PAUSE` also consults): `True` in
  `Camera Transition` (transition `FINISHED -> Ambient SFX On?`), `False` in
  `Camera Transition 2` (`FINISHED -> Autoplay Waiting?`) and `Camera
  Transition 3` (`FINISHED -> Enable Location`).
- **`Scenes Active?`** — 227 writes across two template families: the
  one-shot autoplay character-cutscene canvas template (`<Name> Canvas`,
  sampled instances: Feng Canvas 1/2/3, Peake Refuge Canvas 1/2/3, Helene
  Canvas, Dragos Canvas, Bliss Canvas, Riko Canvas (END), Sabine Canvas,
  Castor Canvas, Castor Hooded Canvas, Lem & Mina Canvas 1/2, Peake Canvas
  1/2, Rabiah Canvas — dozens of instances, identical wiring: `True` on
  `Click to Play` (`everyFrame: True` — asserted continuously while playing),
  `False` on `Off`/`Scene Complete`/`Neg Scene Checker`/`ELIMINATED`) and the
  `Quality Control Switch`/`Peake Note Switch` one-shot-trigger template
  (`Kill`→`False` only). `Intro Sequence` also writes it directly at several
  named beats (`Dragos State`, `Cycle Tutorial`→`False`; `Action Tutorial`,
  `Dice + Energy Tutorial 3`→`True`).
- **`Autoplay Waiting`** — 258 writes, same two families as `Scenes Active?`
  plus the much larger `One Shot <Name>` one-shot-scene template (~90
  distinct one-shot instances found, e.g. `One Shot Hunter`, `One Shot Killer`,
  `One Shot Scan Navigator`, `One Shot Yannick end`, plus many single-instance
  named beats): `True` on `Autoplay Wait` (character-canvas variant transition
  `Autoplay -> Autoplay`; one-shot variant transition `Autoplay -> Scene
  Queue Check`), `False` on `Autoplay`/`Scene` (transition `Scene Complete ->
  Pos Neg Check` or `Reset -> Outcome`).
- **`Data?`** — 58 hits, 10 writes, single owner `Bottom UI/Inventory`:
  `True` in `Data`/`Data 2`/`Data 3`/`Data 4`/`Data 5`, `False` in `Item`/
  `Item 2`/`Item 3`/`Item 4`/`Item 5` — the ITEM/DATA sub-mode flag for the
  inventory strip, confirmed exactly per `ui-state-map.md` section 3.8.

Two flags initially suspected as PlayMaker globals turned out to be something
else entirely on direct inspection (**correction to prior docs**, see
SURPRISES):

- **`LightCycle`** and **`Breakdown?`** are not consulted via `$Name` FSM
  references anywhere except as the *target* of a `GetVariable` (Dialogue
  System Lua) call whose `storeFloatResult` happens to be `$LightCycle`/
  `$Breakdown?` — i.e. these are local FSM float variables fed by a Lua
  read, not global bools. `LightCycle` (Lua, float) is read on `Cycle
  Controller`'s `Get Light State + Swap`/`Get Light State + Swap 2` states,
  mirrored into local `$LightCycle`, then `FloatSwitch`-branched into
  `Light 1`..`Light 4` events; also directly written (as a plain FSM local,
  not Lua) on `DEBUG Capture/Light Controller` and `Cycle Controller`'s
  `Bloom Edge`/`Slice`/`Reverse`/`Silohuette` states. `Breakdown?` is a
  `Cycle Controller`-local float populated from Lua `BREAKDOWN_CYCLE` (state
  `Breakdown?`) and `FloatCompare`d against 0.

`SlottedDiceGlobal` (2,168 hits — `SendEvent`/`SetGameObject`/`GetChild`/
`SetFsmVariable`, not `SetBoolValue`) is a GameObject-typed pointer variable
(which die/slot is currently occupied), not a bool mode flag — its shape
matches `ui-state-map.md`'s existing description; not re-derived in depth
here since it's dice-allocation plumbing, not a top-level mode.

## LIFECYCLES

Every openable surface in the corpus follows one of two idioms: a **shared
window-toggle-button template** (own-Rewired-action, own-`Open`-event,
symmetric open/close), or **per-node click entry** (no dedicated toggle
button; entry is a uGUI `Clicked` event on the target object itself).

### Character window (confirms/extends the already-known lifecycle)

`Letterbox Canvas/Character UI/Character UI Button`. Rewired watchers:
`Character Screen` (states `Idle`, `Open`) and `Back` (state `Open`) — both
fire the FSM's own `Open` event, so pressing either while the window is
already open closes it (toggle idiom).

- **Open** (state `Open`): clears `UpgradeAvailable` (Lua `SetVariable`,
  `boolValue: False`), deactivates `Locations`/`Characters`/`Character`/
  `Class Name`/`Highlight`/`Top UI`/`Bottom UI` siblings, sets
  `SetAnimatorBool` `Character Window`.`Active = True` — **this animator bool
  is the durable open-state signal** — and sends `event:Deactivate` to
  `GameObject:Inventory` (mutual exclusion with the inventory strip).
- **Close** (state `Close`, entered by the same `Open` event firing again):
  `SetAnimatorBool` `Active = False`, sends `event:Save` to `$Saver` (a
  GameObject-typed FSM variable pointing at the save system), plays a close
  sound, `Wait 0.25`.
- **Reset** (transition `FINISHED -> Reset`, entered from `Close`):
  reactivates the siblings hidden on open, sends `event:Reset` to
  `GameObject:UI selector`, broadcasts `event:RefocusUI`.
- **Gamepad Checker**: `BoolTest $Gamepad` — if true, sends `RefocusUI` and
  reactivates the UI selector before returning to `Set Up`/`Idle`.

### Drive log — same template, different target

`Letterbox Canvas/Drive System/Drive Log Button`. Rewired watcher: `Drive
Log` (states `Idle`, `Open`), same own-`Open`-event toggle idiom.

- **Open** (state `SFX`→`Gamepad Checker 2`→state `Open`): `CallMethod`
  `UnityUIQuestLogWindow:CS Drive Log`.`Open()` — **the durable open-state
  signal here is the PixelCrushers `UnityUIQuestLogWindow` component's own
  open state**, not an animator bool. Also sends `event:Deactivate` to
  `GameObject:Inventory` — same cross-window exclusion as the character
  window.
- **Close** (state `Close`, entered by `Open` firing again): plays
  `event:/UI Sounds/Drive Close Drive Menu`, `CallMethod`
  `UnityUIQuestLogWindow:CS Drive Log`.`Close()`.
- Same `Gamepad Checker`/`RefocusUI` reselection tail as the character
  window.

**Structural finding**: Character UI Button and Drive Log Button are the
*same underlying button-FSM template* (identical event set —`FINISHED`,
`CloseAction`, `Deactivate`, `Disappear`, `ForceUnslotDice`, `Leave`,
`MouseOff`, `MouseOver`, `Open`, `RefocusUI`, `Reset` — near-identical state
names, identical `Gamepad Checker` tail), just pointed at different target
systems (an Animator bool vs. a Dialogue-System window component). Any
mod-side "window lifecycle" abstraction can key off this one template rather
than treating each window as bespoke.

### Inventory (confirms `ui-state-map.md` section 3.8, no changes)

`Letterbox Canvas/Bottom UI/Inventory`, `startState: Setup`. `Setup`
branches on controller type (`Gamepad -> Item`, `PC -> Xbox` → `Xbox ->
Item`, `PC -> Item 2`). Numbered `Item N`/`Data N` state pairs (1 through 5)
are per-input-context variants (mouse vs. gamepad vs. with/without an item
selected — `Item 3`/`Data 3` add `Mouse -> Item 2`/`Data 2`, `Item 4`/`Data
4`/`Item 5`/`Data 5` add `Deactivate` transitions with `UiGetSelectedGameObject`
+ `StringCompare` guards). `Rewired Inventory Toggle` on each numbered state
fires `Activate` (lower-numbered states) or `Deactivate` (higher-numbered).
`I to D`/`D to I` swap states just play a sound and reposition — no window
close, this is the ITEM/DATA sub-tab swap. No mutual-exclusion `SendEvent`
to other windows found on Inventory itself (Character/Drive Log both push
*it* closed, it doesn't reciprocate).

### Cloud/hacking view — per-node entry, no central toggle (confirms + extends report C)

No Rewired action opens cloud view (absent from the 22-action list above).
Entry is a native uGUI click on the target node itself: every `Location
Button` under `.../Hacking UI/...` (template, ~49 hacking-flagged instances)
has:

- `Idle` → transition `Clicked -> Camera Transition` (also `MouseOn ->
  MouseOn`, `Disable -> Disable`).
- `Camera Transition` → sets `Hacking? = True` and `Action View? = True`
  (both `SetBoolValue`), plays a sound, `VirtualCameraSetPriority`, sends an
  event, `Wait`, transition `FINISHED -> Active`.
- `Active` → transition `CloseAction -> Camera Transition 2` (exit trigger).
- `Camera Transition 2` → `VirtualCameraSetPriority` back, sets the flags
  `False`, transition `FINISHED -> UI Camera On`.
- `UI Camera On` → two `SendEvent`s, transition `FINISHED -> Idle`.

Confirms report C's "no central Hacking Manager, toggling is per-node" with
the exact event chain (`Clicked` in, `CloseAction` out) that C could only
infer.

### Pause and scan

Both match `ui-state-map.md`/report C exactly on direct read of the full
corpus — no corrections. `PAUSE` root FSM: `Idle`→(`Pause`)→`Pause`, `Sure?`/
`Sure? 2`/`Sure? 3`, `OPTIONS`/`OPTIONS 2`, `Unpause`→`Idle`, watching
`Pause`/`Pause Back` (see Input Surface). `Top UI/Scan Button`: watches `Scan
Toggle`, fires `Click` from `Holding`/`Normal Idle`/`Scan Idle`.

### Map/locations — no separate modal surface

`Location Controller` (scene root): `Startup Order -> Filter`, then
`Filter` branches on `Rim`/`Greenway`/`Hub` events into three loop states
with `Transition to <Zone>` sub-states. No distinct "map" window exists in
the corpus — location navigation *is* this camera-zone system (confirms
report C's `Focus`/`Location Controller` finding; nothing new to add).

## LUA STORE

**397 distinct Dialogue-System Lua variable names** found via `GetVariable`
(36,820 call sites) / `SetVariable` (19,863 call sites) actions — these are
literally-named PlayMaker actions from the PixelCrushers Dialogue System
PlayMaker-integration package; params are flat (`variableName`,
`storeStringResult`/`storeFloatResult`/`storeIntResult`/`storeBoolResult` for
reads, `stringValue`/`floatValue`/`intValue`/`boolValue` for writes) — no
`$`-prefix on `variableName` itself, unlike ordinary FSM/global variable
references elsewhere in the corpus, which is how these are distinguishable
from PlayMaker globals at a glance.

### Cycle-number verdict (the brief's headline question)

Two distinct mechanisms, not one:

1. **Live in-game cycle count**: Lua variable **`Cycle`** (float). **Exactly
   one writer**: `Cycle Controller` (1 write call site). **4,628 read call
   sites across 1,549 distinct FSM paths** — essentially every clock/step-
   clock FSM in the game (`Branch Access Clock`, every `N Step Clock`/`N
   Step Accruing Clock` under every `1_Action Groups/.../*Actions/*Clock`)
   reads it every frame to compute its own countdown. This is the
   authoritative, single-sourced current-cycle value; the mod's C-status
   query should read this Lua variable (`Cycle`) directly rather than
   deriving it from Cycle Controller's own locals.
2. **Save-slot menu labels** (title/main-menu scene, `MAIN MENU/Demo
   Menu/Slot Menu/Slot 1`/`Slot 2`/`Slot 3`): each slot FSM has its own
   *local* string variable named `Cycle` (plus `Class`, `Save Description`,
   `Save Description Formatted`, `Class and Cycle` array), populated in
   state `Filled` via `CallMethod` → `GetSaveDescription` (a save-system API
   call, not a Lua read — this runs before any save is loaded, so the live
   Lua database isn't available yet), then `StringSplit` on `_` and
   `ArrayGet` to pull fields out of the returned descriptor string, with a
   `CSUI_`-prefixed localization lookup built from one of the split parts
   for the class label. (Some param values in this specific FSM's `Filled`
   state came back `null` from ASCII-run recovery — a known corpus
   limitation on this one blob, per `tools/analysis/README.md` — so the
   exact string-split indices used for the cycle portion aren't fully
   recovered; the mechanism and its inputs are, though.) This is a
   **different, save-file-scoped mechanism** from #1 — don't conflate them.

This directly resolves `build-plan.md` section 7's parked "Cycle number...
Dialogue System Lua variable is the lead candidate" — confirmed, named
(`Cycle`), and single-sourced.

### Player_* stats — resolves a previously-UNRESOLVED question

`ui-state-map.md` section 2 and report C both treated `Player_Condition`,
`Player_Energy`, `Player_Class`, `Player_UpgradePoints` as **PlayMaker
GLOBALS** (underscore naming) with a separate, possibly-authoritative set of
**spaced-name FSM locals** on `Cycle Controller` (`Player Energy`, `Player
Condition`) of unclear relationship. Direct inspection of every `GetVariable`/
`SetVariable` call site shows this is wrong on the mechanism (right on the
existence of both names): **`Player_Energy`/`Player_Condition`/`Player_Class`/
`Player_UpgradePoints`/`Player_DrivePoints`/`Player_Bits` are Dialogue-System
Lua variables**, not PlayMaker globals — there is no `$Player_Energy`-style
FSM reference anywhere in the corpus. `Cycle Controller`'s spaced-name locals
(`$Player Energy` etc.) are populated by reading the Lua variable via
`GetVariable` and mirrored back by `SetVariable` — an ordinary read/write
mirror, not two independent stores. The station HUD widgets
(`Top UI/Energy UI/Condition System`, `Energy UI/Energy Bar System`) read
*and write* the same Lua variables directly, alongside `Cycle Controller` and
the dev-only `DEBUG MENU`/`DEBUG - Global Variable Setter`/`DEBUG Energy
Tester`. **Verdict: single authoritative store (Dialogue System Lua
database), read via `PixelCrushers.DialogueSystem.DialogueLua` (or the
`Lua.Run`/`DialogueLua.GetVariable` API) using the underscore names — this
should be the mod's canonical energy/condition read, not a `FsmVariables`
lookup on any FSM.**

### Domain groups (representative, not exhaustive — see `lua_vars.txt`
structure for the full 397)

- **Player stats**: `Player_Energy`, `Player_Condition`, `Player_Class`,
  `Player_UpgradePoints`, `Player_DrivePoints`, `Player_Bits`.
- **Cycle/clocks**: `Cycle` (see verdict above), `CycleSceneCount` (read by
  `Cycle Controller`, `Cycle Controller/Cycle Scene Manager`, `Intro
  Sequence`, `Top UI/Scan Button`), `DeathCycle`, `GOTCYCLEPURGE`.
- **Breakdowns/mode floats**: `BREAKDOWN_CYCLE` (feeds local `Breakdown?`),
  `LightCycle` (feeds local `$LightCycle`, lighting-phase switch).
- **Drive/story clocks**: the `C_*` convention confirmed real and large (dozens
  of entries, e.g. `C_ONELASTJOB`, `C_RIGGEDANDREADY`, `C_ALGAEGROWTH_CYCLE_
  DISCOVERED`, `C_CORDONSWEEP_CYCLE_DISCOVERED`, `C_SALVAGESORTIE_CYCLE_
  DISCOVERED`) — matches report C's "real, not Ghost-Tracker-confirmed" verdict;
  a dedicated `Top UI/Ghost Trackers`-scoped pass would still be needed to
  isolate the Hunter/Killer/Gardener-specific subset (not attempted here,
  out of scope).
- **Scan/UI flags**: `ScanActive` (single reader/writer pair, both on `Top
  UI/Scan Button`).
- **Intro gating**: `IntroComplete` — **correction to report C**: this is
  *also* purely a Dialogue-System Lua variable, not a PlayMaker global read
  via `FsmVariables.GlobalVariables` as report C's VERDICTS section assumed.
  No `$IntroComplete` FSM-variable reference exists anywhere in the corpus;
  every touch point is a `GetVariable`/`SetVariable` Lua call. The mod should
  read it via the Lua API, same as the Player_* stats above.

## LOCALIZATION

Two text tables, both accessed exclusively via `GetLocalizedText`
(10,999 call sites total):

- **`TextTable:CS-TextTable UI`** (1,805 lookups) — the structural UI-chrome
  table, `CSUI_`-prefixed key convention. 1,685 literal (compile-time-known)
  keys, 22 distinct: dominated by the action-panel status quartet
  (`CSUI_ACTION_RELATIONSHIP_WORKING` 575, `..._ACTION_COMPLETE` 455,
  `..._ACTION_UNAVAILABLE` 333, `..._INPUT_DICE` 119) and the hacking-action
  quintet (`CSUI_ACTION_HACKING_UNLOCKED`/`LOCKED`/`WORKING`/
  `COMPLETE_SEQUENCE`/`DATA_EXTRACTED`, 30-60 sites each). Also present:
  `CSUI_LEAVE`/`CSUI_CONTINUE` (the Leave Button's two rendered labels, per
  report C), `CSUI_LAST_AUTOSAVE_1`/`CSUI_LAST_AUTOSAVE_3` (pause menu's
  "time since last autosave" widget — `PAUSE/Pause Canvas/Time SInce Last
  Autosave`, gated by an `IntroComplete` `FloatCompare` to 1 — this is
  autosave-*timer* text, not the save-slot cycle-number label, don't
  conflate with the LUA STORE section's cycle verdict), `CSUI_SKILL_LIST_
  UPGRADES_AVAILABLE`, `CSUI_CHARACTER_UI_VIEW`, `CSUI_ENDCYCLE_DESC_1`/`_2`.
  120 more lookups use a dynamic (`$Variable`-sourced) key rather than a
  literal — i.e. some UI labels are chosen at runtime from a variable holding
  the key name, not hardcoded per call site.
- **`TextTable:CS-TextTable`** (9,194 lookups) — the general/narrative table.
  Overwhelmingly dynamic-keyed (8,969 of 9,194 — the `field` param is a
  `$Variable` reference, not a literal string, since most of this table's
  content is per-object/per-scene narrative text resolved at runtime). Of
  the 225 literal-key lookups, structural (non-narrative) ones include
  `INPUT` (147 — generic input-prompt label) and `CRYO` (72); the small
  remainder (`DECLINING`, `FADING`, `FLICKERING`, `STABLE`, `DYING`,
  `SABINESPASSKEY`, one site each) are mood/state-word keys, not sentences.

Pattern for the mod: any label the game renders that needs reproducing
verbatim should resolve through this same two-table, key-based lookup
(`CS-TextTable UI` for chrome, `CS-TextTable` for content) rather than
hardcoding English strings — this is exactly the mechanism the game itself
uses, so it's guaranteed to match rendered text including future
localization.

## SURPRISES

- **`Player_*` and `IntroComplete` are Lua variables, not PlayMaker
  globals** — corrects `ui-state-map.md` section 2 and report C's VERDICTS
  entry for `IntroComplete`. See LUA STORE above for the full correction;
  this changes which API the mod should call for these reads (Dialogue
  System Lua accessor, not `FsmVariables.GlobalVariables`).
- **Character UI Button and Drive Log Button are the same template.**
  Identical event set, near-identical state graph, identical
  `Gamepad Checker`/`RefocusUI` tail — the only difference is which
  component they call into on open/close (an `Animator` bool vs. a
  `UnityUIQuestLogWindow`). A mod-side window-lifecycle abstraction can
  target this template directly.
- **Windows push each other closed, one-directionally.** Both `Character UI
  Button` and `Drive Log Button` send `event:Deactivate` to
  `GameObject:Inventory` on open; neither is pushed closed by Inventory, and
  neither pushes the other closed (no `Deactivate` send between Character UI
  Button and Drive Log Button themselves) — worth a live check for what
  happens if both are opened in sequence.
- **`Hacking?` and `Action View?` share one write template but different
  instance counts** (49 vs. 227 `Location Button` instances) — `Action
  View?` is the broader "an action/cloud panel has camera focus" signal
  (written by every station action-panel-adjacent node), `Hacking?` a
  strict subset (only the cloud/hacking-flagged nodes). Both live on the
  identical `Location Button` leaf-FSM template, just instanced under
  different parent hierarchies.
- **The save-slot-menu "Cycle" and the live-game Lua "Cycle" are unrelated
  mechanisms that happen to share a variable name** — see LUA STORE. Don't
  build one code path assuming it covers both.
- **No Rewired action opens cloud/hacking view.** Every other major surface
  (character window, drive log, inventory, scan, pause) has a dedicated
  bound button; cloud view is uGUI-click-only, per-node. This matches report
  C's "no central Hacking Manager" finding but is worth flagging again here
  since it means the mod's keyboard-input mapping for entering cloud view
  necessarily goes through node-click emulation, not a bindable toggle key.

## OPEN QUESTIONS

- The save-slot menu's exact `Cycle` string-parsing steps (which
  `StringSplit`/`ArrayGet` index holds the cycle number vs. the class name)
  didn't fully recover from ASCII extraction on that one FSM blob (`Slot
  Menu/Slot 1`'s `Filled` state) — several action params came back `null`.
  Not blocking (the live `Cycle` Lua variable is the mechanism the mod
  actually needs for in-session queries), but if the mod ever wants to
  read/display save-slot metadata from the main menu, this needs either a
  live probe or a typetree retry on that specific blob.
- `CycleCanEnd`'s gate gives a structural mechanism (`Off`/`On` `BoolTest`
  states on every home's `End Cycle Action`) but the live-interactability
  claim from `ui-state-map.md` task 1 (`Button.IsInteractable()` during a
  mid-intro read) still needs a live session to close out.
- The `C_*` drive/story-clock variable set (dozens found) hasn't been
  cross-referenced against `Top UI/Ghost Trackers` specifically to confirm
  which subset (if any) are the Hunter/Killer/Gardener Ghost Tracker stage
  variables — flagged as out of scope in report C, still out of scope here.
- What happens when Character Window and Drive Log are opened in sequence
  (see SURPRISES) — no mutual-exclusion wiring found between the two
  directly; worth a quick live check before relying on "only one window open
  at a time" as an invariant.
