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
   lands.
7. **Characters: both** (owner, walk 2026-07-20): interleaved in zone tabs in
   corridor order (a character is another stop on the ring; name cell carries
   the game's kind word) AND an extra Characters tab whose additional
   "where are they" cell gives zone + nearest non-Off location ("near Feng's
   Bay, the Rim"). Both halves ride one new instrument: corridor-axis marker
   positions (zone assignment + proximity) — see prerequisites.
8. **Camera-synced browse** (owner, walk 2026-07-20 — supersedes the
   no-highlight consequence below): the browse DRIVES the camera. Slash tab =
   fire the Location Controller's own zone transition (designed event;
   character tab excluded by design); row move = move the camera to the
   highlighted marker and let the native highlight land. The screen stays
   synced with the browse at all times.
9. **Commit = Enter on ANY cell of the row** (owner-confirmed): horizontal
   position is facet browsing; Enter always means "go there." Space is
   reserved for future cell-level actions (consistent with Space-for-detail).
10. **Key: N** (owner-confirmed). Wording: ships provisional, owner
   calibrates live (standing practice).

## Announce grammar (draft — owner calibration pass expected)

- Row move (Up/Down): "<Name>[, new][, disabled]. [Clock <n> of <m>
  <positive/negative>.] [Drive: <name>[, <name>].]" — populated facets only.
- Row move while parked in a non-Name column: "<Name>. <current cell>."
  (scan-one-facet-down-the-station idiom).
- Column move (Left/Right): "<Header>: <cell>." Empty: "<Header>: none." /
  facet-specific ("No clock.").
- Tab swap (slash): "<Zone>." then first row report. Empty zone tab: see
  spoiler guard.

## Channel + selection (RULED — camera-synced browse, walk 2026-07-20)

