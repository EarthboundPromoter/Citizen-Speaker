using System.Collections.Generic;
using TMPro;
using UnityEngine;

using CSAccess.Scaffold;
using CSAccess.Middleware;

namespace CSAccess.Sleeper1
{
    /// <summary>Explorable review of the character-creation carousel.
    /// Up/Down step through the centered class's details; Left/Right change class; Enter starts.
    ///
    /// The carousel is the Moving Panel FSM (live decode 2026-10-25): one state per class
    /// plus four Transition states that tween the panel and FINISH into the neighbour.
    /// LEFT/RIGHT events exist only where a neighbour exists (MACHINIST has no LEFT,
    /// EXTRACTOR no RIGHT), and both arrow buttons are hidden while a transition runs.
    /// An arrow click moves the FSM into its Transition synchronously, so the destination
    /// is known at the key press and is spoken then, not after the ~0.5 s slide.</summary>
    internal static class CharacterSelect
    {
        private static int _reviewIndex = -1;

        /// <summary>The class last announced, by a key press or by the carousel watcher.
        /// Null while the screen is closed, so the next entry speaks the full intro.</summary>
        internal static string LastSpoken;

        public static GameObject Canvas()
        {
            var canvas = GameObject.Find("MAIN MENU/Demo Menu/Character Select Canvas");
            return canvas != null && canvas.activeInHierarchy ? canvas : null;
        }

        public static bool IsActive() => Canvas() != null;

        public static void ResetReview() => _reviewIndex = -1;

        public static Transform CenteredClass()
        {
            var canvas = Canvas();
            if (canvas == null) return null;
            var movingPanel = canvas.transform.Find("Moving Panel");
            if (movingPanel == null) return null;

            Transform best = null;
            float bestDistance = float.MaxValue;
            float centerX = canvas.transform.position.x;
            foreach (Transform child in movingPanel)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (System.Array.IndexOf(GameVocab.ClassNames, child.name) < 0) continue;
                float d = Mathf.Abs(child.position.x - centerX);
                if (d < bestDistance) { bestDistance = d; best = child; }
            }
            return best;
        }

        public static void Review(int delta)
        {
            var panel = CenteredClass();
            if (panel == null) return;
            var items = BuildItems(panel);
            if (items.Count == 0) return;
            int newIndex = Mathf.Clamp(_reviewIndex + delta, 0, items.Count - 1);
            string boundary = "";
            if (newIndex == _reviewIndex)
                boundary = delta > 0 ? "End of list. " : "Top of list. ";
            _reviewIndex = newIndex;
            SpeechService.Say(boundary + items[_reviewIndex], Priority.Immediate, "class");
        }

        public static PlayMakerFSM CarouselFsm()
        {
            var canvas = Canvas();
            var panel = canvas != null ? canvas.transform.Find("Moving Panel") : null;
            return panel != null ? panel.GetComponent<PlayMakerFSM>() : null;
        }

        private static bool IsClass(string name)
            => name != null && System.Array.IndexOf(GameVocab.ClassNames, name) >= 0;

        /// <summary>The class the carousel rests on, or the one a running slide is heading to.</summary>
        public static string Destination(PlayMakerFSM fsm)
        {
            string state = fsm != null ? fsm.ActiveStateName : null;
            if (state == null) return null;
            return IsClass(state) ? state : FinishedTarget(fsm, state);
        }

        /// <summary>Where LEFT or RIGHT leads from a class state; null at the edge.</summary>
        private static string Neighbour(PlayMakerFSM fsm, string classState, string eventName)
        {
            var state = fsm.Fsm.GetState(classState);
            if (state == null) return null;
            foreach (var t in state.Transitions)
                if (t.EventName == eventName)
                    return IsClass(t.ToState) ? t.ToState : FinishedTarget(fsm, t.ToState);
            return null;
        }

        private static string FinishedTarget(PlayMakerFSM fsm, string stateName)
        {
            var state = fsm.Fsm.GetState(stateName);
            if (state == null) return null;
            foreach (var t in state.Transitions)
                if (t.EventName == "FINISHED" && IsClass(t.ToState)) return t.ToState;
            return null;
        }

        /// <summary>1-based position from the left, read from the carousel's own layout
        /// (live 2026-10-25: MACHINIST, OPERATOR, EXTRACTOR), so Left always counts down.</summary>
        public static int Ordinal(string className)
        {
            var canvas = Canvas();
            var panel = canvas != null ? canvas.transform.Find("Moving Panel") : null;
            var self = panel != null ? panel.Find(className) : null;
            if (self == null) return System.Array.IndexOf(GameVocab.ClassNames, className) + 1;
            int rank = 1;
            foreach (Transform child in panel)
                if (child != self && IsClass(child.name) && child.localPosition.x < self.localPosition.x)
                    rank++;
            return rank;
        }

