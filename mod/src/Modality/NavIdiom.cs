namespace CSAccess.Modality
{
    /// <summary>
    /// D3 (owner ruling 2026-07-20): table navigation is the permanent idiom for
    /// station and cloud — no open/close state, no N. Ctrl+X is the deliberately
    /// buried escape hatch: it reverts BOTH surfaces to fully game-native navigation
    /// (the pre-table mod functions — native arrow graph through the focus fence,
    /// Space describes focus, focus announcements unmuted) and toggles back.
    /// Session-only by design; the location table is not gated by this.
    /// </summary>
    internal static class NavIdiom
    {
        public static bool Native;
    }
}
