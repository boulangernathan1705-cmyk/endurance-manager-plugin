// Le mur des stands en fenêtre (22/11/2026) : le plugin sert la page du mur (src/mur/, rangée dans la DLL) sur ce PC et
// sur le wifi, port 8686. « Ouvrir le mur » l'ouvre dans une fenêtre d'Edge sans barre (mode application), à la place
// et à la taille où le pilote l'a laissée ; une tablette ou un téléphone sur le même réseau ouvre la même adresse.
// La page lit /vue quatre fois par seconde : le mur fait par Wall.cs, en JSON. Rien ne part sur Internet.
// Un petit serveur à la main (TcpListener) : HttpListener demanderait les droits d'administrateur pour le wifi.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Reflection;
using System.Text;
using System.Threading;
using Newtonsoft.Json;

namespace EnduranceManager.SimHub
{
    public class WallServer
    {
        public const int FirstPort = 8686;
        readonly Func<object> view;
        readonly Action<int, int, int, int> placed;
        TcpListener listener;
        Thread thread;
        volatile bool running;
        public int Port { get; private set; }
        public string Error { get; private set; }

        // view: what /vue answers (the wall and the active profile); placed: the window's place and size, from this PC.
        public WallServer(Func<object> view, Action<int, int, int, int> placed)
        {
            this.view = view;
            this.placed = placed;
        }

        public bool Running => running;

        // On the network (the tablet), else on this PC alone; the next port when one is taken.
        public void Start()
        {
            if (running) return;
            for (int port = FirstPort; port < FirstPort + 5 && listener == null; port++)
                foreach (var address in new[] { IPAddress.Any, IPAddress.Loopback })
                    try { var l = new TcpListener(address, port); l.Start(); listener = l; Port = port; break; }
                    catch (Exception e) { Error = e.Message; }
            if (listener == null) return;
            Error = null;
            running = true;
            thread = new Thread(Loop) { IsBackground = true, Name = "EnduranceManager.Mur" };
            thread.Start();
        }

        public void Stop()
        {
            running = false;
            try { listener?.Stop(); } catch { }
            listener = null;
        }

        void Loop()
        {
            while (running)
            {
                TcpClient client;
                try { client = listener.AcceptTcpClient(); } catch { if (!running) return; Thread.Sleep(200); continue; }
                ThreadPool.QueueUserWorkItem(_ => Serve(client));
            }
        }

        void Serve(TcpClient client)
        {
            using (client)
            {
                try
                {
                    client.ReceiveTimeout = 3000; client.SendTimeout = 3000;
                    var stream = client.GetStream();
                    string request = ReadHead(stream);
                    var parts = request.Split(' ');
                    if (parts.Length < 2 || parts[0] != "GET") { Send(stream, 405, "text/plain", Encoding.UTF8.GetBytes("GET seulement")); return; }
                    string target = parts[1], path = target.Split('?')[0].TrimStart('/'), query = target.Contains("?") ? target.Substring(target.IndexOf('?') + 1) : "";
                    bool local = client.Client.RemoteEndPoint is IPEndPoint remote && IPAddress.IsLoopback(remote.Address);
                    if (path == "vue")
                        Send(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(view())));
                    else if (path == "place")
                    {
                        var q = Query(query);
                        if (local && q.TryGetValue("x", out int x) && q.TryGetValue("y", out int y) && q.TryGetValue("w", out int w) && q.TryGetValue("h", out int h) && w > 200 && h > 150)
                            placed(x, y, w, h);
                        Send(stream, 204, "text/plain", new byte[0]);
                    }
                    else if (Files.TryGetValue(path == "" ? "index.html" : path, out var file))
                        Send(stream, 200, Type(path == "" ? "index.html" : path), file);
                    else Send(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Introuvable"));
                }
                catch { }
            }
        }

        static string ReadHead(NetworkStream stream)
        {
            var bytes = new List<byte>();
            int b;
            while (bytes.Count < 8192 && (b = stream.ReadByte()) >= 0)
            {
                bytes.Add((byte)b);
                int n = bytes.Count;
                if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n') break;
            }
            string head = Encoding.ASCII.GetString(bytes.ToArray());
            int end = head.IndexOf("\r\n", StringComparison.Ordinal);
            return end < 0 ? head : head.Substring(0, end);
        }

