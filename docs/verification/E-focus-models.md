# E — Focus models (FSM corpus verification)

Desk pass against the runtime FSM corpus (`tools/analysis/corpus/fsm-census.jsonl`, 4,948
FSMs from `level1`/station, dumped live via the bridge's `/fsmcensus` endpoint per
`build-plan.md` section 5; `fsm-census-title.jsonl`, 29 FSMs from `level0`/title, used for
cross-checks). Unlike the prior static pass (`D-selection-machinery.md`), this corpus is
extracted by the game's own deserializer, so **state-to-state transition wiring is fully
reliable here**, not just state/event/variable name vocabulary. Every claim below cites an
exact FSM path, state name, and action. Scratch scripts and per-object JSON dumps used to
build this report are session-local (not in repo), under the analysis scratchpad.

One corpus limitation worth flagging up front: `SetProperty` actions (39,277 occurrences
scene-wide, the single most common action type) serialize with an opaque
`{"targetProperty": "FsmProperty"}` payload — the dump does not preserve which property
(e.g. a `CanvasGroup.interactable`/`alpha`/`blocksRaycasts` field) or what value is set.
Anywhere this report needed that detail, it's marked unreadable rather than guessed at; see
SURPRISES and OPEN QUESTIONS.

---

## CHECKER TEMPLATE

A scene-wide scan for FSMs containing a `UiSetSelectedGameObject` action returns **590**
instances in `level1` (owner histogram: `Dice Slot Button` ×257, `Gamepad Dice Slot` ×218,
`Gamepad Dice Slot 1` ×30 cloud variant, `Sequence Complete Button` ×30, `Item Cursor` ×24,
plus ~25 singleton menu/window buttons), matching the prior static pass's count closely. The
corpus's full transition graphs reveal this is **not one algorithm** — it's three
structurally distinct sub-templates built from the same primitives (`GetOwner`→`$Owner`/
`$Self`, `UiSetSelectedGameObject($Owner)`, a gamepad-mode gate via `BoolTest($Gamepad)` or
platform checks `IsNintendoSwitch`/`IsXboxGameCore`/`IsPlayStation`). Which variant a widget
gets depends on its navigational context, and the variants differ sharply in whether they
fight back after losing selection — the load-bearing fact for "does a mod-set foreign
selection get stomped."

**Variant A — permanent watchdog (self-reasserting loop).** Used where exactly one control
should always hold focus once its panel is live (single-target modals, always-fights).
Annotated example — `PAUSE/Pause Canvas/RESUME`:
- State `Gamepad Checker` (start state): `IsNintendoSwitch`/`IsXboxGameCore`/
  `BoolTest($Gamepad)` → event `Gamepad` → state `Set Selected 2`.
- State `Set Selected 2`: `UiSetSelectedGameObject(null)`, `GetOwner`→`$Owner`,
  `UiSetSelectedGameObject($Owner)`. Its only transition is `Reselect -> Set Selected 2`
  (a **self-loop**) — but note RESUME's own record carries no `UiGetSelectedGameObject`
  watch action in this dump; the loop is armed by other Checker-variant-A instances that DO
  poll (see below) or by the game re-firing `Reselect` externally.
- The clearest self-contained example of the full loop is `Letterbox Canvas/Tutorial
  System/Button`: state `Set Selected` performs the same set-self sequence, then
  `UiGetSelectedGameObject(StoreGameObject: $Selected, ObjectChangedEvent: event:Reselect,
  everyFrame: True)` — i.e. it polls the current EventSystem selection every frame, and the
  instant it changes to *anything*, fires `Reselect`, whose only transition
  (`Reselect -> Set Selected`) re-enters `Set Selected` and reclaims itself. This runs
  forever as long as the object stays enabled. Same shape, same self-loop, confirmed in
  `Letterbox Canvas/2_Hacking Action Groups/Network Node Æ21 Actions/Node Æ21
  Action/Action Elements/Sequence Complete Button` (state `Focus Self 2`, using
  `GameObjectCompare(notEqualEvent: event:Reselect)` instead of `ObjectChangedEvent`, same
  self-loop transition `Reselect -> Focus Self 2`).
