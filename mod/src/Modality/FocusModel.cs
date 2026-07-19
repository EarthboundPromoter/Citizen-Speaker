using CSAccess.Game;
using CSAccess.Speech;
using CSAccess.UI;
using UnityEngine;
using Priority = CSAccess.Speech.Priority;

namespace CSAccess.Modality
{
    /// <summary>
    /// The per-surface focus table (W3, focus-model.md — every row owner-walked
    /// 2026-07-19). One place answers "where does the game intend focus in this
    /// mode, and how does it come back": the universal mode-aware re-anchor
    /// (H commitment 4), extending the empty-Enter Confirm-backstop mirror from
    /// station-only to every mode with a designed anchor.
    ///
    /// Channel law (binding): mod-owned review/index browsing never holds or moves
    /// EventSystem selection; a commit is exactly one designed native activation.
    /// Checker variants (brief E) bound what ReAnchor may select: variant-C anchors
    /// (dialogue continue, dice cursors) set themselves once and never restore —
    /// restoring them IS the designed recovery; variant-A watchdogs (pause RESUME,
    /// tutorial button) recover themselves and need nothing from us.
    /// </summary>
    internal static class FocusModel
    {
        /// <summary>Put focus back where the game intends it for this mode. Returns
        /// true if a designed recovery was performed (speech/logging included).</summary>
        public static bool ReAnchor(Mode mode)
        {
            switch (mode)
            {
                case Mode.Station:
                {
                    // The game's own Confirm backstop: UI Reselector -> UI selector Reset
                    // re-anchors to the nearest marker (E, live-verified W2).
                    var selector = GameQueries.FindFsm("UI selector");
                    if (selector == null) return false;
                    selector.SendEvent("Reset");
                    Plugin.Log.LogInfo("[Focus] ReAnchor(Station): UI selector Reset.");
                    return true;
                }

                case Mode.ActionView:
                {
                    // Inside an action view the designed recovery is the RefocusUI
                    // broadcast — the exact signal the picker fires on Back; slots'
                    // Idle states re-enter Focus on it (E). The UI selector Reset
                    // (previous mapping) anchors MARKERS — the wrong surface here
                    // (session-5 owner-caught).
                    PlayMakerFSM.BroadcastEvent("RefocusUI");
                    Plugin.Log.LogInfo("[Focus] ReAnchor(ActionView): RefocusUI broadcast.");
                    return true;
                }

                case Mode.Dialogue:
                {
                    // Continue Button is Checker variant C: sets itself once on state
                    // entry, never fights, never self-restores (E). If focus wandered,
                    // reselecting it IS the designed recovery (focus-model row 6 —
                    // the fix for "lost focus in dialogue, could not regain").
                    var btn = GameObject.Find(
                        "CS Dialogue Manager/Canvas/TMP CS Dialogue UI 1/Dialogue Panel/Main Panel/Continue Button");
                    if (btn == null || !btn.activeInHierarchy) return false;
                    Navigator.Select(btn);
                    Plugin.Log.LogInfo("[Focus] ReAnchor(Dialogue): Continue Button reselected.");
                    return true;
                }

                case Mode.Tutorial:
                {
                    var btn = GameObject.Find("Letterbox Canvas/Tutorial System/Button");
                    if (btn == null || !btn.activeInHierarchy) return false;
                    Navigator.Select(btn);
                    return true;
                }

                case Mode.DiceAllocation:
                {
                    // The picker's own anchor family: Dice Cursor N, variant C (E).
                    for (int i = 1; i <= 5; i++)
                    {
                        var cursor = GameObject.Find(
                            "Letterbox Canvas/Top UI/Dice UI/Dice Gamepad System/Dice Cursor " + i);
                        if (cursor != null && cursor.activeInHierarchy)
                        {
                            Navigator.Select(cursor);
                            Plugin.Log.LogInfo("[Focus] ReAnchor(DiceAllocation): Dice Cursor " + i + ".");
                            return true;
                        }
                    }
                    return false;
                }

                case Mode.CharacterWindow:
                {
                    // The window's confirmed delayed anchor (E): Upgrade Button.
                    var btn = GameObject.Find(
                        "Letterbox Canvas/Character Window/Upgrade Tracker/Top Line/Upgrade UI/Upgrade Button");
                    if (btn == null || !btn.activeInHierarchy) return false;
                    Navigator.Select(btn);
                    return true;
                }

                default:
                    // Variant-A contexts (Pause, Title menus) self-recover; listening
                    // modes have no anchor by design. Nothing to do is not a failure.
                    Plugin.Log.LogInfo("[Focus] ReAnchor(" + mode + "): no mod-side anchor (by design).");
                    return false;
            }
        }
    }
}