        private static string Position(string className)
            => "class " + Ordinal(className) + " of " + GameVocab.ClassNames.Length;

        /// <summary>Speak a class with its position. The intro form is the screen's entry read.</summary>
        internal static void Announce(string className, bool intro, string prefix = "")
        {
            LastSpoken = className;
            ResetReview();
            SpeechService.Say(prefix + className + ", " + Position(className)
                    + (intro ? ". Up and Down to review details, Left and Right to change class, Enter to start." : ""),
                Priority.Immediate, "class");
        }

        /// <summary>Left/Right. The carousel arrows are plain buttons with no keyboard path;
        /// click them, and speak the destination at the press. Every press speaks: at an
        /// edge (arrow hidden, no FSM transition) the current class with an edge cue, and
        /// mid-slide (both arrows hidden, input ignored by the game) where it is heading.</summary>
        public static void ChangeClass(bool right)
        {
            var canvas = Canvas();
            if (canvas == null) return;
            var arrow = canvas.transform.Find(right ? "RIGHT" : "LEFT");
            var fsm = CarouselFsm();
            if (fsm == null)
            {
                // Unknown carousel shape: the old path, the watcher speaks when it lands.
                if (arrow != null && arrow.gameObject.activeInHierarchy)
                    Navigator.Click(arrow.gameObject);
                return;
            }

            string state = fsm.ActiveStateName;
            if (!IsClass(state))
            {
                string heading = Destination(fsm);
                if (heading != null) Announce(heading, false);
                return;
            }

            string target = Neighbour(fsm, state, right ? "RIGHT" : "LEFT");
            if (target == null || arrow == null || !arrow.gameObject.activeInHierarchy)
            {
                Announce(state, false, target == null ? (right ? "Last class. " : "First class. ") : "");
                return;
            }

            Navigator.Click(arrow.gameObject);
            // The click enters the Transition synchronously; if it somehow did not, the
            // carousel watcher speaks whatever the game settles on.
            if (Destination(fsm) == target) Announce(target, false);
        }

        /// <summary>Items: class name + position, description, perk, then one entry per skill
        /// with its modifier and the skill's own description.</summary>
        public static List<string> BuildItems(Transform panel)
        {
            var items = new List<string>();
            items.Add(panel.name + ", " + Position(panel.name) +
                      ". Up and Down to review details, Left and Right to change class, Enter to start.");

            string desc = Describe.TextUnder(panel, "DESC");
            if (desc != null) items.Add(desc);

            string perkTitle = null, perkDesc = null;
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt)) continue;
                if (perkTitle == null && txt.StartsWith("PERK")) perkTitle = txt;
            }
            perkDesc = Describe.TextUnderPrefix(panel, "Perk Desc");
            if (perkTitle != null || perkDesc != null)
                items.Add((perkTitle ?? "PERK").Replace("PERK //", "Perk:").Trim() + ". " + (perkDesc ?? ""));

            var skillDescs = SkillDescriptions(panel);
            var modifiers = SkillModifiers(panel);
            var order = GameVocab.SkillDisplayOrder;
            for (int i = 0; i < order.Length; i++)
            {
                string entry = order[i];
                if (modifiers.TryGetValue(order[i], out string mod) && mod != "0")
                    entry += " " + mod;
                if (i < skillDescs.Count)
                    entry += ": " + skillDescs[i];
                items.Add(entry);
            }
            return items;
        }

        /// <summary>The "Skill Desc" block lists all five descriptions in canonical order,
        /// separated by blank lines.</summary>
        private static List<string> SkillDescriptions(Transform panel)
        {
            var result = new List<string>();
            string block = Describe.TextUnder(panel, "Skill Desc");
            if (block == null) return result;
            foreach (var line in block.Split('\n'))
            {
                string trimmed = line.Trim();
                if (trimmed.Length > 0) result.Add(trimmed);
            }
            return result;
        }

        private static Dictionary<string, string> SkillModifiers(Transform panel)
        {
            var skills = new List<TMP_Text>();
            var mods = new List<TMP_Text>();
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt)) continue;
                if (System.Array.IndexOf(GameVocab.SkillDisplayOrder, txt) >= 0) skills.Add(tmp);
                else if (txt == "+1" || txt == "+2" || txt == "-1" || txt == "0") mods.Add(tmp);
            }
            var result = new Dictionary<string, string>();
            foreach (var skill in skills)
            {
                string modifier = null;
                float best = float.MaxValue;
                foreach (var mod in mods)
                {
                    float d = Mathf.Abs(mod.transform.position.y - skill.transform.position.y);
                    if (d < best) { best = d; modifier = mod.text.Trim(); }
                }
                if (modifier != null) result[skill.text.Trim()] = modifier;
            }
            return result;
        }
    }
}
