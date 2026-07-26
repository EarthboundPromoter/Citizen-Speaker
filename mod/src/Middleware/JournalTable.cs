using System.Collections.Generic;
using PixelCrushers.DialogueSystem;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

using CSAccess.Scaffold;

namespace CSAccess.Middleware
{
    /// <summary>
    /// The drive journal as a table (map-table-design.md, journal section — owner
    /// rulings 2026-07-20). Active while the drive log window is open: arrows walk a
    /// mod-owned grid over the QuestLog truth; the game's own nav soup (headings,
    /// toggles, tab row, scrollbar in one Automatic graph) is never walked. Slash =
    /// native tab swap (existing). PRESS-ONLY: movement reads; Enter performs the
    /// cell's full game-sanctioned action via native clicks.
    /// </summary>
    internal static class JournalTable
    {
        private static class W
        {
            public const string Tracked = "tracked";
            public const string NotTracked = "Not tracked.";
            public const string TrackedCell = "Tracked.";
            public const string TrackingNone = "Tracking nothing.";
            public const string TrackingPrefix = "Tracking: ";
            public const string Abandonable = "Abandon.";
            public const string NotAbandonable = "Cannot abandon.";
            public const string NoObjectives = "No objectives.";
            public const string NoQuests = "No drives here.";
            public const string Expanded = "Expanded.";
            public const string HeaderName = "Name";
            public const string HeaderObjectives = "Objectives";
            public const string HeaderTrack = "Track";
            public const string HeaderAbandon = "Abandon";
        }

        private static readonly string[] Headers =
            { W.HeaderName, W.HeaderObjectives, W.HeaderTrack, W.HeaderAbandon };

        // Engine composition (audit A1, 2026-07-26): cursor/clamp/dispatch live in
        // Scaffold; the QuestLog truth, wording, and the expansion policy stay here.
        // Owner ruling (session 11) rides in the hooks: the highlighted row IS the
        // expanded row - expand on arrival, close the one we left; Right keeps the
        // row current so Track/Abandon are always live.
        private static readonly TableEngine T = new TableEngine
        {
            Rows = () => Rows().Count,
            Cols = _ => Headers.Length,
            RowSpeech = (row, col) =>
            {
                var rows = Rows();
                return col == 0 ? RowReport(rows[row])
                    : rows[row] + ". " + CellText(rows[row], col);
            },
            CellSpeech = (row, col) => Headers[col] + ": " + CellText(Rows()[row], col),
            OnRowArrive = (prev, row) =>
            {
                var rows = Rows();
                prev = Mathf.Clamp(prev, 0, rows.Count - 1);
                if (row != prev) SyncExpansion(rows[prev], rows[row]);
                else EnsureExpanded(rows[row]);
            },
            OnColArrive = (row, col, delta) => { if (delta > 0) EnsureExpanded(Rows()[row]); },
            Detail = (row, _) => FullRow(row),
            Commit = (row, col) => Activate(Rows()[row], col),
            EmptyRow = () => W.NoQuests,
            EmptyCol = () => W.NoQuests,
            EmptyDetail = () => W.NoQuests,
        };

        /// <summary>Routed from InputManager while mode == DriveLog. Returns true when
        /// the key was consumed. Slash falls through to the native tab swap.</summary>
        public static bool HandleKeys() => T.HandleKeys();

        public static void OnWindowClosed() => T.Reset();

        // ---------- Rows (stateless reads — the QuestLog is the truth every press) ----------

        private static List<string> Rows()
        {
            var rows = new List<string>();
            try
            {
                bool active = WindowState.DriveLogShowingActive != false;
                var states = active ? QuestState.Active
                                    : QuestState.Success | QuestState.Failure;
                foreach (var q in QuestLog.GetAllQuests(states)) rows.Add(q);
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("[Journal] quests: " + e.Message); }
            return rows;
        }

        /// <summary>Expand the arriving row; if the departed row is still pulled out
        /// afterwards (the window is not an accordion), close it via its own heading.</summary>
        private static void SyncExpansion(string prevQuest, string quest)
        {
            EnsureExpanded(quest);
            if (prevQuest == quest) return;
            if (FindRowActionButton(prevQuest, "TRACKING") == null) return; // already closed
            var heading = FindHeadingButton(prevQuest);
            if (heading != null) Navigator.Click(heading.gameObject);
        }

