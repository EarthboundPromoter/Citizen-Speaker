# Test session triage queue — 2026-07-18

Owner is playtesting CSAccess v0.1. Reports land here verbatim-ish with watch-log context; NO fixes until owner calls triage at end of session.

Watch tail: watch.jsonl (background task bcxswvb2b, cursor started at seq 133).

## Reports

### 1. Tutorial button prompts need transcoding to mod keys

Owner report (~t2480, seq 161, "TUTORIAL | INTRODUCTION" popup at Empty Container): tutorial shows controller button prompts; if player is on keyboard, announcements should name the mod's keys instead.

Snapshot evidence (hierarchy of Letterbox Canvas/Tutorial System): the prompt line is a TMP element `Gamepad Text` whose string is literally `"Select locations with          and        ."` — the "buttons" are NOT in the text. They are sibling child objects positioned in the whitespace gaps: `A Prompt` (ControllerIconPrompt, child TMP text "A"), `DPAD prompt` (images only, no text). So TutorialReview currently reads a sentence with silent holes.

Transcode design (for triage): detect prompt-child objects under a tutorial text element, map them to mod-appropriate keyboard keys (A→Enter, DPAD→arrow keys, Y→S scan, B→Backspace, R1→U, L1→J, per the controller-census mapping in cs-mod-progress), and splice into the gap positions. Whitespace-run in TMP string = splice point candidate.

Owner also asked: are we making the game think controller is on? Note: game may show gamepad prompts unconditionally (it has zero keyboard UI support natively). `Gamepad` global FSM bool state unchecked — verify at triage time whether prompts vary with it.

### 2. Proper focus model for location selection

Owner report: only one location now so bare L works, but L-cycle won't scale to multiple locations.

Snapshot evidence: each location is its own canvas under `ERLIN MAIN/1_Station UI/Locations` (Empty Container, Ambergris ×4 variants, Reservoir S3, ...); only unlocked ones have `Location Contents/Billboard Elements/Marker/Location Button` populated; label available at sibling `Portrait Name` TMP ("EMPTY CONTAINER"). Tutorial confirms controller players navigate markers with D-pad → designer navigation graph exists between Location Buttons.

Design direction (triage): L jumps focus onto current/nearest Location Button, arrows then walk the game's own Selectable graph (moveHandler path per core input principle), announce Portrait Name + marker glyph state on focus. Folds in parked map-marker glyph transcoding.

Session 7 live evidence (owner report): L's forced selection onto a marker fires the nav event but does NOT stick — the game's reselector machinery snaps selection back to its own anchor, so the following Enter acts on the snapped-back ("stale") object. Confirms tier-2 doctrine requirement: engage via the game's own selection anchors (its UiSetSelectedGameObject targets), never inject mod-chosen targets.

### 3. End-cycle: suppress flurry, hook the real structure

Owner report: after resting at container, flurry of unclear announcements, incl. "endurance -1"; wants proper end-cycle hook, only game-relevant data spoken.

