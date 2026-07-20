using System.Collections.Generic;

namespace CSAccess.Modality
{
    internal enum ModKey
    {
        Navigate,       // arrows through the game's own graph
        Activate,       // Enter
        Cancel,         // Backspace — designed cancel, mode-resolved
        Vitals,         // C (s5 keymap reorder)
        Dice,           // V (s5 keymap reorder)
        Clocks,         // K
        WhereAmI,       // L
        Respeak,        // Z
        RereadDialogue, // R
        Describe,       // Space
        Help,           // F1
        InventoryToggle,   // I
        CharacterToggle,   // U
        DriveLogToggle,    // J
        ScanToggle,        // O (s5 keymap reorder; S/D are native camera keys)
        Reroll,            // Shift+R
        TutorialContinue,  // T
        NumberChoices,     // 1-9
        ReviewArrows,      // review-cursor movement in cursor-owning modes
        MapTable,          // N — station map table (map-table-design.md)
    }

    /// <summary>
    /// Per-mode key availability (W2, input-model.md) — the one table both input
    /// dispatch and F1 contextual help read, so what F1 speaks and what keys do can
    /// never drift apart. Queries and speech keys are deliberately near-universal in
    /// gameplay ("queriable whenever", owner ruling); game-touching keys are scoped.
    /// </summary>
    internal static class KeyScope
    {
        private static readonly ModKey[] SpeechAndQueries =
            { ModKey.Vitals, ModKey.Dice, ModKey.WhereAmI, ModKey.Respeak, ModKey.RereadDialogue, ModKey.Describe, ModKey.Help };

        private static readonly Dictionary<Mode, HashSet<ModKey>> Table = Build();

        private static Dictionary<Mode, HashSet<ModKey>> Build()
        {
            var t = new Dictionary<Mode, HashSet<ModKey>>();
            HashSet<ModKey> S(params ModKey[] keys)
            {
                var set = new HashSet<ModKey>(keys);
                foreach (var k in SpeechAndQueries) set.Add(k);
                return set;
            }

            // Title carries no save-backed queries — minimal set, no V/D/K/L.
            t[Mode.Title] = new HashSet<ModKey>
                { ModKey.Navigate, ModKey.Activate, ModKey.Respeak, ModKey.Help };
            t[Mode.CharacterSelect] = new HashSet<ModKey>
                { ModKey.Navigate, ModKey.Activate, ModKey.ReviewArrows, ModKey.Respeak, ModKey.Help };

            // Listening states: game-facing keys quiet; speech and queries remain.
            t[Mode.CycleTransition] = S();
            t[Mode.Autoplay] = S();

            // Pause: ReviewArrows added for the options-menu review (focus-model row 16).
            t[Mode.Pause] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.ReviewArrows);
            // Tutorial: ScanToggle stays live — the cloud tutorial's continue sits
            // DISABLED while its panel is up; performing the taught action (the scan
            // toggle) is its designed dismissal. Refusing S was a mod trap
            // (session-5, third of the taught-action trap class).
            t[Mode.Tutorial] = S(ModKey.Navigate, ModKey.Activate, ModKey.ReviewArrows, ModKey.TutorialContinue,
                ModKey.ScanToggle);
            t[Mode.ResponseMenu] = S(ModKey.Navigate, ModKey.Activate, ModKey.NumberChoices);
            t[Mode.Dialogue] = S(ModKey.Navigate, ModKey.Activate);
            t[Mode.DiceAllocation] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Reroll,
                ModKey.InventoryToggle, ModKey.CharacterToggle, ModKey.DriveLogToggle);
            // Character table owns arrows/Enter/Space here (fourth table, owner
            // 2026-07-20) — ReviewArrows dropped with the retired review cursor so
            // F1 no longer offers "review".
            t[Mode.CharacterWindow] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel,
                ModKey.CharacterToggle);
            t[Mode.DriveLog] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.DriveLogToggle);
            // Inventory: U/J stay live — the designed controller idiom is dedicated
            // buttons (R1/L1) and the opening panel itself closes the strip (corpus:
            // Character UI Button and Drive Log Button Open states both send Inventory
            // Deactivate); refusing them was stricter than the game (focus-model row 11).
            // S stays out pending the scan-from-strip check.
            t[Mode.Inventory] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.InventoryToggle,
                ModKey.CharacterToggle, ModKey.DriveLogToggle);
            // Cancel in scope (owner-approved 2026-07-19): the universal Leave Button
            // click IS the designed cloud-node exit — its Leave Action sends
            // CloseAction to $ActiveAction, which cloud markers answer with the
            // camera pull-back (corpus: Havenage Agent 2 Canvas 2 Location Button,
            // Active -> Camera Transition 2). Without it, an open node is a trap:
            // the game disables the Scan toggle while a node is open.
            t[Mode.Cloud] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Clocks, ModKey.Reroll,
                ModKey.ScanToggle);
            // K retired at action view (owner 2026-07-20): the location table's Clocks
            // tab is the clock index — "K becomes unnecessary under a logical nav
            // grammar." Arrows/Enter/Space route to LocationTable in InputManager.
            t[Mode.ActionView] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Reroll,
                ModKey.InventoryToggle, ModKey.CharacterToggle, ModKey.DriveLogToggle, ModKey.ScanToggle);
            t[Mode.Station] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Clocks,
                ModKey.InventoryToggle, ModKey.CharacterToggle, ModKey.DriveLogToggle, ModKey.ScanToggle,
                ModKey.MapTable);
            return t;
        }

        public static bool Allows(Mode mode, ModKey key)
            => Table.TryGetValue(mode, out var set) && set.Contains(key);

        // ---------- F1 contextual help (glyph-guide idiom, owner design) ----------

        private static readonly (ModKey key, string help)[] HelpOrder =
        {
            (ModKey.Navigate, "Arrows: move"),
            (ModKey.Activate, "Enter: activate"),
            (ModKey.Cancel, "Backspace: back"),
            (ModKey.ReviewArrows, "Up and Down: review"),
            (ModKey.NumberChoices, "Number keys: pick a response"),
            (ModKey.TutorialContinue, "T: focus continue"),
            (ModKey.Reroll, "Shift R: reroll dice"),
            (ModKey.MapTable, "N: station table"),
            (ModKey.InventoryToggle, "I: inventory"),
            (ModKey.CharacterToggle, "U: character window"),
            (ModKey.DriveLogToggle, "J: drive log"),
            (ModKey.ScanToggle, "O: scan"),
            (ModKey.Vitals, "C: vitals"),
            (ModKey.Dice, "V: dice"),
            (ModKey.Clocks, "K: clocks"),
            (ModKey.WhereAmI, "L: where am I"),
            (ModKey.RereadDialogue, "R: reread dialogue"),
            (ModKey.Respeak, "Z: repeat speech"),
            (ModKey.Describe, "Space: describe focus"),
        };

        /// <summary>Only the keys live on the current screen — the console glyph-bar idiom.</summary>
        public static string HelpFor(Mode mode)
        {
            var sb = new System.Text.StringBuilder(ModeModel.Name(mode)).Append(". ");
            foreach (var (key, help) in HelpOrder)
                if (Allows(mode, key))
                    sb.Append(help).Append(". ");
            return sb.ToString().TrimEnd();
        }
    }
}
