using System.Collections.Generic;
using CSAccess.Substrate;
using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace CSAccess.Modality
{
    /// <summary>
    /// Event-driven window/strip open truth (W3, focus-model.md; H commitment 2).
    /// Mode authority reads these flags instead of polling CanvasGroup alphas —
    /// a window is "open" because its own FSM entered its open state (heard via
    /// FsmSignals), not because an alpha happens to sit at 1 (incident 7: the
    /// drive log read falsely open from boot on a fresh game).
    ///
    /// Drive log additionally has framework boot-truth: the window carries the
    /// Dialogue System's own QuestLogWindow, whose public isOpen is authoritative
    /// at any moment including before any FSM state change this session.
    ///
    /// The old alpha reads survive only as a divergence diagnostic (the
    /// conversation-events pattern): disagreement is logged, never trusted.
    /// </summary>
    internal static class WindowState
    {
        public static bool CharacterWindowOpen { get; private set; }
        public static bool InventoryOpen { get; private set; }

        private static bool _driveLogEventOpen;

        private static QuestLogWindow _questLog;
        private static float _questLogNextSearch;

        public static bool DriveLogOpen
        {
            get
            {
                var qlw = QuestLog();
                if (qlw != null) return qlw.isOpen;
                return _driveLogEventOpen;
            }
        }

        public static void Init()
        {
            TrackAutoplayStates();

            // Character UI Button: state "Open" opens; close route runs "Reset" then
            // conditionally "Gamepad UI" (brief E, character-window verdict).
            FsmSignals.Subscribe("Character UI Button", null, (fsm, state) =>
            {
                switch (state)
                {
                    case "Open": CharacterWindowOpen = true; break;
                    case "Reset":
                    case "Gamepad UI": CharacterWindowOpen = false; break;
                }
            });

            // Drive Log Button: "Open" is the open resting state; "Close" tears down,
            // "Idle" is closed resting (corpus, this session's decode).
            FsmSignals.Subscribe("Drive Log Button", null, (fsm, state) =>
            {
                switch (state)
                {
                    case "Open": _driveLogEventOpen = true; break;
                    case "Close":
                    case "Idle": _driveLogEventOpen = false; break;
                }
            });

            // Inventory strip root FSM: Item 4/Data 4 (and the 5-family platform
            // variants) are the open states; the plain/2/3 families are closed
            // (corpus decode, focus-model row 11). Resolves the parked session-3
            // inventory open-signal probe.
            FsmSignals.Subscribe("Inventory", null, (fsm, state) =>
            {
                switch (state)
                {
                    case "Item 4":
                    case "Data 4":
                    case "Item 5":
                    case "Data 5": InventoryOpen = true; break;
                    case "Item":
                    case "Data":
                    case "Item 2":
                    case "Data 2":
                    case "Item 3":
                    case "Data 3": InventoryOpen = false; break;
                }
            });
        }

        // ---------- Autoplay scene tracking (session-5 trap fix) ----------
        // The Autoplay Waiting global is a scheduling flag with a designed leak
        // (Autoplay Wait's "Check Variables -> Off" exit never clears it) — it
        // stranded the mode in listening twice in one session. The scene FSMs'
        // OWN states cannot strand: a scene is pending exactly while some FSM
        // sits in Autoplay Wait / Autoplay; leaving by ANY route exits the set.

        private static readonly HashSet<PlayMakerFSM> AutoplayFsms = new HashSet<PlayMakerFSM>();

        public static bool AutoplayScenePending => AutoplayFsms.Count > 0;

        private static void TrackAutoplayStates()
        {
            FsmSignals.Subscribe(null, null, (fsm, state) =>
            {
                if (state == "Autoplay Wait" || state == "Autoplay")
                    AutoplayFsms.Add(fsm);
                else if (AutoplayFsms.Count > 0)
                    AutoplayFsms.Remove(fsm);
            });
        }

        /// <summary>Windows cannot be open across a scene load; flags reset with it.</summary>
        public static void OnSceneChanged()
        {
            CharacterWindowOpen = false;
            InventoryOpen = false;
            _driveLogEventOpen = false;
            _questLog = null;
            AutoplayFsms.Clear();
        }

        private static QuestLogWindow QuestLog()
        {
            if (_questLog != null) return _questLog;
            if (Time.unscaledTime < _questLogNextSearch) return null;
            _questLogNextSearch = Time.unscaledTime + 5f;
            var go = GameObject.Find("Letterbox Canvas/Drive System/CS Drive Log");
            if (go != null) _questLog = go.GetComponent<QuestLogWindow>();
            return _questLog;
        }

        // ---------- Divergence diagnostic (alpha demoted per H; drop after clean sessions) ----------

        private static float _nextDivergenceCheck;
        private static string _lastDivergence;

        public static void DivergenceTick()
        {
            if (Time.unscaledTime < _nextDivergenceCheck) return;
            _nextDivergenceCheck = Time.unscaledTime + 5f;

            string report = null;
            bool charAlpha = AlphaOpen("Letterbox Canvas/Character Window");
            if (charAlpha != CharacterWindowOpen)
                report = "character window alpha=" + charAlpha + " event=" + CharacterWindowOpen;
            bool driveAlpha = AlphaOpen("Letterbox Canvas/Drive System/CS Drive Log");
            if (driveAlpha != DriveLogOpen)
                report = (report == null ? "" : report + "; ") + "drive log alpha=" + driveAlpha + " truth=" + DriveLogOpen;

            if (report != null && report != _lastDivergence)
            {
                _lastDivergence = report;
                Plugin.Log.LogInfo("[WindowState] DIVERGENCE (alpha vs truth): " + report);
            }
            else if (report == null)
                _lastDivergence = null;
        }

        private static bool AlphaOpen(string path)
        {
            var go = GameObject.Find(path);
            if (go == null || !go.activeInHierarchy) return false;
            var group = go.GetComponent<CanvasGroup>();
            return group == null || group.alpha > 0.5f;
        }
    }
}
