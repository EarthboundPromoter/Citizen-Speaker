# D — Selection machinery (static verification)

Static-only pass (game not running) against `level0` (MAIN MENU), `level1` (station/
world scene — the FSM-dense scene), `level2` (near-empty; 0 selection-related strings
found, not covered further). Tooling: UnityPy + TypeTreeGenerator per
`tools/analysis/README.md` conventions. Two extraction methods were used and their
reliability differs:

- **Typetree read** (`read_typetree()`) — reliable for plain Unity components
  (Selectable/Button `m_Navigation`, GameObject `m_Tag`/`m_Layer`, TMP `m_text`).
- **Raw ASCII-run extraction** (`fsm_strings`, per `dump_state_map.py`'s pattern) —
  the only method that worked for PlayMakerFSM blobs in this pass. A large fraction
  of PlayMakerFSM MonoBehaviours in this build **fail typetree read** with
  `read_str out of bounds` (confirmed on `UI Reselector`'s FSM and on the `Dice Slot
  Button` "Checker" FSM under `End Cycle Action` — both previously known-relevant
  objects). This is a generator limitation, not a data-corruption signal. Consequence:
  state **names/events/variables** below are recovered reliably; explicit
  state-to-state **transition wiring** could not be extracted for most FSMs this pass
  (only order-of-appearance in the blob, which is a serialization artifact, not proof
  of execution order). Where transition graphs were confirmed structurally (e.g. via
  `dump_endcycle_wiring.py`'s working typetree path on non-FSM components), it's
  noted.

Scratch scripts (session-local, not in repo): `sel_strings_census.py`,
`scan_selection_fsms.py`, `dump_location_markers.py`, `explore_focus_rotator.py`,
`explore_clocks.py`, `dump_fsm_full.py`, `list_components.py`, `tag_check`/
`tagmanager` inline scripts. Outputs retained alongside them in the session
scratchpad, not the repo.

---

## EVIDENCE

### Q1 — Reselector FSMs (who SETS selection)

A scene-wide scan of every `MonoBehaviour` in `level1` whose raw blob contains the
string `HutongGames.PlayMaker.Actions.UiSetSelectedGameObject` returned **589 distinct
FSM components**. `level0` (MAIN MENU) returned **9**. This is not noise: it reflects
one recurring architecture reused everywhere in the UI, not a handful of unique
systems.

**The dominant pattern — per-widget "Checker" (selection-tracker) FSM.** Owner-name
histogram of the 589 `level1` hits: `Dice Slot Button` ×257, `Gamepad Dice Slot`
×218, `Gamepad Dice Slot 1` ×30 (the `2_Hacking Action Groups` / cloud variant of the
same widget — confirmed by chain, e.g. `Flux Node 1 Action`, `Network Node 0021
Action`, `ConSec S1 Actions`), `Sequence Complete Button` ×30, `Item Cursor` ×24
(inventory item-slot cursors, e.g. under `Sabines Passkey Slot < ITEM Inventory UI`),
plus singletons: `Dice Cursor 1..5`, `Upgrade Button`, `Drive Log Button`, `RESUME`,
`CANCEL` ×2, `Back`, five Character-Window skill buttons (`ENGINEER`, `ENDURE`,
`INTERFACE`, `ENGAGE`, `INTUIT`), `100 CRYO` (DEBUG), `Gamepad Manager`, `Intro
Sequence`. Root-of-hierarchy check: 580 of the 589 hits live under `Letterbox Canvas`
(everything is one shared UI tree); 5 live under `PAUSE`.

Fully recovered example — `RESUME` (pid 1388, comp_pid 193552, chain `Pause Canvas <
PAUSE`): FSM strings include `Gamepad Checker` (the FSM's own name), `Set Selected 2`,
`Reselect` (event name), `Owner` (a self-reference variable), `Gamepad`
(mode-gate bool), `Selected` (a compared/stored bool). Shape: a self-selecting FSM
that (a) checks whether the game is in gamepad mode, (b) compares itself against
current EventSystem selection, (c) on mismatch/enable, calls
`UiSetSelectedGameObject` targeting its own `Owner` reference, i.e. **it reasserts
itself as the selection whenever it becomes eligible**. This is the concrete
"selection-tracker FSM ... showing Selected = <object>" pattern named in the brief.

Second example — `Sequence Complete Button` (pid 38870, chain `Action Elements <
Yatagan Agent 2 Action < ... < 2_Hacking Action Groups`): FSM name string `Checker 3`,
states/actions `Focus Self 2`, `Reselect`, `StoreGameObject`, `ObjectChangedEvent`,
`gameObjectVariable`, `compareTo`/`equalEvent`/`notEqualEvent`, `storeResult`,
`Gamepad`/`Mouse` mode gate, `Button?`. Shape: reads current selection via
`UiGetSelectedGameObject`, compares to self, and on inequality fires `Reselect`
(→ `UiSetSelectedGameObject` self). This IS the doctrine's "snap-back" mechanism at
first-hand: **every eligible interactive widget in the game carries its own
comparison-and-reassert FSM**; there is no single central arbiter deciding "what
should be selected" — each widget independently fights to hold/reclaim selection
while its host object is enabled+interactable. This explains why a mod-injected
selection on a foreign object (e.g. a location marker the mod picked without going
through the marker's own path) gets stomped: as soon as any other eligible widget's
Checker FSM runs its comparison and finds itself not-selected, it reselects itself.

**The scene-root backstop — `UI Reselector`** (pid 4226, inactive-by-default, no
parent, comp_pid 194664 — **typetree-unreadable**, raw strings only per prior
`dump_state_map.py` pass): states `Idle`/`Pressed`/`Check Selected`/`Reset Selection`.
Interpretation carried over from `docs/ui-state-map.md` §4 (that doc's characterization
matches the raw strings: it's a global "if Confirm/Submit is pressed and nothing is
currently selected, force a selection" fallback, not itself a per-context anchor
setter).

**MAIN MENU (`level0`) uses the identical template.** `NEW GAME` (`Menu Canvas`),
`Choose Class` (`Character Select Canvas`), `CLOSE` (`Newsletter/QR Code Canvas`),
`Slot 1` (`Slot Menu`), `CONTINUE` (`Menu Canvas`), `BACK` (`Warning Menu`), `English`
(`Language Menu`) — all carry `Set Selected`/`Set Selected 2` + `Reselect` +
`Owner`/`Gamepad`/`Deselect` strings, i.e. the same Checker FSM template used
throughout `level1`. This is one reusable prefab-level component applied game-wide,
not a station-specific system — evidence this is a deliberate, load-bearing
convention in this game's Unity project, not incidental.

**Non-widget selection setters found.** Two owners stood out as NOT being simple
per-widget checkers:
- `Intro Sequence` (pid 2872, scene root, no parent) — the intro/dialogue director FSM
  (`Idle`, `Text Intro`, `End Intro Conversation`, `Camera Transition`, `Response
  Menu`, vars `Dragos State`, `DragosSceneCount`, `Conversation`, `CycleCanEnd`) is
  itself one of the 589 `UiSetSelectedGameObject`-containing FSMs. It is the FSM
  documented in `ui-state-map.md` §3.4 as driving intro conversations. Its presence
  in this scan means **during the intro, dialogue/response-menu focus handoff is
  owned directly by Intro Sequence**, not by the Dialogue System's own C# UI code.
  Which state(s) perform the set and what exact target could not be extracted (no
  reliable typetree read for this blob); flagged UNRESOLVED below.
- `Gamepad Manager` (both scenes, scene root) also appears in the hit list, but its
  recovered strings are all about controller-type detection (`Switch or PC`, `Check
  Last State`, `Rewired.ControllerType`) with no selection-target strings visible in
  the 60-string cap — likely a shared action-list artifact (cursor lock/hide actions
  serialize adjacent to unrelated ones) rather than Gamepad Manager itself picking a
  UI target. Treat as noise unless contradicted live.

### Q2 — Selection anchors per context

Derived from Q1's evidence plus targeted subtree reads:

- **Station free-roam**: no single fixed anchor; whichever Location Button / action
  slot's own Checker FSM is currently enabled+interactable holds/reclaims it. See Q3
  for how the *initial* one is chosen.
- **Dice allocation (station and cloud)**: `Dice Cursor 1..5` (station) — each is a
  standard uGUI Button (Normal/Highlighted/Pressed/Selected states) plus its own FSM
  that calls `UiSetSelectedGameObject`, confirmed already in `ui-state-map.md` §6b and
  re-confirmed here (all five `Dice Cursor N` objects are individually present in the
  589-hit list). **Cloud/hacking data actions use the identical chain**: `Gamepad Dice
  Slot 1` (30 instances, all under `2_Hacking Action Groups`, e.g. `Flux Node 1
  Action`, `ConSec S1 Actions`, `Network Node 0021 Action`) carries the same
  `Focus`/`Idle`/`Click`/`Set Slot Pos`/`Select Dice`/`Slotted` state names and the
  same `Dice Gamepad System` coordination string as the station's `Gamepad Dice Slot`.
  This is static confirmation of triage report 19's live finding ("the die picker
  works identically inside the cloud, same Dice Gamepad System") — **CONFIRMED**, not
  just live-observed.
- **Inventory (ITEM tab)**: `Item Cursor` (24 instances, one per inventory slot, e.g.
  `Sabines Passkey Variant < Sabine Passkey Slot < ITEM Inventory UI < Inventory <
  Bottom UI`). FSM strings: `Off`/`Activate`/`Type Checker`, `Inventory Toggle`
  (Rewired action watch), `Deactivate`/`Reset Selection`/`Reset`, `UI Selector`,
  `RefocusUI`, `Data Type`/`Data`/`Item Type`/`Item`/`Slot`, `Data?` (bool — ITEM vs
  DATA tab discriminator). This is a per-slot cursor analogous to `Dice Cursor N`, not
  a single fixed inventory anchor — the anchor is whichever populated slot's cursor
  is enabled.
- **Pause**: `RESUME` under `Pause Canvas < PAUSE` (pid 1388) — see Q1's fully
  recovered example. **CONFIRMED as the pause anchor**: it is the only Pause-menu
  Checker FSM whose recovered strings show `Reselect=True` **and** carries a
  `Selected` comparison var at the top level of `Pause Canvas` (siblings `Default`
  under `Options Menu`, `CANCEL` under both Warning-Menu variants, `Back` under
  `Options Menu` are all present but are sub-menu-scoped, only relevant once those
  sub-panels are open).
- **Character window**: five skill-row buttons (`ENGINEER`/`ENDURE`/`INTERFACE`/
  `ENGAGE`/`ENGAGE`/`INTUIT`, under `SKILL List < Character Window`) and `Upgrade
  Button` (under `Upgrade UI < Top Line < Upgrade Tracker < Character Window`) all
  carry the Checker pattern. No single one is distinguished as "the" opening anchor
  in the recovered strings — **UNRESOLVED** which is default-selected on window open
  (plausibly Upgrade Button when an upgrade point is pending, matching triage report
  20's framing; needs live confirmation).
- **Drive Log**: `Drive Log Button` itself (pid 4916, `Drive System < Letterbox
  Canvas`) carries BOTH its open/close FSM (`Idle`/`MouseOver`/`Highlight`/`Open`/
  `Close`/`New?`) AND a `Gamepad Checker`/`Gamepad Checker 2` pair, i.e. the button
  that opens the log is also its own selection anchor before/after the window is
  open. Notably its strings also include `Response Menu`, `Dialogue Panel`,
  `Subtitle Panel`, `focusCheckFrequency`, `Gamepad UI off` — i.e. **this FSM
  explicitly watches dialogue-panel state** (see Q5).
- **Cloud/hacking toggle**: no dedicated "cloud selection anchor" FSM found distinct
  from the dice/data-action chain above; `2_Hacking Action Groups` action slots use
  the same Checker template as station action slots. No `Hacking?`-scoped selector
  object was located this pass — **UNRESOLVED**.
- **Dialogue/response menu**: intro-time handoff owned by `Intro Sequence` (Q1).
  Post-intro conversation selection (the general `CS Dialogue Manager` panel /
  PlaymakerInkProxy path) was **not** found among the 589 `UiSetSelectedGameObject`
  hits under any dialogue-named owner — no `Dialogue`/`Response`/`Subtitle`/
  `Continue`-named GameObject appears in the hit list at all. Either post-intro
  dialogue selection is handled by the Dialogue System's own compiled C# UI code
  (Pixel Crushers `UnityUIDialogueUGUI`/response-button prefab, outside scene FSM
  data and outside this static pass's reach), or it reuses the Intro Sequence FSM's
  own Response Menu state even post-intro. **UNRESOLVED — needs live check or DLL
  inspection.**
- **End Cycle card**: no Checker-pattern hit specific to an End Cycle summary screen
  was found; the End Cycle home buttons behave as ordinary Dice-Slot-Button-pattern
  widgets per `ui-state-map.md` §6 (unchanged by this pass).
- **Tutorial**: no PlayMakerFSM under `Letterbox Canvas/Tutorial System` appeared in
  the 589-hit list — tutorial panels do not appear to set EventSystem selection
  themselves (consistent with `ui-state-map.md`'s open question about whether
  tutorials gate input federal — they may simply not be selectable/navigable UI at
  all, relying on the underlying station selection remaining live under them, matching
  triage report 1's finding that station input can stay live under a tutorial panel).

### Q3 — Location marker graph

Walked `ERLIN MAIN/1_Station UI/Locations` in `level1`: **110 child canvases**, 108 of
which have a populated `Location Contents/Billboard Elements/Marker/Location Button`
chain (the remaining two, `Post Rim Gate` and `Rim Gate Controller`, have no
`Location Contents` child — structurally different, likely transit/gate markers, not
location-action canvases).

**Every single one of the 108 Location Button Selectable components reads:**
`m_Navigation.m_Mode = 3` (Unity's `Navigation.Mode` enum: `None=0, Horizontal=1,
Vertical=2, Automatic=3, Explicit=4` — so 3 is **Automatic**), with
`m_SelectOnUp/Down/Left/Right` all `(null)`, and `m_Interactable = 1` (serialized
default true — actual runtime interactability is separately gated, not visible in
this static field). Checked all 108; zero exceptions.

This directly contradicts the presumption in `docs/triage-queue-2026-07-18.md` report
2 / `ui-state-map.md` that a **designer-authored explicit navigation graph** exists
between Location Buttons. There is no serialized graph to read. Unity's Automatic mode
computes neighbors **at runtime** from each Selectable's on-screen `RectTransform`
rectangle (nearest-neighbor-in-direction over all enabled+interactable Selectables
sharing eligibility, not a baked adjacency list) — see SURPRISES.

**Candidate mechanism for "which marker gets initial/default selection."** Under
scene root `Focus Rotator/Focus` (the camera-orbit target — `Focus` itself carries a
`Rigidbody`, i.e. lives in 3D world space) sits `UI selector` (pid 2649, comp_pid
194025, **inactive by default in the scene**). Recovered states/strings: `Idle` →
`Select Button` → `Check Distance` → `Reset`/`Disable`/`Disabled`; action-parameter
strings `withTag` = literal `UI Button`, `ignoreOwner`, `mustBeVisible`, `layerMask`,
`invertMask`, `storeObject`/`storeDistance` into a var named `Closest UI Button`,
`Selectable?`/`Selectable`/`Interactable?` (component-presence + bool checks),
`Too Far` (event, driven by a `floatVariable lessThan Distance` compare), `Recheck`.
Shape: **a closest-tagged-object selector** — find the nearest enabled/interactable/
visible object tagged `UI Button` (within a distance threshold; beyond it → `Too Far`
→ Disable), then `Select` it.

Cross-checked the tag: the game's `TagManager` (`globalgamemanagers`) lists a
user tag literally named `UI Button` (present in the tag list read via typetree).
Location Button GameObjects all read `m_Tag = 20010` (a non-default, identical value
across all 108 — `Dice Slot Button`/`Gamepad Dice Slot` objects by contrast read
`m_Tag = 0`, i.e. Untagged). The raw index-to-name mapping wasn't resolved
numerically (2021.3 GameObject serialization stores tags by hash, not by the small
index the `TagManager.tags` array implies), so this is not a byte-for-byte proof, but
the combination — a `UI Button` tag exists in the project, Location Buttons uniformly
carry one non-default tag while ordinary action buttons carry none, and the only
FSM anywhere in the scene that filters `withTag: "UI Button"` is this closest-selector
sitting on the camera-focus rig — is strong, consistent, multi-source circumstantial
evidence that **`UI selector` is the mechanism that picks the default/nearest
Location Button marker** when the camera settles on a zone. Not fully proven; see
VERDICTS.

The same `Focus Rotator/Focus` FSM (pid 2872... actually pid distinct, comp_pid
194865) recovers the zone-gating machinery already documented in `ui-state-map.md`:
states/vars `GATED RIM`, `IntroComplete`, `HUB`, `RIM`, `RIMGATE`, `Rim Gate`,
`HubTransit`, `RimTransit`, `GreenwayTransit`, `FLOTILLAOPEN` — this is a byte-level
match to that doc's existing claim (Focus Rotator gates Rim behind IntroComplete),
now source-confirmed rather than inferred.

### Q4 — Clocks focusable?

Enumerated every GameObject with `Clock` in its name across `level1`: **4923 objects**,
155 distinct name patterns. Bulk of the count is per-step-count clock-face prefabs
instantiated once per clock-bearing action/location (`8 Step Clock`, `32 Step Clock`,
`3 Step Clock`, `6 Step Clock`, `2 Step Clock`, `12 Step Clock`, `4 Step Clock`,
`24 Step Clock` — 234 each), plus supporting display parts `Clock Outline` (1602),
`Cycle Clock Icon`/`Cycle Clock Visual` (234 each), `3 Step Accruing Clock` (171),
`Clock Description`/`Clock BG`/`Clock Name` (171 each), `Location Clock` (51),
`Clock Box` (36), `Clock Connector` (34), and dozens of one-off named story clocks.

Full component-type census across **all 4923** objects: only `Animator`,
`CanvasGroup`, `CanvasRenderer`, `MonoBehaviour` (script class unreadable — almost
certainly a clock-rendering PlayMakerFSM or a dedicated clock-display script, not a
`Button`/`Toggle`), `RectTransform`. **Zero** objects carry a `Button`, `Toggle`, or
any other Selectable-derived component. No clock object appeared anywhere in the
589-hit `UiSetSelectedGameObject` scan either (no `Clock`-named owner). The game's
own `TagManager` even carries a separate tag, `Cycle Clock Group`, distinct from
`UI Button` — clocks are tagged as a different category of object entirely, not as
selectable UI.

### Q5 — Mode-change selection handoff

- **Dialogue open (intro)**: `Intro Sequence` FSM (scene root) is confirmed (Q1) to
  itself perform `UiSetSelectedGameObject`; its recovered vocabulary (`Text Intro`,
  `Response Menu`, `Dialogue Panel`, `Subtitle Panel`, `alpha`/`interactable`/
  `blocksRaycasts` toggles) matches `ui-state-map.md`'s existing intro-conversation
  description. The mod should treat Intro Sequence as owning selection during intro
  dialogue and never contest it.
- **Dialogue open (post-intro)**: **no FSM-level evidence found** this pass (see Q2).
  Open question for live/DLL follow-up.
- **Pause open/close**: `ui-state-map.md` §3.1 already documents `PAUSE`'s own states
  performing "DIalogue UI Unfocus"/"Dialogue UI focus" — note the root `PAUSE` FSM
  itself does **not** appear among the 589 `UiSetSelectedGameObject` hits under its
  own name; only its child buttons (`RESUME`, `Default`, `CANCEL` ×2, `Back`) do.
  Interpretation: **PAUSE's root FSM only toggles panel activation** (show/hide,
  interactable/blocksRaycasts); it is each child button's own Checker FSM that claims
  selection once its panel becomes active/enabled. This is a generalizable pattern
  worth stating explicitly: mode-controller FSMs (PAUSE, presumably Character UI
  Button's Open/Close, Drive Log Button's Open/Close) manage panel visibility;
  per-widget Checker FSMs manage the actual selection handoff once a panel is live.
  This is corroborated by Character Window and Drive Log Button both having their
  window-owning object ALSO carry (or sit beside) Checker-pattern children, rather
  than the window-opening event itself carrying a selection payload.
- **Drive Log open**: `Drive Log Button`'s single FSM (pid 4916) carries its own
  Open/Close logic **and** references `Response Menu`/`Dialogue Panel`/`Subtitle
  Panel`/`focusCheckFrequency` — i.e. it appears to **gate its own Open transition (or
  its "Gamepad UI" hint-icon visibility) on dialogue-panel state**. Read literally:
  the button watches whether a dialogue is up before acting. This is a concrete
  "what the mod must never fight" data point for report 18's focus-confinement ask:
  Drive Log's own FSM already refuses to engage while dialogue is active; the mod's
  J key should mirror that gate rather than force it open. Exact condition/target
  field not extracted (typetree-unreadable FSM); **UNRESOLVED** at the byte level,
  but the string co-occurrence is a strong signal.
- **Cloud toggle**: no dedicated mode-transition FSM located distinct from the
  per-action Checker/cursor pattern already covered in Q2. **UNRESOLVED.**
- **Character window open**: `Character UI Button`'s own FSM (Open/Close/Back per
  `ui-state-map.md` §3.7) does **not** appear in the 589-hit list under its own name —
  same pattern as PAUSE: the opening control doesn't itself set selection; whichever
  internal skill/upgrade button's Checker FSM is enabled does. Confirms doctrine tier
  2 generalizes here too, but the code path for choosing WHICH internal control gets
  first selection is unresolved (Q2).

---

## VERDICTS

1. **Reselector FSMs exist as a per-widget architecture, not a single central
   arbiter.** CONFIRMED. 589 (`level1`) + 9 (`level0`) independent FSM instances
   share one "Checker" template (self-reference var, Gamepad/Mouse mode gate,
   compare-current-selection-to-self, `Reselect`/`Set Selected` on mismatch). Applied
   uniformly from the main menu through every station action slot, cloud/hacking
   action slot, inventory slot, dice cursor, and menu button. This is the
   "snap-back" mechanism named in the brief, now traced to source rather than
   inferred from live behavior.
2. **A scene-root `UI Reselector` backstop exists, inactive by default.** CONFIRMED
   (carried over from the prior `dump_state_map.py` pass; re-verified present and
   still typetree-unreadable this pass). Its role (Confirm-with-nothing-selected
   fallback) is inferred from state names, not fully proven — states `Idle`/
   `Pressed`/`Check Selected`/`Reset Selection` are consistent with, but don't
   conclusively establish, that specific behavior.
3. **Location markers are governed by a designer-authored EXPLICIT navigation
   graph.** REFUTED. All 108 populated Location Buttons use
   `m_Navigation.m_Mode = Automatic (3)` with null explicit references. There is no
   serialized adjacency to read; the graph is computed by Unity at runtime from
   on-screen rectangle positions. The mod cannot derive marker adjacency from static
   data — any "walk the graph" implementation is really "trust Unity's runtime
   automatic neighbor computation," which cannot be verified further without a live
   session.
4. **A specific FSM selects the initial/default marker when entering station view.**
   UNRESOLVED, but a strong, well-evidenced candidate was found: `Focus Rotator/
   Focus/UI selector`, a closest-object selector filtered by the game's own `UI
   Button` tag (confirmed present in `TagManager`; Location Buttons uniformly carry a
   shared non-default tag consistent with it). Mechanism (find nearest
   enabled+interactable+visible `UI Button`-tagged object within a distance
   threshold, then select it) is read from source; the *trigger* that activates this
   normally-inactive FSM (camera settling? zone entry? both?) was not found statically.
5. **Clocks are display-only, not part of any navigation graph.** CONFIRMED. Zero
   `Button`/`Toggle`/Selectable-derived components across all 4923 clock-related
   objects in `level1`; a distinct non-UI-Button tag (`Cycle Clock Group`) is used for
   them; no clock object appears in the 589-hit selection-setting scan. The
   clock-cycling idiom (triage report 14) should stay a pure readout — it has nothing
   to engage focus-wise, confirming the repo's own tier-3 doctrine applies here
   ("no native selectables" → mod-side review walker/cycler, never a focus engage).
6. **Mode-controller FSMs (PAUSE, Character UI Button, Drive Log Button's open/close
   logic) themselves set the post-transition selection.** REFUTED as a general rule.
   In every case checked, the mode-controller only toggles panel
   active/interactable/blocksRaycasts state; selection handoff is performed
   separately by whichever child widget's own Checker FSM becomes eligible. The one
   confirmed exception is `Intro Sequence`, which does appear to set selection
   directly for its Response Menu / Text Intro states — worth treating as a special
   case, not the general pattern.
7. **Dialogue/response-menu selection is FSM-driven throughout the game (both intro
   and post-intro conversations).** UNRESOLVED / PARTIALLY REFUTED. Only confirmed
   for the intro (`Intro Sequence` FSM). No FSM evidence was found for post-intro
   conversations run through the general `CS Dialogue Manager` panel — plausibly
   handled by Pixel Crushers' own compiled C# UI code outside scene-serialized FSM
   data, which this static pass cannot reach without decompiling `Managed\` DLLs.

---

## OPEN DESIGN QUESTIONS

- Since Location Button navigation is Unity Automatic (runtime rectangle-based), the
  mod cannot pre-compute or validate the marker adjacency graph offline. Does the
  mod's L-key design need a live-observation fallback (e.g., query current selection
  + move-event result) rather than any static graph reference?
- If `UI selector` (Q3/VERDICT 4) is confirmed live as the initial-marker selector,
  is it re-triggered every time station view is (re-)entered (e.g., after closing a
  window), or only once per zone/camera settle? This changes whether the mod's L key
  needs to nudge/re-trigger it versus simply reading current selection.
- Post-intro dialogue selection handoff (VERDICT 7) is a real gap: if it's owned by
  Dialogue-System-for-Unity's own compiled UI code rather than any FSM, the mod may
  need a different detection strategy (e.g., watching the Dialogue Panel's
  active/alpha state plus native EventSystem.currentSelectedGameObject) rather than
  looking for an FSM-level "set selection" signal.
- Drive Log Button's apparent dialogue-panel gate (VERDICT/Q5) suggests other
  window-opening controls (Character UI Button, Inventory toggle) may have similar
  self-gates against dialogue/other-modal state. Worth a systematic live check before
  the mod builds its own modal-gate detection — the game may already refuse to open
  windows in states the mod would otherwise need to guess about.
- Character window's default-selected control on open (Upgrade Button vs. first
  skill row) is unresolved and matters directly for triage report 20 (character menu
  reportedly non-functional) — recommend prioritizing a live check here since it's
  both a known bug report and a data gap this pass couldn't close.

---

## SURPRISES

- **The "designer navigation graph" the repo's own docs presumed for Location Buttons
  does not exist as serialized data.** `docs/triage-queue-2026-07-18.md` report 2 and
  `docs/ui-state-map.md` both describe/assume an explicit graph; source shows
  Automatic mode with all explicit slots null, uniformly across all 108 markers. This
  doesn't invalidate the report's design direction (arrows still drive Unity's own
  navigation), but it does mean the mod has literally nothing to read offline about
  marker adjacency — everything is runtime-computed.
- **The reselector "doctrine" understates its own scale.** The repo's existing
  characterization (`ui-state-map.md` §4: "Per-button 'Gamepad Checker' FSMs (e.g.
  PAUSE/RESUME, dice slot buttons)") reads as a short, illustrative list. Source shows
  this is not a short list — it's a single reusable component instantiated **598
  times** across both scenes, on effectively every interactive UI element in the
  entire game including the main menu. This is worth internalizing as "the" selection
  architecture, not one pattern among several.
- **The cloud/hacking dice-picker being identical to the station one — previously a
  live-only finding (triage report 19) — is now statically confirmed** down to
  matching FSM state names and shared `Dice Gamepad System` targeting, across all 30
  `Gamepad Dice Slot 1` cloud instances.
- **A large fraction of PlayMakerFSM blobs in this build are typetree-unreadable**
  (`read_str out of bounds`), including several of the most decision-relevant ones
  (`UI Reselector`, every `Dice Slot Button`/`Gamepad Dice Slot` Checker FSM, `UI
  selector`). This is a tooling ceiling for future static work on this game's FSMs,
  not specific to this task — raw ASCII-run extraction remains the reliable fallback,
  but it cannot recover transition wiring (only names), so anything requiring exact
  state-machine graphs (not just vocabulary) will need either a working typetree path
  for that specific FSM shape or live/runtime confirmation.
- **Portrait Name TMP text reads the literal placeholder string `'LOCATION'` for
  every one of the 108 markers**, not the real location name. This means the
  rendered location label is set at runtime (from a data source/localization table),
  not baked per-canvas in the scene — a caveat for anyone relying on static
  extraction for marker labels (out of this report's scope, noted for whoever
  builds Q3's L-key announcement wiring).
