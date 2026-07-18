using System.Collections.Generic;
using CSAccess.Game;
using CSAccess.Speech;
using TMPro;
using UnityEngine;

namespace CSAccess
{
    /// <summary>Polled watchers for UI that appears without a hookable code path:
    /// tutorial popups, character notifications, and gamepad-mode enforcement.</summary>
    internal class Watchers
    {
        private const float Interval = 0.4f;
        private float _nextCheck;

        private readonly HashSet<int> _seenPanels = new HashSet<int>();
        private Transform _tutorialRoot;
        private Transform _notificationRoot;
        private string _lastScene = "";

        public void Tick()
        {
            if (Time.unscaledTime < _nextCheck) return;
            _nextCheck = Time.unscaledTime + Interval;

            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene != _lastScene)
            {
                _lastScene = scene;
                _tutorialRoot = null;
                _notificationRoot = null;
                _seenPanels.Clear();
                if (scene.Contains("MAIN TITLE"))
                    SpeechService.Say("Main menu.", Priority.Queued, "scene");
                else if (scene.Contains("MAIN"))
                {
                    SpeechService.Say("On station.", Priority.Queued, "scene");
                    DumpGlobalVariables();
                }
            }

            CheckTutorial();
            CheckNotifications();
            CheckClassCarousel();
            CheckActionOutcomes();
            CheckDiceAllocation();
            CheckSoleContinue();
            CheckResponseFocus();
            CheckPauseMenu();
            CheckDriveLog();
            // Mode switching is last-input-wins in InputManager.Tick now — the old 3s
            // re-assert loop fought sighted co-pilot mouse use (hid the cursor).
        }

        private Transform _pauseCanvas;
        private bool _pauseWasOpen;

        /// <summary>On pause-menu open, speak the menu's own last-autosave line — the game is
        /// autosave-only, so this is what "can I safely quit?" sounds like.</summary>
        private void CheckPauseMenu()
        {
            if (_pauseCanvas == null)
            {
                var root = GameObject.Find("PAUSE");
                if (root == null) return;
                _pauseCanvas = root.transform.Find("Pause Canvas");
                if (_pauseCanvas == null) return;
            }
            bool open = _pauseCanvas.gameObject.activeInHierarchy;
            if (open && !_pauseWasOpen)
            {
                var autosave = _pauseCanvas.Find("Time SInce Last Autosave");
                string line = autosave != null ? UI.Describe.JoinTexts(autosave.gameObject, 3) : null;
                SpeechService.Say("Paused." + (line != null ? " " + line + "." : ""),
                    Priority.Queued, "nav");
            }
            _pauseWasOpen = open;
        }

        /// <summary>One-time diagnostic: log all PlayMaker global variables so we can learn
        /// where condition/energy/cycle live (they're rendered as bar graphics only).</summary>
        private static void DumpGlobalVariables()
        {
            try
            {
                var g = HutongGames.PlayMaker.FsmVariables.GlobalVariables;
                var sb = new System.Text.StringBuilder("[Globals]");
                foreach (var v in g.IntVariables) sb.Append(" int:").Append(v.Name).Append('=').Append(v.Value);
                foreach (var v in g.FloatVariables) sb.Append(" float:").Append(v.Name).Append('=').Append(v.Value);
                foreach (var v in g.BoolVariables) sb.Append(" bool:").Append(v.Name).Append('=').Append(v.Value);
                foreach (var v in g.StringVariables) sb.Append(" str:").Append(v.Name).Append('=').Append(v.Value);
                Plugin.Log.LogInfo(sb.ToString());
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[Globals] dump failed: " + e.Message);
            }
            DumpStatFsms();
        }

