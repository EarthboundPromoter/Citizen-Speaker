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

        // Right/Left perk axis on the current skill row (owner ruling 2026-07-19):
        // 0 = the row's own button, 1..2 = perk slots, spoken "Perk: name, description"
        // (read-all for now; Space-buried description is a queued affordance).
        private static int _perkIndex;

        public static bool IsActive()
        {
            var w = Window();
            if (w == null) return false;
            var group = w.GetComponent<CanvasGroup>();
            bool open = w.gameObject.activeInHierarchy && (group == null || group.alpha > 0.5f);
            if (!open) { _index = -1; _perkIndex = 0; }
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
                        // Each row has ONE upgrade button, named for the skill's NEXT
                        // ladder rung (-1 → 0 → Perk 1 → +1 → Perk 2 → +2), e.g.
                        // "Upgrade Button 1" or "Upgrade Button Perk 1" — so no name
                        // filtering: excluding "Perk" names deleted every row whose
                        // next rung is a perk (live regression, this session). The
                        // Right/Left perk REVIEW reads the BG Marker slots and is
                        // independent of which rung the row's button targets.
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
            _perkIndex = 0;
            _index = _index < 0
                ? (delta > 0 ? 0 : targets.Count - 1)
                : Mathf.Clamp(_index + delta, 0, targets.Count - 1);
            SpeechService.Say(Describe.Element(targets[_index], detailed: false),
                Priority.Immediate, "nav");
        }

        /// <summary>Right/Left: step the perk axis on the current skill row. Dead end
        /// (row edge, no perks, non-skill row) = bare repeat, per the owner idiom.</summary>
        public static void Adjust(int delta)
        {
            var targets = Targets();
            if (_index < 0 || _index >= targets.Count) { Review(delta > 0 ? 1 : -1); return; }

            var skill = SkillRowOf(targets[_index]);
            int max = 0;
            if (skill != null)
            {
                if (FindPerk(skill, 1) != null) max = 1;
                if (FindPerk(skill, 2) != null) max = 2;
            }
            _perkIndex = Mathf.Clamp(_perkIndex + delta, 0, max);
            AnnounceCurrent(targets);
        }

        private static void AnnounceCurrent(List<GameObject> targets)
        {
            if (_perkIndex == 0)
            {
                SpeechService.Say(Describe.Element(targets[_index], detailed: false),
                    Priority.Immediate, "nav");
                return;
            }
            var skill = SkillRowOf(targets[_index]);
            var perk = skill != null ? FindPerk(skill, _perkIndex) : null;
            if (perk == null) { _perkIndex = 0; return; }
            string name = TextOf(perk, "Perk Name");
            string desc = TextOf(perk, "Perk Description");
            // Owned = the FG Markers fill layer renders this rung (BG = full ladder,
            // FG = achieved progress; owner ruling: show owned status). NB the FG
            // container name carries a trailing space in the shipped hierarchy.
            var fg = skill.Find("FG Markers /Perk " + _perkIndex);
            if (fg == null) fg = skill.Find("FG Markers/Perk " + _perkIndex);
            bool owned = fg != null && fg.gameObject.activeInHierarchy;
            SpeechService.Say("Perk: " + (string.IsNullOrEmpty(name) ? "unnamed" : name)
                + (owned ? ", owned" : "")
                + (string.IsNullOrEmpty(desc) ? "" : ", " + desc),
                Priority.Immediate, "nav");
        }

        /// <summary>The SKILL List row an element belongs to; null for the main button.</summary>
        private static Transform SkillRowOf(GameObject target)
        {
            for (var t = target.transform; t != null && t.parent != null; t = t.parent)
                if (t.parent.name == "SKILL List") return t;
            return null;
        }

        /// <summary>A perk slot counts as present only when rendered with a name
        /// (render-honesty; empty template slots stay silent).</summary>
        private static Transform FindPerk(Transform skill, int n)
        {
            var perk = skill.Find("BG Markers/Perk " + n);
            if (perk == null || !perk.gameObject.activeInHierarchy) return null;
            return string.IsNullOrEmpty(TextOf(perk, "Perk Name")) ? null : perk;
        }

        private static string TextOf(Transform perk, string child)
        {
            var t = perk.Find(child);
            var tmp = t != null ? t.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
            return tmp != null ? SpeechService.Clean(tmp.text) : null;
        }

        /// <summary>Activate the cursor target; false if no cursor is set (caller falls
        /// through to the game-selection activate). A review-focused perk activates its
        /// own purchase button — the only sanctioned route to a perk buy.</summary>
        public static bool Activate()
        {
            var targets = Targets();
            if (_index < 0 || _index >= targets.Count) return false;

            if (_perkIndex > 0)
            {
                var skill = SkillRowOf(targets[_index]);
                Button perkButton = null;
                if (skill != null)
                    foreach (var b in skill.GetComponentsInChildren<Button>(false))
                        if (b.gameObject.name == "Upgrade Button Perk " + _perkIndex)
                        { perkButton = b; break; }
                if (perkButton != null)
                    Navigator.Click(perkButton.gameObject);
                else
                    SpeechService.Say("Not activatable.", Priority.Immediate, "nav");
                return true;
            }

            Navigator.Click(targets[_index]);
            return true;
        }
    }
}
