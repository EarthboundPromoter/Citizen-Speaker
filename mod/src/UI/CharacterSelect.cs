using System.Collections.Generic;
using CSAccess.Speech;
using TMPro;
using UnityEngine;

namespace CSAccess.UI
{
    /// <summary>Explorable review of the character-creation carousel.
    /// Up/Down step through the centered class's details; Left/Right change class; Enter starts.</summary>
    internal static class CharacterSelect
    {
        private static readonly string[] ClassNames = { "EXTRACTOR", "OPERATOR", "MACHINIST" };
        private static readonly string[] SkillOrder = { "ENGINEER", "INTERFACE", "ENDURE", "INTUIT", "ENGAGE" };

        private static int _reviewIndex = -1;

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
                if (System.Array.IndexOf(ClassNames, child.name) < 0) continue;
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

        /// <summary>The carousel arrows are plain buttons with no keyboard path; click them.</summary>
        public static void ChangeClass(bool right)
        {
            var canvas = Canvas();
            if (canvas == null) return;
            var arrow = canvas.transform.Find(right ? "RIGHT" : "LEFT");
            if (arrow != null && arrow.gameObject.activeInHierarchy)
                Navigator.Click(arrow.gameObject);
            // The carousel watcher announces the newly centered class.
        }

        /// <summary>Items: class name + position, description, perk, then one entry per skill
        /// with its modifier and the skill's own description.</summary>
        public static List<string> BuildItems(Transform panel)
        {
            var items = new List<string>();
            int classIndex = System.Array.IndexOf(ClassNames, panel.name);
            items.Add(panel.name + ", class " + (classIndex + 1) + " of " + ClassNames.Length +
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
            for (int i = 0; i < SkillOrder.Length; i++)
            {
                string entry = SkillOrder[i];
                if (modifiers.TryGetValue(SkillOrder[i], out string mod) && mod != "0")
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
                if (System.Array.IndexOf(SkillOrder, txt) >= 0) skills.Add(tmp);
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
