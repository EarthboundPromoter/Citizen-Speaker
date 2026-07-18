using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace CSAccessBridge
{
    [BepInPlugin(Id, "Citizen Sleeper Access Bridge", "0.1.0")]
    public class Plugin : BaseUnityPlugin
    {
        public const string Id = "com.sleeperaccess.bridge";

        internal static ManualLogSource Log;
        private BridgeServer _server;

        private void Awake()
        {
            Log = Logger;
            Application.runInBackground = true;

            var harmony = new Harmony(Id);
            harmony.PatchAll(typeof(Plugin).Assembly);

            int port = Config.Bind("Bridge", "Port", 8330, "Localhost port for the debug bridge").Value;
            _server = new BridgeServer(port);
            _server.Start();
            Log.LogInfo($"Access bridge listening on http://127.0.0.1:{port}/");
        }

        private void Update()
        {
            MainThread.Drain();
        }

        private void OnDestroy()
        {
            _server?.Stop();
        }
    }
}
