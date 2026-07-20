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
            public const string NotActivatable = "Not activatable.";
            public const string HeaderName = "Name";
            public const string HeaderSkill = "Skill";
            public const string HeaderTakes = "Takes";
            public const string HeaderRisk = "Risk";
        }

        private static bool _clocksTab;
        private static int _row, _col;
        private static readonly string[] Headers =
            { W.HeaderName, W.HeaderSkill, W.HeaderTakes, W.HeaderRisk };

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
                SpeechService.Say(ClockRow(clocks[_row]), Priority.Immediate, "table");
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
            if (_clocksTab) return; // clock rows are single-cell
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
                string desc = Describe.TextUnder(clocks[_row], "Description")
                              ?? Describe.TextUnder(clocks[_row], "Clock Description");
                SpeechService.Say(ClockRow(clocks[_row]) + (desc != null ? " " + desc : ""),
                    Priority.Immediate, "table");
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
            if (button != null) Navigator.Click(button.gameObject);
            else SpeechService.Say(W.NotActivatable, Priority.Immediate, "table");
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

        private static string ActionRow(Transform root)
        {
            var sb = new System.Text.StringBuilder(ActionName(root));
            var selectable = FindCardButton(root);
            string skill = Cell(root, 1);
            string takes = Cell(root, 2);
            string risk = Cell(root, 3);
            if (skill != null) sb.Append(". ").Append(skill);
            if (risk != null) sb.Append(", ").Append(risk);
            sb.Append(". ").Append(takes ?? W.EnterToActivate).Append('.');
            if (selectable == null) sb.Append(" Not activatable.");
            return sb.ToString();
        }

        private static string Cell(Transform root, int col)
        {
            switch (col)
            {
                case 0: return ActionRow(root);
                case 1: return Describe.SkillLine(root);
                case 2: return Describe.TakesLine(root);
                default:
                    var rating = Describe.TextUnder(root, "Rating Name");
                    return rating != null ? rating.ToLowerInvariant() : null;
            }
        }

        private static string ClockRow(Transform clock)
        {
            string name = Describe.TextUnder(clock, "Clock Name") ?? clock.name;
            string progress = GameQueries.ClockProgress(clock);
            return name + (progress != null ? ", " + progress : "") + ".";
        }
    }
}
