# H — Comprehensive focus assessment (owner-requested, 2026-07-19)

Commissioned after the session-5 incident cascade (false drive-log mode, wandering
selection, unadvanceable wake dialogue). Synthesizes the FSM corpus (briefs D/E), the
full incident history (sessions 1–5), and the mod's current focus-touching code.
Desk analysis only — no code has changed. Section 5 is a PROPOSAL for the design
dialogue; nothing in it is committed.

---

## 1. The game's focus system is one coherent protocol

Brief E decoded the parts; assembled, they form a single, consistent machine with
three layers and no central manager:

**Claiming.** Every selectable context claims focus through a Checker-template FSM in
one of three variants (E): A — permanent watchdog, reclaims on any change (menu/modal
buttons: pause RESUME, tutorial Button, cloud Sequence Complete); B — one-shot, then
relinquishes for good (End Cycle slot); C — sets itself once on state entry and never
fights (dialogue Continue Button, the five dice cursors, item cursors, action slots).

**Anchoring.** Station roam has a spatial anchor authority — `UI selector`
(closest visible `UI Button`-tagged object within range, interactability-checked,
zone-repositioned by Location Controller). Windows have designed anchors (character
window → Upgrade Button after 0.3s; picker → Dice Cursor 1; inventory → Item Cursor;
dialogue → Continue Button). Cycle transition is deliberately anchorless — READOUT.

**Handing off.** Every legitimate context exit is event-marked by the game itself:
`RefocusUI` broadcast ("everyone recheck your claim" — 399 senders), a targeted
`Reset` to UI selector ("hand back to the nearest marker" — a closed set of four
owner types), and `UI Reselector` (the Confirm-with-dead-selection backstop). Windows
fire both signals on close, identically (Character UI Button, Drive Log Button).

Two structural facts matter most for the mod:

1. **The game never leaves a focus context silently.** Handoffs are always signaled;
   the signals are enumerable and subscribable (FsmSignals can hear every one).
2. **Selection identity is sanctioned mode truth.** The game itself derives mode from
   focus: the inventory strip name-compares the current selection against
   `"Item Cursor"` every frame and self-closes on mismatch. Reading "what is
   selected" to know "where we are" is the game's own idiom, not a mod invention.

## 2. What the mod currently does about focus

- **Observes:** FocusPatch announces `SetSelectedGameObject` (2.5s re-announce
  cooldown, boot-sweep silence until first input, cycle-transition suppression,
  Continue Button never announced).
- **Moves:** Navigator rides the native graph (moveHandler/submit on the current
  selection); three mod-owned review cursors where nav is Automatic soup
  (CharacterSelect, TutorialReview, CharacterWindowReview); response-menu vertical
  remap.
- **Recovers:** empty-Enter mirrors the game's Confirm backstop (UI selector Reset) —
  but only in Station and ActionView modes. No other mode has any recovery idiom.
- **Governs:** ModeModel derives 15 modes by polling proxies (scene name, FSM flags,
  CanvasGroup alphas, DS events) under a hand-ordered precedence list; KeyScope maps
  mode → keys.

Notably absent: the mod subscribes to none of the game's handoff signals
(`RefocusUI`, UI selector `Reset`, window FSM open/close states) even though the W1
FsmSignals bus was built to hear exactly these. E's per-context anchor table and
variant classification exist only as prose — no code consumes them.

## 3. Incident ledger — every focus/mode failure to date, classed

1. S1 — foreign selection "jumps on and off" → fighting variant-A watchdogs.
   Class: engaging off-design.
2. S2 — intro-hang family → double dispatch per Enter. Class: engaging off-design.
3. S2 — TutorialReview always-active, ate Up/Down everywhere. Class: stale signal.
4. S4 — tutorial mode permanent on station (text-less Input Pauser child).
   Class: stale/mis-scoped mode signal.
5. S4 — wake dialogue scoped out (conversation vs `Scenes Active?`).
   Class: precedence derivation error.
6. S4 — station controls dead at intro handoff (`Scenes Active?` is
   scenery-engaged, not input-held). Class: proxy-semantics error.
7. S5 — drive-log mode falsely open from fresh boot: ModeModel reads raw
   CanvasGroup alpha with no baseline; the announcement watcher got the session-3
   baseline fix, the mode model silently duplicated the unfixed derivation.
   Class: stale mode signal + duplicated derivation.
8. S5 — under the false mode, in-scope arrows/Enter acted on the *station's* live
   graph (inventory strip, Character UI button): the player was navigating a surface
   the mod believed absent. Class: consequence of 7 — scoping without ground truth.
9. S5 — wake dialogue unadvanceable: the wake conversation runs *inside* the cycle
   transition (Cycle Controller not yet Idle), and CycleTransition outranks Dialogue
   in the precedence list, so Enter/arrows were silently scoped out while the game
   rendered a live Continue affordance. Class: precedence derivation error —
   **third instance of the same class**.
10. Standing — incidents 5, 6, and 9 were all *hidden* by silent refusal
    fall-through (the open owner ruling on audible refusals).

**The pattern:** not one incident came from the game misbehaving. Every failure is
the mod deriving game state wrongly, or engaging the game off-design. The game's
protocol is consistent and self-describing; the mod reconstructs state from proxies
instead of joining the protocol.

## 4. Why spot-checking keeps failing

