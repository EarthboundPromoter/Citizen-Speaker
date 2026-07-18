# A — Action-resolution pipeline: static verification

Static source verification of Citizen Sleeper's `level1` scene data (game not running).
Scope: the four questions in the brief — clock/action tie, outcome-narrative render
route, durable slot-commit signal, end-cycle state sequence. Method notes and raw
script output are cited by path; no Ink/dialogue/outcome prose is quoted anywhere
below (spoiler rule) — only object paths, FSM state/event/variable names, component
class names, and rendered UI *labels* (button/field captions), never body text.

## Method note (read this before the evidence — it shapes what's citable)

`tools/analysis/README.md` already flags that PlayMakerFSM blobs need
TypeTreeGeneratorAPI for a full structural parse, and that ASCII-run string
extraction is the fallback. This session confirms the fallback is not optional —
it is the **only working method** for FSM interiors here. `read_typetree()`
(both the C-boost path and the pure-Python fallback, tried explicitly) throws
`EOFError: read_str out of bounds` deep inside `Fsm.states[].actionData` for
*every* PlayMakerFSM component tested: Action Controller (station and hacking),
Cycle Controller, Dice Gamepad System, Gamepad Dice Slot, Dice Cursor 1, and the
clock dial FSMs. The generated typetree's header fields (`m_GameObject`,
`m_Script`, `m_Name`, `Fsm.dataVersion/name/startState`, per-state
`name/description/position/transitions`) are all correctly shaped — the crash is
specifically inside the per-state `ActionData` parallel-array block (custom
PlayMaker action parameters: `unityObjectParams`, `fsmGameObjectParams`,
`fsmOwnerDefaultParams`, etc.), which this game's PlayMaker build apparently
serializes in a shape TypeTreeGeneratorAPI's Cecil-based reconstruction doesn't
reproduce byte-for-byte. Confirmed with a full traceback and by disabling the
boost reader — the pure-Python path fails at the same logical point, ruling out
a boost-specific bug.

