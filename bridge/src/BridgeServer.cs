using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace CSAccessBridge
{
    /// <summary>Minimal single-threaded HTTP server over TcpListener (avoids HttpListener URL-ACL requirements).
    /// GET only, one request per connection.</summary>
    internal class BridgeServer
    {
        private readonly int _port;
        private TcpListener _listener;
        private Thread _thread;
        private volatile bool _running;
        private readonly string _shotDir;

        public BridgeServer(int port)
        {
            _port = port;
            _shotDir = Path.Combine(BepInEx.Paths.GameRootPath, "BepInEx", "bridge-shots");
        }

        public void Start()
        {
            _listener = new TcpListener(IPAddress.Loopback, _port);
            _listener.Start();
            _running = true;
            _thread = new Thread(Loop) { IsBackground = true, Name = "CSAccessBridge" };
            _thread.Start();
        }

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }

        private void Loop()
        {
            while (_running)
            {
                TcpClient client = null;
                try
                {
                    client = _listener.AcceptTcpClient();
                    client.ReceiveTimeout = 5000;
                    client.SendTimeout = 30000;
                    HandleClient(client);
                }
                catch (Exception e)
                {
                    if (_running) Plugin.Log.LogWarning("Bridge: " + e.Message);
                }
                finally
                {
                    try { client?.Close(); } catch { }
                }
            }
        }

        private void HandleClient(TcpClient client)
        {
            var stream = client.GetStream();
            var reader = new StreamReader(stream, Encoding.UTF8, false, 8192, leaveOpen: true);

            string requestLine = reader.ReadLine();
            if (string.IsNullOrEmpty(requestLine)) return;
            while (true)
            {
                string header = reader.ReadLine();
                if (string.IsNullOrEmpty(header)) break;
            }

            string body;
            int status = 200;
            try
            {
                var parts = requestLine.Split(' ');
                if (parts.Length < 2 || parts[0] != "GET")
                {
                    status = 405;
                    body = Json.Serialize(Err("GET only"));
                }
                else
                {
                    body = Route(parts[1]);
                }
            }
            catch (Exception e)
            {
                status = 500;
                body = Json.Serialize(Err(e.Message));
            }

            byte[] payload = Encoding.UTF8.GetBytes(body);
            string head = "HTTP/1.1 " + status + (status == 200 ? " OK" : " Error") + "\r\n" +
                          "Content-Type: application/json; charset=utf-8\r\n" +
                          "Content-Length: " + payload.Length + "\r\n" +
                          "Connection: close\r\n\r\n";
            byte[] headBytes = Encoding.ASCII.GetBytes(head);
            stream.Write(headBytes, 0, headBytes.Length);
            stream.Write(payload, 0, payload.Length);
            stream.Flush();
        }

        private static Dictionary<string, object> Err(string message)
        {
            return new Dictionary<string, object> { ["error"] = message };
        }

        private static Dictionary<string, string> ParseQuery(string url, out string path)
        {
            var query = new Dictionary<string, string>();
            int q = url.IndexOf('?');
            path = q < 0 ? url : url.Substring(0, q);
            if (q >= 0)
            {
                foreach (var pair in url.Substring(q + 1).Split('&'))
                {
                    if (pair.Length == 0) continue;
                    int eq = pair.IndexOf('=');
                    if (eq < 0)
                        query[Uri.UnescapeDataString(pair)] = "";
                    else
                        query[Uri.UnescapeDataString(pair.Substring(0, eq))] =
                            Uri.UnescapeDataString(pair.Substring(eq + 1).Replace('+', ' '));
                }
            }
            return query;
        }

        private string Route(string url)
        {
            var q = ParseQuery(url, out string path);
            string spec = q.TryGetValue("path", out var p) ? p : (q.TryGetValue("id", out var i) ? "@" + i : null);

            object result;
            switch (path)
            {
                case "/ping":
                    result = MainThread.Run<object>(() => new Dictionary<string, object>
                    {
                        ["ok"] = true,
                        ["product"] = UnityEngine.Application.productName,
                        ["scene"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name,
                        ["time"] = UnityEngine.Time.realtimeSinceStartup,
                    });
                    break;
                case "/scenes":
                    result = MainThread.Run(() => UiQuery.Scenes());
                    break;
                case "/hierarchy":
                {
                    int depth = q.TryGetValue("depth", out var d) ? int.Parse(d) : 3;
                    bool inactive = q.TryGetValue("inactive", out var ia) && ia != "0";
                    result = MainThread.Run(() => UiQuery.Hierarchy(UiQuery.Resolve(spec), depth, inactive));
                    break;
                }
                case "/texts":
                    result = MainThread.Run(() => UiQuery.Texts());
                    break;
                case "/fsms":
                {
                    string filter = q.TryGetValue("filter", out var f) ? f : null;
                    bool activeOnly = !q.TryGetValue("all", out var all) || all == "0";
                    result = MainThread.Run(() => UiQuery.Fsms(filter, activeOnly));
                    break;
                }
                case "/fsm":
                {
                    string name = q.TryGetValue("name", out var n) ? n : null;
                    result = MainThread.Run(() => UiQuery.FsmDetail(UiQuery.Resolve(spec), name));
                    break;
                }
                case "/selectables":
                    result = MainThread.Run(() => UiQuery.Selectables());
                    break;
                case "/selection":
                    result = MainThread.Run(() => UiQuery.Selection());
                    break;
                case "/find":
                    result = MainThread.Run(() => UiQuery.Find(q["q"]));
                    break;
                case "/ink":
                    result = MainThread.Run(() => UiQuery.InkState());
                    break;
                case "/select":
                    result = MainThread.Run(() => UiQuery.Select(UiQuery.Resolve(spec)));
                    break;
                case "/click":
                    result = MainThread.Run(() => UiQuery.Click(UiQuery.Resolve(spec)));
                    break;
                case "/hover":
                {
                    bool exit = q.TryGetValue("exit", out var ex) && ex != "0";
                    result = MainThread.Run(() => UiQuery.Hover(UiQuery.Resolve(spec), exit));
                    break;
                }
                case "/sendmessage":
                {
                    string msg = q["msg"];
                    string argSpec = q.TryGetValue("argpath", out var ap) ? ap : null;
                    result = MainThread.Run(() => UiQuery.SendMsg(
                        UiQuery.Resolve(spec), msg,
                        argSpec != null ? UiQuery.Resolve(argSpec) : null));
                    break;
                }
                case "/fsmevent":
                {
                    string name = q.TryGetValue("name", out var n) ? n : null;
                    result = MainThread.Run(() => UiQuery.SendFsmEvent(UiQuery.Resolve(spec), name, q["event"]));
                    break;
                }
                case "/broadcast":
                    result = MainThread.Run(() => UiQuery.Broadcast(q["event"]));
                    break;
                case "/modstate":
                    result = MainThread.Run(() => ModState.Read());
                    break;
                case "/watch":
                {
                    long since = q.TryGetValue("since", out var s) ? long.Parse(s) : 0;
                    int max = q.TryGetValue("max", out var m) ? int.Parse(m) : 500;
                    result = WatchLog.Query(since, max);
                    break;
                }
                case "/fsmcensus":
                {
                    string file = q.TryGetValue("file", out var cf) ? cf : null;
                    result = MainThread.Run(() => FsmCensus.Run(file));
                    break;
                }
                case "/screenshot":
                    result = MainThread.Run(() => UiQuery.Screenshot(_shotDir));
                    break;
                default:
                    result = Err("unknown endpoint: " + path);
                    break;
            }
            return Json.Serialize(result);
        }
    }
}
