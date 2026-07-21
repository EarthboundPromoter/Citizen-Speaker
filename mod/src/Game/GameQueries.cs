using System.Collections.Generic;
using System.Text;
using CSAccess.UI;
using TMPro;
using UnityEngine;

namespace CSAccess.Game
{
    /// <summary>Reads game state from PlayMaker FSMs and drives the gamepad-mode dice flow.</summary>
    internal static class GameQueries
    {
        // ---------- FSM helpers ----------

        public static PlayMakerFSM FindFsm(string goName, string pathHint = null)
        {
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || fsm.gameObject == null) continue;
                if (fsm.gameObject.name != goName) continue;
                if (pathHint != null && !PathOf(fsm.gameObject).Contains(pathHint)) continue;
                return fsm;
            }
            return null;
        }

        public static string PathOf(GameObject go)
        {
            var sb = new StringBuilder(go.name);
            for (var t = go.transform.parent; t != null; t = t.parent)
                sb.Insert(0, t.name + "/");
            return sb.ToString();
        }

        // ---------- Cycle transition (end-cycle pipeline) ----------

        private static PlayMakerFSM _cycleController;

        public static PlayMakerFSM CycleControllerFsm()
        {
            if (_cycleController == null || _cycleController.gameObject == null)
                _cycleController = FindFsm("Cycle Controller");
            return _cycleController;
        }

        /// <summary>True only while the real end-cycle pipeline runs. Event-armed by
        /// CycleGate (Cycle-state entry via the designed EndCycle event) — the old
        /// "any non-Idle state" poll wrongly included the game's startup walk.</summary>
        public static bool CycleTransitionActive() => CycleGate.Active;

        /// <summary>Raw/boosted two-phase global — NOT reliable at slot time (written
        /// raw in Die.Slotted, overwritten boosted in Die.Boost, race with the tier
        /// read). Prefer SlottedBoostedDieValue (dice-lifecycle map 2026-07-21).</summary>
        public static float SlottedDiceValueGlobal()
        {
            var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.GetFsmFloat("SlottedDiceValueGlobal");
            return v != null ? v.Value : 0f;
        }

        /// <summary>The game's own final die value, READ (not computed) from the Die
        /// object ($SlottedDiceGlobal) — the same DiceValue the game's Dice Chance
        /// Filter switches on. Boosted actions land it in a "Boost N" state (DiceValue
        /// already includes the skill modifier); zero-modifier actions settle in
        /// "Slotted" with DiceValue = the face. Returns the value only once it has
        /// SETTLED so the caller never speaks the transient raw face a boosted die
        /// shows for ~0.1s before Die.Boost runs: settled = "Boost N", OR "Slotted"
        /// when the action's modifier is 0 (no boost coming). Null while still
        /// settling or when nothing is slotted — the caller watches per frame and
        /// speaks the instant it settles (no fixed delay).</summary>
        public static int? SettledSlottedDieValue()
        {
            var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables.GetFsmGameObject("SlottedDiceGlobal");
            var go = g != null ? g.Value : null;
            if (go == null) return null;
            PlayMakerFSM dieFsm = null;
            foreach (var f in go.GetComponents<PlayMakerFSM>())
                if (f.FsmVariables.GetFsmFloat("DiceValue") != null) { dieFsm = f; break; }
            if (dieFsm == null) return null;
            var dv = dieFsm.FsmVariables.GetFsmFloat("DiceValue");
            if (dv == null) return null;
            string st = dieFsm.ActiveStateName;
            bool settled = st.StartsWith("Boost ");
            if (!settled && st == "Slotted")
            {
                // Slotted is final only when no boost is coming (modifier 0); otherwise
                // DiceValue is still the raw face and Die.Boost is about to overwrite it.
                var ac = go.transform.parent != null
                    ? go.transform.parent.GetComponent<PlayMakerFSM>() : null;
                var mod = ac != null ? ac.FsmVariables.GetFsmFloat("Required Skill Value") : null;
                settled = mod != null && Mathf.RoundToInt(mod.Value) == 0;
            }
            return settled ? (int?)Mathf.RoundToInt(dv.Value) : null;
        }

        /// <summary>The game's own start-action prompt text for the slotted action
        /// (its Dice Slot Button label — "START ACTION"), read so the mod can append
        /// it to the odds line in a deterministic order after muting the game's own
        /// focus announce. The slotted die ($SlottedDiceGlobal) is parented under its
        /// Action Controller, so the action root is two levels up. Null if not found.</summary>
        public static string SlottedActionButtonText()
        {
            var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables.GetFsmGameObject("SlottedDiceGlobal");
            var die = g != null ? g.Value : null;
            if (die == null) return null;
            var ac = die.transform.parent;                       // Action Controller
            var actionRoot = ac != null ? ac.parent : null;      // the Action
            if (actionRoot == null) return null;
            var btn = StationAtlas.FindDeep(actionRoot, "Dice Slot Button");
            return btn != null ? UI.Describe.FirstText(btn.gameObject) : null;
        }

        /// <summary>The action's Gamepad Dice Slot currently in the picker flow —
        /// "Select Dice" (choosing) or "Select Dice 2" (die slotted). Its Reset event
        /// is context-sensitive: from Select Dice 2 it unslots the die (ForceUnslotDice
        /// → Action Controller); from Select Dice it returns the slot to Idle. One
        /// Reset therefore handles BOTH retract and close (dice-lifecycle map
        /// 2026-07-21). Null when idle.</summary>
        public static PlayMakerFSM ActiveDiceSlot()
        {
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || fsm.gameObject == null) continue;
                if (fsm.gameObject.name != "Gamepad Dice Slot") continue;
                var s = fsm.ActiveStateName;
                if (s == "Select Dice" || s == "Select Dice 2") return fsm;
            }
            return null;
        }

        /// <summary>Read a PlayMaker global bool (dial read, invariant 4).</summary>
        public static bool GlobalBoolValue(string name)
        {
            var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.GetFsmBool(name);
            return v != null && v.Value;
        }

        // ---------- Scripted input pauses ----------

        /// <summary>The Tutorial System's Input Pauser FSM pauses player input during scripted
        /// windows (tutorial transitions, intro beats). Hardware input goes through Rewired and
        /// is genuinely paused; the mod's synthetic uGUI events would bypass it and can break
        /// scripted chains (the intro tutorial hang, sessions 2 and 3). Honor the pause.</summary>
        public static bool InputPaused()
        {
            var pauser = FindFsm("Input Pauser", "Tutorial System");
            if (pauser == null) return false;
            string state = pauser.ActiveStateName;
            return !string.IsNullOrEmpty(state) && state != "UNPAUSED";
        }

        // ---------- Gamepad-mode enforcement ----------

        public static void EnsureGamepadMode()
        {
            var manager = FindFsm("Gamepad Manager");
            if (manager != null && manager.ActiveStateName == "Mouse")
            {
                manager.SendEvent("Gamepad");
                Plugin.Log.LogInfo("[Game] Keyboard input: asserted gamepad UI mode.");
            }
        }

        /// <summary>A mouse click claims mouse mode (cursor for a sighted co-pilot).
        /// Sends the Gamepad Manager's own Mouse event; if the FSM's current state has no
        /// transition for it, the event is dropped harmlessly.</summary>
        public static void EnsureMouseMode()
        {
            var manager = FindFsm("Gamepad Manager");
            if (manager != null && manager.ActiveStateName != "Mouse")
            {
                manager.SendEvent("Mouse");
                Plugin.Log.LogInfo("[Game] Mouse click: asserted mouse UI mode.");
            }
        }

        // ---------- Dice ----------

        public class DieInfo
        {
            public int SlotNumber;
            public int Value;
            public string State;
            public PlayMakerFSM Fsm;
        }

        public static List<DieInfo> GetDice()
        {
            var dice = new List<DieInfo>();
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || fsm.gameObject == null || fsm.gameObject.name != "Die") continue;
                var parent = fsm.transform.parent;
                if (parent == null || !parent.name.StartsWith("Dice Slot")) continue;
                if (!PathOf(fsm.gameObject).Contains("Dice UI")) continue;

                int slotNum = ParseTrailingInt(parent.name);
                var value = fsm.FsmVariables.GetFsmInt("DiceValue");
                dice.Add(new DieInfo
                {
                    SlotNumber = slotNum,
                    Value = value?.Value ?? 0,
                    State = fsm.ActiveStateName,
                    Fsm = fsm,
                });
            }
            dice.Sort((a, b) => a.SlotNumber.CompareTo(b.SlotNumber));
            return dice;
        }

        public static string DescribeDice()
        {
            var dice = GetDice();
            if (dice.Count == 0) return "No dice available.";
            var sb = new StringBuilder("Dice: ");
            foreach (var d in dice)
            {
                sb.Append("die ").Append(d.SlotNumber).Append(", ");
                if (d.State == "Used") sb.Append("spent");
                else if (d.State == "Slotted" || d.State.StartsWith("Boost")) sb.Append("value ").Append(d.Value).Append(", slotted");
                else sb.Append("value ").Append(d.Value);
                sb.Append(". ");
            }
            return sb.ToString();
        }

        /// <summary>Leading integer of a name ("32 Step Clock" -> 32); 0 if none.</summary>
        public static int LeadingInt(string name)
        {
            int i = 0;
            while (i < name.Length && char.IsDigit(name[i])) i++;
            int.TryParse(name.Substring(0, i), out int result);
            return result;
        }

        private static int ParseTrailingInt(string name)
        {
            int i = name.Length - 1;
            while (i >= 0 && char.IsDigit(name[i])) i--;
            int.TryParse(name.Substring(i + 1), out int result);
            return result;
        }

        // ---------- Actions at the current location ----------

        public static List<Transform> GetActionPanels()
        {
            var results = new List<Transform>();
            var groupsRoot = GameObject.Find("Letterbox Canvas/1_Action Groups");
            if (groupsRoot == null) return results;
            foreach (Transform group in groupsRoot.transform)
            {
                if (!group.gameObject.activeInHierarchy) continue;
                foreach (Transform child in group)
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    // TrimEnd: card names ship with trailing spaces
                    // ("Ask for Directions Action " — fresh-run F9).
                    if (child.name.TrimEnd().EndsWith(" Action"))
                        results.Add(child);
                }
            }
            return results;
        }

        public static List<Transform> GetClockPanels()
        {
            var results = new List<Transform>();
            var groupsRoot = GameObject.Find("Letterbox Canvas/1_Action Groups");
            if (groupsRoot == null) return results;
            foreach (Transform group in groupsRoot.transform)
            {
                if (!group.gameObject.activeInHierarchy) continue;
                foreach (Transform child in group)
                {
                    if (!child.gameObject.activeInHierarchy) continue;
                    if (child.name.TrimEnd().EndsWith(" Clock"))
                        results.Add(child);
                }
            }
            return results;
        }

        public static string DescribeClocks()
        {
            var clocks = GetClockPanels();
            if (clocks.Count == 0) return "No clocks at this location.";
            var sb = new StringBuilder();
            foreach (var clock in clocks)
            {
                string name = Describe.TrimQuotes(Describe.TextUnder(clock, "Clock Name")) ?? clock.name;
                sb.Append(name);
                string progress = ClockProgress(clock);
                if (progress != null) sb.Append(", ").Append(progress);
                string desc = Describe.TextUnder(clock, "Clock Description");
                if (desc != null) sb.Append(". ").Append(desc);
                sb.Append(" ");
            }
            return sb.ToString();
        }

        /// <summary>Read the clock's active "N Step Clock" dial. Everything spoken is rendered
        /// on the dial: filled wedges (ClockValue), segment count (dial variant), the +/- glyph
        /// (Positive?), and the CYCLE CLOCK banner (Cycle Clock?). Verified live against the
        /// Back in Business 8-step dial, session 5.</summary>
        public static string ClockProgress(Transform clock)
        {
            foreach (Transform child in clock)
            {
                if (!child.gameObject.activeInHierarchy || !child.name.Contains("Step ")) continue;
                var fsm = child.GetComponent<PlayMakerFSM>();
                if (fsm == null) continue;

                int steps = ParseLeadingInt(child.name);
                int value = ReadFsmNumber(fsm, "ClockValue");
                bool positive = fsm.FsmVariables.GetFsmBool("Positive?")?.Value ?? false;
                bool cycle = fsm.FsmVariables.GetFsmBool("Cycle Clock?")?.Value ?? false;

                var sb = new StringBuilder();
                // F11: ClockValue can pass the authored size before completion
                // handling ("9 of 8" live) — a full dial renders full: "complete".
                if (steps > 0 && value >= steps)
                    sb.Append("complete, ");
                else if (steps > 0)
                    sb.Append(value).Append(" of ").Append(steps).Append(" segments, ");
                sb.Append(positive ? "positive" : "negative");
                if (cycle) sb.Append(" cycle");
                sb.Append(" clock");
                return sb.ToString();
            }
            return null;
        }

        /// <summary>PlayMaker numeric variable that may be authored as int or float.</summary>
        private static int ReadFsmNumber(PlayMakerFSM fsm, string name)
        {
            var iv = fsm.FsmVariables.GetFsmInt(name);
            if (iv != null) return iv.Value;
            var fv = fsm.FsmVariables.GetFsmFloat(name);
            if (fv != null) return Mathf.RoundToInt(fv.Value);
            return 0;
        }

        private static int ParseLeadingInt(string name)
        {
            int i = 0;
            while (i < name.Length && char.IsDigit(name[i])) i++;
            int.TryParse(name.Substring(0, i), out int result);
            return result;
        }

        // ---------- World map buttons ----------

        public static List<GameObject> GetWorldButtons()
        {
            var results = new List<GameObject>();
            foreach (var button in Object.FindObjectsOfType<UnityEngine.UI.Button>())
            {
                if (!button.IsInteractable()) continue;
                string name = button.gameObject.name;
                if (name == "Location Button" || name == "Character Button")
                    results.Add(button.gameObject);
            }
            return results;
        }

        // DescribeStatus (the old unbound "status" reader) removed 2026-07-20: its
        // facts all live in bound readers now — vitals/cryo in DescribeVitals (C),
        // tracked drive in the map table's Drives column and Tracked Drives tab.

        /// <summary>Energy and condition, read from the HUD bar FSMs that drive the rendered
        /// meters (live-verified 2026-07-18). The Cycle Controller's same-named locals are only
        /// populated transiently during the end-cycle computation — zero at Idle. The game
        /// renders no cycle number in station view, so none is spoken (clocks-not-content;
        /// the save-slot label's cycle display is a pending separate source).</summary>
        public static string MetersBrief()
        {
            var sb = new StringBuilder();
            var energyFsm = FindFsm("Energy Bar System", "Energy UI");
            if (energyFsm != null && TryReadFsmNumber(energyFsm, "Player Energy", out int energy))
                sb.Append("Energy ").Append(EnergyBoxes(energy)).Append(". ");
            var conditionFsm = FindFsm("Condition System", "Energy UI");
            if (conditionFsm != null && TryReadFsmNumber(conditionFsm, "Player Condition", out int condition))
            {
                sb.Append("Condition ").Append(ConditionBoxes(condition));
                // The FSM holds the rendered condition label in per-band string vars;
                // exactly the current one is non-empty (e.g. Fading = "FADING").
                string word = ConditionWord(conditionFsm);
                if (word != null) sb.Append(", ").Append(word.ToLowerInvariant());
                sb.Append('.');
            }
            if (energyFsm == null || conditionFsm == null)
                Plugin.Log.LogInfo("[Status] HUD meter FSM missing: energy="
                    + (energyFsm != null) + " condition=" + (conditionFsm != null));
            return sb.Length > 0 ? sb.ToString().TrimEnd() : null;
        }

        // ---------- Segments-first stat reads (owner ruling 2026-07-20) ----------
        // Energy renders 5 boxes at 20 points (4 Splitter objects; FSM Setter index =
        // box count, live pairs 2↔40 / 3↔60 / 5↔100). Condition renders 20 boxes at
        // 5 points (art-level ticks, owner-counted; chunk live-confirmed 65→60).
        // All spoken stat values use filled-box form; band words ride with the count.

        public static string EnergyBoxes(int value)
            => Mathf.RoundToInt(value / 20f) + " of 5";

        public static string ConditionBoxes(int value)
            => Mathf.RoundToInt(value / 5f) + " of 20";

        private static readonly string[] ConditionWordVars =
            { "Stable", "Flickering", "Fading", "Declining", "Breaking", "Attempting Recovery" };

        /// <summary>The Condition System keeps one string var per display label; only the
        /// current condition band's var is non-empty.</summary>
        private static string ConditionWord(PlayMakerFSM fsm)
        {
            foreach (var name in ConditionWordVars)
            {
                string v = fsm.FsmVariables.GetFsmString(name)?.Value;
                if (!string.IsNullOrEmpty(v)) return v;
            }
            return null;
        }

        /// <summary>Presence-aware numeric read: distinguishes a genuine 0 from a missing
        /// variable (energy 0 is a real, meaningful value).</summary>
        private static bool TryReadFsmNumber(PlayMakerFSM fsm, string name, out int value)
        {
            var iv = fsm.FsmVariables.GetFsmInt(name);
            if (iv != null) { value = iv.Value; return true; }
            var fv = fsm.FsmVariables.GetFsmFloat(name);
            if (fv != null) { value = Mathf.RoundToInt(fv.Value); return true; }
            value = 0;
            return false;
        }

        // ---------- V and D queries (input-model.md; wording provisional until calibration) ----------

        /// <summary>V: "Cycle 4. Energy 3 of 5. Condition 8 of 20, declining. Cryo 80."
        /// Values from the W1 Lua adapter; condition band word and STARVING from the
        /// rendered HUD. Segments-first (owner ruling 2026-07-20): energy 5 boxes at
        /// 20 pts, condition 20 boxes at 5 pts (art-tick render, owner-counted).
        /// Cryo stays numeric (value resource).</summary>
        public static string DescribeVitals()
        {
            var sb = new StringBuilder();
            int? cycle = Substrate.LuaStore.CycleNumber();
            if (cycle != null) sb.Append("Cycle ").Append(cycle).Append(". ");
            // Segments-first (owner ruling 2026-07-20): calibrated 20 pts/box × 5.
            int? energy = Substrate.LuaStore.Energy();
            if (energy != null)
            {
                sb.Append("Energy ").Append(EnergyBoxes(energy.Value));
                var starving = GameObject.Find("Letterbox Canvas/Top UI/Energy UI/Energy Bar System/Starving");
                if (starving != null && starving.activeInHierarchy) sb.Append(", starving");
                sb.Append(". ");
            }
            int? condition = Substrate.LuaStore.Condition();
            if (condition != null)
            {
                sb.Append("Condition ").Append(ConditionBoxes(condition.Value));
                var conditionFsm = FindFsm("Condition System", "Energy UI");
                string band = conditionFsm != null ? ConditionWord(conditionFsm) : null;
                if (band != null) sb.Append(", ").Append(band.ToLowerInvariant());
                sb.Append(". ");
            }
            int? bits = Substrate.LuaStore.Bits();
            if (bits != null) sb.Append("Cryo ").Append(bits).Append(". ");
            return sb.Length > 0 ? sb.ToString().TrimEnd() : "Vitals not available.";
        }

        /// <summary>D: "3 dice: 6, 2, 5." — flat string per input-model (tray is not
        /// natively focusable outside allocation). Spent handling provisional.</summary>
        public static string DescribeDiceBrief()
        {
            var dice = GetDice();
            if (dice.Count == 0) return "No dice.";
            var values = new List<string>();
            int spent = 0;
            foreach (var d in dice)
            {
                if (d.State == "Used") spent++;
                else values.Add(d.Value.ToString());
            }
            var sb = new StringBuilder();
            if (values.Count > 0)
                sb.Append(values.Count).Append(values.Count == 1 ? " die: " : " dice: ")
                  .Append(string.Join(", ", values)).Append('.');
            else sb.Append("No dice left.");
            if (spent > 0) sb.Append(' ').Append(spent).Append(" spent.");
            // ENDURE perk 2: the cycle reset renders a "PERK: Hard to Kill" badge
            // beside the dice when the perk kept the extra die (corpus, Reset 1
            // PERKED); GameObject.Find sees active objects only, so this speaks
            // exactly while the game shows it.
            if (GameObject.Find("PERK: Hard to Kill") != null)
                sb.Append(" Perk: Hard to Kill.");
            return sb.ToString();
        }

        // ---------- Dice allocation mode (native uGUI picker; see docs/ui-state-map.md 6b) ----------

        /// <summary>The Dice Gamepad System FSM owns allocation mode: Off -> Active while the
        /// die picker is open. The picker's cursors are ordinary uGUI Buttons the game itself
        /// selects into the EventSystem, so arrows and Enter drive them natively.</summary>
        public static PlayMakerFSM DiceSystemFsm()
        {
            return FindFsm("Dice Gamepad System", "Dice UI");
        }

        private static float _lastDiceLive = -10f;

        /// <summary>Dice-allocation mode = the Dice Gamepad System doing ANYTHING but
        /// resting Off (bridge-decoded states: Active / Slotted / Reselector / Setup).
        /// The old "Active only" test missed Reselector (the Active->Back cancel path)
        /// and Setup, dropping the mode mid-interaction — a Backspace retract then
        /// misrouted and a follow-up press leaked to the action commit, spending the
        /// slotted die (live find 2026-07-21). A short hysteresis holds the mode across
        /// a one-frame null/empty FSM read while a die rests, so the same leak can't
        /// open in a transition gap. Covers the whole retract-and-reselect loop.</summary>
        public static bool DiceAllocationActive()
        {
            var fsm = DiceSystemFsm();
            string s = fsm != null ? fsm.ActiveStateName : null;
            bool live = !string.IsNullOrEmpty(s) && s != "Off";
            if (live) _lastDiceLive = Time.unscaledTime;
            return live || Time.unscaledTime - _lastDiceLive < 0.25f;
        }

        /// <summary>Spoken description of the die a picker cursor stands for.
        /// Owner wording ruling 2026-07-20: value leads — "value X, die a of b".</summary>
        public static string DescribeDieForCursor(int cursorNumber)
        {
            var dice = GetDice();
            int ordinal = 0;
            foreach (var die in dice)
            {
                ordinal++;
                if (die.SlotNumber != cursorNumber) continue;
                string pos = "die " + ordinal + " of " + dice.Count;
                if (die.State == "Used") return "spent, " + pos;
                return "value " + die.Value + ", " + pos;
            }
            return "Die " + cursorNumber;
        }
    }
}
