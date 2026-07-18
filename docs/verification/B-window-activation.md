# B — Window activation model (static verification)

Source: UnityPy + TypeTreeGeneratorAPI extraction against `level1` (Citizen Sleeper_Data),
2026-07-18. Game not running; nothing here is live-observed. All object paths are under
`Letterbox Canvas` unless noted. Scripts used: `find_targets.py`, `find_partial.py`,
`dump_windows.py`, `dump_extra.py` (scratchpad, not repo — patterns follow
`tools/analysis/dump_endcycle_wiring.py`).

**Tooling caveat (affects every FSM citation below):** TypeTreeGeneratorAPI's structured
`fsm` typetree read (states/transitions as nested dicts, the technique
`dump_endcycle_wiring.py` used successfully on the End Cycle button) **fails** with
`EOFError: read_str out of bounds` on every FSM component in this window set (Character UI
Button, Character Window sub-FSMs, Inventory, Drive Log Button, Drive Log's per-entry FSM,
PAUSE). Isolated retries confirm it's a per-object parse failure, not caching. Fallback used
throughout: ASCII-run extraction from the raw serialized blob (same technique as
`dump_state_map.py`) — this recovers state names, event names, action class names, and
watched variable/action names reliably, but **not** the state→event→transition graph (which
state fires which transition on which event). Where I mark a transition, it's inferred from
button `m_OnClick` wiring (which typetree reads fine) or from naming convention, not read
directly off the FSM graph. Marked accordingly below.

## EVIDENCE

### 1. Character UI Button — click wiring (Q1)

Object: `Letterbox Canvas/Character UI/Character UI Button` (pid 2217, both it and its
parent `Character UI` pid 279 are ACTIVE in the scene default).

- `Button` component: `m_Interactable = 1`, `m_Navigation.m_Mode = 0` (None).
  `m_OnClick`: **1 persistent call** → target = the button's own `PlayMakerFSM` component
  (pid 193885, on the same GameObject) → method `SendEvent`, mode 5 (String argument) →
  **string argument `"Open"`**.
- The FSM itself (pid 193885, ASCII-recovered): state names `Set Up`, `Idle`, `MouseOver`,
  `Open`, `Close` (+ sub-states `Highlight`, `MouseOff`, `Highlight 2`, `Back`, `Active`,
  `Deactivate`). Action classes present: `HutongGames.PlayMaker.Actions.ActivateGameObject`,
  `SetProperty`, `SetAnimatorBool`, `SendEvent`, `Wait`, `GetOwner`, `BoolTest`, `FMODPlayOneShot`
  (`event:/UI Sounds/Skills Menu Open`, `Skills Menu Close`, `Skills Tab Mouseover`),
  `PixelCrushers.DialogueSystem.PlayMaker.SetVariable` (writes bool `UpgradeAvailable`), and
  **three separate `Rewired.Integration.PlayMaker.RewiredPlayerGetButtonDown` actions**. The
  string `"Character Screen"` appears once, positioned adjacent to the `actionName`/`playerId`
  fields these Rewired actions use — near-certainly the watched Rewired action name for (at
  least) one of the three watches, but typetree failure means I cannot confirm which action
  instance carries it nor what its `isTrueEvent` target is.
- **No string `"Click"` appears anywhere in the FSM's recovered strings** (full list in
  `dump_windows.out.txt` — checked: `Set Up | Idle | MouseOver | Highlight | Open | SFX | ... |
  MouseOff | ... | Close | Highlight 2 | ... | Back | Active | Deactivate | ... | RefocusUI |
  ... | CloseAction | Disappear | ForceUnslotDice | Leave | Clear Button`). ASCII extraction
  reliably recovers PlayMaker event names elsewhere (End Cycle's `EndCycle`, Drive Log's
  `Reset`/`Update Tracking` below all show up cleanly) so this is a real absence, not a
  scan miss.
