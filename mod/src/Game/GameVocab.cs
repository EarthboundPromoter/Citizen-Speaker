namespace CSAccess.Game
{
    /// <summary>The game's vocabulary as data: skill words, class names, condition
    /// bands, the dice-odds table, and the registry of shipped-data warts. One
    /// authority for strings that used to live as parallel per-file copies (audit
    /// 2026-07-22 A3/A4/A10/C1/D). CS2 seam: the port re-keys THIS module's
    /// contents; consumers stay untouched. Per-surface spoken phrasing does NOT
    /// belong here — that stays in each surface's own W wording block, where owner
    /// calibration lands.</summary>
    internal static class GameVocab
    {
        /// <summary>Skill words in the game's DISPLAY order — the character-select
        /// carousel and the card renders use this sequence. NOT the Lua variable
        /// order below; zipping the two lists misattributes skills (the audit's A3
        /// latent hazard, now explicit at the definition).</summary>
        public static readonly string[] SkillDisplayOrder =
            { "ENGINEER", "INTERFACE", "ENDURE", "INTUIT", "ENGAGE" };

        /// <summary>Skill Lua variable names, ORDER-BOUND to LuaStore.Skill's enum
        /// values (Endure, Engage, Engineer, Interface, Intuit) — always indexed by
        /// (int)skill, never zipped with the display order.</summary>
        public static readonly string[] SkillLuaVars =
            { "ENDURE", "ENGAGE", "ENGINEER", "INTERFACE", "INTUIT" };

        /// <summary>Class names in carousel layout order — the spoken "class N of 3"
        /// ordinal derives from this order (live-observed layout).</summary>
        public static readonly string[] ClassNames =
            { "EXTRACTOR", "OPERATOR", "MACHINIST" };

        /// <summary>Condition bands as the Condition System's STATE names, ascending
        /// (worst first) — ConditionWatch derives falling/improving by index.</summary>
        public static readonly string[] ConditionBandStates =
            { "Dying", "Declining", "Fading", "Flickering", "Stable" };

        /// <summary>Condition bands as the Condition System's per-band STRING VARIABLE
        /// names — a DIFFERENT set from the state names ("Breaking", not "Dying", plus
        /// "Attempting Recovery"); exactly the current band's var is non-empty.</summary>
        public static readonly string[] ConditionWordVars =
            { "Stable", "Flickering", "Fading", "Declining", "Breaking", "Attempting Recovery" };

        /// <summary>A1: the slot-settle outlook as the game's own odds. The roll
        /// states are hard-coded per boosted die value, uniform across all 119
        /// controllers (corpus: RandomInt 1-4 + FloatSwitch): 1-2 = 50 neg / 50 neu,
        /// 3-4 = 25/50/25, 5 = 50 neu / 50 pos, 6 = 100 pos. The dice tutorial
        /// surfaces these numbers (owner ruling: game-stated, speakable); absent
        /// tiers are omitted. Data, not scattered literals (audit C1) — shipped
        /// built blind 2026-07-21, corroborated by the DICE tutorial's stated odds.</summary>
        public static string OutlookLine(float slottedValue)
        {
            if (slottedValue <= 0f) return null;
            if (slottedValue <= 2f) return "Negative 50 percent, neutral 50 percent.";
            if (slottedValue <= 4f) return "Negative 25 percent, neutral 50 percent, positive 25 percent.";
            if (slottedValue <= 5f) return "Neutral 50 percent, positive 50 percent.";
            return "Positive 100 percent.";
        }

        /// <summary>Shipped-data warts the mod matches VERBATIM (audit D). Each is a
        /// silent break point if the game ever patches its data, and a trap for the
        /// CS2 port if copied blind — one greppable registry instead of scattered
        /// literals. Do not "fix" the spellings; the game's own data is the truth.</summary>
        public static class Warts
        {
            /// <summary>Pause menu autosave line object — "SInce" is authored.</summary>
            public const string TimeSinceLastAutosave = "Time SInce Last Autosave";

            /// <summary>Character-window achieved-perk container — the trailing space
            /// is authored ("FG Markers /Perk N"); probe this first, the space-free
            /// variant as fallback.</summary>
            public const string FgMarkersTrailingSpace = "FG Markers ";

            /// <summary>Per-locale prompt positioner copies ("A Prompt postioner FR")
            /// — matched by the shared misspelled fragment, case-insensitive.</summary>
            public const string PositionerFragment = "ostioner";

            /// <summary>The authored localization default that leaks from unlocalized
            /// string variables; never rendered — speak nothing instead.</summary>
            public const string LocalizationLeakDescription = "Description in text table";

            // Documented-only warts, matched structurally or trimmed at their sites
            // (listed so they grep): "Repair Bid Click" — misspelled clock card,
            // absorbed by GameQueries' structural card typing; "Interface Breakdown "
            // — trailing space in the corpus state name, ConditionWatch trims before
            // lookup; "IsVisibleInCameraFrustrum" — the PlayMaker action's own name,
            // StationAtlas; "Greeenway" — authored item-name typo, surfacing ruling
            // pending (A2 faithful-vs-repair, Describe.ItemDemandName).
        }
    }
}