Log evidence (t2717–2736, seq 167–195 + BepInEx speech log):
- "END CYCLE. END CYCLE" spoken 6× in a row (repeated game-driven re-focus of the End Cycle dice slot; no dedup).
- During transition (t2722.6–2723.7, ~1s), the game programmatically walked focus across EVERY location's action dice slots station-wide: Dragos's Yard (HULL DISSECTION, MANUAL SALVAGE), Scrap Freighter (BUY SOME SCRAP, UNLOAD CONTAINERS), Merchant Freighter (Buy Shipmind Fragment), Min-Gi Express (NOODLE MANUFACTURE, EXPRESS DELIVERY) — likely the dice-refresh/reset sweep for the new cycle. Focus watcher announced each: "MANUAL SALVAGE. ENDURE -1, safe", "UNLOAD CONTAINERS. ENDURE -1, danger", bare "Gamepad Dice Slot", etc. None user-initiated.
- "ENDURE -1" is NOT a stat deduction: it's the action card's endurance-cost label on those (other locations') actions. Spoken during the sweep, out of context.
- After the sweep, wake-up dialogue read correctly.

Design direction (triage): (a) detect cycle-transition state (End Cycle FSM / Action Controller) and gate ALL focus announcements during it — the user-initiated 0.7s window rule already exists for response buttons, extend/generalize to action+dice-slot focus, game-driven events stay silent; (b) dedup identical focus re-announcements; (c) replace with a deliberate end-cycle summary from real data once available: new cycle number, dice rolled, condition/energy change (blocked on [StatFsm]). Core-loop moment — should be a designed announcement, not incidental focus noise.

### 4. Meters (cycle/energy/condition) unreachable — need bounded focus walk or query keys

Owner report: tutorial mentioned the meters; no way to query them. "Idiom depends on UI and focus system. We either need a bounded focus to walk, or we need query keys."

Context: C query exists but stats are blocked on [StatFsm] (parked item a — PlayMaker globals came back negative). The End Cycle card renders meters as "PER CYCLE | - - ENERGY | - CONDITION" (dashes = pips, graphics-only). This report elevates parked item (a) and adds the design constraint: pick idiom AFTER we know where the data lives — if meters are focusable UI, a bounded review walk (TutorialReview pattern); if not, extend C query. Owner decides idiom at triage once StatFsm findings are in.

Supplementary (monitor event, mid-session): owner pressed C; mod spoke "Status: Cryo: 0. SURVIVE. Find a Doctor.." — confirms C query currently returns cryo + drive + objective only, NO condition/energy/cycle. Also minor: double-period formatting in the utterance ("Doctor..").

### 5. I key: opens something with a sound, no announcement of what happened

Owner report: I brings something up, plays a sound, unclear what fired.

Log evidence (t2931, seq 217): I opened the Inventory strip (Letterbox Canvas/Bottom UI/Inventory); game moved focus to DATA Button; mod spoke only "DATA button" — no open/close state, no contents. Sound = game's own FMOD open-inventory sound. Snapshot: Inventory Display panel shows Item Name "IMPRINTED SHIPMIND" + description — note the description TMP currently reads "This is where we describe the item. It should be short and snappy." which is the GAME's own placeholder string, not mod damage. Inventory Data holds ~25 per-item Manager FSMs (counts likely in FSM vars).

Design direction (triage): announce mode change explicitly ("Inventory open, DATA tab" / "Inventory closed") on I toggle; map the inventory screen (parked item f): item list focus walk with name+count+description; counts likely per-item Manager FSM ints. Known gap promoted by owner report.

### 6. Action slots don't announce their affordance (die-required vs plain activate)

Owner report: at End Cycle, "Am I meant to apply dice here? If so, there's no clear direction on how."

Live-state evidence: End Cycle's activation control is named "Dice Slot Button" internally but is a plain Button — FSM (End Cycle Action) has no die requirement, only an energy check (Setup → Normal | Starving; Energy var = 40). Real dice actions (e.g. Dragos's Yard) have per-die Gamepad Dice Slot machinery instead. Sighted players see the visual difference (die-outline slot vs plain button); announcement currently says only "END CYCLE. END CYCLE" — no affordance.

Design direction (triage): classify slot type when announcing a focused action — die-required slots get "slot a die with number keys" (or the die-outline equivalent label the game shows), plain activate buttons get "press Enter". Also dedup the doubled name ("END CYCLE. END CYCLE" = action name + button text, identical strings). Ties into parked item (b) — dice flow still never exercised live.

RESOLVED MID-SESSION (settled after two wrong turns by me — final state): End Cycle takes NO die. Structural proof: every dice-taking action station-wide has a "Gamepad Dice Slot" child; End Cycle actions uniquely have none; Dice Cursor 1-5 (the real picker widgets) were inactive throughout. The screenshot top bar I misread as a modal picker is the PERSISTENT gamepad dice HUD (Letterbox Canvas/Top UI/Dice UI) with the standing "B LEAVE" location prompt. Owner pressed number keys 5×, mod correctly said "This action does not take dice" each time. Remaining fixes for this report: (a) the refusal message should state the affordance ("This action does not take dice. Press Enter to activate."); (b) no dedup on repeated refusals; (c) original affordance-announcement design stands.

