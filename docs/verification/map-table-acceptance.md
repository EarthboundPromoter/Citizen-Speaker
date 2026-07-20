# Map table v1 — acceptance predictions (written before first launch, W5)

Build: `a8aff7b` (mod) + `4da8e4e` (bridge /transforms). Config:
`MapTable.CameraFollow` (default on) kills all camera writes if needed.

## Predictions

1. **N at the station** opens: "Station table. The Rim. <row report>" — starting
   row is the location nearest the current camera. N or Backspace closes.
   N anywhere else refuses mode-aware.
2. **Row report shape**: "Feng's Bay. Clock 2 of 3, negative clock. Actions
   2 actions. Description <tagline>." — populated facets only; a bare location
   is just its name. Characters read "Character: <name>".
3. **Up/Down** moves rows in corridor order and THE CAMERA FOLLOWS (Focus Z
   write): the marker scrolls into view, the native highlight lands SILENTLY
   (mute working = no focus chatter during the sweep). FIRST REAL VALIDATION
   of the camera write — if the camera jumps wrong or oscillates, flip
   CameraFollow off and we calibrate the angle mapping from the F-dump
   (/transforms vs Damped Z).
4. **Angle calibration risk (expected miss, first launch)**: MarkerAngle uses
   raw atan2 vs the rotator — the offset/direction may need one live
   correction. Symptom: camera moves the WRONG WAY or by a wrong offset on
   row moves. The /transforms endpoint exists precisely to calibrate this.
5. **Left/Right** walks columns with stable geometry: "Clock: no clock." on
   empties — never skipped, never reordered.
6. **Slash** cycles tabs: zone tabs limited to visited zones (Greenway needs
   GREENWAYVISITED; Hub needs having been there this session — refinement
   flagged for owner), then Characters, then Tracked Drives (only if any
   tracked). A zone tab for a different zone FIRES THE NATIVE TRANSIT (spoke/
   ferry camera ride + sound) — second first-validation. Greenway↔Hub routes
   through Rim (two rides).
7. **Enter on any cell** commits: one native click, table closes, the marker's
   own flow zooms down. On a disabled row: "<name>: Not open yet."
8. **Space** speaks the full row, every column incl. empties.
9. **Tracked Drives tab**: rows = tracked quests with active objective text
   and pip-derived locations ("At: ..."), or "No current objective." — also
   finally answers whether all four of the owner's toggles landed on.
10. **Zone/staleness caveats (not failures)**: clock and action cells for
    never-visited locations may lag a cycle (as-rendered truth); zone
    classification of Hub rows unproven until first Hub check.

## Verification aids

- Bridge `/transforms?filter=Marker` — marker positions for angle calibration.
- Bridge `/transforms?filter=Focus Rotator&max=5` — live rig angle while
  browsing.
- `[Table]` log lines: camera writes (TraceInput on), zone transit fires,
  graceful silences.
