# Station map table — design artifact (2026-07-20)

Supersedes the input-model.md "N — navigate tree" entry (two-level tree). Owner
design session 2026-07-20; rulings recorded per decision. Style per
focus-model.md: every claim cites evidence or is flagged for verification.
Wording throughout is provisional until owner calibration.

## Evidence base

- **Canvas-FSM availability dial** (corpus decode 2026-07-20): every location
  canvas (one FSM per story variant, ALL always active scene-wide) runs
  `Off → [trigger Lua variables met] → Variables Met ⇄ Off Camera`, with
  `Selected` while entered and `Off 2` on criteria-loss. `Text Setup` (boot)
  resolves localized `Location Name` / `Location Description` into FSM string
  variables. Camera gating is `IsVisibleInCameraFrustrum` — frustum, not zone
  radius. THE STATE NAME IS THE CAMERA-INDEPENDENT AVAILABILITY DIAL:
  - `Off` / `Off 2` / `Text Setup` — story-locked or pre-classified: NOT listed.
  - `Variables Met` / `Off Camera` / `Selected` / `Cycle Check` /
    `Clock Flasher` / `Flashed Already?` — available: listed.
- **Drive pips** (corpus decode 2026-07-20): per-location pip objects named
  "<DRIVE NAME> pip <entry#>" run `IsQuestTrackingEnabled($Quest Name)` on a 1s
  loop — PIP on ONLY while the player tracks that drive; completed entries
  deactivate permanently. Pip variables carry quest name + entry number.
- **BL-16**: the enabled-Selectable set is frustum-local — it can never back a
  full-station instrument. This table replaces that instrument class.
- Marker `Is New?` state (Location Button FSM) = the "new" flag source.
- Button `IsInteractable()` on a rendered marker = the zoom-down affordance
  (the existing ", disabled" suffix check; heard live on Dock B-2 / Kompressor
  Club, s7 run).

## Rulings (owner, 2026-07-20)

1. **Zones first** (Call 1 = B): the table is per-zone, zone names from the
   Location Controller's own states (Rim / Greenway / Hub). Caveat recorded:
   camera behavior as zones open up is unobserved — zone list degrades
   gracefully (see spoiler guard).
2. **Table, not tree.** Rows = rendered locations; columns = facets; slash
   (the defined tabbing idiom) swaps zone tabs. Rationale: facet order stops
   mattering when the player can jump cell-by-cell; spreadsheet navigation is
   native screen-reader idiom.
3. **Stable geometry beats compression.** Empty cells are never skipped and
   never collapse the grid; they speak terse facet-specific emptiness
   ("No clock."). The ROW REPORT carries the compression: row moves speak
   name + flags + populated facets only, so empty cells are rarely visited.
4. **Inclusion = render.** Canvas non-Off → row exists; canvas Off → no row,
   ever ("if the game doesn't show it the player wasn't meant to know it").
   Rendered-but-non-interactable markers ARE listed, flagged — that render is
   the game telling the player "something here eventually."
   "Disabled" defined: an A press / click would not zoom down.
   Content-sparse-but-enterable (Dock C-4 pattern) is NOT disabled — it's a
   normal row whose Clock / Actions cells tell the story.
5. **Drives follow render** (pips are tracking-gated by the game): drive facts
   appear only for drives the player tracks — no forcing, no mod-side opt-out.
   Naming the drive is sanctioned (drive names are player-reachable rendered
   content; pip data carries quest name + entry). A **Tracked Drives tab**
   joins the zone tabs: the same pip data inverted (drive → objective →
   pip-on locations, zone-tagged).
6. **Columns**: Name (new/disabled flags ride it), Clock, Drives, Actions
   count, Description. Icon/type column later, when the W4 glyph transcode
   lands. Characters: TBD (interleave vs own tab — undecided, owner).

## Announce grammar (draft — owner calibration pass expected)

- Row move (Up/Down): "<Name>[, new][, disabled]. [Clock <n> of <m>
  <positive/negative>.] [Drive: <name>[, <name>].]" — populated facets only.
- Row move while parked in a non-Name column: "<Name>. <current cell>."
  (scan-one-facet-down-the-station idiom).
