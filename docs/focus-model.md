# Focus model — per-surface table (2026-07-19)

Status: **WALKED with the owner, 2026-07-19 — design pass complete.** Rows
marked WALKED/CORRECTED carry owner rulings verbatim; unmarked rows were
confirmed as drafted. Nothing is built yet. Rows cite brief E / the corpus /
live findings; unproven mechanics are listed under "Opens" per the method
hierarchy, not wired dark. Companion to
build-plan W3 (amended 2026-07-19) and verification/H-focus-assessment.md.
input-model.md remains the authoritative keymap; this document adds the focus
layer those keys act in.

**Channel law (binding, from W3):** a mod-constructed index or review view never
holds or moves EventSystem selection while browsing. Browsing walks mod-owned
cursor state; commit performs exactly one designed native activation, then the
native channel owns focus again. Native navigation is preserved as-is beside every
index.

**Precedence rule:** keys belong to the surface whose input affordance the game is
currently rendering or holding. Pause always outranks. A rendered interactive
affordance (tutorial dismiss, response menu, dialogue continue, dice cursor, item
cursor, window anchor) outranks ambient wrappers (cycle transition, autoplay,
scene flags). Selection identity is a first-class signal — the game's own idiom.
Ambient wrappers own keys only when no interactive affordance is up.

Channel policies: NATIVE (ride the game's structure as-is), NATIVE+INDEX (native
preserved, mod index alongside), RERENDERED (mod-owned browse over data/rendered
truth), READOUT (announcement stream, no focus).

---

## 1. Title menu — WALKED 2026-07-19

- Affordance: title scene + its menu buttons (game-selected; watchdog checkers —
  native nav sound).
- Full surface (title corpus): Landing (PRESS TO START) → Menu Canvas: NEW GAME,
  CONTINUE, **Update News (opens the QR Code Canvas: Newsletter panel + CLOSE)**,
  options, Language Menu (4 languages), Slot Menu (3 slots with the game's own
  Empty/Filled states), warning menus.
- Policy: NATIVE. Coverage gap (owner report): several items never speak —
  likely icon-only labels or off-graph placement. W3 acceptance item: every
  title item speaks; QR canvas announces its rendered text + CLOSE on open.
- Options menu: see row 16 (shared with pause).
- Recovery: re-anchor to the game's current menu selection; empty-Enter target
  TBD (title has no UI selector).

## 2. Character select

- Affordance: carousel + START (game-selected); review cursor overlays.
- Anchor: game's own (Continue/START focus never announced).
- Policy: RERENDERED browse (CharacterSelect review, shipped + verified) over
  native carousel; Left/Right commit via native carousel button clicks (no
  keyboard path exists in-game).
- Recovery: re-enter review top; native anchor untouched.
- Opens: none — shipped and stable.

## 3. Station free-roam

- Affordance: no overlay affordance up; ambient camera mode with spatial marker
  selection.
- Anchor: UI selector (closest visible UI Button within 3000 units,
  interactable-checked, zone-repositioned by Location Controller) — E, decoded.
- Policy: NATIVE+INDEX. Native: Automatic-spatial arrows (no authored graph
  exists — the spatial computation IS the designed nav; preserves geographic
  intuition). Index: **N tree** (owner design, input-model.md): two levels, region →
  reachable locations, reachability-filtered, camera-for-free.
- Recovery: empty-Enter → UI selector Reset (shipped, Confirm-backstop mirror).
- Commit from tree: set native selection to the target marker via designed
  machinery.
- **Native camera keys LIVE-CONFIRMED 2026-07-19 (owner-observed on screen):**
  W/S scroll the camera, A/D rotate it, even in mod-asserted gamepad mode. This
  corrects brief F's "Rotate View = device probe only." Binding rule (owner):
  the mod works WITH native camera movement, never stomps it. Consequences for
  table eval: **S (scan) must rehome** (it stomps dedicated down/backward camera
  movement) and **D (dice query) must rehome** (it overlays the rotate key).
  Camera movement partially delivers "watch the map while browsing" natively.
