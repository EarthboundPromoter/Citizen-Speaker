using System.Collections.Generic;
using CSAccess.Modality;
using CSAccess.Speech;
using CSAccess.Substrate;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>
    /// The station census (BL-16 redesign, owner design 2026-07-20): spoken
    /// appearance/disappearance callouts for station locations and characters —
    /// the sighted player's "a new marker faded in" for free; twice-motivated live
    /// (SABINE spawned and BRIGHT MARKET vanished, both silent while the table
    /// folded them honestly).
    ///
    /// Identity is the StationAtlas key set — canvas availability FSM states, the
    /// camera-independent dial — so camera/frustum churn can never produce a change
    /// (the trap that killed the first census, BL-16/F3).
    ///
    /// Two channels, owner-ruled:
    /// - Cause-driven notifications, PRESENT tense: population only changes when a
    ///   story beat writes flags (conversation end, action outcome, cycle turnover),
    ///   so the announcement attaches to the tail of the causing beat via OnBeat()
    ///   — behind the beat's own queued speech, never over it, never on dead air,
    ///   never on a keypress.
    /// - N replays the last recorded change, PAST tense, unconditionally. No
    ///   freshness state: tense IS the freshness marker. A missed notification is
    ///   recovered by stumbling on the honest table or by pressing N.
    /// </summary>
    internal static class StationCensus
    {
        private struct Change
        {
            public string Key;
            public string Name;     // display form ("Character: SABINE")
            public bool Appeared;
        }

        // ---------- Wording block (owner calibration lands here; all provisional) ----------
        private static class W
        {
            public const string None = "No changes recorded.";
            public const string LastPrefix = "Last change: ";
            public static string Present(Change c) => c.Name + (c.Appeared ? " has appeared." : " is gone.");
            public static string Past(Change c) => c.Name + (c.Appeared ? " appeared" : " gone");
        }

        private static Dictionary<string, string> _known;                    // null = not baselined
        private static readonly List<Change> Pending = new List<Change>();   // awaiting present-tense flush
        private static readonly List<Change> Last = new List<Change>();      // most recent change event (N)

        private static float _diffDueAt = -1f;       // debounced signal-driven diff
        private static float _beatWindowUntil = -1f; // flush stays live this long past a beat
        private static float _pendingSince = -1f;
        private static bool _staleLogged;

        public static void Init()
        {
            // Population signals: the canvas availability FSMs' own boundary states —
            // "Variables Met*" is the arrival side, "Off"/"Off 2" the removal side.
            // The owner-name filter keeps this to "* Canvas" objects. Frustum flips
            // (Variables Met <-> Off Camera) land here too and diff to nothing: the
            // atlas keys don't care where the camera points.
            foreach (var state in new[]
                { "Variables Met", "Off", "Off 2", "Variables Met Pos", "Variables Met Neg 1" })
                FsmSignals.Subscribe(null, state, OnCanvasSignal);
        }

        private static void OnCanvasSignal(PlayMakerFSM fsm, string state)
        {
            if (fsm == null || fsm.gameObject == null) return;
            if (!fsm.gameObject.name.Contains(" Canvas")) return;
            // Coalesce bursts (a story swap or zone settle flips several canvases):
            // the diff runs once, half a second after the churn quiets.
            _diffDueAt = Time.unscaledTime + 0.5f;
        }

        /// <summary>Scene load: re-baseline silently (cross-load diffs are unknowable).</summary>
        public static void OnSceneChanged()
        {
            _known = null;
            Pending.Clear();
            Last.Clear();
            _diffDueAt = -1f;
            _beatWindowUntil = -1f;
            _pendingSince = -1f;
        }

        /// <summary>A story beat's tail: conversation ended, an outcome resolved, the
        /// cycle settled, or a genuine station-surface arrival. Diff now and open the
        /// flush window — the window absorbs the canvas FSMs' own poll latency (the
        /// beat writes the flag; the canvas flips a beat later).</summary>
        public static void OnBeat()
        {
            if (EndgameWatch.EndgameActive()) return; // stand down (see WorldAtRest)
            _beatWindowUntil = Time.unscaledTime + 3f;
            _flushNotBefore = Time.unscaledTime + 1.5f;
            Diff();
        }

        private static float _flushNotBefore;

        public static void Tick()
        {
            if (_known == null) { TryBaseline(); return; }
            // Signal-due diffs wait for the world to be AT REST (first census ride:
            // a conversation walks its character canvas through unlisted scene
            // states — diffing mid-scene recorded a phantom "-C:DRAGOS"; cycle
            // turnover churns canvases the same way). The due-stamp holds until
            // rest, so no signal is ever lost.
            if (_diffDueAt > 0f && Time.unscaledTime >= _diffDueAt && WorldAtRest())
            {
                _diffDueAt = -1f;
                Diff();
            }
            // Flush inside the beat window only, and not before the settle lead-in:
            // the beat's own diff can see mid-teardown state (the phantom above);
            // the lead-in gives the canvases' return-to-listed a moment to cancel
            // it out of the pending batch before anything speaks. Outside the
            // window a change waits for the next beat or an N press (owner design
            // — no dead-air announcements).
            if (Pending.Count > 0 && Time.unscaledTime < _beatWindowUntil
                && Time.unscaledTime >= _flushNotBefore)
                TryFlush();
            if (Pending.Count > 0 && _pendingSince > 0f && !_staleLogged
                && Time.unscaledTime - _pendingSince > 30f)
            {
                _staleLogged = true;
                Plugin.Log.LogInfo("[Census] change unflushed for 30s (no station beat) — "
                    + "carried by the next beat or N. If this recurs, some beat lacks an OnBeat hook.");
            }
        }

        /// <summary>N: replay the last recorded change, past tense, unconditionally
        /// (owner design). Diffs first — keypress-freshness parity with the tables —
        /// so the answer is never behind the world.</summary>
        public static void SpeakLast()
        {
            if (_known != null) Diff();
            if (Last.Count == 0)
            {
                SpeechService.Say(W.None, Priority.Immediate, "census");
                return;
            }
            var sb = new System.Text.StringBuilder(W.LastPrefix);
            for (int i = 0; i < Last.Count; i++)
                sb.Append(W.Past(Last[i])).Append(i < Last.Count - 1 ? ", " : ".");
            SpeechService.Say(sb.ToString(), Priority.Immediate, "census");
        }

        // ---------- Internals ----------

        private static float _nextBaselineTry;

        private static void TryBaseline()
        {
            // Throttled and surface-gated (live finding, first census boot): the
            // per-frame retry ran StationAtlas.Build at the TITLE scene, where the
            // containers don't exist — two "[Atlas] container not found" log lines
            // per frame for the whole menu dwell. Station-side surfaces only, at
            // most one attempt per 2 s (the save-load window still logs, capped).
            if (Time.unscaledTime < _nextBaselineTry) return;
            _nextBaselineTry = Time.unscaledTime + 2f;
            var surface = ModeModel.Surface();
            if (surface != Mode.Station && surface != Mode.ActionView && surface != Mode.Cloud)
                return;
            var snap = Snapshot();
            if (snap == null) { _candidate = null; return; }
            // Stability requirement (first census ride: baselining the first
            // non-empty snapshot caught the save-load settle at 4 nodes and then
            // recorded the rest of the station coming up as nine phantom
            // "appearances"): the baseline locks only when two consecutive
            // snapshots 2 s apart agree — a station at rest, not one mid-boot.
            if (_candidate != null && SameKeys(_candidate, snap))
            {
                _candidate = null;
                _known = snap;
                Plugin.Log.LogInfo("[Census] baseline: " + snap.Count + " node(s), silent (stable x2).");
                return;
            }
            _candidate = snap;
        }

        private static Dictionary<string, string> _candidate;

        private static bool SameKeys(Dictionary<string, string> a, Dictionary<string, string> b)
        {
            if (a.Count != b.Count) return false;
            foreach (var k in a.Keys)
                if (!b.ContainsKey(k)) return false;
            return true;
        }

        /// <summary>World-truth gate for diffing: conversations walk character
        /// canvases through unlisted scene states and cycle turnover churns the
        /// set — mid-flight reads are not story truth.</summary>
        private static bool WorldAtRest()
        {
            if (Patches.ConversationEvents.ConversationActive) return false;
            // Endgame teardown deactivates the station wholesale (END CREDITS
            // turns 2_Erlin's Eye off) — a mass-removal phantom, never story
            // census. The census stands down for the whole end sequence.
            if (EndgameWatch.EndgameActive()) return false;
            var mode = ModeModel.Current();
            return mode != Mode.CycleTransition && mode != Mode.Autoplay;
        }

        /// <summary>Key -> display name off the atlas (camera-independent by
        /// construction). Null while the scene isn't ready: a real station always
        /// renders locations (session-5 tighten) — never baseline or diff a husk.</summary>
        private static Dictionary<string, string> Snapshot()
        {
            List<StationAtlas.Row> rows;
            try { rows = StationAtlas.Build(); }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[Census] snapshot failed: " + e.Message);
                return null;
            }
            bool anyLocation = false;
            var snap = new Dictionary<string, string>();
            foreach (var r in rows)
            {
                if (!r.IsCharacter) anyLocation = true;
                snap[(r.IsCharacter ? "C:" : "L:") + r.Name] =
                    (r.IsCharacter ? "Character: " : "") + r.Name;
            }
            return anyLocation ? snap : null;
        }

        private static void Diff()
        {
            if (_known == null) return;
            var fresh = Snapshot();
            if (fresh == null) return;

            List<Change> changes = null;
            foreach (var kv in fresh)
                if (!_known.ContainsKey(kv.Key))
                    (changes = changes ?? new List<Change>()).Add(
                        new Change { Key = kv.Key, Name = kv.Value, Appeared = true });
            foreach (var kv in _known)
                if (!fresh.ContainsKey(kv.Key))
                    (changes = changes ?? new List<Change>()).Add(
                        new Change { Key = kv.Key, Name = kv.Value, Appeared = false });
            _known = fresh;
            if (changes == null) return;

            // Fold into the pending batch; an oscillation (appeared then gone before
            // any flush) cancels to nothing rather than speaking a phantom.
            foreach (var c in changes)
            {
                int opposite = Pending.FindIndex(p => p.Key == c.Key && p.Appeared != c.Appeared);
                if (opposite >= 0) Pending.RemoveAt(opposite);
                else Pending.Add(c);
            }
            _pendingSince = Pending.Count > 0
                ? (_pendingSince > 0f ? _pendingSince : Time.unscaledTime) : -1f;
            _staleLogged = _staleLogged && Pending.Count > 0;

            Last.Clear();
            Last.AddRange(changes);
            var log = new System.Text.StringBuilder("[Census] change:");
            foreach (var c in changes)
                log.Append(' ').Append(c.Appeared ? '+' : '-').Append(c.Key);
            Plugin.Log.LogInfo(log.ToString());
        }

        /// <summary>Present-tense flush, gated on mode truth (not silence inference):
        /// station-side surfaces only — overlays, dialogue, transitions and the cloud
        /// all outrank Station/ActionView in Current(), so "quiet" is free. Queued
        /// priority lands it behind the causing beat's own speech.</summary>
        /// <summary>Above this many same-direction changes, the flush batches to a
        /// count (zone audit risk 4: a district gate opening activates dozens of
        /// canvases in one frame — correct, but a name-by-name read at that scale is
        /// a wall of speech; N carries the detail). Wording provisional.</summary>
        private const int BatchThreshold = 6;

        private static void TryFlush()
        {
            if (Pending.Count == 0) return;
            var mode = ModeModel.Current();
            if (mode != Mode.Station && mode != Mode.ActionView) return;
            EmitPending();
        }

        /// <summary>A6 (session-13 ledger): the pause menu is a world-at-rest moment
        /// and modally quiet. Changes recorded but not yet flushed — no qualifying
        /// station beat happened before the player paused — are lost on reload (the
        /// fresh baseline absorbs them, N-replay has no history). Flushing here
        /// rescues them; the pause mode gate in TryFlush would otherwise never let
        /// them out. Called on pause-open.</summary>
        public static void FlushAtPause()
        {
            if (Pending.Count == 0) return;
            EmitPending();
        }

        private static void EmitPending()
        {
            var sb = new System.Text.StringBuilder();
            int appeared = 0, gone = 0;
            foreach (var c in Pending) { if (c.Appeared) appeared++; else gone++; }
            if (appeared > BatchThreshold)
                sb.Append(appeared).Append(" locations have appeared. ");
            if (gone > BatchThreshold)
                sb.Append(gone).Append(" locations are gone. ");
            foreach (var c in Pending)
            {
                if (c.Appeared && appeared > BatchThreshold) continue;
                if (!c.Appeared && gone > BatchThreshold) continue;
                sb.Append(W.Present(c)).Append(' ');
            }
            SpeechService.Say(sb.ToString().TrimEnd(), Priority.Queued, "census");
            Pending.Clear();
            _pendingSince = -1f;
            _staleLogged = false;
        }
    }
}
