# G — code vs. wiring gap ledger

Corpus-grounded audit of every mechanism the mod implements today against what the
FSM census (`tools/analysis/corpus/fsm-census.jsonl`, 4,948 FSMs, station scene)
shows the game's own machinery actually does. Method: streamed the corpus with
Python filters (never opened raw — see spoiler rule in the corpus README);
per-mechanism extracts live in `tools/analysis/scratch_out/*.jsonl`. Every state
name, variable name, and global-variable reference below is corpus-verified by
direct query, not inferred. Ordered by priority — how often the current approach
causes the live-failure class the owner is tired of (silence, wrong branch,
heuristic guess) versus how confidently the fix is known.

## P0 — dead-code precision signal (fix is exact and known)

### 1. Dice-slot commit watcher checks a state name that does not exist

**CURRENT**: `mod/src/Watchers.cs` `CheckDiceAllocation()`, lines 379–397. For every
`Gamepad Dice Slot` FSM the loop only acts `if (slotState == "Slotted")` (guard at
line 388: `if (!known || slotState != "Slotted") continue;`), announcing "Die
slotted. `<action>`." The code comment (line 360) calls this "the commit signal";
when it doesn't fire, `CheckDiceAllocation`'s outer block (lines 348–376) falls
back to a 1.5s heuristic window comparing `SlottedDiceValueGlobal()` against a
snapshot and `_lastControllerLeftIdle`.

**WIRING**: queried every `Gamepad Dice Slot` FSM in the corpus (218 instances,
`tools/analysis/scratch_out/gamepad_dice_slot.jsonl`) and enumerated every state
name that occurs anywhere in them. Two template variants exist, and **neither
has a state named `Slotted`**:
- Single-slot template (99 instances, e.g. `Seed Power Routing Action`): states
  `Idle`, `Slot Item`, `Reset`, `Refocus`, `Focus`, `Check Amount`. The commit
  transition is `Check Amount`'s `Slot Die -> Slot Item`.
- Multi-slot template (119 instances, e.g. `Search for Signals Action`): states
  `Idle`, `Select Dice`, `Select Dice 2`, `Unslot Die`, `Set Slot Pos`,
  `Refocus ui`, `Reset`, `Refocus`, `Focus`. The commit path is
  `Set Slot Pos -> Select Dice -> (Slotted event) -> Select Dice 2`.

`Slotted` is a real event name used *inside* the top-level `Dice Gamepad System`
FSM (`Active` state, `Click -> Slotted` transition — confirmed in
`dice_gamepad_system.jsonl`) and inside `Select Dice`'s own transition table, but
it is never a **state name** on the per-slot FSM the mod polls. This isn't
"transient and the poll misses it" (the existing triage-13 theory) — it's the
wrong identifier entirely, so the branch is unreachable code across the whole
game.