        static Dictionary<string, int> Query(string query)
        {
            var values = new Dictionary<string, int>();
            foreach (var pair in query.Split('&'))
            {
                var kv = pair.Split('=');
                if (kv.Length == 2 && int.TryParse(kv[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out int value)) values[kv[0]] = value;
            }
            return values;
        }

        static void Send(Stream stream, int code, string type, byte[] body)
        {
            string status = code == 200 ? "OK" : code == 204 ? "No Content" : code == 404 ? "Not Found" : "Method Not Allowed";
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
        }

        static string Type(string path) =>
            path.EndsWith(".html") ? "text/html; charset=utf-8" : path.EndsWith(".css") ? "text/css; charset=utf-8" : path.EndsWith(".woff2") ? "font/woff2" : "application/octet-stream";

        // The page and its files, kept in the DLL (EmbeddedResource mur/…).
        static readonly Dictionary<string, byte[]> Files = Load();
        static Dictionary<string, byte[]> Load()
        {
            var files = new Dictionary<string, byte[]>();
            var assembly = typeof(WallServer).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                string key = name.Replace('\\', '/');
                if (!key.StartsWith("mur/")) continue;
                using (var stream = assembly.GetManifestResourceStream(name))
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); files[key.Substring(4)] = memory.ToArray(); }
            }
            return files;
        }

        // ── Addresses ───────────────────────────────────────────────────────────────────────────────────────────────
        public string LocalUrl => "http://localhost:" + Port + "/";

        // The address a tablet on the same network types: this PC's address on its wifi or cable.
        public string NetworkUrl
        {
            get
            {
                try
                {
                    var address = NetworkInterface.GetAllNetworkInterfaces()
                        .Where(n => n.OperationalStatus == OperationalStatus.Up && n.NetworkInterfaceType != NetworkInterfaceType.Loopback
                            && (n.NetworkInterfaceType == NetworkInterfaceType.Wireless80211 || n.NetworkInterfaceType == NetworkInterfaceType.Ethernet)
                            && n.GetIPProperties().GatewayAddresses.Any())
                        .SelectMany(n => n.GetIPProperties().UnicastAddresses)
                        .Select(a => a.Address)
                        .FirstOrDefault(a => a.AddressFamily == AddressFamily.InterNetwork && !IPAddress.IsLoopback(a));
                    return address == null ? null : "http://" + address + ":" + Port + "/";
                }
                catch { return null; }
            }
        }

        // ── The window ──────────────────────────────────────────────────────────────────────────────────────────────
        // Edge (else Chrome) in application mode: no tabs, no address bar, its own profile so the pilot's browser is not
        // touched. Placed where it was last; full screen on request. Neither found: the default browser.
        public void Open(string profileFolder, int? x, int? y, int? w, int? h, bool fullScreen)
        {
            if (!running) Start();
            if (!running) return;
            string browser = Find("msedge.exe") ?? Find("chrome.exe");
            if (browser == null) { Process.Start(new ProcessStartInfo(LocalUrl) { UseShellExecute = true }); return; }
            var args = new StringBuilder();
            args.Append("--app=").Append(LocalUrl);
            args.Append(" --user-data-dir=\"").Append(Path.Combine(profileFolder, "fenetre-mur")).Append('"');
            args.Append(" --no-first-run --no-default-browser-check");
            if (x.HasValue && y.HasValue) args.Append(" --window-position=").Append(x).Append(',').Append(y);
            if (w.HasValue && h.HasValue) args.Append(" --window-size=").Append(w).Append(',').Append(h);
            if (fullScreen) args.Append(" --start-fullscreen");
            Process.Start(new ProcessStartInfo(browser, args.ToString()) { UseShellExecute = false });
        }

        // A browser's path from Windows' « App Paths », else its usual folders.
        static string Find(string exe)
        {
            foreach (var root in new[] { Microsoft.Win32.Registry.CurrentUser, Microsoft.Win32.Registry.LocalMachine })
                try
                {
                    using (var key = root.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\App Paths\" + exe))
                        if (key?.GetValue("") is string path && File.Exists(path.Trim('"'))) return path.Trim('"');
                }
                catch { }
            string sub = exe == "msedge.exe" ? @"Microsoft\Edge\Application\msedge.exe" : @"Google\Chrome\Application\chrome.exe";
            foreach (var folder in new[] { Environment.SpecialFolder.ProgramFilesX86, Environment.SpecialFolder.ProgramFiles, Environment.SpecialFolder.LocalApplicationData })
            {
                string path = Path.Combine(Environment.GetFolderPath(folder), sub);
                if (File.Exists(path)) return path;
            }
            return null;
        }
    }
}
