using System.Collections.Generic;
using CSAccess.Game;
using CSAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>
    /// The location (action view) as the third table (owner rulings 2026-07-20):
    /// always-on while at a location — arrows walk rows, Enter = row commit (one
    /// native click: die actions open allocation, item/cryo actions run their own
    /// slot flow), Space = detail. Supersedes the native-arrow adjacency idiom at
    /// action view and retires K here (the clock rows ARE the clock index — "K
    /// becomes unnecessary under a logical nav grammar").
    ///
    /// D4 (owner ruling 2026-07-20): ONE stacked grid, no tab split — action cards
    /// on top, clock cards below; crossing the section boundary announces the
    /// section and resets the column (the sections carry different columns). Slash
    /// is freed at locations.
    ///
    /// The affordance cell distinguishes the three take-kinds structurally (corpus):
    /// die slots = pure dice machinery; item slots carry Item Cost + the INV_* read;
    /// cryo cards run Action Cryo Controller with Cryo Cost + rendered Cost Label.
    /// </summary>
    internal static class LocationTable
    {
        private static class W
        {
            public const string SectionActions = "Action cards.";
            public const string SectionClocks = "Clock cards.";
            public const string NoRows = "Nothing here.";
            public const string TakesDie = "Takes a die";
            public const string TakesItem = "Takes an item";
            public const string CostPrefix = "Costs ";
            public const string CostSuffix = " cryo";
            public const string EnterToActivate = "Enter to activate";
            public const string NotActivatable = "Action card disabled.";
            public const string HeaderName = "Name";
            public const string HeaderProgress = "Progress";
            public const string HeaderSkill = "Skill";
            public const string HeaderTakes = "Takes";
            public const string HeaderRisk = "Risk";
            public const string HeaderCost = "Cost";
            public const string HeaderPredicted = "Predicted";
            public const string HeaderNarrative = "Narrative";
            public const string NowDisabled = "action card disabled";
            public const string NowEnabled = "action card enabled";
        }

        private static int _row, _col;
        // Section of the current row (crossing announces + resets the column —
        // the two sections carry different columns).
        private static bool _inClocks;
        // Full facet set as columns (owner ruling: full read on row switch, table
        // broken out by risk, cost, narrative block, ...).
        // Predicted rides between Cost and Narrative (Intuit perk 1: the PREDICTIVE
        // display is render-gated — the cell is silent until the perk is bought).
        private static readonly string[] Headers =
            { W.HeaderName, W.HeaderSkill, W.HeaderRisk, W.HeaderTakes, W.HeaderCost,
              W.HeaderPredicted, W.HeaderNarrative };
        // Clock rows are real rows too (owner ruling, live 2026-07-20): whole row
        // auto-reads on row switch, and the narrative block is its own cell.
        private static readonly string[] ClockHeaders =
            { W.HeaderName, W.HeaderProgress, W.HeaderNarrative };

        public static void OnLeftLocation() { _inClocks = false; _row = 0; _col = 0; }

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

        /// <summary>The stacked grid's row space: 0.._actions-1 are action cards,
        /// the rest are clock cards. Rows are fetched fresh per keypress (live).</summary>
        private static bool ClockRowAt(int row, int actionCount) => row >= actionCount;

        private static void MoveRow(int delta)
        {
            var actions = GameQueries.GetActionPanels();
            var clocks = GameQueries.GetClockPanels();
            int total = actions.Count + clocks.Count;
            if (total == 0)
            { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }
            _row = Mathf.Clamp(_row + delta, 0, total - 1);
            bool clockRow = ClockRowAt(_row, actions.Count);
            string prefix = "";
            if (clockRow != _inClocks)
            {
                // Boundary crossing (D4): announce the section, reset the column.
                _inClocks = clockRow;
                _col = 0;
                prefix = (clockRow ? W.SectionClocks : W.SectionActions) + " ";
            }
            if (clockRow)
            {
                var clock = clocks[_row - actions.Count];
                SpeechService.Say(prefix + (_col == 0 ? ClockRow(clock)
                    : ClockName(clock) + ". " + (ClockCell(clock, _col) ?? "none")),
                    Priority.Immediate, "table");
            }
            else
            {
                var action = actions[_row];
                SpeechService.Say(prefix + (_col == 0 ? ActionRow(action)
                    : ActionName(action) + ". " + (Cell(action, _col) ?? "none")),
                    Priority.Immediate, "table");
            }
        }

        private static void MoveCol(int delta)
        {
            var actions = GameQueries.GetActionPanels();
            var clocks = GameQueries.GetClockPanels();
            int total = actions.Count + clocks.Count;
            if (total == 0) return;
            _row = Mathf.Clamp(_row, 0, total - 1);
            _inClocks = ClockRowAt(_row, actions.Count);
            // Empty cells speak a terse form (session-11 live nit: bare "Cost:").
            if (_inClocks)
            {
                _col = Mathf.Clamp(_col + delta, 0, ClockHeaders.Length - 1);
                SpeechService.Say(ClockHeaders[_col] + ": "
                    + (ClockCell(clocks[_row - actions.Count], _col) ?? "none"),
                    Priority.Immediate, "table");
            }
            else
            {
                _col = Mathf.Clamp(_col + delta, 0, Headers.Length - 1);
                SpeechService.Say(Headers[_col] + ": " + (Cell(actions[_row], _col) ?? "none"),
                    Priority.Immediate, "table");
            }
        }

        private static void Detail()
        {
            var actions = GameQueries.GetActionPanels();
            var clocks = GameQueries.GetClockPanels();
            int total = actions.Count + clocks.Count;
            if (total == 0) return;
            _row = Mathf.Clamp(_row, 0, total - 1);
            if (ClockRowAt(_row, actions.Count))
            {
                // ClockRow already carries the narrative (full-row ruling).
                SpeechService.Say(ClockRow(clocks[_row - actions.Count]),
                    Priority.Immediate, "table");
                return;
            }
            SpeechService.Say(Describe.DescribeAction(actions[_row].gameObject, detailed: true),
                Priority.Immediate, "table");
        }

        private static void Commit()
        {
            var actions = GameQueries.GetActionPanels();
            if (actions.Count == 0 || _row >= actions.Count)
                return; // clocks are display-only (corpus: no selection machinery)
            _row = Mathf.Clamp(_row, 0, actions.Count - 1);
            var root = actions[_row];
            // Skill-locked cards keep an interactable button and open a DOOMED picker
            // (Haggle live case: LOCKED has no DiceSlotted response) — Enter states the
            // reason instead of clicking (owner ruling 2026-07-20).
            if (Describe.ActionSkillLocked(root))
            {
                SpeechService.Say(Describe.DisabledReason(root) ?? W.NotActivatable,
                    Priority.Immediate, "table");
                return;
            }
            // One native click on the card's own button (single-dispatch): die actions
            // open allocation, item/cryo actions run their designed slot flow.
            var button = FindCardButton(root);
            if (button != null)
            {
                SnapshotForDiff();
                Navigator.Click(button.gameObject);
            }
            // Refusal states its reason (owner ruling 2026-07-20): resting lock dial
            // + the game's own Button Label text, falling back to the generic word.
            else SpeechService.Say(Describe.DisabledReason(root) ?? W.NotActivatable,
                Priority.Immediate, "table");
        }

        // ---------- Post-roll change callouts (owner ruling: all changes announced
        // automatically; the narrative repopulation itself is spoken by the outcome
        // pipeline, which reads the card's new Description) ----------

        private static readonly Dictionary<string, string> _clockSnapshot =
            new Dictionary<string, string>();
        private static readonly Dictionary<string, bool> _activatableSnapshot =
            new Dictionary<string, bool>();
        private static bool _hasSnapshot;

        private static void SnapshotForDiff()
        {
            _clockSnapshot.Clear();
            _activatableSnapshot.Clear();
            foreach (var clock in GameQueries.GetClockPanels())
            {
                string name = Describe.TextUnder(clock, "Clock Name") ?? clock.name;
                _clockSnapshot[name] = GameQueries.ClockProgress(clock) ?? "";
            }
            foreach (var action in GameQueries.GetActionPanels())
                _activatableSnapshot[ActionName(action)] = FindCardButton(action) != null;
            _hasSnapshot = true;
        }

        /// <summary>Called by ActionOutcomes after its outcome announce: diff the
        /// location's clocks and every card's activatable state against the
        /// commit-time snapshot, announce what changed, refresh the snapshot.</summary>
        public static void AfterOutcome()
        {
            if (!_hasSnapshot || Modality.ModeModel.Current() != Modality.Mode.ActionView)
                return;
            var parts = new List<string>();
            // F7 dedupe: clocks the outcome's effect line already spoke as state
            // ("NAME now x of y") are skipped here — one tick, one announcement.
            bool effectFresh = UnityEngine.Time.unscaledTime - Watchers.RecentEffectClocksAt < 2f;
            foreach (var clock in GameQueries.GetClockPanels())
            {
                string name = Describe.TextUnder(clock, "Clock Name") ?? clock.name;
                if (effectFresh && Watchers.RecentEffectClocks.Contains(name.Trim().ToUpperInvariant()))
                    continue;
                string now = GameQueries.ClockProgress(clock) ?? "";
                if (_clockSnapshot.TryGetValue(name, out string was) && was != now
                    && now.Length > 0)
                    parts.Add(name + ", " + now);
                else if (!_clockSnapshot.ContainsKey(name) && now.Length > 0)
                    parts.Add(name + ", " + now);
            }
            foreach (var action in GameQueries.GetActionPanels())
            {
                string name = ActionName(action);
                bool now = FindCardButton(action) != null;
                if (_activatableSnapshot.TryGetValue(name, out bool was) && was != now)
                    parts.Add(name + ", " + (now ? W.NowEnabled : W.NowDisabled));
            }
            if (parts.Count > 0)
                SpeechService.Say(string.Join(". ", parts) + ".", Priority.Queued, "table");
            SnapshotForDiff(); // rows stay live for the next roll
        }

        private static Button FindCardButton(Transform root)
        {
            var slotButton = StationAtlas.FindDeep(root, "Dice Slot Button");
            var b = slotButton != null ? slotButton.GetComponent<Button>() : null;
            if (b != null && b.gameObject.activeInHierarchy && b.IsInteractable()) return b;
            foreach (var candidate in root.GetComponentsInChildren<Button>(false))
                if (candidate.IsInteractable()) return candidate;
            return null;
        }

        // ---------- Rows / cells ----------

        private static string ActionName(Transform root)
            => Describe.TextUnder(root, "Action Name") ?? root.name.TrimEnd();

        /// <summary>Full read on row switch (owner ruling): every populated facet in
        /// column order, narrative last.</summary>
        private static string ActionRow(Transform root)
        {
            var sb = new System.Text.StringBuilder(ActionName(root));
            var selectable = FindCardButton(root);
            string skill = Cell(root, 1);
            string risk = Cell(root, 2);
            string takes = Cell(root, 3);
            string cost = Cell(root, 4);
            string predicted = Cell(root, 5);
            string narrative = Cell(root, 6);
            if (skill != null) sb.Append(". ").Append(skill);
            if (risk != null) sb.Append(", ").Append(risk);
            sb.Append(". ").Append(takes ?? W.EnterToActivate).Append('.');
            // Disabled rows carry the reason (owner ruling 2026-07-20).
            if (selectable == null)
                sb.Append(' ').Append(Describe.DisabledReason(root) ?? W.NotActivatable);
            if (cost != null) sb.Append(' ').Append(cost).Append('.');
            if (predicted != null)
                sb.Append(' ').Append(W.HeaderPredicted).Append(": ").Append(predicted).Append('.');
            if (narrative != null) sb.Append(' ').Append(narrative);
            return sb.ToString();
        }

        private static string Cell(Transform root, int col)
        {
            switch (col)
            {
                case 0: return ActionRow(root);
                case 1: return Describe.SkillLine(root);
                case 2:
                {
                    // Risk cell carries the type badge too (fresh-run F10):
                    // "risky" / "danger, critical action". Repeatable = silent.
                    var rating = Describe.TextUnder(root, "Rating Name");
                    string risk = rating != null ? rating.ToLowerInvariant() : null;
                    string badge = Describe.CriticalBadge(root);
                    if (badge != null)
                        risk = risk != null
                            ? risk + ", " + badge.ToLowerInvariant()
                            : badge.ToLowerInvariant();
                    return risk;
                }
                case 3: return Describe.TakesLine(root);
                case 4: return Describe.TextContaining(root, "PER CYCLE");
                case 5: return Describe.PredictiveLine(root);
                default: return Describe.TextUnder(root, "Description");
            }
        }

        private static string ClockName(Transform clock)
            => Describe.TextUnder(clock, "Clock Name") ?? clock.name;

        private static string ClockNarrative(Transform clock)
            => Describe.TextUnder(clock, "Description")
               ?? Describe.TextUnder(clock, "Clock Description");

        /// <summary>Full read on row switch (owner ruling, live 2026-07-20): name,
        /// progress, then the narrative block.</summary>
        private static string ClockRow(Transform clock)
        {
            string progress = GameQueries.ClockProgress(clock);
            string narrative = ClockNarrative(clock);
            return ClockName(clock) + (progress != null ? ", " + progress : "") + "."
                   + (narrative != null ? " " + narrative : "");
        }

        private static string ClockCell(Transform clock, int col)
        {
            switch (col)
            {
                case 0: return ClockRow(clock);
                case 1: return GameQueries.ClockProgress(clock);
                default: return ClockNarrative(clock);
            }
        }
    }
}
