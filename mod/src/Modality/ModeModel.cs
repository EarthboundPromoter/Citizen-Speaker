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

            // --- Full-screen interrupts, strongest first ---
            if (PauseOpen()) return Mode.Pause;
            if (GameQueries.CycleTransitionActive()) return Mode.CycleTransition;
            if (AutoplayActive()) return Mode.Autoplay;
            if (TutorialPanelActive()) return Mode.Tutorial;

            // --- Conversation layer ---
            if (DialogueState.MenuOpen) return Mode.ResponseMenu;
            if (ConversationActive()) return Mode.Dialogue;

            // --- Allocation rides on top of action/cloud views ---
            if (GameQueries.DiceAllocationActive() || DiceSlottedResting()) return Mode.DiceAllocation;

            // --- Windows (one-directional exclusion only; see class doc) ---
            bool charWin = CharacterWindowOpen();
            bool driveLog = DriveLogOpen();
            if (charWin && driveLog && Time.unscaledTime - _bothWindowsLogged > 30f)
            {
                _bothWindowsLogged = Time.unscaledTime;
                Plugin.Log.LogInfo("[Mode] Character window and drive log both read open (ledgered ambiguity).");
            }
            if (charWin) return Mode.CharacterWindow;
            if (driveLog) return Mode.DriveLog;
            if (InventoryCursorFocused()) return Mode.Inventory;

            // --- Camera-level modes ---
            if (CloudActive()) return Mode.Cloud;
            if (GlobalBool("Action View?")) return Mode.ActionView;
            return Mode.Station;
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

        /// <summary>Autoplay flags — corpus-verified writers (brief F MODE MACHINERY),
        /// both present in the live globals dump.</summary>
        private static bool AutoplayActive()
            => GlobalBool("Scenes Active?") || GlobalBool("Autoplay Waiting");

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
                foreach (var tmp in panel.GetComponentsInChildren<TMPro.TMP_Text>(false))
                {
                    string txt = tmp.text;
                    if (!string.IsNullOrEmpty(txt) && txt.Trim().Length > 1)
                        return true;
                }
            }
            return false;
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

        /// <summary>CanvasGroup-alpha open checks — live-proven pattern (window watchers).</summary>
        private static bool CharacterWindowOpen() => AlphaOpen("Letterbox Canvas/Character Window");
        private static bool DriveLogOpen() => AlphaOpen("Letterbox Canvas/Drive System/CS Drive Log");

        private static bool AlphaOpen(string path)
        {
            var go = GameObject.Find(path);
            if (go == null || !go.activeInHierarchy) return false;
            var group = go.GetComponent<CanvasGroup>();
            return group == null || group.alpha > 0.5f;
        }

        /// <summary>Selection inside an Item Cursor — the game's own semi-modal watch uses
        /// exactly this name compare (brief E, inventory constraint).</summary>
        private static bool InventoryCursorFocused()
        {
            var selected = Navigator.Current();
            return selected != null && selected.name == "Item Cursor";
        }

        /// <summary>Cloud: the Hacking? global (corpus-verified writers) or the Scan Button
        /// dial sitting on its scan side (named states, brief F). An unrecognized Scan
        /// Button state is logged once rather than guessed at (graceful silence).</summary>
        private static string _unknownScanStateLogged;

        private static bool CloudActive()
        {
            if (GlobalBool("Hacking?")) return true;
            var scan = GameQueries.FindFsm("Scan Button", "Top UI");
            if (scan == null) return false;
            string state = scan.ActiveStateName;
            switch (state)
            {
                case "Scan Idle":
                case "Scan Mode Transition":
                    return true;
                case "Normal Idle":
                case "Normal Transition":
                case "Holding":
                case null:
                case "":
                    return false;
                default:
                    if (_unknownScanStateLogged != state)
                    {
                        _unknownScanStateLogged = state;
                        Plugin.Log.LogInfo("[Mode] Scan Button in unmapped state '" + state + "' — treated as not-cloud.");
                    }
                    return false;
            }
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