### 7. Condition text + meter visible in persistent dice HUD — data source lead for report 4

Screenshot (shot_141937_575.png): the always-on gamepad dice HUD (Top UI/Dice UI) shows condition state as TEXT ("FLICKERING") plus a segmented meter bar above the five dice (5,5,1,4,+one alert-glyph die). So condition (and possibly energy) is readable as a TMP string / UI fill in that HUD — likely no StatFsm dependency. Triage: locate the HUD element paths, wire into C query / meter idiom (report 4). Also transcode the alert-glyph die state (broken/unusable?).

MAJOR DATA FIND (supersedes StatFsm hunt for report 4): root object "Cycle Controller" FSM holds Player Energy, Player Condition, Cycle Count, Starving, Die Condition, LightCycle, Intro Complete? as plain FSM variables. Read directly — no PlayMaker globals needed.

### 8. GOVERNING DESIGN ITEM (owner-set): define and announce the interaction modality of each moment — never assume

Owner (verbatim intent): "Instead of assuming what the game wants, we need to define the interaction modality of this specific moment in the game's intro flow and make that clear."

The End Cycle impasse in full, as evidence: Enter (submit+click) inert; bridge click fired all uGUI handlers, FSM unmoved; card FSM turned out to own wake-scenes, not activation; I then BROADCAST the global EndCycle event to unblock (owner note: this was an assumption-driven injection — it worked, cycle ticked 1→2, scripted scene fired cleanly, but the sanctioned input path was never identified).

~~Open hypothesis~~ FALSIFIED by owner + log ("No tutorial open" on T-press post-scene): the tutorial modal was NOT up. Original text kept for the record: the end-cycle TUTORIAL modal may still have been up — Tutorial System has an "Input Pauser" FSM, the slot-button's selection-tracker FSM showed Selected = Tutorial System/Button throughout, and the screenshot's letterboxing matches the tutorial's translucent bars. If so, the true modality of that moment was "dismiss tutorial (T)" and ALL slot input was gated behind it — which would explain every dead input at once, and means the mod failed by not announcing the modal gate.

Design consequence: the mod needs a moment-modality model — at any time, know and announce which mode the game is in (tutorial modal / dialogue / station free-roam / dice allocation / pause) and what inputs that mode accepts. Focus announcements alone are insufficient; a modal gate that silently eats input is exactly what stranded the owner. Triage tasks: (a) ~~verify Input Pauser gating~~ (hypothesis falsified — but Input Pauser's actual role still worth understanding during the intro-flow evaluation); (b) resolve End Cycle's real sanctioned input path (dnSpy/UnityPy on the click wiring if needed); (c) modality announcements on mode transitions; (d) never resolve player impasses by event injection — diagnose the modality instead.

### 9. OWNER MANDATE (session close): evaluate intro flow + this game portion; build a real UI model

Owner (closing the game): "this screen is broken... We'll need to evaluate this portion of the game, and the intro flow itself, so that we can build a model around how the UI actually works, rather than your quick and dirty initial build."

Broken-screen evidence (post cycle-2 scene, t3971–4152): focus bounced Continue Button ↔ deselected repeatedly, then went fully unfocused ("Nothing focused" on nav key); owner wandered DATA/ITEM buttons and pause menu; T said "No tutorial open"; number key at END CYCLE still refused. Screen state unrecoverable from keyboard.

MUST INVESTIGATE as possible cause: my EndCycle broadcast fired mid-INTRO ("Intro Complete?" = 0 throughout). The injection skipped whatever the intro's sanctioned end-cycle path was — plausibly derailed the scripted intro state machine, leaving the post-scene screen broken. The breakage may be mod-inflicted, not a game bug. Dev save (slot 1) state now suspect — consider fresh save for next session.

Scope of the mandate: map the intro flow's scripted sequence (Cycle Controller + scene FSMs + tutorial system + Input Pauser + selection-tracker FSMs) end to end; derive the UI interaction model from evidence (how activation actually flows per moment/mode); rebuild the mod's input/focus/announcement layer on that model. This supersedes the initial quick-and-dirty focus-hook build. Reports 2, 3, 6, 8 all fold into this as sub-problems.

### 10. Surface what the game currently expects/offers, not just what's focused (session 5, owner report — REFRAMED per owner correction)

CORRECTED RECORD: owner was fully aware of being in the container throughout. The gap was knowing WHAT THE GAME EXPECTED — the intro wanted a leave/conversation beat while the owner reasonably expected die allocation to be possible at the container. My earlier framing ("didn't know they were there") was my presumption; owner's first-person account of their own state is authoritative — assent to it.

Actual finding: the mod must convey a location's offering when it matters — which actions exist here, how many are available vs disabled, and (modality layer) what the current script beat accepts. The container in the intro has NO dice-allocatable actions (static census: End Cycle active/no-dice; Sunbathe/Self Repair/Inject Stabilizer all Action-Switch-gated INACTIVE) — that fact spoken once would have answered the owner's whole question.

Owner hypothesis to verify when flow works: the mod may be "auto-reading all possible actions on location load." Code fact: no deliberate auto-read exists; the suspected mechanism is the focus watcher echoing the game's programmatic selection sweeps on load (same mechanism as report 3's end-cycle flurry) — report 3's game-driven-focus gating covers it; validate live.

