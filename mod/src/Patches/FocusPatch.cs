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

            int id = selected.GetInstanceID();
            float now = Time.unscaledTime;
            // The cooldown exists to de-chatter game-driven reselection ping-pong;
            // user navigation must always speak, no matter how fast it revisits.
            if (!userInitiated &&
                LastAnnounced.TryGetValue(id, out float last) && now - last < ReannounceCooldown)
                return;
            LastAnnounced[id] = now;
            if (LastAnnounced.Count > 200) LastAnnounced.Clear();

            string description = Describe.Element(selected, detailed: false);
            if (string.IsNullOrEmpty(description)) return;

            // Game-driven focus must never stomp queued dialogue.
            SpeechService.Say(description, userInitiated ? Priority.Immediate : Priority.Queued, "focus");
        }
    }
}
