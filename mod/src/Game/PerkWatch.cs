using CSAccess.Speech;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>
    /// Perk proc announcements (owner-approved 2026-07-20). Four perk effects fire
    /// graphics-only notification animators that live under Perks Manager and
    /// Inventory Data — not the notification canvas, so the notification watcher
    /// never sees them. FsmSignals clocks the FSMs' own success states; the words
    /// prefer the notification's rendered text, falling back to the decoded effect
    /// (corpus 2026-07-20: Thrill Seeker chance of +20 energy after Engage actions,
    /// Efficient Extractor chance of +1 scrap component after Engineer actions,
    /// Transfer Intercept chance of +5 cryo after Interface actions, Icebreaker
    /// doubles agent data). All procs suspend game-side while the skill is broken.
    /// </summary>
    internal static class PerkWatch
    {
        public static void Init()
        {
            Substrate.FsmSignals.Subscribe("ENGAGE - Thrill Seeker", "PERK SUCCESS",
                (f, s) => Announce("ENGAGE - Thrill Seeker Notification",
                    "Perk Thrill Seeker: energy up."));
            Substrate.FsmSignals.Subscribe("ENGINEER - Efficient Extractor", "PERK SUCCESS",
                (f, s) => Announce("ENGINEER - Efficient Extractor Notification",
                    "Perk Efficient Extractor: 1 scrap component gained."));
            Substrate.FsmSignals.Subscribe("INTERFACE - Transfer Intercept", "PERK SUCCESS",
                (f, s) => Announce("INTERFACE - Transfer Intercept Notification (1)",
                    "Perk Transfer Intercept: 5 cryo gained."));
            // Icebreaker procs inside the agent RNG rolls — Regular 2 / Rare 2 are
            // the perked branches (the unperked ones are Regular / Rare).
            foreach (var agent in new[] { "RNG Havenage Agent", "RNG Yatagan Agent" })
                foreach (var state in new[] { "Regular 2", "Rare 2" })
                    Substrate.FsmSignals.Subscribe(agent, state,
                        (f, s) => Announce("INTERFACE - Icebreaker Notification",
                            "Perk Icebreaker: extra data gained."));
        }

        /// <summary>Rendered-first: speak the notification's own text when it carries
        /// any; the decoded effect line otherwise (the animator may not have shown
        /// the object yet at state entry — the fallback is corpus truth).</summary>
        private static void Announce(string notifyName, string fallback)
        {
            string line = fallback;
            var notify = GameObject.Find(notifyName);
            if (notify != null)
            {
                var texts = new System.Collections.Generic.List<string>();
                foreach (var tmp in notify.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    string t = tmp.text != null ? tmp.text.Trim() : null;
                    if (!string.IsNullOrEmpty(t)) texts.Add(SpeechService.Clean(t));
                }
                if (texts.Count > 0) line = string.Join(", ", texts) + ".";
            }
            SpeechService.Say(line, Priority.Queued, "perk");
        }
    }
}
