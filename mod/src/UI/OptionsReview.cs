using System.Collections.Generic;
using CSAccess.Speech;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Priority = CSAccess.Speech.Priority;

namespace CSAccess.UI
{
    /// <summary>
    /// Options menu review (focus-model row 16, owner-ruled; previously fully
    /// inaccessible). The menu is entirely discrete native Buttons — TEXT
    /// Default/Large, SCROLL Slower/Default/Faster, MUSIC 0-5, SFX 0-5, Back.
    ///
    /// Idiom (owner): Up/Down between rows, speaking the rendered row label plus
    /// the current value; Left/Right MUTATE directly on value rows (click the
    /// neighboring value button natively — slider-like, auto-apply); Enter engages
    /// only where it leads to a subdecision (Back). Every commit is a native
    /// Button click; settings apply as the game applies them.
    ///
    /// Current value marker (screenshot oracle, 2026-07-19): the active value
    /// renders its LABEL in the accent color; hover renders an outline instead.
    /// Reading the label color is a literal glyph transcode over a closed set
    /// (invariant 8). Colors are logged once per open for calibration.
    /// </summary>
    internal static class OptionsReview
    {
        private static GameObject _panel;
        private static int _row;
        private static bool _wasActive;
        private static bool _colorsLogged;

        public static bool IsActive()
        {
            if (_panel == null || !_panel.activeInHierarchy)
            {
                _panel = GameObject.Find("PAUSE/Pause Canvas/Options Menu");
                // Title-scene options path is uncaptured (acceptance item); a live
                // title visit with logging will surface it.
            }
            bool active = _panel != null && _panel.activeInHierarchy;
            if (active && !_wasActive)
            {
                _row = 0;
                _colorsLogged = false;
                SpeechService.Say("Options. Up and Down for settings, Left and Right to change.",
                    Priority.Queued, "nav");
            }
            _wasActive = active;
            return active;
        }

        public static void Review(int direction)
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            _row = Mathf.Clamp(_row + direction, 0, rows.Count - 1);
            SpeakRow(rows[_row]);
        }

        /// <summary>Left/Right on a value row: click the neighbor of the current value.</summary>
        public static void Adjust(int direction)
        {
            var rows = Rows();
            if (rows.Count == 0) return;
            var row = rows[_row];
            if (row.Values.Count == 0)
            {
                SpeakRow(row); // Back row: nothing to adjust
                return;
            }
            int current = CurrentValueIndex(row);
            int target = current < 0
                ? (direction > 0 ? 0 : row.Values.Count - 1)
                : Mathf.Clamp(current + direction, 0, row.Values.Count - 1);
            if (target == current)
            {
                SpeakRow(row); // already at the end — restate
                return;
            }
            var button = row.Values[target];
            if (button != null && button.interactable)
            {
                Navigator.Click(button.gameObject);
                SpeechService.Say(Label(button) ?? "changed", Priority.Immediate, "nav");
            }
        }

        /// <summary>Enter: engage only where it leads onward (Back). Value rows restate.</summary>
        public static bool Activate()
        {
            var rows = Rows();
            if (rows.Count == 0) return false;
            var row = rows[_row];
            if (row.Values.Count == 0 && row.Self != null)
            {
                Navigator.Click(row.Self.gameObject);
                return true;
            }
            SpeakRow(row);
            return true;
        }

        // ---------- Structure ----------

        private sealed class Row
        {
            public string LabelText;
            public Button Self;            // Back-style rows: the row IS a button
            public readonly List<Button> Values = new List<Button>();
        }

        private static List<Row> Rows()
        {
            var rows = new List<Row>();
            if (_panel == null) return rows;
            foreach (Transform child in _panel.transform)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                var row = new Row();
                row.Self = child.GetComponent<Button>();
                var ownTmp = child.GetComponent<TMP_Text>();
                if (ownTmp != null) row.LabelText = ownTmp.text?.Trim();
                else
                {
                    var childTmp = child.GetComponentInChildren<TMP_Text>(false);
                    if (childTmp != null) row.LabelText = childTmp.text?.Trim();
                }
                if (row.Self == null)
                {
                    foreach (Transform v in child)
                    {
                        var b = v.GetComponent<Button>();
                        if (b != null) row.Values.Add(b);
                    }
                }
                if (row.Self != null || row.Values.Count > 0)
                    rows.Add(row);
            }
            return rows;
        }

        private static void SpeakRow(Row row)
        {
            if (row.Values.Count == 0)
            {
                SpeechService.Say((row.LabelText ?? "item") + " button.", Priority.Immediate, "nav");
                return;
            }
            int current = CurrentValueIndex(row);
            string value = current >= 0 ? Label(row.Values[current]) : null;
            SpeechService.Say((row.LabelText ?? "setting") + (value != null ? ", " + value : "") + ".",
                Priority.Immediate, "nav");
        }

        /// <summary>The value whose label wears the accent color (redness winner). The
        /// rendered marker sighted players read; logged for calibration per open.</summary>
        private static int CurrentValueIndex(Row row)
        {
            int best = -1;
            float bestScore = 0.12f; // below this nothing is confidently "accented"
            for (int i = 0; i < row.Values.Count; i++)
            {
                var tmp = row.Values[i].GetComponentInChildren<TMP_Text>(false);
                if (tmp == null) continue;
                Color c = tmp.color;
                float score = c.r - (c.g + c.b) / 2f;
                if (!_colorsLogged)
                    Plugin.Log.LogInfo("[Options] " + (row.LabelText ?? "?") + "/" + (tmp.text?.Trim() ?? "?")
                                       + " color=" + c + " score=" + score.ToString("F3"));
                if (score > bestScore) { bestScore = score; best = i; }
            }
            _colorsLogged = true;
            return best;
        }

        private static string Label(Button b)
        {
            var tmp = b != null ? b.GetComponentInChildren<TMP_Text>(false) : null;
            return tmp != null ? tmp.text?.Trim() : null;
        }
    }
}