- **Polled proxies instead of designed events.** Mode is re-derived every frame from
  flags, alphas, and scene names whose semantics we learn one incident at a time
  (`Scenes Active?`, boot-time alpha, Cycle non-Idle spanning dialogue). The game
  doesn't publish "mode" — it publishes *events and selection*, and we listen to
  almost none of them.
- **Precedence is an ordered list amended per incident.** The correct principle is
  already written in the ModeModel comment that fixed incident 5: "a live
  conversation is interactive by definition — the game is rendering an input
  affordance." That principle was applied to one pair (Dialogue vs Scenes) and never
  generalized, so incident 9 (Dialogue vs CycleTransition) was the same bug wearing
  a different flag.
- **Brief E was never turned into data.** The anchor table, the variant
  classification, and the handoff signals are exactly what a focus model needs, and
  W3 ("focus models per the E verdicts") was scheduled to encode them — but W2's
  live-verification work rode straight past it. The empty-Enter backstop is the sole
  fragment of E in the code, wired for one mode.
- **Duplicated derivations diverge.** The drive-log open check exists twice with
  different bug states (incident 7). Anything derived in two places will disagree
  eventually.

## 5. PROPOSAL — join the game's protocol (for design dialogue)

Principle: **the game already runs a coherent focus protocol; the mod's job is to
join it, not shadow it.** Concretely, five commitments:

1. **A FocusModel table, sourced row-for-row from E.** Per mode: designed anchor
   (path or identity), Checker variant (fights / relinquishes / passive), focus
   policy (NATIVE ride / REVIEW cursor / READOUT), and the game's own recovery
   signal for that context. Data, not prose — the same discipline as KeyScope, so
   dispatch, F1, and recovery can never drift from one another.
2. **Mode from events first, polls last.** Window lifecycles from their button FSMs'
   own states via FsmSignals; conversation from DS events (done); cycle from the
   event-armed gate (done); tutorial from rendered text (done). Flag/alpha polls
   demoted to boot-time initialization and divergence diagnostics (the
   conversation-poll pattern, generalized). Incident 7 becomes structurally
   impossible: a window is "open" when its FSM entered its open state, not when a
   CanvasGroup happens to sit at alpha 1.
3. **Precedence from affordance, not from a list.** The mode that owns the keys is
   the mode whose input affordance the game is currently rendering/holding — with
   selection identity as a first-class signal (the game's own idiom, §1). Ordering
   falls out: Pause, then whichever interactive affordance holds selection or is
   rendered (tutorial button / response menu / continue / dice cursor / item cursor
   / window cursor), then ambient wrappers (transition, autoplay, scene), then
   camera modes. Incidents 5, 6, and 9 all become the same solved case: a rendered
   Continue affordance outranks any wrapper, whatever flag the wrapper flies.
4. **One universal mode-aware re-anchor.** Extend the empty-Enter mirror to every
   mode using that mode's designed recovery from the FocusModel row: dialogue →
   reselect Continue Button (variant C never fights or self-restores — restoring the
   game's own anchor IS the designed recovery); picker → its own anchor machinery;
   windows → their delayed anchors; station → UI selector Reset (shipped). One
   idiom: "put focus back where the game intends it."
5. **Resync discipline in code.** Subscribe to `RefocusUI` and UI selector `Reset`
   as focus-settling cues; the mod never sets selection in the same frame
   (E's practical rule, currently enforced by luck).

Also on the table, per the session-5 evidence: the three provisionally-agreed fixes
(drive-log signal, inventory designed toggle + Up/Down = designed Swap, the S-key
native camera-scroll collision) should land *as parts of this model*, not as another
round of spot patches — each is an instance of commitments 2–4. And incident 10
strengthens the case for audible refusals at least as a diagnostic default; the
ruling stays the owner's.

## 6. Build-plan impact

This is W3, executed as the plan specified, plus the ModeModel hardening it
justifies. W2 wasn't a wrong turn — the mode model and KeyScope table are the
skeleton this attaches to — but W2 verification cannot complete until the mode
authority stops lying (incident 7 blocked half the checklist).
Correction for input-contract.md while we're here: the game's shipped keyboard map
is exactly five binds — W/S = camera scroll, A/D = rotate view, Tab = DEBUG MENU.
So our S key collides with a native camera bind (real, silent double-handling), and
killing Tab is vindicated (it would open a debug menu).

## 7. What needs confirmation before/while building (method hierarchy)

Corpus/desk first:
- Which cycle-transition sub-states host conversations (the wake beat) — bounds the
  affordance-precedence rule against the transition readout.
- `UnityUIQuestLogWindow.isOpen` (or equivalent) as the drive-log truth — assembly
  metadata check, same route that verified the DS conversation events.
Live (short, scripted):
- E's open questions that gate rows of the FocusModel: drive-log open branch,
  response-menu default selection, cloud initial anchor, per-tutorial Input Pauser
  firing.

---

Sources: docs/verification/E-focus-models.md, D-selection-machinery.md,
F-input-and-modes.md; tools/analysis/corpus/fsm-census.jsonl (incl. this session's
Bottom UI/Inventory and Gamepad Data / Item Swapper decode); level0 Rewired
InputManager dump (session-5 scratchpad); mod source (ModeModel, KeyScope,
InputManager, Watchers, FocusPatch); BepInEx log snapshot
LogOutput-session5-run1.log; action_log #001–#052 incident history.