- One `SetProperty` action targets `UnityEngine.CanvasGroup.interactable` (bool) on an object
  the recovered strings label `"Station UI Canvas"`. I could not locate a GameObject with
  that exact name anywhere in `level1` by exact-name search — **UNRESOLVED**, see Open
  Questions. This looks like a side effect fired *by* opening (disable background station
  input while the character screen is up), not a precondition gating the open.
- Rendered label under `Character UI`: child `Character/Character` TMP text = `"View Character"`
  (string-table field `CSUI_CHARACTER_UI_VIEW`); a second FSM on that child (`Checker`/
  `Update Tracker`/`Points or no?`) swaps in string-table field
  `CSUI_SKILL_LIST_UPGRADES_AVAILABLE` when `UpgradeAvailable` is true and reads
  `Player_UpgradePoints` via `FloatCompare`. Sibling `New Shine` (Animator, controller
  `Action Elements`) is the presumable upgrade-available glint. Sibling `Gamepad Prompt R`
  (CanvasGroup `interactable=False blocksRaycasts=False` — display-only) renders the
  controller-button icon for this action — i.e. the game renders this as an **R-button**
  (right shoulder) prompt, consistent with a Rewired watch rather than pure click-to-open on
  gamepad.

### 2. Character Window — open/close signal (Q2, Q3)

Object: `Letterbox Canvas/Character Window` (pid 2901).

- Root `CanvasGroup`: `alpha=0.0, interactable=False, blocksRaycasts=False` (serialized rest
  state — i.e. **closed** by default). `Animator` present, `controller` resolves to an asset
  named `'Neg explain'` (pid 1567) — an odd/probably-reused controller name; flagged under
  Surprises.
- Child `Upgrade Tracker` (FSM, ASCII): states `Update`, `Checker`, `Update Tracker`,
  `Up Or Down`; watches `Player_UpgradePoints` (`FloatChanged`), writes the `Points` TMP text,
  and on increase plays `event:/UI Sounds/Skills Upgrade Available` and sets bool
  `UpgradeAvailable`. Rendered labels nearby: `"UPGRADE POINTS"`, `"1 UPGRADE POINT"` /
  `"2 UPGRADE POINTS"` / `"3 POINTS"` (per-count label variants), `"UPGRADE"` button label.
  `Upgrade Button` under `Points UI` is itself gated: `CanvasGroup interactable=False
  blocksRaycasts=True` at rest, `onClick` → `SendEvent("Activate")` to its own small FSM (ASCII
  states `Platform / Gamepad`, `Switch`) plus a `Play` (sound) call.
- `SKILL List` (pid 528, child of Character Window): four skill entries, each a sibling
  GameObject carrying its own large per-skill FSM (~130 KB blob, typetree fails on all four):
  **ENGINEER** (`"Work with machines and physical tools."`), **INTERFACE**
  (`"Work with digital interfaces."`), **ENDURE** (`"Use strength or strength of will."`),
  **INTUIT** (`"Approach problems with awareness."`) — rendered skill names/descriptions and
  two perk name/description pairs each (perk text omitted here per spoiler discipline where
  it reads as mechanic-reveal; names are UI labels, kept). Recovered FSM strings per skill:
  `Broken`, `Get Skill Rating`, `Upgrade`, `Check Upgrade Points` (×5), `Confirm?`,
  `Upgrade Invalid`, `Upgrade Valid`, `UPGRADE`, watches `Player_UpgradePoints`, writes a
  `<SKILL>_PERKS` bool, plays `event:/UI Sounds/Skills Cannot Upgrade` and
  `Skills Upgrade Skill`.
  - Each skill has up to 4 `Upgrade Button N` / `Upgrade Button Perk N` variants (some
    `INACTIVE` — active set depends on current point count / perk ownership, matches the
    `Check Upgrade Points` branches). Every one wired identically: `Button.interactable=1`,
    `navMode=3` (Automatic — see nav caveat below), `onClick` → `SendEvent("Upgrade")` on the
    skill's own FSM.
  - A `BREAKDOWN button` variant (rendered `"REPAIR?"`) exists per skill (`INACTIVE` at rest,
    for the skill-broken state — matches ui-state-map's unverified Breakdown modal; still
    unreached live) — same `SendEvent("Upgrade")` wiring.
  - **Confirm sub-panel** per skill (`[Confirm?]`, `CanvasGroup alpha=0 interactable=False
    blocksRaycasts=False` at rest): rendered `"UPGRADE SKILL?"`, `Confirm Button`
    (`navMode=4` Explicit, `nav.right → Back Button`) whose `onClick` fires **two** calls —
    `SendEvent("Upgrade")` again (same event name, second context — this is the actual spend
    commit) and a `Play` sound; `Back Button` (`navMode=4`, `nav.left → Confirm Button`)
    → `SendEvent("Back")` + `Play`. Cost labels `"COST | 1/2/3 UPGRADE POINT(S)"` sit beside it
    (inactive placeholders, count-gated).
  - **Upgrade-spend flow (inferred from wiring, not the transition graph):** focus an
    `Upgrade Button` (or `Upgrade Button Perk N`) → Enter/click sends `SendEvent("Upgrade")` to
    the skill FSM, which opens the `Confirm?` sub-panel → focus moves to `Confirm Button` /
    `Back Button` (only these two are Explicit-nav-linked to each other) → Confirm re-sends
    `"Upgrade"` (commit) or Back sends `"Back"` (cancel).

