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
- **B. Modality layer.** Formalize the mode model over the verified anchors; migrate
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

## 5. Practices that keep it durable

- Verification docs are ground truth; when live evidence contradicts them, the
  correction is written back the same session (see triage queue live-corrections).
- The triage queue is per-session working memory; this plan only changes when the
  architecture picture changes.
- Nothing is "done" until live-validated with the owner on NVDA; wording ships as
  provisional until the owner calibrates it.
- Owner sets ordering and idiom; design commitments get surfaced before code.

## 6. Parked design questions

- Cycle number: rendered on save labels, so speakable — live source not yet found
  (Dialogue System Lua variable is the lead candidate).
- Inventory open/close signal (probe next session).
- Clock-cycling key idiom (report 14): key choice, position memory.
- Modifier-row emphasis layer (calibration log will settle; report 12).
- Skill values/breakdowns readout idiom in the character window.
- Controller users + mod coexistence; multi-save/save-slot surfaces.
