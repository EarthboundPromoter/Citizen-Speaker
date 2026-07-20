# Fix-pass verification — run 2 on the fresh save (2026-07-20)

Verifies commits `3d14b86` (F1–F10 fix pass) + `862c222` (F5 energy channel).
Save state at last quit: slot 1, MACHINIST, cycle 2, at/near Bright Market,
SURVIVE tracked, BACK IN BUSINESS 2/8. Play-flow ordered; F3 on anything odd.
F4 (tutorial transcode) is DEFERRED — needs unseen tutorials; likely a later
run. Companion: fresh-run-findings-2026-07-20.md (mechanisms + evidence).

## 1. Boot

- [ ] Mod loads clean; energy AND condition channels stay SILENT at boot
      (baselines) — no spurious "Energy falling" on load.

## 2. Map table (F1 ghosts, F8 pips)

- [ ] EMPTY CONTAINER row: NO clock cell spoken at all (was three ghosts).
- [ ] DRAGOS'S YARD row: BOTH real clocks — BACK IN BUSINESS 2 of 8 positive
      AND A DEBT CALLED IN negative cycle (was one wrong-polarity ghost).
- [ ] BRIGHT MARKET row: LOCAL KNOWLEDGE 0 of 3 kept (real clock survives the
      filter).
- [ ] BRIGHT MARKET Drives cell: "Drives: SURVIVE." (was "No tracked drive").
- [ ] Same Drives read from a FAR camera position (pan away first) —
      camera-independence check (F8 addendum).
- [ ] Tracked Drives tab: SURVIVE row with objective + "At:" location.
- [ ] Sweep the corridor: note every clock cell — diff against run 1's reads
      to learn which of the unvisited-location clocks were real (Shipyard 0/8,
      Dock B-2 0/4, Rotunda ×2, Dock C-4 0/8, Lowend/others bare).

## 3. Bright Market location table (F9 trim, F10 critical)

- [ ] Actions tab now has TWO rows; Up/Down actually alternates them.
- [ ] ASK FOR DIRECTIONS full row reads: "... INTUIT ..., danger, critical
      action. Takes a die. ..." (badge text after risk word — F10).
- [ ] Risk column on that row: "Risk: danger, critical action".
- [ ] Enter on ASK FOR DIRECTIONS opens the die picker (commit path clean on
      the trailing-space card).
- [ ] EXPLORE THE MARKET row: no "critical action" (repeatable = silent).

## 4. Focus settle idiom (F2/F6)

- [ ] Die picker open: ONE die readout only (the resting hover), then the
      picker prompt — no four-die burst, no queued stragglers.
- [ ] After any conversation ends: no action-card pinball; at most a single
      settled announcement (or silence if focus lands somewhere already known).
- [ ] REGRESSION: user arrowing (tables, picker, dialogue choices) still
      speaks instantly — the settle delay must never touch user-initiated moves.
- [ ] REGRESSION: BL-10 inventory cursor reads still work (separate deferral).

## 5. Outcome composition (F7)

- [ ] A clock-progressing outcome: effect line says "BACK IN BUSINESS now 3
      of 8" (state form) and the separate clock callout does NOT repeat that
      clock (dedupe). A clock NOT in the card (cycle self-tick) still calls out.
- [ ] An energy-costing outcome: "ENERGY minus 1, now <Lua>" — NOTE the "now"
      value is still the Lua integer this build; segments-unification is the
      NEXT wording pass (calibration below feeds it).
- [ ] Condition effect (if any): delta + now-value + band word rides.
- [ ] Cryo gain unchanged: "plus 15 CRYO" style.
- [ ] OBSERVE: any multi-sign item effect ("++ X") now reads "plus 2, X" —
      provisional wording, owner calibrates by ear.

## 6. Energy channel (F5, new)

- [ ] Mid-cycle energy drop (action cost / cycle tick): "Energy falling: N."
      once per LEVEL change — no chatter while value drifts within a level.
- [ ] Eating/recovery: "Energy rising: N."
- [ ] If energy empties: "Starving. Energy empty." exactly once; re-arms
      after recovery.
- [ ] Log calibration (Claude): `[EnergyWatch] level N (Lua E)` pairs → fix
      the level↔value thresholds and confirm max (then "of 6" wording call).

## 7. Census mute (F3)

- [ ] Entering/leaving locations and camera pans produce ZERO census speech
      all session; `[NodeCensus] (muted, BL-16)` lines may appear in the log.

## 8. Condition-segment calibration (Claude, bridge — feeds the unification pass)

- [ ] Count tick/divider objects in the condition bar hierarchy live.
- [ ] Note condition value before/after one cycle end (segment size check;
      expect 5 points → 20 segments if the tutorial's "one segment per cycle"
      maps linearly).
- [ ] Same look at the energy bar graphic to confirm 6 boxes rendered.

## 9. Carry-over from run 1 (still unexercised, opportunistic)

- [ ] Bare-outcome family (BL-14): any item/value-check action — name +
      content, no tier word.
- [ ] Skill-locked card row + Enter reason; cryo/item affordability bounces.
- [ ] Character window (§8 checklist): first Enter watch (armed-detection
      risk), purchase compose once points exist.
- [ ] Condition channel (§7): first genuine band crossing; breakdown +
      BL-3 late-fill tutorial when it comes (also first F4 data point if the
      breakdown tutorial carries glyphs).
- [ ] Track toggle "Tracking: X.", abandon two-step, BL-4 swap watch.
- [ ] Cloud (§11) once story unlocks it.

## Standing watches

BL-15 (char toggle refusal — F3 it), BL-19 Ghost Trackers, BL-2 glyph guard,
strip-steal recovery, drive-log alpha divergence (2 samples, cosmetic),
new-flag pulses (samples accumulating), P1–P6 polish samples by ear.
