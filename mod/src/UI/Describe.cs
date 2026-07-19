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

            if (name == "Item Cursor")
            {
                // BL-10: cursors carry no text; the hovered item's identity renders in
                // the Inventory Display (both panels share it), the count in the slot's
                // Amount. Falls through to the bare name when the display is empty.
                string item = InventoryItem(go, detailed);
                if (item != null) return item;
            }

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

        /// <summary>Inventory item under the cursor: Inventory Display's rendered Item
        /// Name + the slot's rendered Amount; detailed adds the Item Description.
        /// Null when the display holds no name (caller falls back).</summary>
        public static string InventoryItem(GameObject cursor, bool detailed)
        {
            var display = GameObject.Find("Letterbox Canvas/Bottom UI/Inventory/Inventory Display");
            if (display == null) return null;
            string itemName = TextUnder(display.transform, "Item Name");
            if (string.IsNullOrEmpty(itemName)) return null;

            var sb = new StringBuilder(itemName);
            // Slot = cursor's grandparent (…/<X> Slot/<item family>/Item Cursor).
            var slot = cursor.transform.parent != null ? cursor.transform.parent.parent : null;
            var amountT = slot != null ? slot.Find("Amount") : null;
            var amount = amountT != null ? amountT.GetComponent<TMP_Text>() : null;
            if (amount != null && !string.IsNullOrWhiteSpace(amount.text))
                sb.Append(", amount ").Append(amount.text.Trim());
            if (detailed)
            {
                string desc = TextUnder(display.transform, "Item Description");
                if (desc != null) sb.Append(". ").Append(desc);
            }
            return sb.ToString();
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

            // Cloud node die demand (BL-12): the gating fact, graphics-only on the card.
            string demand = HackingDemand(root);
            if (demand != null) sb.Append(". ").Append(demand);

            string buttonLabel = TextUnder(root, "Dice Slot Button");
            if (buttonLabel != null && !buttonLabel.Equals(spokenName, System.StringComparison.OrdinalIgnoreCase))
                sb.Append(". ").Append(buttonLabel);

            if (detailed)
            {
                string desc = TextUnder(root, "Description");
                if (desc != null) sb.Append(". ").Append(desc);
                string perCycle = TextContaining(root, "PER CYCLE");
                if (perCycle != null) sb.Append(". ").Append(perCycle);
                // Cloud sequence steps (BL-13): rendered step name + lock state per
                // Elements Slot ("ACCESS PROTOCOLS, LOCKED").
                foreach (Transform child in root)
                {
                    if (!child.name.StartsWith("Elements Slot")) continue;
                    if (!child.gameObject.activeInHierarchy) continue;
                    string step = TextUnder(child, "Outline (1)");
                    string state = TextUnder(child, "Outline (2)");
                    if (step == null) continue;
                    sb.Append(". ").Append(step);
                    if (state != null) sb.Append(", ").Append(state);
                }
            }
            return sb.ToString();
        }

        /// <summary>Cloud node die demand (BL-12): the card renders 1–3 die glyphs whose
        /// values live in the Hacking Dice Slot FSM — Required Roll (+1/+2 with skill
        /// bucket) drives the rendered glyph via FloatSwitch → Dice Value states, and
        /// Slotted 1–3 accept exactly these values (corpus render-route trace,
        /// Potential Dice = rendered glyph count). Null on non-cloud actions or an
        /// unset slot (graceful silence).</summary>
        private static string HackingDemand(Transform actionRoot)
        {
            var slotT = actionRoot.Find("Hacking Dice Slot 1");
            if (slotT == null) return null;
            var fsm = slotT.GetComponent<PlayMakerFSM>();
            if (fsm == null) return null;
            var vars = fsm.FsmVariables;
            int count = Mathf.RoundToInt(vars.GetFsmFloat("Potential Dice")?.Value ?? 0f);
            if (count < 1) return null;

            var names = new[] { "Required Roll", "Required Roll +1", "Required Roll +2" };
            var values = new System.Collections.Generic.List<string>();
            for (int i = 0; i < count && i < names.Length; i++)
            {
                int v = Mathf.RoundToInt(vars.GetFsmFloat(names[i])?.Value ?? 0f);
                if (v < 1 || v > 6) continue;
                values.Add(v.ToString());
            }
            if (values.Count == 0) return null;
            if (values.Count == 1) return "Matches die " + values[0];
            return "Matches die " + string.Join(", ", values.GetRange(0, values.Count - 1))
                + " or " + values[values.Count - 1];
        }

        /// <summary>Die-taking actions carry a "Gamepad Dice Slot" descendant (inactive while
        /// the picker is closed, so search inactive too); plain-activate actions never do.</summary>
        private static bool TakesDie(Transform actionRoot)
        {
            foreach (var t in actionRoot.GetComponentsInChildren<Transform>(true))
                if (t.name == "Gamepad Dice Slot") return true;
            return false;
        }

        /// <summary>Cloud action roots break the " Action" suffix convention freely
        /// ("Yatagan Agent 1 Action " trailing space, "ConSec X3 Hack Action (2)",
        /// "Hardin Agent Action 1 slot" — corpus). The durable convention both sides
        /// honor: action cards are the direct children of a "* Actions" group.</summary>
        public static Transform FindActionRoot(Transform t)
        {
            for (var cur = t; cur != null; cur = cur.parent)
            {
                if (cur.name.TrimEnd().EndsWith(" Action"))
                    return cur;
                if (cur.parent != null && cur.parent.name.TrimEnd().EndsWith(" Actions"))
                    return cur;
            }
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

        /// <summary>Skill word from the card's rendered texts; the modifier bucket comes from
        /// the Lua skill variable — the same value whose bucket the row highlights (brief
        /// G#2, BL-1). The row renders all four labels and marks one graphically, so the
        /// rendered texts alone can't say which; Lua is the render-paired source.</summary>
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
                        string modifier = Substrate.LuaStore.SkillModifierForWord(skill);
                        return modifier != null ? skill + " " + modifier : skill;
                    }
                }
            }
            return null;
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
