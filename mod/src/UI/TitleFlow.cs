using CSAccess.Speech;

namespace CSAccess.UI
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
            Substrate.FsmSignals.Subscribe("MAIN MENU", "Landing", (fsm, state) =>
            {
                _announced = true;
                Modality.ModeModel.ForcedTitle = true; // D1
                SpeechService.Say("Main menu. Press Enter to start.", Priority.Queued, "scene");
            });
            Substrate.FsmSignals.Subscribe("MAIN MENU", "Demo Menu", (fsm, state) =>
            {
                // D1: any Demo Menu arrival means we are at the title — the
                // quit-to-title path reaches here without a scene change, and the
                // mode authority must stop reading the ended session's Station.
                Modality.ModeModel.ForcedTitle = true;
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
            Modality.ModeModel.ForcedTitle = false; // D1: a real scene owns truth again
        }
    }
}
