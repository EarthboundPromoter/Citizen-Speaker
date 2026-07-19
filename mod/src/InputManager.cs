using CSAccess.Game;
using CSAccess.Modality;
using CSAccess.Patches;
using CSAccess.Speech;
using CSAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess
{
    /// <summary>Keyboard commands, dispatched through the W2 modality layer: every key
    /// is gated by KeyScope for the current mode; game-touching keys resolve to designed
    /// effects per mode (input-contract.md); refusals answer mode-aware. Polled from
    /// Plugin.Update via legacy Input.</summary>
    internal class InputManager
    {
        private readonly MonoBehaviour _host;

        public InputManager(MonoBehaviour host)
        {
            _host = host;
        }

        public void Tick()
        {
            // --- Last-input-wins mode switching: a mouse click claims mouse mode; any
            //     keyboard key re-asserts gamepad mode (which the mod's navigation needs). ---
            if (Plugin.ForceGamepadUI.Value)
            {
                if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2))
                    GameQueries.EnsureMouseMode();
                else if (Input.anyKeyDown)
                    GameQueries.EnsureGamepadMode();
            }

            // Any player input releases the post-scene-load focus silence.
            if (Input.anyKeyDown || Input.GetMouseButtonDown(0))
                FocusPatch.MarkSettled();

            bool shift = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            var mode = ModeModel.Current();

            // Node census: announces additions/removals at the first full-control
            // station moment per cycle (focus-model row 3; cheap no-op otherwise).
            Game.NodeCensus.Tick(mode);

            // --- Speech and queries (near-universal; KeyScope gates the title scene) ---
            if (Input.GetKeyDown(KeyCode.Z) && Allowed(mode, ModKey.Respeak))
            { SpeechService.RepeatLast(); return; }

            if (Input.GetKeyDown(KeyCode.F1) && Allowed(mode, ModKey.Help))
            { SpeechService.Say(KeyScope.HelpFor(mode), Priority.Immediate, "help"); return; }

            // Keymap reorder (owner ruling 2026-07-19): C = meter/vitals reads,
            // V = dice. D is the game's native rotate key and passes through
            // untouched (same ruling as S/camera-scroll).
            if (Input.GetKeyDown(KeyCode.C) && Query(mode, ModKey.Vitals))
            { SpeechService.Say(GameQueries.DescribeVitals(), Priority.Immediate, "query"); return; }

            if (Input.GetKeyDown(KeyCode.V) && Query(mode, ModKey.Dice))
            { SpeechService.Say(GameQueries.DescribeDiceBrief(), Priority.Immediate, "query"); return; }

            if (Input.GetKeyDown(KeyCode.K) && Query(mode, ModKey.Clocks))
            { SpeechService.Say(GameQueries.DescribeClocks(), Priority.Immediate, "query"); return; }

            if (Input.GetKeyDown(KeyCode.L) && Query(mode, ModKey.WhereAmI))
            { SpeechService.Say(ModeModel.WhereAmI(), Priority.Immediate, "query"); return; }

            if (Input.GetKeyDown(KeyCode.R) && !shift && Allowed(mode, ModKey.RereadDialogue))
            { RereadDialogue(); return; }

            // --- Scripted input pause: swallow game-facing keys; speech keys above still
            //     work. Exception: a tutorial continue the game itself selected. ---
            if (GameQueries.InputPaused() && !TutorialContinueFocused()) return;

            if (Input.GetKeyDown(KeyCode.Space) && Allowed(mode, ModKey.Describe))
            {
                var current = Navigator.Current();
                SpeechService.Say(current != null
                        ? Describe.Element(current, detailed: true)
                        : "Nothing focused.",
                    Priority.Immediate, "focus");
                return;
            }

            // --- Review cursors (per-context shapes; gated by their own IsActive checks,
            //     which remain the authority — the mode gate adds scoping consistency) ---
            if (Allowed(mode, ModKey.ReviewArrows))
            {
                if (OptionsReview.IsActive())
                {
                    if (Input.GetKeyDown(KeyCode.DownArrow)) { OptionsReview.Review(1); return; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) { OptionsReview.Review(-1); return; }
                    if (Input.GetKeyDown(KeyCode.RightArrow)) { OptionsReview.Adjust(1); return; }
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) { OptionsReview.Adjust(-1); return; }
                    if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        && OptionsReview.Activate())
                        return;
                }
                if (TutorialReview.IsActive())
                {
                    if (Input.GetKeyDown(KeyCode.DownArrow)) { TutorialReview.Review(1); return; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) { TutorialReview.Review(-1); return; }
                }
                if (CharacterWindowReview.IsActive())
                {
                    if (Input.GetKeyDown(KeyCode.DownArrow)) { CharacterWindowReview.Review(1); return; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) { CharacterWindowReview.Review(-1); return; }
                    if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                        && CharacterWindowReview.Activate())
                        return;
                }
                if (CharacterSelect.IsActive())
                {
                    if (Input.GetKeyDown(KeyCode.DownArrow)) { CharacterSelect.Review(1); return; }
                    if (Input.GetKeyDown(KeyCode.UpArrow)) { CharacterSelect.Review(-1); return; }
                    if (Input.GetKeyDown(KeyCode.LeftArrow)) { CharacterSelect.ChangeClass(right: false); return; }
                    if (Input.GetKeyDown(KeyCode.RightArrow)) { CharacterSelect.ChangeClass(right: true); return; }
                }
            }

            // --- Drive log: slash = tab swap (owner ruling) — clicks the OTHER
            //     native tab button (Active/Completed), announced by rendered label. ---
            if (mode == Mode.DriveLog && Input.GetKeyDown(KeyCode.Slash))
            {
                bool? showingActive = WindowState.DriveLogShowingActive;
                string target = showingActive == false ? "Active Button" : "Completed Button";
                var tab = GameObject.Find(
                    "Letterbox Canvas/Drive System/CS Drive Log/Quest Log Window Main Panel/Vertical Group/Main Button Horizontal Group/"
                    + target);
                var tabBtn = tab != null ? tab.GetComponent<Button>() : null;
                if (tabBtn != null && tabBtn.IsInteractable())
                {
                    var tmp = tab.GetComponentInChildren<TMPro.TMP_Text>(true);
                    string label = tmp != null ? tmp.text?.Trim() : null;
                    Navigator.Click(tab);
                    SpeechService.Say(string.IsNullOrEmpty(label)
                        ? (target == "Active Button" ? "Active." : "Completed.")
                        : label + ".", Priority.Immediate, "nav");
                }
                else
                    SpeechService.Say("Tab not available.", Priority.Immediate, "nav");
                return;
            }

            // --- Inventory: Up/Down = the designed panel Swap (the Swapper's own
            //     vertical-axis idiom, focus-model row 11); Left/Right stay native
            //     moves between Item Cursors, CONFINED to the cursor family — an
            //     edge move would escape it and trip the game's auto-close watchdog
            //     (session-5 live bug: Left closed the strip). Dead-end = bare repeat. ---
            if (mode == Mode.Inventory)
            {
                if (Input.GetKeyDown(KeyCode.LeftArrow) || Input.GetKeyDown(KeyCode.RightArrow))
                {
                    bool right = Input.GetKeyDown(KeyCode.RightArrow);
                    var cur = Navigator.Current();
                    var curSel = cur != null ? cur.GetComponent<Selectable>() : null;
                    if (curSel != null && cur.name == "Item Cursor")
                    {
                        var target = right ? curSel.FindSelectableOnRight() : curSel.FindSelectableOnLeft();
                        if (target == null || target.gameObject.name != "Item Cursor")
                        {
                            SpeechService.Say(Describe.Element(cur, detailed: false) ?? "Item Cursor",
                                Priority.Immediate, "focus");
                            return;
                        }
                    }
                    Navigator.Move(right
                        ? UnityEngine.EventSystems.MoveDirection.Right
                        : UnityEngine.EventSystems.MoveDirection.Left);
                    return;
                }
                if (Input.GetKeyDown(KeyCode.UpArrow) || Input.GetKeyDown(KeyCode.DownArrow))
                {
                    var strip = GameQueries.FindFsm("Inventory", "Bottom UI");
                    if (strip != null)
                    {
                        // Speak the rendered label of the panel we're switching TO.
                        // The strip FSM's OWN Data? variable, not the same-named global
                        // (session-5: the global never updates — always announced DATA).
                        var dataVar = strip.FsmVariables.GetFsmBool("Data?");
                        bool toData = dataVar == null || !dataVar.Value;
                        var panelBtn = GameObject.Find("Letterbox Canvas/Bottom UI/Inventory/" +
                                                       (toData ? "DATA Button" : "ITEM Button"));
                        var tmp = panelBtn != null ? panelBtn.GetComponentInChildren<TMPro.TMP_Text>(true) : null;
                        string label = tmp != null ? tmp.text?.Trim() : null;
                        strip.SendEvent("Swap");
                        SpeechService.Say(string.IsNullOrEmpty(label)
                            ? (toData ? "Data." : "Items.") : label + ".", Priority.Immediate, "nav");
                    }
                    return;
                }
            }

            // --- Response menu: vertical remap over the horizontal graph + number picks ---
            if (mode == Mode.ResponseMenu)
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Right); return; }
                if (Input.GetKeyDown(KeyCode.UpArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Left); return; }
                for (int i = 0; i < 9 && i < DialogueState.CurrentResponses.Count; i++)
                {
                    if (Input.GetKeyDown(KeyCode.Alpha1 + i)) { PickResponse(i); return; }
                }
            }

            // --- Tutorial continue (mode-scoped; Enter fires it through native submit) ---
            if (Input.GetKeyDown(KeyCode.T))
            {
                if (!Allowed(mode, ModKey.TutorialContinue)) { Refuse(mode, "Tutorial continue"); return; }
                var button = GameObject.Find("Letterbox Canvas/Tutorial System/Button");
                if (button != null && button.activeInHierarchy) Navigator.Select(button);
                else SpeechService.Say("No tutorial open.", Priority.Immediate, "nav");
                return;
            }

            // --- Surface toggles (designed actions; refusals answer mode-aware) ---
            if (Input.GetKeyDown(KeyCode.I))
            {
                if (!Allowed(mode, ModKey.InventoryToggle)) { Refuse(mode, "Inventory"); return; }
                // The strip FSM's own designed toggle (focus-model row 11): its states
                // watch the Inventory Toggle action and fire Activate (closed) or
                // Deactivate (open) — we send the event the action would produce.
                // Replaces the off-contract ITEM/DATA button clicks.
                var strip = GameQueries.FindFsm("Inventory", "Bottom UI");
                if (strip != null)
                    strip.SendEvent(WindowState.InventoryOpen ? "Deactivate" : "Activate");
                else
                    SpeechService.Say("Inventory not available.", Priority.Immediate, "nav");
                return;
            }
            if (Input.GetKeyDown(KeyCode.U))
            {
                if (!Allowed(mode, ModKey.CharacterToggle)) { Refuse(mode, "Character window"); return; }
                ClickFirstActive("Character window",
                    "Letterbox Canvas/Character UI/Character UI Button");
                return;
            }
            if (Input.GetKeyDown(KeyCode.J))
            {
                if (!Allowed(mode, ModKey.DriveLogToggle)) { Refuse(mode, "Drive log"); return; }
                // The button FSM's designed toggle event works BOTH directions (Idle
                // -Open-> opens, Open -Open-> closes); a raw click never reaches it in
                // the open state (session-5: J couldn't close the window it opened).
                var driveBtn = GameQueries.FindFsm("Drive Log Button");
                if (driveBtn != null) driveBtn.SendEvent("Open");
                else SpeechService.Say("Drive log not available.", Priority.Immediate, "nav");
                return;
            }
            // S and D are NOT mod binds (owner ruling 2026-07-19): native camera
            // scroll and rotate keys — they pass through untouched. Scan lives on O
            // (U/I/O = the state-changing panel toggles, owner keymap reorder).
            if (Input.GetKeyDown(KeyCode.O))
            {
                if (!Allowed(mode, ModKey.ScanToggle)) { Refuse(mode, "Scan"); return; }
                ClickFirstActive("Scan",
                    "Letterbox Canvas/Top UI/Scan Button");
                return;
            }
            if (Input.GetKeyDown(KeyCode.R) && shift)
            {
                if (!Allowed(mode, ModKey.Reroll)) { Refuse(mode, "Reroll"); return; }
                ClickFirstActive("Reroll dice",
                    "Letterbox Canvas/Top UI/Dice UI/REROLL DICE");
                return;
            }

            // --- Backspace: the designed cancel, resolved per mode ---
            if (Input.GetKeyDown(KeyCode.Backspace))
            {
                if (!Allowed(mode, ModKey.Cancel)) { Refuse(mode, "Back"); return; }
                ResolveCancel(mode);
                return;
            }

            // --- Native navigation and activation ---
            if (Allowed(mode, ModKey.Navigate))
            {
                if (Input.GetKeyDown(KeyCode.DownArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Down); return; }
                if (Input.GetKeyDown(KeyCode.UpArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Up); return; }
                if (Input.GetKeyDown(KeyCode.LeftArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Left); return; }
                if (Input.GetKeyDown(KeyCode.RightArrow)) { Navigator.Move(UnityEngine.EventSystems.MoveDirection.Right); return; }
            }
            if ((Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
                && Allowed(mode, ModKey.Activate))
            {
                // Empty-Enter = the universal mode-aware re-anchor (W3, H commitment 4):
                // put focus back where the game intends it for this mode — extends the
                // station Confirm-backstop mirror to every mode with a designed anchor.
                if (Navigator.Current() == null && FocusModel.ReAnchor(mode))
                    return;
                Navigator.ActivateCurrent();
                return;
            }
        }

        // ---------- Helpers ----------

        private static bool Allowed(Mode mode, ModKey key) => KeyScope.Allows(mode, key);

        /// <summary>Queries speak a mode-aware refusal instead of stale data when scoped out.</summary>
        private static bool Query(Mode mode, ModKey key)
        {
            if (KeyScope.Allows(mode, key)) return true;
            SpeechService.Say(ModeModel.Name(mode) + ".", Priority.Immediate, "nav");
            return false;
        }

        private static void Refuse(Mode mode, string label)
        {
            SpeechService.Say(ModeModel.Name(mode) + ". " + label + " not available here.",
                Priority.Immediate, "nav");
        }

        /// <summary>The designed cancel per mode. Each branch sends a corpus-verified
        /// designed event or clicks the designed control; nothing here invents a path.</summary>
        private static void ResolveCancel(Mode mode)
        {
            switch (mode)
            {
                case Mode.DiceAllocation:
                    // Designed Back: retracts a resting die (Slotted -> Active) or
                    // cancels the picker (Active -> teardown) — the FSM resolves depth.
                    GameQueries.DiceSystemFsm()?.SendEvent("Back");
                    return;

                case Mode.CharacterWindow:
                    // The FSM's designed toggle event closes it (Open -> Close, corpus).
                    GameQueries.FindFsm("Character UI Button")?.SendEvent("Open");
                    return;

                case Mode.DriveLog:
                    // Same button template as the character window — same designed toggle.
                    GameQueries.FindFsm("Drive Log Button")?.SendEvent("Open");
                    return;

                case Mode.Inventory:
                    // The item cursor's designed Back effect is its own Deactivate (brief F).
                    var cursor = Navigator.Current();
                    var cursorFsm = cursor != null ? cursor.GetComponent<PlayMakerFSM>() : null;
                    if (cursorFsm != null) { cursorFsm.SendEvent("Deactivate"); return; }
                    break; // fall through to generic leave if the cursor wasn't found

                case Mode.Pause:
                    // Emulate the designed Pause Back mapping: the PAUSE FSM maps that
                    // action per state — Pause/Pause 3 -> Unpause, Sure?/OPTIONS -> Back (F).
                    var pause = GameQueries.FindFsm("PAUSE");
                    string state = pause != null ? pause.ActiveStateName : null;
                    if (state != null && state.StartsWith("Pause")) { pause.SendEvent("Unpause"); return; }
                    if (state != null && (state.StartsWith("Sure?") || state.StartsWith("OPTIONS")))
                    { pause.SendEvent("Back"); return; }
                    Plugin.Log.LogInfo("[Input] Pause cancel: unmapped PAUSE state '" + state + "' — no event sent.");
                    return;
            }

            // Station/action view: the Leave Button is the designed leave.
            ClickFirstActive("Leave or back",
                "Letterbox Canvas/Top UI/Leave Button",
                "Back Button", "Close Button", "BACK");
        }

        private void RereadDialogue()
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
        }

        /// <summary>True when the game has put selection on the Tutorial System's continue
        /// button — the one case where input is wanted while the Input Pauser is PAUSED.</summary>
        private static bool TutorialContinueFocused()
        {
            var selected = Navigator.Current();
            return selected != null && selected.activeInHierarchy &&
                   selected.name == "Button" &&
                   Describe.HasAncestor(selected, "Tutorial System");
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
                if (button != null)
                {
                    if (button.IsInteractable())
                    {
                        Navigator.Click(button.gameObject);
                        return;
                    }
                    // A disabled button is the game gating this control — report it,
                    // never bypass it through the FSM.
                    SpeechService.Say(label + " is disabled.", Priority.Immediate, "nav");
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
    }
}
