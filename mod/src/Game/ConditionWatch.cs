using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>Condition band transitions + breakdown events (owner-approved design
    /// 2026-07-20, built speculatively from the corpus decode ahead of a live break).
    ///
    /// Machinery (Condition System FSM): a continuous loop reads Player_Condition and
    /// bands it — Dying &lt;21 / Declining &lt;41 / Fading &lt;61 / Flickering &lt;81 / Stable —
    /// each band state rendering its localized word into the bar label ($Breaking).
    /// Inside Dying, condition &lt;2 arms the breakdown chain: once-per-cycle gate
    /// (BREAKDOWN_CYCLE), five-broken death check (C_BREAKDOWNS), then a RANDOM skill
    /// (fall-through past already-broken) — the skill state sets its _BROKEN flag,
    /// fires its rendered BROKEN glyph, and USE ALL DICE wipes the remaining pool.
    ///
    /// Channel rules: band announcements fire on band CHANGE only (the loop re-enters
    /// band states every pass — dedupe is load-bearing); the first band after boot is
    /// a silent baseline. Breakdown states are one-shot by nature. Wording provisional.</summary>
    internal static class ConditionWatch
    {
        private static readonly string[] BandOrder =
            { "Dying", "Declining", "Fading", "Flickering", "Stable" };

        // State-name → skill word (NB "Interface Breakdown " ships with a trailing
        // space in the corpus — names are trimmed before lookup).
        private static readonly Dictionary<string, string> BreakStates =
            new Dictionary<string, string>
            {
                { "Endure Breakdown", "ENDURE" },
                { "Engage Breakdown", "ENGAGE" },
                { "Engineer Breakdown", "ENGINEER" },
                { "Intuit Breakdown", "INTUIT" },
                { "Interface Breakdown", "INTERFACE" },
            };

        private struct PendingSay
        {
            public string Text;
            public float Due;
        }

        private static string _lastBand;
        private static readonly List<PendingSay> Queue = new List<PendingSay>();

        public static void Init()
        {
            FsmSignals.Subscribe("Condition System", null, OnState);
        }

        private static void OnState(PlayMakerFSM fsm, string state)
        {
            string trimmed = state.Trim();

            if (BreakStates.TryGetValue(trimmed, out string skill))
            {
                Queue.Add(new PendingSay
                {
                    Text = "Breakdown. " + skill + " broken. All dice spent.",
                    Due = Time.unscaledTime + 0.2f,
                });
                return;
            }
            if (trimmed == "Dead")
            {
                Queue.Add(new PendingSay
                {
                    Text = "Breakdown. All five skills broken.",
                    Due = Time.unscaledTime + 0.2f,
                });
                return;
            }

            int paren = trimmed.IndexOf(" (");
            string band = paren > 0 ? trimmed.Substring(0, paren) : null;
            if (band == null || System.Array.IndexOf(BandOrder, band) < 0) return;
            if (_lastBand == null) { _lastBand = band; return; } // boot baseline, silent
            if (band == _lastBand) return;

            bool falling = System.Array.IndexOf(BandOrder, band)
                < System.Array.IndexOf(BandOrder, _lastBand);
            _lastBand = band;
            // The band state renders its localized word into $Breaking — prefer the
            // rendered word, fall back to the state-name word.
            string word = fsm.FsmVariables.GetFsmString("Breaking")?.Value?.Trim();
            if (string.IsNullOrEmpty(word)) word = band;
            // Segments-first (owner ruling 2026-07-20): box count rides with the
            // band word — 20 boxes at 5 points, from the same FSM's value var.
            var cv = fsm.FsmVariables.GetFsmFloat("Player Condition");
            string boxes = cv != null
                ? GameQueries.ConditionBoxes(Mathf.RoundToInt(cv.Value)) + ", " : "";
            Queue.Add(new PendingSay
            {
                Text = "Condition " + (falling ? "falling: " : "improving: ")
                    + boxes + word.ToLowerInvariant() + ".",
                Due = Time.unscaledTime + 0.3f, // let the localized label land
            });
        }

        public static void Tick()
        {
            for (int i = Queue.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < Queue[i].Due) continue;
                SpeechService.Say(Queue[i].Text, Priority.Queued, "condition");
                Queue.RemoveAt(i);
            }
        }
    }
}
