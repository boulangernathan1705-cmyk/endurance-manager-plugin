// Mise à jour du plugin, toute dans SimHub (22/11/2026, maquette validée par Nathan) : le site publie la dernière version
// (/downloads/simhub/v0/plugin.json : version, taille, empreinte, changements ; la DLL à côté). Une petite fenêtre
// (UpdateWindow.cs) l'annonce avec ses changements ; « Mettre à jour » télécharge la DLL, la vérifie, la met à la place
// de celle de SimHub (Windows laisse renommer une DLL chargée), puis SimHub se ferme et revient tout seul. Aucune fenêtre
// noire ; Windows demande les droits seulement si le dossier de SimHub l'exige. Jamais pendant que le pilote roule.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json.Linq;

namespace EnduranceManager.SimHub
{
    public class Updater
    {
        readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        readonly string folder;
        DateTime checkedAt = DateTime.MinValue;
        int busy, installing;

        public static readonly Version Current = typeof(Updater).Assembly.GetName().Version;
        // Without the site module, the public site gives the updates: SimHub alone keeps up to date too.
        public const string PublicSite = "https://endurance-manager.app";
        // The 0.x line (new numbering from 0.1.0, 22/11/2026) has its own folder on the site: /downloads/simhub/ still holds
        // the old 2.x plugin, which a 0.x would otherwise take for a newer version.
        public const string Channel = "/downloads/simhub/v0/";
        public const string DllName = "EnduranceManager.SimHub.dll";
        static string Origin(LinkCode code) => code?.Origin ?? PublicSite;
        // « 2.6.0 (abc1234) »: the version and the commit it was built from, one build told from another.
        public static string Build
        {
            get
            {
                string info = (typeof(Updater).Assembly.GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false) is object[] a && a.Length > 0
                    ? ((System.Reflection.AssemblyInformationalVersionAttribute)a[0]).InformationalVersion : null) ?? "";
                int plus = info.IndexOf('+');
                string commit = plus > 0 ? info.Substring(plus + 1) : "";
                return Current.ToString(3) + (commit.Length > 0 ? " (" + (commit.Length > 7 ? commit.Substring(0, 7) : commit) + ")" : "");
            }
        }

        // One line of « Ce qui change » (src/changements.json): nouveau, mieux or corrige; the page of the plugin's screen
        // where it is (index.html, PAGES), or null.
        public class Change
        {
            public string Type { get; set; }
            public string Text { get; set; }
            public string Page { get; set; }
        }

        // The latest version on the site, when it is newer than this one, and what it changes.
        public Version Available { get; private set; }
        public List<Change> Changes { get; private set; } = new List<Change>();
        // While « Mettre à jour » runs: "telechargement", then "redemarrage". Error: why it stopped (nothing was changed).
        public volatile string Phase;
        public volatile string Error;
        public volatile bool CheckSucceeded;
        public volatile string CheckError;

        public Updater(string home) { folder = Path.Combine(home, "update"); }

        // The pictures of the screen (tests/Preview): a version as if the site gave it.
        public void Pretend(Version version, List<Change> changes) { Available = version; Changes = changes; }

        // « 1.2.0 » on the site, 1.2.0.0 in the DLL.
        static Version Full(Version version) => new Version(version.Major, version.Minor, Math.Max(version.Build, 0), Math.Max(version.Revision, 0));

        public static List<Change> Read(JToken list) => (list as JArray ?? new JArray()).OfType<JObject>()
            .Select(c => new Change { Type = (string)c["type"] ?? "mieux", Text = (string)c["text"] ?? "", Page = (string)c["page"] })
            .Where(c => c.Text.Length > 0).ToList();