- A variant-A' sub-case is externally-cued rather than self-polling:
  `Letterbox Canvas/Character Window/Upgrade Tracker/Top Line/Upgrade UI/Upgrade Button`
  reclaims itself only when it receives an explicit `Reset` event (sent by its sibling skill
  buttons' own upgrade-confirmation flow, e.g. `ENGINEER`'s `UPGRADE`/`UPGRADE 2`... states
  each `SendEvent target:GameObject/$Upgrade Button event:Reset`), via state `Active`
  (`Reset -> Switch / Gamepad`) → `Switch / Gamepad` (reclaims self) → `Wait` (0.3s, reclaims
  self again) → `Active`.

**Variant B — one-shot compare, then relinquish (no fight-back).** Used for
multiple-siblings-active-at-once contexts where losing selection is legitimate (navigating
to a neighbor). Example — `Letterbox Canvas/1_Action Groups/Empty Container
Actions/End Cycle Action/Dice Slot Button`: state `State 1` sets self once
(`UiSetSelectedGameObject($Self)`), then watches once per frame via
`UiGetSelectedGameObject`→`GameObjectCompare($Self, $Selected, notEqualEvent:
event:Unselected)`. On mismatch it transitions to `State 2` — a **terminal state** (no
transitions back) that only resets the widget's highlight color. It never reclaims
selection again until the FSM is re-enabled (e.g. the home location becomes current again).

**Variant C — state-entry-only (no watch loop at all).** Used for native multi-directional
uGUI navigation groups where fighting siblings would break arrow-key movement entirely.
Examples: `Letterbox Canvas/Top UI/Dice UI/Dice Gamepad System/Dice Cursor 1` (state `Idle`
calls `UiSetSelectedGameObject($Self)` once on entry, no compare/poll — the five cursors
navigate via ordinary uGUI `Selectable` adjacency, none of them fighting the others);
`Letterbox Canvas/1_Action Groups/Power Routing Actions 2/Seed Power Routing
Action/Gamepad Dice Slot` (state `Focus` sets self once on entry, listens for a scene-wide
`RefocusUI` event to re-enter `Focus` — see RESELECTION TRIGGERS); `Item Cursor` (state `On`
sets self once, then listens directly for the *Rewired input* `Inventory Toggle`/`Back`
button-down — not a selection-compare — to trigger its own explicit relinquish, see below);
and `CS Dialogue Manager/Canvas/TMP CS Dialogue UI 1/Dialogue Panel/Main Panel/Continue
Button` (state `Idle` sets self once on entry, no watch loop — see PER-CONTEXT VERDICTS,
dialogue).

**Practical read for the mod:** whether a foreign mod-set selection gets "stomped" depends
entirely on which variant currently owns the object being displaced. Variant A/A' widgets
(menu buttons, tutorial dismiss buttons, dialogue-adjacent Confirm-style controls, the
Upgrade Button) will always reclaim themselves the instant they detect a change. Variant B
and C widgets will not — once genuinely deselected they either go quiet (B) or simply wait
to be told to reclaim by an explicit event (C). This refines `D-selection-machinery.md`
VERDICT 1 ("every eligible interactive widget... independently fights") — that's only true
for variant A/A' widgets, which are the minority by instance count but the majority by
*context type* (essentially all menu/modal buttons).

---

## UI SELECTOR

`Focus Rotator/Focus/UI selector` (comp under the camera-orbit rig, so it lives in
world/camera space, not under the UI canvas) is the game's closest-tagged-object focus
recovery mechanism, fully decoded (previously typetree-unreadable; now complete):

- State `Idle` (start state): `GetOwner`→`$UI Selector`; `FindClosest2(gameObject: owner,
  withTag: "UI Button", ignoreOwner: True, mustBeVisible: True, storeObject: $Closest UI
  Button, storeDistance: $Distance, everyFrame: True)` — continuously finds the nearest
  enabled, visible, non-self object tagged `UI Button` (the same tag confirmed in
  `TagManager` and uniformly carried by all 108 populated Location Buttons per
  `D-selection-machinery.md` Q3). `GameObjectChanged($Closest UI Button, changedEvent:
  event:Select Button)` fires whenever the nearest candidate changes. Local transitions:
  `Select Button -> Check Distance`, `Reset -> Check Distance` (an externally-sent `Reset`
  event forces immediate re-evaluation), `Disable -> Disabled`.
- State `Check Distance`: `FloatSwitch($Distance, lessThan: [3000, Infinity], sendEvent:
  [event:Close, event:Too Far])` — within 3000 units → `Close` → `Selectable?`; beyond →
  `Too Far` → back to `Idle` (no selection made).
