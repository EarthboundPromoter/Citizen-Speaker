using System.Collections.Generic;
using CSAccess.Speech;
using CSAccess.UI;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using Priority = CSAccess.Speech.Priority;

namespace CSAccess.Patches
{
    [HarmonyPatch(typeof(EventSystem), nameof(EventSystem.SetSelectedGameObject),
        typeof(GameObject), typeof(BaseEventData))]
    internal static class FocusPatch
    {
        /// <summary>The game shuffles selection between the same few objects (Continue vs DATA,
        /// carousel arrows vs START GAME); don't re-announce an object focused again this recently.</summary>
        private const float ReannounceCooldown = 2.5f;

        private static readonly Dictionary<int, float> LastAnnounced = new Dictionary<int, float>();
        private static float _userMoveExpires = -1f;

        /// <summary>Called by Navigator immediately before it moves or sets selection.
        /// Only selection changes we cause ourselves count as user-initiated — Enter and
        /// game-driven reselection must never interrupt the active text block.</summary>
        public static void NoteUserNavigation() => _userMoveExpires = Time.unscaledTime + 0.25f;

        // --- Boot-sweep silence (W2 hardening, owner design): after a Unity scene load,
        //     the game walks focus across controls the player can't touch yet. Game-driven
        //     focus stays silent until the player's first input; user navigation always
        //     speaks. Orientation comes from the designed channels (scene line, dialogue
        //     auto-read, L on demand). ---
        private static bool _sceneSettled;

        public static void OnSceneChanged() => _sceneSettled = false;

        public static void MarkSettled()
        {
            if (_sceneSettled) return;
            _sceneSettled = true;
            Plugin.Log.LogInfo("[Focus] scene settled (first user input).");
        }

        private static float _lastStripReanchor = -10f;
        private static float _reanchorAt = -1f;
        private static Modality.Mode _reanchorMode = Modality.Mode.ActionView;

        // BL-10 freshness deferral: the Inventory Display populates a beat after the
        // cursor moves — announcing immediately would read the PREVIOUS item's name.
        // Rapid arrowing collapses to the last cursor (natural debounce).
        private static GameObject _pendingItem;
        private static float _pendingItemAt;
        private static bool _pendingItemUser;

        /// <summary>From Plugin.Update: scheduled strip-steal recovery + deferred
        /// inventory-item announcements.</summary>
        public static void Tick()
        {
            if (_reanchorAt >= 0 && Time.unscaledTime >= _reanchorAt)
            {
                _reanchorAt = -1f;
                Modality.FocusModel.ReAnchor(_reanchorMode);
            }

            if (_pendingItem != null && Time.unscaledTime >= _pendingItemAt)
            {
                var go = _pendingItem;
                _pendingItem = null;
                if (go != null && go.activeInHierarchy && EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == go)
                {
                    string description = Describe.Element(go, detailed: false);
                    if (!string.IsNullOrEmpty(description))
                        SpeechService.Say(description,
                            _pendingItemUser ? Priority.Immediate : Priority.Queued, "focus");
                }
            }
        }

        /// <summary>Called by Navigator before user-initiated selection so it always announces.</summary>
        public static void ClearCooldown(GameObject go)
        {
            if (go != null) LastAnnounced.Remove(go.GetInstanceID());
        }

        private static void Postfix(GameObject selected)
        {
            if (!Plugin.AnnounceFocus.Value) return;
            if (selected == null) return;

            // Character select has its own review UI; game-driven focus is noise there.
            if (CharacterSelect.IsActive()) return;

            // Scrollbars carry no information.
            if (selected.GetComponent<UnityEngine.UI.Scrollbar>() != null) return;

            // The game re-selects Continue after every advance — never informative.
            if (selected.name == "Continue Button") return;

            bool userInitiated = Time.unscaledTime < _userMoveExpires;

            if (!userInitiated && !_sceneSettled)
            {
                Plugin.Log.LogInfo("[Focus] suppressed (boot sweep): " + selected.name);
                return;
            }

            // The end-cycle pipeline walks focus across every location's slots station-wide
            // (the reset sweep, triage report 3) — machinery, not information. Only the user's
            // own navigation speaks while the Cycle Controller is away from Idle.
            if (!userInitiated && Game.GameQueries.CycleTransitionActive())
            {
                Plugin.Log.LogInfo("[Focus] suppressed (cycle transition): " + selected.name);
                return;
            }

            // Cloud camera flight: the spatial selector re-claims disabled markers
            // mid-flight — machinery, not information (owner-approved mute; the
            // settled selection is announced once by CloudFlight.Tick).
            if (!userInitiated && Modality.CloudFlight.Suppressing())
            {
                Plugin.Log.LogInfo("[Focus] suppressed (cloud flight): " + selected.name);
                return;
            }

            // Strip steal (owner-approved 2026-07-19): game-driven selection landing
            // on the CLOSED inventory strip in the action view is the spatial
            // selector's closest-button artifact (live: die-picker cancel handed
            // focus to DATA Button). Suppress the announcement and fire the view's
            // designed recovery — the same RefocusUI hand-off the game uses. Never
            // the same frame (resync discipline); rate-limited so a recovery that
            // itself lands on the strip cannot loop.
            // Extended to Station (build-queue Q5): the same artifact parked an L
            // query on "DATA button" at the station map — recovery there is the
            // UI-selector Reset, the station's designed re-anchor.
            if (!userInitiated
                && !Modality.WindowState.InventoryOpen
                && Describe.HasAncestor(selected, "Bottom UI")
                && Describe.HasAncestor(selected, "Inventory")
                && Time.unscaledTime - _lastStripReanchor > 1.5f)
            {
                var mode = Modality.ModeModel.Current();
                if (mode == Modality.Mode.ActionView || mode == Modality.Mode.Station)
                {
                    _lastStripReanchor = Time.unscaledTime;
                    _reanchorAt = Time.unscaledTime + 0.1f;
                    _reanchorMode = mode;
                    Plugin.Log.LogInfo("[Focus] strip steal suppressed (" + selected.name
                        + "): " + mode + " re-anchor scheduled.");
                    return;
                }
            }

            int id = selected.GetInstanceID();
            float now = Time.unscaledTime;
            // The cooldown exists to de-chatter game-driven reselection ping-pong;
            // user navigation must always speak, no matter how fast it revisits.
            if (!userInitiated &&
                LastAnnounced.TryGetValue(id, out float last) && now - last < ReannounceCooldown)
                return;
            LastAnnounced[id] = now;
            if (LastAnnounced.Count > 200) LastAnnounced.Clear();

            // Inventory cursors defer ~0.15s so the Inventory Display has written the
            // NEW item's name before we read it (BL-10; spoken by Tick).
            if (selected.name == "Item Cursor")
            {
                _pendingItem = selected;
                _pendingItemAt = Time.unscaledTime + 0.15f;
                _pendingItemUser = userInitiated;
                return;
            }

            string description = Describe.Element(selected, detailed: false);
            if (string.IsNullOrEmpty(description)) return;

            // Game-driven focus must never stomp queued dialogue.
            SpeechService.Say(description, userInitiated ? Priority.Immediate : Priority.Queued, "focus");
        }
    }
}
