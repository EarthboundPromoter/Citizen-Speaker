# Character table — acceptance predictions (built 2026-07-20, commit 9929506)

Written before the live session per the W5 model. Owner rulings: table view per the
standard idiom; Enter anywhere fires the upgrade action speaking "Choose a skill to
upgrade."; armed reads constrain to skill + perks; purchase feedback = remaining
points, then perk bought when one was, then skill value after purchase.

## Predictions

1. **Open (U):** Watchers speaks the open line + points as before. Up/Down now walk
   the five skill rows with a full-row read:
   "ENDURE +1. Perk 1, owned: NAME, DESC. Perk 2, not owned: NAME, DESC.
   Next: Perk 2, NAME, costs 2 points."
   A broken skill's name cell carries ", broken" and its Next reads
   "repair, costs 1 point". A maxed skill reads "Next: Ladder complete."
2. **Left/Right:** header + cell — "Perks: …", "Next: …". Name column returns the
   full row (standard idiom). Space = full row.
3. **Enter, not armed:** clicks the main Upgrade button, speaks
   "Choose a skill to upgrade." — or "Choose a skill to upgrade or repair." when
   any row rests BROKEN (owner ruling). Row buttons become interactable (game's
   arming).
   Full-row reads now DROP the Next tail (owner: constrain to skill + perks);
   the Next cell stays reachable by column. Broken flag stays in-row (repair is
   what Enter buys there — surfaced call, owner may reverse).
4. **Enter on a row, armed:** clicks the row's single rung button. ~0.3s later the
   composed result: "<rendered points line>. Perk bought: NAME. ENDURE +1." —
   perk clause only when a perk rung was bought; refusal = "No change. <points>."
5. **F1:** no longer offers "Up and Down: review" in the character window.

## Wiring risks flagged in advance

- Next cell reads the row FSM's RESTING state (the ladder dial). During window-open
  transients the state may be a setup state → Next facet silently absent for a beat.
- Armed detection = any row button interactable (game truth). If the game leaves a
  row button interactable pre-arm on some save states, Enter would buy directly —
  watch the first Enter.
- The native confirm sub-step (corpus Confirm? states) never appeared on the s6/s7
  click path (one click bought directly). If it ever does, RowButton prefers a
  Confirm-named button so the second Enter confirms. Unexercised.
- Costs 1/1/1/2/3 and repair 1 are corpus constants (the same values the Upgrade
  Tracker's tier labels render) — verify the spoken cost matches the rendered
  label the first time each rung is heard.