        /// <summary>Pull the quest's row out if it isn't already (the tracking toggle
        /// renders only inside an expanded row — its presence IS the expanded test).
        /// Silent: the cell read that follows is the announcement.</summary>
        private static void EnsureExpanded(string quest)
        {
            if (FindRowActionButton(quest, "TRACKING") != null) return;
            var heading = FindHeadingButton(quest);
            if (heading != null) Navigator.Click(heading.gameObject);
        }

        private static void FullRow(int row)
        {
            var rows = Rows();
            var sb = new System.Text.StringBuilder();
            for (int i = 0; i < Headers.Length; i++)
                sb.Append(Headers[i]).Append(": ").Append(CellText(rows[row], i)).Append(' ');
            SpeechService.Say(sb.ToString().TrimEnd(), Priority.Immediate, "table");
        }

        private static string RowReport(string quest)
        {
            bool tracked = false;
            try { tracked = QuestLog.IsQuestTrackingEnabled(quest); } catch { }
            return quest + (tracked ? ", " + W.Tracked + "." : ".");
        }

        private static string CellText(string quest, int col)
        {
            switch (col)
            {
                case 0: return RowReport(quest);
                case 1: return ObjectivesText(quest);
                case 2:
                    try
                    {
                        return QuestLog.IsQuestTrackingEnabled(quest)
                            ? W.TrackedCell : W.NotTracked;
                    }
                    catch { return W.NotTracked; }
                default:
                    try
                    {
                        return QuestLog.IsQuestAbandonable(quest)
                            ? W.Abandonable : W.NotAbandonable;
                    }
                    catch { return W.NotAbandonable; }
            }
        }

        /// <summary>Objective text from the QuestLog API (precedent: the map table's
        /// drives tab, owner-heard) — entries with their state; completed tab rows get
        /// success/failure wording from the entry states themselves.</summary>
        private static string ObjectivesText(string quest)
        {
            var sb = new System.Text.StringBuilder();
            try
            {
                string desc = QuestLog.GetQuestDescription(quest);
                if (!string.IsNullOrEmpty(desc))
                    Append(sb, SpeechService.Clean(desc));
                int n = QuestLog.GetQuestEntryCount(quest);
                for (int i = 1; i <= n; i++)
                {
                    var st = QuestLog.GetQuestEntryState(quest, i);
                    if (st == QuestState.Unassigned) continue;
                    string text = SpeechService.Clean(QuestLog.GetQuestEntry(quest, i));
                    if (string.IsNullOrEmpty(text)) continue;
                    // C6: dash placeholders are non-entries; trim the entry's own
                    // terminal period before appending status ("Doctor., done").
                    if (text.Trim('-', ' ').Length == 0) continue;
                    if (st == QuestState.Success) text = text.TrimEnd('.') + ", done";
                    else if (st == QuestState.Failure) text = text.TrimEnd('.') + ", failed";
                    Append(sb, text);
                }
            }
            catch (System.Exception e) { Plugin.Log.LogWarning("[Journal] entries: " + e.Message); }
            return sb.Length > 0 ? sb.ToString() : W.NoObjectives;
        }

        private static void Append(System.Text.StringBuilder sb, string part)
        {
            if (sb.Length > 0) sb.Append(' ');
            sb.Append(part);
            if (!part.EndsWith(".") && !part.EndsWith("!") && !part.EndsWith("?")) sb.Append('.');
        }

        // ---------- Enter: the cell's game-sanctioned action (press-only, owner ruling) ----------

        private static void Activate(string quest, int col)
        {
            switch (col)
            {
                case 0:
                case 1:
                    // Native pull-out for visual sync (the heading's own click).
                    var heading = FindHeadingButton(quest);
                    if (heading != null)
                    {
                        Navigator.Click(heading.gameObject);
                        SpeechService.Say(W.Expanded, Priority.Immediate, "table");
                    }
                    else Plugin.Log.LogInfo("[Journal] heading for '" + quest + "' not found — silent.");
                    return;
                case 2: ToggleTracking(quest); return;
                default: Abandon(quest); return;
            }
        }

