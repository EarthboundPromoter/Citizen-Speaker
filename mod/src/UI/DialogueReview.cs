using System.Collections.Generic;
using CSAccess.Patches;
using CSAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace CSAccess.UI
{
    /// <summary>
    /// The rendered dialogue log as a review (owner design 2026-07-23): the game
    /// keeps past lines as dimmed blocks in the subtitle panel's scroll content —
    /// reading back is a sanctioned affordance, surfaced. B enters on the live
    /// block; Up steps back a block, Down forward; Space and Enter re-read (Enter
    /// presses NOTHING — the log can never advance the conversation); B or
    /// Backspace return to live, which during a response menu means the choices,
    /// untouched. The scroll view follows the focused block as a strictly-may-fail
    /// garnish — any failure there degrades to no scroll, never to broken speech.
    ///
    /// Decode (live 2026-07-23): blocks are "Previous Text(Clone)" TMPs in layout
    /// order under Scroll Content, oldest first; "Subtitle Text" is the live line;
    /// chosen responses never enter the log (the menu is a sibling panel).
    ///
    /// Attribution (owner ruling): the rendered blocks are bare text — the mod
    /// remembers who spoke each line as it was live (DialogueState.History) and
    /// backfills per block, UNCONDITIONALLY (random access has no flow context, so
    /// the auto-read's same-speaker suppression does not apply here); bare when
    /// nothing matches (lines from before the mod listened).
    /// </summary>
    internal static class DialogueReview
    {
        private const string PanelPath =
            "CS Dialogue Manager/Canvas/TMP CS Dialogue UI 1/Dialogue Panel/Main Panel";

        private static class W
        {
            public const string Entered = "Dialogue log.";
            public const string Empty = "No dialogue log.";
            public const string Back = "Back.";
        }

        private static bool _active;
        private static int _index;

        public static bool Active => _active;

        public static void Toggle()
        {
            if (_active) { Exit(); return; }
            var blocks = Blocks();
            if (blocks.Count == 0)
            {
                SpeechService.Say(W.Empty, Priority.Immediate, "dialogue");
                return;
            }
            _active = true;
            _index = blocks.Count - 1; // enter on the live block
            SpeechService.Say(W.Entered + " " + BlockRead(blocks, _index),
                Priority.Immediate, "dialogue");
            ScrollTo(blocks[_index]);
        }

        /// <summary>From InputManager while active. True = key consumed. Deactivates
        /// itself when the conversation modes end (scene advance, cutaway).</summary>
        public static bool HandleKeys(Modality.Mode mode)
        {
            if (!_active) return false;
            if (mode != Modality.Mode.Dialogue && mode != Modality.Mode.ResponseMenu)
            { _active = false; return false; }

            if (Input.GetKeyDown(KeyCode.UpArrow)) { Move(-1); return true; }
            if (Input.GetKeyDown(KeyCode.DownArrow)) { Move(1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { Move(0); return true; }
            if (Input.GetKeyDown(KeyCode.Backspace)) { Exit(); return true; }
            return false;
        }

        private static void Move(int delta)
        {
            var blocks = Blocks();
            if (blocks.Count == 0) { Exit(); return; }
            _index = Mathf.Clamp(_index + delta, 0, blocks.Count - 1);
            SpeechService.Say(BlockRead(blocks, _index), Priority.Immediate, "dialogue");
            ScrollTo(blocks[_index]);
        }

        private static void Exit()
        {
            _active = false;
            // Live selection was never touched; during a response menu the choices
            // are exactly where they were.
            SpeechService.Say(W.Back, Priority.Immediate, "dialogue");
        }

        // ---------- Blocks ----------

        private static List<Transform> Blocks()
        {
            var list = new List<Transform>();
            var go = GameObject.Find(PanelPath + "/Scroll Rect/Scroll Content");
            if (go == null) return list;
            foreach (Transform child in go.transform)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                if (!child.name.StartsWith("Previous Text") && child.name != "Subtitle Text")
                    continue;
                var tmp = child.GetComponent<TMPro.TMP_Text>();
                if (tmp != null && !string.IsNullOrWhiteSpace(tmp.text)) list.Add(child);
            }
            return list;
        }

        private static string BlockText(Transform block)
        {
            var tmp = block.GetComponent<TMPro.TMP_Text>();
            return tmp != null ? SpeechService.Clean(tmp.text.Trim()) : "";
        }

        private static string BlockRead(List<Transform> blocks, int index)
        {
            string text = BlockText(blocks[index]);
            string speaker = SpeakerFor(blocks, index, text);
            bool named = !string.IsNullOrEmpty(speaker) && Plugin.SpeakSpeakerNames.Value
                && !speaker.Equals("UNKNOWN", System.StringComparison.OrdinalIgnoreCase);
            return (named ? speaker + ": " : "") + text;
        }

        /// <summary>Text-keyed match against the live-capture history, with an order
        /// tiebreak for duplicate lines: the Nth same-text block takes the Nth
        /// same-text history entry. Null = no match, block reads bare.</summary>
        private static string SpeakerFor(List<Transform> blocks, int index, string text)
        {
            var hist = DialogueState.History;
            if (hist.Count == 0) return null;
            string norm = Normalize(text);
            if (norm.Length == 0) return null;
            int occurrence = 0;
            for (int i = 0; i < index; i++)
                if (Normalize(BlockText(blocks[i])) == norm) occurrence++;
            int seen = 0;
            foreach (var entry in hist)
            {
                if (Normalize(entry.Text) != norm) continue;
                if (seen == occurrence) return entry.Speaker;
                seen++;
            }
            return null;
        }

        private static string Normalize(string s)
            => string.IsNullOrEmpty(s) ? "" :
               System.Text.RegularExpressions.Regex.Replace(
                   SpeechService.Clean(s), @"\s+", " ").Trim();

        // ---------- Scroll sync (garnish; every failure path is silent) ----------

        private static void ScrollTo(Transform block)
        {
            var sr = GameObject.Find(PanelPath + "/Scroll Rect");
            var scroll = sr != null ? sr.GetComponent<ScrollRect>() : null;
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            float contentH = scroll.content.rect.height;
            float viewH = scroll.viewport.rect.height;
            if (contentH <= viewH) return;
            var rt = block as RectTransform;
            if (rt == null) return;
            // Block center's distance from the content top (layout children anchor
            // top-down, anchoredPosition.y runs negative downward), centered in the
            // viewport, normalized to the scrollable span. uGUI: 1 = top, 0 = bottom.
            float fromTop = -rt.anchoredPosition.y + rt.rect.height * 0.5f;
            float norm = 1f - Mathf.Clamp01((fromTop - viewH * 0.5f) / (contentH - viewH));
            scroll.verticalNormalizedPosition = norm;
        }
    }
}
