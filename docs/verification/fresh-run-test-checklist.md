# Fresh-run test checklist — inaugural new-game verification of all built surfaces

Session 9 close, 2026-07-20. Next session opens with this run (owner-set marker).
Ordered by natural play flow; check items off as heard. F3 on anything odd.
Companion acceptance docs: character-table-acceptance.md, cloud-table-acceptance.md,
map-table-acceptance.md, build-queue-acceptance-2026-07-19.md.

**Save discipline: start the fresh game on a SPARE slot.** Slot 1 is the real
OPERATOR save (PRECIOUS). The overwrite-warning check happens naturally when the
occupied-slot warning fires — back out, do NOT confirm.

## 1. Title

- [ ] NEW GAME on an occupied slot → overwrite warning body spoken (was silent —
      new). Back out without confirming.
- [ ] Save-slot labels (BL-5 wording scramble — note current shape for calibration).
- [ ] Character select: class review Up/Down, Left/Right class change, Enter start.

## 2. Intro + tutorials (guard contract, all new this session)

- [ ] Each tutorial panel auto-announces — including any whose text fills late
      (BL-3 fix: the poll now retries until text lands).
- [ ] Up/Down step text blocks with Top/End boundaries; Left/Right = bare repeat
      (never a native move).
- [ ] R rereads the tutorial (not dialogue) while a panel is up.
- [ ] Enter fires continue; on a taught-action tutorial with disabled continue:
      "Continue not ready. Follow the tutorial's instruction first."
- [ ] T still focuses/fires continue. No way to strand: only Enter/T/taught key act.

## 3. Dialogue

- [ ] Auto-read, choice counts, number picks, R reread — regression pass.

## 4. Dice flow (new wording + narration)

- [ ] Picker hover: "value X, die a of b"; spent die leads with "spent".
- [ ] After a die SETTLES: pre-spend narrative + "Enter to start."
      (Speech.NarrateOnPlacement; optionally verify the config off-switch).
- [ ] Backspace cancel: "Die picker closed.", retraction still silent in the
      refusal channel.

## 5. Location table

- [ ] Full row reads; Takes variants (die / item+cost / cryo+cost).
- [ ] Clocks tab: full-row auto-read INCLUDING narrative; Left/Right walks
      Name | Progress | Narrative cells.
- [ ] A skill-locked card (fresh char has low skills): row says "skill locked";
      Enter states the reason and does NOT open the picker.
- [ ] A completed/working card: row carries the game's own label, not bare
      "disabled"; Enter states it.
- [ ] Cryo/item bounce: slot a die onto an unaffordable card → "Not enough
      cryo. Costs X, holding Y." / "Missing the required item. Needs N, holding M."
- [ ] Post-roll callouts: clock ticks + "action card enabled/disabled" diffs.

## 6. Station outcomes (PRIORITY carry-over — still zero exercise on the event path)

- [ ] A tiered outcome (any die action): "NAME: positive/neutral/negative outcome."
      + card content.
- [ ] A bare-outcome action (item/value-check family, BL-14): name + content, no
      tier word.

## 7. Condition + breakdown channel (new, speculative build)

- [ ] Band transitions: "Condition falling: <band>." on genuine crossings only
      (no chatter while condition drifts within a band).
- [ ] First breakdown: "Breakdown. SKILL broken. All dice spent." + the breakdown
      tutorial box speaks (BL-3 verification) + dice audibly gone.
- [ ] After repair or item recovery: "Condition improving: <band>."

## 8. Character window (fourth table, new)

- [ ] Rows navigable READ-ONLY before any Upgrade press; full reads:
      name+rating(+broken) / perks with owned state / "Next: rung, costs N."
- [ ] Costs spoken match the rendered tracker tier labels (1/1/1/2/3, repair 1).
- [ ] Enter anywhere: "Choose a skill to upgrade." — "…or repair." when a skill
      is broken. Rows arm; constrained reads (Next tail drops).
- [ ] Purchase: points line + "Perk bought: NAME" (perk rungs) + skill value
      after. Refusal: "No change." + points.
- [ ] F1 no longer offers "review" here.

## 9. Map table (regression)

- [ ] Zone tabs, camera-synced browse, Enter commit, tracked-drives tab.
- [ ] Census channel: remember station census is RETRACTED (BL-16) — ignore its
      absence.

## 10. Journal + inventory (regression)

- [ ] Track toggle: "Tracking: X." Abandon two-step. Objectives cell.
- [ ] Item cursor reads, Space description, Up/Down swap (BL-4 bounce watch).

## 11. Cloud (once story unlocks it, ~cycle 2)

- [ ] First entry: SILENT census baseline.
- [ ] N: cloud table — corridor rows; DEMAND on never-entered dice nodes
      ("Matches die …" — verify against the glyphs after entering that node);
      cipher rows read "Takes an item…"; narrative empty until first entry, then
      re-readable; N inside an open node lands on its row.
- [ ] Camera-synced browse pans the corridor; Enter = flight + settle announce.
- [ ] Node outcomes, sequence steps, collect press → "DATA EXTRACTED" (still
      unexercised ever).
- [ ] Second entry: census speaks new/gone/moved.
- [ ] A gate hack: "Revealed: …" callout (~0.6s after outcome).
- [ ] OBSERVE ONLY: slot a wrong die against a demand — report exactly what
      happens (pending live shape before its refusal string is wired).
- [ ] OBSERVE: mid-sequence placement narration says "Enter to start." — note if
      the rendered button label would read better.

## 12. Cycle end

- [ ] Totals string with bare dice tail; placement narration interplay clean.

## Standing watches during the run

BL-15 (character toggle refusal — F3 if it recurs), Ghost Trackers (BL-19 — any
unexpected HUD tracker speech or silence), BL-2 glyph guard (no "Y button"),
strip-steal recovery, double-census on cloud mode flicker (acceptance risk item).
