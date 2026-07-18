using System.Collections;
using System.Collections.Generic;
using System.Text;
using CSAccess.Speech;
using CSAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

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

        // ---------- Gamepad-mode enforcement ----------

        public static void EnsureGamepadMode()
        {
            var manager = FindFsm("Gamepad Manager");
            if (manager != null && manager.ActiveStateName == "Mouse")
            {
                manager.SendEvent("Gamepad");
                Plugin.Log.LogInfo("[Game] Switched UI to gamepad mode for keyboard dice flow.");
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
                if (d.State == "Used") sb.Append("used");
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

        /// <summary>Best-effort clock progress from the segment FSM's int variables.</summary>
        private static string ClockProgress(Transform clock)
        {
            var fsm = clock.GetComponentInChildren<PlayMakerFSM>(false);
            if (fsm == null) return null;
            int steps = ParseLeadingInt(fsm.gameObject.name);
            foreach (var iv in fsm.FsmVariables.IntVariables)
            {
                string n = iv.Name.ToLowerInvariant();
                if (n.Contains("segment") || n.Contains("progress") || n.Contains("count") || n.Contains("step"))
                    return iv.Value + (steps > 0 ? " of " + steps + " segments" : " segments");
            }
            return null;
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
            var cryo = GameObject.Find("Letterbox Canvas/Bottom UI/Inventory/ITEM Inventory UI/Cryo Slot /Amount");
            if (cryo != null)
            {
                var tmp = cryo.GetComponent<TMP_Text>();
                if (tmp != null) sb.Append("Cryo: ").Append(tmp.text.Trim()).Append(". ");
            }
            var driveRoot = GameObject.Find("Letterbox Canvas/Drive System/Drive Tracker HUD");
            if (driveRoot != null)
            {
                foreach (var tmp in driveRoot.GetComponentsInChildren<TMP_Text>(false))
                {
                    string txt = tmp.text?.Trim();
                    if (!string.IsNullOrEmpty(txt)) sb.Append(txt).Append(". ");
                }
            }
            return sb.Length > 0 ? "Status: " + sb : "Status not available.";
        }

        // ---------- Dice slotting (gamepad FSM path, proven via bridge survey) ----------

        public static IEnumerator SlotDieRoutine(Transform actionRoot, int dieNumber)
        {
            EnsureGamepadMode();
            yield return new WaitForSeconds(0.2f);

            PlayMakerFSM slotFsm = null;
            foreach (var fsm in actionRoot.GetComponentsInChildren<PlayMakerFSM>(true))
            {
                if (fsm.gameObject.name == "Gamepad Dice Slot") { slotFsm = fsm; break; }
            }
            if (slotFsm == null)
            {
                SpeechService.Say("This action does not take dice.", Priority.Immediate, "dice");
                yield break;
            }

            slotFsm.SendEvent("Click");
            yield return new WaitForSeconds(0.35f);

            var cursorFsm = FindFsm("Dice Cursor " + dieNumber);
            if (cursorFsm == null)
            {
                SpeechService.Say("Die " + dieNumber + " not found.", Priority.Immediate, "dice");
                yield break;
            }
            cursorFsm.SendEvent("Click");
            yield return new WaitForSeconds(0.6f);

            string buttonLabel = Describe.TextUnder(actionRoot, "Dice Slot Button");
            if (buttonLabel != null && buttonLabel.ToUpperInvariant().Contains("START"))
                SpeechService.Say("Die " + dieNumber + " slotted. Press Enter to start the action.",
                    Priority.Immediate, "dice");
            else
                SpeechService.Say("Could not slot die " + dieNumber + ". " +
                    (buttonLabel ?? ""), Priority.Immediate, "dice");
        }

    }
}