**VERDICT**: REPLACE. Check `slotState == "Slot Item"` for the single-slot
template; for the multi-slot template, watch for entry into `Select Dice 2`
(reached only via the `Slotted` event) or, more simply, watch the *event* name
`Slotted` rather than a state name if a Harmony hook on event dispatch is
available (see #4). Either fix removes the need for the 1.5s heuristic window
entirely — a real, always-correct signal exists per template.

**RISK**: low. This only changes which branch fires; the heuristic fallback
stays as a safety net during the transition. Verify against both templates live
(single-slot action and a multi-slot one, e.g. a Cryo-cost action) before
removing the fallback.

**PRIORITY**: highest — this is the exact mechanism behind "Die slotted" silently
never announcing per-die and always falling through to the coarser heuristic,
which is one of the concrete failure classes in the triage history.

## P1 — Lua variable routes (resolve two parked design questions + remove heuristics)

### 2. Modifier-row emphasis: no FSM signal exists; the real source is a Dialogue System Lua variable per skill

**CURRENT**: `mod/src/UI/Describe.cs` `SiblingModifier()`, lines 187–229. Scans
the four modifier texts (`-1`/`0`/`+1`/`+2`, `ModifierLabels` at line 181) that
render as siblings, and picks the "single color outlier" (`SingleColorOutlier`,
lines 235–251) via a `0.05f` RGBA-closeness heuristic, logging a throttled
calibration dump when it can't decide. `docs/build-plan.md` §7 lists this as a
parked/unresolved design question ("Modifier-row emphasis layer... report 12").

**WIRING**: exhaustively searched the corpus for any FSM whose path or FSM name
contains "modifier" (case-insensitive) — **zero matches** across all 4,948 FSMs.
The only FSM under `Action Elements/Action Skill Display/` is `Skill Icon`
(119 instances), which only switches an icon sprite by skill enum — it does not
touch the modifier row. The modifier row genuinely has no PlayMaker presence.

But the *value* it's trying to show has an authoritative, non-visual source:
the Character Window's `SKILL List/{ENGINEER,INTERFACE,ENDURE,INTUIT,ENGAGE}`
FSMs (`character_window_fsm.jsonl`) each run a `Get Skill Rating` state that does
```
GetVariable  variableName=ENGINEER  storeFloatResult=$SKILL
FloatSwitch  $SKILL  lessThan=[0,1,2,3]  sendEvent=[-1, 0, +1, +2]
```
i.e. reads the Dialogue System Lua variable named after the skill
(`ENGINEER`/`INTERFACE`/`ENDURE`/`INTUIT`/`ENGAGE`) and buckets it into the
same four labels the modifier row renders. Companion Lua variables
`<SKILL>_PERKS` and `<SKILL>_BROKEN` drive the perk/broken states seen in the
same FSMs (`Perk 1`/`Perk 2`/`Broken`/`BROKEN` states).

**VERDICT**: REPLACE. Read the five skill Lua variables directly (Pixel Crushers
`DialogueLua.GetVariable("ENGINEER")` etc. — the assembly is already referenced
in `Patches/DialoguePatches.cs`), bucket with the same thresholds
(`<0→-1, <1→0, <2→+1, <3→+2`), and match against the skill name already read by
`CollectSkillLine()` (`Describe.cs` lines 163–179). No color heuristic, no
render-timing race, no per-instance calibration needed — this is a global,
always-current value independent of which action card is on screen.

**RISK**: low; purely additive read, easy to fall back to the current heuristic
if the Lua variable is missing/nil (`storeStringResult` showed `'nil'` in the
census for an un-set case, so nil-handling is required).

**PRIORITY**: high — resolves the single most-flagged unresolved calibration
item in the build plan, and eliminates a heuristic that can silently return "no
modifier" whenever the row doesn't split cleanly into one-vs-rest.

### 3. Status queries (C/K) and cycle number: authoritative source is the same Lua layer, one hop upstream of the HUD FSM locals

**CURRENT**: `mod/src/Game/GameQueries.cs` `MetersBrief()` (lines 306–326) linear-
scans `PlayMakerFSM.FsmList` via `FindFsm("Energy Bar System", "Energy UI")` and
`FindFsm("Condition System", "Energy UI")`, then reads their local
`Player Energy`/`Player Condition` floats and per-band string vars
(`ConditionWord`, lines 328–341). The code comment (lines 301–305) already
correctly distinguishes these from the Cycle Controller's same-named locals
(transient) but treats the HUD widget's copy as the ground truth.

**WIRING**: `Energy Bar System`'s `Energy Getter` state runs, every frame:
```
GetVariable  variableName=Player_Energy  storeFloatResult=$Player Energy  everyFrame=True
FloatChanged $Player Energy -> event:UpdateBar
```
i.e. the HUD widget is itself just a live mirror of the Dialogue System Lua
variable `Player_Energy`. Same pattern confirmed for `Player_Condition` in
`Condition System`, and for `Cycle` in `Cycle Controller`'s `Tick Cycle` state
(`GetVariable variableName=Cycle storeFloatResult=$Cycle Count`, then
`SetVariable variableName=Cycle floatValue=$Cycle Count` after incrementing).
Also present in the same Lua layer: `DeathCycle`, `BREAKDOWN_CYCLE`,
`ENDURE_BROKEN`, `ENDURE_PERKS`, `MAYWICKCYCLESCENE`, `CycleSceneCount`.

**VERDICT**: HYBRID. The current HUD-FSM read is *not wrong* (zero lag, same
value) — but it costs an `FsmList` scan (build-plan Phase D flags "FsmList scans
per tick" as a perf concern) and only works while that widget GameObject is
active in the hierarchy. Reading `DialogueLua.GetVariable("Player_Energy")` /
`("Player_Condition")` / `("Cycle")` directly is O(1), needs no scene traversal,
and is exactly as current (the FSM itself polls it every frame). This also
answers the build-plan §7 parked question ("Cycle number... Dialogue System Lua
variable is the lead candidate") — confirmed, not just a lead. Worth adding
`Cycle` to the K or C readout now that the source is corpus-proven.

**RISK**: low — additive/parallel read; keep the FSM path as fallback in case a
save has stale Lua state before the widget's first `Energy Getter` tick.

**PRIORITY**: high — direct perf win on the every-tick path, plus unblocks a
parked feature (cycle number) with a confirmed source instead of a guess.

## P2 — architecture-level: polling vs. FSM state-entry hooks, and a whole missing scope

### 4. The 0.4s Watchers poll is solving a problem PlayMaker already announces via named state entry

**CURRENT**: `mod/src/Watchers.cs` `Tick()` (lines 21–55) runs every mechanism
below off one 0.4s timer (`Interval`, line 13), each re-scanning
`PlayMakerFSM.FsmList` and diffing `ActiveStateName` against a cache:
`CheckActionOutcomes` (284–325), `CheckDiceAllocation` (338–398),
`CheckCycleTransition` (482–510), plus CanvasGroup-alpha polls for
`CheckCharacterWindow` (440–471) and `CheckDriveLog` (408–431).

**WIRING**: every one of these has a *named* state the game itself enters at the
exact right moment — confirmed states, not inferred:
- Action Controller: `Positive Outcome` / `Neutral Outcome` / `Negative Outcome`
  (confirmed in `action_controller.jsonl`, matches the mod's existing switch).
- Dice Gamepad System: `Off` / `Active` / `Slotted` / `Reselector`.
- Cycle Controller: `Idle` and the ~60-state pipeline documented in #3's corpus
  dump (leaves `Idle` via `EndCycle -> Cycle`, returns via the pipeline's tail).
- Character UI Button: `Idle` (closed) → `Open` event → `SFX` → `Open` state
  (window visible) → `Open` event again → `Close` → `Reset` → `Idle`. This is
  already corpus-verified and used by `InputManager.cs` lines 194–198 for the
  Backspace handler — but `Watchers.CheckCharacterWindow` still polls
  CanvasGroup alpha instead of this same FSM's state.
- Drive Log Button (`drive_log_fsm.jsonl`): **identical** template to Character
  UI Button — `Idle`/`Highlight`/`Open`/`Close`/`Gamepad Checker`/`Gamepad UI`
  states, same shape. `Watchers.CheckDriveLog` polls CanvasGroup alpha on
  `CS Drive Log` instead of this button FSM one hop away.

A Harmony postfix on PlayMaker's state-switch entry point (filtered by owner
GameObject name + target state name) would fire once, exactly on transition,
for all of these — no 0.4s latency, no diff-cache bookkeeping, no
`FsmList`-wide scan every tick, and it structurally can't hit the P0 dead-code
class of bug (a hook on a state that doesn't exist fails loudly at dev time
instead of silently never firing).

**VERDICT**: REPLACE (staged). This is a genuine architecture change, not a
one-line fix — recommend building the generic "Fsm state-entry hook" adapter
once (per build-plan §3's "Substrate adapters" layer) and migrating
`CheckActionOutcomes`, `CheckDiceAllocation`, `CheckCharacterWindow`, and
`CheckDriveLog` onto it as the first four clients, keeping `Tick()`'s 0.4s poll
only for mechanisms with no FSM signal (see P4).

**RISK**: medium — Harmony-patching PlayMaker's internal state-switch method is
more invasive than anything the mod currently does (all existing patches target
game-specific MonoBehaviours or Dialogue System, not the PlayMaker library
itself); needs a live spike to confirm the right patch point
(`HutongGames.PlayMaker.Fsm.SwitchState` or equivalent) and that it doesn't
fire during FSM setup/teardown in surprising ways. Should be validated on one
mechanism (recommend #1's dice-slot fix, since its correct target state is now
known) before generalizing.

**PRIORITY**: high — this is the structural fix behind the "repeatedly coming in
here and spot checking failures" pattern the owner named: every poll-plus-cache
mechanism is a fresh opportunity for the exact class of bug found in #1.

### 5. Tab/K/action-cycling only ever look at the station action tree; the cloud/hacking tree is a full parallel scope

**CURRENT**: `mod/src/Game/GameQueries.cs` `GetActionPanels()` (160–176) and
`GetClockPanels()` (178–194) both hardcode
`GameObject.Find("Letterbox Canvas/1_Action Groups")` as their only root.
`InputManager.CycleActions` (Tab, 296–313) and the K query both depend on these.

**WIRING**: `Letterbox Canvas/2_Hacking Action Groups` exists as a full sibling
tree — 260 FSMs (`Letterbox Canvas/1_Action Groups` has 2,992) — with the same
`" Action"`-suffix convention on most entries (`Yatagan Agent 1 Action`,
`Flux Node 1 Action`, `ConSec S1 Hack Action`, ...), though not perfectly
uniform (found one `Hardin Agent 2 Action slot (1)` that doesn't end in
`" Action"` — the suffix-match in `Describe.FindActionRoot` (lines 138–144)
would miss it). The corpus also confirms a designed "current target" anchor for
this scope: the global variable `$ActiveAction` is written 227 times, exclusively
by `Location Button` FSMs under `ERLIN MAIN/1_Station UI/Hacking UI/*` on their
`Active` state (`SetGameObject variable=$ActiveAction`) — never by anything in
the station action tree. This is the cloud-mode equivalent of "what's focused
right now," unused by the mod (S/Tab/K only target station paths; cloud mode
isn't built yet per build-plan Phase C).

**VERDICT**: HYBRID. Short-term: make `GetActionPanels`/`GetClockPanels` try
`2_Hacking Action Groups` when `1_Action Groups` yields nothing (or based on
which root is active), so Tab/K at least don't go silent while in cloud mode.
Full cloud support (node targeting via `$ActiveAction`, scan-key behavior) is
its own Phase C item per the build plan, not a quick patch — flagging the
anchor here so that work starts from the corpus-verified global instead of a
live probe.

**RISK**: low for the short-term fallback; the full cloud mode is new surface
and needs its own live validation pass.

**PRIORITY**: medium-high — cloud/hacking is a real, frequently-visited mode
where the mod currently offers no Tab/K coverage at all (worse than a
heuristic: total silence).

## P3 — validated opportunities, lower urgency

### 6. Leave Button hardcode matches a static global anchor (low-value migration)

**CURRENT**: `InputManager.cs` `ClickFirstActive("Leave or back",
"Letterbox Canvas/Top UI/Leave Button", ...)`, lines 199–203.

**WIRING**: `$Leave Button` is set exactly once, station-wide, by a `Variable
Setter` FSM on `Letterbox Canvas` (`SetGameObject variable=$Leave Button
gameObject=GameObject:Leave Button`) — a genuine anchor, matching the mod's
hardcoded path exactly. It's then referenced 2,508 times across the corpus
(nearly every Action Controller/Cycle Controller/Gamepad Dice Slot state that
needs to hide/show it during actions, dice-slotting, and cycle transitions) —
confirms why it needs to be findable everywhere, and confirms the hardcoded
path is correct and stable.

**VERDICT**: KEEP as-is; optionally read the anchor instead of the literal path
for resilience to a future path rename, but there is no live-observed failure
class this would fix.

**RISK**: negligible either way. **PRIORITY**: low.

### 7. Input Pauser: confirmed pure Rewired controller-map swap, not a generic block

**CURRENT**: `GameQueries.InputPaused()` (lines 67–73) treats any non-
`"UNPAUSED"` state as "swallow all game-facing keys except the tutorial
continue," per `InputManager.cs` line 54 and the `TutorialContinueFocused()`
carve-out (237–245).

**WIRING**: `Input Pauser`'s `PAUSED` state does exactly two things:
```
RewiredPlayerSetControllerMapsEnabled  enabled=False  layout=Default
RewiredPlayerSetControllerMapsEnabled  enabled=True   layout=Pause
```
(`UNPAUSED` does the reverse.) This is corpus-proof that the existing code
comment ("it's a Rewired remap, not a block") is correct — during `PAUSED`,
whatever Rewired actions are bound in the `"Pause"` controller-map layout stay
live; only `"Default"`-layout actions go dark. The FSM census can't show what's
bound in the `Pause` layout (that's Rewired's own asset, not PlayMaker data).

**VERDICT**: NEEDS-LIVE-CHECK. The current behavior (swallow everything except
the one hardcoded tutorial-continue carve-out) is a reasonable approximation but
may be over-blocking if the `Pause` layout binds anything beyond continue (e.g.
a skip-intro action). Needs a bridge probe of the Rewired `Pause` layout's
action bindings to know definitively what should pass through.

**RISK**: low to check, but changing behavior here risks reopening the intro-
tutorial-hang class of bug the current conservative swallow was built to avoid
— validate live before loosening it.

**PRIORITY**: medium — not currently causing failures (it's conservative by
design), but worth closing out since the exact designed behavior is now half-
confirmed.

## P4 — confirmed KEEP: no better wiring exists

### 8. Character notifications: polling + CanvasGroup alpha is the only available signal

**CURRENT**: `Watchers.CheckNotifications()` / `EffectiveAlpha()`, lines 159–199.

**WIRING**: the entire `Character Notifications` subtree carries exactly **one**
FSM in the whole station corpus — a startup dispatcher (`INIT -> Visible`, no
further logic) directly on the root. No per-notification-item FSM exists
anywhere to hook. This isn't a hard-to-find signal; there structurally isn't one
to find under this system.

**VERDICT**: KEEP. **RISK/PRIORITY**: n/a — nothing to change.

### 9. Clock reads (K query): variable names and per-instance locality both confirmed correct

**CURRENT**: `GameQueries.ClockProgress()`, lines 218–239, reads each clock's own
`"N Step Clock"` child FSM's `ClockValue`/`Positive?`/`Cycle Clock?` locals.

**WIRING**: confirmed exact variable names across all 2,043 step-clock FSMs
(8 template variants, all carrying `ClockValue`, `Positive?`, `Cycle Clock?`).
Each clock also carries a `Clock Variable` string field naming *its own*
Dialogue System Lua/quest variable — but that name differs per clock instance,
so there's no single universal Lua variable to shortcut through (unlike #2/#3);
reading each clock's own FSM locals, as the mod already does, is the correct
and only generic route.

**VERDICT**: KEEP. **RISK/PRIORITY**: n/a.

### 10. Station action-root convention (`FindActionRoot`'s `" Action"` suffix walk)

**CURRENT**: `Describe.FindActionRoot()`, lines 138–144.

**WIRING**: no `$ActiveAction`-style "current action" anchor exists anywhere in
the station scope (`1_Action Groups`) — that global is written exclusively by
the Hacking UI (see #5). For station, the name-suffix walk is the only
available route and matches the convention on all 2,992 station action FSMs
sampled.

**VERDICT**: KEEP for station scope; extend to hacking scope per #5.

## Surprises

- The modifier row has **zero** PlayMaker presence anywhere in the 4,948-FSM
  corpus — not merely hard to read, structurally absent. The right fix isn't a
  better visual heuristic; it's bypassing the visual layer for the Lua skill
  variable that actually drives it (#2).
- The `"Slotted"` state the dice-commit code has been chasing since triage
  report 13 does not exist under that name on any of the 218
  `Gamepad Dice Slot` FSMs. The design intuition (a definite per-slot commit
  state exists) was right; the identifier was wrong, and no amount of live
  spot-checking would have surfaced this as fast as the corpus grep did.
- Cycle Controller's `Player Energy`/`Player Condition`/`Cycle` locals — which
  build-plan §1 flags as "transient, only during the pipeline" — turn out to be
  literal every-state-entry mirrors of Dialogue System Lua variables of nearly
  the same name (`Player_Energy`, `Player_Condition`, `Cycle`). The HUD widgets
  mirror the *same* Lua variables every frame. The Lua layer, not any FSM, is
  the actual single source of truth station-wide.
- Two ending controllers (`Ending Controller`, `Flux Ending Controller`) exist
  at the top level of the scene, confirming build-plan Phase D's note; still
  entirely unmapped by the mod.
- `Letterbox Canvas/2_Hacking Action Groups` is a full 260-FSM parallel tree to
  the 2,992-FSM station tree, invisible to every station-scoped query (#5).
- The build-plan's "~600 Checker watchdog FSMs" figure understates the pattern:
  `Checker`/`Gamepad Checker` isn't a fixed set of ~600 distinct objects, it's a
  recurring **state name** baked into dozens of different templates (Dice Slot
  Button, Skill Icon, Scan Button, Character UI Button, Drive Log Button, ...),
  always the same shape: `BoolTest $Gamepad every-frame -> reassert selection`.
  Worth remembering as a single idiom, not a list of special cases, if a
  generic Harmony hook is ever built for it.

## New capabilities the wiring enables that the mod doesn't attempt yet

- Direct Dialogue System Lua reads (`DialogueLua.GetVariable`) for
  `Player_Energy`, `Player_Condition`, `Cycle`, and the five skill variables
  (`ENGINEER`/`INTERFACE`/`ENDURE`/`INTUIT`/`ENGAGE`, each with `_PERKS`/
  `_BROKEN` companions) — O(1), no scene traversal, always current.
- `$ActiveAction` + Hacking UI `Location Button` `Active` state as the designed
  "current cloud node" pointer, ready for cloud-mode node targeting/scan once
  that mode is built.
- Character Window `SKILL List/<Skill>` FSMs' `ActiveStateName`
  (`-1`/`0`/`+1`/`+2`/`Broken`/`BROKEN`) as an exact, non-heuristic skill-rating
  read — usable for both the character window's own skill rows (a parked
  design question) and the action-card modifier row (#2).
- A generic FSM state-entry Harmony hook (once built for #1/#4) would cover
  action outcomes, dice commit, character/drive window open-close, and cycle
  transition boundaries from one mechanism instead of five separate pollers.
