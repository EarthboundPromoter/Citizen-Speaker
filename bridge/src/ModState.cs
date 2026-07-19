using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;

namespace CSAccessBridge
{
    /// <summary>/modstate: the CSAccess mod's own believed state (mode, window flags,
    /// selection, input ring, FSM ring, speech tail), read via reflection from
    /// CSAccess.Diag.Snapshot() — zero coupling added to the mod, and the bridge
    /// still works when the mod isn't installed. Must run on the main thread.</summary>
    internal static class ModState
    {
        private static MethodInfo _snapshot;

        public static object Read()
        {
            if (_snapshot == null)
            {
                var diag = AccessTools.TypeByName("CSAccess.Diag");
                if (diag != null)
                    _snapshot = AccessTools.Method(diag, "Snapshot");
            }
            if (_snapshot == null)
                return new Dictionary<string, object>
                {
                    ["error"] = "CSAccess mod not loaded (CSAccess.Diag.Snapshot not found)",
                };
            return _snapshot.Invoke(null, null);
        }
    }
}
