using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>Ambient energy channel (owner ruling, fix-pass session 2026-07-20:
    /// "core functionality"; segments-first reads — the rendered box count, not the
    /// Lua integer).
    ///
    /// Machinery (Energy Bar System FSM, corpus): Energy Getter → Energy Checker →
    /// Energy Setter 0..6 — the checker maps the energy value to the Setter whose
    /// index IS the rendered box count (7 authored levels, max 6). Condition Minus
    /// is the starving path: empty energy converts to condition damage, paired with
    /// the rendered Starving marker.
    ///
    /// Channel rules (ConditionWatch shape): announce on LEVEL CHANGE only — the
    /// loop re-enters setter states every pass, dedupe is load-bearing; first level
    /// after boot is a silent baseline. Starving announce is edge-only. Every
    /// announce logs Setter index + Lua Player_Energy for threshold calibration
    /// (max-boxes wording deferred until the pair confirms live).</summary>
    internal static class EnergyWatch
    {
        private struct PendingSay
        {
            public string Text;
            public float Due;
        }

        private static int _lastLevel = -1;
        private static bool _starving;
        private static readonly List<PendingSay> Queue = new List<PendingSay>();

        public static void Init()
        {
            FsmSignals.Subscribe("Energy Bar System", null, OnState);
        }

        private static void OnState(PlayMakerFSM fsm, string state)
        {
            string trimmed = state.Trim();

            if (trimmed == "Condition Minus")
            {
                if (!_starving)
                {
                    _starving = true;
                    Queue.Add(new PendingSay
                    {
                        Text = "Starving. Energy empty.",
                        Due = Time.unscaledTime + 0.2f,
                    });
                }
                return;
            }

            const string prefix = "Energy Setter ";
            if (!trimmed.StartsWith(prefix)) return;
            if (!int.TryParse(trimmed.Substring(prefix.Length), out int level)) return;

            if (level > 0) _starving = false; // starving edge re-arms on recovery

            if (_lastLevel < 0) { _lastLevel = level; return; } // boot baseline, silent
            if (level == _lastLevel) return;

            bool falling = level < _lastLevel;
            _lastLevel = level;
            Plugin.Log.LogInfo("[EnergyWatch] level " + level
                + " (Lua " + (LuaStore.Energy()?.ToString() ?? "?") + ")");
            Queue.Add(new PendingSay
            {
                // "of 5" per the segments-first ruling (5 rendered boxes; Setter 6
                // = boost overcharge, spoken as "6 of 5" if it ever fires — flag it).
                Text = "Energy " + (falling ? "falling: " : "rising: ") + level + " of 5.",
                Due = Time.unscaledTime + 0.3f,
            });
        }

        public static void Tick()
        {
            for (int i = Queue.Count - 1; i >= 0; i--)
            {
                if (Time.unscaledTime < Queue[i].Due) continue;
                SpeechService.Say(Queue[i].Text, Priority.Queued, "energy");
                Queue.RemoveAt(i);
            }
        }
    }
}
