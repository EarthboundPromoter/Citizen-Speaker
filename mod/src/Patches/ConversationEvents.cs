using PixelCrushers.DialogueSystem;
using UnityEngine;

namespace CSAccess.Patches
{
    /// <summary>Event-driven conversation truth (W2 hardening, owner design): subscribes
    /// to the Dialogue System's own conversationStarted/conversationEnded — fired by the
    /// same code path that creates conversations, so no trigger route (player input,
    /// on-leave triggers, Ink story beats) can bypass them. Reliable by construction;
    /// replaces polling currentConversationState in the mode model.</summary>
    internal static class ConversationEvents
    {
        public static bool ConversationActive { get; private set; }

        private static DialogueSystemController _subscribed;

        public static void Tick()
        {
            // The controller is a scene object; resubscribe if it was recreated.
            var controller = DialogueManager.instance;
            if (!ReferenceEquals(controller, _subscribed))
            {
                if (controller != null)
                {
                    controller.conversationStarted += OnStarted;
                    controller.conversationEnded += OnEnded;
                    Plugin.Log.LogInfo("[Dialogue] conversation lifecycle events subscribed.");
                }
                _subscribed = controller;
                ConversationActive = false;
            }
            // The event-vs-poll divergence diagnostic ran clean across the six session-6
            // run snapshots (zero warnings) and was removed per its retirement condition.
        }

        private static void OnStarted(Transform actor)
        {
            ConversationActive = true;
            // U2: a fresh window announces its first named speaker once.
            DialogueState.LastAnnouncedSpeaker = "";
        }

        private static void OnEnded(Transform actor)
        {
            ConversationActive = false;
            // A conversation's end is a census beat: dialogue is the canonical writer
            // of the story flags that spawn/remove station markers (SABINE case).
            Game.StationCensus.OnBeat();
        }
    }
}
