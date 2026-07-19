# Input model — keymap design session output

Designed with the owner, 2026-07-18 (session 4), over interaction-surface.md
(modes, channels) and input-contract.md (the 22 sanctioned actions). This
supersedes all prior bind rationale; wording examples are provisional until
owner calibration (standing rule). W2 implements against this document.

## Principles

- Channels do the work: FOCUS speaks free on highlight; AMBIENT announces what
  the game renders when it renders; QUERY answers on demand from stable
  rendered state; CONTEXT keys exist only inside their mode.
- The design serves the loop: wake (what do I have), navigate (where do I
  spend it), allocate (native flow + focus), resolve (ambient), review
  (character panel, drive log).
- Every game-touching key maps to one designed effect (input-contract rule).
  Queries and readouts touch nothing.
- Simple mod, spotless UX: contained readouts over data dumps; no invented
  render surfaces; camera and highlight ride the game's own machinery.

## Global keys

- **Arrows / Enter** — native move and submit on current selection. Enter with
  nothing selected mirrors the game's own Confirm backstop (re-anchor to
  nearest marker) instead of silence.
- **Backspace** — the designed cancel, mode-resolved: die-picker Back, window
  close, Leave button; inside pause it becomes the pause-scoped cancel
  (`Pause Back`) replacing today's name-guess clicks.
- **V — vitals readout.** "Cycle 4. Energy 3 of 5. Condition 40, declining.
  Cryo 80." Energy spoken in boxes (bar has 5 real segment splits); condition
  as value + rendered band word (bar is mechanically continuous fill — boxes
  would be invented); STARVING folds into the energy clause when rendered;
  cryo appended provisionally (owner: revisit if play surfaces a better home).
- **D — dice string.** "3 dice: 6, 2, 5." Tray is not natively focusable
  outside allocation (corpus-verified), so a flat string, no pulldown. Spent
  handling at wording calibration.
- **Z — respeak last utterance**, whatever it was.
- **R — reread last dialogue block** (absorbs F2's old role).
- **Alt+Up / Alt+Down — dialogue buffer walk**, block by block. Live-render
  scroll sync is a deferred grace feature (the designed `Scroll Window`
  scrollbar input exists to ride when we add it).
- **Esc** — pause (native, untouched).
- **Space** — describe focused element (retained).
- **L — where-am-I report.** The location/mode query: current zone and marker
  on the station map, current window or mode elsewhere. (Owner assignment,
  correcting an earlier draft that had L on the tree.)
- **Speech control: Z only.** Grave (stop), brackets (speech history), and F2
  are killed (owner ruling) — the screen reader's own interrupt covers
  stopping, and Z + the Alt dialogue buffer cover recall.
- **F1 — contextual help.** Speaks only the keys active on the current screen,
  the console glyph-guide idiom — the modality layer's key scoping feeds it
  directly. Full-keymap listing retired with it. Later grace feature: surface
  it through the game's own UI if a sanctioned render path exists.

## Mode-scoped keys

- **Station map: N — navigate tree.** Two levels: region (Rim, Greenway, Hub)
  → reachable markers only. Down/Up walk; Right expands a region; Left
  collapses. Each cursor move natively selects the marker (game renders its
  own highlight); camera rides the game's machinery if that's free (W3 wiring
  question), no drawn UI ever. Marker items carry clock annotation (licensed:
  markers render their own live clock dials — corpus, 504 instances) and, if
  TBD-4 lands rendered, drive-relevance. Retires the old L-cycle; outside the
  tree, plain arrows drive the game's own adjacency navigation exactly as a
  controller would.
- **Surface toggles (as-is, owner ruling: placement immaterial):** I inventory,
  U character window, J drive log, S scan/cloud boundary. W2 makes their
  refusals mode-aware ("Character window open") instead of generic.
- **Location action view: arrows, no Tab.** Action cards are walked
  forward/backward with plain arrows — the game's own native adjacency
  (variant-C checkers, brief E); Tab is dead as a browsing idiom (owner
  ruling: it broke the game's nav mold). The mod adds indexed framing to the
  utterance ("2 of 4", wording at calibration). **K browses clocks
  separately:** clocks are location-tied by the game's own structure —
  rendered as their own cards sibling to actions (never children of one), as
  the location marker's dial on the map, and display-only outside the nav
  graph (no selection machinery, corpus) — so per owner ruling they get
  independent browsing and no action attachment in utterances.
- **Dice allocation:** fully native picker; per-die focus string "Die 2,
  value 5" / "Die 4, spent". Shift+R reroll (retained; reroll render
  semantics await first live sighting — owner defers). Placement and
  activation are SEPARATE designed inputs (settled after two wrong readings;
  the evidence, in order of weight: the cursor's Slot Die state has NO
  FINISHED exit — its only transition is DragReset → Unslot, so a placed die
  RESTS in the slot; the system FSM likewise rests in Slotted with
  Back → Active; and the deleted session-1 numeric implementation had to
  synthesize START ACTION as its own third event — native flow gets it from
  a distinct player press). Flow: Enter applies the die (rests, "Die
  slotted."), Enter again activates (START ACTION), Backspace retracts
  (DragReset → Unslot, die returns to tray). Retraction is bound to the
  UNIVERSAL Back action by the game itself (Slot Die state watches
  RewiredPlayerGetButtonDown "Back" → DragReset, corpus-verified) — so
  Backspace's layered meaning here follows game convention exactly; a
  dedicated retract key would deviate from it. Retraction gets its own
  announcement — it must not sound like a cancel. Wiring detail for W3: the
  exact native target of the activation press; live-confirm both
  announcements' timing.
- **Dialogue:** continue on Enter (native); number keys pick responses;
  auto-read ambient.
- **Tutorial:** T re-engages continue (retained); per-context review cursor.
- **Character window:** per-context review (shipped); window-open readout
  includes upgrade points AND drive points; skill rows on cursor.
- **Cloud:** list-primary model (scoped tree, families → nodes), pending the
  scan-mode native-nav live check; dice machinery native as station.

## Ambient commitments

- Wake readout: when the cycle transition completes, announce the new state —
  dice rolled + vitals changes — in V's vocabulary (extends the shipped
  "Cycle complete" summary).
- Points earned, skill break/recover, drive events: notification templates
  confirmed rendered; speak them (partly shipped).
- TBD-1: if an upgrade-available badge renders on the character button, it
  becomes a standing notification.
- Input-mode switches, scan toggle: retained ambient announcements.
- Force-unslot: the game can yank a slotted die itself (ForceUnslotDice →
  Off, e.g. a window opening mid-allocation). Announce it via the state-entry
  signal so the player's mental model never goes stale silently.

## Open items ledger

Design slots: where-am-I key; speech-control survivors; cryo long-term home;
reroll surfacing.
Evidence (desk-first): TBD-1 upgrade badge; TBD-2 skill render moments; TBD-3
reroll render; TBD-4 drive→node linkage; condition-bar art check (paint-level,
wording-only).
Wiring (W2/W3): camera-follow on native marker select; cloud scan-mode
controller-nav check; drive-log open branch; response-menu default selection;
Input Pauser per-trigger census; buffer→scroll sync (deferred grace feature).
