
using CSAccess.Scaffold;
using CSAccess.Middleware;

namespace CSAccess.Sleeper1
{
    /// <summary>
    /// Title arrival announcements (owner ruling 2026-07-20). The old scene-load
    /// "Main menu." spoke over the company crawl: the MAIN TITLE scene loads while
    /// the ENGINE splash still owns the screen — the MAIN MENU FSM literally waits
    /// in SplashScreen on WaitForSplashToFinish, whose only exit is FINISHED (no
    /// input transition: the crawl is not skippable). Announce on the FSM's own
    /// arrivals instead:
    ///  - Landing (first boot, after the splash): the press-to-start canvas. Its
    ///    rendered prompt is a localized "click to start" for mouse and a glyph in
    ///    gamepad mode (which the mod forces) — transcoded to the key we drive.
    ///  - Demo Menu without a Landing this scene visit (return-to-title path, which
    ///    skips splash and landing entirely): the menu itself.
    /// </summary>
    internal static class TitleFlow
    {
        private static bool _announced;

        public static void Init()
        {
            FsmSignals.Subscribe("MAIN MENU", "Landing", (fsm, state) =>
            {
                _announced = true;
                ModeModel.ForcedTitle = true; // D1
                // Once the player has pressed a key, the gate's own focus read says
                // "Press Enter to start" (Describe); before that focus is silent, so
                // the instruction rides this line (2026-10-27).
                SpeechService.Say(FocusPatch.SceneSettled ? "Main menu." : "Main menu. Press Enter to start.",
                    Priority.Queued, "scene");
            });
            FsmSignals.Subscribe("MAIN MENU", "Demo Menu", (fsm, state) =>
            {
                // D1: any Demo Menu arrival means we are at the title — the
                // quit-to-title path reaches here without a scene change, and the
                // mode authority must stop reading the ended session's Station.
                ModeModel.ForcedTitle = true;
                // Demo Menu re-enters constantly inside the menu (Back, language,
                // profile); only the first arrival per scene visit orients.
                if (_announced) return;
                _announced = true;
                SpeechService.Say("Main menu.", Priority.Queued, "scene");
            });
        }

        public static void OnSceneChanged()
        {
            _announced = false;
            ModeModel.ForcedTitle = false; // D1: a real scene owns truth again
        }

        // MAIN MENU states whose Back transition is a menu step back (live decode
        // 2026-10-25): Class Select -> slot list, New Game / Continue (slot lists) ->
        // menu, Warning N (overwrite prompt) -> slot list, Language -> menu. Other
        // states also carry a Back event (User Profile, Slot N Load, Check Save Slot)
        // but there it means a failed profile or load, so it is never sent there.
        private static readonly string[] BackStates =
            { "Class Select", "New Game", "Continue", "Warning 1", "Warning 2", "Warning 3", "Language" };

        /// <summary>Backspace at the title: the game's own Back, which it reads from the
        /// Rewired "Back" action — bound to the right mouse button and the gamepad only,
        /// so keyboard players had no way back. Returns false where Back means nothing.</summary>
        public static bool Back()
        {
            var menu = GameQueries.FindFsm("MAIN MENU");
            string state = menu != null ? menu.ActiveStateName : null;
            if (state == null || System.Array.IndexOf(BackStates, state) < 0) return false;
            // The menu it lands on selects its first button synchronously inside this
            // event; that selection is the player's doing and must speak.
            FocusPatch.NoteUserNavigation();
            menu.SendEvent("Back");
            return true;
        }
    }
}
