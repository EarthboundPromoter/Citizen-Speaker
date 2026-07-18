# Modality anchors — static source verification

Static, read-only pass over `level0`/`level1`/`level2` (UnityPy, ASCII-run
extraction per `tools/analysis/README.md` — full PlayMakerFSM typetree parse
fails on most FSM blobs here with `EOFError: read_str out of bounds`, a Cecil/
custom-action mismatch; ASCII-run extraction is the reliable method, as the
README already notes) plus `Assembly-CSharp.dll` for the PixelCrushers
Dialogue System API. Cross-checked against `docs/ui-state-map.md` and
`docs/triage-queue-2026-07-18.md` reports 8, 10, 19, and the mod's own
`GameQueries.cs` / `Watchers.cs` / `InputManager.cs` / `DialoguePatches.cs`,
which already encode some of this (notably `GameQueries.InputPaused()`).
Game was never launched; nothing here is live-confirmed unless marked so from
an existing report.

## EVIDENCE

### 1. Mode variables

- **`Gamepad`** (global PlayMaker bool). Owner: `Gamepad Manager` (scene root,
  pid 717), FSM states `Switch or PC` → `Check Last State`, events
  `Mouse`/`Gamepad`. Reads `Rewired.ControllerType` via a
  `RewiredGetAnyButtonDown` action, drives cursor hide/lock. Confirms
  `ui-state-map.md` section 1 exactly. `Gamepad` is read by dozens of other
  FSMs scene-wide (Dice Gamepad System, Character UI Button, PAUSE, Gamepad
  Dice Slot on every dice action, Tutorial System's per-button "Gamepad
  Checker" FSMs, Inventory) — it is the single most-referenced modality flag
  in the scene, read-only for the mod via
  `HutongGames.PlayMaker.FsmVariables.GlobalVariables`.

- **`Hacking?`** (global bool). Found on `Leave Button`
  (`Top UI/Letterbox Canvas`) and on ~40 `Location Button` instances under
  `1_Station UI/ERLIN MAIN/.../Hacking UI` (each a cloud network-node marker,
  e.g. object paths ending `.../Hacking UI/Gate S7 Canvas/Billboard
  Elements/Location Button`, `.../ConSec Node F1/...`, `.../Port H33
  Canvas/...`). This is the doc's cited "cloud view" global — confirmed real
  and confirmed read by both the station's own Leave Button (station
  leave-paths check it) and by the individual hacking-node markers.

- **`Action View?`** (global bool). Found on `PAUSE` (scene root FSM) and on
  the same set of `Hacking UI` Location Button instances plus two "One Shot"
  autoplay-scene trigger objects. `PAUSE` consulting it explains the
  "DIalogue UI Unfocus"/"Dialogue UI focus" pause-entry logic needing to know
  whether an action panel currently has UI focus.

- **`Intro Complete?`** (Cycle Controller local) vs **`IntroComplete`**
  (no-space token). Both tokens co-occur inside `Cycle Controller` and
  `Intro Sequence`'s own FSM blobs, plus `IntroComplete` (no space) alone
  appears on: `Drive System` (root), the `Action Switch (Self Repair)` /
  `Action Switch (Sunbathe)` gating objects at every home location
  (Empty Container, Repaired Unit, Hypha Dorm, Briar Base Camp, Capsule
  0451 — confirms report 10's "Action-Switch-gated INACTIVE" claim
  structurally), `Time SInce Last Autosave` (Pause Canvas — confirms the
  pause-menu autosave gating), `DEBUG Intro Skipper`, and `Focus` (a *child*
  of `Focus Rotator`, not Focus Rotator itself — see SURPRISES). Best-evidenced
  reading: `IntroComplete` (no space) is the PlayMaker GLOBAL; `Intro
  Complete?` (space, question mark) is a per-FSM LOCAL mirror that individual
  FSMs (Cycle Controller, Intro Sequence) keep in sync from the global via a
  `Set Bool Value`-style action — both strings living in the same blob is
  consistent with that idiom. Not 100% certain without the transition graph
  (typetree parse fails on these blobs); reading the global directly via
  `FsmVariables.GlobalVariables` should be authoritative regardless of which
  local copies exist.

