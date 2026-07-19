# CSAccess build plan

Durable, whole-mod plan, grounded in the game's verified construction (see
game-architecture-survey.md and docs/verification/). Session work items live in the
triage queue; this document is the architecture they must fit into. Update it when
the architecture understanding changes, not per work item.

## 1. The game we are building against (ground truth)

- The game is PlayMaker data, not code: ~4,900 FSMs stamped from a small set of
  templates (action controller, clock group, selection checker, tutorial panel);
  custom C# is three enums and a dozen helpers. There is no mechanics class layer to
  hook. FSM engagement is the substrate, not a workaround.
- The UI is controller-only by design: Rewired input, uGUI selection, per-widget
  "Checker" watchdog FSMs (~600) that continuously re-assert selection. Mod-held
  selection always loses.
- Narrative is Pixel Crushers Dialogue System + Ink (compiled stories in
  sharedassets0). Drives are Dialogue System quests (Lua database, QuestLog API).
- The game publishes its own pointer registry: GameObject-typed FSM globals
  (ActiveAction, Dialogue Panel, Response Menu, Tutorial System, UI Selector, Leave
  Button, ...). This is the sanctioned way to find "what's current".
- Everything player-facing is rendered TMP text or transcodable graphics (pips,
  meters, +/- effect glyphs, marker sprites — loose sprites, no atlas). Live stat
  stores are the HUD widget FSMs, not the Cycle Controller locals (transient).

## 2. Binding invariants (violations are bugs, whatever the feature)

1. Render-honesty: speak only what the game renders. FSM signals are CLOCKS, never
   CONTENT — they time announcements; the words come from rendered text. No feedback
   rendered = nothing spoken.
2. Single-dispatch activation: exactly one native activation per user intent
   (submit, else full pointer sequence), through the game's own handlers.
3. Never hold EventSystem selection against the game. Where native navigation works,
   ride it (moveHandler on current selection). Where it doesn't (Automatic-nav soup),
   use a mod-owned review cursor and activate targets natively (CharacterSelect,
   TutorialReview, CharacterWindowReview pattern).
4. Depth-zero reads: read dials (named FSM states/variables), anchors (the registry),
   and rendered text. Never trace event chains across FSMs to derive a fact — if a
   fact matters to the player, the game renders it somewhere.
5. Template engagement with graceful silence: features key on template conventions
   (names, child markers like Gamepad Dice Slot). A non-conforming instance gets
   silence plus a log line, never a crash or a guess.
6. Unverified signals don't ship: a hook goes in only after its signal is probed
   live (bridge) or proven in serialized data. (Counterexamples cost us: Cycle
   Controller stat locals, the inventory open signal.)
7. Observation-only tooling: the bridge reads; it never drives game state.
8. Game vocabulary over invented vocabulary ("spent", "FADING", tier names); glyph
   conventions transcoded literally, counts not interpreted.
9. Speech discipline: game-driven events queue, user-initiated speech may interrupt;
   flushes are explicit; dedup/cooldown for reselection ping-pong.

## 3. Target architecture (layers)

- **Substrate adapters** (read-only): Dials (Cycle Controller, Dice Gamepad System,
  mode flags, HUD stat FSMs), Anchors (registry lookups replacing hardcoded Find
  paths), Render (Describe: text + effective-alpha visibility + glyph transcode),
  Dialogue (conversation state, QuestLog).
- **Modality layer** (the spine, mostly still to build): one model that always knows
  the current mode and owns three decisions per mode: what to announce on entry/exit,
  which keys are in scope (and what they do), and the focus policy (native graph /
  review cursor / readout-only). Modes, from verified anchors: title, station
  free-roam, action view, dice allocation, dialogue, response menu, tutorial modal,
  cloud (Hacking?), cycle transition (Cycle Controller non-Idle), autoplay scene,
  pause, and each window (character, drive log, inventory).
