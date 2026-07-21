using System.Collections.Generic;
using CSAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>
    /// The character window as the fourth table (owner rulings 2026-07-20): rows = the
    /// five SKILL List rows, columns Name | Perks | Next, full-row read on row switch,
    /// all navigable READ-ONLY before the upgrade flow is armed (the window renders
    /// everything permanently; the main Upgrade button only activates the rows'
    /// interactive buttons). Enter anywhere fires the main Upgrade button and speaks
    /// "Choose a skill to upgrade."; once armed, row reads constrain to skill + perks
    /// (+ broken flag on broken rows, since repair is what Enter buys there) and Enter
    /// buys the row's NEXT ladder rung via the game's own button.
    ///
    /// Ladder ground truth (corpus, ENDURE row FSM): the resting state IS the position
    /// dial — -1 → 0 (1 pt) → Perk 1 (1 pt) → +1 (1 pt) → Perk 2 (2 pts) → +2 (3 pts,
    /// terminal); BROKEN repairs for 1 pt. Perks are purchased rungs, never granted
    /// automatically. Tier costs render on the Upgrade Tracker (render pairing).
    ///
    /// Purchase feedback (owner composition): remaining points, then the perk bought
    /// when one was, then the skill value after purchase. Replaces
    /// CharacterWindowReview (the review cursor, retired this commit).
    /// </summary>
    internal static class CharacterTable
    {
        private static class W
        {
            public const string ChooseSkill = "Choose a skill to upgrade.";
            public const string ChooseSkillRepair = "Choose a skill to upgrade or repair.";
            public const string UpgradeUnavailable = "Upgrade not available.";
            public const string NoRows = "No skill rows.";
            public const string LadderComplete = "Ladder complete.";
            public const string Broken = "broken";
            public const string NoChange = "No change.";
            public const string Cancelled = "Cancelled.";
            public const string ConfirmPrompt = "Enter to confirm, Backspace to cancel.";
            public const string HeaderName = "Name";
            public const string HeaderPerks = "Perks";
            public const string HeaderNext = "Next";
        }

        private static readonly string[] Headers = { W.HeaderName, W.HeaderPerks, W.HeaderNext };

        private static Transform _window;
        private static int _row, _col;
        private static bool _wasActive;

        public static bool IsActive()
        {
            var w = Window();
            if (w == null) return false;
            var group = w.GetComponent<CanvasGroup>();
            bool open = w.gameObject.activeInHierarchy && (group == null || group.alpha > 0.5f);
            if (!open && _wasActive) { _row = 0; _col = 0; }
            _wasActive = open;
            return open;
        }

        private static Transform Window()
        {
            if (_window == null)
            {
                var go = GameObject.Find("Letterbox Canvas/Character Window");
                if (go != null) _window = go.transform;
            }
            return _window;
        }

        public static bool HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { Detail(); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Commit(); return true; }
            return false;
        }

        // ---------- Rows ----------

        private static List<Transform> Rows()
        {
            var list = new List<Transform>();
            var w = Window();
            var skills = w != null ? w.Find("SKILL List") : null;
            if (skills == null) return list;
            foreach (Transform skill in skills)
                if (skill.gameObject.activeInHierarchy) list.Add(skill);
            return list;
        }

        private static void MoveRow(int delta)
        {
            var rows = Rows();
            if (rows.Count == 0)
            { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }
            _row = Mathf.Clamp(_row + delta, 0, rows.Count - 1);
            SpeechService.Say(_col == 0 ? RowRead(rows[_row])
                : NameCell(rows[_row]) + ". " + Cell(rows[_row], _col),
                Priority.Immediate, "table");
        }

        private static void MoveCol(int delta)
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            _col = Mathf.Clamp(_col + delta, 0, Headers.Length - 1);
            SpeechService.Say(Headers[_col] + ": " + Cell(rows[_row], _col),
                Priority.Immediate, "table");
        }

        private static void Detail()
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            SpeechService.Say(RowRead(rows[_row]), Priority.Immediate, "table");
        }

        /// <summary>Full row per the standard idiom; once the flow is armed the read
        /// constrains to skill + perks (owner ruling), keeping the broken flag on
        /// broken rows (repair is exactly what Enter buys there).</summary>
        private static string RowRead(Transform row)
        {
            var sb = new System.Text.StringBuilder(NameCell(row));
            string perks = PerksCell(row);
            if (perks != null) sb.Append(". ").Append(perks);
            if (!Armed())
            {
                string next = NextCell(row);
                if (next != null) sb.Append(". ").Append(W.HeaderNext).Append(": ").Append(next);
            }
            return sb.ToString();
        }

        private static string Cell(Transform row, int col)
        {
            switch (col)
            {
                case 0: return RowRead(row);
                case 1: return PerksCell(row);
                default: return NextCell(row);
            }
        }

        // ---------- Cells ----------

        private static string SkillWord(Transform row) => row.name.Trim();

        /// <summary>"ENDURE +1" (Lua bucket, the BL-1 render pairing) + broken flag from
        /// the row FSM's resting state (the ladder dial).</summary>
        private static string NameCell(Transform row)
        {
            string word = SkillWord(row);
            string modifier = Substrate.LuaStore.SkillModifierForWord(word);
            string cell = modifier != null ? word + " " + modifier : word;
            if (RestingState(row) == "BROKEN") cell += ", " + W.Broken;
            return cell;
        }

        /// <summary>"Perk 1, owned: name, description. Perk 2, not owned: ..." — only
        /// rendered perk slots (render-honesty; empty template slots stay silent).</summary>
        private static string PerksCell(Transform row)
        {
            var parts = new List<string>();
            for (int n = 1; n <= 2; n++)
            {
                var perk = FindPerk(row, n);
                if (perk == null) continue;
                string name = TextOf(perk, "Perk Name");
                string desc = TextOf(perk, "Perk Description");
                parts.Add("Perk " + n + (PerkOwned(row, n) ? ", owned: " : ", not owned: ")
                    + (string.IsNullOrEmpty(name) ? "unnamed" : name)
                    + (string.IsNullOrEmpty(desc) ? "" : ", " + desc));
            }
            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        /// <summary>Next rung + cost from the row FSM's resting state (corpus ladder:
        /// costs 1/1/1/2/3, repair 1 — the same constants the Upgrade Tracker's tier
        /// cost labels render). Perk rungs carry the perk's rendered name.</summary>
        private static string NextCell(Transform row)
        {
            switch (RestingState(row))
            {
                // C6: bare rung numbers read as counts — "rank" gives them a noun.
                case "-1": return "rank 0, costs 1 point";
                case "0": return PerkRung(row, 1, 1);
                case "Perk 1": return "rank +1, costs 1 point";
                case "+1": return PerkRung(row, 2, 2);
                case "Perk 2": return "rank +2, costs 3 points";
                case "+2": return W.LadderComplete;
                case "BROKEN": return "repair, costs 1 point";
                default: return null; // transient state — no stable fact to speak
            }
        }

        private static string PerkRung(Transform row, int n, int cost)
        {
            var perk = FindPerk(row, n);
            string name = perk != null ? TextOf(perk, "Perk Name") : null;
            return "Perk " + n + (string.IsNullOrEmpty(name) ? "" : ", " + name)
                + ", costs " + cost + (cost == 1 ? " point" : " points");
        }

        private static string RestingState(Transform row)
        {
            var fsm = row.GetComponent<PlayMakerFSM>();
            return fsm != null ? fsm.ActiveStateName : null;
        }

        // ---------- Perk slots (marker layers; BG = full ladder, FG = achieved) ----------

        private static Transform FindPerk(Transform skill, int n)
        {
            var perk = skill.Find("BG Markers/Perk " + n);
            if (perk == null || !perk.gameObject.activeInHierarchy) return null;
            return string.IsNullOrEmpty(TextOf(perk, "Perk Name")) ? null : perk;
        }

        private static bool PerkOwned(Transform skill, int n)
        {
            // NB the FG container name carries a trailing space in the shipped hierarchy.
            var fg = skill.Find("FG Markers /Perk " + n) ?? skill.Find("FG Markers/Perk " + n);
            return fg != null && fg.gameObject.activeInHierarchy;
        }

        private static string TextOf(Transform perk, string child)
        {
            var t = perk.Find(child);
            var tmp = t != null ? t.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            return tmp != null ? SpeechService.Clean(tmp.text) : null;
        }

        // ---------- Commit ----------

        /// <summary>Armed = the game has activated the rows' interactive buttons (what
        /// the main Upgrade press does). Game truth, not mod state.</summary>
        private static bool Armed()
        {
            foreach (var row in Rows())
                if (RowButton(row) != null) return true;
            return false;
        }

        /// <summary>The row's single interactable button. Preference for a Confirm-named
        /// button covers the native confirm sub-step when the game presents one; the
        /// rung button is named for the NEXT rung (never name-filtered — s6 regression).</summary>
        private static Button RowButton(Transform row)
        {
            Button rung = null;
            foreach (var b in row.GetComponentsInChildren<Button>(false))
            {
                if (!b.IsInteractable() || !b.gameObject.activeInHierarchy) continue;
                if (b.gameObject.name.Contains("Confirm")) return b;
                if (rung == null) rung = b;
            }
            return rung;
        }

        private static void Commit()
        {
            var w = Window();
            if (w == null) return;
            if (!Armed())
            {
                // Owner ruling: Enter anywhere fires the upgrade action.
                var main = w.Find("Upgrade Tracker/Top Line/Upgrade UI/Upgrade Button");
                var mainBtn = main != null ? main.GetComponent<Button>() : null;
                if (mainBtn != null && mainBtn.IsInteractable() && main.gameObject.activeInHierarchy)
                {
                    Navigator.Click(main.gameObject);
                    // Owner ruling 2026-07-20: the arm announce names repair when any
                    // skill is broken (repair is the same buy on a broken row).
                    bool anyBroken = false;
                    foreach (var row in Rows())
                        if (RestingState(row) == "BROKEN") { anyBroken = true; break; }
                    SpeechService.Say(anyBroken ? W.ChooseSkillRepair : W.ChooseSkill,
                        Priority.Immediate, "table");
                }
                else
                    SpeechService.Say(W.UpgradeUnavailable, Priority.Immediate, "table");
                return;
            }
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, rows.Count - 1);
            var button = RowButton(rows[_row]);
            if (button == null)
            {
                SpeechService.Say(W.UpgradeUnavailable, Priority.Immediate, "table");
                return;
            }
            ArmPurchaseCheck(rows[_row]);
            Navigator.Click(button.gameObject);
        }

        // ---------- Purchase feedback (BL-9 lineage; owner composition 2026-07-20:
        // remaining points, then the perk bought when one was, then the skill value
        // after purchase; refusals stay "No change." + points) ----------

        /// <summary>Stamped when the purchase compose speaks — ResourceWatch stands
        /// down for point changes this interaction already voiced (A4 lane rule).</summary>
        internal static float LastPurchaseSpokeAt = -10f;

        private static string _pointsBefore;
        private static Transform _watchRow;
        private static readonly bool[] _perkWasOwned = new bool[3];
        private static float _checkAt = -1f;

        private static string PointsLine()
        {
            var w = Window();
            var pointsAv = w != null ? w.Find("Upgrade Tracker/Top Line/Points UI/Points Av") : null;
            return pointsAv != null ? Describe.JoinTexts(pointsAv.gameObject, 2) : null;
        }

        private static void ArmPurchaseCheck(Transform row)
        {
            _pointsBefore = PointsLine();
            _watchRow = row;
            for (int n = 1; n <= 2; n++) _perkWasOwned[n] = PerkOwned(row, n);
            _checkAt = Time.unscaledTime + 0.3f;
            // F12: a fresh arm on a row with no modal up starts a new attempt.
            if (ConfirmRoot(row) == null) _confirmAnnounced = false;
        }

        /// <summary>F12: the row's Confirm? modal when it is actually up. The game
        /// opens it on the first row click WITHOUT moving selection (run-3 finding:
        /// two silent false "No change." refusals) — the deferred read must treat
        /// it as a pending confirmation, not a result.</summary>
        private static Transform ConfirmRoot(Transform row)
        {
            var c = row != null ? row.Find("Confirm?") : null;
            return c != null && c.gameObject.activeInHierarchy ? c : null;
        }

        private static bool _confirmAnnounced;

        /// <summary>From Plugin.Update: the deferred post-activation read.</summary>
        public static void Tick()
        {
            if (_checkAt < 0 || Time.unscaledTime < _checkAt) return;
            _checkAt = -1f;
            if (!IsActive() || _watchRow == null) return;

            string after = PointsLine();
            bool pointsChanged = after != null && _pointsBefore != null && after != _pointsBefore;

            // F12: modal up and nothing spent yet = PENDING, not refused. Announce
            // the modal once (its own rendered text when present), keep re-clocking
            // until it resolves — Enter fires the Confirm button (RowButton prefers
            // it), Backspace closes it natively.
            var confirm = ConfirmRoot(_watchRow);
            if (!pointsChanged && confirm != null)
            {
                if (!_confirmAnnounced)
                {
                    _confirmAnnounced = true;
                    string label = Describe.JoinTexts(confirm.gameObject, 3);
                    SpeechService.Say((string.IsNullOrEmpty(label) ? "Confirm upgrade" : label)
                        + ". " + W.ConfirmPrompt, Priority.Queued, "table");
                }
                _checkAt = Time.unscaledTime + 0.3f; // watch stays armed
                return;
            }

            string perkBought = null;
            for (int n = 1; n <= 2; n++)
            {
                if (_perkWasOwned[n] || !PerkOwned(_watchRow, n)) continue;
                var perk = FindPerk(_watchRow, n);
                perkBought = perk != null ? TextOf(perk, "Perk Name") : ("Perk " + n);
                break;
            }

            // Wording provisional (owner calibration).
            var sb = new System.Text.StringBuilder();
            if (pointsChanged || perkBought != null)
            {
                if (after != null) sb.Append(after).Append(". ");
                if (perkBought != null) sb.Append("Perk bought: ").Append(perkBought).Append(". ");
                sb.Append(NameCell(_watchRow)).Append('.');
            }
            else
            {
                // F12: an announced modal resolving with nothing spent = the
                // player cancelled; a true refusal never showed a modal.
                sb.Append(_confirmAnnounced ? W.Cancelled : W.NoChange);
                if (after != null) sb.Append(' ').Append(after).Append('.');
            }
            _confirmAnnounced = false;
            LastPurchaseSpokeAt = Time.unscaledTime;
            SpeechService.Say(sb.ToString(), Priority.Queued, "table");
            _watchRow = null;
        }
    }
}