**Consequence:** everything below about FSM interiors (states, events,
variable *names*) comes from noise-filtered ASCII-run extraction over the raw
component bytes (same technique as `tools/analysis/dump_state_map.py` /
`census.py`), not from a structural parse. This recovers state/event/variable
**names** reliably (they're plain UTF-8 strings in the blob) but not their
**values** (e.g., which specific clock GameObject a "Clock Identifier" FsmGameObject
variable actually points to) — those are PPtr fields inside the same corrupted
block. Where I needed a value or a component's exact script class rather than a
name, I used `ObjectReader.parse_monobehaviour_head()` (reads only the
MonoBehaviour header + script PPtr, bypassing the broken FSM body) — this is
fully reliable and is how the TMP/LocalizeUI class names below were confirmed.
GameObject hierarchy, transform trees, and Button `m_OnClick` persistent calls
(the proven `dump_endcycle_wiring.py` method) also parse cleanly — the bug is
specific to `ActionData`.

New scripts (scratchpad, not committed): `census_action_groups.py` (per-location
action/clock sibling census), `action_controller_strings.py` (per-action FSM
string census with clock/dialogue/outcome-text token flags), `dump_action_hierarchy.py`
(full subtree + component-class dump for one action), plus a Cycle Controller
string dump. Full output retained at
`tools/analysis/scratch_out/{action_groups_census.txt, action_controller_strings.txt,
manual_salvage_hierarchy.txt, cycle_controller_strings.txt}` if you want to grep further.

---

## EVIDENCE

### Q1 — clock/action tie

**Schema level (every action, no exceptions):** ASCII-run extraction of all 193
"Action Controller" FSM components found under `1_Action Groups`, plus all
sampled ones under `2_Hacking Action Groups`, show the *same* clock-variable
slot vocabulary in 100% of cases: `Optional Clock Identifier`,
`Clock Complete Variable`, `Clock Complete?` (single-clock actions) or
`+ Positive Clock Identifier` / `Positive Clock Complete Variable` /
`Positive Clock Complete?` / `- Negative Clock Identifier` /
`Negative Clock Complete Variable` / `Negative Clock Complete?` (dual-clock
actions). `183/183` station Action Controllers and `10/10` sampled hacking
ones carry these tokens (`tools/analysis/scratch_out/action_controller_strings.txt`,
summary lines). This is a shared prefab template's variable *schema* — it
proves every action has a clock-wiring slot available, not that the slot is
populated for that specific action (the PPtr *value* behind
`Optional Clock Identifier` is inside the corrupted `ActionData` block and
unreadable statically).

**Spatial/UI-hierarchy level:** `mod/src/Game/GameQueries.cs` (`GetActionPanels`/
`GetClockPanels`, live-verified code already in the mod) discovers clocks as
GameObjects ending in `" Clock"` that are *siblings* of `" Action"` objects
inside the same `<Location> Actions` group under `Letterbox Canvas/1_Action Groups`.
Census of that structure (`action_groups_census.txt`) over all 1_Action Groups
children:
- 181 action objects, 146 clock objects, across ~180 location groups.
- **41 groups have one or more actions but zero clocks** (e.g. `Overlook Bar
  Actions`, `Climbing Briar Actions`, `Docking Structure Actions`) — actions
  with no co-located clock at all.
- **35 groups have one or more clocks but zero actions** (e.g. `The Ambergris
  Actions -1`, `Sealed Dock Actions`, `Havenage Cordon Actions`) — clocks with
  no local action to have ticked them.
- `2_Hacking Action Groups`: **35 actions across 40 groups, 0 clocks anywhere**
  — not one `" Clock"`-suffixed sibling in the entire cloud/hacking action
  tree (all 40 groups are template-pool objects, `m_IsActive=false` until a
  hack starts, per the existing `ui-state-map.md` note; the zero-clock finding
  holds regardless of active state since it's a structural census).

**The per-action global-variable mechanism (the real tie, not spatial):** the
FSM string census recovers, per action, a distinctive pair of fields —
`Variable to Affect` / `Variable to Affect 2` — with values that are
**global PlayMaker variable names built from the action's own identifier**,
e.g. `C_AXISSUPPLY` (on `Supply Spores Action`, identifier `AXISSUPPLY`),
`C_SHIPYARDRIP_CYCLE_SWITCH` / `C_FENGSPROGRESS` (on hacking actions under
`Port H33 Actions` / `ConSec Port S Actions`), `GATES4_COMPLETE` /
`GATES7_COMPLETE` (on cipher-slotting hacking actions). These are **global**
variables (PlayMaker's global-variable pool, confirmed present as a
`PlayMakerGlobals` asset in `resources.assets`; `SlottedDiceGlobal` and
`SlottedDiceValueGlobal` also live there and are referenced 2168 and 428 times
respectively across `level1`) — not scoped to any location. A hacking action
under `2_Hacking Action Groups` (physically nowhere near a station location
canvas) can and does write to a `C_`-prefixed global that a station-side clock
widget elsewhere reads to render its fill. One sampled action
(`Slot Havenage Cipher Action`) has *both* `Variable to Affect` fields present
but **no assigned string next to them** — i.e., that specific action's
progress-variable slot is unpopulated, a direct instance of "schema present,
value absent."

**Drive clocks:** `Letterbox Canvas/Drive System` (full subtree in
`action_groups_census.txt`) contains `Drive Tracker HUD/.../Quest Track
Template` and `CS Drive Log/Quest Log Window Main Panel` — PixelCrushers
Dialogue System's quest/track window (matches the already-documented DS
integration in `citizen-sleeper-a11y-architecture.md`), entirely disjoint
from both `1_Action Groups` and `2_Hacking Action Groups`. Drives are not
represented as `" N Step Clock"` dial objects at all; they're DS quest-log
entries with their own tracked/untracked state (already the subject of
triage report 18). There is no location-group co-location to test for drives
because they don't live in the location-group tree in the first place.

### Q2 — outcome-narrative render route

Full subtree dump of `Manual Salvage Action` (`Dragos's Yard`,
`manual_salvage_hierarchy.txt`) plus targeted `parse_monobehaviour_head()`
script-class lookups (bypasses the broken FSM body, reliable):

- The action card carries its own `OUTCOMES` child with four sub-panels:
  `Positive`, `Neutral`, `Negative` (each: `RectTransform, MonoBehaviour,
  Animator, CanvasGroup` — no FSM of its own), and `PREDICTIVE` (which *does*
  carry its own PlayMakerFSM, `Animation CG` sub-object). Each of
  `Positive/Neutral/Negative` has an `Outcome Type` object and one or more
  `Image (1)/Effect N` children (`Effect 1`, `Effect 2`, `Effect 3`, the last
  inactive by default on this action).
- **Confirmed by direct script-class resolution:** `Outcome Type` is
  `TMPro.TextMeshProUGUI` **+ `PixelCrushers.Wrappers.LocalizeUI`**. `Effect 1`
  is `TMPro.TextMeshProUGUI` (a real text label, not an icon/image as the
  generic component census alone would suggest).
- The Action Controller FSM's own string pool (every action, station and
  hacking alike) contains a matched family of tokens: `textTable`, `field`,
  `Text Setup`, `Outcome Setup`, and outcome-tier-specific key/label pairs —
  `NEU_OUTCOME1` / `Neutral Outcome Field 1`, `NEU_OUTCOME2` / `Neutral
  Outcome Field 2`, `NEU_OUTCOME3` / `Neutral Outcome Field 3` (and the
  `POS_`/`NEG_` equivalents — `"Positive Outcome"`/`"Negative Outcome"` occur
  238 times total across the sampled Action Controllers, confirming the full
  three-tier vocabulary exists even though one sampled action only showed the
  neutral-tier set). Same pattern for the card's other localized fields:
  `NAME`/`Action Name Field`, `DESC`/`Action Description Field`, `INPUT`/
  `Item Display Name Field`. No GameObject named `"Outcome Field"`,
  `"NEU_OUTCOME1"`, or similar exists anywhere in `level1` — confirming these
  are **table lookup keys**, not object names; the `LocalizeUI` wrapper on
  `Outcome Type` (and, by the same wiring pattern, presumably on the `Effect N`
  labels) is what turns a key into rendered text via the Dialogue System's
  text-table/localization pipeline (`PixelCrushers.Wrappers.LocalizeUI` is a
  DS-namespaced component — this is the same subsystem
  `citizen-sleeper-a11y-architecture.md` already confirmed live as the sole
  narrative-rendering path for regular dialogue).