### 3. Inventory strip — activation model + item structure (Q2, Q4)

Object: `Letterbox Canvas/Bottom UI/Inventory` (pid 365, ACTIVE; `CanvasGroup
alpha=1.0 interactable=True blocksRaycasts=True` at rest — the strip is semi-persistent,
matching the doc's "semi-modal" characterization).

- The `Inventory` FSM (ASCII): states `Setup`, `Item`, `Data`, `D to I`, `Swap`, `Activate`,
  `I to D`, ... Action classes include a `Rewired.Integration.PlayMaker.RewiredPlayerGetButtonDown`
  whose adjacent strings include `Inventory Toggle` and `Data?` — the FSM **polls the Rewired
  action `"Inventory Toggle"` directly**, every frame, independent of any button.
- Sibling `Inventory Button (OLD)` — **INACTIVE**, dev-labeled deprecated. It still carries a
  complete click→`SendEvent("Open")`-to-its-FSM wiring identical in shape to Character UI
  Button/Drive Log Button, but is disabled in the live scene. **This means Inventory's
  sanctioned activation path today is the Rewired watch alone — there is no live clickable
  button for it**, unlike Character (button live) and Drive Log (button live).
- `ITEM Button` and `DATA Button` (siblings, both ACTIVE): plain uGUI Buttons,
  `navMode=0`, `onClick` → `SendEvent("Item")` / `SendEvent("Data")` respectively, both
  targeted at the same Inventory FSM (pid 193138). Rendered labels `"ITEMS"` / `"DATA"`.
- Item representation: `Inventory Data` (pid 1240, logic-only, not part of the visual
  hierarchy) holds **24 `<Item> Manager` FSM objects** (e.g. `Stabilizer Manager`,
  `Cryo Chits Manager`, `Imprinted Shipmind Manager`, `Scrap Components Manager`, ... full
  list in `dump_windows.out.txt` tail) plus **20 `RNG <...>` FSM objects** (loot-roll/payout
  logic, not inventory items). Each Manager FSM (ASCII): states `Checker`, `Checker 2`,
  `Update Inventory`, `Updating Inventory`; watches one global float var per item
  (`INV_Stabilizer`, `INV_ScrapComponents`, `INV_ImprintedShipmind`, etc., or `Player_Bits`
  for Cryo Chits) via `FloatChanged`, on change plays a sound (`Inv Item Gain` / `Inv Data
  Gain` / `Inv Chit Gain`) and sets an `Slot On`/`Slot Off` trigger. **None of these Manager
  objects carry a Button, Toggle, or any Selectable component** — they are pure count-tracking
  logic, not focus targets.
- Visual item slots live separately under `ITEM Inventory UI` / `DATA Inventory UI`
  (children of `Inventory`, both `CanvasGroup alpha=1 interactable=True` at rest): a grid of
  `<Item> Slot` GameObjects (e.g. `Cryo Slot`, `Stablizer Slot`, `Encrypted Key Slot` — most
  `INACTIVE` at rest, presumably `SetActive(count>0)` at runtime). Each slot is **Image +
  Animator only** (controller `Change Notification`) — **no Button/Selectable component on
  any slot**. Confirms: item entries are **display-only, not focus-walkable**.
- `Inventory Display` (single reusable info card, sibling of the tab buttons): CanvasGroup
  `alpha=0 interactable=False blocksRaycasts=False` at rest; small FSM (`SetUp`/`Appear`/
  `Disappear`) that does `GetComponent` on child `Item Name` / `Item Description` TMP texts.
  This is the one place item name+description render — it looks like a single popover that
  something else (mouse hover, most likely — no static evidence of a keyboard/gamepad trigger
  for it) populates and shows, not a per-slot always-visible label. **UNRESOLVED**: what
  drives `Appear`/`Disappear` — no caller found in this pass.

### 4. Drive Log — window + entries (Q2, Q5)

Object: `Letterbox Canvas/Drive System/CS Drive Log` (pid 3247), a
`PixelCrushers.DialogueSystem.Wrappers.UnityUIQuestLogWindow` (confirmed via typetree —
this component reads fine; fields: `mainPanel`, `activeQuestsButton`, `completedQuestsButton`,
`questTable`, `questGroupTemplate`, `questTemplate`, `pauseWhileOpen`,
`unlockCursorWhileOpen`, `useGroups`, `trackOneQuestAtATime`, ...).

- `CanvasGroup` on `CS Drive Log`: `alpha=0.0, interactable=True, blocksRaycasts=True` at
  rest — **note the contrast with Character Window**: alpha is the closed signal here too,
  but `interactable`/`blocksRaycasts` stay `True` at alpha 0 (Character Window's are `False`).
  Whether the Animator (`controller = 'Image (6)'`, pid 291) also drives those two fields at
  runtime is not visible from the serialized rest values — flagged as a caveat, not a
  contradiction, since Animator curves aren't captured by a static rest-state read.
- `Drive System/Drive Log Button` (pid 4916, ACTIVE): same shape as Character UI Button —
  plain `Button`, `onClick` → `SendEvent("Open")` on its own FSM (pid 194965). ASCII strings
  on that FSM include `UiSetSelectedGameObject`, `CallMethod` targeting
  `PixelCrushers.DialogueSystem.Wrappers.UnityUIQuestLogWindow`, `Gamepad Checker`,
  `RefocusUI`, `No Gamepad Focus`/`autoFocus`, and two FMOD events (`Drive Open Drive Menu`/
  `Drive Close Drive Menu`) — i.e. the FSM both calls a method on the quest-log-window script
  directly *and* sets EventSystem selection on open (the tier-2 doctrine anchor). No
  `RewiredPlayerGetButtonDown`/Rewired-watch string turned up on this FSM's own blob (unlike
  Character UI Button) — Drive Log looks click-only from statics; a hotkey binding, if any,
  isn't visible here.
- Tab buttons `Active Button` / `Completed Button` (under `Quest Log Window Main Panel/
  Main Button Horizontal Group`, rendered `"ACTIVE"`/`"COMPLETE"`): `navMode=3` (Automatic —
  the `nav.down`/`nav.right` fields present are vestigial under Automatic mode, see caveat
  below), `onClick` → `SendEvent("Reset")` on a **different** FSM (pid 217194) than the
  window-open FSM — this is presumably a dedicated quest-list-refresh controller.
- `Close Button` (**INACTIVE** at rest): wired *not* through the FSM at all — `onClick` calls
  the C# method `ClickCloseButton` directly on the `CS Drive Log` component (mode 1 = void
  call). Different mechanism from every other button in this document.
