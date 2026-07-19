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
            string desc = ElementCore(go, detailed);
            if (desc == null || go == null) return desc;
            var selectable = go.GetComponent<Selectable>();
            if (selectable != null && !selectable.IsInteractable())
                desc += ", disabled";
            return desc;
        }

        private static string ElementCore(GameObject go, bool detailed)
        {
            if (go == null) return null;
            string name = go.name;

            if (name.StartsWith("Dice Cursor "))
            {
                // Die picker: cursor N stands for die N (native uGUI Buttons the game
                // selects itself; see docs/ui-state-map.md 6b).
                int.TryParse(name.Substring("Dice Cursor ".Length), out int cursorNum);
                return Game.GameQueries.DescribeDieForCursor(cursorNum);
            }

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
                if (selectable is Toggle toggle)
                    role = toggle.isOn ? " toggle, on" : " toggle, off";
                // Character window skill rows: each row's Confirm/Back buttons carry identical
                // labels — prefix the owning skill so they're distinguishable (triage report 20).
                string skillRow = AncestorDirectlyUnder(go, "SKILL List");
                if (skillRow != null) return skillRow + ": " + label + role;
                return label + role;
            }
            string skillOwner = AncestorDirectlyUnder(go, "SKILL List");
            return skillOwner != null ? skillOwner + ": " + name : name;
        }

        /// <summary>Compose an action panel description from its texts:
        /// name, skill + modifier, risk rating, slot state; detailed adds description and per-cycle costs.</summary>
        public static string DescribeAction(GameObject anywhereInAction, bool detailed)
        {
            var root = FindActionRoot(anywhereInAction.transform);
            if (root == null) return FirstText(anywhereInAction) ?? anywhereInAction.name;

            var sb = new StringBuilder();
            string spokenName = TextUnder(root, "Action Name") ?? CleanActionRootName(root.name);
            sb.Append(spokenName);

            // Skill name + modifier live in small labeled texts; sweep for known skill words.
            string skills = CollectSkillLine(root);
            if (skills != null) sb.Append(". ").Append(skills);

            string rating = TextUnder(root, "Rating Name");
            if (rating != null) sb.Append(", ").Append(rating.ToLowerInvariant());

            // Affordance: a "Gamepad Dice Slot" child (even inactive) marks a die-taking
            // action; its absence marks a plain activate button. Universal differentiator —
            // "Dice Slot Button" itself is present on ALL actions (docs/verification/C).
            sb.Append(TakesDie(root) ? ". Takes a die" : ". Enter to activate");

            string buttonLabel = TextUnder(root, "Dice Slot Button");
            if (buttonLabel != null && !buttonLabel.Equals(spokenName, System.StringComparison.OrdinalIgnoreCase))
                sb.Append(". ").Append(buttonLabel);

            if (detailed)
            {
                string desc = TextUnder(root, "Description");
                if (desc != null) sb.Append(". ").Append(desc);
                string perCycle = TextContaining(root, "PER CYCLE");
                if (perCycle != null) sb.Append(". ").Append(perCycle);
            }
            return sb.ToString();
        }

        /// <summary>Die-taking actions carry a "Gamepad Dice Slot" descendant (inactive while
        /// the picker is closed, so search inactive too); plain-activate actions never do.</summary>
        private static bool TakesDie(Transform actionRoot)
        {
            foreach (var t in actionRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "Gamepad Dice Slot") return true;
            return false;
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

        private static readonly string[] ModifierLabels = { "-1", "0", "+1", "+2" };

        /// <summary>The modifier row renders ALL FOUR labels (-1, 0, +1, +2) as separate texts;
        /// the applied one is distinguished graphically (triage report 12). Pick the single
        /// color outlier; if emphasis can't be resolved, speak no modifier rather than a wrong
        /// one, and log the row once for live calibration.</summary>
        private static string SiblingModifier(Transform skillText)
        {
            var parent = skillText.parent;
            if (parent == null) return null;
            var candidates = new System.Collections.Generic.List<TMP_Text>();
            foreach (var tmp in parent.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (System.Array.IndexOf(ModifierLabels, txt) >= 0)
                    candidates.Add(tmp);
            }
            if (candidates.Count == 0) return null;
            if (candidates.Count == 1) return candidates[0].text.Trim();

            var outlier = SingleColorOutlier(candidates);
            if (outlier != null) return outlier.text.Trim();

            if (!_modifierRowLogged)
            {
                _modifierRowLogged = true;
                var dump = new StringBuilder("[Describe] modifier row emphasis unresolved:");
                foreach (var c in candidates)
                    dump.Append(' ').Append(c.text.Trim()).Append('=').Append(c.color)
                        .Append("/a").Append(c.alpha.ToString("0.00"));
                Plugin.Log.LogInfo(dump.ToString());
            }
            return null;
        }

        private static bool _modifierRowLogged;

        /// <summary>The one text whose rendered color (incl. alpha) differs from all others,
        /// or null if the row doesn't split cleanly into one-vs-rest.</summary>
        private static TMP_Text SingleColorOutlier(System.Collections.Generic.List<TMP_Text> texts)
        {
            TMP_Text outlier = null;
            for (int i = 0; i < texts.Count; i++)
            {
                bool matchesAnother = false;
                for (int j = 0; j < texts.Count; j++)
                {
                    if (i == j) continue;
                    if (ColorsClose(texts[i].color, texts[j].color)) { matchesAnother = true; break; }
                }
                if (matchesAnother) continue;
                if (outlier != null) return null;
                outlier = texts[i];
            }
            return outlier;
        }

        private static bool ColorsClose(Color a, Color b)
        {
            return Mathf.Abs(a.r - b.r) < 0.05f && Mathf.Abs(a.g - b.g) < 0.05f
                && Mathf.Abs(a.b - b.b) < 0.05f && Mathf.Abs(a.a - b.a) < 0.05f;
        }

        /// <summary>Name of the ancestor of go that is a direct child of containerName,
        /// or null if go isn't inside such a container.</summary>
        private static string AncestorDirectlyUnder(GameObject go, string containerName)
        {
            for (var cur = go.transform; cur != null && cur.parent != null; cur = cur.parent)
                if (cur.parent.name == containerName) return cur.name;
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
