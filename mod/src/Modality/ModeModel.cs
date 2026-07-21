using CSAccess.Game;
using CSAccess.Patches;
using CSAccess.UI;
using HutongGames.PlayMaker;
using UnityEngine;

namespace CSAccess.Modality
{
    internal enum Mode
    {
        Title,
        CharacterSelect,
        Pause,
        CycleTransition,
        Autoplay,
        Tutorial,
        ResponseMenu,
        Dialogue,
        DiceAllocation,
        CharacterWindow,
        DriveLog,
        Inventory,
        Cloud,
        ActionView,
        Station,
    }

    /// <summary>
    /// The mode authority (W2, input-model.md). Pure derivation from signals that are
    /// each corpus-verified or live-proven — this class answers "what mode are we in",
    /// it announces nothing (shipped watchers keep their validated announcements).
    ///
    /// Precedence is documented top-down in Current(): overlays and modals first,
    /// ambient camera modes last. Character window before drive log: the corpus found
    /// no mutual exclusion between those two (brief F SURPRISES, ledgered open
    /// question); if both read open we report the character window and log it.
    /// </summary>
    internal static class ModeModel
    {
        private static float _bothWindowsLogged = -60f;

        public static Mode Current()
        {
            // --- Title scene ---
            string scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            if (scene.Contains("MAIN TITLE"))
                return CharacterSelect.IsActive() ? Mode.CharacterSelect : Mode.Title;

            // --- Pause outranks everything ---
            if (PauseOpen()) return Mode.Pause;

            // --- AFFORDANCE PRECEDENCE (W3, brief H): the mode that owns the keys is
            //     the mode whose input affordance the game is rendering or holding.
            //     Interactive affordances outrank ambient wrappers, whatever flag the
            //     wrapper flies — incidents 5 (dialogue vs Scenes Active?), 6 (autoplay)
            //     and 9 (wake dialogue inside the cycle transition) were all this one
            //     rule missing. The wrappers only own keys between affordances. ---
            if (TutorialPanelActive()) return Mode.Tutorial;
            if (DialogueState.MenuOpen) return Mode.ResponseMenu;
            if (ConversationActive()) return Mode.Dialogue;
            if (GameQueries.DiceAllocationActive() || DiceSlottedResting()) return Mode.DiceAllocation;

            // --- Windows: event-driven truth (WindowState; incident 7 killed the
            //     alpha polls). One-directional exclusion only; see class doc. ---
            bool charWin = WindowState.CharacterWindowOpen;
            bool driveLog = WindowState.DriveLogOpen;
            if (charWin && driveLog && Time.unscaledTime - _bothWindowsLogged > 30f)
            {
                _bothWindowsLogged = Time.unscaledTime;
                Plugin.Log.LogInfo("[Mode] Character window and drive log both read open (ledgered ambiguity).");
            }
            if (charWin) return Mode.CharacterWindow;
            if (driveLog) return Mode.DriveLog;
            if (WindowState.InventoryOpen || InventoryCursorFocused()) return Mode.Inventory;

            // --- Ambient wrappers (listening modes) ---
            if (GameQueries.CycleTransitionActive()) return Mode.CycleTransition;
            if (AutoplayActive()) return Mode.Autoplay;

            // --- Camera-level modes ---
            if (CloudActive()) return Mode.Cloud;
            if (GlobalBool("Action View?")) return Mode.ActionView;
            return Mode.Station;
        }

        /// <summary>
        /// Camera-level surface memory (D3, owner ruling 2026-07-20): overlays —
        /// windows, the dice picker, dialogue, tutorials, pause, response menus,
        /// autoplay — SUSPEND a surface without leaving it, so the permanent tables
        /// keep their position across them and only rebuild/announce on a genuine
        /// surface change (Station / ActionView / Cloud / cycle transition / title).
        /// </summary>
        private static Mode _surface = Mode.Title;

