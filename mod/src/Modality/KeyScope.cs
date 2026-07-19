using System.Collections.Generic;

namespace CSAccess.Modality
{
    internal enum ModKey
    {
        Navigate,       // arrows through the game's own graph
        Activate,       // Enter
        Cancel,         // Backspace — designed cancel, mode-resolved
        Vitals,         // V
        Dice,           // D
        Clocks,         // K
        WhereAmI,       // L
        Respeak,        // Z
        RereadDialogue, // R
        Describe,       // Space
        Help,           // F1
        InventoryToggle,   // I
        CharacterToggle,   // U
        DriveLogToggle,    // J
        ScanToggle,        // S
        Reroll,            // Shift+R
        TutorialContinue,  // T
        NumberChoices,     // 1-9
        ReviewArrows,      // review-cursor movement in cursor-owning modes
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

            t[Mode.Pause] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel);
            t[Mode.Tutorial] = S(ModKey.Navigate, ModKey.Activate, ModKey.ReviewArrows, ModKey.TutorialContinue);
            t[Mode.ResponseMenu] = S(ModKey.Navigate, ModKey.Activate, ModKey.NumberChoices);
            t[Mode.Dialogue] = S(ModKey.Navigate, ModKey.Activate);
            t[Mode.DiceAllocation] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Reroll,
                ModKey.InventoryToggle, ModKey.CharacterToggle, ModKey.DriveLogToggle);
            t[Mode.CharacterWindow] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.ReviewArrows,
                ModKey.CharacterToggle);
            t[Mode.DriveLog] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.DriveLogToggle);
            t[Mode.Inventory] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.InventoryToggle);
            t[Mode.Cloud] = S(ModKey.Navigate, ModKey.Activate, ModKey.Clocks, ModKey.Reroll, ModKey.ScanToggle);
            t[Mode.ActionView] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Clocks, ModKey.Reroll,
                ModKey.InventoryToggle, ModKey.CharacterToggle, ModKey.DriveLogToggle, ModKey.ScanToggle);
            t[Mode.Station] = S(ModKey.Navigate, ModKey.Activate, ModKey.Cancel, ModKey.Clocks,
                ModKey.InventoryToggle, ModKey.CharacterToggle, ModKey.DriveLogToggle, ModKey.ScanToggle);
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
            (ModKey.InventoryToggle, "I: inventory"),
            (ModKey.CharacterToggle, "U: character window"),
            (ModKey.DriveLogToggle, "J: drive log"),
            (ModKey.ScanToggle, "S: scan"),
            (ModKey.Vitals, "V: vitals"),
            (ModKey.Dice, "D: dice"),
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