- Runtime entry template: `Quest Template` (under `.../Scroll Area/Scroll Rect/Scroll
  Content`) carries the standard PixelCrushers quest-template script (fields `heading`,
  `description`, `entryContainer`, `entryDescription`, `trackButton`, `abandonButton`) **plus**
  its own small FSM: states `Checker`, `UnTracked`/`UNTRACKED`, `Tracked`/`TRACKED` — compares
  its own GameObject name against a var the strings label `Tracked Quest Name`
  (`StringCompare`/`GetName`), and on match recolors an `Image` — **this is the tracked-state
  signal**, not a Toggle component.
- `Quest Heading Button` (per-entry heading row, e.g. rendered placeholder `"DRIVE NAME"` /
  `"DRIVE"`): plain Button, `onClick` → `Play` (sound) only, no `SendEvent`. Children
  `Tracked Icon` and `Tracked Color` (both plain Images, no logic of their own) — the visual
  targets the template FSM above recolors/re-sprites.
- `Track Button` (nested under the heading row, rendered `"TOGGLE TRACKING"`): plain Button,
  `onClick` → `Play` + `SendEvent("Update Tracking")` targeted at the **same** pid 217194 FSM
  the tab buttons use.
- Scroll/list mechanics: `Scroll Area` carries its own FSM (`Gamepad?`/`Stick Scrolling`/
  `Mouse Scrolling`) reading a Rewired axis (`Selection Axis Vertical`) to drive a
  `UnityEngine.UI.Scrollbar.value` — gamepad stick scroll is FSM-mediated, not native
  Selectable navigation.