- The browse drives the camera (ruling 8). Row moves bring the target marker
  on-frustum, its canvas re-enables the contents, the selector/native
  machinery places the real highlight — the old channel-law concern (mod-held
  browse selection) is moot because selection lands via the game's own claim,
  not a mod grab. This restores the original input-model intent ("each cursor
  move natively selects the marker") inside the table UX.
- Camera mechanism, preference ladder (decode decides — prerequisites 7/8):
  (a) designed camera-rig event if one exists (canvas FSMs hold Main Camera
  Transform references — a rig FSM is likely); (b) short synthesized native
  scroll hops toward the target — zone jumps absorb long distances, so
  within-zone hops stay small; (c) fallback: highlight without camera, camera
  on commit only.
- Pass-over chatter: as the camera sweeps between rows the selector claims
  intermediate markers — reuse the CloudFlight pattern (mute game-driven focus
  during a table-driven move, announce the settle once). Second customer of
  that machinery.
- Commit: Enter on any cell = one native click on the row's marker
  (single-dispatch; the click's own Camera Transition flow does the zoom).
- Companion feature (endorsed, separate build): **scroll ticker** — FsmSignals
  on canvas FSMs entering/leaving `Variables Met` announces markers as they
  come over the horizon during native WASD browse. Pure announcement layer,
  no interception. (Full WASD interception/snap-scroll: rejected —
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

1. ~~Live one-shot canvas dial~~ PROVEN (live 2026-07-20): all 147 canvas
   FSMs read camera-independently via bridge; states matched reality exactly
   (3 Variables Met on-camera, 6 Off Camera = the WASD-discovered set, rest
   Off/Off 2); Rotunda observed flipping Off Camera → Variables Met live as
   the owner scrolled to it. Variant dedup confirmed (Empty Container live
   variant = Canvas 2, original Off 2). NOTE: bridge FsmList/Find cannot
   reach DISABLED objects — the mod-side reader walks from the always-active
   canvas roots (GetComponentsInChildren(true)); bridge limitation only.
2. ~~Clock-dial staleness~~ RESOLVED BY DESIGN (live 2026-07-20): billboard
   dial FSMs drop out of FsmList while contents are deactivated, and their
   Setter re-syncs on enable — so the table reads the authoritative clock
   source (the same variables the K index reads), not the billboard render.
   Verify that read at build. Also learned: not every location billboards a
   clock (Rotunda has none — its clocks render only in the location view);
   the Clock column is map-parity and stays empty there, correctly.
3. ~~Description render target~~ RESOLVED (live 2026-07-20): the billboard
   itself renders name + a SHORT TAGLINE ("ROTUNDA" / "Old dock terminal") —
   the `<ID>_DESC` string the canvas FSM resolves. Description column = the
   tagline, map-parity by construction. The long in-location narrative is
   separate content and stays in the location view.
4. Action-count off-camera: decode action-card gating before the column reads
   anything not currently rendered (cards flip availability live — s7
   DELIVER DATA). STILL OPEN.
5. ~~Multi-drive tracking~~ RESOLVED (owner account + live table evidence,
   2026-07-20): tracking is SINGLE — the journal enforces one live tracked
   drive; each new track replaces the previous (three consecutive
   replacements observed via the table; owner confirms that is how the game
   presents it). The pip data could hold several flags, but the game never
   does. Consequences: the Drives column carries at most one name; the
   Tracked Drives tab is a one-row what-am-I-tracking status view — exactly
   the BL-18 workaround. Pip observation still lands naturally when the
   tracked drive's location becomes available.
6. ~~Zone-name visibility~~ CHECKED (live 2026-07-20): zero zone names
   rendered at the station map — the visited-flag gating on zone tabs stands.
7. ~~Zone-transition decode~~ DONE (2026-07-20, desk): the Location
   Controller's zone states consume plain FSM events (`RimTransit` /
   `GreenwayTransit` / `HubTransit`) and run fully self-contained tweened
   transitions (Focus position, rotator angle, zoom, sound, Leave Button
   notify). Zone truth = Lua `LOCATION` (0 Rim / 1 Greenway / 2 Hub).
   Topology: Greenway↔Hub only via Rim (tab routing follows it). CAVEATS:
   (a) no in-corpus sender of these events was found — they exist and are
   consumed, but the sanctioned sender is unidentified; first live zone-tab
   validates. (b) STORY SAFETY (design amendment, needs owner confirmation):
   the Greenway state SETS `GREENWAYVISITED` — firing a transit into a
   never-visited zone could bypass first-visit story gating. Zone tabs
   therefore only offer zones already visited natively (visited flags are
   clock-tier reads); first-time entry stays on the game's own ferry/spoke
   flow. This also strengthens the spoiler guard.
8. ~~Camera-rig decode~~ DONE (2026-07-20, desk): the station camera is one
   axis — Focus Rotator's Z angle, written every frame from `$Damped Z`
   smooth-damped toward `$Focus Z`, which accumulates the Rewired scroll
   axes and is FloatClamp'd per zone state (Rim 135–258; gated Rim 135–200 —
   the `RIMGATE` Lua variable opens corridor). The game's own designed jump
   (double-press scroll) IS a discrete write: Jump states do `$Damped Z ±20`
   + clamp + SetRotation. Row-move camera route, preference order:
   (a) RECOMMENDED — write `$Focus Z` to the target marker's rotator angle
   and let the game's own damping/clamping/rotation drive everything (the
   variable is the designed input accumulator; the game's jump states are
   precedent for discrete writes; native clamps make overshoot impossible);
   (b) fire the FSM's own `Jump Up/Down` events (±20° designed steps) with
   canvas-state feedback; settle signal either way = target canvas leaving
   `Off Camera`, CloudFlight-style mute during motion. Corridor order for
   rows = marker rotator angle (same instrument as prerequisite 9).
9. Corridor-axis position instrument: marker world positions for zone
   assignment + character proximity (ruling 7); one live read.

## Open questions

All design calls made (owner walk, 2026-07-20). Remaining before build:
the prerequisites above, then wording calibration on first live build.
