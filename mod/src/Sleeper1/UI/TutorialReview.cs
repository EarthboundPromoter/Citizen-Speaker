using System.Collections.Generic;
using TMPro;
using UnityEngine;

using CSAccess.Scaffold;
using CSAccess.Middleware;

namespace CSAccess.Sleeper1
{
    /// <summary>Focus model for tutorial popups. The game's tutorials are static text
    /// overlays whose only selectable is the shared CONTINUE button, so review structure
    /// is ours: arrows step through the panel's text blocks; T continues.</summary>
    internal static class TutorialReview
    {
        private static int _index = -1;
        private static int _panelId;

        public static Transform ActivePanel()
        {
            var root = GameObject.Find("Letterbox Canvas/Tutorial System");
            if (root == null) return null;
            foreach (Transform child in root.transform)
            {
                if (child.name == "Button") continue;
                if (!child.gameObject.activeInHierarchy) continue;
                // The Tutorial System permanently hosts always-active helper objects
                // (tutorial triggers, Input Pauser). Only a child that actually renders
                // text is a tutorial panel — without this check IsActive() is always
                // true and eats Up/Down everywhere (the silent pause-menu arrows bug).
                if (child.GetComponentInChildren<TMP_Text>(false) == null) continue;
                // Rendered visibility required (session-5): the cloud tutorial panel
                // stays GameObject-active after dismissal and hides by CanvasGroup
                // alpha — the active+text test alone never releases.
                if (Util.AlphaUpTo(child) < 0.5f) continue;
                return child;
            }
            return null;
        }

        public static bool IsActive() => ActivePanel() != null;

        public static void Review(int delta)
        {
            var panel = ActivePanel();
            if (panel == null) return;
            if (panel.GetInstanceID() != _panelId)
            {
                _panelId = panel.GetInstanceID();
                _index = -1;
            }
            var items = BuildItems(panel);
            if (items.Count == 0) return;
            int next = Mathf.Clamp(_index + delta, 0, items.Count - 1);
            string boundary = "";
            // delta 0 = bare repeat of the current block (Left/Right seal) — no
            // boundary phrase.
            if (next == _index && delta != 0)
                boundary = delta > 0 ? "End of tutorial. Press Enter to continue. " : "Top of tutorial. ";
            _index = next;
            SpeechService.Say(boundary + items[_index], Priority.Immediate, "tutorial");
        }

        /// <summary>Text blocks in hierarchy (layout) order; fragments of one or two
        /// characters are diagram labels (dice pips, bar numbers), not prose. Each
        /// block goes through the announce's own transcode, so key glyphs read as
        /// the same key names the full read gives.</summary>
        private static List<string> BuildItems(Transform panel)
        {
            var items = new List<string>();
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string raw = tmp.text?.Trim();
                if (string.IsNullOrEmpty(raw) || raw.Length <= 2) continue;
                string txt = Watchers.TutorialBlock(tmp, panel);
                if (txt != null && !items.Contains(txt)) items.Add(txt);
            }
            return items;
        }
    }
}
