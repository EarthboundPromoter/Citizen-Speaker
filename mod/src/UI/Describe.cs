using System.Text;
using CSAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>Builds spoken descriptions for UI GameObjects using Citizen Sleeper's naming conventions
    /// (mapped during the July 2026 bridge survey).</summary>
    internal static class Describe
    {
        public static string Element(GameObject go, bool detailed)
        {
            if (go == null) return null;
            string name = go.name;

            if (name.StartsWith("Response: "))
            {
                // Bare numeric idiom: the spoken number is the key that picks it.
                string text = name.Substring("Response: ".Length);
                var responses = Patches.DialogueState.CurrentResponses;
                for (int i = 0; i < responses.Count; i++)
                {
                    if (responses[i] == text || responses[i].StartsWith(text))
                        return (i + 1) + ": " + text;
                }
                return text;
            }

            if (name == "Continue Button")
                return "Continue";

            if (name == "Dice Slot Button")
                return DescribeAction(go, detailed);

            if (name.StartsWith("Slot ") && int.TryParse(name.Substring(5), out int slotNum))
            {
                string contents = JoinTexts(go, 5);
                return "Save slot " + slotNum + (contents != null ? ", " + contents : "");
            }

            if ((name == "LEFT" || name == "RIGHT") && HasAncestor(go, "Character Select Canvas"))
                return name == "LEFT" ? "Previous class" : "Next class";

            if (name == "Location Button" || name == "Character Button")
            {
                string kind = name == "Location Button" ? "Location" : "Character";
                string owner = CanvasOwnerName(go);
                return owner != null ? kind + ": " + owner : kind;
            }

            var parentAction = FindActionRoot(go.transform);
            if (parentAction != null)
                return DescribeAction(go, detailed);

            string label = FirstText(go);
            if (!string.IsNullOrEmpty(label))
            {
                var selectable = go.GetComponent<Selectable>();
                string role = selectable is Button ? " button" : "";
                return label + role;
            }
            return name;
        }

        /// <summary>Compose an action panel description from its texts:
        /// name, skill + modifier, risk rating, slot state; detailed adds description and per-cycle costs.</summary>
        public static string DescribeAction(GameObject anywhereInAction, bool detailed)
        {
            var root = FindActionRoot(anywhereInAction.transform);
            if (root == null) return FirstText(anywhereInAction) ?? anywhereInAction.name;

            var sb = new StringBuilder();
            string actionName = TextUnder(root, "Action Name");
            sb.Append(actionName ?? CleanActionRootName(root.name));

            // Skill name + modifier live in small labeled texts; sweep for known skill words.
            string skills = CollectSkillLine(root);
            if (skills != null) sb.Append(". ").Append(skills);

            string rating = TextUnder(root, "Rating Name");
            if (rating != null) sb.Append(", ").Append(rating.ToLowerInvariant());

            string buttonLabel = TextUnder(root, "Dice Slot Button");
            if (buttonLabel != null) sb.Append(". ").Append(buttonLabel);

            if (detailed)
            {
                string desc = TextUnder(root, "Description");
                if (desc != null) sb.Append(". ").Append(desc);
                string perCycle = TextContaining(root, "PER CYCLE");
                if (perCycle != null) sb.Append(". ").Append(perCycle);
            }
            return sb.ToString();
        }

        public static Transform FindActionRoot(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
                if (cur.name.EndsWith(" Action"))
                    return cur;
            return null;
        }

        private static string CleanActionRootName(string name)
        {
            return name.EndsWith(" Action") ? name.Substring(0, name.Length - " Action".Length) : name;
        }

        private static string CanvasOwnerName(GameObject go)
        {
            for (var cur = go.transform; cur != null; cur = cur.parent)
            {
                int idx = cur.name.IndexOf(" Canvas");
                if (idx > 0) return cur.name.Substring(0, idx);
            }
            return null;
        }

        private static readonly string[] SkillNames = { "ENGINEER", "INTERFACE", "ENDURE", "INTUIT", "ENGAGE" };

        private static string CollectSkillLine(Transform root)
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt)) continue;
                foreach (var skill in SkillNames)
                {
                    if (txt == skill)
                    {
                        string modifier = SiblingModifier(tmp.transform);
                        return modifier != null ? skill + " " + modifier : skill;
                    }
                }
            }
            return null;
        }

        private static string SiblingModifier(Transform skillText)
        {
            var parent = skillText.parent;
            if (parent == null) return null;
            foreach (var tmp in parent.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (txt == "+1" || txt == "+2" || txt == "-1" || txt == "0")
                    return txt;
            }
            return null;
        }

        public static bool HasAncestor(GameObject go, string name)
        {
            for (var cur = go.transform; cur != null; cur = cur.parent)
                if (cur.name == name) return true;
            return false;
        }

        /// <summary>Join up to maxParts distinct child texts, e.g. a save slot's contents.</summary>
        public static string JoinTexts(GameObject go, int maxParts)
        {
            var parts = new System.Collections.Generic.List<string>();
            foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt) || parts.Contains(txt)) continue;
                parts.Add(txt);
                if (parts.Count >= maxParts) break;
            }
            return parts.Count > 0 ? string.Join(", ", parts) : null;
        }

        public static string TextUnderPrefix(Transform root, string namePrefix)
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
            {
                for (var cur = tmp.transform; cur != null && cur != root; cur = cur.parent)
                {
                    if (cur.name.StartsWith(namePrefix))
                    {
                        string txt = tmp.text?.Trim();
                        if (!string.IsNullOrEmpty(txt)) return txt;
                    }
                }
            }
            return null;
        }

        public static string FirstText(GameObject go)
        {
            var tmp = go.GetComponentInChildren<TMP_Text>(false);
            if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text)) return tmp.text.Trim();
            var legacy = go.GetComponentInChildren<Text>(false);
            if (legacy != null && !string.IsNullOrWhiteSpace(legacy.text)) return legacy.text.Trim();
            return null;
        }

        public static string TextUnder(Transform root, string childName)
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
            {
                for (var cur = tmp.transform; cur != null && cur != root; cur = cur.parent)
                {
                    if (cur.name == childName)
                    {
                        string txt = tmp.text?.Trim();
                        if (!string.IsNullOrEmpty(txt)) return txt;
                    }
                }
            }
            return null;
        }

        public static string TextContaining(Transform root, string fragment)
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (!string.IsNullOrEmpty(txt) && txt.Contains(fragment)) return txt;
            }
            return null;
        }
    }
}
