using CSAccess.Speech;
using CSAccess.Substrate;

namespace CSAccess.Game
{
    /// <summary>The cycle-transition gate, event-armed (W2 hardening; live finding
    /// 2026-07-19). Polling ActiveStateName != Idle lumped the startup walk
    /// (Load From Save -> Die N -> Idle) in with the real pipeline — misdetected mode,
    /// suppressed intro focus, spoke a bogus summary. Corpus truth: Idle's ONLY exit is
    /// EndCycle -> Cycle, and nothing else enters Cycle — so arming on Cycle entry IS
    /// arming on the designed End Cycle press; boot paths physically cannot arm.</summary>
    internal static class CycleGate
    {
        public static bool Active { get; private set; }

        public static void Init()
        {
            FsmSignals.Subscribe("Cycle Controller", "Cycle", (fsm, state) =>
            {
                Active = true;
                Plugin.Log.LogInfo("[Cycle] pipeline armed (Cycle entry via EndCycle).");
            });
            FsmSignals.Subscribe("Cycle Controller", "Idle", (fsm, state) =>
            {
                if (!Active) return; // startup and load walks end at Idle unarmed
                Active = false;
                string dice = GameQueries.DescribeDice();
                string meters = GameQueries.MetersBrief();
                SpeechService.Say("Cycle complete. " + (dice ?? "") + (meters != null ? " " + meters : ""),
                    Priority.Queued, "cycle");
            });
        }
    }
}
