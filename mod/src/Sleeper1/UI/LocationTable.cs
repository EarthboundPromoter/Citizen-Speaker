using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using CSAccess.Scaffold;
using CSAccess.Middleware;

namespace CSAccess.Sleeper1
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
            public const string HeaderRequires = "Requires";
            public const string HeaderRisk = "Risk";
            public const string HeaderCost = "Cost";
            public const string HeaderPredicted = "Predicted";
            public const string HeaderNarrative = "Narrative";
            public const string NowDisabled = "action card disabled";
            public const string NowEnabled = "action card enabled";
            public const string ClockNoCommit = "Clock card, nothing to activate.";
        }

        // Full facet set as columns (owner ruling: full read on row switch, table
        // broken out by risk, cost, narrative block, ...).
        // Requires merges the old Skill and Takes cells (owner ruling, session-12
        // ride: one requirements cell — die/item first, skill rank after, no verb
        // echo under the header; "Takes: Takes a die" was the heard dupe).
        // Predicted rides between Cost and Narrative (Intuit perk 1: the PREDICTIVE
        // display is render-gated — the cell is silent until the perk is bought).
        private static readonly string[] Headers =
            { W.HeaderName, W.HeaderRequires, W.HeaderRisk, W.HeaderCost,
              W.HeaderPredicted, W.HeaderNarrative };
        // Clock rows are real rows too (owner ruling, live 2026-07-20): whole row
        // auto-reads on row switch, and the narrative block is its own cell.
        private static readonly string[] ClockHeaders =
            { W.HeaderName, W.HeaderProgress, W.HeaderNarrative };

        /// <summary>The stacked grid's row space: 0.._actions-1 are action cards,
        /// the rest are clock cards. Rows are fetched fresh per keypress (live).</summary>
        private static bool ClockRowAt(int row) => row >= GameQueries.GetActionPanels().Count;

        // Engine composition (audit A1, 2026-07-26): the D4 stacked grid is the
        // sections capability — crossing the action/clock boundary announces the
        // section and resets the column (the sections carry different columns).
        // Empty cells speak a terse form (session-11 live nit: bare "Cost:").
        private static readonly TableEngine T = new TableEngine
        {
            Rows = () => GameQueries.GetActionPanels().Count + GameQueries.GetClockPanels().Count,
            Cols = row => ClockRowAt(row) ? ClockHeaders.Length : Headers.Length,
            SectionOf = row => ClockRowAt(row) ? 1 : 0,
            SectionPrefix = section => section == 1 ? W.SectionClocks : W.SectionActions,
            RowSpeech = (row, col) =>
            {
                var actions = GameQueries.GetActionPanels();
                if (row >= actions.Count)
                {
                    var clock = GameQueries.GetClockPanels()[row - actions.Count];
                    return col == 0 ? ClockRow(clock)
                        : ClockName(clock) + ". " + (ClockCell(clock, col) ?? "none");
                }
                var action = actions[row];
                return col == 0 ? ActionRow(action)
                    : ActionName(action) + ". " + (Cell(action, col) ?? "none");
            },
            CellSpeech = (row, col) =>
            {
                var actions = GameQueries.GetActionPanels();
                return row >= actions.Count
                    ? ClockHeaders[col] + ": "
                        + (ClockCell(GameQueries.GetClockPanels()[row - actions.Count], col) ?? "none")
                    : Headers[col] + ": " + (Cell(actions[row], col) ?? "none");
            },
            Detail = (row, _) => Detail(row),
            Commit = (row, _) => Commit(row),
            EmptyRow = () => W.NoRows,
        };

        public static void OnLeftLocation() => T.Reset();

        public static bool HandleKeys() => T.HandleKeys();

        private static void Detail(int row)
        {
            var actions = GameQueries.GetActionPanels();
            if (row >= actions.Count)
            {
                // ClockRow already carries the narrative (full-row ruling).
                SpeechService.Say(ClockRow(GameQueries.GetClockPanels()[row - actions.Count]),
                    Priority.Immediate, "table");
                return;
            }
            SpeechService.Say(Describe.DescribeAction(actions[row].gameObject, detailed: true),
                Priority.Immediate, "table");
        }

        private static void Commit(int row)
        {
            var actions = GameQueries.GetActionPanels();
            if (actions.Count == 0 || row >= actions.Count)
            {
                // Clocks are display-only (corpus: no selection machinery). Say so
                // rather than leave Enter silent (2026-10-25).
                SpeechService.Say(W.ClockNoCommit, Priority.Immediate, "table");
                return;
            }
            var root = actions[row];
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
            var button = Describe.FindCardButton(root);
            if (button != null && Describe.NoDieToSlot(root))
            {
                SpeechService.Say(Describe.NoDiceLeftLine, Priority.Immediate, "table");
                return;
            }
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
                string name = Describe.TrimQuotes(Describe.TextUnder(clock, "Clock Name")) ?? clock.name;
                _clockSnapshot[name] = GameQueries.ClockProgress(clock) ?? "";
            }
            foreach (var action in GameQueries.GetActionPanels())
                _activatableSnapshot[ActionName(action)] = Describe.FindCardButton(action) != null;
            _hasSnapshot = true;
        }

        /// <summary>Called by ActionOutcomes after its outcome announce: diff the
        /// location's clocks and every card's activatable state against the
        /// commit-time snapshot, announce what changed, refresh the snapshot.</summary>
        public static void AfterOutcome()
        {
            if (!_hasSnapshot || ModeModel.Current() != Mode.ActionView)
                return;
            var parts = new List<string>();
            // F7 dedupe: clocks the outcome's effect line already spoke as state
            // ("NAME now x of y") are skipped here — one tick, one announcement.
            bool effectFresh = UnityEngine.Time.unscaledTime - Watchers.RecentEffectClocksAt
                < Timing.EffectEchoWindow;
            foreach (var clock in GameQueries.GetClockPanels())
            {
                string name = Describe.TrimQuotes(Describe.TextUnder(clock, "Clock Name")) ?? clock.name;
                if (effectFresh && Watchers.RecentEffectClocks.Contains(name.ToUpperInvariant()))
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
                bool now = Describe.FindCardButton(action) != null;
                if (_activatableSnapshot.TryGetValue(name, out bool was) && was != now)
                    parts.Add(name + ", " + (now ? W.NowEnabled : W.NowDisabled));
            }
            if (parts.Count > 0)
                SpeechService.Say(string.Join(". ", parts) + ".", Priority.Queued, "table");
            SnapshotForDiff(); // rows stay live for the next roll
        }

        // Card-button resolution lives in Describe.FindCardButton (audit A7).

        // ---------- Rows / cells ----------

        private static string ActionName(Transform root)
            => Describe.TextUnder(root, "Action Name") ?? root.name.TrimEnd();

        /// <summary>Full read on row switch (owner ruling): every populated facet in
        /// column order, narrative last.</summary>
        private static string ActionRow(Transform root)
        {
            var sb = new System.Text.StringBuilder(ActionName(root));
            var selectable = Describe.FindCardButton(root);
            string requires = Cell(root, 1);
            string risk = Cell(root, 2);
            string cost = Cell(root, 3);
            string predicted = Cell(root, 4);
            string narrative = Cell(root, 5);
            // "Enter to activate" only on a card Enter can activate (2026-10-25: a
            // disabled card said it, then gave the reason it cannot be activated).
            // A spent card speaks its unavailable label below, not what it
            // would take (2026-10-27, used critical action).
            if (requires != null && !Describe.ActionSpent(root))
                sb.Append(". ").Append(W.HeaderRequires + " " + requires).Append('.');
            else if (selectable != null)
                sb.Append(". ").Append(W.EnterToActivate).Append('.');
            else
                sb.Append('.');
            if (risk != null) sb.Append(' ').Append(risk).Append('.');
            // Disabled rows carry the reason (owner ruling 2026-07-20).
            string reason = null;
            if (selectable == null)
            {
                reason = Describe.DisabledReason(root);
                sb.Append(' ').Append(reason ?? W.NotActivatable);
            }
            if (cost != null) sb.Append(' ').Append(cost).Append('.');
            if (predicted != null)
                sb.Append(' ').Append(W.HeaderPredicted).Append(": ").Append(predicted).Append('.');
            // Card-root gate reasons quote the card's own Description (flux-blocker
            // fix 2026-07-25) — the narrative cell would then repeat it verbatim in
            // the same breath. One read, one utterance (the outlawed-dupe rule).
            if (narrative != null && (reason == null || !reason.Contains(narrative)))
                sb.Append(' ').Append(narrative);
            return sb.ToString();
        }

        /// <summary>The merged requirements cell (owner ruling): what the action
        /// takes — die or item — then the skill rank, comma-joined, no verb under
        /// the header. Cryo costs read as their bare amount ("15 cryo").</summary>
        private static string RequiresCell(Transform root)
        {
            var parts = new List<string>();
            string takes = Describe.RequiresPhrase(root);
            if (takes != null) parts.Add(takes);
            string skill = Describe.SkillLine(root);
            if (skill != null) parts.Add(skill);
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        private static string Cell(Transform root, int col)
        {
            switch (col)
            {
                case 0: return ActionRow(root);
                case 1: return RequiresCell(root);
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
                case 3: return Describe.TranscodeCycleStrip(
                    Describe.TextContaining(root, "PER CYCLE"));
                case 4: return Describe.PredictiveLine(root);
                default: return Describe.TextUnder(root, "Description");
            }
        }

        private static string ClockName(Transform clock)
            => Describe.TrimQuotes(Describe.TextUnder(clock, "Clock Name")) ?? clock.name;

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