- Column move (Left/Right): "<Header>: <cell>." Empty: "<Header>: none." /
  facet-specific ("No clock.").
- Tab swap (slash): "<Zone>." then first row report. Empty zone tab: see
  spoiler guard.

## Channel + selection (supersedes one line of the old N-tree spec)

- Browsing NEVER moves EventSystem selection (channel law, W3) — and cannot:
  off-camera rows have no active Button. Consequence, surfaced for explicit
  owner sign-off: **the browse cursor renders no on-screen highlight**; the
  game shows nothing until commit. (The old input-model N-tree line "each
  cursor move natively selects the marker" is retired by this.)
- Commit (OPEN — Call 3, owner has not ruled): on-camera rows = one native
  click (camera flight for free, single-dispatch). Off-camera rows, options
  on the table: (a) guidance-only v1 — announce direction, player travels by
  WASD; (b) camera-ride commit — synthesize native camera input until the
  frustum enables the marker, then one click; (c) zone-jump decode — find who
  sends the Location Controller's `Transition to <zone>` events and whether a
  designed event exists. Claude's recommendation: (a) for v1, (b) as v2.
- Companion feature (endorsed, separate build): **scroll ticker** — FsmSignals
  on canvas FSMs entering/leaving `Variables Met` announces markers as they
  come over the horizon during native WASD browse. Pure announcement layer,
  no interception. (Full WASD interception/snap-scroll: rejected for now —
  fights the native channel.)

## Spoiler guard

A zone tab exists only when the zone contains ≥1 non-Off location. UNVERIFIED:
whether the game shows all three zone names to a fresh player from boot — if it
does, the guard can relax to match render. Check on the fresh save.

## Architecture: build this flexibly (owner requirement)

Current string composition, honestly stated: ad-hoc — Describe / GameQueries /
CycleGate each build strings inline (StringBuilder + concatenation at call
sites); wording lives where it's used; there is no central wording registry.
Fine for one-shot describes; wrong for a table. The table build introduces:

- **Column registry**: an ordered list of Column records
  `{ id, header, CellProvider }` where CellProvider is
  `location row → string or null` (null = empty cell). Adding, removing, or
  reordering a column is a one-line registry edit; the announce grammar reads
  the registry, so nothing else changes.
- **Row model = data, not strings**: rows are built on open/refresh from the
  dials (canvas FSM state + Location Name variable; Is New?; interactable;
  clock dial variables; pip quest/entry + PIP state; action count;
  Description variable). Strings are composed only at announce time.
- **Wording block**: all table phrases (headers, empty forms, flag words,
  report joiners) gathered in ONE place for owner calibration — no scattered
  literals.
- **Refresh discipline**: rebuild on table open and on tab swap; no cross-open
  caching (anchors populate lazily; variants swap — s6/s7 lessons).
- **Extension hooks**: characters ride the same registry when ruled; the cloud
  field is a future second instance of the same table model (scoped columns);
  the census redesign (BL-16) reads the same canvas-state instrument — one
  source of truth, three consumers.

## Build prerequisites (invariant 6 — no unverified signals ship)

1. Live one-shot: bridge read of ALL canvas FSM ActiveStateNames + Location
   Name variables with camera parked — proves the dial incl. off-camera rows.
2. Clock-dial staleness off-camera: do inactive dial FSM variables hold
   current values? (Same one-shot session.)
3. `Location Description` render target (map hover vs entered header) — where
   the game shows it decides which cell/level it rides.
4. Action-count off-camera: decode action-card gating before the column reads
   anything not currently rendered (cards flip availability live — s7
   DELIVER DATA).
5. Multi-drive tracking: confirm two simultaneous tracked drives → two pips
   (expected yes; per-quest flags, nothing exclusive in the decode).
6. Zone-name visibility from boot (spoiler guard relaxation check).

## Open questions

- Characters: interleaved rows vs own tab (ruling 6, owner).
- Commit path for off-camera rows (Call 3, owner).
- No-highlight browse: owner sign-off (channel section).
- Key to open the table (N inherited from the tree spec; confirm).
- All wording (calibration pass on first live build).
