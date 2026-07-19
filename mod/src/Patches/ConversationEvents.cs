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
        private static float _nextDivergenceCheck;

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

            // Diagnostic, one clean session then remove: event flag vs the old poll.
            if (Time.unscaledTime >= _nextDivergenceCheck)
            {
                _nextDivergenceCheck = Time.unscaledTime + 30f;
                bool polled = false;
                try { polled = DialogueManager.isConversationActive; } catch { }
                if (polled != ConversationActive)
                    Plugin.Log.LogWarning("[Dialogue] DIVERGENCE: event flag=" + ConversationActive
                        + " poll=" + polled + " — investigate before dropping the poll.");
            }
        }

        private static void OnStarted(Transform actor) => ConversationActive = true;
        private static void OnEnded(Transform actor) => ConversationActive = false;
    }
}
