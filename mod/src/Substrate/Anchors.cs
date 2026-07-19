using HutongGames.PlayMaker;
using UnityEngine;

namespace CSAccess.Substrate
{
    /// <summary>
    /// The game's own pointer registry: GameObject-typed PlayMaker globals the FSMs use
    /// to find "what's current". Reading these is the sanctioned replacement for
    /// hardcoded GameObject.Find paths (build-plan W1). All six names below are
    /// corpus-verified as $-referenced globals; a missing anchor returns null with a
    /// one-time log line (graceful silence, invariant 5).
    /// </summary>
    internal static class Anchors
    {
        /// <summary>The action panel currently engaged — written by every Location Button
        /// on click; the cloud's designed anchor (build-plan G#5).</summary>
        public static GameObject ActiveAction() => Get("ActiveAction");

        public static GameObject DialoguePanel() => Get("Dialogue Panel");

        public static GameObject ResponseMenu() => Get("Response Menu");

        public static GameObject LeaveButton() => Get("Leave Button");

        /// <summary>The save system — target of the character window's close-time Save event.</summary>
        public static GameObject Saver() => Get("Saver");

        public static GameObject UISelector() => Get("UI Selector");

        private static readonly System.Collections.Generic.HashSet<string> MissLogged =
            new System.Collections.Generic.HashSet<string>();

        /// <summary>Registry read. Private on purpose: feature code goes through the named,
        /// corpus-verified accessors above so every anchor in use is documented here.</summary>
        private static GameObject Get(string name)
        {
            var v = FsmVariables.GlobalVariables.GetFsmGameObject(name);
            var go = v != null ? v.Value : null;
            if (go == null && MissLogged.Add(name))
                Plugin.Log.LogInfo("[Substrate] Anchor '" + name + "' is "
                    + (v == null ? "not defined" : "currently null") + ".");
            return go;
        }
    }
}
