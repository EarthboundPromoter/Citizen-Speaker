# Gameplay observations — running log

Owner-directed running file (2026-07-19): facts gleaned from live play sessions
that aren't design decisions (those live in focus-model.md / input-model.md) or
architecture (build-plan.md / verification briefs). Append per session.

## Session 5 — 2026-07-19 (six fresh-game runs, MACHINIST test saves)

### Game facts

- Shipped keyboard map is exactly five binds (level0 Rewired dump, live-confirmed
  owner-observed): W/S camera scroll, A/D rotate view, Tab DEBUG MENU. The game
  is otherwise mouse+controller; camera keys work even in gamepad UI mode.
- MACHINIST fresh start: energy 40, condition 65 ("flickering"), 4 dice.
- Condition band "flickering" spans at least 65 down to 45 — bands are wide.
- "Starving" renders as the energy band word at energy 0.
- Cloud/DATA CLOUD unlocks via a story beat around cycle 2 on a fresh game.
- Cycle clocks self-tick (HELION CROSSING observed 0→1 across a cycle).
- END CYCLE renders a disabled state when unavailable ("Enter to activate,
  disabled").
- The cycle-end sequence has exactly ONE exit (corpus): every route lands at
  Idle; narrative injection only via Cycle Scene Manager's five one-shot beats;
  REVEAL DICE! runs immediately before Idle (new dice rendered by summary time).
- `Autoplay Waiting` is a scheduling flag with a designed leak (Autoplay Wait's
  Check Variables → Off exit never clears it), and **Autoplay Wait means "scene
  armed, waiting for the player to click the character marker"** — it is
  interactive, not a playing scene. Only the `Autoplay` state is a playing scene.
- Tutorial "perform the taught action" dismissal variant exists (cloud tutorial:
  continue stays disabled while the panel is up). Two disabled-continue
  instances observed; it's a pattern, not a one-off.
- The cloud tutorial panel stays GameObject-active after dismissal and hides by
  CanvasGroup alpha (the panel corpse held Tutorial mode until the alpha fix;
  the trap outlived even the pause escape — owner had to Alt+F4).
- The drive log is a LIGHT overlay: opening deactivates only the inventory;
  locations/top UI/bottom UI stay active and selectable underneath (brief E
  modal map, felt live as focus escapes).
- The game has NO nav boundaries anywhere: Automatic adjacency spans all active
  selectables scene-wide; on controller the game never walks this graph
  (stick-scroll + parked selection) — mod arrows were its only traverser.
- Dice model: single-die register by construction (`SlottedDiceGlobal` holds THE
  resting die; sibling slotting fires DragReset at the previous die). Designed
  flow is slot → start (or Back to retract); no multi-action staging. Post-slot
  navigation collapses to the armed card + START by the game's own graph.
- Native retraction (Back with resting die) lands focus on the ACTION CARD, not
  back in the picker (owner wants retract→rehover; approved design, pending).
- The UI selector's closest-candidate set includes the inventory DATA button —
  with few markers unlocked it becomes the default focus after tutorials and
  cycle ends (nothing re-anchors at the cycle boundary; windows do on close).
- Station node families: Locations + Characters share the billboard template;
  nodes migrate between families as story moves (Dragos: character marker at
  intro → Dragos's Yard location after cycle 1 — heard live as "1 location
  added. 1 node removed."). Location canvases have story-state variants
  (Canvas / Canvas 2); latent active-but-noninteractable canvases exist.
- Sealed docks render at NEITHER game start nor cycle 4 — progressive reveal
  confirmed; Map Key carries a "Sealed" legend for whenever they do.
- Dock C-4: clickable narrative/clock-only location (its action group holds a
  clock card and no Action objects) — "has actions" vs "narrative only" is a
  readable per-node distinction.
- Marker billboards: name text renders only on selection/hover; map-level clocks
  are dial-only (name lives in the location card); drive relevance renders as a
  named-per-drive pip — the drives tutorial calls it a "YELLOW MARKER" (the
  game's own vocabulary for it).
- Options menu is ALL discrete native buttons (TEXT Default/Large, SCROLL
  Slower/Default/Faster, MUSIC/SFX 0–5, Back); current value renders as the
  accent-colored label; hover is a distinct outline. Rendered row labels:
  "SCENE TEXT SIZE", "SCROLL SPEED", "MUSIC LEVEL", "AMBIENT SFX LEVEL".
- Title surface (corpus): Landing → NEW GAME / CONTINUE / Update News (QR
  Code Canvas + Newsletter + CLOSE) / Language (4) / 3-slot save menu with
  Empty/Filled states.

### Mod behavior observed (run 6, the long run)

- Composed cycle-end totals string fired three times, values tracking perfectly
  (energy 40→0 starving; condition 65→60→45; cryo appearing 35→65).
  WORDING GAP: dice tail is verbose ("die 1, value 1") vs owner spec (bare
  values) — calibration item.
- The event-truth architecture caught a live recurrence of the drive-log alpha
  lie (one DIVERGENCE line: alpha=True, truth=False) and neutralized it — the
  old system would have re-trapped the run.
- Scan Button reported unmapped state 'Off' via graceful silence — add to the
  not-cloud map.
- Node census ran a cosmetic early 0/0 baseline before the real one (0
  locations, 3 characters) — tighten the trigger.
- Options review has had NO live hearing yet (no [Options] lines — owner never
  entered options post-build).
- Enter on a disabled tutorial continue says "Not activatable" — honest but
  unhelpful; consider "Continue not ready yet" wording.
- Full milestone: complete intro tutorial sequence + three cycle ends traversed
  keyboard-only on a fresh save; both outcome tiers heard; retraction verbatim
  captured (picker prompt re-announce — wording deferred to W4 real-state
  hooks); K clock reads correct at two locations; autosave activated on tutorial
  completion (both pause-line branches heard on one save).