- The FSM string pool also includes `gameObject, activate, recursive,
  resetOnExit` immediately adjacent to the clock-variable tokens — these are
  PlayMaker's built-in "Activate Game Object" action's parameter names,
  consistent with the Action Controller's outcome state directly
  `SetActive`-ing the matching `OUTCOMES/<Tier>` panel (an FsmGameObject
  target, unreadable as a value, but the mechanism is legible from the
  parameter names alone).
- State names `"Idle"`, `"Slotted"`, `"ActionStart"`, `"Working"`,
  `"Positive Outcome"`, `"Neutral Outcome"`, `"Negative Outcome"` all recur
  verbatim in the Action Controller's own string pool, matching
  `mod/src/Watchers.cs`'s `CheckActionOutcomes()`, which already switches on
  exactly `"Positive Outcome"|"Neutral Outcome"|"Negative Outcome"` as
  `ActiveStateName` (that code is live-verified, not just static — it's the
  existing mod behavior producing today's "MANUAL SALVAGE: neutral outcome."
  utterance per triage report 22).
- No dedicated "outcome popup"/"result window" GameObject exists anywhere in
  `level1` outside the action card itself (name-scanned for `outcome`/`result`/
  `notification` — the only hits are `OUTCOMES`/`Outcome BG`/`Outcome Type`,
  all on-card, plus an unrelated per-inventory-item `"Change Notification"`
  popup under `Bottom UI/Inventory` that has nothing to do with actions).

### Q3 — durable commit signal

- `mod/src/Watchers.cs` `CheckDiceAllocation()` (live-verified, current mod
  code) already polls `PlayMakerFSM.FsmList` for GameObjects named
  `"Gamepad Dice Slot"` and treats `ActiveStateName == "Slotted"` as the
  commit signal — exactly the transient state the brief describes as missed
  by the 0.4s poll (comment in source: *"A slot FSM reaching Slotted is the
  commit signal"*).
- Static confirmation: the Action Controller FSM's **own** string pool (not
  just the separate slot-picker FSM) also contains `"Slotted"` as a state
  name, alongside `"Idle"`, `"ActionStart"`, `"Working"`, and events
  `"DiceSlotted"`, `"SlottedDice"`, `"ActionComplete"`/`"Action Completed"`.
  This means the Action Controller itself transitions Idle → (something) →
  ActionStart → Working → outcome-state on a die commit — a second, parallel
  FSM going through a **durable** sequence (Working plays its own FMOD cue,
  `event:/UI Sounds/Action Working`, implying a non-instant hold) driven by
  the same commit event.
