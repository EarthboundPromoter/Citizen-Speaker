using CSAccess.Speech;
using CSAccess.Substrate;
using CSAccess.UI;
using UnityEngine;

namespace CSAccess.Modality
{
    /// <summary>
    /// Cloud-node camera-flight silence (owner-approved 2026-07-19). Corpus: a cloud
    /// marker's Clicked flow runs Camera Transition -> Active (zoom in) and
    /// CloseAction runs Camera Transition 2 -> UI Camera On -> Idle (pull back);
    /// during both flights the game disables sibling markers and its spatial
    /// selector re-claims them, producing a flurry of stale "X, disabled" focus
    /// announcements. This class mutes game-driven focus for the duration of the
    /// flight (FsmSignals clock on the clicked marker's own states) and announces
    /// the settled selection once afterward. User-initiated navigation still
    /// speaks — an arrow press is never silent.
    ///
    /// Scoped to Hacking UI markers only: station markers share the "Location
    /// Button" name and a similar template, but station commit behavior is a
    /// separate owner ruling — this class must not change it.
    /// </summary>
    internal static class CloudFlight
    {
        /// <summary>Flag leaks are a known trap class — a missed settle state must
        /// never strand silence. The longest observed flight is well under this.</summary>
        private const float MaxFlightSeconds = 3f;

        private const float SettleAnnounceDelay = 0.25f;

        private static PlayMakerFSM _flightFsm;
        private static float _flightExpires = -1f;
        private static float _announceAt = -1f;

        public static void Init()
        {
            FsmSignals.Subscribe("Location Button", "Camera Transition", OnFlightStart);
            FsmSignals.Subscribe("Location Button", "Camera Transition 2", OnFlightStart);
            FsmSignals.Subscribe("Location Button", "Active", OnSettle);
            FsmSignals.Subscribe("Location Button", "UI Camera On", OnSettle);
            FsmSignals.Subscribe("Location Button", "Idle", OnSettle);
        }

        /// <summary>True while a cloud camera flight is running (with timeout backstop).</summary>
        public static bool Suppressing()
            => _flightFsm != null && Time.unscaledTime < _flightExpires;

        private static void OnFlightStart(PlayMakerFSM fsm, string state)
        {
            if (!IsHackingMarker(fsm)) return;
            _flightFsm = fsm;
            _flightExpires = Time.unscaledTime + MaxFlightSeconds;
            _announceAt = -1f;
            // Drop focus chatter already queued from the click moment.
            SpeechService.FlushSource("focus");
            Plugin.Log.LogInfo("[Focus] cloud flight started (" + state + "): focus muted.");
        }

        private static void OnSettle(PlayMakerFSM fsm, string state)
        {
            if (_flightFsm == null || fsm != _flightFsm) return;
            _flightFsm = null;
            _announceAt = Time.unscaledTime + SettleAnnounceDelay;
            Plugin.Log.LogInfo("[Focus] cloud flight settled (" + state + ").");
        }

        /// <summary>From Plugin.Update: releases the timeout backstop and speaks the
        /// settled selection once, after the game has finished placing it.</summary>
        public static void Tick()
        {
            if (_flightFsm != null && Time.unscaledTime >= _flightExpires)
            {
                // Timeout: release and still announce where we landed (graceful).
                _flightFsm = null;
                _announceAt = Time.unscaledTime;
                Plugin.Log.LogInfo("[Focus] cloud flight timeout: focus unmuted.");
            }

            if (_announceAt < 0 || Time.unscaledTime < _announceAt) return;
            _announceAt = -1f;
            // Table idiom (D3): the settled announcement is the card row (zoom-in)
            // or the field row (pull-back), not the native focus description. The
            // native path below survives for Ctrl+X native mode and cloud exits.
            if (CloudTable.AnnounceSettled()) return;
            var current = Navigator.Current();
            if (current == null || !current.activeInHierarchy) return;
            Patches.FocusPatch.ClearCooldown(current);
            string description = Describe.Element(current, detailed: false);
            if (!string.IsNullOrEmpty(description))
                SpeechService.Say(description, Priority.Queued, "focus");
        }

        private static bool IsHackingMarker(PlayMakerFSM fsm)
        {
            if (fsm == null) return false;
            for (var t = fsm.transform; t != null; t = t.parent)
                if (t.name == "Hacking UI") return true;
            return false;
        }
    }
}
