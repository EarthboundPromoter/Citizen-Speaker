# Interaction surface and queriable states

Working document for the W1→W2 keymap design session. Part 1 enumerates every
mode and what the player can DO there (the interaction surface). Part 2
enumerates everything the game shows that a player might ask to hear on demand
(the queriable states). The input model gets designed on top of these; nothing
here is a key assignment.

Sources: verification briefs E (focus models, modal enforcement) and F (input
surface, lifecycles, Lua store), input-contract.md, ui-state-map.md, shipped
live-validated features. Status marks: VERIFIED (corpus or live), LIVE-OPEN
(mechanism known, needs a live confirm), UNBUILT (surface exists, mod support
doesn't).

## Part 1 — Interaction surface, mode by mode

### 1. Title / main menu

- Navigate menu items; pick a save slot (slots render class-and-cycle labels —
  the mod speaks them); settings. VERIFIED (shipped).
- Character select (new game): review class info, change class via carousel
  buttons (no keyboard path exists — mod clicks LEFT/RIGHT), confirm start.
  VERIFIED (shipped, mod review cursor).

### 2. Station free-roam

- Move focus among location/character markers: the game's UI selector picks the
  nearest marker (anchor is native); movement between markers is runtime
  spatial navigation — no authored graph exists. VERIFIED (E).
- Zone crossings (Rim / Greenway / Hub): camera-zone system, not a window; the
  UI selector re-anchors on zone entry. Camera pan/zoom and rim jump-up/down
  exist on Scroll Axis. VERIFIED (E, F). Mod treatment of pan/zoom: none
  (cosmetic) — confirm at design time.
- Open a location's action view: activate the marker (native click/submit).
  VERIFIED.
- Open windows: character (toggle action), drive log (toggle action),
  inventory (toggle action), scan/cloud (toggle action), pause. VERIFIED (F).
- End cycle: a home location's End Cycle action, gated by CycleCanEnd.
  VERIFIED (corpus; live interactability check still open).
- Dead-selection recovery: the game's own Confirm-with-nothing-selected
  backstop re-anchors to the nearest marker. VERIFIED (E).

### 3. Location action view

- Cycle among the location's action slots (variant-C checkers: slots reclaim
  focus only on RefocusUI, arrows ride native adjacency). VERIFIED (E).
- Read an action's card (name, affordance, status, cost, skill). VERIFIED
  (shipped).
- Engage an action → dice allocation. VERIFIED.
- Leave: Leave Button / Back. VERIFIED (F).

### 4. Dice allocation (station and cloud — identical machinery)

- Arrows among the five dice cursors (native uGUI adjacency, no fighting).
  VERIFIED (E).