- `SlottedDiceGlobal` and `SlottedDiceValueGlobal` are both confirmed real,
  heavily-referenced PlayMaker **globals** (2168 and 428 raw-string hits in
  `level1`; both also appear once in the `PlayMakerGlobals` asset in
  `resources.assets`, i.e. that's where they're canonically declared).
  `ui-state-map.md` already notes `SlottedDiceGlobal` is read by the Leave
  Button FSM (gates leaving while a die is slotted).
- Values behind any of these (the *actual* new state name, or the actual new
  global value) are not staticaly readable — the `ActionData`/global-variable
  value blocks are exactly what the typetree bug corrupts. This section is
  therefore state/variable **names**, confirmed to exist and be exercised;
  not runtime values.

### Q4 — end-cycle sequence

Full ASCII string census of the `Cycle Controller` FSM (scene root; 291
strings after noise filtering — `cycle_controller_strings.txt`; previously
`ui-state-map.md` only had the variable names for this FSM, not a state
census). Read in extraction order (which for a hand-authored FSM tracks
declaration/editorial order, not guaranteed execution order, but is
suggestive and internally consistent with the sequence triage report 3
observed live):

- `"Idle"` with an `"EndCycle"` transition (matches the already-confirmed
  `Dice Slot Button` → `SendEvent("EndCycle")` wiring from
  `dump_endcycle_wiring.py`/`resolve_sendevent.py`, and `ui-state-map.md`
  section 6).
- UI teardown tokens immediately after: `"Stored Home Cam"`, `"Home Camera"`,
  `"Stored Home Actions"`, `"Home Actions"`, `"Leave Button"`, `alpha, Fade,
  blocksRaycasts` — matches triage report 3's live-observed "flurry": the
  transition fades/deactivates the home location's action groups
  station-wide (`"Action Group"` / `"Home Actions Parent"` also appear later
  in the pool).
- Stat/dice rollover block: `"Condition Checker"`, `"PERK CHECK"`,
  `"Player_Condition"`/`"Player Condition"`, `"Player_Energy"`/`"Player Energy"`,
  `"DieCondition"`, `"RollDice"`, `"Tick Cycle"` (plausibly where Cycle Count
  actually increments), followed by a per-character scene-complete check
  (`"Character Name 1..~20"`, `"SceneComplete"`, `"Character Scene Complete
  Variable"` — story-scene gating, names withheld here per spoiler
  discipline).
- `"Roll Cycle Changes"`, `"Refresh End Cycle Bool"`, `"Check Variables"`,
  `"Energy Checker"`, `"Starving"`, `"Regular Depletion"`, `"Home Check"`,
  **`"Update Clocks"`**, `"Cycle Count"`/`"add"` — an explicit
  `"Update Clocks"` state exists in the Cycle Controller itself, separate
  from any Action Controller. This is direct confirmation that **some**
  clocks tick on the cycle boundary rather than (or in addition to) on
  action resolution — a second, cycle-driven clock-progression path
  independent of the per-action `Variable to Affect` mechanism in Q1.
- Branch check: `"Cycle Scene?"`, `"Outcome Animation Checker"`,
  `"Scene Trigger"`, `"Scene Delay"`, `"CycleSceneCount"` — whether a
  scripted cutscene plays this cycle.
- `"CycleCanEnd"` appears **inside the Cycle Controller's own string pool**,
  not only on the Intro Sequence FSM as `ui-state-map.md` §2 currently
  states — see SURPRISES.
- Visual/audio: `"Music Trigger"`, two `"Get Light State + Swap"` blocks
  (`Light 1..4`, `"LightCycle"`) — day/night lighting swap, cosmetic.
- `"Action Group"`, **`"Cycle Clock Group"`**, `"Load From Save"`,
  `"PERK CHECK 2"`, `"Die 1"`.."Die 5"", `"Die Condition"` — a
  `"Cycle Clock Group"` refresh alongside the new cycle's dice being
  (re)rolled/reset.
- Depletion-tier check: `"Basic"`, `"Comfy"`/`"Comfy Depletion"`,
  `"Stable"`/`"Stable Depletion"`, `"Global Home Grade"` — home-quality-gated
  depletion amount.