        /// <summary>Condition/energy/cycle aren't globals — find them on the HUD widget FSMs.</summary>
        private static void DumpStatFsms()
        {
            try
            {
                foreach (var fsm in PlayMakerFSM.FsmList)
                {
                    if (fsm == null || fsm.gameObject == null) continue;
                    string path = Game.GameQueries.PathOf(fsm.gameObject);
                    string lower = path.ToLowerInvariant();
                    if (!lower.Contains("energy ui") && !lower.Contains("condition") &&
                        !lower.Contains("cycle clock") && !lower.Contains("flicker"))
                        continue;
                    var sb = new System.Text.StringBuilder("[StatFsm] ").Append(path)
                        .Append(" (").Append(fsm.FsmName).Append(") state=").Append(fsm.ActiveStateName);
                    foreach (var v in fsm.FsmVariables.IntVariables) sb.Append(" int:").Append(v.Name).Append('=').Append(v.Value);
                    foreach (var v in fsm.FsmVariables.FloatVariables) sb.Append(" float:").Append(v.Name).Append('=').Append(v.Value);
                    Plugin.Log.LogInfo(sb.ToString());
                }
            }
            catch (System.Exception e)
            {
                Plugin.Log.LogWarning("[StatFsm] dump failed: " + e.Message);
            }
        }

        private void CheckTutorial()
        {
            if (_tutorialRoot == null)
            {
                var go = GameObject.Find("Letterbox Canvas/Tutorial System");
                if (go == null) return;
                _tutorialRoot = go.transform;
            }
            foreach (Transform panel in _tutorialRoot)
            {
                if (!panel.gameObject.activeInHierarchy) continue;
                if (panel.name == "Button") continue;
                int id = panel.GetInstanceID();
                if (_seenPanels.Contains(id)) continue;
                _seenPanels.Add(id);
                AnnouncePanelTexts(panel, "tutorial", "Tutorial. ", ". Press Enter to continue.");
            }
            // Allow re-announcing panels after they close.
            _seenPanels.RemoveWhere(id =>
            {
                foreach (Transform panel in _tutorialRoot)
                    if (panel.GetInstanceID() == id && panel.gameObject.activeInHierarchy)
                        return false;
                return true;
            });
        }

        private readonly HashSet<int> _visibleNotifications = new HashSet<int>();
        private bool _notificationsBaselined;

        private void CheckNotifications()
        {
            if (_notificationRoot == null)
            {
                var go = GameObject.Find("Letterbox Canvas/Character Notifications");
                if (go == null) return;
                _notificationRoot = go.transform;
                _notificationsBaselined = false;
                _visibleNotifications.Clear();
            }
            foreach (Transform category in _notificationRoot)
            {
                foreach (Transform notification in category)
                {
                    int id = notification.GetInstanceID();
                    bool visible = notification.gameObject.activeInHierarchy &&
                                   EffectiveAlpha(notification) > 0.5f;
                    if (!visible)
                    {
                        _visibleNotifications.Remove(id);
                        continue;
                    }
                    if (_visibleNotifications.Add(id) && _notificationsBaselined)
                        AnnouncePanelTexts(notification, "notify", "", "");
                }
            }
            _notificationsBaselined = true;
        }

        /// <summary>Notification templates stay active and are hidden via CanvasGroup alpha —
        /// multiply alphas up to the notification root to get real visibility.</summary>
        private float EffectiveAlpha(Transform t)
        {
            float alpha = 1f;
            for (var cur = t; cur != null && cur != _notificationRoot.parent; cur = cur.parent)
            {
                var group = cur.GetComponent<CanvasGroup>();
                if (group != null) alpha *= group.alpha;
            }
            return alpha;
        }

        private static readonly string[] ClassNames = { "EXTRACTOR", "OPERATOR", "MACHINIST" };
        private static readonly string[] SkillWords = { "ENGINEER", "INTERFACE", "ENDURE", "INTUIT", "ENGAGE" };
        private static readonly string[] ModifierWords = { "+2", "+1", "-1", "0" };
        private string _lastClass;

