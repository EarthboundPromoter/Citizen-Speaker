using System.Collections.Generic;
using CSAccess.Game;
using CSAccess.Patches;
using CSAccess.Speech;
using CSAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess
{
    /// <summary>Keyboard commands. Polled from Plugin.Update via legacy Input.</summary>
    internal class InputManager
    {
        private readonly MonoBehaviour _host;
        private List<Transform> _actionPanels = new List<Transform>();
        private int _actionIndex = -1;
        private List<GameObject> _worldButtons = new List<GameObject>();
        private int _worldIndex = -1;

        public InputManager(MonoBehaviour host)
        {
            _host = host;
        }

        public void Tick()
        {
            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

            // --- Speech control ---
            if (Input.GetKeyDown(KeyCode.BackQuote)) { SpeechService.Stop(); return; }
            if (Input.GetKeyDown(KeyCode.R) && !shift) { SpeechService.RepeatLast(); return; }
            if (Input.GetKeyDown(KeyCode.LeftBracket)) { SpeechService.HistoryBack(); return; }
            if (Input.GetKeyDown(KeyCode.RightBracket)) { SpeechService.HistoryForward(); return; }

            // --- Help ---
            if (Input.GetKeyDown(KeyCode.F1)) { SpeakHelp(); return; }

            // --- Repeat dialogue line ---
            if (Input.GetKeyDown(KeyCode.F2))
            {
                if (!string.IsNullOrEmpty(DialogueState.LastSubtitle))
                {
                    string speaker = DialogueState.LastSpeaker;
                    bool sayName = speaker.Length > 0 && Plugin.SpeakSpeakerNames.Value &&
                                   !speaker.Equals("UNKNOWN", System.StringComparison.OrdinalIgnoreCase);
                    SpeechService.Say((sayName ? speaker + ": " : "") + DialogueState.LastSubtitle,
                        Priority.Immediate, "dialogue");
                }
                else
                    SpeechService.Say("No dialogue line.", Priority.Immediate, "dialogue");
                return;
            }

            // --- Status queries ---
            if (Input.GetKeyDown(KeyCode.D) && !shift) { SpeechService.Say(GameQueries.DescribeDice(), Priority.Immediate, "query"); return; }
            if (Input.GetKeyDown(KeyCode.K)) { SpeechService.Say(GameQueries.DescribeClocks(), Priority.Immediate, "query"); return; }
            if (Input.GetKeyDown(KeyCode.C)) { SpeechService.Say(GameQueries.DescribeStatus(), Priority.Immediate, "query"); return; }

            // --- Detailed description of focused element ---
            if (Input.GetKeyDown(KeyCode.Space))
            {
                var current = Navigator.Current();
                if (current != null)
                    SpeechService.Say(Describe.Element(current, detailed: true), Priority.Immediate, "focus");
                else
                    SpeechService.Say("Nothing focused.", Priority.Immediate, "focus");
                return;
            }

            // --- Tutorial review mode (T to continue falls through to its handler below) ---
            if (TutorialReview.IsActive())
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) { TutorialReview.Review(1); return; }
                if (Input.GetKeyDown(KeyCode.UpArrow)) { TutorialReview.Review(-1); return; }
            }

            // --- Character creation review mode (Enter falls through to global activate) ---
            if (CharacterSelect.IsActive())
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) { CharacterSelect.Review(1); return; }
                if (Input.GetKeyDown(KeyCode.UpArrow)) { CharacterSelect.Review(-1); return; }
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { CharacterSelect.ChangeClass(right: false); return; }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { CharacterSelect.ChangeClass(right: true); return; }
            }

            // --- Choice number keys (when a response menu is open) ---
            if (DialogueState.MenuOpen)
            {
                for (int i = 0; i < 9 && i < DialogueState.CurrentResponses.Count; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        PickResponse(i);
                        return;
                    }
                }
            }
            else
            {
                // --- Dice number keys (when an action is focused) ---
                for (int i = 0; i < 6; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i))
                    {
                        TrySlotDie(i + 1);
                        return;
                    }
                }
            }

            // --- Action cycling at a location ---
            if (Input.GetKeyDown(KeyCode.Tab))
            {
                CycleActions(shift ? -1 : 1);
                return;
            }

            // --- World map cycling ---
            if (Input.GetKeyDown(KeyCode.L))
            {
                CycleWorldButtons(shift ? -1 : 1);
                return;
            }

            // --- Button-bound UI the navigation graph can't reach (game binds these to
            //     controller shoulder buttons; we bind keys) ---
            if (Input.GetKeyDown(KeyCode.I))
            {
                ClickFirstActive("Inventory toggle",
                    "Letterbox Canvas/Bottom UI/Inventory/ITEM Button",
                    "Letterbox Canvas/Bottom UI/Inventory/DATA Button");
                return;
            }
            if (Input.GetKeyDown(KeyCode.U))
            {
                ClickFirstActive("Character window",
                    "Letterbox Canvas/Character UI/Character UI Button");
                return;
            }
            if (Input.GetKeyDown(KeyCode.J))
            {
                ClickFirstActive("Drive tracker",
                    "Letterbox Canvas/Drive System/Drive Tracker HUD",
                    "Letterbox Canvas/Drive System");
                return;
            }
            if (Input.GetKeyDown(KeyCode.S))
            {
                ClickFirstActive("Scan",
                    "Letterbox Canvas/Top UI/Scan Button");
                return;
            }
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                ClickFirstActive("Leave or back",
                    "Letterbox Canvas/Top UI/Leave Button",
                    "Back Button", "Close Button", "BACK");
                return;
            }
            if (Input.GetKeyDown(KeyCode.R) && shift)
            {
                ClickFirstActive("Reroll dice",
                    "Letterbox Canvas/Top UI/Dice UI/REROLL DICE");
                return;
            }

            // --- Tutorial continue ---
            if (Input.GetKeyDown(KeyCode.T))
            {
                var button = GameObject.Find("Letterbox Canvas/Tutorial System/Button");
                if (button != null && button.activeInHierarchy) Navigator.Click(button);
                else SpeechService.Say("No tutorial open.", Priority.Immediate, "nav");
                return;
            }

            // --- Navigation and activation: the game has NO keyboard UI input of its own.
            //     Arrows emulate dpad moves through the game's navigation graph;
            //     Enter submits the current selection. ---
            if (Input.GetKeyDown(KeyCode.DownArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Down); return; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Up); return; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Left); return; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Right); return; }
            if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            {
                Navigator.ActivateCurrent();
                return;
            }
        }

        /// <summary>Activate a controller-button-bound element: prefer a uGUI Button on the
        /// object or its children, else fall back to a Click event on its FSM.</summary>
        private static void ClickFirstActive(string label, params string[] paths)
        {
            foreach (var path in paths)
            {
                var go = GameObject.Find(path);
                if (go == null || !go.activeInHierarchy) continue;

                var button = go.GetComponent<Button>() ?? go.GetComponentInChildren<Button>(false);
                if (button != null && button.IsInteractable())
                {
                    Navigator.Click(button.gameObject);
                    return;
                }
                var fsm = go.GetComponent<PlayMakerFSM>() ?? go.GetComponentInChildren<PlayMakerFSM>(false);
                if (fsm != null)
                {
                    fsm.SendEvent("Click");
                    SpeechService.Say(label + ".", Priority.Immediate, "nav");
                    return;
                }
            }
            SpeechService.Say(label + " not available.", Priority.Immediate, "nav");
        }

        private void PickResponse(int index)
        {
            string text = DialogueState.CurrentResponses[index];
            var buttonName = "Response: " + text;
            foreach (var b in Object.FindObjectsOfType<Button>())
            {
                if (b.gameObject.name == buttonName && b.IsInteractable())
                {
                    DialogueState.MenuOpen = false;
                    Navigator.Click(b.gameObject);
                    return;
                }
            }
            SpeechService.Say("Choice not found.", Priority.Immediate, "choices");
        }

        private void TrySlotDie(int dieNumber)
        {
            var current = Navigator.Current();
            var actionRoot = current != null ? Describe.FindActionRoot(current.transform) : null;
            if (actionRoot == null && _actionIndex >= 0 && _actionIndex < _actionPanels.Count)
                actionRoot = _actionPanels[_actionIndex];
            if (actionRoot == null)
            {
                SpeechService.Say("No action focused. Press Tab to cycle actions.", Priority.Immediate, "dice");
                return;
            }
            _host.StartCoroutine(GameQueries.SlotDieRoutine(actionRoot, dieNumber));
        }

        private void CycleActions(int delta)
        {
            _actionPanels = GameQueries.GetActionPanels();
            if (_actionPanels.Count == 0)
            {
                SpeechService.Say("No actions here.", Priority.Immediate, "nav");
                return;
            }
            _actionIndex = ((_actionIndex + delta) % _actionPanels.Count + _actionPanels.Count) % _actionPanels.Count;
            var panel = _actionPanels[_actionIndex];

            // Focus the action's button so Enter and dice keys apply to it.
            var button = panel.GetComponentInChildren<Button>(false);
            if (button != null)
                Navigator.Select(button.gameObject);
            else
                SpeechService.Say(Describe.DescribeAction(panel.gameObject, detailed: false), Priority.Immediate, "nav");
        }

        private void CycleWorldButtons(int delta)
        {
            _worldButtons = GameQueries.GetWorldButtons();
            if (_worldButtons.Count == 0)
            {
                SpeechService.Say("No locations or characters available.", Priority.Immediate, "nav");
                return;
            }
            _worldIndex = ((_worldIndex + delta) % _worldButtons.Count + _worldButtons.Count) % _worldButtons.Count;
            Navigator.Select(_worldButtons[_worldIndex]);
        }

        private static void SpeakHelp()
        {
            SpeechService.Say(
                "Citizen Sleeper Access commands. " +
                "Arrows and Enter use the game's own navigation. Space: describe the focused element in detail. " +
                "Tab: jump between actions at this location. L: jump between locations and characters. " +
                "I: toggle inventory items and data. U: character window. J: drive tracker. " +
                "S: scan. Backspace: leave or back. Shift R: reroll dice. " +
                "Number keys: pick a dialogue choice, or slot that die into the focused action. " +
                "D: read dice. K: read clocks. C: read status. " +
                "F2: repeat dialogue line. R: repeat last speech. " +
                "Left and right brackets: speech history. Grave: stop speech. T: continue tutorial.",
                Priority.Immediate, "help");
        }
    }
}