- **WALKED 2026-07-19, decisions owner-made (two-census experiment, fresh intro
  vs cycle-4 save):**
  - Commit = native click on the marker: the Location Button's own Clicked flow
    IS the camera move (per-location Cinemachine vcam, Far/Close-aware) and opens
    the location. Cross-zone commits need no extra machinery. Browse stays
    camera-silent (owner ruling; native W/S/A/D camera keys cover watching).
  - Reachability instrument = the game's enabled-Selectable enumeration
    (activeInHierarchy + interactable), NOT a mod hierarchy walk (the walk missed
    Dock C-4; the selectable read caught it). Two node families: Locations +
    Characters, same billboard template; nodes can migrate between families as
    story moves. Location canvases have story-state VARIANTS (Canvas / Canvas 2) —
    tree dedups to one node per location, live variant wins. Latent
    active-but-noninteractable canvases exist — the interactable check filters
    them.
  - Sealed/undiscovered: progressive reveal CONFIRMED (sealed docks render at
    neither checkpoint). Tree surfaces exactly what the game renders; sealed
    label from the game's Map Key vocabulary if/when that state appears.
  - Action distinction: every location pairs by name to a "<Name> Actions" group;
    children are Action objects / Action Switches / Clock cards. Tree annotates
    coarse "actions" vs "narrative or clock only" (Dock C-4 = clock only); exact
    contents speak at open, per render-honesty (switches resolve at open).
  - New-node affordance (owner design): game's own Is New?/Set Old newness state
    marks unvisited nodes "new" in the tree (self-clearing on first commit).
    Additions/removals announced as a composed count string ("2 locations added,
    1 removed") at the FIRST full-control station moment per cycle (after any
    leading dialogue), NOT in the cycle-end vitals string. Instrument: diff of
    the enabled-Selectable node set.
  - Map-level clocks (owner ruling: map parity): billboards render dial-only
    (name text is selection-contextual; clock name renders only in the location
    card). Review string: "Dock C4, one active clock, progress x of y" + drive
    objective appended when the marker carries a drive pip (drive pips are named
    per drive on the billboard — TBD-4 resolved by observation). Delivery
    (owner-tweaked): **drive objective speaks on hover ALWAYS** — it is primary,
    not drilldown ("Dock C4. Drive objective."); clock detail stays on-demand:
    Space (existing Describe key, no conflicts) speaks it for the focused
    marker; tree gets it as a third drilldown level.
- Opens: does a mod-set far-marker selection survive UI selector's
  closest-candidate poll (fires only on nearest-candidate CHANGE — read says
  yes, unproven live).

## 4. Location action view

- Affordance: action slots rendered (location open).
- Anchor: per-slot `Focus` state, variant C; RefocusUI re-arms it (E).
- Policy: NATIVE. Arrows ride slot adjacency (brief E: Tab's old role is served
  natively). "2 of 4" position framing = W4 wording work.
- Recovery: empty-Enter → RefocusUI-consistent re-anchor (slot's own Focus);
  Backspace = Leave Button (designed).
- Opens: Action Controller's RefocusUI sender state (E open) — matters for
  post-outcome focus handoff timing.

## 5. Dice allocation (station and cloud, identical machinery)

- Affordance: dice cursors rendered + game-selected (Dice Gamepad System Active).
- Anchor: Dice Cursor 1 (UiSetSelectedGameObject on Active); cursors variant C —
  uncontested native adjacency (E).
- Policy: NATIVE. Placement rests (Slot Die has no FINISHED exit); activation is a
  separate press; retraction = universal Back (game design; input-model.md).
- Recovery: empty-Enter → re-select the game's current cursor anchor; Back path
  fires the system's own Reselector → RefocusUI teardown (E).
- Opens: die-commit real-state hook (W4: Slot Item / Select Dice 2); retraction
  announcement verbatim (W2 checklist item, still unverified).

## 6. Dialogue (subtitle + continue)

- Affordance: rendered Continue Button, game-selected on entry — **outranks every
  wrapper it appears inside** (scene, cycle transition, autoplay): incidents 5 and
  9 were both this rule missing.
- Anchor: CS Dialogue Manager Continue Button — variant C: sets itself once, never
  fights, never self-restores (E). If focus wanders, nothing native brings it back.
- Policy: NATIVE. Enter advances; R rereads; Alt+Up/Down buffer = W4.
- Recovery: **mod re-anchor to Continue Button** on empty selection or on demand —
  restoring the game's own anchor is the designed recovery (variant C won't
  contest it). This is the fix for "lost focus in dialogue, could not regain."
