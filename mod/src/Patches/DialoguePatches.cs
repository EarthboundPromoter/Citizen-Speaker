using System.Collections.Generic;
using CSAccess.Speech;
using HarmonyLib;
using PixelCrushers.DialogueSystem;
using Priority = CSAccess.Speech.Priority;

namespace CSAccess.Patches
{
    /// <summary>Tracks the currently offered dialogue responses so number keys can pick them.</summary>
    internal static class DialogueState
    {
        public static readonly List<string> CurrentResponses = new List<string>();
        public static bool MenuOpen;
        public static string LastSubtitle = "";
        public static string LastSpeaker = "";
        /// <summary>Incremented per subtitle so the watcher can announce a sole Continue option.</summary>
        public static long SubtitleSeq;

        public static void SetResponses(Response[] responses)
        {
            CurrentResponses.Clear();
            if (responses != null)
                foreach (var r in responses)
                    CurrentResponses.Add(r?.formattedText?.text ?? "");
            MenuOpen = CurrentResponses.Count > 0;
        }
    }

    internal static class DialogueAnnouncer
    {
        public static void OnSubtitle(Subtitle subtitle)
        {
            var text = subtitle?.formattedText?.text;
            if (string.IsNullOrEmpty(text)) return;
            var speaker = subtitle.speakerInfo?.Name;
            DialogueState.LastSubtitle = text;
            DialogueState.LastSpeaker = speaker ?? "";
            DialogueState.MenuOpen = false;
            DialogueState.SubtitleSeq++;

            if (!Plugin.AutoReadDialogue.Value) return;
            string line = (!string.IsNullOrEmpty(speaker) && Plugin.SpeakSpeakerNames.Value &&
                           !speaker.Equals("UNKNOWN", System.StringComparison.OrdinalIgnoreCase))
                ? speaker + ": " + text
                : text;
            SpeechService.Say(line, Priority.Queued, "dialogue");
        }

        public static void OnResponses(Response[] responses)
        {
            DialogueState.SetResponses(responses);
            int n = DialogueState.CurrentResponses.Count;
            if (n == 0) return;
            // Just the count; the focused choice announces itself (game auto-selects one,
            // or the watcher focuses the first). Arrows and number keys cover the rest.
            SpeechService.Say(n == 1 ? "1 choice." : n + " choices.", Priority.Queued, "choices");
        }
    }

    [HarmonyPatch(typeof(UnityUIDialogueUI), nameof(UnityUIDialogueUI.ShowSubtitle))]
    internal static class UnityUiSubtitlePatch
    {
        private static void Postfix(Subtitle subtitle) => DialogueAnnouncer.OnSubtitle(subtitle);
    }

    [HarmonyPatch(typeof(StandardDialogueUI), nameof(StandardDialogueUI.ShowSubtitle))]
    internal static class StandardSubtitlePatch
    {
        private static void Postfix(Subtitle subtitle) => DialogueAnnouncer.OnSubtitle(subtitle);
    }

    [HarmonyPatch(typeof(UnityUIDialogueUI), nameof(UnityUIDialogueUI.ShowResponses))]
    internal static class UnityUiResponsesPatch
    {
        private static void Postfix(Response[] responses) => DialogueAnnouncer.OnResponses(responses);
    }

    [HarmonyPatch(typeof(StandardDialogueUI), nameof(StandardDialogueUI.ShowResponses))]
    internal static class StandardResponsesPatch
    {
        private static void Postfix(Response[] responses) => DialogueAnnouncer.OnResponses(responses);
    }

    [HarmonyPatch(typeof(UnityUIDialogueUI), nameof(UnityUIDialogueUI.ShowAlert))]
    internal static class UnityUiAlertPatch
    {
        private static void Postfix(string message) =>
            SpeechService.Say(message, Priority.Queued, "alert");
    }

    [HarmonyPatch(typeof(StandardDialogueUI), nameof(StandardDialogueUI.ShowAlert))]
    internal static class StandardAlertPatch
    {
        private static void Postfix(string message) =>
            SpeechService.Say(message, Priority.Queued, "alert");
    }
}
