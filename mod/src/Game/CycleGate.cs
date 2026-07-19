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
                // Composed cycle-end string (owner design, focus-model row 13):
                // ABSOLUTE TOTALS only, no deltas — degradation is presumed. REVEAL
                // DICE! runs immediately before Idle (corpus), so dice are rendered.
                // Dice tail = the bare-values D string per owner spec (session-5
                // wording gap: the verbose per-die form was never the design).
                string vitals = GameQueries.DescribeVitals();
                string dice = GameQueries.DescribeDiceBrief();
                SpeechService.Say("Cycle ended. " + (vitals ?? "") + (dice != null ? " " + dice : ""),
                    Priority.Queued, "cycle");
                // Node additions/removals speak at the first full-control station
                // moment (after any leading scene-beat dialogue), not here.
                NodeCensus.MarkCycleBoundary();
            });
        }
    }
}
