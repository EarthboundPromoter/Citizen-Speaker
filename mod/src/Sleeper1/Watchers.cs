using System.Collections.Generic;
using TMPro;
using UnityEngine;

using CSAccess.Scaffold;
using CSAccess.Middleware;

namespace CSAccess.Sleeper1
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
        private int _lastSceneHandle = -1;

        /// <summary>True on the frames the 0.4 s poll runs. Per-frame checks only read
        /// objects they already hold; looking objects up is left to these frames.</summary>
        private bool _pollFrame;

        /// <summary>Two cadences (2026-10-25). Every frame: what answers the player's own
        /// keys (dice slot and odds, pause, the drive log and character window, the
        /// title overwrite warning) and the scene check, so none of it waits up to
        /// 0.4 s for the poll. Every 0.4 s: the rest, plus every GameObject lookup.</summary>
        public void Tick()
        {
            _pollFrame = Time.unscaledTime >= _nextCheck;
            if (_pollFrame) _nextCheck = Time.unscaledTime + Interval;

            CheckScene();
            CheckDiceAllocation();
            CheckDiceOutlook();
            CheckPauseMenu();
            CheckWarningMenus();
            CheckTitleOverwriteWarning();
            CheckDriveLog();
            CheckCharacterWindow();

            if (!_pollFrame) return;

            CheckTutorial();
            CheckNotifications();
            CheckClassCarousel();
            CheckActionOutcomes();
            CheckSoleContinue();
            CheckResponseFocus();
            CheckQrCanvas();
            WindowState.DivergenceTick();
            // Cycle transition arming + completion summary moved to CycleGate
            // (event-armed on the designed EndCycle entry; W2 hardening).
            // Mode switching is last-input-wins in InputManager.Tick now — the old 3s
            // re-assert loop fought sighted co-pilot mouse use (hid the cursor).
        }

        private void CheckScene()
        {
            // The handle compare is allocation-free; the name is read only when it moves.
            var active = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            if (active.handle == _lastSceneHandle) return;
            _lastSceneHandle = active.handle;
            string scene = active.name;
            if (scene != _lastScene)
            {
                _lastScene = scene;
                _tutorialRoot = null;
                _notificationRoot = null;
                _seenPanels.Clear();
                // A new scene re-baselines the dice watcher silently (no stray
                // "Die picker closed." from the old scene's teardown).
                _diceSystem = null;
                _diceStateKnown = false;
                _outlookPending = false;
                _titleWarning = null;
                _qrCanvas = null;
                FocusPatch.OnSceneChanged();
                WindowState.OnSceneChanged();
                StationCensus.OnSceneChanged();
                TitleFlow.OnSceneChanged();
                // Title arrival speaks from the MAIN MENU FSM's own states (TitleFlow)
                // — the scene loads while the engine splash still owns the screen, so
                // a scene-keyed line here spoke over the company crawl (owner report
                // 2026-07-20).
                if (scene.Contains("MAIN") && !scene.Contains("MAIN TITLE"))
                {
                    SpeechService.Say("On station.", Priority.Queued, "scene");
                    // B2: config-gated — these dumps served the original stat-source
                    // hunt and only add log bulk on a routine boot.
                    if (Plugin.DumpStatDiagnostics.Value)
                        DumpGlobalVariables();
                }
            }
        }

        private Transform _pauseCanvas;
        private bool _pauseWasOpen;

        /// <summary>On pause-menu open, speak the menu's own last-autosave line — the game is
        /// autosave-only, so this is what "can I safely quit?" sounds like.</summary>
        private void CheckPauseMenu()
        {
            if (_pauseCanvas == null)
            {
                if (!_pollFrame) return;
                var root = GameObject.Find("PAUSE");
                if (root == null) return;
                _pauseCanvas = root.transform.Find("Pause Canvas");
                if (_pauseCanvas == null) return;
            }
            bool open = _pauseCanvas.gameObject.activeInHierarchy;
            if (open && !_pauseWasOpen)
            {
                var autosave = _pauseCanvas.Find(GameVocab.Warts.TimeSinceLastAutosave);
                string line = autosave != null ? Describe.JoinTexts(autosave.gameObject, 3) : null;
                // C6: the render is three fragments ("TIME SINCE LAST AUTOSAVE",
                // "160", "SECONDS") — join to a sentence instead of comma splices.
                if (line != null)
                {
                    var frag = line.Split(new[] { ", " }, System.StringSplitOptions.None);
                    if (frag.Length == 3 && int.TryParse(frag[1], out _))
                        line = frag[1] + " " + frag[2].ToLowerInvariant() + " since last autosave";
                }
                // Immediate: Escape is the player's own key, and this is its answer.
                SpeechService.Say("Paused." + (line != null ? " " + line + "." : ""),
                    Priority.Immediate, "nav");
                // A6: pause is a world-at-rest beat tail — flush unheard census
                // changes now (queued behind "Paused.") so a quit from here does
                // not silently lose them.
                StationCensus.FlushAtPause();
            }
            _pauseWasOpen = open;
        }

        private readonly Dictionary<string, bool> _warningWasOpen = new Dictionary<string, bool>();
        private static readonly string[] PauseWarningMenus =
            { "Warning Menu Quit Game", "Warning Menu Quit Menu" };

        /// <summary>Pause warning menus speak their rendered warning body on open
        /// (focus-model row 12, W3 addition).</summary>
        private void CheckWarningMenus()
        {
            if (_pauseCanvas == null) return;
            foreach (var name in PauseWarningMenus)
            {
                var menu = _pauseCanvas.Find(name);
                if (menu == null) continue;
                bool open = menu.gameObject.activeInHierarchy;
                _warningWasOpen.TryGetValue(name, out bool was);
                if (open && !was)
                {
                    // The joined body already includes the QUIT/CANCEL button labels —
                    // no appended tail (run 3 spoke "QUIT QUIT or CANCEL").
                    string body = Describe.JoinTexts(menu.gameObject, 2);
                    SpeechService.Say(body ?? "Are you sure? QUIT or CANCEL.",
                        Priority.Queued, "nav");
                }
                _warningWasOpen[name] = open;
            }
        }

        private bool _qrWasOpen;
        private GameObject _qrCanvas;

        /// <summary>Title QR canvas (Update News): announce its rendered text + CLOSE on
        /// open (focus-model row 1 — previously silent). No scene gate: quit-to-title
        /// arrives without a scene change (D1), and the lookup is null elsewhere.</summary>
        private void CheckQrCanvas()
        {
            if (_qrCanvas == null)
                _qrCanvas = GameObject.Find("MAIN MENU/Demo Menu/QR Code Canvas");
            var canvas = _qrCanvas;
            bool open = canvas != null && canvas.activeInHierarchy;
            if (open && !_qrWasOpen)
            {
                string body = Describe.JoinTexts(canvas, 6);
                SpeechService.Say((body ?? "QR code.") + " CLOSE button.", Priority.Queued, "nav");
            }
            _qrWasOpen = open;
        }

        private bool _titleWarningWasOpen;
        private GameObject _titleWarning;

        /// <summary>Title new-game overwrite warning (corpus: one shared
        /// "Demo Menu/Warning Menu" panel serves the per-slot Warning 1/2/3 states of the
        /// MAIN MENU master FSM). Previously silent — a player could overwrite a save
        /// without hearing the warning. Same rendered-body-on-open idiom as the pause
        /// warning menus; the joined body carries its own button labels.
        /// No scene gate (2026-10-25): quit-to-title arrives without a scene change
        /// (D1), and the old MAIN TITLE gate left this warning silent after a quit.
        /// The panel is looked up on poll frames (an inactive panel is not found by
        /// GameObject.Find, so its parent is held) and read every frame.</summary>
        private void CheckTitleOverwriteWarning()
        {
            if (_titleWarning == null)
            {
                if (!_pollFrame) return;
                var demo = GameObject.Find("MAIN MENU/Demo Menu");
                var t = demo != null ? demo.transform.Find("Warning Menu") : null;
                if (t == null) return;
                _titleWarning = t.gameObject;
            }
            var menu = _titleWarning;
            bool open = menu != null && menu.activeInHierarchy;
            if (open && !_titleWarningWasOpen)
            {
                string body = Describe.JoinTexts(menu, 3);
                SpeechService.Say(body ?? "Warning: starting a new game here overwrites this save slot.",
                    Priority.Queued, "nav");
            }
            _titleWarningWasOpen = open;
        }

        /// <summary>Stat-source diagnostic (config-gated, Debug.DumpStatDiagnostics):
        /// log all PlayMaker global variables — the original hunt for where
        /// condition/energy/cycle live (they're rendered as bar graphics only).</summary>
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
                    string path = Util.PathOf(fsm.gameObject);
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
                // Mark seen only when the announce actually spoke (BL-3 root-cause
                // candidate, corpus + code walk 2026-07-20): trigger-activated panels
                // populate text a beat AFTER activation — marking first burned the
                // one announce chance on an empty read, silent forever after.
                if (AnnouncePanelTexts(panel, "tutorial", "Tutorial. ", ". Press Enter to continue."))
                    _seenPanels.Add(id);
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
                    // Alphas multiply up to (not including) the notification root's
                    // parent, so the root's own group counts.
                    bool visible = notification.gameObject.activeInHierarchy &&
                                   Util.AlphaUpTo(notification, _notificationRoot.parent) > 0.5f;
                    if (!visible)
                    {
                        _visibleNotifications.Remove(id);
                        continue;
                    }
                    if (_visibleNotifications.Add(id) && _notificationsBaselined)
                    {
                        // The cloud hack compose already voiced this proc as part of
                        // its own string (owner composition 2026-07-23) — the rendered
                        // notification stands down (A4 lane rule).
                        if (notification.name.StartsWith("INTERFACE - Transfer Intercept")
                            && Time.unscaledTime - CloudOutcomes.LastCryoComposedAt
                               < Timing.CryoComposeSuppress)
                            continue;
                        AnnouncePanelTexts(notification, "notify", "", "");
                    }
                }
            }
            _notificationsBaselined = true;
        }

        /// <summary>The character-select entry read, and class changes our keys did not make
        /// (mouse, gamepad stick). Left/Right presses speak their destination themselves
        /// (CharacterSelect.ChangeClass) and record it in LastSpoken, so nothing repeats here.
        /// The carousel FSM's destination is read first: it is known the moment a slide
        /// starts, where the centered-panel position only flips halfway through it.</summary>
        private void CheckClassCarousel()
        {
            if (!CharacterSelect.IsActive())
            {
                CharacterSelect.LastSpoken = null;
                return;
            }
            string current = CharacterSelect.Destination(CharacterSelect.CarouselFsm());
            if (current == null)
            {
                var centered = CharacterSelect.CenteredClass();
                current = centered != null ? centered.name : null;
            }
            if (current == null || current == CharacterSelect.LastSpoken) return;
            CharacterSelect.Announce(current, intro: CharacterSelect.LastSpoken == null);
        }

        private long _lastSubtitleSeq;

        /// <summary>When a line ends and the conversation has no upcoming player choices,
        /// announce Continue as the sole option. The Dialogue System's conversation state
        /// knows choices are coming long before the menu appears.</summary>
        private void CheckSoleContinue()
        {
            long seq = DialogueState.SubtitleSeq;
            if (seq == _lastSubtitleSeq) return;
            _lastSubtitleSeq = seq;
            if (DialogueState.MenuOpen) return;
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
            if (!DialogueState.MenuOpen) return;
            var es = UnityEngine.EventSystems.EventSystem.current;
            if (es == null) return;
            var selected = es.currentSelectedGameObject;
            if (selected != null && selected.name.StartsWith("Response: ")) return;
            // FindObjectsOfType has no defined order, so the first match was any choice.
            // Prefer the menu's first response by its own button name (2026-10-25).
            string firstName = DialogueState.CurrentResponses.Count > 0
                ? "Response: " + DialogueState.CurrentResponses[0] : null;
            GameObject fallback = null;
            foreach (var b in Object.FindObjectsOfType<UnityEngine.UI.Button>())
            {
                if (!b.gameObject.name.StartsWith("Response: ") || !b.IsInteractable()) continue;
                if (b.gameObject.name == firstName)
                {
                    es.SetSelectedGameObject(b.gameObject);
                    return;
                }
                if (fallback == null) fallback = b.gameObject;
            }
            if (fallback != null) es.SetSelectedGameObject(fallback);
        }

        private readonly Dictionary<int, string> _controllerStates = new Dictionary<int, string>();

        /// <summary>Track Action Controller Idle exits for the dice-commit heuristic.
        /// Outcome ANNOUNCEMENTS moved to ActionOutcomes (FsmSignals clock — the
        /// poll here missed clock-completing outcomes whose variant swap deactivated
        /// the controller between polls, BL-8).</summary>
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

                if (previous == "Idle")
                    _lastControllerLeftIdle = Time.unscaledTime;
            }
            if (_controllerStates.Count > 300) _controllerStates.Clear();
        }

        private PlayMakerFSM _diceSystem;
        private string _diceSystemState;
        private bool _diceStateKnown;
        private float _lastSlottedTime = -10f;
        private float _slottedGlobalAtOpen;
        private float _lastControllerLeftIdle = -10f;
        private bool _outlookPending;
        private float _outlookSince;
        private Transform _slottedActionRoot; // captured at Slotted entry (corpus Q5 guard)

        /// <summary>Slot-odds read (A1): after "Die slotted." fires, WATCH the game's
        /// own die value each frame and speak the odds the instant it has SETTLED —
        /// no fixed delay. The value read is the game's DiceValue (boosted for a
        /// modifier action, the raw face for a zero-modifier one); OutlookLine ciphers
        /// it to the tutorial percentages via the game's own tier thresholds. Fires
        /// immediately for zero-modifier actions and the moment the boost lands for
        /// modifier actions; cancelled if the die is retracted (state leaves Slotted).
        /// The safety bail stops a stuck read from lingering.</summary>
        private void CheckDiceOutlook()
        {
            if (!_outlookPending) return;
            if (_diceSystemState != "Slotted") { _outlookPending = false; return; }
            int? v = GameQueries.SettledSlottedDieValue();
            bool timedOut = Time.unscaledTime - _outlookSince > Timing.OutlookSettleBail;
            if (v == null && !timedOut) return; // keep watching until it settles
            // Bail fallback (2026-07-22): when the settle detection misses (14/118
            // slots overnight spoke a bare start prompt), the value itself is final
            // by 0.6s — read it unconditionally rather than dropping the odds.
            if (v == null) v = GameQueries.SlottedDieValueUnsettled();
            _outlookPending = false;
            // Compose the odds and the game's own START ACTION prompt as ONE queued
            // string (owner ruling 2026-07-21): the game's focus announce for the slot
            // button is muted, so the mod re-emits its useful tail here, guaranteeing
            // the order "Die slotted." -> odds -> start action with nothing racing.
            string line = v != null ? GameVocab.OutlookLine(v.Value) : null;
            string start = GameQueries.SlottedActionButtonText();
            var sb = new System.Text.StringBuilder();
            if (!string.IsNullOrEmpty(line)) sb.Append(line);
            if (!string.IsNullOrEmpty(start))
            {
                if (sb.Length > 0) sb.Append(". ");
                sb.Append(start);
            }
            if (sb.Length > 0)
                SpeechService.Say(sb.ToString(), Priority.Queued, "dice");
        }

        private const string PickerPrompt =
            "Choose a die. Arrows to choose, Enter to slot, Backspace to cancel.";

        /// <summary>Announce dice-allocation transitions by tracking the Dice Gamepad
        /// System's full state set (bridge-decoded 2026-07-21: Off / Active /
        /// Reselector / Setup / Slotted — the old "no Slotted state" belief conflated
        /// this FSM with the Action Controller's Slot Item / Select Dice 2). The
        /// picker's cursor Buttons are selected into the EventSystem by the game, so
        /// per-die announcements ride the focus watcher (docs/ui-state-map.md 6b).
        ///
        /// Flow (owner ruling 2026-07-21): Active = picker prompt; Active->Slotted =
        /// "Die slotted." + odds (rerun on every placement, incl. a replacement after
        /// a retract); Slotted->Active (Backspace) = "Die removed." + picker prompt;
        /// Slotted->Off = the spend (silent, the outcome carries it) or a bare unslot
        /// ("Die removed."); Active/Reselector->Off = "Die picker closed.".</summary>
        private void CheckDiceAllocation()
        {
            // Read every frame from a held reference (2026-10-25): the 0.4 s poll made
            // "Die slotted." up to 0.4 s late and the odds up to 1.2 s. The lookup
            // (a full FSM-list scan) runs on poll frames only.
            if (_diceSystem == null && _pollFrame)
                _diceSystem = GameQueries.DiceSystemFsm();
            var system = _diceSystem;
            // A null/inactive object reads as a closed picker so the Slotted->Off
            // transition (spend or teardown) is still processed when the UI tears down.
            string state = system != null && system.isActiveAndEnabled ? system.ActiveStateName : "Off";
            if (string.IsNullOrEmpty(state)) state = "Off";

            if (!_diceStateKnown)
            {
                _diceSystemState = state;
                _diceStateKnown = true;
                return; // baseline, silent
            }
            if (state == _diceSystemState) return;
            string previous = _diceSystemState;
            _diceSystemState = state;

            switch (state)
            {
                case "Active":
                    _outlookPending = false; // a retract/reopen cancels a pending odds read
                    if (previous == "Slotted")
                    {
                        // Retract (corrected 2026-07-21): ResolveCancel now sends Reset
                        // to the action slot, which genuinely unslots the die — so
                        // "Die removed" is true again. Then rerun the picker prompt.
                        // Commit guard (2026-07-22, corpus Q5): the slot's cleanup Reset
                        // fires identically on retract and on a resolved action — the
                        // game's own discriminator is the action controller sitting in
                        // its Outcome family. A commit re-arm is not a removal.
                        if (!GameQueries.ActionResolving(_slottedActionRoot))
                            SpeechService.Say("Die removed. " + PickerPrompt,
                                Priority.Immediate, "dice");
                    }
                    else
                    {
                        // Picker opened (Off / Setup / Reselector -> Active).
                        _slottedGlobalAtOpen = GameQueries.SlottedDiceValueGlobal();
                        SpeechService.Say(PickerPrompt, Priority.Immediate, "dice");
                    }
                    break;

                case "Slotted":
                    // Die placed (Active -> Slotted): standard slot flow, spoken every
                    // time a die enters the slot including replacements after a retract.
                    _lastSlottedTime = Time.unscaledTime;
                    // Capture the action root NOW — after a commit or unslot the
                    // SlottedDiceGlobal clears and the teardown transitions can no
                    // longer tell whose action just resolved (corpus Q5 guard).
                    _slottedActionRoot = GameQueries.SlottedActionRoot();
                    // Owner ruling 2026-07-21: the slot-time strings ("Die slotted." +
                    // odds) are QUEUED, not Immediate — so they render in order and
                    // never stomp each other OR the game's own after-strings (START
                    // ACTION), which are left to work as they always have.
                    SpeechService.Say("Die slotted.", Priority.Queued, "dice");
                    // Odds ride the game's own settled die value, spoken the instant it
                    // settles (CheckDiceOutlook), also queued.
                    _outlookPending = true;
                    _outlookSince = Time.unscaledTime;
                    break;

                case "Off":
                    if (previous == "Slotted")
                    {
                        // Slotted -> Off is the spend (action resolved, UI torn down)
                        // or ForceUnslotDice (safety unslot, no outcome). The old 1.5s
                        // controller-left-Idle window missed real spends 26 times in
                        // one overnight session (the picker teardown lags the commit by
                        // the outcome animation, ~3-5s). The game's own discriminator
                        // (corpus Q5): the action controller in its Outcome family =
                        // spend — the outcome announce carries it; only a genuine bare
                        // unslot speaks its removal.
                        bool spent = GameQueries.ActionResolving(_slottedActionRoot)
                            || Time.unscaledTime - _lastControllerLeftIdle < Timing.DiceSpendWindow
                            || GameQueries.SlottedDiceValueGlobal() != _slottedGlobalAtOpen;
                        if (!spent)
                            SpeechService.Say("Die removed.", Priority.Immediate, "dice");
                    }
                    else if (previous == "Active" || previous == "Reselector")
                    {
                        // Picker cancelled without slotting.
                        SpeechService.Say("Die picker closed.", Priority.Immediate, "dice");
                    }
                    // previous == "Setup": startup teardown, silent.
                    break;

                // Reselector / Setup: transient, auto-FINISH to Off — no announce.
            }
        }

        private Transform _driveLog;
        private bool _driveLogWasOpen;
        private bool _driveLogBaselined;

        /// <summary>Announce the drive log window opening/closing. The window hides via its
        /// CanvasGroup (Animator-driven), so visibility is alpha, not active state. The first
        /// sighting only records state — the Animator can sit at alpha 1 for a moment during
        /// scene load, which spoke a false "open" (validation session 2026-07-18).</summary>
        private void CheckDriveLog()
        {
            if (_driveLog == null)
            {
                if (!_pollFrame) return;
                var win = GameObject.Find("Letterbox Canvas/Drive System/CS Drive Log");
                if (win == null) return;
                _driveLog = win.transform;
                _driveLogBaselined = false;
            }
            var group = _driveLog.GetComponent<CanvasGroup>();
            bool open = _driveLog.gameObject.activeInHierarchy && (group == null || group.alpha > 0.5f);
            if (!_driveLogBaselined)
            {
                _driveLogBaselined = true;
                _driveLogWasOpen = open;
                return;
            }
            if (open != _driveLogWasOpen)
            {
                if (open) JournalTable.OnWindowOpened();
                else JournalTable.OnWindowClosed();
                SpeechService.Say(open ? "Drive log open." : "Drive log closed.",
                    Priority.Immediate, "nav");
                _driveLogWasOpen = open;
            }
        }

        private Transform _characterWindow;
        private bool _charWindowWasOpen;
        private bool _charWindowBaselined;

        /// <summary>Announce the character window opening/closing (triage report 20 — the
        /// window opens fine; the mod just never said so). Same CanvasGroup-alpha pattern as
        /// the drive log. On open, also speak the upgrade tracker's rendered points line.</summary>
        private void CheckCharacterWindow()
        {
            if (_characterWindow == null)
            {
                if (!_pollFrame) return;
                var win = GameObject.Find("Letterbox Canvas/Character Window");
                if (win == null) return;
                _characterWindow = win.transform;
                _charWindowBaselined = false;
            }
            var group = _characterWindow.GetComponent<CanvasGroup>();
            bool open = _characterWindow.gameObject.activeInHierarchy && (group == null || group.alpha > 0.5f);
            if (!_charWindowBaselined)
            {
                _charWindowBaselined = true;
                _charWindowWasOpen = open;
                return;
            }
            if (open == _charWindowWasOpen) return;
            _charWindowWasOpen = open;
            if (!open)
            {
                SpeechService.Say("Character window closed.", Priority.Immediate, "nav");
                return;
            }
            // Points render as value + label in Points Av ("1" / "UPGRADE POINTS"); the
            // tracker also holds hidden threshold templates ("2 UPGRADE POINTS"), so read
            // the live container only (live-mapped 2026-07-18).
            var pointsAv = _characterWindow.Find("Upgrade Tracker/Top Line/Points UI/Points Av");
            string points = pointsAv != null ? Describe.JoinTexts(pointsAv.gameObject, 2) : null;
            SpeechService.Say("Character window open." + (points != null ? " " + points + "." : ""),
                Priority.Immediate, "nav");
        }

        /// <summary>Rendered outcome content of a resolved action card: the visible tier's
        /// effect lines (leading +/- glyphs transcoded to words, counts not interpreted),
        /// then the completion narrative from the Description element. Cloud node cards
        /// carry the same OUTCOMES template, so CloudOutcomes reuses this reader.</summary>
        internal static string DescribeOutcomeCard(Transform actionRoot)
        {
            if (actionRoot == null) return null;
            var parts = new List<string>();
            var outcomes = actionRoot.Find("OUTCOMES");
            if (outcomes != null)
            {
                foreach (var tmp in outcomes.GetComponentsInChildren<TMP_Text>(false))
                {
                    if (!tmp.gameObject.name.StartsWith("Effect")) continue;
                    if (Util.AlphaUpTo(tmp.transform, actionRoot) < 0.5f) continue;
                    string txt = tmp.text?.Trim();
                    if (string.IsNullOrEmpty(txt)) continue;
                    txt = ComposeEffect(txt);
                    if (!parts.Contains(txt)) parts.Add(txt);
                }
            }
            string narrative = Describe.TextUnder(actionRoot, "Description");
            if (narrative != null) parts.Add(narrative);
            return parts.Count > 0 ? string.Join(". ", parts) : null;
        }

        /// <summary>Clock names spoken as state by the last ComposeEffect batch —
        /// LocationTable.AfterOutcome's callout skips these (F7 dedupe: the effect
        /// line already said "NAME now x of y").</summary>
        internal static readonly HashSet<string> RecentEffectClocks = new HashSet<string>();
        internal static float RecentEffectClocksAt = -10f;

        /// <summary>Every effect BODY the last compose spoke (upper) — ResourceWatch's
        /// stand-down check: an effect line that already voiced a resource means the
        /// ambient lane stays quiet for it (A4 architecture, owner ruling).</summary>
        internal static readonly HashSet<string> RecentEffectBodies = new HashSet<string>();
        internal static float RecentEffectBodiesAt = -10f;

        /// <summary>F7 (owner ruling, fresh run 2026-07-20): effect lines compose
        /// STATE, not raw markup. Clocks → "NAME now x of y" (post-tick dial).
        /// ENERGY/CONDITION → delta + present value (condition adds the rendered
        /// band word). Value resources keep the rendered amount ("plus 15 CRYO").
        /// Unrecognized multi-sign effects speak "plus N, X" (provisional).</summary>
        private static string ComposeEffect(string txt)
        {
            string s = txt.Trim();
            int i = Util.GlyphRun(s, out int gain, out int loss);
            int delta = gain + loss;
            if (delta == 0) return s;
            // A2: effect targets are names — quote-normalize so the fully-quoted
            // authored effect entry matches the half-quoted authored render name.
            string body = Describe.TrimQuotes(s.Substring(i));
            if (body.Length == 0) return s;
            string sign = gain >= loss ? "plus" : "minus";
            string upper = body.ToUpperInvariant();

            if (Time.unscaledTime - RecentEffectBodiesAt > Timing.EffectEchoWindow)
                RecentEffectBodies.Clear();
            RecentEffectBodies.Add(upper);
            RecentEffectBodiesAt = Time.unscaledTime;

            // Segments-first (owner ruling): sign counts ARE box counts (-- ENERGY
            // = 2 boxes = 40 pts; - CONDITION = 1 box = 5 pts, live-confirmed);
            // "now" values speak as filled boxes, condition with its band word.
            if (upper == "ENERGY")
            {
                int? now = LuaStore.Energy();
                return "ENERGY " + sign + " " + delta
                    + (now != null ? ", now " + GameQueries.EnergyBoxes(now.Value) : "");
            }
            if (upper == "CONDITION")
            {
                int? now = LuaStore.Condition();
                string band = ConditionBand();
                return "CONDITION " + sign + " " + delta
                    + (now != null ? ", now " + GameQueries.ConditionBoxes(now.Value) : "")
                    + (band != null ? ", " + band : "");
            }

            string clock = ClockNow(body);
            if (clock != null)
            {
                if (Time.unscaledTime - RecentEffectClocksAt > Timing.EffectEchoWindow)
                    RecentEffectClocks.Clear();
                RecentEffectClocks.Add(upper);
                RecentEffectClocksAt = Time.unscaledTime;
                return clock;
            }

            if (delta == 1) return sign + " " + body;
            return sign + " " + delta + ", " + body;
        }

        /// <summary>Post-tick dial for the location clock whose rendered name matches
        /// the effect target, or null when no such clock renders here.</summary>
        private static string ClockNow(string name)
        {
            foreach (var clock in GameQueries.GetClockPanels())
            {
                string cname = Describe.TrimQuotes(Describe.TextUnder(clock, "Clock Name"));
                if (cname == null
                    || !cname.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                    continue;
                string now = GameQueries.ClockProgress(clock);
                if (now == null) return null;
                return cname + " now " + now;
            }
            return null;
        }

        private static string ConditionBand()
        {
            var fsm = GameQueries.FindFsm("Condition System", "Energy UI");
            string band = fsm != null
                ? fsm.FsmVariables.GetFsmString("Breaking")?.Value?.Trim()
                : null;
            return string.IsNullOrEmpty(band) ? null : band.ToLowerInvariant();
        }

        /// <summary>The last spoken tutorial announce, for R-reread in Tutorial mode
        /// (owner ruling 2026-07-20 — tutorials reread the way dialogue does).</summary>
        internal static string LastTutorialLine;

        /// <summary>True when the panel had readable text and speech was queued —
        /// callers must not mark a panel handled on a false return (BL-3).</summary>
        private static bool AnnouncePanelTexts(Transform panel, string source, string prefix, string suffix)
        {
            // F13 (run 3, CLOUD tutorial): activation ≠ presentation — cycle-end
            // triggers activate the panel while the game presents it only later.
            // Announce only when it actually renders; a false return leaves the
            // panel unmarked and the poll retries (BL-3 machinery unchanged).
            if (Util.AlphaUpTo(panel) < 0.5f) return false;

            var parts = new List<string>();
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt) || txt.Length <= 1) continue;
                // A prompt's own two-letter glyph label ("RB", "LS") is read through
                // the gap fill below, never as a line of its own.
                if (txt.Length <= 2 && GlyphLabelWords.ContainsKey(txt)) continue;
                // F4: glyph gaps ("select it with [] and []") filled with the
                // rendered prompt's controller name; die-odds texts prefixed with
                // the die value(s) their Die group renders.
                txt = FillPromptGaps(tmp, txt);
                txt = SubstituteGamepadPhrases(txt);
                string die = DiePrefix(tmp.transform, panel);
                if (die != null) txt = die + txt;
                if (!parts.Contains(txt)) parts.Add(txt);
            }
            if (parts.Count == 0) return false;
            string line = prefix + string.Join(". ", parts) + suffix;
            if (source == "tutorial") LastTutorialLine = line;
            SpeechService.Say(line, Priority.Queued, source);
            return true;
        }

        // ---------- F4: tutorial glyph + die-odds transcodes ----------

        /// <summary>B2 (owner ruling 2026-07-21): prompt glyphs speak the MOD's keys,
        /// not the gamepad vocabulary — the game believes a controller is present
        /// because the mod itself holds the flag (load-bearing: the dice picker and
        /// the whole selection machinery are gamepad-gated, B2 desk check), so its
        /// prompts can never be right for this player. Semantic mapping per the
        /// README key table; the F4 calibration logging stays for verification.</summary>
        private static readonly Dictionary<string, string> PromptWords =
            new Dictionary<string, string>
            {
                { "A Prompt", "Enter" },
                { "B Prompt", "Backspace" },
                { "X Prompt", "the O key" },
                { "Y Prompt", "the O key" },
                { "DPAD", "the arrow keys" },
                { "Stick", "the arrow keys" },
            };

        /// <summary>B2 second layer: gamepad vocabulary the game AUTHORS as words
        /// (specimens: "the A button and the D-pad", "L1" drives, "R1" character,
        /// "the X button" cloud). Replaced with the mod's keys at announce time.
        /// Longest-first order so button phrases beat bare shoulder tokens.</summary>
        private static readonly string[,] GamepadPhrases =
        {
            { "the A button", "Enter" },
            { "the B button", "Backspace" },
            { "the X button", "the O key" },
            { "the Y button", "the O key" },
            { "the D-pad", "the arrow keys" },
            { "the left stick", "the arrow keys" },
            { "the right stick", "the arrow keys" },
            { "the stick", "the arrow keys" },
            { "L1", "the J key" },
            { "R1", "the U key" },
        };

        private static string SubstituteGamepadPhrases(string txt)
        {
            for (int i = 0; i < GamepadPhrases.GetLength(0); i++)
            {
                string from = GamepadPhrases[i, 0];
                if (txt.IndexOf(from, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                txt = System.Text.RegularExpressions.Regex.Replace(txt,
                    System.Text.RegularExpressions.Regex.Escape(from),
                    GamepadPhrases[i, 1],
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            }
            return txt;
        }

        /// <summary>A prompt's own glyph label (the "A", "RB", "LS" drawn inside it),
        /// for prompts whose object name says nothing ("Switch/Gamepad Prompt (3)"
        /// draws "RB"; live 2026-10-25). Same B2 rule: the mod's key, not the pad's.
        /// LS/LT/RT scroll the station, which the table does with the arrows; RS
        /// rotates the camera, the game's own D key.</summary>
        private static readonly Dictionary<string, string> GlyphLabelWords =
            new Dictionary<string, string>
            {
                { "A", "Enter" },
                { "B", "Backspace" },
                { "X", "the O key" },
                { "Y", "the O key" },
                { "LB", "the J key" },
                { "RB", "the U key" },
                { "L1", "the J key" },
                { "R1", "the U key" },
                { "LS", "the arrow keys" },
                { "LT", "the arrow keys" },
                { "RT", "the arrow keys" },
                { "L2", "the arrow keys" },
                { "R2", "the arrow keys" },
                { "RS", "the D key" },
            };

        private static string PromptWordFor(Transform prompt)
        {
            var label = prompt.GetComponentInChildren<TMP_Text>(false);
            string glyph = label != null ? label.text?.Trim() : null;
            if (!string.IsNullOrEmpty(glyph) && GlyphLabelWords.TryGetValue(glyph, out string word))
                return word;
            string n = prompt.name;
            foreach (var kv in PromptWords)
                if (n.StartsWith(kv.Key) || n.Contains(kv.Key)) return kv.Value;
            return null;
        }

        /// <summary>Fills a prompt-glyph text's whitespace gaps ("select it with
        /// [gap] and [gap]") with its rendered prompt children's key names, in the
        /// order they sit on screen — line by line, left to right. Child order is not
        /// screen order ("select it with [DPAD] and [A]" has A as the first child,
        /// which made it "with Enter and the arrow keys"; 2026-10-25). Mismatched
        /// counts append instead (logged).</summary>
        private static string FillPromptGaps(TMP_Text tmp, string txt)
        {
            var found = new List<(float X, float Y, string Word)>();
            foreach (Transform child in tmp.transform)
            {
                if (!child.gameObject.activeSelf) continue;
                string n = child.name;
                if (n.IndexOf(GameVocab.Warts.PositionerFragment,
                        System.StringComparison.OrdinalIgnoreCase) >= 0
                    || n.IndexOf(GameVocab.Warts.PositionerFragmentCorrect,
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue; // per-locale positioner copies ("A Prompt postioner FR")
                // A prompt draws something; positioners are bare RectTransforms.
                if (child.GetComponentInChildren<UnityEngine.UI.Graphic>(false) == null)
                    continue;
                string word = PromptWordFor(child);
                if (word != null)
                    found.Add((child.localPosition.x, child.localPosition.y, word));
            }
            if (found.Count == 0) return txt;
            found.Sort((a, b) => Mathf.Abs(a.Y - b.Y) > 10f ? b.Y.CompareTo(a.Y) : a.X.CompareTo(b.X));
            var prompts = new List<string>();
            foreach (var f in found) prompts.Add(f.Word);
            var segments = System.Text.RegularExpressions.Regex.Split(txt, "\\s{3,}");
            if (segments.Length == prompts.Count + 1)
            {
                var sb = new System.Text.StringBuilder(segments[0]);
                for (int i = 0; i < prompts.Count; i++)
                {
                    string next = segments[i + 1].TrimStart();
                    sb.Append(' ').Append(prompts[i]);
                    // No space before trailing punctuation ("with Enter.", not "Enter .").
                    if (next.Length > 0 && !char.IsPunctuation(next[0])) sb.Append(' ');
                    sb.Append(next);
                }
                return CollapseRepeatedKeys(sb.ToString(), prompts);
            }
            Plugin.Log.LogInfo("[Tutorial] prompt gap mismatch: " + prompts.Count
                + " prompt(s), " + (segments.Length - 1) + " gap(s) — appending.");
            return txt + ". Shown with " + string.Join(" and ", prompts) + ".";
        }

        /// <summary>Pad alternatives that map to one key read once: "scroll with LS or
        /// LT / RT" is "with the arrow keys", not "the arrow keys or the arrow keys /
        /// the arrow keys".</summary>
        private static string CollapseRepeatedKeys(string txt, List<string> words)
        {
            foreach (var w in new HashSet<string>(words))
            {
                string e = System.Text.RegularExpressions.Regex.Escape(w);
                txt = System.Text.RegularExpressions.Regex.Replace(txt,
                    e + "(?:\\s*(?:or|/)\\s*" + e + ")+", w);
            }
            return txt;
        }

        /// <summary>"Die N. " / "Dice N and M. " prefix for odds texts inside a
        /// tutorial Die group (fresh-run F4: the die faces are graphics, so the
        /// value→odds mapping was unheard). Values parse from the group's Image
        /// sprite names — Marker images first, any image as fallback; every sprite
        /// name is logged for calibration since this ships built blind (game was
        /// closed). No parse → no prefix, odds read as before.</summary>
        private static string DiePrefix(Transform tmp, Transform panel)
        {
            Transform dieRoot = null;
            for (var cur = tmp; cur != null && cur != panel; cur = cur.parent)
                if (cur.name.TrimEnd() == "Die") dieRoot = cur; // topmost Die wins
            if (dieRoot == null) return null;

            var fromMarkers = new SortedSet<int>();
            var fromAny = new SortedSet<int>();
            foreach (var img in dieRoot.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                var sp = img.sprite;
                if (sp == null) continue;
                Plugin.Log.LogInfo("[Tutorial] die sprite (" + img.gameObject.name + "): " + sp.name);
                var m = System.Text.RegularExpressions.Regex.Match(sp.name, "[1-6]");
                if (!m.Success) continue;
                int v = int.Parse(m.Value);
                fromAny.Add(v);
                if (img.gameObject.name.StartsWith("Marker") || img.gameObject.name.StartsWith("FILL"))
                    fromMarkers.Add(v);
            }
            var values = fromMarkers.Count > 0 ? fromMarkers : fromAny;
            if (values.Count == 0) return null;
            if (values.Count == 1)
            {
                foreach (int v in values) return "Die " + v + ". ";
            }
            return "Dice " + string.Join(" and ", values) + ". ";
        }
    }
}
