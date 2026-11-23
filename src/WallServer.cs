// Le mur des stands en fenêtre (22/11/2026) : le plugin sert la page du mur (src/mur/, rangée dans la DLL) sur ce PC et
// sur le wifi, port 8686. « Ouvrir le mur » l'ouvre dans une fenêtre d'Edge sans barre (mode application), à la place
// et à la taille où le pilote l'a laissée ; une tablette ou un téléphone sur le même réseau ouvre la même adresse.
// La page lit /vue quatre fois par seconde : le mur fait par Wall.cs, en JSON. Rien ne part sur Internet.
// Un petit serveur à la main (TcpListener) : HttpListener demanderait les droits d'administrateur pour le wifi.
// Il sert aussi l'écran du plugin (src/ecran/, ScreenApi.cs) : /ecran/, /etat et /action, sur ce PC seulement. Il tourne
// toujours sur ce PC pour l'écran, et s'ouvre au wifi seulement quand le module Mur des stands est allumé.
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
        readonly Func<object> state;
        readonly Func<string, object> act;
        TcpListener listener;
        public bool Network { get; private set; }
        Thread thread;
        volatile bool running;
        public int Port { get; private set; }
        public string Error { get; private set; }

        // view: what /vue answers (the wall and the active profile); placed: the window's place and size, from this PC.
        // state, act: the plugin's screen (ScreenApi), on this PC only.
        public WallServer(Func<object> view, Action<int, int, int, int> placed, Func<object> state = null, Func<string, object> act = null)
        {
            this.view = view;
            this.placed = placed;
            this.state = state;
            this.act = act;
        }

        public bool Running => running;

        // network: also on the wifi (the tablet), else on this PC alone. The same port as before when free (the screen's
        // page keeps its address), else the next one.
        public void Start(bool network = true)
        {
            if (running && network == Network) return;
            if (running) Stop();
            var ports = new List<int>();
            if (Port != 0) ports.Add(Port);
            for (int port = FirstPort; port < FirstPort + 5; port++) if (!ports.Contains(port)) ports.Add(port);
            foreach (int port in ports)
            {
                foreach (var address in network ? new[] { IPAddress.Any, IPAddress.Loopback } : new[] { IPAddress.Loopback })
                    try { var l = new TcpListener(address, port); l.Start(); listener = l; Port = port; Network = address.Equals(IPAddress.Any); break; }
                    catch (Exception e) { Error = e.Message; }
                if (listener != null) break;
            }
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
                    var head = ReadHead(stream);
                    var parts = head.Count > 0 ? head[0].Split(' ') : new string[0];
                    if (parts.Length < 2 || (parts[0] != "GET" && parts[0] != "POST")) { Send(stream, 405, "text/plain", Encoding.UTF8.GetBytes("GET ou POST")); return; }
                    string target = parts[1], path = target.Split('?')[0].TrimStart('/'), query = target.Contains("?") ? target.Substring(target.IndexOf('?') + 1) : "";
                    bool local = client.Client.RemoteEndPoint is IPEndPoint remote && IPAddress.IsLoopback(remote.Address);
                    // The screen: this PC only, asked by its own page (a header no other site can send without asking first),
                    // with a Host of this PC (no page of the web renamed to point here).
                    string host = Header(head, "host").ToLowerInvariant();
                    bool screen = local && Header(head, "x-em") == "1" && (host.StartsWith("localhost:") || host.StartsWith("127.0.0.1:"));
                    if (path == "etat" || path == "action")
                    {
                        if (!screen || state == null) { Send(stream, 403, "text/plain", Encoding.UTF8.GetBytes("Interdit")); return; }
                        object answer;
                        if (path == "etat") answer = state();
                        else
                        {
                            if (parts[0] != "POST") { Send(stream, 405, "text/plain", Encoding.UTF8.GetBytes("POST")); return; }
                            int.TryParse(Header(head, "content-length"), out int length);
                            answer = act(ReadBody(stream, Math.Max(0, Math.Min(length, 65536))));
                        }
                        Send(stream, 200, "application/json; charset=utf-8", Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(answer)));
                        return;
                    }
                    if (parts[0] != "GET") { Send(stream, 405, "text/plain", Encoding.UTF8.GetBytes("GET")); return; }
                    if (path == "ecran") path = "ecran/";
                    if (path.StartsWith("ecran/"))
                    {
                        if (!local) { Send(stream, 403, "text/plain", Encoding.UTF8.GetBytes("Interdit")); return; }
                        if (path == "ecran/") path = "ecran/index.html";
                        if (Files.TryGetValue(path, out var page)) Send(stream, 200, Type(path), page);
                        else Send(stream, 404, "text/plain", Encoding.UTF8.GetBytes("Introuvable"));
                        return;
                    }
                    // The mur: on the wifi only while its module is on (the server stays up on this PC for the screen).
                    if (!local && !Network) { Send(stream, 403, "text/plain", Encoding.UTF8.GetBytes("Interdit")); return; }
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

        // The request's lines up to the blank one: the request line, then the headers.
        static List<string> ReadHead(NetworkStream stream)
        {
            var bytes = new List<byte>();
            int b;
            while (bytes.Count < 16384 && (b = stream.ReadByte()) >= 0)
            {
                bytes.Add((byte)b);
                int n = bytes.Count;
                if (n >= 4 && bytes[n - 4] == '\r' && bytes[n - 3] == '\n' && bytes[n - 2] == '\r' && bytes[n - 1] == '\n') break;
            }
            return Encoding.ASCII.GetString(bytes.ToArray()).Split(new[] { "\r\n" }, StringSplitOptions.RemoveEmptyEntries).ToList();
        }

        static string Header(List<string> head, string name)
        {
            foreach (var line in head.Skip(1))
            {
                int colon = line.IndexOf(':');
                if (colon > 0 && line.Substring(0, colon).Trim().Equals(name, StringComparison.OrdinalIgnoreCase)) return line.Substring(colon + 1).Trim();
            }
            return "";
        }

        static string ReadBody(NetworkStream stream, int length)
        {
            var body = new byte[length];
            int read = 0;
            while (read < length) { int n = stream.Read(body, read, length - read); if (n <= 0) break; read += n; }
            return Encoding.UTF8.GetString(body, 0, read);
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
            string status = code == 200 ? "OK" : code == 204 ? "No Content" : code == 403 ? "Forbidden" : code == 404 ? "Not Found" : "Method Not Allowed";
            var head = Encoding.ASCII.GetBytes($"HTTP/1.1 {code} {status}\r\nContent-Type: {type}\r\nContent-Length: {body.Length}\r\nCache-Control: no-store\r\nConnection: close\r\n\r\n");
            stream.Write(head, 0, head.Length);
            stream.Write(body, 0, body.Length);
        }

        static string Type(string path) =>
            path.EndsWith(".html") ? "text/html; charset=utf-8" : path.EndsWith(".css") ? "text/css; charset=utf-8" : path.EndsWith(".js") ? "text/javascript; charset=utf-8"
            : path.EndsWith(".woff2") ? "font/woff2" : path.EndsWith(".svg") ? "image/svg+xml" : path.EndsWith(".jpg") ? "image/jpeg" : path.EndsWith(".png") ? "image/png" : "application/octet-stream";

        // The pages and their files, kept in the DLL (EmbeddedResource mur/… at the root, ecran/… under /ecran/).
        static readonly Dictionary<string, byte[]> Files = Load();
        static Dictionary<string, byte[]> Load()
        {
            var files = new Dictionary<string, byte[]>();
            var assembly = typeof(WallServer).Assembly;
            foreach (var name in assembly.GetManifestResourceNames())
            {
                string key = name.Replace('\\', '/');
                if (!key.StartsWith("mur/") && !key.StartsWith("ecran/")) continue;
                using (var stream = assembly.GetManifestResourceStream(name))
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); files[key.StartsWith("mur/") ? key.Substring(4) : key] = memory.ToArray(); }
            }
            return files;
        }

        // ── Addresses ───────────────────────────────────────────────────────────────────────────────────────────────
        public string LocalUrl => "http://localhost:" + Port + "/";
        public string ScreenUrl => "http://localhost:" + Port + "/ecran/";

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
            if (!running) Start(Network);
            if (!running) return;
            string browser = Browser();
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

        // Edge, else Chrome, else null (also for the pictures of the screen, tests/Preview).
        public static string Browser() => Find("msedge.exe") ?? Find("chrome.exe");

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