- Opens: none for the model; buffer scroll sync deferred (W4).

## 7. Response menu

- Affordance: rendered response buttons (menu open) — outranks wrappers, same rule
  as dialogue.
- Anchor: Pixel Crushers compiled code selects (no FSM checker exists under the
  panel — E; uncontested).
- Policy: NATIVE with the shipped vertical remap (Up/Down → horizontal graph axis)
  + number picks.
- Recovery: re-anchor to first response (watcher auto-focus shipped since S1).
- Opens: default-selection identity/stability (E open; live confirm during the
  dialogue checklist pass).

## 8. Tutorial modal

- Affordance: rendered panel text + dismiss Button (variant A watchdog — always
  reclaims).
- Anchor: Tutorial System Button.
- Policy: RERENDERED browse (TutorialReview text blocks, shipped) over the native
  single-button structure; T re-engages the dismiss button; Enter continues.
- Recovery: variant A self-recovers; mod does nothing.
- Opens: per-trigger Input Pauser firing (which of the 16 tutorials hard-lock
  input — E open); prompt-glyph transcode (W4, the blank "press ▢" gaps heard this
  session).

## 9. Character window

- Affordance: window open (FSM state via FsmSignals in the W2 revision), station
  UI deactivated wholesale.
- Anchor: Upgrade Button, set 0.3s after open (E, confirmed).
- Policy: RERENDERED (CharacterWindowReview, shipped + verified) — nav soup
  underneath (all-Automatic).
- Recovery: re-enter review top; close = designed toggle event (shipped); close
  fires UI selector Reset + RefocusUI (game hands back to station).
- Opens: skill values/breakdowns readout idiom (Lua-backed; parked design
  question).

## 10. Drive log — WALKED 2026-07-19, decisions owner-made

- Affordance: window open. Mode truth event-driven: Drive Log Button FSM `Open`
  state via FsmSignals + `QuestLogWindow.isOpen` (public, assembly-confirmed) as
  boot truth — NOT CanvasGroup alpha (incident 7).
- Structure (live + corpus + assembly, decoded this session): native Active /
  Completed tab buttons + designed Close button; per-drive template = heading
  button (name, tracked icon), Quest Description narrative TMP, entry lines incl.
  separate success/failure descriptions, native Track + Abandon buttons; per-entry
  TRACKED/UNTRACKED checker FSM compares against the tracked-quest global and
  renders the icon (render-honest tracked source). Scroll Content FSM (formerly
  the E undecoded checker): on gamepad it natively selects the first heading
  (set-once, no watchdog) — **controller users CAN track/untrack natively**
  (owner-corrected; the old "window never takes focus" observation was an
  artifact of the broken open branch). FWD/FIRE states = post-change refresh loop
  pinging entry checkers.
- Policy: RERENDERED browse over rendered entry text (native nav is Automatic
  soup — owner: "total mess"); commits are native clicks (track, untrack,
  abandon, tab, close). Respect the parked native selection; nothing fights the
  set-once selector.
- Idiom (owner-confirmed): two-level tree, same verbs as the N tree — drives →
  drive content. **Dedicated tab-swap method for this window**: clicks the
  native Active/Completed button and re-renders the list (key assignment → table
  eval; Up/Down is taken by list movement here). **Pull-out accounted**: drilling
  into a drive IS a native heading click — the game plays its own expansion,
  Track/Abandon go live, the detail walk reads what renders; collapse state is
  left to the game.
- Recovery: close = designed toggle event; Close button as native alternative;
  J also closes. Close fires UI selector Reset + RefocusUI (hand-back, same as
  character window).
- Deferred nicety: sync visual scroll to the review cursor via the Scroll Area
  FSM's own Scroll Up/Down events (designed mechanism, cheap, not required).
- Opens: none blocking the row; W2 checklist item 3 (which key closed it, live
  verbatim) still wanted on the next run.

## 11. Inventory strip — WALKED 2026-07-19, decisions owner-made

- Affordance: strip open + Item Cursor selection (the game name-compares selection
  against "Item Cursor" every frame — selection identity IS this surface's mode
  truth).