- State `Selectable?`: `GetComponent($Closest UI Button)`→`$Button Canvas Group`,
  `GetProperty`, `BoolTest($Interactable?, isTrue: event:Selectable, isFalse:
  event:Hidden)` — reads the candidate's own `CanvasGroup.interactable` (the actual boolean
  read here is legible even though the property-path itself isn't); interactable → `Select`;
  not → back to `Idle`.
- State `Select`: `UiSetSelectedGameObject(null)` then `UiSetSelectedGameObject($Closest UI
  Button)`, then `SendEvent target:GameObject/GameObject:ITEM Inventory UI event:Deactivate`
  (closes the item-inventory panel if it happened to be open), then `FINISHED -> Idle`.
- State `Disabled`: waits for an `Enable` event.

**Who arms/re-arms it.** Four owners send it a direct `Reset` event (28 instances
collapsing to 4 owner types — a closed set): `Letterbox Canvas/Character UI/Character UI
Button` (states `Reset` and `Gamepad UI`, on window close), `Letterbox Canvas/Drive
System/Drive Log Button` (state `Gamepad UI`, on window close), all 24 `Item Cursor`
instances (state `Reset Selection`, on inventory close), and `UI Reselector` (state `Reset
Selection` — the scene-root Confirm-with-nothing-selected backstop, see below). Separately,
`Location Controller`'s zone-transition states (`Rim`, `Greenway`, `Hub`) each
`SetPosition(GameObject:UI selector, (0,0,0), space: Self)` on zone entry — re-anchoring the
selector's world position (not the same as sending it `Reset`) whenever the player crosses
into a new zone.