### 5. PAUSE — comparison baseline (Q2)

Object: root `PAUSE` (pid 286).

- Unlike Character Window / Drive Log, the open/close signal is a plain **GameObject active
  toggle** on `Pause Canvas` (child, `INACTIVE` at rest) — no CanvasGroup fade, no Animator
  gate on the canvas itself.
- All menu buttons (`RESUME`, `OPTIONS`, `QUIT TO MENU`, `QUIT GAME`, warning-dialog
  `QUIT`/`CANCEL` pairs, options-menu `Back`) are plain uGUI Buttons, `navMode=4` (Explicit,
  vertically chained via real `nav.up`/`nav.down`), each `onClick` → `SendEvent(<verb>)` on
  the **same** root FSM (pid 193115) — event names `Unpause`, `Options`, `ExitToMenu`, `Exit`,
  `Back`. One root FSM handles the whole menu tree, vs. Character/Drive Log's per-button
  private FSMs.

## VERDICTS

1. **Character window sanctioned open path — CONFIRMED (structurally).** The uGUI `Button`
   IS live (`interactable=1`, GameObject chain active) and its `onClick` fires exactly one
   call: `SendEvent("Open")` on its own FSM. **REFUTED: there is no `"Click"` event on this
   FSM** — no such string exists anywhere in the recovered blob, while every other named event
   on it (`Open`, `Close`, `Back`, `Active`, `Deactivate`, `RefocusUI`, `ForceUnslotDice`,
   `Leave`, `Clear Button`) does. If the mod's fallback path sends a generic `"Click"` event,
   it is chasing a name that does not exist on this FSM and will silently no-op — this is a
   strong, evidence-backed candidate for why U fails. **Recommended route: drive the button
   like every other working button in the mod (focus it, invoke uGUI submit/onClick) so
   `SendEvent("Open")` fires naturally — do not hand-construct a `SendEvent` call with a
   guessed event name.** A second, independent path may exist: the FSM also polls Rewired
   action `"Character Screen"` directly (three `RewiredPlayerGetButtonDown` actions present),
   mirroring Inventory's pure-Rewired-watch model — but I could not confirm its target event
   without a working typetree read, so I list it as a *candidate* alternate path, not a
   confirmed one.