- Anchor: Item Cursor (variant C; listens to Inventory Toggle/Back directly for
  its own relinquish). **The Item Cursors are the ONLY designed focus home in the
  open strip** — the watchdog closes it if selection lands anywhere else.
  Adjacent buttons reachable by raw arrows (ITEM/DATA, Scan, Character UI) are
  Automatic-nav soup, not design.
- Mode truth goes event-driven: Inventory mode = strip FSM in an open state
  (Item 4 / Data 4 family) via FsmSignals — resolves the parked session-3
  "inventory open-signal probe". Selection identity corroborates.
- Policy: RERENDERED browse (mod cursor over slots/amounts — satisfies the
  auto-close constraint by construction) + native Item Cursor commits.
- Key idiom (owner-confirmed): I = the strip FSM's own Activate/Deactivate
  toggle (replaces ITEM/DATA button clicks, closing the input-contract flag);
  **Up/Down = designed panel Swap** (the Swapper's own vertical-axis idiom;
  announce panel by rendered label ITEM/DATA); **Left/Right = slot browse**
  (mod cursor); Enter = native-select the real Item Cursor (further semantics
  pending live check); Backspace or I = designed close.
- Cross-panel (owner-confirmed): **U and J stay IN SCOPE inside the strip** —
  the designed controller idiom is dedicated buttons (R1/L1), and the opening
  panel itself sends Inventory `Deactivate` (corpus: Character UI Button and
  Drive Log Button Open states both do). Our previous refusals were stricter
  than the game. S flagged pending a live scan-from-strip check (Scan's teardown
  list doesn't mention Inventory; S is also mid-rehome for the camera collision).
- Recovery: strip self-closes on focus drift (designed); reopen via I. Item
  Cursor close fires UI selector Reset + RefocusUI.
- Opens: Enter-on-item native semantics (data items carry Slot On/Off machinery —
  live check); slot template content sourcing (rendered names/amounts) for the
  browse cursor; scan-from-strip behavior.

## 12. Pause — WALKED 2026-07-19

- Affordance: pause canvas up — outranks everything, including dialogue.
- Anchor: RESUME (variant A watchdog). Menu: RESUME / OPTIONS / QUIT TO MENU /
  QUIT GAME + two warning menus (QUIT/CANCEL) + autosave line (shipped, both
  pre/post-tutorial branches heard live).
- Policy: NATIVE. Backspace = designed Pause Back per state (shipped); unpause
  conditionally broadcasts RefocusUI if paused from an action view (E).
- Addition (W3): warning menus speak their rendered warning body on open.
- Options menu: row 16.
- Recovery: variant A self-recovers.
- Opens: Pause CanvasGroup block mechanism (E open; low stakes).

## 16. Options menu (title + pause) — NEW ROW, WALKED 2026-07-19, owner-ruled

- Currently fully inaccessible (owner report) — the priority row of the quiet
  set.
- Structure (live-decoded): ALL discrete native Buttons — TEXT: Default/Large;
  SCROLL: Slower/Default/Faster; MUSIC: 0–5; SFX: 0–5; Back (own FSM, designed
  exit). No sliders/dropdowns.
- Idiom (owner-ruled): review cursor; Up/Down between rows (speak row name +
  current value); **Left/Right mutates directly where values are mutable**
  (volume/scroll/text: Left/Right clicks the neighboring value button natively —
  slider-like, auto-apply); **Enter engages only where it leads to
  subdecisions** (e.g. Back, or any future submenu entries).
- Current-value readout: PROBED LIVE 2026-07-19 (screenshot oracle): current
  value renders as **red/accent label text** on its value button (hover/selection
  is a distinct red OUTLINE). Row labels render fuller than object names — speak
  the rendered TMP: "SCENE TEXT SIZE", "SCROLL SPEED", "MUSIC LEVEL", "AMBIENT
  SFX LEVEL". Readout = label-color marker (literal glyph transcode, closed set,
  invariant 8); settings globals as cross-check.
- Commits: native Button clicks only; settings apply as the game applies them.

## 13. Cycle transition — WALKED 2026-07-19, decisions owner-made