This confirms `D-selection-machinery.md` VERDICT 4 (previously "strong, well-evidenced
candidate, not fully proven") outright: `UI selector` is definitively the mechanism that
picks the default/nearest Location Button, and its trigger is now known precisely — a
`Reset` event from window-closing controllers, plus its own continuous `FindClosest2`
polling picking up any change in nearest candidate on its own.

---

## PER-CONTEXT VERDICTS

- **Station free-roam.** VERDICT: hybrid, effectively NATIVE. Anchor-setting is fully
  native via `UI selector` (above); multi-directional movement between the 108 Location
  Buttons remains Unity `Automatic` navigation mode (`D-selection-machinery.md` Q3,
  unchanged — no serialized adjacency exists to read). The mod rides the game's own
  anchor-set and its own runtime neighbor computation; it cannot pre-validate marker
  adjacency offline.
- **Location action view** (a location's action-slot list open). VERDICT: NATIVE. Action
  slots use Checker variant C — e.g. `Letterbox Canvas/1_Action Groups/Power Routing
  Actions 2/Seed Power Routing Action/Gamepad Dice Slot`, state `Focus` sets the anchor
  (`$Slot`) once per activation; state `Idle` has a local transition `RefocusUI -> Focus`
  so the slot reclaims itself whenever the game broadcasts `RefocusUI` (e.g. returning from
  a sub-allocation).
- **Dice allocation** (station and cloud). VERDICT: NATIVE, confirmed identical in both
  contexts. `Letterbox Canvas/Top UI/Dice UI/Dice Gamepad System`, state `Active`, sets the
  initial anchor via `UiSetSelectedGameObject($Dice Cursor 1)`; each `Dice Cursor N` is
  Checker variant C (self-selects once on its own `Idle` state, no fight-back), so
  multi-directional uGUI navigation among the five cursors is uncontested. On `Back`, state
  `Reselector` does `SendEvent target:BroadcastAll/owner event:RefocusUI` before tearing the
  picker down. Cloud/hacking action slots (`Gamepad Dice Slot 1`, 30 instances under
  `2_Hacking Action Groups`) and their `Sequence Complete Button` companions (Checker
  variant A — permanent watchdog, e.g. under `Network Node Æ21 Actions`) use the identical
  machinery — this is now a full corpus-level confirmation of the prior static/live finding
  that cloud dice allocation mirrors the station picker exactly.
- **Dialogue (subtitle/continue).** VERDICT: NATIVE, CONFIRMED. `CS Dialogue Manager/Canvas/
  TMP CS Dialogue UI 1/Dialogue Panel/Main Panel/Continue Button`, state `Idle`, is Checker
  variant C — self-selects on activation, no watchdog, so nothing to fight if the mod
  transiently redirects focus. This resolves `D-selection-machinery.md` VERDICT 7's
  "partially refuted" status for the Continue affordance specifically: post-intro dialogue
  continue focus IS FSM-Checker-driven, the same object used at every point in the game
  (this path lives under the shared `CS Dialogue Manager`, not `Intro Sequence`).
- **Response menu** (multi-choice dialogue). VERDICT: NATIVE but NOT FSM-mediated —
  genuinely unresolved by static/corpus means. `.../Response Menu Panel` and `.../Response
  Menu Panel/Response Button Template` are both nearly-empty FSMs: the panel only does a
  `GetComponent`, the button template only plays mouseover/click SFX. Neither contains any
  `UiSetSelectedGameObject`/`UiGetSelectedGameObject` action. Selection is presumably
  Pixel Crushers' own compiled `StandardUIResponseButton`/response-menu code, outside FSM
  data. The useful finding: **no PlayMaker Checker of any kind lives under Response Menu
  Panel**, so whatever selection Pixel Crushers' code sets is uncontested — the mod's
  arrows/moveHandler should ride it cleanly. Which response is selected by default, and
  whether that's stable, remains an OPEN QUESTION for live confirmation.
- **End Cycle / cycle transition.** VERDICT: split. The End Cycle button click itself is
  Checker variant B (`.../End Cycle Action/Dice Slot Button`, `State 1`→`State 2`,
  one-shot-then-relinquish) riding plain uGUI submit (`onClick` → `SendEvent("EndCycle")`,
  unchanged from `D-selection-machinery.md` §6). The post-click cycle-transition/summary
  sequence is READOUT, not a focus context at all: `Cycle Controller`'s root FSM (`Idle`,
  `Cycle`, and dozens of sibling states) contains **zero** `UiSetSelectedGameObject`/
  `UiGetSelectedGameObject` actions anywhere (grep-confirmed across the full ~104KB record)
  — only large blocks of `ActivateGameObject` toggling HUD/summary panels. Nothing to
  select; treat as a pure sequential announcement stream.
- **Cloud/hacking view (toggle + interior).** VERDICT: NATIVE, and the modal boundary is
  now precisely located. `Letterbox Canvas/Top UI/Scan Button`, state `Scan Mode
  Transition`, deactivates `Character UI`, `1_Action Groups`, `Locations`, `Characters` and
  activates `Hacking UI`/`Scan Ambience`/`Hack Camera`/`Hack UI Camera`; state `Normal
  Transition` reverses all of it and additionally sends `Deactivate` to `Ghost Trackers`.
  Inside Hacking UI, action slots reuse the dice-allocation machinery verbatim (previous
  bullet). This resolves half of `D-selection-machinery.md`'s "no Hacking?-scoped selector
  object was located" — the entry/exit gate is `Scan Button`, even though there's still no
  separate cloud-only selection anchor beyond what dice allocation already provides.
- **Pause.** VERDICT: NATIVE. `PAUSE/Pause Canvas/RESUME` is Checker variant A (confirmed
  anchor). The root `PAUSE` FSM itself never sets selection (matches
  `D-selection-machinery.md`), but on unpause it conditionally broadcasts `RefocusUI`:
  state `Gamepad` → `Action view?` (`BoolTest($Action View?, isTrue: event:Reselect)`) →
  state `Reselect` (`SendEvent target:BroadcastAll/owner event:RefocusUI`) → `Text Size
  Check`. So if the player paused while inside an action view, unpausing hands focus back
  to that context's own Checker rather than forcing a station-anchor reselect — a
  deliberate, confirmed behavior.
- **Tutorial modal.** VERDICT: split, and now more precisely bounded. `Letterbox
  Canvas/Tutorial System/Button` is the *only* Tutorial-System FSM among the 590
  selection-setting instances (Checker variant A — single dismiss-button watchdog); treat
  tutorial panels that carry this child as NATIVE. `Letterbox Canvas/Tutorial
  System/Input Pauser` is a separate, non-selection FSM: global transitions `Activate ->
  PAUSED` / `Deactivate -> UNPAUSED`, each toggling Rewired's `Default` vs `Pause` input-map
  categories wholesale via `RewiredPlayerSetControllerMapsEnabled` — a genuine input-level
  lockout, distinct from any GameObject activation. This explains
  `ui-state-map.md`'s noted contradiction ("a tutorial CAN be up while station input stays
  live"): whichever of the 16 individual tutorial trigger FSMs (`Dice, Condition and Energy
  tutorial`, `Hacking Action Tutorial`, `New Action Tutorial 1/2`, etc.) chooses to fire
  `Activate` at Input Pauser gets the hard lockout; others leave it alone. Which triggers
  fire it wasn't checked individually this pass (OPEN QUESTIONS).
- **Character window.** VERDICT: NATIVE, anchor now confirmed. `Letterbox Canvas/Character
  UI/Character UI Button`, state `Open`, deactivates `Locations`, `Characters`, `Top UI`,
  `Bottom UI` and sends `Deactivate` to `Inventory` (matches the known lifecycle). On close
  (`Reset` state, then conditionally `Gamepad UI`), it performs the identical two-part
  hand-back sequence as Drive Log Button: `SendEvent target:GameObject/GameObject:UI
  selector event:Reset` + `SendEvent target:BroadcastAll/owner event:RefocusUI`. Inside the
  window, `Letterbox Canvas/Character Window/Upgrade Tracker/Top Line/Upgrade UI/Upgrade
  Button` is the **confirmed default anchor**: its `Wait` state unconditionally calls
  `UiSetSelectedGameObject($Upgrade Button)` 0.3s after the window activates. This resolves
  `D-selection-machinery.md`'s open item — it's Upgrade Button, not a skill row; skill-row
  buttons (`ENGINEER` etc.) only call `UiSetSelectedGameObject` inside their own
  upgrade-confirmation sub-flow, never as a window-open anchor.
- **Drive log.** VERDICT: provisionally NATIVE, with one real gap. `Drive Log Button`
  carries its own open/close FSM and performs the same `UI selector` Reset +
  `BroadcastAll RefocusUI` hand-back on close (state `Gamepad UI`) as Character UI Button.
  Separately, `Letterbox Canvas/Drive System/CS Drive Log/Quest Log Window Main
  Panel/.../Scroll Content` is itself Checker-templated (one of the 590) — the drive-entry
  list has its own selection machinery distinct from the open/close button, not yet
  decoded in this pass. The exact branch that gets from a gamepad-mode initial keypress to
  actually invoking `UnityUIQuestLogWindow.Open()` is ambiguous from the static graph
  (`Gamepad Checker`'s Gamepad-true path never visibly reaches the `Open` state in this
  record — see OPEN QUESTIONS); needs a live trace before shipping drive-log enrichment.
- **Inventory strip.** VERDICT: NATIVE, semi-modal — and the semi-modal mechanism is now
  precisely identified, not just observed. `Item Cursor` (24 instances, variant C)
  self-selects on activation and listens directly to Rewired `Inventory Toggle`/`Back`
  button-down (not a selection watch) to trigger its own close: state `Reset Selection` →
  `SendEvent .../UI selector event:Reset` + `BroadcastAll RefocusUI` → `Cooldown` → `Off`.
  Independently, the **parent** `Letterbox Canvas/Bottom UI/Inventory` FSM polls
  `UiGetSelectedGameObject` every frame in states `Item 4`/`Data 4` and `StringCompare`s the
  selected object's literal name against `"Item Cursor"` — if the current selection is
  anything else, it fires its own `Deactivate` and closes the strip. This is a genuine,
  deliberate focus-drift watchdog, not a passive observation: **if the mod ever redirects
  EventSystem selection away from an Item Cursor while inventory is open, the game will
  auto-close the inventory strip**, which would look like an unexplained closure to the
  player. This is a hard implementation constraint for any inventory-review feature.

---

## MODAL ENFORCEMENT MAP

Confined to what's legible in this corpus — `ActivateGameObject`/`SendEvent Deactivate`
patterns are fully readable; `SetProperty`-based `CanvasGroup` toggles are not (see corpus
limitation note at top).

- **Character UI Button** (`Open` state): deactivates `Locations`, `Characters`, `Top UI`,
  `Bottom UI`; sends `Deactivate` to `Inventory`. (Known lifecycle, confirmed here with
  exact action list.)
- **Drive Log Button** (`Open` state): sends `Deactivate` to `Inventory` only — does not
  deactivate Locations/Top UI/Bottom UI the way Character UI Button does. The drive log is
  a lighter overlay than the character window; station objects stay GameObject-active
  underneath it.
- **Scan Button** (`Scan Mode Transition` / `Normal Transition`): the cloud/hacking
  boundary. Entering deactivates `Character UI`, `1_Action Groups`, `Locations`,
  `Characters`; activates `Hacking UI`, `Scan Ambience`, `Hack Camera`, `Hack UI Camera`.
  Leaving reverses all of it and additionally sends `Deactivate` to `Ghost Trackers`.
- **Inventory**: no wholesale teardown of the rest of station UI by an external controller
  — it's self-contained and self-closing (focus-drift watch, above), matching
  `ui-state-map.md`'s "semi-modal" characterization exactly.
- **PAUSE** (`Pause` state): activates `Pause Canvas`, `QUIT GAME`, `PAUSE snapshot` (audio
  ducking). No `ActivateGameObject(false)` targeting Bottom UI/Top UI/Locations was found in
  this state's action list — Pause likely relies on Pause Canvas's own full-screen block
  (opaque `SetProperty` payloads in `DIalogue UI Unfocus`/`Dialogue UI focus` states,
  unreadable this pass) rather than explicit per-subsystem deactivation.
- **Tutorial System/Input Pauser**: not a GameObject-activation mechanism at all — a
  Rewired input-map toggle (`Default` category off, `Pause` category on, or the reverse),
  triggered by whichever tutorial FSMs choose to fire its `Activate`/`Deactivate` events.
- **Cycle Controller** (`Cycle` state): a large block of `ActivateGameObject` calls (dozens)
  toggling HUD/cycle-summary panels; no selection-related actions anywhere in the FSM.

---

## RESELECTION TRIGGERS

The scene-wide `RefocusUI` broadcast (`SendEvent target:BroadcastAll/owner
event:RefocusUI`) is the general "something closed/reset, please reclaim yourself" signal —
399 `SendEvent` actions carry it scene-wide. By owner:

- `Gamepad Dice Slot` ×218 (station action slots, state `Refocus`, on leaving an action's
  allocation).
- `Action Controller` ×119 (the per-action outcome engine — sends it too; which exact state
  and condition wasn't traced this pass, flagged in OPEN QUESTIONS).
- `Gamepad Dice Slot 1` ×30 (cloud variant, identical trigger).
- `Item Cursor` ×24 (state `Reset Selection`, on inventory close).
- `Character UI Button` ×2 (states `Reset` and `Gamepad UI`, on window close).
- `PAUSE` ×1 (state `Reselect`, conditionally on unpause if `$Action View?` was true).
- `Hacking Action Tutorial`, `Intro Sequence`, `Clock Tutorial`, `Dice Gamepad System`
  (state `Reselector`, on leaving allocation), `Drive Log Button` (state `Gamepad UI`, on
  window close) — one instance each.

A narrower, more targeted signal — a direct `Reset` event sent specifically to `UI
selector` — comes from a **closed set of four owner types** (28 instances total):
`Character UI Button`, `Drive Log Button`, all 24 `Item Cursor` slots, and `UI Reselector`
itself. This is the precise "hand focus back to the nearest location marker" moment,
distinct from the broader "everyone should recheck their own claim" `RefocusUI` broadcast.

`UI Reselector` (scene root, previously typetree-unreadable, now fully decoded) is the
game's own dead-selection backstop: state `Idle` watches Rewired's `Confirm` button
specifically (`RewiredPlayerGetButtonDown(actionName: "Confirm")`) → `Pressed` → `Check
Selected` — `UiGetSelectedGameObject`→`$Selected`, `GameObjectCompare($Selected, null,
equalEvent: event:Reset Selection, notEqualEvent: event:Do Nothing)`. If something IS
selected, it further checks `IsActive($Selected)` (state `Check Active`) — a
selected-but-now-disabled object also triggers `Reset Selection`. `Reset Selection` sends
`Reset` to `UI selector` and returns to `Idle`. This is exactly the game's own tier-2
engage-key mechanism from `ui-state-map.md` §6c ("native selectables exist but nothing is
selected → an engage key sets selection to the game's own anchor"), now fully proven rather
than inferred, and tied specifically to the `Confirm`/submit input.

**Practical rule for the mod:** any moment `RefocusUI` fires (or a widget's own close-state
sends `UI selector` a `Reset`) is a "focus is about to settle" signal — the mod should treat
it as a cue to wait a beat and resync, not as an opportunity to set its own selection in the
same frame.

---

## SURPRISES

- The Checker template is not one algorithm. Three structurally distinct sub-variants exist
  (permanent watchdog / one-shot-then-relinquish / state-entry-only), and only the
  watchdog variant actually fights back after losing selection. Prior docs described a
  single reusable pattern; this materially changes how risky a foreign mod-set selection is
  depending on context.
- Post-intro dialogue's Continue Button IS Checker-templated (`CS Dialogue Manager/.../
  Continue Button`) — resolves `D-selection-machinery.md` VERDICT 7's "partially refuted"
  status to CONFIRMED for the Continue affordance. Response Menu buttons remain outside FSM
  data (Pixel Crushers' own compiled code), so that half of VERDICT 7 still stands open.
- `UI Reselector`, previously only characterized by state names via unreliable raw-string
  extraction, is now fully decoded and confirms `D-selection-machinery.md` VERDICT 2
  outright — it's wired to the `Confirm` input specifically, and its dead-selection check
  also covers a selected-but-inactive object, not just a null selection.
- Character UI Button and Drive Log Button both perform an *identical* two-action "hand
  back to station" sequence on close (`UI selector` Reset + `BroadcastAll RefocusUI`) — a
  clean, generalizable "focus-settled" signal, stronger than the prior doc's more tentative
  framing of window-close behavior.
- `D-selection-machinery.md`'s claim that Drive Log Button's strings include "Response
  Menu"/"Dialogue Panel"/"Subtitle Panel"/"focusCheckFrequency" is **not corroborated** by
  this corpus's full record for that object — none of those strings or variables appear
  anywhere in it. Likely a false attribution from that pass's known-unreliable raw-ASCII
  extraction (adjacent serialized blob bleed, a limitation that report itself flagged).
  Recommend treating that specific claim as retracted.
- Inventory's documented "semi-modal" behavior turns out to be a precise, deliberate
  focus-drift watchdog (every-frame name-string compare against `"Item Cursor"`), not just
  an emergent property. This is a hard constraint: redirecting focus away from an Item
  Cursor while inventory is open will make the game auto-close it.
- Location Controller's zone-transition states reposition `UI selector` to local origin
  independently of the `Reset`-event mechanism — an additional, previously-undocumented
  re-anchoring step tied to zone crossings specifically.

---

## OPEN QUESTIONS

- Drive Log Button: which exact branch takes a gamepad-mode initial keypress to actually
  calling `UnityUIQuestLogWindow.Open()`? The static graph shows `Gamepad Checker`'s
  Gamepad-true path returning to `Idle` without visibly reaching the `Open` state, while
  `Gamepad Checker 2`'s paths do reach it. Needs a live trace: press Drive Log in gamepad
  mode and observe whether the window opens immediately or only arms `UI selector` for a
  follow-up confirm.
- Response Menu's default-selection identity and stability are owned by Pixel Crushers'
  compiled C# (`StandardUIResponseButton`/response menu template), outside this corpus.
  Needs live confirmation of which response gets initial selection and whether it's
  consistent.
- PAUSE's exact `CanvasGroup`/interactable toggles (`DIalogue UI Unfocus`/`Dialogue UI
  focus` states) are unreadable in this corpus (opaque `SetProperty` payloads). A live
  `CanvasGroup` probe on Pause Canvas and Dialogue Panel would confirm whether Pause hard-
  blocks input or relies on Pause Canvas's own top-level raycast block.
- Whether each of the 16 Tutorial System trigger FSMs individually fires Input Pauser's
  `Activate` event was not checked per-instance — this determines, per tutorial, whether
  station input stays live underneath or gets hard-locked. Worth a systematic per-trigger
  check before the modality layer's tutorial handling ships.
- Cloud/hacking's initial node/die selection anchor on entering Scan Mode (analogous to
  Dice Gamepad System's `UiSetSelectedGameObject($Dice Cursor 1)`) wasn't pinned to a
  specific FSM/state this pass.
- `Action Controller` (the ~150KB per-action outcome engine, 119 instances) sends
  `RefocusUI` too — which state and under what condition wasn't traced; likely a
  post-outcome focus handoff back to the action slot, worth a quick follow-up given it's
  the largest non-widget `RefocusUI` source after the dice-slot family.

---

## Sources

`tools/analysis/corpus/fsm-census.jsonl` (station, 4,948 FSMs), `fsm-census-title.jsonl`
(title, 29 FSMs). Cross-referenced against `docs/verification/D-selection-machinery.md`,
`docs/ui-state-map.md`, `docs/build-plan.md` section 5. Individual per-object JSON dumps and
scan scripts used to build this report are session-local scratch artifacts, not retained in
repo.