Session 5 facts kept: location markers INACTIVE through early intro (travel not offered; L correctly empty); leaving the container mid-intro advanced the script to a conversation beat; view-context announcements ("At: <location>" / "Station view", anchors: Action View? global, Location Controller, Leave Button state) remain worth building — as information, not as a remedy for owner confusion.

### 11. Transcode action success odds (session 5, owner report — HELD, no action yet)

Owner: action announcements carry skill+modifier and risk level, but not the ODDS of success — the game shows outcome probability graphically on the card (die-value bands for negative/neutral/positive, shifted by skill modifiers per the ACTIONS tutorial). That graphic needs capture + transcode.

Leads for triage (updated after live Manual Salvage dump): the Action Controller FSM holds NO per-action odds thresholds — outcome text/effects/clock wiring only. Odds appear to be a GLOBAL rule (die value + skill modifier vs fixed bands); the ACTIONS tutorial states the rule in-game — lift exact band wording from the tutorial's full spoken text in the speech log. Card texts that ARE rendered and speakable: name, skill + modifier row with applied modifier, SAFE/RISKY/DANGER rating, REPEATABLE/CRITICAL tag, INPUT DICE, description, and the on-card outcome PREVIEW lines (effect summaries per outcome type).

CONSTRAINT (owner-set, binding): transcode ONLY what the game renders. Graphics needing transcode (odds display, pips, meters) count as rendered. FSM-internal prose — future outcome descriptions, completion scripts, unseen identifiers — is NEVER spoken by the mod and not quoted to the owner in conversation (spoiler discipline; see memory feedback-no-spoilers-rendered-only).

### 13. Dice flow LIVE-VALIDATED with one gap: slot-commit announcement missed (session 5)

First full live run: Enter on Manual Salvage slot → "Choose a die" ✓ → die chosen → action resolved → "MANUAL SALVAGE: neutral outcome" ✓ → clocks tutorial followed. Native arrows/Enter flow works end to end. GAP: "Die slotted." never spoke — the slot FSM's Slotted state is transient and the 0.4s poll missed it; consequently picker close after a successful commit spoke the cancel wording ("Die picker closed"). Also an earlier genuine cancel spoke correctly.

Fix leads (triage): detect commit via a durable signal instead of the transient state — SlottedDiceGlobal / SlottedDiceValueGlobal float change, Action Controller leaving Idle, or Harmony-hook the slot FSM's Slotted entry rather than polling. Then close-after-commit can stay silent (outcome announcement covers it) or say "slotted" first.

Also (same area, small): TutorialReview's end-of-review prompt still says "Press T to continue" — stale since the native-Enter change; should say "Press Enter to continue." (TutorialReview.cs, not Watchers.)

### 17. Drive log v1 BUILT+DEPLOYED (session 7 close) — validation pending next session