2. **Gating cause of the U-key failure — UNRESOLVED (needs live check).** No static evidence
   of a disabling ancestor CanvasGroup, an inactive object in the chain, or an
   `IntroComplete`/tutorial gate literally wired into this FSM's recovered strings. The one
   CanvasGroup-`interactable` write found is a `SetProperty` the FSM performs *on opening*
   (disabling a `"Station UI Canvas"`-labeled object, which I could not locate by exact name —
   see Open Questions), i.e. an effect of success, not a precondition. The failure is most
   likely one of: (a) EventSystem selection isn't actually on this button when U fires, (b)
   the mod's U handler sends a different event/method than the button's real wiring, or (c) a
   runtime-only gate (script beat, active save state) invisible to static scene defaults.
   Needs one live bridge read: `Button.IsInteractable()` + `EventSystem.currentSelectedGameObject`
   at the moment U is pressed, and a check of exactly what the mod's current U handler sends.
3. **General window model — CONFIRMED, two-and-a-half patterns, not one.**
   - *Click→own-FSM-event* (Character UI Button, Drive Log Button, all PAUSE buttons, Drive
     Log's Active/Completed/Track buttons, all Character skill Upgrade/Confirm/Back buttons,
     Inventory's ITEM/DATA buttons): plain uGUI `Button.onClick` → `SendEvent(<verb>)` on an
     FSM. **The verb is never a fixed convention — it's chosen per-FSM** (`Open`, `Upgrade`,
     `Back`, `Reset`, `Update Tracking`, `Item`, `Data`, `Unpause`, `Exit`...). This
     reconfirms `docs/ui-state-map.md` §6's End Cycle finding generalizes: always read the
     actual `m_OnClick` string argument, never assume a name.
   - *Rewired-watch-on-the-window's-own-FSM, no button* (Inventory): the FSM polls a Rewired
     action every frame; the one button that used to open it is present but disabled
     (`Inventory Button (OLD)`, INACTIVE).
   - *Direct C# method call, bypassing FSM entirely* (Drive Log's `Close Button`): `onClick`
     invokes `ClickCloseButton` on the window script directly.
   - Character UI Button is a **hybrid** of the first two (live button + own Rewired watch).
4. **Open-state signal per window — CONFIRMED, not uniform.** Character Window and Drive Log
   both expose `CanvasGroup.alpha` on the window root as the anchor (Animator-driven), matching
   `docs/triage-queue-2026-07-18.md` report 17's live finding for Drive Log — but Character
   Window's `interactable`/`blocksRaycasts` co-vary with alpha at rest (0/False/False) while
   Drive Log's don't (0/True/True), so an announcer keyed only on `interactable` would miss
   Drive Log and one keyed only on alpha is the one signal that generalizes across both.
   Inventory has no single "closed" state — it's semi-persistent; the meaningful signal is
   which FSM state (`Item`/`Data`) is current. Pause uses GameObject-active, not CanvasGroup,
   on `Pause Canvas`.
5. **Character window structure (Q3) — CONFIRMED.** Class portrait FSM (`Get Class`,
   compares `Player_Class` against `EXTRACTOR`/`MACHINIST`/`OPERATOR`, swaps a sprite).
   Upgrade Tracker FSM watches `Player_UpgradePoints`, drives the points display and
   `UpgradeAvailable` bool. Four skills (`ENGINEER`/`INTERFACE`/`ENDURE`/`INTUIT`), each with
   its own large FSM; upgrade spend = focus an Upgrade Button → `SendEvent("Upgrade")` opens a
   Confirm sub-panel (Explicit nav, Confirm↔Back cross-linked) → Confirm re-sends `"Upgrade"`
   to commit, Back sends `"Back"` to cancel.
6. **Inventory structure (Q4) — CONFIRMED.** 24 per-item `Manager` FSMs are logic-only
   (no Selectable component anywhere on them) tracking global `INV_*`/`Player_Bits` floats.
   The matching visual `<Item> Slot` objects (separate hierarchy, `ITEM Inventory UI`/
   `DATA Inventory UI`) are Image+Animator only — also no Selectable. **Item entries are not
   focus-walkable; they are passive, count-gated visual slots.** The single `Inventory
   Display` info card is the only place name+description render, and its trigger is
   unresolved (see Open Questions).