        private static void ToggleTracking(string quest)
        {
            EnsureExpanded(quest);
            var toggle = FindRowActionButton(quest, "TRACKING");
            if (toggle == null)
            {
                // The pull-out can render a beat after the heading click — retry
                // once before refusing (a dead Track press was the owner report).
                _pendingTrackQuest = quest;
                _pendingTrackAt = Time.unscaledTime + Timing.JournalRetryDefer;
                return;
            }
            Navigator.Click(toggle.gameObject);
            // Announce the RESULT from the API the pips use (single tracking: one name).
            _announceTrackingAt = Time.unscaledTime + Timing.JournalResultDefer;
        }

        private static float _announceTrackingAt = -1f;
        private static string _pendingTrackQuest;
        private static float _pendingTrackAt = -1f;

        public static void Tick()
        {
            if (_pendingTrackQuest != null && Time.unscaledTime >= _pendingTrackAt)
            {
                string quest = _pendingTrackQuest;
                _pendingTrackQuest = null;
                var toggle = FindRowActionButton(quest, "TRACKING");
                if (toggle != null)
                {
                    Navigator.Click(toggle.gameObject);
                    _announceTrackingAt = Time.unscaledTime + Timing.JournalResultDefer;
                }
                else
                    SpeechService.Say("Track not available.", Priority.Immediate, "table");
            }

            if (_announceTrackingAt < 0 || Time.unscaledTime < _announceTrackingAt) return;
            _announceTrackingAt = -1f;
            string tracked = null;
            try
            {
                foreach (var q in QuestLog.GetAllQuests(QuestState.Active))
                    if (QuestLog.IsQuestTrackingEnabled(q)) { tracked = q; break; }
            }
            catch { }
            SpeechService.Say(tracked != null ? W.TrackingPrefix + tracked + "." : W.TrackingNone,
                Priority.Immediate, "table");
        }

        private static void Abandon(string quest)
        {
            bool abandonable = false;
            try { abandonable = QuestLog.IsQuestAbandonable(quest); } catch { }
            if (!abandonable)
            { SpeechService.Say(W.NotAbandonable, Priority.Immediate, "table"); return; }

            // The abandon button lives in the selected quest's details panel: pull the
            // quest out first if needed, then click its rendered abandon button; the
            // game's own confirmation panel takes over (its buttons stay native).
            var heading = FindHeadingButton(quest);
            if (heading != null) Navigator.Click(heading.gameObject);
            var abandonBtn = FindWindowButton("ABANDON");
            if (abandonBtn != null) Navigator.Click(abandonBtn.gameObject);
            else Plugin.Log.LogInfo("[Journal] abandon button not found — silent.");
        }

        // ---------- Native-object resolution (rendered labels, graceful silence) ----------

        private static Transform WindowRoot()
        {
            var go = GameObject.Find("Letterbox Canvas/Drive System/CS Drive Log");
            return go != null ? go.transform : null;
        }

        private static Button FindHeadingButton(string quest)
        {
            var root = WindowRoot();
            if (root == null) return null;
            foreach (var b in root.GetComponentsInChildren<Button>(false))
            {
                var tmp = b.GetComponentInChildren<TMP_Text>(false);
                if (tmp != null && string.Equals(tmp.text?.Trim(), quest,
                        System.StringComparison.OrdinalIgnoreCase))
                    return b;
            }
            return null;
        }

        /// <summary>The row-local action button (e.g. the tracking toggle) — a labeled
        /// Button inside the same heading template instance.</summary>
        private static Button FindRowActionButton(string quest, string labelFragment)
        {
            var heading = FindHeadingButton(quest);
            if (heading == null) return null;
            var templateRoot = heading.transform.parent;
            if (templateRoot == null) return null;
            foreach (var b in templateRoot.GetComponentsInChildren<Button>(false))
            {
                var tmp = b.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null && tmp.text != null
                    && tmp.text.ToUpperInvariant().Contains(labelFragment))
                    return b;
            }
            return null;
        }

        private static Button FindWindowButton(string labelFragment)
        {
            var root = WindowRoot();
            if (root == null) return null;
            foreach (var b in root.GetComponentsInChildren<Button>(false))
            {
                var tmp = b.GetComponentInChildren<TMP_Text>(true);
                if (tmp != null && tmp.text != null
                    && tmp.text.ToUpperInvariant().Contains(labelFragment))
                    return b;
            }
            return null;
        }
    }
}