        // At most once an hour, aside: a slow site never holds up anything. Never throws.
        public void Check(LinkCode code, bool force = false)
        {
            if (!force && DateTime.Now - checkedAt < TimeSpan.FromHours(1)) return;
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            checkedAt = DateTime.Now;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    var info = JObject.Parse(client.GetStringAsync(Origin(code) + Channel + "plugin.json").GetAwaiter().GetResult());
                    if (!Version.TryParse((string)info["version"], out var latest))
                        throw new InvalidDataException("la version publiée est illisible");
                    bool newer = Full(latest) > Full(Current);
                    Changes = newer ? Read(info["changes"]) : new List<Change>();
                    Available = newer ? latest : null;
                    CheckError = null;
                    CheckSucceeded = true;
                }
                catch (Exception e)
                {
                    CheckSucceeded = false;
                    CheckError = "Vérification impossible : " + Plain(e) + ".";
                }
                finally { busy = 0; }
            });
        }

        // The version just installed and its changes, for the window « Tu as la version… » after SimHub came back.
        public (string Version, List<Change> Changes)? Installed()
        {
            try
            {
                string path = Path.Combine(folder, "changements.json");
                if (!File.Exists(path)) return null;
                var saved = JObject.Parse(File.ReadAllText(path));
                if ((string)saved["version"] != Current.ToString(3)) return null;
                return (Current.ToString(3), Read(saved["changes"]));
            }
            catch { return null; }
        }

        // The DLL put aside by the last update (renamed while SimHub ran): removed at the next start.
        public static void Tidy()
        {
            try { string old = typeof(Updater).Assembly.Location + ".old"; if (File.Exists(old)) File.Delete(old); } catch { }
        }

        // « Mettre à jour »: aside, never twice. quit closes SimHub (its settings saved), which then comes back by itself.
        public void Install(LinkCode code, Action quit)
        {
            if (Interlocked.Exchange(ref installing, 1) == 1) return;
            Error = null;
            Phase = "telechargement";
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    string origin = Origin(code);
                    var info = JObject.Parse(client.GetStringAsync(origin + Channel + "plugin.json").GetAwaiter().GetResult());
                    string version = (string)info["version"];
                    byte[] dll = client.GetByteArrayAsync(origin + Channel + DllName).GetAwaiter().GetResult();
                    string sha;
                    using (var hash = SHA256.Create()) sha = string.Concat(hash.ComputeHash(dll).Select(b => b.ToString("X2")));
                    if (dll.Length != ((long?)info["size"] ?? -1) || !string.Equals(sha, (string)info["sha256"], StringComparison.OrdinalIgnoreCase))
                        throw new InvalidDataException("le fichier reçu est abîmé, réessaie");
                    Directory.CreateDirectory(folder);
                    string fresh = Path.Combine(folder, DllName);
                    File.WriteAllBytes(fresh, dll);
                    // Read again after the restart, for the window « Tu as la version… ».
                    File.WriteAllText(Path.Combine(folder, "changements.json"), new JObject { ["version"] = version, ["changes"] = info["changes"] ?? new JArray() }.ToString(), new UTF8Encoding(false));

                    string target = typeof(Updater).Assembly.Location;
                    if (!Swap(fresh, target)) SwapAsAdmin(fresh, target);
                    if (new FileInfo(target).Length != dll.Length) throw new IOException("la copie dans le dossier de SimHub n'a pas pu se faire");

                    Phase = "redemarrage";
                    var simhub = Process.GetCurrentProcess();
                    // SimHub can stay alive in the background after its windows close (Nathan, 0.1.4): after 30 s the
                    // process is ended, then started again.
                    Hidden(Script(
                        $"$p = Get-Process -Id {simhub.Id} -ErrorAction SilentlyContinue",
                        "if ($p) { if (-not $p.WaitForExit(30000)) { Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue; $p.WaitForExit(15000) | Out-Null } }",
                        "Start-Sleep -Seconds 2",
                        $"Start-Process -FilePath {Quote(simhub.MainModule.FileName)} -WorkingDirectory {Quote(Path.GetDirectoryName(simhub.MainModule.FileName))}"), admin: false);
                    Thread.Sleep(600);
                    quit();
                }
                catch (Exception e)
                {
                    Report.Error("Mise à jour", e);
                    Error = "La mise à jour n'a pas marché (" + Plain(e) + "). Rien n'a changé.";
                    Phase = null;
                    installing = 0;
                }
            });
        }

        // The DLL SimHub runs is renamed (allowed while loaded) and the new one put in its place. false: no rights.
        static bool Swap(string fresh, string target)
        {
            string old = target + ".old";
            try
            {
                if (File.Exists(old)) File.Delete(old);
                File.Move(target, old);
            }
            catch (UnauthorizedAccessException) { return false; }
            catch (IOException) { return false; }
            try { File.Copy(fresh, target, true); return true; }
            catch
            {
                try { File.Move(old, target); } catch { }
                return false;
            }
        }

        // The same, by Windows' administrator prompt (SimHub's folder under Program Files). Waits for it.
        static void SwapAsAdmin(string fresh, string target)
        {
            string old = target + ".old";
            var process = Hidden(Script(
                $"Remove-Item -LiteralPath {Quote(old)} -Force -ErrorAction SilentlyContinue",
                $"Move-Item -LiteralPath {Quote(target)} -Destination {Quote(old)} -Force",
                $"try {{ Copy-Item -LiteralPath {Quote(fresh)} -Destination {Quote(target)} -Force }} catch {{ Move-Item -LiteralPath {Quote(old)} -Destination {Quote(target)} -Force }}"), admin: true);
            if (process == null || !process.WaitForExit(60000)) throw new IOException("Windows n'a pas donné les droits");
        }

        static string Script(params string[] lines) => string.Join("; ", lines);
        static string Quote(string text) => "'" + text.Replace("'", "''") + "'";

        // PowerShell without a window. As administrator: Windows asks first (UseShellExecute is needed for it).
        static Process Hidden(string script, bool admin)
        {
            string encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(script));
            var start = new ProcessStartInfo("powershell.exe", "-NoProfile -NonInteractive -ExecutionPolicy Bypass -WindowStyle Hidden -EncodedCommand " + encoded);
            if (admin) { start.UseShellExecute = true; start.Verb = "runas"; start.WindowStyle = ProcessWindowStyle.Hidden; }
            else { start.UseShellExecute = false; start.CreateNoWindow = true; }
            try { return Process.Start(start); }
            catch (System.ComponentModel.Win32Exception) when (admin) { return null; }
        }

        static string Plain(Exception e)
        {
            var inner = e is AggregateException a ? a.InnerException ?? e : e;
            if (inner is HttpRequestException || inner is System.Threading.Tasks.TaskCanceledException) return "le site ne répond pas, vérifie ta connexion";
            return inner.Message.TrimEnd('.');
        }
    }
}