7. **Drive Log entry structure (Q5) — CONFIRMED for statics.** Template object (`Quest
   Template`) has a small per-entry FSM that is the tracked/untracked signal (name compare
   against `Tracked Quest Name`, recolors `Tracked Icon`/`Tracked Color` on the heading row).
   Track state toggling is a plain Button (`Track Button`, rendered `"TOGGLE TRACKING"`) →
   `SendEvent("Update Tracking")`, not a Toggle component. Tab state: Active/Completed are
   plain Buttons → `SendEvent("Reset")` on the same controller FSM as Track Button; no
   separate persisted "current tab" variable was located in this pass (see Open Questions).

## OPEN DESIGN QUESTIONS

- Which of the three `Character Screen`-adjacent `RewiredPlayerGetButtonDown` actions on
  Character UI Button's FSM is the actual "open" watch, what event it fires on true, and
  whether it duplicates or differs from the button's `"Open"` — needs either a live
  input-triggered observation or a typetree workaround (see Tooling note below).
- Where is `"Station UI Canvas"` actually named/located? Exact-name search across `level1`
  found nothing; it may be a differently-cased or differently-spaced name, or an object this
  particular FSM instance references via a prefab-level default that gets overridden per
  scene. Matters for understanding what exactly gets locked out while the character screen is
  open.
- What triggers Inventory's `Inventory Display` info-card `Appear`/`Disappear`? No caller
  found among the Inventory FSM's recovered strings in this pass — likely mouse-hover-only,
  unconfirmed.
- Drive Log: is there a persisted "current tab" (Active vs Completed) variable anywhere, or is
  tab state purely which button was last clicked (no readable state variable)? Not located
  here — would need a targeted variable dump on FSM pid 217194 (typetree-broken; ASCII-only).
- Character Window's Animator controller resolving to an asset literally named `'Neg explain'`
  — worth confirming this isn't a misresolved pid before building an Animator-bool-based open
  signal around it (see Surprises).
- Whether Drive Log's `interactable`/`blocksRaycasts=True`-at-alpha-0 rest state is overridden
  by Animator curves at runtime (i.e. whether the window is truly click-through while
  "closed") — static rest-state reads can't answer this; would need a runtime read while the
  window is confirmed closed.

## SURPRISES (source contradicts or extends repo docs)

- `docs/triage-queue-2026-07-18.md` report 20 frames the U-key failure as "probably the FSM's
  own Open event via its Rewired watch — needs the Click/submit route verified." Statics
  invert the framing: the **click/submit route is the one with confirmed, correct wiring**
  (`onClick` → `SendEvent("Open")`); it's the **Rewired-watch route that's unconfirmed** (typetree
  broken on that specific action). If the mod's current fallback logic assumes the reverse —
  that clicking is unreliable and a hand-sent `"Click"` event is the "real" path — that
  assumption is backwards and the event name is also wrong (no `"Click"` event exists on this
  FSM at all).
- Inventory turns out to have **no live clickable open button at all** — the one that looks
  like it should exist (`Inventory Button (OLD)`) is explicitly disabled by the developers.
  This wasn't called out in `ui-state-map.md` §3.8, which described Inventory only via its FSM
  states/Rewired action without noting the dead button sitting right next to it.
- Character Window and Drive Log look like the same "CanvasGroup + Animator" pattern from the
  outside (per triage report 17's Drive Log finding), but their rest-state `interactable`/
  `blocksRaycasts` fields disagree (False/False vs True/True) — a naive announcer built to
  Drive Log's pattern alone would misread Character Window's closed state, and vice versa. Use
  `alpha` as the one thing that generalizes.
- Several buttons' serialized `m_Navigation.m_Mode` is `3` (Automatic) while still carrying
  populated `m_SelectOnUp/Down/Left/Right` fields (e.g. Drive Log's Active/Completed tabs,
  Character's plain Upgrade Buttons). Under Automatic mode Unity computes neighbors
  spatially at runtime and **ignores** those explicit fields — only the `Confirm Button`/
  `Back Button` pair (mode `4`, Explicit) and PAUSE's menu (mode `4`) have nav graphs that are
  actually authoritative from statics. Don't build a nav-graph doc off the Automatic-mode
  fields without flagging them as non-binding.