        /// <summary>Announce the class centered in the character-select carousel whenever it changes.</summary>
        private void CheckClassCarousel()
        {
            var canvas = GameObject.Find("MAIN MENU/Demo Menu/Character Select Canvas");
            if (canvas == null)
            {
                _lastClass = null;
                return;
            }
            Transform movingPanel = canvas.transform.Find("Moving Panel");
            if (movingPanel == null) return;

            Transform centered = null;
            float bestDistance = float.MaxValue;
            float centerX = canvas.transform.position.x;
            foreach (Transform child in movingPanel)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (System.Array.IndexOf(ClassNames, child.name) < 0) continue;
                float d = Mathf.Abs(child.position.x - centerX);
                if (d < bestDistance) { bestDistance = d; centered = child; }
            }
            if (centered == null || centered.name == _lastClass) return;
            bool firstAnnounce = _lastClass == null;
            _lastClass = centered.name;
            UI.CharacterSelect.ResetReview();
            SpeechService.Say(firstAnnounce
                    ? centered.name + ". Up and Down to review details, Left and Right to change class, Enter to start."
                    : centered.name,
                Priority.Immediate, "class");
        }

        private long _lastSubtitleSeq;

        /// <summary>When a line ends and the conversation has no upcoming player choices,
        /// announce Continue as the sole option. The Dialogue System's conversation state
        /// knows choices are coming long before the menu appears.</summary>
        private void CheckSoleContinue()
        {
            long seq = Patches.DialogueState.SubtitleSeq;
            if (seq == _lastSubtitleSeq) return;
            _lastSubtitleSeq = seq;
            if (Patches.DialogueState.MenuOpen) return;
            try
            {
                var state = PixelCrushers.DialogueSystem.DialogueManager.currentConversationState;
                if (state != null && state.hasPCResponses) return;
            }
            catch { }
            var button = GameObject.Find(
                "CS Dialogue Manager/Canvas/TMP CS Dialogue UI 1/Dialogue Panel/Main Panel/Continue Button");
            if (button != null)
                SpeechService.Say("Continue", Priority.Queued, "choices");
        }

        /// <summary>If a response menu is open and nothing (or a non-response) is focused,
        /// focus the first choice so it announces.</summary>
        private void CheckResponseFocus()
        {
            if (!Patches.DialogueState.MenuOpen) return;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            var selected = es.currentSelectedGameObject;
            if (selected != null && selected.name.StartsWith("Response: ")) return;
            foreach (var b in Object.FindObjectsOfType<UnityEngine.UI.Button>())
            {
                if (b.gameObject.name.StartsWith("Response: ") && b.IsInteractable())
                {
                    es.SetSelectedGameObject(b.gameObject);
                    return;
                }
            }
        }

        private readonly Dictionary<int, string> _controllerStates = new Dictionary<int, string>();

        /// <summary>Announce action results by watching Action Controller FSM state transitions,
        /// regardless of how the action was started (mouse, Enter, or dice keys).</summary>
        private void CheckActionOutcomes()
        {
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || fsm.gameObject == null) continue;
                if (fsm.gameObject.name != "Action Controller" || !fsm.gameObject.activeInHierarchy) continue;

                int id = fsm.GetInstanceID();
                string state = fsm.ActiveStateName;
                if (_controllerStates.TryGetValue(id, out string previous) && previous == state) continue;
                bool known = _controllerStates.ContainsKey(id);
                _controllerStates[id] = state;
                if (!known) continue;

                string outcome = state switch
                {
                    "Positive Outcome" => "positive outcome",
                    "Neutral Outcome" => "neutral outcome",
                    "Negative Outcome" => "negative outcome",
                    _ => null,
                };
                if (outcome == null) continue;

                var actionRoot = UI.Describe.FindActionRoot(fsm.transform);
                string actionName = actionRoot != null
                    ? (UI.Describe.TextUnder(actionRoot, "Action Name") ?? actionRoot.name)
                    : "Action";
                SpeechService.Say(actionName + ": " + outcome + ".", Priority.Queued, "outcome");
            }
            if (_controllerStates.Count > 300) _controllerStates.Clear();
        }

        private string _diceSystemState;
        private bool _diceStateKnown;
        private float _lastSlottedTime = -10f;
        private readonly Dictionary<int, string> _diceSlotStates = new Dictionary<int, string>();

        /// <summary>Announce dice-allocation mode transitions. The Dice Gamepad System FSM is
        /// Off outside the picker and Active inside it; the game selects the cursor Buttons
        /// into the EventSystem itself, so per-die announcements ride the focus watcher
        /// (see docs/ui-state-map.md 6b).</summary>
        private void CheckDiceAllocation()
        {
            var system = GameQueries.DiceSystemFsm();
            if (system == null) return;
            string state = system.ActiveStateName;
            if (!_diceStateKnown)
            {
                _diceSystemState = state;
                _diceStateKnown = true;
            }
            else if (state != _diceSystemState)
            {
                string previous = _diceSystemState;
                _diceSystemState = state;
                if (state == "Active")
                    SpeechService.Say("Choose a die. Arrows to choose, Enter to slot, Backspace to cancel.",
                        Priority.Immediate, "dice");
                else if (previous == "Active" && Time.unscaledTime - _lastSlottedTime > 1.5f)
                    SpeechService.Say("Die picker closed.", Priority.Immediate, "dice");
            }

            // A slot FSM reaching Slotted is the commit signal; the outcome watcher speaks next.
            foreach (var fsm in PlayMakerFSM.FsmList)
            {
                if (fsm == null || fsm.gameObject == null) continue;
                if (fsm.gameObject.name != "Gamepad Dice Slot" || !fsm.gameObject.activeInHierarchy) continue;
                int id = fsm.GetInstanceID();
                string slotState = fsm.ActiveStateName;
                if (_diceSlotStates.TryGetValue(id, out string prev) && prev == slotState) continue;
                bool known = _diceSlotStates.ContainsKey(id);
                _diceSlotStates[id] = slotState;
                if (!known || slotState != "Slotted") continue;

                _lastSlottedTime = Time.unscaledTime;
                var root = UI.Describe.FindActionRoot(fsm.transform);
                string actionName = root != null
                    ? (UI.Describe.TextUnder(root, "Action Name") ?? root.name)
                    : "action";
                SpeechService.Say("Die slotted. " + actionName + ".", Priority.Queued, "dice");
            }
            if (_diceSlotStates.Count > 300) _diceSlotStates.Clear();
        }

        private Transform _driveLog;
        private bool _driveLogWasOpen;

        /// <summary>Announce the drive log window opening/closing. The window hides via its
        /// CanvasGroup (Animator-driven), so visibility is alpha, not active state.</summary>
        private void CheckDriveLog()
        {
            if (_driveLog == null)
            {
                var win = GameObject.Find("Letterbox Canvas/Drive System/CS Drive Log");
                if (win == null) return;
                _driveLog = win.transform;
            }
            var group = _driveLog.GetComponent<CanvasGroup>();
            bool open = _driveLog.gameObject.activeInHierarchy && (group == null || group.alpha > 0.5f);
            if (open != _driveLogWasOpen)
            {
                SpeechService.Say(open ? "Drive log open." : "Drive log closed.",
                    Priority.Immediate, "nav");
                _driveLogWasOpen = open;
            }
        }

        private static void AnnouncePanelTexts(Transform panel, string source, string prefix, string suffix)
        {
            var parts = new List<string>();
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt) || txt.Length <= 1) continue;
                if (!parts.Contains(txt)) parts.Add(txt);
            }
            if (parts.Count == 0) return;
            SpeechService.Say(prefix + string.Join(". ", parts) + suffix, Priority.Queued, source);
        }
    }
}
