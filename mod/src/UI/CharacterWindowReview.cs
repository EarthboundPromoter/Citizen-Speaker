using System.Collections.Generic;
using CSAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>Mod-owned review cursor for the character window (triage report 20).
    /// The window's buttons are Automatic-navigation over a wheel layout — spatial arrowing
    /// is unpredictable and INTUIT can be unreachable — and the only authored links are each
    /// skill's Confirm/Back pair (static nav dump, 2026-07-18). Same pattern as
    /// TutorialReview/CharacterSelect: arrows move a mod cursor, EventSystem selection stays
    /// untouched (the per-widget Checker FSMs own it and win), Enter fires one native
    /// activation on the cursor target.</summary>
    internal static class CharacterWindowReview
    {
        private static Transform _window;
        private static int _index = -1;

        public static bool IsActive()
        {
            var w = Window();
            if (w == null) return false;
            var group = w.GetComponent<CanvasGroup>();
            bool open = w.gameObject.activeInHierarchy && (group == null || group.alpha > 0.5f);
            if (!open) _index = -1;
            return open;
        }

        private static Transform Window()
        {
            if (_window == null)
            {
                var go = GameObject.Find("Letterbox Canvas/Character Window");
                if (go != null) _window = go.transform;
            }
            return _window;
        }

        /// <summary>Actionable buttons in fixed announce order: the main Upgrade button, then
        /// each skill's interactable buttons in SKILL List child order. Recomputed per press,
        /// so the list follows the window's mode (e.g. Confirm buttons appearing after
        /// UPGRADE is pressed).</summary>
        private static List<GameObject> Targets()
        {
            var list = new List<GameObject>();
            var w = Window();
            if (w == null) return list;
            AddIfActionable(list, w.Find("Upgrade Tracker/Top Line/Upgrade UI/Upgrade Button"));
            var skills = w.Find("SKILL List");
            if (skills != null)
                foreach (Transform skill in skills)
                    foreach (var b in skill.GetComponentsInChildren<Button>(false))
                        AddIfActionable(list, b.transform);
            return list;
        }

        private static void AddIfActionable(List<GameObject> list, Transform t)
        {
            if (t == null) return;
            var b = t.GetComponent<Button>();
            if (b == null || !b.IsInteractable() || !t.gameObject.activeInHierarchy) return;
            if (!list.Contains(t.gameObject)) list.Add(t.gameObject);
        }

        public static void Review(int delta)
        {
            var targets = Targets();
            if (targets.Count == 0)
            {
                SpeechService.Say("No available controls.", Priority.Immediate, "nav");
                return;
            }
            _index = _index < 0
                ? (delta > 0 ? 0 : targets.Count - 1)
                : Mathf.Clamp(_index + delta, 0, targets.Count - 1);
            SpeechService.Say(Describe.Element(targets[_index], detailed: false),
                Priority.Immediate, "nav");
        }

        /// <summary>Activate the cursor target; false if no cursor is set (caller falls
        /// through to the game-selection activate).</summary>
        public static bool Activate()
        {
            var targets = Targets();
            if (_index < 0 || _index >= targets.Count) return false;
            Navigator.Click(targets[_index]);
            return true;
        }
    }
}
