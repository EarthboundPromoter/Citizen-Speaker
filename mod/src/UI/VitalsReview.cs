using CSAccess.Game;
using CSAccess.Modality;
using CSAccess.Speech;
using UnityEngine;

namespace CSAccess.UI
{
    /// <summary>
    /// The top instrument band as an enterable review (owner design 2026-07-23): the
    /// screen's anatomy is world tables in the middle and instrument bands at the
    /// edges — the bottom band (inventory/data) is already an enterable surface, and
    /// this mirrors it up top. Two rows, Vitals over Dice, matching the rendered
    /// Top UI (Energy UI + Ghost Trackers over Dice UI).
    ///
    /// Entry: the existing C (vitals) / V (dice) queries speak their full row exactly
    /// as they always have and, at camera surfaces, additionally position the review
    /// on that row. The same key again — or Backspace — returns to the surface, whose
    /// own table position is untouched. Arrows browse: Up/Down between the two rows
    /// (dead-end = bare repeat), Left/Right per cell with the column label. Read-only:
    /// Enter is a bare repeat — the band holds instruments, not affordances (Scan and
    /// Leave keep their own keys).
    ///
    /// CS2 seam: this class is band MECHANISM only; the per-game cell content lives
    /// in GameQueries.VitalsCells / DiceCells — the port swaps those providers.
    /// </summary>
    internal static class VitalsReview
    {
        public const int RowVitals = 0;
        public const int RowDice = 1;

        private static class W
        {
            // Paired-band naming (owner ruling 2026-07-23): either entry key announces
            // the shared bar, then its row — the rows are one structure, not two stops.
            public const string RowNameVitals = "UI bar: vitals.";
            public const string RowNameDice = "UI bar: dice.";
            public const string Back = "Back.";
        }

        private static bool _active;
        private static int _row;
        private static int _col = -1; // -1 = row level; cells start on first Left/Right

        public static bool Active => _active;

        /// <summary>The band exists over the camera surfaces only — overlays (windows,
        /// dialogue, the die picker) keep winning the keys, per affordance precedence.</summary>
        private static bool AtSurface(Mode mode)
            => mode == Mode.Station || mode == Mode.ActionView || mode == Mode.Cloud;

        /// <summary>C / V: speak the row (the query read, unchanged) and position the
        /// review there. While the band is open, EITHER entry key exits, wherever the
        /// arrows have moved you (owner live report 2026-07-23: the old same-row test
        /// repositioned first, so the second press exited from the wrong row);
        /// repositioning between rows is the arrows' job. Away from camera surfaces
        /// the keys stay plain glances.</summary>
        public static void QueryKey(Mode mode, int row)
        {
            if (!AtSurface(mode))
            {
                SpeechService.Say(RowRead(row), Priority.Immediate, "query");
                return;
            }
            if (_active) { Exit(mode); return; }
            _active = true;
            _row = row;
            _col = -1;
            SpeechService.Say(RowName(row) + " " + RowRead(row), Priority.Immediate, "query");
        }

        /// <summary>From InputManager, above the input-pause guard (speech-only keys,
        /// same standing as the queries this extends). True = key consumed.</summary>
        public static bool HandleKeys(Mode mode)
        {
            if (!_active) return false;
            if (!AtSurface(mode)) { _active = false; return false; }

            if (Input.GetKeyDown(KeyCode.DownArrow)) { MoveRow(1); return true; }
            if (Input.GetKeyDown(KeyCode.UpArrow)) { MoveRow(-1); return true; }
            if (Input.GetKeyDown(KeyCode.RightArrow)) { MoveCol(1); return true; }
            if (Input.GetKeyDown(KeyCode.LeftArrow)) { MoveCol(-1); return true; }
            if (Input.GetKeyDown(KeyCode.Space)
                || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
            { SpeechService.Say(RowRead(_row), Priority.Immediate, "query"); return true; }
            if (Input.GetKeyDown(KeyCode.Backspace)) { Exit(mode); return true; }
            return false;
        }

        private static void MoveRow(int delta)
        {
            int next = Mathf.Clamp(_row + delta, RowVitals, RowDice);
            if (next != _row) { _row = next; _col = -1; }
            // Edge = bare repeat (dead-end idiom), same speech either way.
            SpeechService.Say(RowName(_row) + " " + RowRead(_row), Priority.Immediate, "query");
        }

        private static void MoveCol(int delta)
        {
            var cells = _row == RowVitals ? GameQueries.VitalsCells() : GameQueries.DiceCells();
            if (cells.Count == 0)
            {
                SpeechService.Say(RowRead(_row), Priority.Immediate, "query");
                return;
            }
            _col = Mathf.Clamp(_col < 0 ? (delta > 0 ? 0 : 0) : _col + delta, 0, cells.Count - 1);
            var cell = cells[_col];
            SpeechService.Say(cell.Label + ": " + cell.Value + ".", Priority.Immediate, "query");
        }

        private static string RowName(int row)
            => row == RowVitals ? W.RowNameVitals : W.RowNameDice;

        private static string RowRead(int row)
            => row == RowVitals ? GameQueries.DescribeVitals() : GameQueries.DescribeDiceBrief();

        /// <summary>Return to the surface. Station/cloud re-announce their table position
        /// (the Ctrl+X return convention); the location grid kept its position and gets
        /// the bare acknowledgment.</summary>
        private static void Exit(Mode mode)
        {
            _active = false;
            _col = -1;
            if (NavIdiom.Native) { SpeechService.Say(W.Back, Priority.Immediate, "query"); return; }
            if (mode == Mode.Station) { MapTable.AnnouncePosition(); return; }
            if (mode == Mode.Cloud) { CloudTable.AnnouncePosition(); return; }
            SpeechService.Say(W.Back, Priority.Immediate, "query");
        }
    }
}
