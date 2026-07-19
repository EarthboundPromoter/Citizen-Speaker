# W1 substrate — acceptance predictions (written pre-launch, 2026-07-18)

## RESULTS — live run, 2026-07-18 night (same day)

All four predictions checked; substrate CONFIRMED. Details:

1. **CONFIRMED.** `FsmSignals hook alive: 11 state entries observed` at title.
2. **CONFIRMED with a save-identity surprise.** Snapshot fired during station
   load: `cycle=4 energy=0 condition=40 bits=80 upgradePoints=0 drivePoints=?
   class=OPERATOR skills=-1/0/0/1/0 introComplete=True`. These disagree with the
   session-3 save description (cycle ~7, INTUIT upgraded) — but bridge `/texts`
   cross-check against rendered truth on the same launch matched the Lua values
   exactly on every value the game renders as text: Class Name "OPERATOR",
   Cryo Slot Amount "80", Points Av "0", condition band "DECLINING" with the
   energy UI's STARVING label active (energy 0). Verdict: the adapter is right;
   this launch loaded a different playthrough (early-game drive tracker state,
   no upgrades spent) than the one the state log describes. Both saves
   presumably exist; which is current is the owner's to say. Numeric
   energy/condition cross-check CLOSED same session: the owner's C query
   (HUD-FSM read) spoke "Energy 0. Condition 40, declining" — exact agreement
   with the Lua snapshot. The single-store verdict (brief F) holds numerically;
   W4's C-query migration to Lua is cleared.
   `drivePoints=?` = `Player_DrivePoints` unset in this save — presence-aware
   nil, working as designed.
3. **PARTIALLY FALSIFIED, benign.** At the mid-load snapshot moment only
   leaveButton and uiSelector (`UI selector`, lowercase — noted) were set;
   Dialogue Panel / Response Menu / Saver / ActiveAction were null. The
   registry is populated lazily as systems spawn — anchors must always be read
   at use time, never cached at load (the accessors already read live; W2
   consumers must not add caching). Non-null confirmation for the late four
   rides the next dialogue/save moment.
4. **CONFIRMED.** No new speech, no flood, shipped features unaffected.

## Original predictions (pre-launch)

Per the W5 validation model: these are wiring-predicted outcomes for the next
launch. The live session confirms or falsifies them; a falsified line gets a
same-session correction here. Everything below is log-only — the substrate speaks
nothing and changes no shipped behavior.

Read from `BepInEx/LogOutput.log`.

## Predicted, in order

1. **FsmSignals hook alive.** Within ~10s of the title menu:
   `[Substrate] FsmSignals hook alive: N state entries observed.` with N well
   above zero (title FSMs transition constantly). N stuck at 0 = the
   `Fsm.EnterState` patch is dead — W2 blocks on diagnosing it.
2. **Lua adapter up** — only after the save is loaded (the Lua database carries
   save state; at title, `Cycle` reads nil and the selftest keeps retrying
   silently at 5s intervals):
   `[Substrate] Lua adapter up. cycle=~7 energy=0 condition=30 ...` matching the
   owner's live save (cycle ~7, energy 0, condition 30, INTUIT bucket reflecting
   the spent upgrade point, introComplete=True). Bits/upgradePoints/drivePoints:
   no predicted numbers on file — record actuals here and cross-check against
   rendered HUD/character-window values on the same launch.
3. **Anchors snapshot**, same moment: dialoguePanel, responseMenu, leaveButton,
   saver, uiSelector non-null; `activeAction=null` expected at station idle (it
   points at the engaged action panel — null until one is opened). Any
   `Anchor 'X' is not defined` line = a name-spelling miss against the corpus,
   fix same session.
4. **Silence.** No new speech, no change to any shipped feature, no log flood
   (TraceFsmSignals defaults false; the per-transition cost is a ring-buffer
   struct write).

## Failure signatures

- `[Substrate] Lua read failed (...)` — DialogueLua call path wrong; adapter down.
- Condition/energy from Lua disagreeing with the HUD-FSM values the C query
  currently speaks — would contradict brief F's single-store verdict; escalate to
  a doc correction, do not ship W4's C-query migration until resolved.
