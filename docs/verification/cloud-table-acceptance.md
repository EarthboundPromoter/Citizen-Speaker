# Cloud table — acceptance predictions (built 2026-07-20)

Owner-approved design (geometry walk + bridge sweep same day): corridor-sorted
node table + two callout channels (entry census with agent "moved" lines,
post-hack reveal callout). Ground truth: field is one-axis (statics), reveal
edges are local gate→neighborhood (sweep), completed nodes self-hide, agents are
3 position-variants per identity. Columns Name | Status | Drives — Demand/Actions
render only inside an entered node (surfaced deviation from the pre-walk sketch;
they stay with the card flow).

## Predictions

1. **N in cloud mode:** "Cloud table. <first row>" — rows are rendered nodes
   (canvas dial in Variables Met, camera-independent), Z-descending. Full row =
   name, tagline riding, "open" when it's the entered node, drives when pips
   render. N or Backspace closes ("Cloud table closed."); N at station still
   opens the station table unchanged.
2. **Left/Right:** "Status: open/available", "Drives: <names>" / "No marked
   nodes." Up/Down at column ≠ 0 scans that column down rows (name + cell).
3. **Enter on a row:** table closes silently, one native marker click, the
   game's camera flight runs, CloudFlight's settle announce lands as today.
   Cloud markers have no frustum gate (no Off Camera state in their FSMs), so
   commit should work anywhere on the corridor without a camera pre-move — if a
   marker refuses off-screen, that's the finding.
4. **Entry census:** first cloud entry of the game run is a silent baseline.
   Later entries: "1 new node: NAME. 1 node gone: NAME. NAME moved." — moved =
   same rendered name at a different corridor position (agents).
5. **Post-hack reveal:** completing a gate-type hack speaks "Revealed: NAME[,
   NAME]" ~0.6s after the outcome read; an ordinary data-node completion speaks
   nothing extra (its own despawn updates the rolling set silently — the outcome
   announce already covered it).

## Round 2 additions (owner rulings, same day — commit c43cb92)

6. **Columns now Name | Status | Demand | Narrative | Drives.** Demand speaks on
   EVERY dice node, entered or not: values from the authored Required Roll
   constants, count from the INTERFACE bucket (at +1, every node offers 2 dice —
   "Matches die N or M"). Cipher/gate rows read "Takes an item…" instead. A
   demand heard on a dormant node should match the glyphs seen after entering
   it — the acceptance check.
7. **Narrative cell** is empty until a node's group first activates, then
   persists for the session — an entered node's narrative is re-readable from
   the table any time (N inside the node auto-selects its row).
8. **Camera-synced browse** (commit 0f4fd7e): row moves pan the corridor like
   the station table; same config switch.

## Wiring risks flagged in advance

- Node names come from the canvas FSM's Location Name variable (Text Setup runs
  at scene load, so names should resolve even for not-yet-revealed nodes; if a
  first-reveal row speaks an empty name, that's the Text Setup timing finding).
- Cloud-mode entry edge drives the census schedule; a mode flicker during
  camera flights would double-schedule — dial-first CloudActive should prevent
  this (108-divergence session evidence), but listen for double censuses.
- Browse is camera-silent in v1. Focus-rig sync (the station table's camera
  driver) is a live-check follow-up: the Hacking UI hangs under the same
  one-axis rig, so writing Focus Z may pan the cloud view too — check by ear
  with W/S while the cloud is up before wiring it into row moves.
- "moved" threshold is 100 Z-units (agent variant spacing is ≥300; billboard
  jitter is 0) — false "moved" lines would mean the threshold needs raising.