- Slot the chosen die (submit). VERIFIED (shipped: "Die slotted.").
- Cancel out (Back → picker's own teardown + RefocusUI). VERIFIED.
- Reroll (REROLL action, when available). VERIFIED (shipped).

### 5. Dialogue (subtitle / continue)

- Advance (continue button, variant C — safe to ride). VERIFIED (E).
- Long-text scroll exists on Scroll Window (dialogue scrollbar). LIVE-OPEN:
  when it matters and how the mod should surface it.
- Auto-read of lines; repeat. VERIFIED (shipped).

### 6. Response menu

- Choose among responses: selection is Pixel Crushers compiled code, no FSM
  checker — uncontested, mod arrows ride it (graph is horizontal, rendered
  vertical; mod remaps). VERIFIED (E + shipped).
- Pick by number. VERIFIED (shipped).
- Default initial selection: LIVE-OPEN (E open question).

### 7. Tutorial modal

- Read the panel (no native focus structure — mod review cursor steps text
  blocks). VERIFIED (shipped).
- Continue/dismiss (the panel's single button, variant-A watchdog — the game
  holds selection on it). VERIFIED (E).
- Input lockout varies per tutorial trigger (16 triggers; which ones hard-pause
  input is unmapped). LIVE-OPEN — affects what keys work mid-tutorial.
- Prompt glyphs in panel text: UNBUILT (transcode via localization table, W4).

### 8. Cloud / scan mode

- Enter/exit scan mode: Scan Button toggle — the modal boundary (deactivates
  station UI wholesale, activates Hacking UI). VERIFIED (E).
- Navigate cloud nodes; enter a node: per-node click, no bound action.
  VERIFIED (F). Initial selection anchor on entry: LIVE-OPEN.
- Node actions: dice allocation identical to station; Sequence Complete button
  is a watchdog. VERIFIED (E).
- Die-match target readout: UNBUILT (W4).

### 9. Cycle transition

- No interaction — pure readout stream (zero selection actions in the whole
  Cycle Controller). Mod gates announcements and summarizes. VERIFIED (E +
  shipped flurry gate / "Cycle complete" summary).

### 10. Autoplay scenes / one-shot story beats

- Mostly wait states (Scenes Active? / Autoplay Waiting flags); interaction
  limited to what the scene itself surfaces. VERIFIED as flags (F);
  per-scene interaction reality LIVE-OPEN.

### 11. Pause

- Menu buttons (RESUME is a permanent watchdog — the game owns selection).
  VERIFIED (E).
- Confirm flows (Sure?), options submenu. VERIFIED (F state graph).
- Dedicated pause-scoped cancel exists (Pause Back action) — distinct from
  gameplay Back. VERIFIED (F). Mod currently doesn't map it (contract audit).
- Autosave-timer readout on open. VERIFIED (shipped).

### 12. Character window

- Open/close toggle (dedicated action; Back also closes; close = save).
  VERIFIED (F).
- Default anchor: Upgrade Button, set 0.3s after open. VERIFIED (E).
- Review skills/points/class/portrait content (window is Automatic-nav soup —
  mod review cursor, shipped). VERIFIED.
- Upgrade flow: skill row → confirm sub-flow (rows reclaim the Upgrade Button
  via Reset when done). VERIFIED (E).
- Skill detail dismiss (Back). VERIFIED (F).

### 13. Drive log

- Open/close toggle (dedicated action). VERIFIED (F).
- Open-branch in gamepad mode is ambiguous in the static graph — LIVE-OPEN
  (E open question; blocks drive-log enrichment).
- Entry list has its own selection machinery (Checker-templated Scroll
  Content, undecoded); scroll on Selection Axis Vertical. LIVE-OPEN.
- Tabs, tracked-drive state: QuestLog API reachable. UNBUILT (W4 enrichment).

### 14. Inventory strip (semi-modal)

- Toggle open/closed; ITEM/DATA sub-tab swap. VERIFIED (F).
- Move among item cursors (variant C, native). VERIFIED (E).
- HARD CONSTRAINT: the parent FSM watches selection every frame and closes the
  strip if selection leaves an object literally named "Item Cursor" — any
  review feature must stay inside the cursors. VERIFIED (E).
- Item swapper on Selection Axis Vertical. VERIFIED (F). Mod support UNBUILT.

### 15. Endgame surfaces

- Two Ending Controllers exist, unmapped; credits skip on any button.
  UNBUILT / out of scope until story reaches them.

## Part 2 — Queriable states

What the game renders (or keeps in an allowlisted store) that a player might
ask to hear on demand. Grouped by domain. Source key: LUA = render-paired Lua
adapter value (W1, live-verified); TEXT = rendered text readable in place;
DIAL = named FSM state/variable (verified); UNBUILT = no mod support yet.

### A. Player vitals

- Energy (LUA), condition value + band word (LUA + TEXT band). VERIFIED.
- Cycle number (LUA). VERIFIED this session.
- Class (LUA/TEXT), cryo/bits (LUA/TEXT). VERIFIED.
- Upgrade points (LUA/TEXT), drive points (LUA; unset until first earned).
- Skills: five values/buckets (LUA). VERIFIED. Broken-skill state: rendered
  labels exist ("Skill Broken"); Lua *_BROKEN variables exist but have no
  documented render pairing yet — UNBUILT.

### B. Dice

- Per-die value and state (value / slotted / spent) (DIAL, live-verified).
- Reroll availability (DIAL: REROLL DICE On-states). UNBUILT as a query.

### C. Current location and actions

- Location name/description (TEXT billboard). VERIFIED (focus announcements).
- Action list; per action: name, affordance, status label (the WORKING /
  COMPLETE / UNAVAILABLE / dice-input quartet — TEXT via localization keys),
  cryo cost (TEXT), required skill + modifier (TEXT/LUA). VERIFIED (shipped).
- Odds/risk readout (#11): rendered odds bands. UNBUILT (W4).

### D. Clocks

- Per clock: name, filled/total segments, positive/negative, cycle-clock
  banner, description (DIAL + TEXT, live-verified). VERIFIED (K query).

### E. Drives

- Tracked drive name + description (TEXT, drive tracker HUD). VERIFIED
  (C query tail).
- Drive log entries, tabs, tracked state (QuestLog API). UNBUILT (W4).

### F. World / map

- Cycle list of locations and characters (TEXT names). VERIFIED (L key).
- Marker glyph semantics (sealed / type / clock-warning per the rendered Map
  Key legend vocabulary). UNBUILT (W4 sprite-name transcode).

### G. Inventory

- Item/data lists with amounts; selected item name + description (TEXT).
  Partially shipped (cryo in C query); full review UNBUILT pending the
  Item Cursor constraint design.

### H. Dialogue

- Last line + speaker; speech history; current responses list. VERIFIED
  (shipped).

### I. Mode and meta

- "Where am I": current mode/window — becomes reliable with the W2 mode model.
  UNBUILT (currently implicit).
- Focused-element detail (Space). VERIFIED (shipped).
- Input mode (gamepad/mouse), autosave timer (pause TEXT). VERIFIED.

### J. Cloud

- Scan state (DIAL: ScanActive). VERIFIED as signal.
- Node list/status (TEXT via hacking label quintet), die-match target,
  active node (ActiveAction anchor). UNBUILT (W4).

### K. Outcomes and notifications

- Last action outcome: effect lines + completion narrative (TEXT).
  VERIFIED (shipped auto-read).
- Cycle summary (dice + meters). VERIFIED (shipped).
- Notification history beyond speech history. UNBUILT.

### L. Tutorial

- Panel text blocks (TEXT, review cursor). VERIFIED. Prompt glyphs UNBUILT.

## Part 3 — Channel classification (owner frame, 2026-07-18 session 4)

The input model hangs off channels, not domains. Channels:

- **FOCUS** — spoken free on highlight/selection; needs no key.
- **AMBIENT** — announced when the game renders it (events, notifications).
- **QUERY** — stable rendered backing; answerable on demand at any moment.
- **CONTEXT** — meaningful only inside its mode; any key for it lives there.
- **TBD** — render source or timing unknown; evidence item before channeling.

### Classification

- Energy, condition + band: QUERY (HUD always rendered) + AMBIENT via outcome
  effect lines (shipped).
- Cycle number: QUERY (Lua, verified).
- Class, cryo: QUERY.
- Upgrade points: AMBIENT at earn ("+ 1 UPGRADE POINT" notification template
  exists) + CONTEXT in character window. TBD-1: the Character UI Button keeps
  an UpgradeAvailable flag it clears on open — does an upgrade-available badge
  render on the button, and when? Determines whether points-available is also
  QUERY outside the window.
- Skill values: CONTEXT (character window rows; Lua backing exists). TBD-2:
  all moments skill values/changes render outside the window (level-up
  notifications confirmed as templates; anything else?).
- Dice pool: QUERY during a cycle; per-die detail FOCUS in the picker
  (shipped).
- Reroll availability: CONTEXT (dice UI). TBD-3: its rendered signal
  (REROLL DICE On-states are the dial; what does the player see?).
- Location name/description: FOCUS (shipped).
- Action card (name, affordance, status, cost, skill/modifier): FOCUS
  (shipped) + CONTEXT re-read.
- Odds bands: FOCUS enrich once built (W4).
- Clocks at location: QUERY scoped to location (shipped K) + AMBIENT via
  outcome lines when they tick.
- Tracked drive: QUERY (drive tracker HUD always rendered). Drive
  events (new/complete/fail): AMBIENT (notification templates confirmed).
- Drive log detail: CONTEXT (drive log window).
- TBD-4 (owner-named): does the game render which locations/nodes serve a
  drive's next step (marker highlight, label, anything)? If rendered,
  it transcodes into FOCUS + master-list annotation; if not rendered,
  it does not exist for the mod (render-honesty).
- World master list: nav surface, not a query; marker glyph semantics enrich
  its items and FOCUS (W4).
- Inventory contents: FOCUS inside the strip + CONTEXT detail.
- Dialogue last line / speaker: QUERY (shipped repeat). History: QUERY
  (shipped).
- Where-am-I (mode): QUERY once the W2 mode model exists.
- Focused-element detail: QUERY (shipped Space).
- Input mode switch: AMBIENT (shipped).
- Autosave timer: CONTEXT (pause) (shipped).
- Cloud node status / die-match: FOCUS + CONTEXT once built (W4).
- Scan-mode toggle: AMBIENT (shipped announcement path).
- Outcome effects + completion narrative: AMBIENT (shipped); history via
  speech history QUERY.
- Tutorial blocks: CONTEXT (review cursor, shipped).

### Evidence items opened by this pass

TBD-1 upgrade-badge render/timing; TBD-2 skill render moments outside window;
TBD-3 reroll's rendered signal; TBD-4 drive→location/node linkage rendering.
All four are desk-first (corpus render-route trace), live-confirm second.
