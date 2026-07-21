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
            public bool RetryUsed;
        }

        private sealed class LabelWatch
        {
            public Transform Button;
            public string LastLabel;
        }

        private static readonly List<PendingRead> Pending = new List<PendingRead>();
        private static readonly List<LabelWatch> Watches = new List<LabelWatch>();
        private static readonly Dictionary<int, (string Text, float Time)> LastAnnounced =
            new Dictionary<int, (string, float)>();

        public static void Init()
        {
            // Postfix signal: the state's OnEnter actions (incl. the Complete state's
            // button activation) have already run when these fire. Content still animates
            // in, so reads are deferred slightly and Complete is the authoritative pass.
            FsmSignals.Subscribe("Hacking Slots Controller", "Outcome Animation",
                (fsm, state) => Schedule(fsm, 0.35f));
            FsmSignals.Subscribe("Hacking Slots Controller", "Complete",
                (fsm, state) => Schedule(fsm, 0.2f));

            // The hack press (owner ruling, session 11): a matched die re-arms the
            // slot button (Ready state, "Hacking Placed" sound), and pressing it
            // fires Hack -> ANIMATION — the cloud's designed middle stage between
            // placing and starting. Acknowledge with the owner's word. Slots 2/3
            // covered for multi-dice sequence nodes.
            foreach (var slot in new[]
                { "Hacking Dice Slot 1", "Hacking Dice Slot 2", "Hacking Dice Slot 3" })
                FsmSignals.Subscribe(slot, "ANIMATION",
                    (fsm, state) => Speech.SpeechService.Say("Hacked.",
                        Speech.Priority.Immediate, "cloud"));
        }

        private static void Schedule(PlayMakerFSM controller, float delay)
        {
            // Runs inside the game's state switch — record only, read later in Tick.
            Pending.Add(new PendingRead { Controller = controller, Due = Time.unscaledTime + delay });
        }

        public static void Tick()
        {
            for (int i = Pending.Count - 1; i >= 0; i--)
            {
                var p = Pending[i];
                if (Time.unscaledTime < p.Due) continue;
                Pending.RemoveAt(i);
                ReadOutcome(p);
            }

            for (int i = Watches.Count - 1; i >= 0; i--)
            {
                var w = Watches[i];
                if (w.Button == null || !w.Button.gameObject.activeInHierarchy)
                {
                    Watches.RemoveAt(i);
                    continue;
                }
                string label = UI.Describe.FirstText(w.Button.gameObject);
                if (label == null || label == w.LastLabel) continue;
                w.LastLabel = label;
                // The collect press re-renders the button label ("DATA EXTRACTED").
                SpeechService.Say(label + ".", Priority.Queued, "cloud");
                Watches.RemoveAt(i);
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

            AnnounceCollectButton(root, p);
            // Reveal callout (cloud table two-channel design): diff the rendered node
            // set a beat after resolve — gate hacks reveal their corridor neighborhood.
            UI.CloudTable.AfterOutcome();
            if (LastAnnounced.Count > 100) LastAnnounced.Clear();
        }

        /// <summary>The Sequence Complete Button is a required, previously-unannounced
        /// press (BL-13). Announce its rendered label when it is up, then watch that
        /// label for the post-press re-render.</summary>
        private static void AnnounceCollectButton(Transform root, PendingRead p)
        {
            var button = root.Find("Action Elements/Sequence Complete Button");
            if (button == null) return;
            var selectable = button.GetComponent<Selectable>();
            bool up = button.gameObject.activeInHierarchy
                && (selectable == null || selectable.IsInteractable());
            if (!up)
            {
                // Complete's activation may still be animating in — one retry, then
                // graceful silence with a log line (invariant 5).
                if (!p.RetryUsed)
                    Pending.Add(new PendingRead
                        { Controller = p.Controller, Due = Time.unscaledTime + 1f, RetryUsed = true });
                else
                    Plugin.Log.LogInfo("[Cloud] Sequence Complete Button never came up ("
                        + root.name.TrimEnd() + ") — silent.");
                return;
            }

            foreach (var w in Watches)
                if (w.Button == button) return; // already announced + watching

            string label = UI.Describe.FirstText(button.gameObject);
            if (label == null) return;
            SpeechService.Say(label + " button.", Priority.Queued, "cloud");
            Watches.Add(new LabelWatch { Button = button, LastLabel = label });
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
