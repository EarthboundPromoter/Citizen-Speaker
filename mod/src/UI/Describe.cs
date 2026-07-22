using System.Collections.Generic;
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
                // Owner ruling (session 11): proper order — class, then cycle,
                // labeled — instead of the prefab's hierarchy order. Raw join
                // remains the fallback (empty slots, unexpected shapes).
                string contents = SaveSlotContents(go) ?? JoinTexts(go, 5);
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

            // Type badge (fresh-run F10): cards render Repeatable|Critical one-
            // active; critical = one-time-only roll — decision-relevant, so speak
            // the badge's own rendered text. Repeatable is the default: silent.
            string critical = CriticalBadge(root);
            if (critical != null) sb.Append(", ").Append(critical.ToLowerInvariant());

            // Affordance: the three take-kinds distinguished structurally (corpus
            // 2026-07-20): cryo controller / item slot (Item Cost) / die slot; plain
            // activate otherwise. Prefix match fixes the cloud numbered-slot miss
            // ("Gamepad Dice Slot 1" read as plain activate — s8 finding).
            // Requires grammar (owner ruling, session-12 ride): the detail read
            // conforms to the table's Requires cell — "Takes a die" retired.
            // Cloud node die demand (BL-12): the gating fact, graphics-only on the
            // card. A demand names the exact die, so the generic "a die" beside it
            // is the outlawed dupe ("Required die: 2. Takes a die." heard live) —
            // the demand speaks alone.
            string requiresPhrase = RequiresPhrase(root);
            string demand = HackingDemand(root);
            if (demand != null && requiresPhrase == "a die") requiresPhrase = null;
            if (requiresPhrase != null)
                sb.Append(". ").Append("Requires ").Append(requiresPhrase);
            else if (demand == null)
                sb.Append(". ").Append("Enter to activate");
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

        /// <summary>The card's Critical Action badge text when that badge renders
        /// (its local activeSelf is the authored type dial; the plate carries
        /// localized text "CRITICAL ACTION" — fresh-run F10 render map).</summary>
        public static string CriticalBadge(Transform root)
        {
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != "Critical Action") continue;
                if (!t.gameObject.activeSelf) return null;
                return FirstText(t.gameObject) ?? "Critical action";
            }
            return null;
        }

        /// <summary>Cloud node die demand (BL-12): the card renders 1–3 die glyphs whose
        /// values live in the Hacking Dice Slot FSM — Required Roll (+1/+2 with skill
        /// bucket) drives the rendered glyph via FloatSwitch → Dice Value states, and
        /// Slotted 1–3 accept exactly these values (corpus render-route trace,
        /// Potential Dice = rendered glyph count). Null on non-cloud actions or an
        /// unset slot (graceful silence). fallbackCount covers DORMANT cards (cloud
        /// table demand column, owner override 2026-07-20): Required Roll/+1/+2 are
        /// authored constants readable pre-activation; only the glyph count (Potential
        /// Dice) is Setup-computed — callers pass the INTERFACE-bucket count (the
        /// game's own 0→1/+1→2/+2→3 mapping; all 30 cloud dice nodes check INTERFACE,
        /// corpus-verified).</summary>
        public static string HackingDemand(Transform actionRoot, int fallbackCount = 0)
        {
            var slotT = actionRoot.Find("Hacking Dice Slot 1");
            if (slotT == null) return null;
            var fsm = slotT.GetComponent<PlayMakerFSM>();
            if (fsm == null) return null;
            var vars = fsm.FsmVariables;
            int count = Mathf.RoundToInt(vars.GetFsmFloat("Potential Dice")?.Value ?? 0f);
            if (count < 1) count = fallbackCount;
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
            // Wording (owner ruling, session 11): "Required die/dice: x, y, z" —
            // self-labeled, so table columns speak it without a header prefix.
            if (values.Count == 1) return "Required die: " + values[0];
            return "Required dice: " + string.Join(", ", values);
        }

        /// <summary>Cloud gate/item take (owner correction, session 11: gates take
        /// ITEMS, not dice — "Takes a die" was the dormant-card fallback lying). The
        /// authored requirement lives in an action parameter we cannot read at rest,
        /// but the card's own action name carries it: "Slot Havenage Cipher Action".
        /// Transcodes to "Takes HAVENAGE CIPHER"; null when the pattern is absent.</summary>
        public static string CloudItemTake(Transform actionRoot)
        {
            // A5 (S7 wrong-cipher field row): the OBJECT name is authored per card
            // from birth and cannot go stale; the rendered Action Name text can lag
            // or carry a neighbor template's string pre-resolution — it spoke
            // "Takes HAVENAGE CIPHER" where the detail read and the actual spend
            // both said SOLHEIM. Object name first, rendered text as fallback, so
            // the field row and the detail read share one source.
            string item = SlotItem(actionRoot.name) ?? SlotItem(TextUnder(actionRoot, "Action Name"));
            return item != null ? "Takes " + item : null;
        }

        private static string SlotItem(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            name = name.Trim();
            if (name.EndsWith(" Action")) name = name.Substring(0, name.Length - 7);
            if (!name.ToUpperInvariant().StartsWith("SLOT ")) return null;
            string item = name.Substring(5).Trim();
            return item.Length > 0 ? item : null;
        }

        /// <summary>The card's take-kind, structurally distinguished: cryo cards run
        /// Action Cryo Controller (Cryo Cost + rendered Cost Label); item slots carry
        /// Item Cost > 0 (Check Amount reads the INV_* held count); die slots are the
        /// same machinery without a cost; null = plain activate. Wording provisional.</summary>
        /// <summary>The verb-free requirements phrase (owner ruling, session-12
        /// ride): TakesLine's decode with the verb normalized away, so headers and
        /// composers add their own "Requires" exactly once — "a die" / "an item,
        /// cost 2" / "15 cryo, discounted".</summary>
        public static string RequiresPhrase(Transform actionRoot)
        {
            string takes = TakesLine(actionRoot);
            if (takes == null) return null;
            if (takes.StartsWith("Takes ")) return takes.Substring("Takes ".Length);
            if (takes.StartsWith("Costs ")) return takes.Substring("Costs ".Length);
            return takes;
        }

        public static string TakesLine(Transform actionRoot)
        {
            foreach (var t in actionRoot.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Action Cryo Controller")
                {
                    var fsm = t.GetComponent<PlayMakerFSM>();
                    var label = fsm != null ? fsm.FsmVariables.GetFsmString("Cost Label") : null;
                    var cost = fsm != null ? fsm.FsmVariables.GetFsmFloat("Cryo Cost") : null;
                    // The "Cryo" controller is the game's GENERIC cost controller
                    // (corpus 2026-07-22, 25 instances): its "Cryo" STRING localizes
                    // from field "CRYO" on 24 and from an item-specific key on the
                    // Unit 207-F passkey card (INV_SabinesPasskey in Value Check) —
                    // the unchecked assumption spoke "Requires 1 cryo" for a card
                    // demanding the passkey. The string CONTENT is the functional
                    // discriminator; the "Item" float is authored scratch for the
                    // Outcome relationship-adjust (never branched on — corpus), so it
                    // serves only as a weak hint while the string is still unlocalized.
                    var cryoStr = fsm != null ? fsm.FsmVariables.GetFsmString("Cryo") : null;
                    string cryoVal = cryoStr != null && cryoStr.Value != null
                        ? cryoStr.Value.Trim() : null;
                    bool isItemCost;
                    if (!string.IsNullOrEmpty(cryoVal))
                        isItemCost = !cryoVal.Equals("CRYO",
                            System.StringComparison.OrdinalIgnoreCase);
                    else
                    {
                        var itemFlag = fsm != null ? fsm.FsmVariables.GetFsmFloat("Item") : null;
                        isItemCost = itemFlag != null && itemFlag.Value >= 0.5f;
                    }
                    if (isItemCost)
                    {
                        string nm = !string.IsNullOrEmpty(cryoVal)
                            ? cryoVal : ItemDemandName(actionRoot);
                        int n = cost != null ? Mathf.RoundToInt(cost.Value) : 1;
                        if (nm != null)
                            return "Takes " + (n > 1 ? n + " " : "") + nm;
                        return "Takes an item" + (n > 1 ? ", cost " + n : "");
                    }
                    // P8 (heard three rides): the label renders the full prompt text
                    // ("INPUT 100 CRYO") and our "Costs ... cryo" frame doubled its
                    // words. The label's own number is the rendered amount — speak
                    // that; whole label only when it carries no number.
                    string amount = null;
                    if (label != null && !string.IsNullOrEmpty(label.Value))
                    {
                        var num = System.Text.RegularExpressions.Regex.Match(label.Value, "\\d+");
                        amount = num.Success ? num.Value : label.Value.Trim();
                    }
                    if (amount == null && cost != null)
                        amount = Mathf.RoundToInt(cost.Value).ToString();
                    string line = amount != null ? "Costs " + amount + " cryo" : "Costs cryo";
                    // ENGAGE perk 2 (corpus): the controller cuts the cost 20%,
                    // rewrites the rendered label with the new number, and shows a
                    // DISCOUNT! badge — the label flows through above; the badge is
                    // one word.
                    var badge = Game.StationAtlas.FindDeep(actionRoot, "DISCOUNT!");
                    if (badge != null && badge.gameObject.activeInHierarchy)
                        line += ", discounted";
                    return line;
                }
            }
            foreach (var t in actionRoot.GetComponentsInChildren<Transform>(true))
            {
                if (!t.name.StartsWith("Gamepad Dice Slot")) continue;
                var fsm = t.GetComponent<PlayMakerFSM>();
                var itemCost = fsm != null ? fsm.FsmVariables.GetFsmFloat("Item Cost") : null;
                bool isItem = (itemCost != null && itemCost.Value > 0f)
                    // Item Cost populates LAZILY (owner catches, session 11: DELIVER
                    // DATA and the cloud gates both read "Takes a die" at rest). The
                    // slot's own Check Amount state — the INV_* holdings check — is
                    // the authored marker for an item take and exists from birth.
                    || (fsm != null && HasState(fsm, "Check Amount"));
                if (isItem)
                {
                    // The demanded item's identity lives on the sibling Action
                    // Controller (Item Name / Cost Label / Item Required), never on
                    // the slot itself — 341 generic "an item" reads in one overnight
                    // session while the name sat authored beside them.
                    string nm = ItemDemandName(actionRoot);
                    int n = itemCost != null ? Mathf.RoundToInt(itemCost.Value) : 0;
                    if (nm != null)
                        return "Takes " + (n > 1 ? n + " " : "") + nm;
                    return n > 0 ? "Takes an item, cost " + n : "Takes an item";
                }
                return "Takes a die";
            }
            return null;
        }

        /// <summary>The demanded item's name from the card's own Action Controller,
        /// in source order: (1) "Item Name" — the game's display name, populates
        /// lazily on first render then sticks (faithful, authored typos kept);
        /// (2) "Cost Label" ("INPUT 1 YATAGAN DATA") minus the INPUT-count prefix —
        /// also rendered truth, also lazy; (3) "Item Required" — the INV_* Lua
        /// variable name, authored from birth on EVERY item card (33/33 live),
        /// de-camelCased (INV_GirolleCaps -> "Girolle Caps"). PROVISIONAL (owner
        /// ruling pending): source 3 derives a name instead of reading a render and
        /// silently normalizes authored typos ("Greeenway" -> "Greenway"); flip the
        /// order or drop source 3 per the ruling when it lands. Null when no source
        /// resolves — callers keep their generic form.</summary>
        public static string ItemDemandName(Transform actionRoot)
        {
            PlayMakerFSM ac = null;
            foreach (Transform child in actionRoot)
                if (child.name == "Action Controller")
                { ac = child.GetComponent<PlayMakerFSM>(); break; }
            if (ac == null) return null;
            var v = ac.FsmVariables;
            var itemName = v.GetFsmString("Item Name");
            if (itemName != null && !string.IsNullOrEmpty(itemName.Value))
                return itemName.Value.Trim();
            var label = v.GetFsmString("Cost Label");
            if (label != null && !string.IsNullOrEmpty(label.Value))
            {
                var m = System.Text.RegularExpressions.Regex.Match(
                    label.Value.Trim(), @"^INPUT\s+\d+\s+(.+)$",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                if (m.Success) return m.Groups[1].Value.Trim();
            }
            var req = v.GetFsmString("Item Required");
            if (req != null && !string.IsNullOrEmpty(req.Value))
            {
                string s = req.Value.Trim();
                if (s.StartsWith("INV_")) s = s.Substring(4);
                s = System.Text.RegularExpressions.Regex.Replace(
                    s, "(?<=[a-z0-9])(?=[A-Z])|(?<=[A-Za-z])(?=[0-9])", " ");
                return s.Length > 0 ? s : null;
            }
            return null;
        }

        private static bool HasState(PlayMakerFSM fsm, string stateName)
        {
            try
            {
                foreach (var s in fsm.FsmStates)
                    if (s.Name == stateName) return true;
            }
            catch { }
            return false;
        }

        /// <summary>Rendered skill word + Lua modifier bucket (public for the location
        /// table's Skill column).</summary>
        public static string SkillLine(Transform root) => CollectSkillLine(root);

        /// <summary>The Intuit perk's predicted-outcomes display: every station action
        /// card carries an OUTCOMES/PREDICTIVE object gated on INTUIT_PERKS >= 1
        /// (corpus 2026-07-20). Speaks its rendered text while it is on; null while
        /// dormant. If it turns out to render icon-only, the one-time log line below
        /// flags the live calibration pass it needs.</summary>
        public static string PredictiveLine(Transform actionRoot)
        {
            var pred = Game.StationAtlas.FindDeep(actionRoot, "PREDICTIVE");
            if (pred == null || !pred.gameObject.activeInHierarchy) return null;
            // Live finding (session 11, owner-caught): without the perk the object
            // stays GameObject-active and hides by alpha — the active+text test spoke
            // an invisible teaser. Same effective-alpha standard as the notification
            // watcher and F13; per-text alpha guards color-faded glyph rows too.
            if (EffectiveAlpha(pred) < 0.5f) return null;
            var texts = new System.Collections.Generic.List<string>();
            foreach (var tmp in pred.GetComponentsInChildren<TMPro.TMP_Text>(false))
            {
                if (tmp.color.a < 0.05f) continue;
                string t = tmp.text != null ? tmp.text.Trim() : null;
                if (string.IsNullOrEmpty(t)) continue;
                // B3: cull the PERK caption — ten syllables of noise per row; the
                // column name already says what the cell is (owner ruling).
                if (t.StartsWith("PERK", System.StringComparison.OrdinalIgnoreCase)) continue;
                t = TranscodePredictedEntry(tmp, pred);
                if (!string.IsNullOrEmpty(t) && !texts.Contains(t))
                    texts.Add(Speech.SpeechService.Clean(t));
            }
            if (texts.Count == 0)
            {
                if (!_predictiveLogged)
                {
                    _predictiveLogged = true;
                    Plugin.Log.LogInfo("[Describe] PREDICTIVE renders but carries no text"
                        + " — icon decode needed (live look).");
                }
                return null;
            }
            return string.Join("; ", texts);
        }

        /// <summary>B3: one Predicted tier entry — glyph runs to counts, tier
        /// labeled. "++ YARD HAND" → "positive: YARD HAND, 2 segments" (clock
        /// wording law: NAME, X segments — universal); "- CONDITION" → "negative:
        /// 1 condition"; "+ 15 CRYO" → "positive: 15 CRYO" (explicit number kept).
        /// Tier identity from the marker ancestry when it names one, else from the
        /// glyph sign — resolves the unlabeled pairs ("+ ENERGY, - ENERGY" was
        /// flatly contradictory as rendered text).</summary>
        private static string TranscodePredictedEntry(TMPro.TMP_Text tmp, Transform pred)
        {
            string t = tmp.text.Trim();
            int plus = 0, minus = 0, i = 0;
            while (i < t.Length && (t[i] == '+' || t[i] == '-' || t[i] == ' '))
            {
                if (t[i] == '+') plus++;
                else if (t[i] == '-') minus++;
                i++;
            }
            string body = t.Substring(i).Trim();
            int count = plus + minus;
            if (count == 0 || body.Length == 0) return t;

            string tier = null;
            for (var cur = tmp.transform; cur != null && cur != pred; cur = cur.parent)
            {
                string n = cur.name.ToLowerInvariant();
                if (n.Contains("boon")) { tier = "best"; break; }
                if (n.Contains("positive")) { tier = "positive"; break; }
                if (n.Contains("negative")) { tier = "negative"; break; }
                if (n.Contains("neutral")) { tier = "neutral"; break; }
            }
            if (tier == null) tier = plus >= minus ? "positive" : "negative";

            string upper = body.ToUpperInvariant();
            string spoken;
            if (upper == "ENERGY" || upper == "CONDITION")
                spoken = count + " " + body.ToLowerInvariant();
            else if (char.IsDigit(body[0]))
                spoken = body; // explicit amount ("15 CRYO") — the count is the marker
            else
                spoken = body + ", " + count + (count == 1 ? " segment" : " segments");
            return tier + ": " + spoken;
        }

        private static bool _predictiveLogged;

        private static float EffectiveAlpha(Transform t)
        {
            float a = 1f;
            for (var cur = t; cur != null; cur = cur.parent)
            {
                var g = cur.GetComponent<CanvasGroup>();
                if (g != null) a *= g.alpha;
            }
            return a;
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
            name = name.TrimEnd();
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
                        string line = modifier != null ? skill + " " + modifier : skill;
                        if (ActionSkillLocked(root)) line += ", skill locked";
                        return line;
                    }
                }
            }
            return null;
        }

        /// <summary>Why a rendered card can't be acted on (owner ruling 2026-07-20:
        /// every lock reason spoken, and refusals state it). Sources are the
        /// controller's RESTING state + its Button Label FSM string — the game's own
        /// localized reason text, which survives readable even though the game
        /// deactivates the button object carrying it (corpus: Action Completed /
        /// Temp Complete / Working all deactivate Dice Slot Button and write the
        /// label). Null when the card has no known gate (caller keeps its generic
        /// wording). Wording provisional.</summary>
        public static string DisabledReason(Transform actionRoot)
        {
            foreach (var fsm in actionRoot.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                string owner = fsm.gameObject.name;
                if (owner != "Action Controller" && owner != "Action Cryo Controller")
                    continue;
                string state = fsm.ActiveStateName;
                string label = fsm.FsmVariables.GetFsmString("Button Label")?.Value?.Trim();
                switch (state)
                {
                    case "LOCKED":
                    case "LOCKED Critical":
                        string skill = SkillWordOf(actionRoot);
                        return "Skill locked" + (skill != null ? " — needs " + skill + " at +1." : ".");
                    case "Action Completed":
                    case "Temp Complete":
                    case "Not Repeatable":
                        return !string.IsNullOrEmpty(label) ? label + "." : "Completed.";
                    case "Working":
                        return !string.IsNullOrEmpty(label) ? label + "." : "Working.";
                }
                return null;
            }
            return null;
        }

        /// <summary>The card's rendered skill word, if any.</summary>
        public static string SkillWordOf(Transform root)
        {
            foreach (var tmp in root.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt)) continue;
                foreach (var skill in SkillNames)
                    if (txt == skill) return skill;
            }
            return null;
        }

        /// <summary>Skill-lock dial (corpus + live 2026-07-20, HAGGLE OVER PRICES): tiered
        /// cards carry an authored Z Skill Lock; modifier buckets -1/0 route the controller
        /// to a RESTING LOCKED state (LOCKED Critical when also non-repeatable) whose only
        /// render is a lock-glyph animator over the skill display — the pips-pattern
        /// transcode. Wording provisional.</summary>
        public static bool ActionSkillLocked(Transform actionRoot)
        {
            foreach (var fsm in actionRoot.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.gameObject.name != "Action Controller") continue;
                string s = fsm.ActiveStateName;
                return s == "LOCKED" || s == "LOCKED Critical";
            }
            return false;
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

        /// <summary>Save-slot contents in proper order (owner ruling, session 11):
        /// "Class: MACHINIST. Cycle: 8." — the class word is one of the three
        /// rendered class names, the cycle rides a CYCLE text (inline number or a
        /// bare label paired with a standalone number); anything else trails in
        /// render order. Null when neither anchor is found (caller falls back).</summary>
        private static string SaveSlotContents(GameObject slot)
        {
            // Straight from the render (owner correction, session 11): the slot's
            // own CLASS/CYCLE label texts pair with their values and are consumed —
            // nothing invented, nothing trailing.
            string cls = null, clsLabel = null, cycleLabel = null, cycleInline = null, number = null;
            var extras = new System.Collections.Generic.List<string>();
            foreach (var tmp in slot.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt)) continue;
                string up = txt.ToUpperInvariant();
                if (up == "CLASS")
                    clsLabel = txt;
                else if (up == "CYCLE")
                    cycleLabel = txt;
                else if (up == "OPERATOR" || up == "EXTRACTOR" || up == "MACHINIST")
                    cls = txt;
                else if (up.Contains("CYCLE"))
                    cycleInline = txt;
                else if (int.TryParse(txt, out _))
                    number = txt;
                else if (!extras.Contains(txt))
                    extras.Add(txt);
            }
            string cycleValue = cycleInline != null
                ? cycleInline.Substring(cycleInline.ToUpperInvariant().IndexOf("CYCLE") + 5).Trim()
                : number;
            if (cls == null && cycleValue == null) return null;
            var sb = new System.Text.StringBuilder();
            if (cls != null)
                sb.Append(clsLabel ?? "Class").Append(": ").Append(cls).Append(". ");
            if (!string.IsNullOrEmpty(cycleValue))
                sb.Append(cycleLabel ?? "Cycle").Append(": ").Append(cycleValue).Append(". ");
            foreach (var extra in extras) sb.Append(extra).Append(". ");
            return sb.ToString().TrimEnd();
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

        /// <summary>First rendered text under go, skipping gamepad-prompt glyph children
        /// (BL-2: the Scan Button's first text is its glyph child, which spoke as
        /// "Y button" — a glyph is never an element's label; full glyph transcode is
        /// the separate W4 item).</summary>
        public static string FirstText(GameObject go)
        {
            foreach (var tmp in go.GetComponentsInChildren<TMP_Text>(false))
            {
                if (UnderPromptGlyph(tmp.transform, go.transform)) continue;
                if (!string.IsNullOrWhiteSpace(tmp.text)) return tmp.text.Trim();
            }
            foreach (var legacy in go.GetComponentsInChildren<Text>(false))
            {
                if (UnderPromptGlyph(legacy.transform, go.transform)) continue;
                if (!string.IsNullOrWhiteSpace(legacy.text)) return legacy.text.Trim();
            }
            return null;
        }

        private static bool UnderPromptGlyph(Transform t, Transform stopAt)
        {
            for (var cur = t; cur != null && cur != stopAt; cur = cur.parent)
                if (cur.name.Contains("Gamepad Prompt") || cur.name.StartsWith("Gamepad Glyph"))
                    return true;
            return false;
        }

        /// <summary>B1: the END CYCLE effect strip renders as glyph runs and pipes
        /// ("PER CYCLE | - - ENERGY | - CONDITION", starving variant "STARVING! |
        /// PER CYCLE | -- CONDITION") — state-dependent survival info, not
        /// decoration. Transcode: pipes to sentence flow, glyph runs to counts
        /// ("Per cycle: 2 energy, 1 condition"), the game's own STARVING! word
        /// kept verbatim. Non-strip text passes through untouched.</summary>
        public static string TranscodeCycleStrip(string s)
        {
            if (s == null || s.IndexOf('|') < 0) return s;
            var costs = new List<string>();
            string lead = null;
            foreach (var seg in s.Split('|'))
            {
                string t = seg.Trim();
                if (t.Length == 0) continue;
                if (t.Equals("PER CYCLE", System.StringComparison.OrdinalIgnoreCase)) continue;
                int plus = 0, minus = 0, i = 0;
                while (i < t.Length && (t[i] == '+' || t[i] == '-' || t[i] == ' '))
                {
                    if (t[i] == '+') plus++;
                    else if (t[i] == '-') minus++;
                    i++;
                }
                string word = t.Substring(i).Trim();
                if (plus + minus == 0) { lead = t; continue; } // STARVING! and kin
                costs.Add((plus > 0 ? "plus " : "") + (plus + minus) + " "
                    + word.ToLowerInvariant());
            }
            if (costs.Count == 0) return lead ?? s;
            return (lead != null ? lead + " " : "")
                + "Per cycle: " + string.Join(", ", costs);
        }

        /// <summary>A2: the game's own localization drops the LEADING straight quote
        /// from some name/label entries ("WINTER LIGHT'", "Sunbathe'") while effect
        /// lines are authored fully quoted — names compare and speak quote-free so
        /// the two authored variants meet. Names only; narrative prose keeps its
        /// quotes untouched.</summary>
        public static string TrimQuotes(string s)
            => s == null ? null : s.Trim().Trim('\'', '"');

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
