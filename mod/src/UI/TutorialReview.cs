using System.Collections.Generic;
using CSAccess.Speech;
using TMPro;
using UnityEngine;

namespace CSAccess.UI
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
                if (child.gameObject.activeInHierarchy) return child;
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
            if (next == _index)
                boundary = delta > 0 ? "End of tutorial. Press T to continue. " : "Top of tutorial. ";
            _index = next;
            SpeechService.Say(boundary + items[_index], Priority.Immediate, "tutorial");
        }

        /// <summary>Text blocks in hierarchy (layout) order; fragments of one or two
        /// characters are diagram labels (dice pips, bar numbers), not prose.</summary>
        private static List<string> BuildItems(Transform panel)
        {
            var items = new List<string>();
            foreach (var tmp in panel.GetComponentsInChildren<TMP_Text>(false))
            {
                string txt = tmp.text?.Trim();
                if (string.IsNullOrEmpty(txt) || txt.Length <= 2) continue;
                if (!items.Contains(txt)) items.Add(txt);
            }
            return items;
        }
    }
}
