using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using CSAccess.Scaffold;
using CSAccess.Middleware;
using CSAccess.Sleeper1;
using Priority = CSAccess.Scaffold.Priority;

namespace CSAccess
{
    [BepInPlugin(Id, "Citizen Sleeper Access", Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.sleeperaccess.mod";
        /// <summary>One source for the declared, logged and spoken version (the spoken
        /// string still said 0.9.6 in the 0.9.7 release).</summary>
        public const string Version = "0.9.7";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> AutoReadDialogue;
        internal static ConfigEntry<bool> SpeakSpeakerNames;
        internal static ConfigEntry<bool> AnnounceFocus;
        internal static ConfigEntry<bool> ForceGamepadUI;
        internal static ConfigEntry<bool> TraceFsmSignals;
        internal static ConfigEntry<bool> TraceInput;
        internal static ConfigEntry<bool> DumpStatDiagnostics;
        internal static ConfigEntry<bool> MapTableCamera;

        private InputManager _input;
        private Watchers _watchers;

        private void Awake()
        {
            Log = Logger;
            Application.runInBackground = true;

            AutoReadDialogue = Config.Bind("Speech", "AutoReadDialogue", true,
                "Automatically read dialogue lines as they appear.");
            SpeakSpeakerNames = Config.Bind("Speech", "SpeakSpeakerNames", true,
                "Announce the dialogue speaker's name when it changes (window open "
                + "or a mid-conversation switch), not on every advance.");
            AnnounceFocus = Config.Bind("Speech", "AnnounceFocus", true,
                "Announce UI elements when they receive focus.");
            ForceGamepadUI = Config.Bind("Input", "ForceGamepadUI", true,
                "Keep the game's UI in gamepad mode so the keyboard dice flow works.");
            TraceFsmSignals = Config.Bind("Debug", "TraceFsmSignals", false,
                "Log every subscribed FSM state-entry dispatch (dev diagnostics).");
            TraceInput = Config.Bind("Debug", "TraceInput", false,
                "Log every mod-relevant key press and synthetic nav event live "
                + "(always recorded in memory for the F3 incident dump regardless).");
            DumpStatDiagnostics = Config.Bind("Debug", "DumpStatDiagnostics", false,
                "On station-scene entry, dump all PlayMaker globals and the HUD stat "
                + "FSMs to the log (the original stat-source hunt; dev diagnostics).");
            MapTableCamera = Config.Bind("MapTable", "CameraFollow", true,
                "Station table row moves drive the camera (Focus Z write - the game's "
                + "own scroll accumulator). Disable to browse without camera movement.");

            SpeechService.Init();

            // Tier seams (stage 1): the scaffold speaks and diagnoses through
            // injected providers; the composition root is the only place that
            // wires game-tier knowledge downward.
            ElementDescriber.Element = Describe.Element;
            NavSignals.UserNavigated = FocusPatch.NoteUserNavigation;
            NavSignals.ClearCooldown = FocusPatch.ClearCooldown;
            Diag.ModeNameProvider = () => ModeModel.Name(ModeModel.Current());
            Diag.StateProvider = DiagState;
            Diag.FsmRecentProvider = n =>
            {
                var list = new System.Collections.Generic.List<(int, string, string)>();
                foreach (var r in FsmSignals.Recent(n))
                    list.Add((r.Frame, r.OwnerName, r.StateName));
                return list;
            };

            var harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Plugin).Assembly);

            _input = new InputManager(this);
            _watchers = new Watchers();
            CycleGate.Init();
            ActionOutcomes.Init();
            CloudOutcomes.Init();
            RefusalWatch.Init();
            ConditionWatch.Init();
            EnergyWatch.Init();
            ResourceWatch.Init();
            StationCensus.Init();
            EndgameWatch.Init();
            WindowState.Init();
            CloudFlight.Init();
            CloudTable.Init();
            PerkWatch.Init();
            TitleFlow.Init();

            Log.LogInfo("Citizen Speaker " + Version + " loaded. Press F1 in game for commands.");
            SpeechService.Say("Citizen Speaker " + Version + ". Press F1 for commands.", Priority.Queued, "init");
        }

        private void Update()
        {
            _input.Tick();
            _watchers.Tick();
            ActionOutcomes.Tick();
            CloudOutcomes.Tick();
            RefusalWatch.Tick();
            ConditionWatch.Tick();
            EnergyWatch.Tick();
            ResourceWatch.Tick();
            StationCensus.Tick();
            CharacterTable.Tick();
            MapTable.Tick();
            JournalTable.Tick();
            CloudTable.Tick();
            CloudFlight.Tick();
            FocusPatch.Tick();
            SpeechService.Tick();
            SelfTest.Tick();
            ConversationEvents.Tick();
        }

        /// <summary>Diag's believed-state entries (keys = the /modstate contract).
        /// Guarded per entry — a broken reader must not cost the rest (the same
        /// resilience StateNow had when the reads were inline).</summary>
        private static System.Collections.Generic.Dictionary<string, object> DiagState()
        {
            var state = new System.Collections.Generic.Dictionary<string, object>();
            try
            {
                state["characterWindow"] = WindowState.CharacterWindowOpen;
                state["driveLog"] = WindowState.DriveLogOpen;
                state["inventory"] = WindowState.InventoryOpen;
                state["autoplayPending"] = WindowState.AutoplayScenePending;
            }
            catch { }
            try { state["conversation"] = ConversationEvents.ConversationActive; } catch { }
            try { state["responseMenu"] = DialogueState.MenuOpen; } catch { }
            try { state["inputPaused"] = GameQueries.InputPaused(); } catch { }
            return state;
        }

        private void OnDestroy()
        {
            SpeechService.Shutdown();
        }
    }
}