- `"Reset 1 PERKED"`, `"Perk"`/`"No Perk"`, `"ENDURE_BROKEN"`,
  `"ENDURE_PERKS"`, `"Broken"`, `"Hold on?"`, then **`"REVEAL DICE!"`** —
  a distinctly-named state, the newly-rolled dice becoming visible/available.
  This reads as the natural announcement point for "new cycle, N dice
  rolled."
- Edge branches further down: `"Debug Death Cycle"`, `"Death Cycle 1"`,
  `"Death Cycle 2"`, `"DeathCycle"`, `"Death"`, `"Break Reset"`,
  `"Breakdown Reset"`, `"BREAKDOWN_CYCLE"`, `"Breakdown Cycle"` — special
  end states for the (rare) death/breakdown cycle path, not the normal loop.
  A per-storyline `"Scrap Clock"` and `"MaywickShot"`/`"C_MAYWICKSHOT"` also
  surface here — more evidence of cycle-driven, per-story global clock
  variables outside the per-action mechanism.
- `"Intro Complete"`/`"Intro Complete?"` also recur here, consistent with
  `ui-state-map.md`'s existing claim that this FSM holds a readable copy of
  that flag.

---

## VERDICTS

**Report 23's presumption — "clock and action are always tied together by the
location" — REFUTED as a co-location claim, PARTIALLY CONFIRMED as a
schema/data claim.**
Every Action Controller (station and the hacking/cloud sample, 193/193) has
the same clock-variable *slots* in its FSM schema (report 11's original
finding, now independently reproduced) — so at the template level, yes, every
action *can* be wired to a clock. But the census directly falsifies "by the
location": 41 station location-groups have actions with zero co-located
clocks, 35 have clocks with zero co-located actions, and the entire
`2_Hacking Action Groups` tree (35 actions, 40 groups) has **no clock
siblings anywhere**, while its Action Controllers still carry per-action
`C_`-prefixed **global** progress variables tied to the action's own identity,
not its physical location. Drive clocks aren't in this tree at all — they're
a separate PixelCrushers quest-log subsystem. The real tie (where one exists)
is a global-variable name keyed to the *action*, not a location; whether a
given action's slot is actually populated (vs. left null, as directly observed
on one sampled action) is not statically readable and needs a runtime check.
**Design implication for report 23:** don't derive "the clock this action
moved" from what's visually co-located at the current location — that's
provably wrong for hacking actions and unreliable for ~40 station groups. If
a live per-action clock read is wanted, it needs the FSM's actual
`Clock Identifier` variable value (or `Variable to Affect`) read at runtime,
not a UI-hierarchy walk.

