using PixelCrushers.DialogueSystem;
using UnityEngine;

using CSAccess.Scaffold;

namespace CSAccess.Middleware
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
                // Leave the old controller first, so a controller that is replaced
                // but not destroyed cannot deliver every event twice (2026-10-25).
                if (!ReferenceEquals(_subscribed, null))
                {
                    try
                    {
                        _subscribed.conversationStarted -= OnStarted;
                        _subscribed.conversationEnded -= OnEnded;
                    }
                    catch { }
                }
                if (controller != null)
                {
                    controller.conversationStarted += OnStarted;
                    controller.conversationEnded += OnEnded;
                    Plugin.Log.LogInfo("[Dialogue] conversation lifecycle events subscribed.");
                }
                _subscribed = controller;
                ConversationActive = false;
            }
        }

        private static void OnStarted(Transform actor)
        {
            ConversationActive = true;
            DialogueState.ClearMenu();
            // U2: a fresh window announces its first named speaker once.
            DialogueState.LastAnnouncedSpeaker = "";
            // The dialogue-log attribution history is per-conversation, like the
            // rendered log itself.
            DialogueState.History.Clear();
        }

        private static void OnEnded(Transform actor)
        {
            ConversationActive = false;
            DialogueState.ClearMenu();
            // A conversation's end is a census beat: dialogue is the canonical writer
            // of the story flags that spawn/remove station markers (SABINE case).
            // TIER-X (stage 1, 2026-07-25): middleware->game reference, fully
            // qualified so it greps; stage 2 inverts this to an event the census
            // subscribes to.
            CSAccess.Sleeper1.StationCensus.OnBeat();
        }
    }
}
