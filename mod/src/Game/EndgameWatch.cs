using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>
    /// Endgame coverage (owner-directed speculative build, session 12; corpus
    /// decode of the two ending controllers). The Ending Controller rests in
    /// END STARTER polling Lua ENDSCENE — ANY other state means the end sequence
    /// is running (the camera-independent endgame dial). Narrative content rides
    /// dialogue (already read); this class covers only the silent machinery:
    ///
    /// - "End sequence." once at Music (the first state past the trigger).
    /// - "Credits. Enter to skip." at Fade / Fade 2 (both flows activate the
    ///   End Credits object there). The scroll itself is a placeholder per owner
    ///   ruling — no active credits reading.
    /// - Skip: the game's own reveal (End Credits FSM, Get Button state) listens
    ///   to Mouse and Joystick ONLY — keyboard cannot reveal the skip button
    ///   natively. Enter mirrors the designed two-step: send the FSM its own
    ///   "SKip On" event (exactly what a joystick press does; the game activates
    ///   AND selects the Button, hiding it again after its 5 s Wait), then a
    ///   second Enter clicks the revealed button (single native dispatch; its
    ///   onClick owns what skipping means).
    /// - "Returning to the main menu." at Reset Game (the leave-flow exit;
    ///   TitleFlow announces the landing). The continue flow's RELOAD reloads
    ///   the station scene, which speaks through the existing scene watcher.
    ///
    /// Flux Ending Controller decodes to pure variable bookkeeping (CHECKER ->
    /// BAD/STANDARD/POS, no UI states) — nothing to speak, no subscription.
    /// All wording provisional; unverifiable until a real ending run (acceptance
    /// notes in the session checklist).
    /// </summary>
    internal static class EndgameWatch
    {
        private static class W
        {
            public const string Begin = "End sequence.";
            public const string Credits = "Credits. Enter to skip.";
            public const string SkipShown = "Skip shown. Enter again to skip.";
            public const string Skipping = "Skipping.";
            public const string ToTitle = "Returning to the main menu.";
        }

        public static void Init()
        {
            FsmSignals.Subscribe("Ending Controller", "Music",
                (fsm, s) => SpeechService.Say(W.Begin, Priority.Queued, "endgame"));
            FsmSignals.Subscribe("Ending Controller", "Fade",
                (fsm, s) => SpeechService.Say(W.Credits, Priority.Queued, "endgame"));
            FsmSignals.Subscribe("Ending Controller", "Fade 2",
                (fsm, s) => SpeechService.Say(W.Credits, Priority.Queued, "endgame"));
            FsmSignals.Subscribe("Ending Controller", "Reset Game",
                (fsm, s) => SpeechService.Say(W.ToTitle, Priority.Queued, "endgame"));
        }

        // ---------- The endgame dial ----------

        private static PlayMakerFSM _controller;
        private static float _nextFind;

        private static PlayMakerFSM Controller()
        {
            if (_controller != null && _controller) return _controller;
            // Bounded re-find: absent at the title scene; never scan per frame.
            if (Time.unscaledTime < _nextFind) return null;
            _nextFind = Time.unscaledTime + 3f;
            _controller = GameQueries.FindFsm("Ending Controller");
            return _controller;
        }

        /// <summary>True from the ENDSCENE trigger until the scene reload:
        /// the controller in any state but its END STARTER resting poll.</summary>
        public static bool EndgameActive()
        {
            var c = Controller();
            return c != null && c.ActiveStateName != "END STARTER";
        }

        // ---------- Skip ----------

        public static void SkipPress()
        {
            var c = Controller();
            if (c == null) return;
            var credits = StationAtlas.FindDeep(c.transform, "End Credits");
            if (credits == null || !credits.gameObject.activeInHierarchy)
            {
                // Pre-credits endgame states (fades, ending camera): nothing to
                // skip yet — graceful silence, log only.
                Plugin.Log.LogInfo("[Endgame] skip pressed before credits — no target.");
                return;
            }
            var button = StationAtlas.FindDeep(credits, "Button");
            if (button != null && button.gameObject.activeInHierarchy)
            {
                SpeechService.Say(W.Skipping, Priority.Immediate, "endgame");
                UI.Navigator.Click(button.gameObject);
                return;
            }
            var creditsFsm = credits.GetComponent<PlayMakerFSM>();
            if (creditsFsm != null)
            {
                creditsFsm.SendEvent("SKip On");
                SpeechService.Say(W.SkipShown, Priority.Immediate, "endgame");
            }
            else
                Plugin.Log.LogInfo("[Endgame] End Credits carries no FSM — skip reveal unavailable.");
        }
    }
}