- **`LightCycle`**. Owners: `Cycle Controller` (defines/drives it — see the
  "Get Light State + Swap" state below), `Light Controller`
  (`DEBUG Capture`), `Jukebox`. Confirms doc.

- **Tutorial System's state machine and Input Pauser** — fully mapped, see
  below (this is the biggest structural finding of this pass).

  `Letterbox Canvas/Tutorial System` (pid 2380) is a plain container: 13
  named tutorial panel children (Intro and Control Tutorial (PROMPTS), Dice
  Condition and Energy tutorial, Drive Tutorial (PROMPTS), Navigation
  Tutorial (PROMPTS), Character Screen Tutorial (PROMPTS), New Action
  Tutorial 1, New Action Tutorial 2, Clock Tutorial, Cloud Tutorial
  (PROMPTS), Hacking Action Tutorial, Breakdown Tutorial), each its own
  panel FSM with states `Wait`/`GO!`/`Off` driving `alpha`/`blocksRaycasts`/
  `interactable` on itself (that trio is the render/interaction-active
  signal — matches doc's "tutorial canvas effective alpha > 0" anchor) —
  plus **one shared** `Button` object (Text (TMP) + Outline children) used
  by *every* panel's Continue prompt, and a sibling `Input Pauser` object,
  `Character Tutorial Trigger`, `Hacking Tutorial Trigger`, `Breakdown
  Tutorial Trigger`.

  `Input Pauser` (pid 4137) is **not itself a PlayMakerFSM state a mode
  tracker reads directly** — it's two components: a small custom
  MonoBehaviour (`TargetFSMs` field, here pointing at its own sibling FSM)
  plus that sibling PlayMakerFSM (states `INIT`/`PAUSED`/`UNPAUSED` — this
  is the FSM `GameQueries.InputPaused()` already finds by name and reads
  `ActiveStateName` from). What it actually *does* on entering PAUSED: it
  runs a `RewiredStandaloneInputModule` action that repoints the
  EventSystem's Rewired-bound field names —
  `horizontalAxis`/`verticalAxis`/`submitButton` — from the default
  `Selection Axis Horizontal`/`Selection Axis Vertical`/`Confirm` action
  names to `Pause Selection Horizontal`/`Pause Selection Vertical`/`Pause
  Confirm`, then restores the default names on UNPAUSED. **This is an input
  *remap*, not a general block**: it changes which Rewired action names the
  input module polls for real hardware navigation. It has no architectural
  connection to the mod's own `UnityEngine.EventSystems` calls
  (`Navigator.Move`/`ActivateCurrent`, which don't go through that polling
  loop at all). See VERDICTS for what this means for report 8.

  The top-level `PAUSE` FSM (pid 286, scene root, distinct from the Tutorial
  System's Input Pauser) uses the **identical** remap trick on `Pause` entry
  (`Time + Controller Maps` state) and reverses it on `Unpause`
  (`Time + Controller Reset`), plus sets Unity `timeScale`/
  `adjustFixedDeltaTime` (real time-pause, not just input). Full state list:
  `Idle`, `Pause`, `DIalogue UI Unfocus` (typo verified in source, not a doc
  typo), `Sure?`, `Sure? 2`/`Sure? 3`, `Options`, `Set Up`, `Unpause`,
  `Exit`/`ExitToMenu`/`Quit Game`/`Quit to Menu`, `Pause Back`, `Dialogue UI
  focus`, `Switch?`/`Xbox?` (input-icon branch), scroll-speed and music/SFX
  volume sub-machines. Matches `ui-state-map.md` section 3.1 closely,
  including the exact "DIalogue UI Unfocus" string.

- **Dialogue-active state (PixelCrushers Dialogue System).**
  `Assembly-CSharp.dll` contains the string-heap entries `isConversationActive`
  (1), `IsConversationActive` (2), and `currentConversationState` (1) —
  confirms `DialogueManager.isConversationActive` is a real static property
  on the shipped assembly, and corroborates the mod's own existing use of
  `PixelCrushers.DialogueSystem.DialogueManager.currentConversationState` in
  `Watchers.cs` (`CheckSoleContinue`). `CS Dialogue Manager` — the object the
  mod path-searches (`CS Dialogue Manager/Canvas/TMP CS Dialogue UI
  1/Dialogue Panel/...`) — does **not** live in `level1` at all; it exists in
  `level0` (main-menu scene) and in `sharedassets1.assets`, i.e. it's a
  persistent (`DontDestroyOnLoad`-style) singleton carried over from the
  main-menu scene, not station-scene-local. `DialogueManager.isConversationActive`
  is the cleanest single anchor for "dialogue active" — cheaper than the
  panel-path lookup the mod currently does and immune to the panel's exact
  hierarchy.

### 2. Transitions (entry/exit hooks)

- **Pause**: entry event on `PAUSE` root FSM's `Idle`→`Pause` transition
  (watches a `RewiredPlayerGetButtonDown` action, i.e. a bound Pause button);
  exit via `Unpause`→back to `Idle`. `Pause Canvas` active/inactive is a
  reliable proxy (what `Watchers.CheckPauseMenu()` already polls).
- **Tutorial modal**: entry/exit is each panel's own `Wait`/`GO!`/`Off` FSM
  driving its `alpha`; a panel's effective alpha crossing >0 is entry, back
  to 0 is exit — matches what `Watchers.CheckTutorial()` already polls via
  `activeInHierarchy`. The **Input Pauser** state (`PAUSED`/`UNPAUSED`) is a
  *separate* signal from panel visibility (see SURPRISES) — don't conflate
  them.
- **Dialogue**: `UnityUIDialogueUI.ShowSubtitle`/`ShowResponses` (already
  Harmony-patched in `DialoguePatches.cs`) are the entry-side hooks per line/
  menu; `DialogueManager.isConversationActive` going false is the clean
  whole-conversation exit hook (not currently used — the mod infers "no
  conversation" only implicitly).
- **Dice allocation**: `Dice Gamepad System` FSM (pid 3331, `Dice UI`) states
  `Setup`/`Off`/`Activate`/`Active`/`Back`/`Reselector`/`Click`/`Slotted`/
  `Inactive`/`Reset`. Entry = state becomes `Active`; exit = leaves `Active`.
  Exactly what `Watchers.CheckDiceAllocation()` already polls.
- **Cycle transition (End Cycle)**: see section 3 below — this is the
  transition set report 3 and doc section 8 task 2 asked for.
- **Cloud/hacking view**: `Hacking?` global flip is the entry/exit signal
  (read by Leave Button and every hacking-node Location Button); no dedicated
  "Hacking Manager" FSM was found — toggling appears to be per-node/per-Leave-
  Button rather than one central state machine. `Hacking Active?` and `Bike
  Active?` are a *different*, Intro-Sequence-local pair (intro-beat gating
  for which tutorial trigger fires), not the runtime cloud-view flag — don't
  confuse the two despite similar names (see SURPRISES).
- **Character screen**: `Character UI Button` FSM (pid 2217) states `Set Up`/
  `Idle`/`MouseOver`/`Highlight`/`Open`/`Close`/`Back`/`Active`/`Deactivate`/
  `Reset`; reads `interactable`, `UpgradeAvailable`, `Gamepad`/`Gamepad UI`,
  and on close sends `ForceUnslotDice`/`Leave`/`Clear Button`. The exact
  transition graph (what gates `Open` firing) could not be extracted — the
  typetree parse that would show transition targets fails on this blob; this
  is the report-20 open item and needs either a live interactable read on
  `Character UI Button` or a working full-FSM parse.
- **Scan mode**: `Scan Button` FSM (pid 2181) states `Intro Check` →
  `Normal Transition`/`Normal Idle` ↔ `Scan Mode Transition`/`Scan Idle`,
  plus `Off`/`Holding`/`Click`/`Holding Scan Mode`, watches a `Scan Toggle`
  Rewired button, vars `ScanActive`/`ScanOn`. Matches doc exactly, including
  the `Intro Check` gate name.

### 3. Game-driven sweep signature (cycle transition)

Found. The machinery is the **`Cycle Controller` root FSM itself** (pid
1082, scene root, no parent) — not the Dice Gamepad System (which per report
6's live resolution stays inactive throughout). Its state list (153KB blob,
ASCII-extracted) includes, in a plausible reset-loop cluster: `EndCycle`
(entry event) → `Cycle` → `Reset UI + ReROLL` → `Reset 0` (labelled
"Reset 0! (Consequence?)") through `Reset 5` → `Roll Cycle Changes` →
`Update Clocks` → `Refresh End Cycle Bool` → `Check Variables` →
`Tick Cycle` → back toward `Idle`. Critically, the blob contains the tokens
`Action Group`, `Cycle Clock Group`, and `storeNextChild` together — 
`storeNextChild` is a standard PlayMaker "iterate this GameObject's children
one at a time" action. That is the walk: the Cycle Controller iterates every
location's Action Group and Cycle Clock Group children as part of its own
post-`EndCycle` reset pass. Each touched action/dice-slot panel's own
"Gamepad Checker" FSM (the same small per-button watcher family documented
in `ui-state-map.md` section 4, confirmed present again here on e.g. the
Tutorial System's shared Button) reselects itself when its Enable/Disable
state changes — that reselection cascade is what the focus watcher picks up
as the report-3 flurry.

**Practical bracket for the mod**: poll `Cycle Controller`'s
`ActiveStateName` (same access pattern `GameQueries.FindFsm` already uses
for other FSMs, this one by scene-root name with no path hint needed since
there's exactly one `Cycle Controller`). Treat entry into the reset cluster
(anything other than the FSM's idle/ready state, entered via the `EndCycle`
event) through return to idle as the suppression window for focus
announcements — the same window in which report 3's "MANUAL SALVAGE.
ENDURE -1, safe" / "UNLOAD CONTAINERS. ENDURE -1, danger" noise fired.
**The exact idle-state name and the precise reset-cluster boundary need a
live state-name log across one real end-cycle** (doc's open task 2) — the
ASCII order is not a reliable transition graph, only a strong structural
hint. `Refresh End Cycle Bool` toggling `CycleCanEnd` off then back on is a
plausible tighter bracket if the live log confirms it brackets the sweep
more precisely than full-Idle-to-Idle.

### 4. UNVERIFIED marks — see VERDICTS below (one per mark).

### 5. Input acceptance per mode (statically determinable)

- **Cloud/hacking view**: die picker identical machinery to station (same
  `Dice Gamepad System`, same `SlottedDiceGlobal`-consuming `Action
  Controller` pattern — confirmed: `2_Hacking Action Groups` contains real
  actions, e.g. `Slot Ripperworm Action` under `Port H33 Actions`, whose
  `Action Controller` references `SlottedDiceGlobal` exactly like station
  actions). `1_Action Groups` carries a real gating FSM on its own root
  (states `Setup`/`Disable`/`Enabled`, driving `alpha`/`blocksRaycasts`/
  `interactable`, with `sendToChildren`/`FSMName`/`subFSMName` fields to
  broadcast Enable/Disable into named per-location sub-FSMs) — but
  **`2_Hacking Action Groups`'s own root carries no such FSM** (just a bare
  RectTransform). Whatever shows/hides hacking nodes is per-node (each
  `Location Button` under `Hacking UI` reading `Hacking?`/`Action View?`
  itself), not one central group-level gate the way station actions have.
  This is a genuine structural asymmetry worth flagging for report 19a's Tab
  fix (scanning `2_Hacking Action Groups` can't lean on the same
  `activeInHierarchy` group-level shortcut `GetActionPanels` uses for
  `1_Action Groups`).
- **Tutorial modal**: per-panel `Wait`/`GO!`/`Off` FSM; the single shared
  `Button` (Continue) is the dismiss path for most panels. **Exception**:
  `Cloud Tutorial (PROMPTS)` does not use the shared Continue Button as its
  primary rendered prompt — it has its own `X Prompt` (`Switch Text`/
  `Gamepad Text` variants), i.e. its taught dismissal input is a different
  bound button than the shared Confirm/Continue path. This lines up with
  report 19c's "continue button component fully disabled while panel up;
  possibly dismisses via the taught action" — static evidence supports that
  hypothesis: this specific panel's own designed prompt is an X press, not
  Continue, which is consistent with (though does not 100% prove without a
  live watch) the shared Button being deliberately left non-interactable
  while this one panel is up. `Hacking Action Tutorial` is a **separate**,
  minimal panel (TITLE only at the depth inspected) — likely the DATA
  ACTIONS die-match explainer (report 19b/21), distinct from the cloud
  toggle-glyph tutorial; the two should not be conflated when fixing report
  1's prompt-transcoding.
- **Station free-roam**: Leave Button (`Top UI`) is the one control whose
  own FSM reads `SlottedDiceGlobal` (leaving blocked while dice slotted —
  confirms doc), `Hacking?`, `Action View?`, and `CycleCanEnd` all
  together, and swaps its own rendered label between `CSUI_LEAVE` and
  `CSUI_CONTINUE` via a text-table lookup — i.e. the same button is
  "Leave" in some contexts and "Continue" in others (matches report 19d's
  observation that leave-paths are scope-dependent; this button is the
  single physical control behind both wordings, not two different
  controls).

## VERDICTS

One verdict per `ui-state-map.md` UNVERIFIED mark, plus the brief's stated
presumptions.

- **Gamepad-mode flip interaction with mod's synthetic events** (§1):
  UNRESOLVED — live-only. Static pass confirms the `Gamepad Manager` FSM
  shape but can't observe runtime interaction with the mod's own event
  injection.
- **`CycleCanEnd` as the End Cycle interactability gate** (§2): CONFIRMED
  structurally — real variable, defined on `Cycle Controller`, referenced by
  `Intro Sequence`, `Leave Button`, and all five End Cycle Action instances.
  The specific live claim ("read `Button.IsInteractable()` while intro is
  mid-script") remains UNRESOLVED — needs a live read, static data can't
  observe a live interactable flag.
- **Relation between Player_\* globals and Cycle Controller's spaced-name
  locals** (§2): UNRESOLVED, but with a lead — the station HUD widgets
  (`Energy Bar System`, `Condition System` under `Top UI/Energy UI`) read the
  underscore globals (`Player_Condition`, `Player_Energy`) directly, while
  `Cycle Controller` carries its own `Player Energy`/`Player Condition`
  locals (spaced). Most likely a global-authoritative-with-local-mirror
  pattern; confirming which is authoritative mid-cycle needs a live compare.
- **Gamepad mode flip risk to synthetic events**: same as first item, no new
  information.
- **Breakdown modal, never reached** (§3.2): CONFIRMED to exist
  structurally — `Cycle Controller` has a `Breakdown?` branch token and
  `Tutorial System` has a full `Breakdown Tutorial` panel — but runtime
  behavior is still UNRESOLVED (never reached live).
- **Input Pauser's actual gating scope** (§3.3): RESOLVED (refutes the
  original "modal gate eats all input" framing). Input Pauser is a Rewired
  input-module axis/button-name remap that only affects real
  controller/keyboard input routed through that specific
  `RewiredStandaloneInputModule` polling path. It has no architectural
  effect on the mod's own `UnityEngine.EventSystems` calls. The blanket
  key-swallow in `InputManager.Tick()` (`if (GameQueries.InputPaused() &&
  !TutorialContinueFocused()) return;`) is the mod's own defensive choice,
  not something the game enforces on it — worth re-examining now that the
  mechanism is understood, since it may be over-cautious (silencing keys the
  game would have accepted fine) or under-cautious (not accounting for
  cases where the *real* gate is a disabled Button rather than this remap).
- **Dice allocation flow, UNVERIFIED live** (§3.5): CONFIRMED — superseded
  by report 13's live validation (full native arrows/Enter run worked
  end-to-end); the FSM shape found here (`Setup/Off/Activate/Active/
  Back/Reselector/Click/Slotted/Inactive/Reset`) matches what that live run
  exercised.
- **Scan mode, UNVERIFIED live** (§3.6): structural shape CONFIRMED
  (`Intro Check`/`Normal Idle`/`Scan Mode Transition`/`Scan Idle` all
  present exactly as documented); live behavior still UNRESOLVED.
- **Character screen, UNVERIFIED** (§3.7): structural shape CONFIRMED
  (states and key variables enumerated above); the specific failure in
  report 20 (U key inert) is UNRESOLVED — the transition graph that would
  show the `Open` gate condition didn't parse statically.
- **2_Hacking Action Groups, "~40 network-node groups, all inactive until a
  hack starts"** (§3.9): PARTIALLY CONFIRMED — the groups and real,
  dice-wired actions inside them are confirmed to exist (not a stub); the
  "all inactive until a hack starts" runtime claim, and the mechanism that
  activates them (no group-level gating FSM was found, unlike `1_Action
  Groups`), remain UNRESOLVED.
- **Focus Rotator gates Rim behind IntroComplete** (§3.9, world zones):
  REFINED — `Focus Rotator` itself carries only a `Transform` (no logic).
  The `IntroComplete`-reading component is on its *child* object `Focus`.
  The actual Rim-transition gating states (`Transition to Rim`/
  `Transition to Rim 2`/`RimTransit`) live on `Location Controller`, using
  numeric compares near those transitions. Attribute the gate to `Focus`
  (child) + `Location Controller`, not `Focus Rotator` itself.
- **Hacking action groups inactive/gamepad dice cursor state** — see above.
- **Ghost Trackers via C_\* globals** (§5): PARTIALLY CONFIRMED — the `C_*`
  boolean-global naming convention is real (found `C_ADEBTCALLEDIN`,
  `C_BACKINBUSINESS`, `C_MAYWICKSHOT` on `Cycle Controller`/`Intro
  Sequence`), but these specific tokens read as drive/story-clock flags, not
  confirmed as the Hunter/Killer/Gardener Ghost Tracker stage variables
  specifically — a dedicated search scoped to `Top UI/Ghost Trackers` would
  be needed to confirm those exact variable names (out of scope this pass).
- **Dice allocation mechanics — native uGUI focus model, "UNVERIFIED live;
  validate at next session"** (§6b): CONFIRMED — superseded by report 13.
- **DEBUG Intro Skipper, "what it sets" UNVERIFIED**: PARTIALLY CONFIRMED —
  it references `IntroComplete` (so it does set/touch that global); the
  full field list is still UNRESOLVED (would need its own typetree/ASCII
  dump, not attempted this pass).
- **Mouse-mode allocation is drag-based, "keyboard cannot synthesize this"**
  (§6b): no new evidence gathered this pass; UNRESOLVED as before.

## OPEN DESIGN QUESTIONS

- Given Input Pauser doesn't architecturally gate the mod's synthetic input,
  should `InputManager.Tick()` still swallow keys during `InputPaused()`
  wholesale, or should the mod instead trust per-control `IsInteractable()`
  checks (as `InputManager.ClickFirstActive` already does for other
  controls) and drop the blanket pause check entirely? The current check may
  be solving a problem ("scripted chains hang") through the wrong mechanism
  — worth understanding what actually caused the sessions 2/3 hangs (now
  separately explained by report 16's double-dispatch root cause) before
  deciding whether the Input-Pauser check still earns its keep.
- The Cycle Controller sweep-bracket needs one live state-name log across a
  real end-cycle to nail the exact idle/ready state name and confirm
  whether `Refresh End Cycle Bool` (toggling `CycleCanEnd`) is a tighter,
  more precise bracket than full-Idle-to-Idle.
- `2_Hacking Action Groups` has no group-level gating FSM the way
  `1_Action Groups` does — should the mod's cloud-modality Tab fix (report
  19a) treat cloud action-panel discovery as inherently per-node rather than
  trying to reuse `GetActionPanels`'s group-`activeInHierarchy` shortcut?
- Is `DialogueManager.isConversationActive` worth adopting as the primary
  "dialogue active" anchor in place of (or alongside) the current
  `MenuOpen`/subtitle-sequence tracking in `DialoguePatches.cs`? It's a
  single static bool with no path-lookup dependency.
- Given the `Dice Slot Button` correction below, report 6's "classify slot
  type when announcing a focused action" design should key off presence/
  absence of a **`Gamepad Dice Slot`** sibling, not `Dice Slot Button`
  presence (which both plain and dice actions share).

## SURPRISES

- **`ui-state-map.md` section 6b's action-archetype claim is wrong on one
  point.** It says plain-activation (End Cycle) actions carry `Dice Slot
  Button` while dice actions carry `Gamepad Dice Slot` "No Dice Slot
  Button." Static proof this pass: ordinary dice-taking actions (e.g. `Seed
  Life Support Action`, `Seed Power Routing Action`, `Pilgrim Seed Supply
  Action`, `Seed Rotational Control Action`, `Supply Spores Action`, `Supply
  Girolles Action` — six sampled, all six positive) carry **both** a `Dice
  Slot Button` (uGUI Button, `onClick` → `SendEvent("ActionStart")` to their
  own Action Controller) **and** a `Gamepad Dice Slot` child, plus an `Input
  Switcher`. `Dice Slot Button` is universal — it's the generic
  "start/click this action" element on every action panel, dice or not; only
  the End Cycle variant happens to target `Cycle Controller` with
  `SendEvent("EndCycle")` instead of an Action Controller with
  `SendEvent("ActionStart")`. The real differentiator for "does this action
  take a die" is the presence of a `Gamepad Dice Slot` sibling (which report
  6's own RESOLVED note already got right: "every dice-taking action
  station-wide has a Gamepad Dice Slot child; End Cycle actions uniquely
  have none" — the doc's older §6b text just hadn't caught up to that).
- **Input Pauser is not a "modal gate that eats input"** — it's a Rewired
  input-module remap. This means report 8's original hang hypothesis, even
  in its already-falsified form, was aimed at the wrong kind of mechanism;
  worth folding into the eventual writeup of what report 16 actually found
  (double-dispatch), since the two investigations were chasing overlapping
  symptoms with different root causes.
- **`Focus Rotator` is an empty Transform.** The doc's phrasing implies it's
  itself a gating FSM; it's a bare parent object. The logic is one level
  down, on a child literally named `Focus`.
- **`CS Dialogue Manager` isn't in the station scene at all** — it's a
  persistent object carried over from `level0` (main menu). Not a problem
  for the mod (runtime `GameObject.Find` still works across
  `DontDestroyOnLoad`), but worth knowing when static-auditing dialogue
  paths against `level1` in future — searches will always come up empty.
- **Two different "Hacking"-adjacent bool pairs exist** and are easy to
  conflate: the runtime cloud-view flag `Hacking?` (read by Leave Button and
  every hacking-node Location Button) versus `Hacking Active?`/`Bike
  Active?`, which are `Intro Sequence`-local gates for which tutorial
  trigger fires during the intro. Same near-identical naming, different
  systems.
- **The Cloud Tutorial and Hacking Action Tutorial are two separate
  panels**, not one — `Cloud Tutorial (PROMPTS)` (X-Prompt dismiss, teaches
  the S/X toggle) and `Hacking Action Tutorial` (minimal, likely the DATA
  ACTIONS die-match explainer). Report 19's grouping of "the cloud tutorial"
  as a single thing should probably split into two when the prompt-
  transcoding fix (report 1) reaches them.
