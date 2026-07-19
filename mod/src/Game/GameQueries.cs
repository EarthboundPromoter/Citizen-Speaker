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

        /// <summary>The Cycle Controller sits in Idle except while running the end-cycle
        /// pipeline (live-verified; state list in docs/verification/A). During that pipeline
        /// the game walks focus across every location's slots — announcements gate on this.</summary>
        public static bool CycleTransitionActive()
        {
            var fsm = CycleControllerFsm();
            return fsm != null && fsm.ActiveStateName != "Idle";
        }

        /// <summary>Durable commit signal: the game writes the slotted die's value here.</summary>
        public static float SlottedDiceValueGlobal()
        {
            var v = HutongGames.PlayMaker.FsmVariables.GlobalVariables.GetFsmFloat("SlottedDiceValueGlobal");
            return v != null ? v.Value : 0f;
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
                    if (child.name.EndsWith(" Action"))
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
                    if (child.name.EndsWith(" Clock"))
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
                string name = Describe.TextUnder(clock, "Clock Name") ?? clock.name;
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
        private static string ClockProgress(Transform clock)
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
                if (steps > 0) sb.Append(value).Append(" of ").Append(steps).Append(" segments, ");
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

        // ---------- Status ----------

        public static string DescribeStatus()
        {
            var sb = new StringBuilder();

            string meters = MetersBrief();
            if (meters != null) sb.Append(meters).Append(' ');

            var cryo = GameObject.Find("Letterbox Canvas/Bottom UI/Inventory/ITEM Inventory UI/Cryo Slot /Amount");
            if (cryo != null)
            {
                var tmp = cryo.GetComponent<TMP_Text>();
                if (tmp != null) sb.Append("Cryo ").Append(tmp.text.Trim().TrimEnd('.')).Append(". ");
            }
            var driveRoot = GameObject.Find("Letterbox Canvas/Drive System/Drive Tracker HUD");
            if (driveRoot != null)
            {
                foreach (var tmp in driveRoot.GetComponentsInChildren<TMP_Text>(false))
                {
                    string txt = tmp.text?.Trim();
                    if (!string.IsNullOrEmpty(txt)) sb.Append(txt.TrimEnd('.')).Append(". ");
                }
            }
            return sb.Length > 0 ? "Status: " + sb.ToString().TrimEnd() : "Status not available.";
        }

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
                sb.Append("Energy ").Append(energy).Append(". ");
            var conditionFsm = FindFsm("Condition System", "Energy UI");
            if (conditionFsm != null && TryReadFsmNumber(conditionFsm, "Player Condition", out int condition))
            {
                sb.Append("Condition ").Append(condition);
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

        // ---------- Dice allocation mode (native uGUI picker; see docs/ui-state-map.md 6b) ----------

        /// <summary>The Dice Gamepad System FSM owns allocation mode: Off -> Active while the
        /// die picker is open. The picker's cursors are ordinary uGUI Buttons the game itself
        /// selects into the EventSystem, so arrows and Enter drive them natively.</summary>
        public static PlayMakerFSM DiceSystemFsm()
        {
            return FindFsm("Dice Gamepad System", "Dice UI");
        }

        public static bool DiceAllocationActive()
        {
            var fsm = DiceSystemFsm();
            return fsm != null && fsm.ActiveStateName == "Active";
        }

        /// <summary>Spoken description of the die a picker cursor stands for.</summary>
        public static string DescribeDieForCursor(int cursorNumber)
        {
            foreach (var die in GetDice())
            {
                if (die.SlotNumber != cursorNumber) continue;
                if (die.State == "Used") return "Die " + cursorNumber + ", spent";
                return "Die " + cursorNumber + ", value " + die.Value;
            }
            return "Die " + cursorNumber;
        }
    }
}
