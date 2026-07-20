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
    /// always-on while at a location — arrows walk rows, slash swaps the Actions and
    /// Clocks tabs, Enter = row commit (one native click: die actions open allocation,
    /// item/cryo actions run their own slot flow), Space = detail. Supersedes the
    /// native-arrow adjacency idiom at action view and retires K here (the Clocks tab
    /// IS the clock index — "K becomes unnecessary under a logical nav grammar").
    ///
    /// The affordance cell distinguishes the three take-kinds structurally (corpus):
    /// die slots = pure dice machinery; item slots carry Item Cost + the INV_* read;
    /// cryo cards run Action Cryo Controller with Cryo Cost + rendered Cost Label.
    /// </summary>
    internal static class LocationTable
    {
        private static class W
        {
            public const string TabActions = "Actions";
            public const string TabClocks = "Clocks";
            public const string NoRows = "Nothing here.";
            public const string NoClocks = "No clocks here.";
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
            public const string HeaderNarrative = "Narrative";
            public const string NowDisabled = "action card disabled";
            public const string NowEnabled = "action card enabled";
        }

        private static bool _clocksTab;
        private static int _row, _col;
        // Full facet set as columns (owner ruling: full read on row switch, table
        // broken out by risk, cost, narrative block, ...).
        private static readonly string[] Headers =
            { W.HeaderName, W.HeaderSkill, W.HeaderRisk, W.HeaderTakes, W.HeaderCost, W.HeaderNarrative };
        // Clock rows are real rows too (owner ruling, live 2026-07-20): whole row
        // auto-reads on row switch, and the narrative block is its own cell.
        private static readonly string[] ClockHeaders =
            { W.HeaderName, W.HeaderProgress, W.HeaderNarrative };

        public static void OnLeftLocation() { _clocksTab = false; _row = 0; _col = 0; }

        public static bool HandleKeys()
        {
            if (Input.GetKeyDown(KeyCode.Slash)) { SwapTab(); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)) { Detail(); return true; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Commit(); return true; }
            return false;
        }

        private static void SwapTab()
        {
            _clocksTab = !_clocksTab;
            _row = 0; _col = 0;
            if (_clocksTab)
            {
                var clocks = GameQueries.GetClockPanels();
                SpeechService.Say(W.TabClocks + ". "
                    + (clocks.Count > 0 ? ClockRow(clocks[0]) : W.NoClocks),
                    Priority.Immediate, "table");
            }
            else
            {
                var actions = GameQueries.GetActionPanels();
                SpeechService.Say(W.TabActions + ". "
                    + (actions.Count > 0 ? ActionRow(actions[0]) : W.NoRows),
                    Priority.Immediate, "table");
            }
        }

        private static void MoveRow(int delta)
        {
            if (_clocksTab)
            {
                var clocks = GameQueries.GetClockPanels();
                if (clocks.Count == 0)
                { SpeechService.Say(W.NoClocks, Priority.Immediate, "table"); return; }
                _row = Mathf.Clamp(_row + delta, 0, clocks.Count - 1);
                SpeechService.Say(_col == 0 ? ClockRow(clocks[_row])
                    : ClockName(clocks[_row]) + ". " + ClockCell(clocks[_row], _col),
                    Priority.Immediate, "table");
                return;
            }
            var actions = GameQueries.GetActionPanels();
            if (actions.Count == 0)
            { SpeechService.Say(W.NoRows, Priority.Immediate, "table"); return; }
            _row = Mathf.Clamp(_row + delta, 0, actions.Count - 1);
            SpeechService.Say(_col == 0 ? ActionRow(actions[_row])
                : ActionName(actions[_row]) + ". " + Cell(actions[_row], _col),
                Priority.Immediate, "table");
        }

        private static void MoveCol(int delta)
        {
            if (_clocksTab)
            {
                var clocks = GameQueries.GetClockPanels();
                if (clocks.Count == 0) return;
                _row = Mathf.Clamp(_row, 0, clocks.Count - 1);
                _col = Mathf.Clamp(_col + delta, 0, ClockHeaders.Length - 1);
                SpeechService.Say(ClockHeaders[_col] + ": " + ClockCell(clocks[_row], _col),
                    Priority.Immediate, "table");
                return;
            }
            var actions = GameQueries.GetActionPanels();
            if (actions.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, actions.Count - 1);
            _col = Mathf.Clamp(_col + delta, 0, Headers.Length - 1);
            SpeechService.Say(Headers[_col] + ": " + Cell(actions[_row], _col),
                Priority.Immediate, "table");
        }

        private static void Detail()
        {
            if (_clocksTab)
            {
                var clocks = GameQueries.GetClockPanels();
                if (clocks.Count == 0) return;
                _row = Mathf.Clamp(_row, 0, clocks.Count - 1);
                // ClockRow already carries the narrative (full-row ruling).
                SpeechService.Say(ClockRow(clocks[_row]), Priority.Immediate, "table");
                return;
            }
            var actions = GameQueries.GetActionPanels();
            if (actions.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, actions.Count - 1);
            SpeechService.Say(Describe.DescribeAction(actions[_row].gameObject, detailed: true),
                Priority.Immediate, "table");
        }

        private static void Commit()
        {
            if (_clocksTab) return; // clocks are display-only (corpus: no selection machinery)
            var actions = GameQueries.GetActionPanels();
            if (actions.Count == 0) return;
            _row = Mathf.Clamp(_row, 0, actions.Count - 1);
            var root = actions[_row];
            // One native click on the card's own button (single-dispatch): die actions
            // open allocation, item/cryo actions run their designed slot flow.
            var button = FindCardButton(root);
            if (button != null)
            {
                SnapshotForDiff();
                Navigator.Click(button.gameObject);
            }
            else SpeechService.Say(W.NotActivatable, Priority.Immediate, "table");
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
            foreach (var clock in GameQueries.GetClockPanels())
            {
                string name = Describe.TextUnder(clock, "Clock Name") ?? clock.name;
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
            string narrative = Cell(root, 5);
            if (skill != null) sb.Append(". ").Append(skill);
            if (risk != null) sb.Append(", ").Append(risk);
            sb.Append(". ").Append(takes ?? W.EnterToActivate).Append('.');
            if (selectable == null) sb.Append(' ').Append(W.NotActivatable);
            if (cost != null) sb.Append(' ').Append(cost).Append('.');
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
                    var rating = Describe.TextUnder(root, "Rating Name");
                    return rating != null ? rating.ToLowerInvariant() : null;
                case 3: return Describe.TakesLine(root);
                case 4: return Describe.TextContaining(root, "PER CYCLE");
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