        public static Mode Surface()
        {
            var m = Current();
            switch (m)
            {
                case Mode.Station:
                case Mode.ActionView:
                case Mode.Cloud:
                case Mode.CycleTransition:
                case Mode.Title:
                case Mode.CharacterSelect:
                    _surface = m;
                    break;
            }
            return _surface;
        }

        /// <summary>Spoken name for refusals and the L query. Wording provisional.</summary>
        public static string Name(Mode mode) => mode switch
        {
            Mode.Title => "Main menu",
            Mode.CharacterSelect => "Character select",
            Mode.Pause => "Pause menu",
            Mode.CycleTransition => "Cycle ending",
            Mode.Autoplay => "Scene playing",
            Mode.Tutorial => "Tutorial",
            Mode.ResponseMenu => "Choosing a response",
            Mode.Dialogue => "In conversation",
            Mode.DiceAllocation => "Choosing a die",
            Mode.CharacterWindow => "Character window",
            Mode.DriveLog => "Drive log",
            Mode.Inventory => "Inventory",
            Mode.Cloud => "In the cloud",
            Mode.ActionView => "At a location",
            Mode.Station => "Station map",
            _ => "Unknown",
        };

        /// <summary>The L query: mode plus whatever verified context enriches it.</summary>
        public static string WhereAmI()
        {
            var mode = Current();
            var sb = new System.Text.StringBuilder(Name(mode));
            if (mode == Mode.Station || mode == Mode.ActionView || mode == Mode.Cloud)
            {
                string zone = CurrentZone();
                if (zone != null) sb.Append(", ").Append(zone);
            }
            var selected = Navigator.Current();
            if (selected != null && (mode == Mode.Station || mode == Mode.ActionView))
            {
                string label = Describe.Element(selected, detailed: false);
                if (!string.IsNullOrEmpty(label)) sb.Append(". ").Append(label);
            }
            return sb.ToString() + ".";
        }

        // ---------- Signals (each cited) ----------

        /// <summary>Pause Canvas active — live-proven (pause watcher, session 2).</summary>
        private static bool PauseOpen()
        {
            var root = GameObject.Find("PAUSE/Pause Canvas");
            return root != null && root.activeInHierarchy;
        }

        /// <summary>Event-derived autoplay (session-5, second trap): the Autoplay
        /// Waiting global is a scheduling flag with a designed leak (Autoplay Wait's
        /// Check Variables -> Off exit never clears it) — it stranded listening mode
        /// twice in one session, once refusing the very Leave the scene waited for.
        /// Truth now comes from the scene FSMs' own states via FsmSignals (a scene is
        /// pending exactly while some FSM sits in Autoplay Wait/Autoplay — no route
        /// can strand it), still yielding to a held interactable affordance. Genuine
        /// cutscene lockouts remain covered by the Input Pauser honor guard.</summary>
        private static bool AutoplayActive()
            => WindowState.AutoplayScenePending && !InteractableSelectionHeld();

        private static bool InteractableSelectionHeld()
        {
            var go = Navigator.Current();
            if (go == null || !go.activeInHierarchy) return false;
            var sel = go.GetComponent<UnityEngine.UI.Selectable>();
            return sel != null && sel.interactable;
        }

        /// <summary>A visible tutorial panel: an active Tutorial System child that isn't
        /// the continue Button or the always-active Input Pauser, and that carries
        /// readable text — the same standard the tutorial announcer applies. (W2 live
        /// finding: the naive active-child check read Tutorial permanently, because the
        /// Input Pauser is a text-less, always-active sibling of the real panels.)</summary>
        private static bool TutorialPanelActive()
        {
            var root = GameObject.Find("Letterbox Canvas/Tutorial System");
            if (root == null) return false;
            foreach (Transform panel in root.transform)
            {
                if (!panel.gameObject.activeInHierarchy) continue;
                if (panel.name == "Button" || panel.name == "Input Pauser") continue;
                // RENDERED visibility required (session-5: the cloud tutorial panel
                // stays GameObject-active after dismissal and hides by alpha — the
                // active+text test never released, trapping Tutorial mode). Same
                // effective-alpha standard as the notification watcher.
                if (EffectiveAlpha(panel) < 0.5f) continue;
                foreach (var tmp in panel.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    string txt = tmp.text;
                    if (!string.IsNullOrEmpty(txt) && txt.Trim().Length > 1)
                        return true;
                }
            }
            return false;
        }

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