- **Features** ride the layers: queries (C/D/K, clock cycling), jump keys, outcome
  pipeline, per-window review cursors, drive log enrichment, prompt-glyph
  transcoding, odds readout.
- **Polish/config**: wording calibration (owner-driven), key rebinding/config
  surface, controller coexistence, packaging.

Existing ad-hoc pieces (flurry gate, window watchers, response-menu remap, review
cursors, input-pause handling) are earmarks of the modality layer built early — they
migrate into it rather than accumulating as special cases.

## 4. Phases

- **A. Evidence and first consumers — done 2026-07-18.** Source sweep (survey +
  briefs A-D), mechanical fixes, outcome pipeline v1, flurry gate + cycle summary,
  character window v1 (announcements, review cursor), commit signal. All
  live-validated except the items staged for next launch.
- **B. Modality layer.** First item: the FSM corpus dump (section 5) — one
  observation-only launch produces the full FSM structure corpus that grounds
  everything after. Then: formalize the mode model over the verified anchors; migrate
  existing gates/watchers/cursors into it; per-mode key scoping (kills the
  Tab-answers-through-windows class of bugs); mode entry/exit announcements; leave
  paths per mode (Backspace = S-exit in cloud, etc.).
- **C. Coverage, mode by mode.** Each follows the same recipe — probe live, verify
  signal, build on anchors, validate live with the owner: inventory (open signal
  probe first), cloud (scan scoping, die-match target, node conveyance), location
  focus model (UI Selector anchor + spatial-nav reality), drive log enrichment
  (tracked state, tabs, confinement), tutorial prompt-glyph transcoding (11 panels,
  closed set), remaining windows as story unlocks them.
- **D. Completeness and hardening.** Odds bands readout; marker glyph transcode
  (sprite names); end-game surfaces (two ending controllers exist, unmapped);
  save/load edge behavior; performance pass (FsmList scans per tick); failure
  telemetry review (the graceful-silence log lines).
- **E. Release readiness** (if/when the owner wants a public mod): config surface,
  key rebinding, other Tolk screen readers, packaging and docs.

Phases B and C interleave in practice — each C item lands as a modality-layer
client, which keeps B honest. D and E stay strictly later.

## 4b. Concrete work plan (corpus synthesis, 2026-07-18 — briefs E/F/G)

Derived from the full FSM corpus weighed against the mod source. Supersedes guesswork
ordering; each item cites its brief. Workstreams in dependency order:

