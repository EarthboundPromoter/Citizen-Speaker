# W1 substrate — acceptance predictions (written pre-launch, 2026-07-18)

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