        /// <summary>Event-driven: the Dialogue System's own conversationStarted/Ended
        /// (ConversationEvents) — reliable by construction, covers every trigger route.</summary>
        private static bool ConversationActive() => ConversationEvents.ConversationActive;

        /// <summary>Dice Gamepad System resting in Slotted (die placed, awaiting activate
        /// or retract — corpus-verified resting state) counts as allocation too.</summary>
        private static bool DiceSlottedResting()
        {
            var fsm = GameQueries.DiceSystemFsm();
            return fsm != null && fsm.ActiveStateName == "Slotted";
        }

        /// <summary>Selection inside an Item Cursor — the game's own semi-modal watch uses
        /// exactly this name compare (brief E, inventory constraint).</summary>
        private static bool InventoryCursorFocused()
        {
            var selected = Navigator.Current();
            return selected != null && selected.name == "Item Cursor";
        }

        /// <summary>Cloud: the Scan Button dial FIRST (named states, live-proven),
        /// the Hacking? global demoted to tiebreaker for transitional dial states
        /// (owner-approved 2026-07-19). The flag leaks — it stayed true after a
        /// cloud exit and stranded Cloud mode at the station (this session's live
        /// incident, the scheduling-flag trap class). State sides read from the
        /// live FSM decode: Disable re-enables to Scan Idle (cloud gate); Holding /
        /// Disable 2 / Disable 3 / Off 2 all resolve to Normal Idle (station side).</summary>
        private static string _cloudDivergenceLogged;

        private static bool CloudActive()
        {
            bool flag = GlobalBool("Hacking?");
            var scan = GameQueries.FindFsm("Scan Button", "Top UI");
            string state = scan != null ? scan.ActiveStateName : null;
            switch (state)
            {
                case "Scan Idle":
                case "Scan Mode Transition":
                case "Disable":
                    LogCloudDivergence(state, flag, dial: true);
                    return true;
                case "Normal Idle":
                case "Normal Transition":
                case "Holding":
                case "Disable 2":
                case "Disable 3":
                case "Off 2":
                    LogCloudDivergence(state, flag, dial: false);
                    return false;
                default:
                    // Transitional (Sound*/Animation/Off/Holding Scan Mode*) or
                    // dial unavailable: the flag breaks the tie.
                    return flag;
            }
        }

        private static void LogCloudDivergence(string state, bool flag, bool dial)
        {
            if (flag == dial) { _cloudDivergenceLogged = null; return; }
            if (_cloudDivergenceLogged == state) return;
            _cloudDivergenceLogged = state;
            Plugin.Log.LogInfo("[Mode] DIVERGENCE: Hacking?=" + flag + " vs Scan dial '"
                + state + "' — dial wins.");
        }

        /// <summary>Zone from the Location Controller dial (corpus: Filter branches into
        /// Rim/Greenway/Hub loop states with Transition sub-states).</summary>
        private static string CurrentZone()
        {
            var fsm = GameQueries.FindFsm("Location Controller");
            string state = fsm != null ? fsm.ActiveStateName : null;
            if (string.IsNullOrEmpty(state)) return null;
            if (state.Contains("Rim")) return "the Rim";
            if (state.Contains("Greenway")) return "the Greenway";
            if (state.Contains("Hub")) return "the Hub";
            return null;
        }

        private static bool GlobalBool(string name)
        {
            var v = FsmVariables.GlobalVariables.GetFsmBool(name);
            return v != null && v.Value;
        }
    }
}