Owner-requested build. J retargeted from the passive tracker HUD to the real Drive Log Button (uGUI Button, native click). Watcher announces "Drive log open."/"Drive log closed." on the window's CanvasGroup alpha (Animator-hidden, not active-toggled). Describe now reports Toggle state ("<label> toggle, on/off") for the track toggles. Window structure (static): Active/Completed tab buttons, Close button, scroll area where entries spawn at runtime (Dialogue System UnityUIQuestLogWindow). VALIDATE live: whether the window takes focus on open, entry structure/announcements, tab navigation, toggle wording (gate).

### 18. Drive log enrichment — owner's three priorities (session 8)

1. **Tracked/untracked status actively announced** — per drive entry, on focus and on toggle change (the Toggle describe covers the widget, but the drive's tracked STATE must be first-class in the entry announcement).
2. **Focus bounding** — while the log is open, navigation confines to the window; arrows must not escape into background UI. First real focus-confinement case for the modality layer (window-modal scope).
3. **Tab state** — announce which tab (Active/Completed) is selected, both on open and on tab change.

Evidence gathering next open: dump the runtime entry structure live (bridge) while the window is up to bind announcements to real objects.

### 19. Cloud (hacking) modality opened — first live findings (session 8)

Story unlocked THE CLOUD (S toggles the view; the tutorial's toggle glyph is another report-1 hole). Confirmed live: the die picker works identically inside the cloud (same Dice Gamepad System). Gaps found: (a) **Tab is blind to the cloud** — GetActionPanels/GetClockPanels scan only `1_Action Groups`; hacking actions live in `2_Hacking Action Groups` — scan both or scope by modality; (b) data actions' die-MATCH requirement (match displayed die value; INTERFACE modifier widens match set) is rendered visually — match target + refusal feedback need transcode; (c) cloud tutorial dismissal behaved differently (continue button component fully disabled while panel up; possibly dismisses via the taught action — modality variant for report 8's catalog, mechanism UNVERIFIED). (d) Backspace's leave-paths are also station-scoped — "Leave or back not available" in cloud; cloud exit = S toggle (Backspace could map to it in cloud modality). Cloud view state (`Hacking?` global) is a modality-layer anchor.

### 20. Character menu non-functional (owner, session 8 close)

U key fails to open the character window (earlier: "Character window not available" / this session gated or inert). The tutorial funnels players here to spend the upgrade point. Known structure: Character UI Button (own FSM: Set Up/Idle/MouseOver/Open/Close, watches a Rewired action, "Character Screen"), Character Window (class portrait FSM, Upgrade Tracker FSM with Player_UpgradePoints, SKILL List). Triage: determine the sanctioned open path (probably the FSM's own Open event via its Rewired watch — needs the Click/submit route verified), then map the window (upgrade spend flow = first priority since the game just handed an upgrade point).

### 21. Owner session-8 wrap: C status must give full readout (folds into 4); scan nodes must convey required die value (folds into 19b)

Owner's other two named priorities. Report 4 now has all data sources identified (Cycle Controller locals + HUD bar FSM locals); wiring is implementation-ready. Node die-match target: rendered on the action card's right side per the DATA ACTIONS tutorial — locate the rendered element (die glyph/value) live in cloud view and speak it in the action description.

### 16. ROOT CAUSE (best-evidenced) of all tutorial-chain hangs: Enter double-dispatched activations — FIXED session 6, validation pending

Session 6 reproduced the intro hang on the fixed build with ZERO interventions and native-Enter-only tutorial continues — falsifying "clicks during pause windows" as sufficient cause. Actual defect: Navigator.Click executed BOTH pointerClick AND submit handlers; uGUI Buttons invoke onClick on each → every Enter (and the old T path) fired the tutorial's advance event TWICE — first advances into the timed pause, second lands in the wrong state → panel/selection stale, script wedged, world input walled ("CONTINUE button, disabled" on the dismissed tutorial's button; owner diagnosed the stale modal live). Unity's input module fires exactly one activation (submit for keyboard, pointerClick for mouse); Click now matches: submit first, full pointer sequence only as fallback. Explains sessions 2-6 hang signatures uniformly. VALIDATE: full intro pass on session 7.

Also observed session 6: bridge stopped answering (even /ping) during the wedged state — listener confirmed started at launch; suspect main-thread starvation in the wedge. Watch for recurrence.

Pause announcement validated + valuable: "Paused. COMPLETE TUTORIAL TO ACTIVATE AUTOSAVE." — the game renders WHY autosave is off; owner always hears quit cost now.

### 15. Dead-end wording: revert to bare repeat (owner verdict, PINNED — no change until triage)

Owner on the new "X. Nothing that way." suffix: bad string construction. A bare repeat of the terminal option is sufficient dead-end feedback for non-wrapping lists. At triage: revert Navigator.Move's dead-end announcement to the plain element description (pre-fix behavior — the wording, not the bug; the repeat must still fire).

### 14. Clock cycling idiom (owner report, PINNED — K full readout acceptable meanwhile)

Owner: K's read-all-clocks-at-location "sucks" as the primary idiom. Want: a clock cycling key — speak ONE clock (name + progress) per press, with a reverse-cycle counterpart. Keep K as the full-dump fallback for now. Design at triage: key choice (shift-pairing convention like Tab/Shift+Tab), whether cycling holds a position pointer per location, and whether it should engage focus if clocks turn out to be focusable (doctrine tier check).

### 12. Skill-modifier readout grabs the first row label, not the applied modifier (session 5, HELD)

Live card evidence (Manual Salvage): the modifier row renders ALL FOUR labels (-1, 0, +1, +2) as separate TMP texts; the player's applied modifier (+1 here) is distinguished graphically. `Describe.CollectSkillLine` → `SiblingModifier` returns the FIRST matching text — "-1" — so announcements say "ENDURE -1" regardless of actual modifier. Session 2's "ENDURE -1" utterances were this bug (I misread them as endurance cost at the time). Fix at triage: identify the applied modifier by its rendered emphasis (color/alpha/scale difference between row labels — inspect live), per render-gated speech rule. Also verify which skill the sweep matches when multiple skill words render on one card.

## Post-session static findings (UnityPy on level1, 2026-07-18 afternoon)

- BUILD SKEW: session was tested on the PRE-choice-rework DLL (game launched ~13:22; choice-rework built 13:24, deployed at exit 14:30). Committed source + next launch = choice rework. All 9 reports were filed against the old build.
- END CYCLE WIRING (solves report 8 sub-question b): Dice Slot Button's serialized Button.m_OnClick → single persistent call → SendEvent("EndCycle"), string-mode, targeted at the Cycle Controller FSM (scene root). Plain uGUI click IS the sanctioned modality — no dice, no gamepad FSM chain for this action.
- INJECTION EXONERATED (breadth): full-scene scan — ONLY Cycle Controller's FSM references EndCycle (other 5 hits = the five home buttons' identical onClick calls). Broadcast ≡ targeted send here. Broken screen NOT caused by the event reaching wrong FSMs. Still suspect: TIMING — the click was likely gated (Button/CanvasGroup non-interactable, intro-scripted), so I may have forced end-cycle at a moment the intro had disabled it. Verify interactability live at next launch (one bridge query).
- Corollary: the mod's Enter path (submit+click) was formally CORRECT; the failure was silence — no feedback that the control was gated/inert. Tooling note: UnityPy + TypeTreeGeneratorAPI now installed and working against level1; FSM blobs partially greppable (event names/action classes readable as strings).

## Session end state (game closed by owner, ~t4152)

- Watch tailer stopped (task bcxswvb2b); full event capture in watch.jsonl (seq 133→290).
- NOTE: on game exit, the PRIOR session's auto-deploy watcher fired and deployed the "choice rework" build to BepInEx/plugins — next game launch runs a NEWER mod build than what was tested today. Verify which build was running during these reports (choice-rework may already have been live if deployed pre-session).
- Save slot 1 (dev save): cycle 2, energy 0, condition 60, mid-intro, screen was broken at close — state suspect per report 9.
