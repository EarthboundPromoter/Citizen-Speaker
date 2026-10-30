using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using Priority = CSAccess.Scaffold.Priority;

using CSAccess.Scaffold;
using CSAccess.Middleware;

namespace CSAccess.Sleeper1
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
        public static void NoteUserNavigation() => _userMoveExpires = Time.unscaledTime + Timing.UserMoveWindow;

        /// <summary>Navigator spoke this element itself (dead-end repeat): the same
        /// object selected again in this frame is an echo (same-frame guard below).</summary>
        public static void NoteSpoken(GameObject go, string description)
        {
            if (go != null) LastAnnounced[go.GetInstanceID()] = Time.unscaledTime;
        }

        // --- Boot-sweep silence (W2 hardening, owner design): after a Unity scene load,
        //     the game walks focus across controls the player can't touch yet. Game-driven
        //     focus stays silent until the player's first input; user navigation always
        //     speaks. Orientation comes from the designed channels (scene line, dialogue
        //     auto-read, L on demand). ---
        private static bool _sceneSettled;

        /// <summary>Has the player taken control of this scene yet (game-driven focus
        /// speaks only after that)?</summary>
        public static bool SceneSettled => _sceneSettled;

        public static void OnSceneChanged() => _sceneSettled = false;

        public static void MarkSettled()
        {
            if (_sceneSettled) return;
            _sceneSettled = true;
            Plugin.Log.LogInfo("[Focus] scene settled (first user input).");
        }

        private static float _lastStripReanchor = -10f;
        private static float _reanchorAt = -1f;
        private static Mode _reanchorMode = Mode.ActionView;

        // BL-10 freshness deferral: the Inventory Display populates a beat after the
        // cursor moves — announcing immediately would read the PREVIOUS item's name.
        // Rapid arrowing collapses to the last cursor (natural debounce).
        private static GameObject _pendingItem;
        private static float _pendingItemAt;
        private static bool _pendingItemUser;

        // Settle idiom (fresh-run F2/F6): game-driven focus arrives in bursts —
        // conversation teardown pinballs across story-unlocked cards (4 events,
        // ~1.3s, live f35510) and picker open fires every die on ONE frame
        // (f64067). Defer game-driven announcements briefly and speak only the
        // burst's endpoint: whatever is STILL selected and alive at settle.
        // Doomed objects (a dying conversation's button) fail the liveness check
        // and drop silently. User navigation is never deferred.
        private const float FocusSettle = 0.25f;
        private static GameObject _pendingFocus;
        private static float _pendingFocusAt;

        /// <summary>From Plugin.Update: scheduled strip-steal recovery + deferred
        /// inventory-item announcements.</summary>
        public static void Tick()
        {
            if (_reanchorAt >= 0 && Time.unscaledTime >= _reanchorAt)
            {
                _reanchorAt = -1f;
                FocusModel.ReAnchor(_reanchorMode);
            }

            if (_pendingFocus != null && Time.unscaledTime >= _pendingFocusAt)
            {
                var go = _pendingFocus;
                _pendingFocus = null;
                if (go != null && go.activeInHierarchy && EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == go)
                {
                    string d = ElementDescriber.Element(go, false);
                    if (!string.IsNullOrEmpty(d))
                        SpeechService.Say(d, Priority.Queued, "focus");
                }
                else if (go != null)
                {
                    Plugin.Log.LogInfo("[Focus] settle-dropped: " + go.name);
                }
            }

            if (_pendingItem != null && Time.unscaledTime >= _pendingItemAt)
            {
                var go = _pendingItem;
                _pendingItem = null;
                if (go != null && go.activeInHierarchy && EventSystem.current != null
                    && EventSystem.current.currentSelectedGameObject == go)
                {
                    string description = ElementDescriber.Element(go, false);
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
            if (!userInitiated && GameQueries.CycleTransitionActive())
            {
                Plugin.Log.LogInfo("[Focus] suppressed (cycle transition): " + selected.name);
                return;
            }

            // Cloud camera flight: the spatial selector re-claims disabled markers
            // mid-flight — machinery, not information (owner-approved mute; the
            // settled selection is announced once by CloudFlight.Tick).
            if (!userInitiated && CloudFlight.Suppressing())
            {
                Plugin.Log.LogInfo("[Focus] suppressed (cloud flight): " + selected.name);
                return;
            }

            // Map table browse/camera moves: the row report is the announcement; the
            // native highlight lands silently (same pattern, second customer).
            if (!userInitiated && MapTable.SuppressingFocus())
            {
                Plugin.Log.LogInfo("[Focus] suppressed (map table): " + selected.name);
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
                && !WindowState.InventoryOpen
                && Util.HasAncestor(selected, "Bottom UI")
                && Util.HasAncestor(selected, "Inventory")
                && Time.unscaledTime - _lastStripReanchor > Timing.StripReanchorCooldown)
            {
                var mode = ModeModel.Current();
                if (mode == Mode.ActionView || mode == Mode.Station)
                {
                    _lastStripReanchor = Time.unscaledTime;
                    _reanchorAt = Time.unscaledTime + Timing.ReanchorDefer;
                    _reanchorMode = mode;
                    Plugin.Log.LogInfo("[Focus] strip steal suppressed (" + selected.name
                        + "): " + mode + " re-anchor scheduled.");
                    return;
                }
            }

            // Dice-allocation focus mute (owner ruling 2026-07-21): during the picker,
            // the mod owns the slot-time speech. Game-driven selection landing OFF the
            // picker — the action card / Dice Slot Button as the die settles — is muted
            // entirely; its useful tail (START ACTION) is re-emitted by the mod at the
            // end of the odds line, in a deterministic order. Scoped to DiceAllocation
            // AND non-Dice-UI selection, so die cursors still announce and no other
            // string in any other mode is affected.
            if (!userInitiated
                && ModeModel.Current() == Mode.DiceAllocation
                && !Util.HasAncestor(selected, "Dice UI"))
            {
                Plugin.Log.LogInfo("[Focus] dice-allocation mute (off-picker): " + selected.name);
                return;
            }

            int id = selected.GetInstanceID();
            float now = Time.unscaledTime;
            // The cooldown exists to de-chatter game-driven reselection ping-pong;
            // user navigation must always speak, no matter how fast it revisits.
            if (!userInitiated &&
                LastAnnounced.TryGetValue(id, out float last) && now - last < ReannounceCooldown)
                return;
            // Same object twice in one frame (the title slot list's Reselect and Set
            // Selected states both select Slot 1, 2026-10-25): one announcement, even
            // for user navigation.
            if (userInitiated && LastAnnounced.TryGetValue(id, out float same) && same == now)
                return;
            LastAnnounced[id] = now;
            if (LastAnnounced.Count > 200) LastAnnounced.Clear();

            // Inventory cursors defer ~0.15s so the Inventory Display has written the
            // NEW item's name before we read it (BL-10; spoken by Tick).
            if (selected.name == "Item Cursor")
            {
                _pendingItem = selected;
                _pendingItemAt = Time.unscaledTime + Timing.ItemNameSettle;
                _pendingItemUser = userInitiated;
                return;
            }

            if (!userInitiated)
            {
                // F2/F6 settle: collapse game-driven bursts to their endpoint;
                // Tick announces only what survives selected + alive.
                _pendingFocus = selected;
                _pendingFocusAt = Time.unscaledTime + FocusSettle;
                return;
            }

            string description = ElementDescriber.Element(selected, false);
            if (string.IsNullOrEmpty(description)) return;

            SpeechService.Say(description, Priority.Immediate, "focus");
        }
    }
}
