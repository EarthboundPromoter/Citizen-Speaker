using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.Substrate;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.Game
{
    /// <summary>Cloud node outcome pipeline (BL-12/BL-13, build-queue Q2). Cloud nodes run
    /// the Hacking Slots Controller template, not the Action Controller, so the station
    /// outcome watcher never fires there — hack resolution, the collect affordance, and
    /// the collect result were all silent (live decode, Node Æ32 2026-07-19).
    ///
    /// Clocks: Hacking Slots Controller state entries (Outcome Animation / Complete) via
    /// FsmSignals, plus a scoped rendered-label watch on the Sequence Complete Button for
    /// the post-press re-render ("COMPLETE SEQUENCE" → "DATA EXTRACTED") — the press has
    /// no proven FSM clock, so the label change itself is the render-paired signal.
    /// Content: the card's rendered OUTCOMES family (same template the station outcome
    /// reader parses) and rendered button labels only (invariant 1).</summary>
    internal static class CloudOutcomes
    {
        private struct PendingRead
        {
            public PlayMakerFSM Controller;
            public float Due;
        }

        private static readonly List<PendingRead> Pending = new List<PendingRead>();
        private static readonly Dictionary<int, (string Text, float Time)> LastAnnounced =
            new Dictionary<int, (string, float)>();

        // Flow affordances (owner composition 2026-07-23): each Enter press announces
        // what it will do — the three-step chain (slot die / hack / extract) was
        // opaque, and the extract press carries the payout, despawn and hunter tick.
        private static float _hackComposeAt = -1f;
        private static float _perkProcAt = -10f;
        private static float _trackerLineAt = -1f;

        /// <summary>Stamped when the hack compose voiced the cryo proc — the rendered
        /// perk notification stands down for it (A4 lane rule; Watchers).</summary>
        internal static float LastCryoComposedAt = -10f;

        public static void Init()
        {
            // Postfix signal: the state's OnEnter actions have already run when these
            // fire. Content still animates in, so reads are deferred slightly and
            // Complete is the authoritative pass. Complete is also the payout state
            // (corpus 2026-07-23) — the tracker tick lands there, spoken as a queued
            // tail after the outcome read.
            FsmSignals.Subscribe("Hacking Slots Controller", "Outcome Animation",
                (fsm, state) => Schedule(fsm, 0.35f));
            FsmSignals.Subscribe("Hacking Slots Controller", "Complete",
                (fsm, state) => { Schedule(fsm, 0.2f); _trackerLineAt = Time.unscaledTime + 0.6f; });

            foreach (var slot in new[]
                { "Hacking Dice Slot 1", "Hacking Dice Slot 2", "Hacking Dice Slot 3" })
            {
                // A matched die re-arms the slot button (Ready, "Hacking Placed"
                // sound): queue the affordance behind the picker's "Die slotted.".
                FsmSignals.Subscribe(slot, "Ready",
                    (fsm, state) => Speech.SpeechService.Say("Enter to hack.",
                        Speech.Priority.Queued, "cloud"));
                // The hack press fires Hack -> ANIMATION. The acknowledgment defers
                // one beat so the Transfer Intercept proc (same-beat Perk Manager
                // notify) can ride the SAME composed string — no overlap.
                FsmSignals.Subscribe(slot, "ANIMATION",
                    (fsm, state) => _hackComposeAt = Time.unscaledTime + 0.3f);
            }

            // The INTERFACE perk's success state is the proc clock (corpus: chance
            // gate -> PERK SUCCESS adds 5 to Player_Bits).
            FsmSignals.Subscribe("INTERFACE - Transfer Intercept", "PERK SUCCESS",
                (fsm, state) => _perkProcAt = Time.unscaledTime);
        }

        private static void Schedule(PlayMakerFSM controller, float delay)
        {
            // Runs inside the game's state switch — record only, read later in Tick.
            Pending.Add(new PendingRead { Controller = controller, Due = Time.unscaledTime + delay });
        }

        public static void Tick()
        {
            // The hack acknowledgment, composed one beat after ANIMATION: proc line
            // folded in when Transfer Intercept fired this beat, affordance tail
            // always (owner composition 2026-07-23; wording provisional).
            if (_hackComposeAt > 0 && Time.unscaledTime >= _hackComposeAt)
            {
                _hackComposeAt = -1f;
                bool proc = Time.unscaledTime - _perkProcAt < 1.5f;
                if (proc) LastCryoComposedAt = Time.unscaledTime;
                SpeechService.Say("Hacked." + (proc ? " Plus 5 cryo." : "")
                    + " Enter to extract data.", Priority.Immediate, "cloud");
            }

            // The tracker tick, after the extract's outcome read: the post-increment
            // value the dial renders, in the clock idiom. Silent when no tracker
            // renders (retired, or the district has none).
            if (_trackerLineAt > 0 && Time.unscaledTime >= _trackerLineAt)
            {
                _trackerLineAt = -1f;
                string line = GameQueries.TrackerLine();
                if (line != null) SpeechService.Say(line, Priority.Queued, "cloud");
            }

            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var p = Pending[i];
                if (Time.unscaledTime < p.Due) continue;
                Pending.RemoveAt(i);
                ReadOutcome(p);
            }
        }

        private static void ReadOutcome(PendingRead p)
        {
            if (p.Controller == null || p.Controller.gameObject == null) return;
            var root = UI.Describe.FindActionRoot(p.Controller.transform);
            if (root == null)
            {
                Plugin.Log.LogInfo("[Cloud] outcome fired but no action root ("
                    + p.Controller.gameObject.name + ") — silent.");
                return;
            }

            string spokenName = UI.Describe.TextUnder(root, "Action Name") ?? root.name.TrimEnd();
            string tier = VisibleOutcomeType(root);
            string extras = Watchers.DescribeOutcomeCard(root);

            var parts = new List<string>();
            if (tier != null) parts.Add(spokenName + ": " + tier);
            if (extras != null) parts.Add(extras);
            if (parts.Count > 0)
            {
                string composed = string.Join(". ", parts) + ".";
                int id = root.GetInstanceID();
                // Two clocks fire per resolve (Outcome Animation, then Complete) —
                // announce identical content once.
                if (!LastAnnounced.TryGetValue(id, out var last)
                    || last.Text != composed || Time.unscaledTime - last.Time > 6f)
                {
                    LastAnnounced[id] = (composed, Time.unscaledTime);
                    SpeechService.Say(composed, Priority.Queued, "cloud");
                }
            }

            // The old post-outcome collect-button announcer is gone (2026-07-23): the
            // button's lifetime is BEFORE the outcome (controller Active -> press ->
            // Hacking deactivates it), so the check here could only ever log "never
            // came up" — the wrong-clock artifact behind session-13's mistaken
            // "vestigial" closure. The press affordance now rides the hack compose.
            // Reveal callout (cloud table two-channel design): diff the rendered node
            // set a beat after resolve — gate hacks reveal their corridor neighborhood.
            UI.CloudTable.AfterOutcome();
            if (LastAnnounced.Count > 100) LastAnnounced.Clear();
        }

        /// <summary>The rendered outcome tier text ("NEUTRAL OUTCOME", ...) — the OUTCOMES
        /// family renders one Outcome Type per tier, the live one at full alpha (same
        /// visibility rule as the station effect-line reader).</summary>
        private static string VisibleOutcomeType(Transform root)
        {
            var outcomes = root.Find("OUTCOMES");
            if (outcomes == null) return null;
            foreach (var tmp in outcomes.GetComponentsInChildren<TMP_Text>(false))
            {
                bool typed = false;
                for (var cur = tmp.transform; cur != null && cur != outcomes; cur = cur.parent)
                    if (cur.name == "Outcome Type") { typed = true; break; }
                if (!typed) continue;
                if (Watchers.AlphaUpTo(tmp.transform, root) < 0.5f) continue;
                string txt = tmp.text?.Trim();
                if (!string.IsNullOrEmpty(txt)) return txt;
            }
            return null;
        }
    }
}
