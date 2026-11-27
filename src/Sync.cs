// Envoi au site : chaque séance est gardée en fichier jusqu'à ce que le site l'ait reçue (POST
// /api/combos/plugin/session, server/combos.mjs). Un fichier que le site refuse est mis de côté avec la raison.
using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;

namespace EnduranceManager.SimHub
{
    public class LinkCode
    {
        public string Origin, Token;
        static readonly Regex Pattern = new Regex(@"EMCOMBO1 (https://[A-Za-z0-9.-]+(?::\d+)?) ([a-f0-9]{64}) EMCOMBO1");

        // The code the site gives on the fiches combo page: the site's address and the pilot's key.
        public static LinkCode Parse(string text)
        {
            var match = Pattern.Match(text ?? "");
            return match.Success ? new LinkCode { Origin = match.Groups[1].Value, Token = match.Groups[2].Value } : null;
        }

        public string QueueId
        {
            get
            {
                using (var sha = SHA256.Create())
                    return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(Origin + "\n" + Token))).Replace("-", "").ToLowerInvariant();
            }
        }

        public override string ToString() => $"EMCOMBO1 {Origin} {Token} EMCOMBO1";
    }

    // The site said no to the key itself: removed (401) or the pilot no longer has the right (403).
    public class LinkRefused : Exception
    {
        public bool Removed;
        public LinkRefused(string message, bool removed) : base(message) { Removed = removed; }
    }

    public class Uploader
    {
        readonly string home;
        readonly HttpClient client;
        string activeQueue;
        bool blockedToken;

        public string Folder => Path.Combine(home, "sessions");
        public string Status { get; private set; } = "En attente";
        // The key is refused: nothing is sent until a new code is pasted; the sessions wait.
        public string Refused { get; private set; }
        // For the settings screen: did the last pass reach the site, and what went with it.
        public bool? Reached { get; private set; }
        public DateTime? LastSent { get; private set; }
        public int SentToday { get; private set; }
        string QueueRoot => Path.Combine(Folder, "queues");
        string Queue(LinkCode code) => Path.Combine(QueueRoot, code?.QueueId ?? "unbound");
        static int Count(string folder) => Directory.Exists(folder) ? Directory.GetFiles(folder, "*.json").Length : 0;
        public int Waiting(LinkCode code) => code == null ? 0 : Count(Queue(code));
        public int Held(LinkCode code)
        {
            string current = code == null ? null : Queue(code);
            return Count(Folder) + Directory.GetDirectories(QueueRoot).Where(dir => dir != current).Sum(Count);
        }
        // The last session the site refused, and why: the file is kept in sessions\queues\…\refused instead of being lost.
        public string LastRefusal { get; private set; }
        DateTime sentDay = DateTime.Today;

        public Uploader(string home, HttpMessageHandler transport = null)
        {
            this.home = home;
            client = transport == null
                ? new HttpClient(new HttpClientHandler { AllowAutoRedirect = false })
                : new HttpClient(transport);
            client.Timeout = TimeSpan.FromSeconds(60);
            Directory.CreateDirectory(QueueRoot);
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls12;
        }

        // A new code: try again at once.
        public void Reset() { Refused = null; blockedToken = false; Reached = null; LastSent = null; SentToday = 0; LastRefusal = null; Status = "En attente"; }

        // Written aside then renamed, so a file is never read half written.
        public void Save(LiveSession session, LinkCode destination)
        {
            string queue = Queue(destination);
            Directory.CreateDirectory(queue);
            string path = Path.Combine(queue, session.At + "-" + Guid.NewGuid().ToString("N") + ".json");
            File.WriteAllText(path + ".tmp", JsonConvert.SerializeObject(session));
            File.Move(path + ".tmp", path);
        }

        // Legacy and unbound files have no known destination. Keep them on disk without sending them.
        public void SendAll(LinkCode code, Func<LinkCode> currentLink)
        {
            string queue = code == null ? null : Queue(code);
            if (queue != activeQueue) { Reset(); activeQueue = queue; }
            if (code == null) { Status = "Colle ton code de liaison dans l'écran du plugin."; return; }
            if (Refused != null && blockedToken) return;
            bool IsCurrent() => String.Equals(code.ToString(), currentLink()?.ToString(), StringComparison.Ordinal);
            try
            {
                if (sentDay != DateTime.Today) { sentDay = DateTime.Today; SentToday = 0; }
                if (!Directory.Exists(queue)) Directory.CreateDirectory(queue);
                foreach (var path in Directory.GetFiles(queue, "*.json").OrderBy(path => path, StringComparer.Ordinal))
                {
                    if (!IsCurrent()) return;
                    string refusal = Send(code, path);
                    if (refusal == null) { File.Delete(path); SentToday++; LastSent = DateTime.Now; continue; }
                    // Refused (4xx): kept aside with its reason, never sent again on its own.
                    string refused = Path.Combine(queue, "refused");
                    Directory.CreateDirectory(refused);
                    string target = Path.Combine(refused, Path.GetFileName(path));
                    File.Move(path, target);
                    File.WriteAllText(target + ".txt", refusal);
                    LastRefusal = "le " + DateTime.Now.ToString("dd/MM à HH:mm") + " (" + refusal + ")";
                }
                if (!IsCurrent()) return;
                Refused = null;
                Reached = true;
                Status = "Relié · " + DateTime.Now.ToString("HH:mm");
            }
            catch (LinkRefused error) { if (IsCurrent()) { Refused = error.Message; blockedToken = error.Removed; Reached = false; Status = error.Message; } }
            catch (Exception error) { if (IsCurrent()) { Reached = false; Status = "Envoi différé, nouvel essai dans une minute (" + error.Message + ")"; } }
        }

        static string Reason(HttpResponseMessage response)
        {
            try { return (string)Newtonsoft.Json.Linq.JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult())["error"]; }
            catch { return null; }
        }

        // null when the site has the session (new or already received), else why it refused it.
        string Send(LinkCode code, string path)
        {
            using (var request = new HttpRequestMessage(HttpMethod.Post, code.Origin + "/api/combos/plugin/session"))
            {
                request.Content = new ByteArrayContent(File.ReadAllBytes(path));
                request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/json");
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", code.Token);
                using (var response = client.SendAsync(request).GetAwaiter().GetResult())
                {
                    if (response.StatusCode == HttpStatusCode.Unauthorized)
                        throw new LinkRefused(Reason(response) ?? "Cette liaison a été retirée. Crée un nouveau code sur le site.", true);
                    if (response.StatusCode == HttpStatusCode.Forbidden)
                        throw new LinkRefused(Reason(response) ?? "Le site refuse cette liaison.", false);
                    if ((int)response.StatusCode >= 500 || response.StatusCode == (HttpStatusCode)429) throw new HttpRequestException("erreur " + (int)response.StatusCode);
                    if (response.StatusCode == HttpStatusCode.OK || response.StatusCode == HttpStatusCode.Created)
                    {
                        var receipt = Newtonsoft.Json.Linq.JObject.Parse(response.Content.ReadAsStringAsync().GetAwaiter().GetResult());
                        if (receipt["created"]?.Type != Newtonsoft.Json.Linq.JTokenType.Boolean)
                            throw new HttpRequestException("Accusé de réception absent.");
                        return null;
                    }
                    if (response.IsSuccessStatusCode) throw new HttpRequestException("Accusé de réception absent.");
                    string reason = Reason(response);
                    return "erreur " + (int)response.StatusCode + (string.IsNullOrEmpty(reason) ? "" : " : " + reason);
                }
            }
        }
    }
}