**Report 22's presumption — outcome narrative renders somewhere findable —
CONFIRMED location, UNRESOLVED whether it's live-readable as expected.**
The render target is very likely the action card's own `OUTCOMES/<Positive|
Neutral|Negative>/.../Effect N` `TextMeshProUGUI` objects (real text labels,
confirmed by direct script-class resolution, not the on-card *preview*
graphic alone as report 11 assumed) populated via a PixelCrushers
`LocalizeUI`/text-table lookup keyed by strings like `NEU_OUTCOME1..3` found
in the FSM. This is a *different* route than the mod's existing
`StandardDialogueUI.ShowSubtitle`/`ShowResponses` Harmony patches
(`DialoguePatches.cs`) — those won't catch it. No separate "outcome
popup"/dialogue-routed narrative panel exists in the static scene. This needs
one live check next session (open an action card post-resolution, read the
revealed tier's `Effect N` text) before building a hook, but the *where* is
now well-evidenced, not a guess.

**Report 13's fix lead — REFUTE the raw poll, CONFIRM the two candidate
durable signals both exist and are real.** `SlottedDiceGlobal` and
`SlottedDiceValueGlobal` are confirmed real, heavily-used PlayMaker globals.
Separately, and not previously documented: the Action Controller FSM itself
(not just the slot-picker FSM the mod currently polls) has its own durable
`Idle → ... → ActionStart → Working → <tier> Outcome` sequence with a
distinct FMOD cue on `Working`, suggesting a non-instant hold — a second,
independently viable hook target.

**Report 3/9's implicit end-cycle-summary ask — CONFIRMED a real, hookable
state sequence exists.** The Cycle Controller's `EndCycle` transition runs
through a long, well-defined pipeline (UI teardown → stat/dice rollover →
per-character scene check → `Update Clocks` → cycle-scene branch → visual
swap → `Cycle Clock Group` refresh + dice reroll → depletion tier → perk
check → `REVEAL DICE!`) with no ambiguity about state *names*, though state
*order* is inferred from extraction order, not confirmed execution order.

---

## OPEN DESIGN QUESTIONS (for the owner, not decided here)

1. Is the `OUTCOMES/<Tier>/Effect N` text genuinely the *narrative* the owner
   means in report 22, or is it a short mechanical effect line (e.g. "+1
   Cryo") distinct from prose narrative delivered elsewhere? Static evidence
   only proves it's real, localized, per-tier text on the card — not its
   register or length. Needs one live look.
2. For report 23's clock-diff idiom: given the clock tie is per-action-global,
   not per-location, should the mod read the *specific* global named in that
   action's `Variable to Affect` (requires a runtime FSM-variable read per
   action, more invasive) or fall back to the current location-based
   `GetClockPanels()` walk and accept it will sometimes miss/misattribute
   (e.g., for every hacking action, always)?
3. For report 13: state-entry hook (Action Controller leaving `Idle`, or
   entering `ActionStart`/`Working`) vs. global-variable-change hook
   (`SlottedDiceValueGlobal`)? Both are now confirmed real and Harmony-viable;
   this doc doesn't pick one — see recommendation lean below, owner's call.
4. Cycle Controller's exact state *order* (as opposed to state *names*, which
   are solid) is inferred, not proven — worth one live trace (StateChanged
   log for one full EndCycle) before hard-coding a hook sequence for a
   designed summary announcement.

**Lean (not a decision):** of the two Q3 candidates, an FSM state-entry hook
on the Action Controller (leaving `Idle`) reads as more robust than polling
or hooking a global-variable write — it fires exactly once per real
GameObject with the action's own transform directly in hand (no separate
lookup back from a global value to "which action"), and the mod already
polls `PlayMakerFSM.FsmList`/`ActiveStateName` on this exact GameObject in
`CheckActionOutcomes()`, so a state-entry Harmony hook (rather than a second
poll) would sit naturally alongside existing code rather than introducing a
new subsystem.

---

## SURPRISES (where source contradicts current docs)

- **`ui-state-map.md` §2 says `CycleCanEnd` is "a variable on the `Intro
  Sequence` FSM."** Static extraction shows `CycleCanEnd` also appears in the
  **Cycle Controller's own string pool**. Either it's referenced (read, not
  necessarily declared) by both FSMs, or the doc's attribution needs
  updating. Worth a live check of which FSM actually *owns* (declares) the
  variable vs. which ones merely reference it.
- **The believed "per-action clock wiring lives inside the FSM and that's the
  end of the story" reading of report 11 undersold it.** The clock tie isn't
  really a same-FSM data link at all in the interesting (cloud/hacking) cases
  — it's a *global variable named after the action*, which is a materially
  different (and more powerful/more decoupled) mechanism than "the Action
  Controller points at a Clock GameObject." This matters for report 23's
  design: a location-scoped clock read will be systematically wrong for an
  entire modality (hacking), not just occasionally wrong.
- **`read_typetree()` does not work at all for any PlayMakerFSM component in
  this game**, not just the large Action Controller ones — Cycle Controller,
  Dice Gamepad System, Gamepad Dice Slot, and Dice Cursor 1 all fail
  identically. Prior session notes (`triage-queue-2026-07-18.md`: "FSM blobs
  partially greppable... UnityPy + TypeTreeGeneratorAPI now installed and
  working against level1") should be read as "the tooling is installed and
  works for non-FSM typetree reads (Buttons, Images, MonoScript headers)," not
  "FSM interiors are structurally parseable" — the latter is not true for this
  build. Future static work on any PlayMakerFSM should go straight to
  ASCII-run extraction rather than re-attempting `read_typetree()`.
- **The outcome-preview area (`OUTCOMES/<Tier>/Effect N`) is real rendered
  text (`TextMeshProUGUI`), not the icon/image-only graphic implied by report
  11's framing** ("Graphics needing transcode... count as rendered" — this
  turns out to include actual localized text objects, not just images/pips,
  making it a more direct transcode target than expected).