**W1 — Substrate rebuild (first; everything else rides on it):**
- Lua adapter: read Dialogue System Lua directly (DialogueLua) for Player_* stats,
  skill values (the rendered modifier bucket -1/0/+1/+2), Cycle, IntroComplete,
  breakdowns. Corrects a two-doc error: these were never PlayMaker globals (F).
  Kills the modifier color heuristic (G#2) and gives C-query O(1) truth incl. the
  cycle number (G#3, F).
  RENDER-PAIRING ALLOWLIST (binding): the adapter exposes only variables with a
  documented render pairing — where the game shows the value to the player
  (rendered = player-reachable in-game, per owner ruling; not necessarily
  on-screen this instant), verified by one of: (a) corpus render-route trace
  (GetVariable → SetProperty/animator/localized-text onto a UI object), (b) live
  bridge check (/texts + effective alpha), (c) screenshot oracle. Feature code
  cannot read off-list variables. The other ~390 Lua variables are hidden story
  state and are never spoken.
- FSM state-entry signal: one Harmony hook on PlayMaker state entry feeding a
  (template, state) event bus. Replaces most 0.4s polling (G#4): outcomes
  (*_Outcome entry), dice commit (real states: `Slot Item` / `Select Dice 2` — the
  "Slotted" state never existed, G#1), window lifecycles (the shared button-FSM
  template behind Character UI and Drive Log buttons, F), cycle transition states.
- Anchor adapter: GameObject-typed globals incl. `$ActiveAction` (written by cloud
  location buttons — the cloud's designed anchor, G#5); retire hardcoded Find paths
  where an anchor exists.
- The input contract: the game listens for exactly 22 Rewired actions (F) — the
  complete sanctioned input surface. Document keyboard→action mapping; every mod
  key must correspond to one action's designed effect (or a pure-read query).
  Note: cloud view has NO bound input action — it is uGUI-click-only (F).

**W1→W2 checkpoint — keymap design session (owner-present, blocking):** with the
input contract and the verified mode list in hand, owner and mod design the keymap
together before any W2 scoping code: global keys, per-mode keys, leave idiom per
mode, and which current binds survive, move, or die. Every key maps to one
action's designed effect or a pure-read query — no invented input schema. All
currently shipped binds are provisional input to this session, not constraints on
it.

**W2 — Modality layer (on W1 signals):**
- Mode model over the traced flag writers (F) + state-entry signals; entry/exit
  announcements; per-mode key scoping using the modal-enforcement map (E) — e.g.
  the character window deactivates the station UI, so Tab answers mode-aware
  ("Character window open") instead of "No actions here".
- Leave paths per mode via the shared window template's close event; cloud exit
  scoped to the S toggle.

**W3 — Focus models per the E verdicts (grounding docs: E + H focus assessment;
amended 2026-07-19 after the session-5 incident cascade):**
- Governing principle (H): the game runs one coherent focus protocol — claiming
  (Checker variants), anchoring (designed per-context anchors), hand-off (RefocusUI /
  UI selector Reset / Confirm backstop, always event-marked). The mod JOINS this
  protocol; it does not shadow it with polled reconstructions.
- **FocusModel table** (data, not prose — same discipline as KeyScope): one row per
  surface with designed anchor, Checker variant (fights / relinquishes / passive),
  channel policy (NATIVE / NATIVE+INDEX / RERENDERED / READOUT), recovery idiom, and
  the affordance signal that gives the surface key-precedence. Rows cite E/corpus or
  are flagged for live confirmation. docs/focus-model.md is the design artifact
  (owner-reviewed per surface before code).
- **Channel law** (binding): a mod-constructed index or review view (N tree, K index,
  skill/drive tables) never holds or moves EventSystem selection while browsing —
  browsing is mod-owned cursor state over rendered/data truth; commit = exactly one
  designed native activation, then authority returns to the native channel. Native
  navigation (incl. station Automatic-spatial movement) is preserved as-is alongside
  every index.
- **Universal mode-aware re-anchor**: the empty-Enter Confirm-backstop mirror extends
  to every mode using that mode's designed recovery from its FocusModel row
  (dialogue → reselect Continue Button; picker → its own anchor machinery; windows →
  their delayed anchors; station → UI selector Reset, shipped).
- **Resync discipline**: subscribe to RefocusUI + UI selector Reset via FsmSignals as
  focus-settling cues; never set selection in the same frame (E practical rule,
  enforced in code).
- **W2 revision folded in** (corrections to shipped internals; KeyScope interface
  stands): mode derived from the game's own events first (window lifecycles via
  FsmSignals on the button FSMs' states; conversation/cycle/tutorial already
  event-driven), flag/alpha polls demoted to boot initialization + divergence
  diagnostics; precedence derived from rendered/held input affordance (selection
  identity is a sanctioned signal — the game's own idiom) instead of a hand-ordered
  list. The three session-5 fixes (drive-log open signal, inventory designed
  toggle + Up/Down = designed Swap, S-key native camera-scroll collision) land as
  instances of this model, not spot patches.
- Sequencing: the W2 live-verification remainder re-runs AFTER this lands (its
  blockers were mode-authority bugs — H incident ledger 7–9).
- The Checker-variant table (watchdog / one-shot / state-entry-only) defines what
  the mod may select freely vs must never fight — consult before any focus feature.
- Logical-cursor rebuilds only where nav is Automatic-spatial soup: character
  window (shipped) and any future context that fails the same test.
- Inventory constraint (E): focus leaving `Item Cursor`-named objects auto-closes
  the strip — any inventory review must stay inside them (a RERENDERED browse that
  never moves real selection satisfies this by construction).
- Correction: retract D's Drive Log Button attribution (string-extraction artifact).
- Correction (H, from level0 Rewired dump): the game's shipped keyboard map is five
  binds — W/S camera scroll, A/D rotate, Tab DEBUG MENU. Our S key collides with a
  native camera bind; Tab's kill is vindicated. input-contract.md updated in W3.

**W4 — Features re-derived on the new substrate:**
- C query → Lua (cycle/energy/condition; condition band word from HUD or bucket).
- Dice commit + picker close wording → real state hooks (drop heuristic window).
- Modifier readout → Lua skill value.
- Cloud coverage: Tab/K/L scoped by `Hacking?` over `2_Hacking Action Groups` +
  `$ActiveAction`; die-match target transcode next.
- Drive log: QuestLog API for entries/tracked state; template lifecycle for tabs.
- Tutorial prompt transcode (#1) with localization-table key resolution (F).
- Later: odds (#11), the two unmapped Ending Controllers (G), endgame surfaces.

**W5 — Validation model change (the point of all this):** features ship with
wiring-predicted acceptance checks written BEFORE the live session; ride-alongs
confirm predictions instead of discovering failures. Live-only items list (E/F):
drive-log open-branch sequencing, response-menu default selection, pause
CanvasGroup behavior, per-tutorial Input Pauser timing, cloud initial anchor,
Action Controller's RefocusUI purpose.

## 5. Method hierarchy: static first, corpus second, probe only true dynamics

Architecture questions are answered in this order:

1. **Serialized statics** (UnityPy + typetrees): hierarchy, uGUI wiring (onClick,
   navigation), defaults, sprites. Fully working; always first.
2. **FSM corpus** (to build, first Phase B item): PlayMakerFSM blobs are
   structurally unparseable by our offline tools (universal typetree failure —
   Brief A), and the FSMs are the game's entire logic. Fix: use the game as its own
   parser — a one-time, observation-only census dump from the running game (station
   idle) writing every FSM's true structure (states, actions, parameters, event
   targets, variables) to files. Offline-greppable corpus thereafter; regenerate per
   game patch. This moves most "live probing" to desk analysis.
3. **Live probing** — reserved for what is dynamic on principle, not by tooling gap:
   runtime-computed spatial navigation (no graph exists to read), live variable
   values (statics carry schema, not state — the Cycle Controller trap), animator/
   CanvasGroup visibility dynamics, runtime-spawned content (drive entries; location
   labels are placeholders in static data), and timing. Plus final validation with
   the owner, which nothing replaces.

## 6. Practices that keep it durable

- Verification docs are ground truth; when live evidence contradicts them, the
  correction is written back the same session (see triage queue live-corrections).
- The triage queue is per-session working memory; this plan only changes when the
  architecture picture changes.
- Nothing is "done" until live-validated with the owner on NVDA; wording ships as
  provisional until the owner calibrates it.
- Owner sets ordering and idiom; design commitments get surfaced before code.

## 7. Parked design questions

- ~~Cycle number source~~ RESOLVED (F): Lua variable `Cycle`, writer Cycle
  Controller. (Save-slot labels use a separate save-file mechanism — don't conflate.)
- ~~Modifier-row emphasis~~ RESOLVED (G): read the Lua skill value; bucket = the
  rendered row. Color heuristic retired.
- Inventory open/close signal: the semi-modal Item Cursor name-watch (E) is the
  lead — verify live.
- Clock-cycling key idiom (report 14): key choice, position memory.
- Skill values/breakdowns readout idiom in the character window (data now: Lua).
- Controller users + mod coexistence; multi-save/save-slot surfaces.