- Affordance: none of its own — READOUT (Cycle Controller has zero selection
  actions; E). **Surfaces rendered inside it keep their own affordances**: the
  wake dialogue runs mid-transition and owns the keys while its Continue is up
  (incident 9). The transition owns keys only between affordances.
- Corpus trace (this session): the sequence has EXACTLY ONE exit — every route
  terminates at Idle (normal path; death branch `Debug Death Cycle → Leave 2 →
  Idle`, presumably via an Ending Controller). Narrative injection point is
  `Cycle Scene? → Scene Trigger` under the Cycle Scene Manager (five one-shot
  beats) — the only source of mid-transition dialogue. `REVEAL DICE!` sits
  immediately before Idle, so new dice are rendered by summary time.
- Policy: READOUT. Arrows/Enter stay SILENT during pure transition (owner ruling —
  refusals not wanted here). Focus flurry suppressed (shipped).
- Summary (owner design): composed string on return to Idle, ABSOLUTE TOTALS only,
  no deltas (degradation is presumed): "Cycle ended. Cycle N. Energy X. Condition
  Y, BAND. Five new dice: ...". Values from the render-paired Lua/HUD reads at
  Idle; branch clocks (Starving / Breakdown / consequence states via FsmSignals)
  may time additional rendered feedback lines, words from rendered text only.
- Diagnostic: FsmSignals logs Cycle Controller state entries for a session or two
  to observe real route variants (covers "watch for unknown exits").

## 14. Autoplay scene

- Affordance: none while Autoplay Waiting is the active signal (S4 fix); any
  rendered dialogue affordance inside the scene outranks it (same rule as 13).
- Policy: READOUT / listening — speech and queries only.
- Opens: none new.

## 15. Cloud (hacking) — CORRECTED 2026-07-19 (owner live report)

- Affordance: Hacking UI active (Scan Button transition states — E); dice
  allocation inside it behaves as surface 5.
- Anchor: no cloud-specific entry anchor located (E open); $ActiveAction is the
  designed current-node pointer (G).
- **Correction (owner): arrows DO navigate between nodes, same as station** —
  the node buttons sit in the Automatic spatial graph. Brief F's "click-only"
  applies to the ENTRY action (no bound input opens the cloud), not to movement
  inside it.
- Policy: NATIVE+INDEX, matching station: native spatial arrows preserved; the
  N-tree index still goes in for comprehension (owner). Layout comprehensibility
  is unmapped — desk approach for W4: dump the 49 nodes' positions from the
  hierarchy and derive the field's real geometry, then design the index grouping
  from it (the station region-grouping method).
- Recovery: S = designed exit (shipped); Backspace deliberately refuses until node
  CloseAction wiring is verified (W4).
- Opens (W4 gate): scan-mode nav check, initial anchor on entry, node list
  source ($ActiveAction + field geometry), die-match transcode, Backspace
  CloseAction wiring.

---

## Cross-surface commitments (from W3, restated for the walk)

- **Focus fence (owner-approved, built 2026-07-19):** the game has no nav
  boundaries — Automatic adjacency spans every active selectable scene-wide, and
  lighter overlays (drive log: deactivates only Inventory on open) leave the
  world selectable underneath. The game never walks this graph on controller
  (stick-scroll + parked selection); mod arrows are the only traverser. Bounded
  surfaces therefore compute the native destination BEFORE dispatch and suppress
  moves that exit their container (drive log / character window / pause = rooted
  fences; inventory = the stricter Item Cursor rule). Dead-end = bare repeat.
  Unbounded surfaces (station, action view, cloud) keep free native movement.

- Universal re-anchor key behavior: empty-Enter (or explicit re-anchor) targets
  the current surface's anchor row above — one idiom everywhere.
- Mode changes ride FsmSignals/DS events; polls become boot init + divergence
  logs.
- RefocusUI and UI selector Reset are focus-settling cues: the mod acts next
  frame, never the same frame.
- Refusals: audible-refusal ruling still open; H incident 10 documents that
  silence hid three precedence bugs.

Suggested walk order (owner picks): 13 (cycle transition — precedence rule
stress-test), 3 (station tree commit + zone crossing), 11 (inventory idiom
replacement), 10 (drive log signal), then the quiet rows.
