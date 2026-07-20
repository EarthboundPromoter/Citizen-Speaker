using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using CSAccess.Speech;
using HarmonyLib;
using UnityEngine;
using Priority = CSAccess.Speech.Priority;

namespace CSAccess
{
    [BepInPlugin(Id, "Citizen Sleeper Access", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.sleeperaccess.mod";

        internal static ManualLogSource Log;
        internal static ConfigEntry<bool> AutoReadDialogue;
        internal static ConfigEntry<bool> SpeakSpeakerNames;
        internal static ConfigEntry<bool> AnnounceFocus;
        internal static ConfigEntry<bool> ForceGamepadUI;
        internal static ConfigEntry<bool> TraceFsmSignals;
        internal static ConfigEntry<bool> TraceInput;
        internal static ConfigEntry<bool> MapTableCamera;
        internal static ConfigEntry<bool> NarrateOnPlacement;

        private InputManager _input;
        private Watchers _watchers;

        private void Awake()
        {
            Log = Logger;
            Application.runInBackground = true;

            AutoReadDialogue = Config.Bind("Speech", "AutoReadDialogue", true,
                "Automatically read dialogue lines as they appear.");
            SpeakSpeakerNames = Config.Bind("Speech", "SpeakSpeakerNames", true,
                "Prefix dialogue lines with the speaker's name.");
            AnnounceFocus = Config.Bind("Speech", "AnnounceFocus", true,
                "Announce UI elements when they receive focus.");
            ForceGamepadUI = Config.Bind("Input", "ForceGamepadUI", true,
                "Keep the game's UI in gamepad mode so the keyboard dice flow works.");
            TraceFsmSignals = Config.Bind("Debug", "TraceFsmSignals", false,
                "Log every subscribed FSM state-entry dispatch (dev diagnostics).");
            TraceInput = Config.Bind("Debug", "TraceInput", false,
                "Log every mod-relevant key press and synthetic nav event live "
                + "(always recorded in memory for the F3 incident dump regardless).");
            MapTableCamera = Config.Bind("MapTable", "CameraFollow", true,
                "Station table row moves drive the camera (Focus Z write - the game's "
                + "own scroll accumulator). Disable to browse without camera movement.");
            NarrateOnPlacement = Config.Bind("Speech", "NarrateOnPlacement", true,
                "After a die settles on an action, read the action's pre-spend narrative "
                + "followed by 'Enter to start.' (owner design 2026-07-20). Disable for "
                + "placement-acknowledgment only.");

            SpeechService.Init();

            var harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Plugin).Assembly);

            _input = new InputManager(this);
            _watchers = new Watchers();
            Game.CycleGate.Init();
            Game.ActionOutcomes.Init();
            Game.CloudOutcomes.Init();
            Game.RefusalWatch.Init();
            Modality.WindowState.Init();
            Modality.CloudFlight.Init();

            Log.LogInfo("Citizen Sleeper Access 0.1.0 loaded. Press F1 in game for commands.");
            SpeechService.Say("Citizen Sleeper Access loaded. Press F1 for commands.", Priority.Queued, "init");
        }

        private void Update()
        {
            _input.Tick();
            _watchers.Tick();
            Game.ActionOutcomes.Tick();
            Game.CloudOutcomes.Tick();
            Game.RefusalWatch.Tick();
            UI.CharacterTable.Tick();
            UI.MapTable.Tick();
            UI.JournalTable.Tick();
            UI.CloudTable.Tick();
            Modality.CloudFlight.Tick();
            Patches.FocusPatch.Tick();
            SpeechService.Tick();
            Substrate.SelfTest.Tick();
            Patches.ConversationEvents.Tick();
        }

        private void OnDestroy()
        {
            SpeechService.Shutdown();
        }
    }
}
