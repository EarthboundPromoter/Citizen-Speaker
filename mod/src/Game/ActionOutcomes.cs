using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>Station action outcome announcements, event-clocked (BL-8, build-queue
    /// Q3; replaces the Watchers poll's announce path). When an outcome completes a
    /// clock, the game swaps the whole action group to its story-variant canvas
    /// ("Dragos's Yard Actions" → "... Actions 2") — the 0.4s poll saw only a
    /// deactivated controller and stayed silent (WINTER LIGHT 8/8: tier + completion
    /// narrative both lost, owner-flagged live).
    ///
    /// Clock: FsmSignals on the Action Controller outcome states — fires at state
    /// entry, before the swap teardown. The action NAME and tier are captured at the
    /// signal; the card content read is deferred a beat, and if the original card was
    /// torn down by a variant swap, the same-named action is re-found in the active
    /// variant group (rendered names, never objects). Content stays rendered text
    /// (invariant 1).</summary>
    internal static class ActionOutcomes
    {
        private struct Pending
        {
            public string Name;
            public Transform Root;
            public Transform GroupParent;
            public float Due;
        }

        private static readonly List<Pending> Queue = new List<Pending>();

        public static void Init()
        {
            Subscribe("Positive Outcome", "positive outcome");
            Subscribe("Neutral Outcome", "neutral outcome");
            Subscribe("Negative Outcome", "negative outcome");
            // BL-14: the item/value-check family runs a third controller template with
            // one bare Outcome state and NO tier states (DELIVER DATA live case +
            // Solheim Data Action corpus decode) — always silent until now. No tier
            // word: the game renders none (render-honesty).
            Subscribe("Outcome", null);

            // Slot-settle outlook (owner ruling, session 11): after a die lands, the
            // controller's Dice Chance Filter renders one of four tier markers for
            // the BOOSTED value (corpus: <=2 Neg, 3-4 Neu, 5 Pos, 6 Boon) — the
            // retract-or-commit signal the player weighs before starting. Queued so
            // it lands after "Die slotted." Wording provisional.
            SubscribeOutlook("Neg", "Negative possible.");
            SubscribeOutlook("Neu", "Neutral likely.");
            SubscribeOutlook("Pos", "Positive likely.");
            SubscribeOutlook("Boon", "Positive, best odds.");
        }

        private static void SubscribeOutlook(string state, string line)
        {
            FsmSignals.Subscribe("Action Controller", state,
                (fsm, s) => Speech.SpeechService.Say(line, Speech.Priority.Queued, "dice"));
        }

        private static void Subscribe(string state, string spokenTier)
        {
            FsmSignals.Subscribe("Action Controller", state,
                (fsm, s) => Capture(fsm, spokenTier));
            // F14 (run 3): cryo-cost actions run their own controller — ORDER
            // FUNGUS resolved with zero announcements because only "Action
            // Controller" was clocked. Same outcome-state template, same capture
            // (DisabledReason has known this family all along).
            FsmSignals.Subscribe("Action Cryo Controller", state,
                (fsm, s) => Capture(fsm, spokenTier));
        }

        private static void Capture(PlayMakerFSM fsm, string spokenTier)
        {
            var root = UI.Describe.FindActionRoot(fsm.transform);
            string name = root != null
                ? (UI.Describe.TextUnder(root, "Action Name") ?? root.name.TrimEnd())
                : "Action";
            // Tierless (bare Outcome) actions announce name only; the deferred card
            // read carries the content.
            SpeechService.Say(spokenTier != null ? name + ": " + spokenTier + "." : name + ".",
                Priority.Queued, "outcome");
            Queue.Add(new Pending
            {
                Name = name,
                Root = root,
                GroupParent = root != null && root.parent != null ? root.parent.parent : null,
                Due = Time.unscaledTime + 0.15f,
            });
        }

        public static void Tick()
        {
            for (int i = Queue.Count - 1; i >= 0; i--)
            {
                var p = Queue[i];
                if (Time.unscaledTime < p.Due) continue;
                Queue.RemoveAt(i);

                var root = p.Root != null && p.Root.gameObject.activeInHierarchy
                    ? p.Root
                    : ReFind(p.GroupParent, p.Name);
                string extras = root != null ? Watchers.DescribeOutcomeCard(root) : null;
                if (extras != null)
                    SpeechService.Say(extras, Priority.Queued, "outcome");
                else if (root == null)
                    Plugin.Log.LogInfo("[Outcome] card for '" + p.Name
                        + "' gone and not re-found in the active variant — silent.");
                // Location table post-roll callouts (clock ticks, cards flipping
                // activatable) — owner ruling: all changes announced automatically.
                UI.LocationTable.AfterOutcome();
            }
        }

        /// <summary>A variant swap replaces the group but keeps the location parent:
        /// find the active "* Actions" sibling group and the same-named card in it.</summary>
        private static Transform ReFind(Transform groupParent, string name)
        {
            if (groupParent == null) return null;
            foreach (Transform group in groupParent)
            {
                if (!group.gameObject.activeInHierarchy) continue;
                if (!group.name.TrimEnd().Contains(" Actions")) continue;
                foreach (Transform card in group)
                {
                    if (!card.gameObject.activeInHierarchy) continue;
                    string cardName = UI.Describe.TextUnder(card, "Action Name");
                    if (cardName != null && cardName == name) return card;
                }
            }
            return null;
        }
    }
}
