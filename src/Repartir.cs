// « Repartir de zéro » (menu « Mes boutons », maquette validée par Nathan le 24/11/2026) : les mêmes commandes chez tous
// les pilotes, recréées proprement.
// 1. Sauvegarder : les fichiers de touches de LMU (UserData\player), les liaisons du Control Mapper et ce que FMT companion
//    avait préparé, dans %LOCALAPPDATA%\EnduranceManager\Sauvegardes boutons\<date>.
// 2. Effacer : dans LMU (manettes, colonnes Principal et Secondaire), toutes les touches sauf la direction, l'accélérateur,
//    le frein, l'embrayage, les palettes et tout ce qui est sur un axe ; dans le Control Mapper, toutes les liaisons, sauf
//    celles qui mènent à une touche gardée dans LMU (palettes passées par le Control Mapper, par exemple).
// 3. Recréer : « Préparer LMU » de FMT companion, avec les commandes cochées dans Black box sur boutons.
// 4. Le pilote relie ensuite lui-même ses boutons, son rotary et son Fn dans le Control Mapper (Nathan : le module n'y
//    touche pas). Le clavier de LMU n'est pas touché.
// « Restaurer » remet la dernière sauvegarde : LMU doit être fermé (il réécrit ses fichiers en quittant).
// LMU efface une touche par POST rest/options/unsetConfigControl { Category 2, Control, Slot } : Slot 1 = Secondaire
// (celui de FMT companion), Slot 0 = Principal (supposé : vérifié après coup, ce qui reste est signalé).
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnduranceManager.SimHub
{
    public static class Repartir
    {
        const string PluginType = "SimHub.Plugins.OutputPlugins.ControlRemapper.ControlMapperPlugin";
        static readonly string[] LmuFiles = { "direct input.json", "keyboard.json", "current controls.json" };
        public static string Root => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnduranceManager", "Sauvegardes boutons");

        static readonly object gate = new object();
        static bool busy;
        static string status;
        static bool waitPrepare;

        // For the screen: running, what it says, the last backup's date.
        public static object State(EnduranceManagerPlugin plugin)
        {
            string text; bool running;
            lock (gate) { text = status; running = busy; }
            var page = plugin.CompanionPage;
            if (waitPrepare && page != null)
            {
                var line = page.PrepareSummary;
                if (page.Preparing) { running = true; text = "Recréer les commandes : " + line; }
                else if (!running) { waitPrepare = false; lock (gate) status = text = "Terminé. " + line + " Relie maintenant tes boutons dans le Control Mapper."; }
            }
            return new { busy = running, status = text, backup = Last()?.Name };
        }

        static DirectoryInfo Last()
        {
            try { return Directory.Exists(Root) ? new DirectoryInfo(Root).GetDirectories().OrderByDescending(d => d.Name).FirstOrDefault() : null; } catch { return null; }
        }

        static void Say(string text) { lock (gate) status = text; Report.Log("repartir", text); }

        // ── Repartir de zéro ────────────────────────────────────────────────────────────────────────────────────────
        public static object Zero(EnduranceManagerPlugin plugin)
        {
            lock (gate) { if (busy) return new { ok = false, message = "Déjà en cours." }; busy = true; status = "Sauvegarder…"; }
            waitPrepare = false;
            Task.Run(async () =>
            {
                try { await Run(plugin); }
                catch (Exception e) { Say("Arrêté : " + (e.InnerException ?? e).Message + " Rien d'autre n'a été changé ; « Restaurer » remet la sauvegarde."); }
                finally { lock (gate) busy = false; }
            });
            return new { ok = true, message = (string)null };
        }

        static async Task Run(EnduranceManagerPlugin plugin)
        {
            if (!plugin.Companion.Running) { Say("Le moteur des touches (FMT) n'est pas lancé : redémarre SimHub, puis recommence."); return; }
            string player = LmuPlayer();
            if (player == null) { Say("Lance LMU (menu principal), puis recommence."); return; }
            using (var lmu = new HttpClient { BaseAddress = new Uri("http://127.0.0.1:6397/"), Timeout = TimeSpan.FromSeconds(3) })
            {
                var di = DirectInput(await Get(lmu, "rest/options/UIScreen/Controls"));
                var phase = (int?)(await Get(lmu, "rest/options/assign/changestatus"))?["Status"] ?? 0;
                if (phase == 1 || phase == 2) { Say("LMU est en train d'affecter une touche : termine ou annule dans LMU, puis recommence."); return; }

                // 1. Sauvegarder.
                string folder = Path.Combine(Root, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
                Directory.CreateDirectory(folder);
                foreach (var name in LmuFiles)
                    if (File.Exists(Path.Combine(player, name))) File.Copy(Path.Combine(player, name), Path.Combine(folder, name), true);
                bool bridge = false;
                OnScreen(() =>
                {
                    var settings = MapperSettings(plugin) ?? throw new InvalidOperationException("Control Mapper absent.");
                    bridge = Nettoyage.Member(settings, "OutputMode")?.ToString() == "Bridge";
                    var snapshot = new JObject { ["lmu"] = player, ["links"] = Links(settings), ["outputs"] = Outputs(settings) };
                    var fmt = Nettoyage.Member(plugin.Companion.Instance, "Settings");
                    snapshot["prepared"] = JToken.FromObject(Nettoyage.Member(fmt, "Prepared") ?? new object[0]);
                    snapshot["reused"] = JToken.FromObject(Nettoyage.Member(fmt, "Reused") ?? new object());
                    File.WriteAllText(Path.Combine(folder, "control-mapper.json"), snapshot.ToString(Formatting.Indented));
                });

                // 2. Effacer dans LMU.
                Say("Effacer les touches dans LMU…");
                var keptButtons = new HashSet<int>();
                var wipe = new List<(string Control, int Slot)>();
                foreach (var (column, slot) in new[] { ("Input", 0), ("Alternative Input", 1) })
                    foreach (var p in ((JObject)di[column]).Properties())
                    {
                        if (!(p.Value is JObject b) || b["device"] == null || b["id"]?.Type != JTokenType.Integer) continue;
                        string device = (string)b["device"], input = InputName(di, device, (int)b["id"]);
                        if (Keep(p.Name, input))
                        {
                            if (Output(device, bridge) && Button(input) is int n) keptButtons.Add(n);
                            continue;
                        }
                        wipe.Add((p.Name, slot));
                    }
                foreach (var (control, slot) in wipe)
                {
                    var body = new JObject { ["Category"] = 2, ["Control"] = control, ["Slot"] = slot }.ToString(Formatting.None);
                    using (var r = await lmu.PostAsync("rest/options/unsetConfigControl", new StringContent(body, Encoding.UTF8, "application/json"))) r.EnsureSuccessStatusCode();
                    await Task.Delay(40);
                }
                di = DirectInput(await Get(lmu, "rest/options/UIScreen/Controls"));
                var left = wipe.Where(w => di[w.Slot == 0 ? "Input" : "Alternative Input"]?[w.Control] is JObject b && b["device"] != null).Select(w => w.Control).Distinct().ToList();

                // 2. Effacer dans le Control Mapper (sauf ce qui mène à une touche gardée) ; FMT companion oublie ce qu'il avait.
                Say("Effacer les liaisons du Control Mapper…");
                int unlinked = 0;
                OnScreen(() =>
                {
                    var settings = MapperSettings(plugin);
                    var output = Nettoyage.Member(Nettoyage.Member(settings, "OutputMapping"), "ControllerMapping");
                    var keptRoles = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var map in Items(Nettoyage.Member(output, "Buttons")))
                    {
                        var role = Nettoyage.Member(map, "TargetRole") as string;
                        if (string.IsNullOrEmpty(role)) continue;
                        if (keptButtons.Contains(Convert.ToInt32(Nettoyage.Member(map, "ButtonId")) + 1)) { keptRoles.Add(role); continue; }
                        Nettoyage.Set(map, "TargetRole", null); unlinked++;
                    }
                    foreach (var source in Items(Nettoyage.Member(settings, "ControllerMappings")))
                    {
                        var map = Nettoyage.Member(source, "ControllerMapping");
                        foreach (var list in new[] { "Buttons", "POVDirections", "AxisToButtons" })
                            foreach (var item in Items(Nettoyage.Member(map, list)))
                                if (Nettoyage.Member(item, "TargetRole") is string role && role != "" && !keptRoles.Contains(role)) { Nettoyage.Set(item, "TargetRole", null); unlinked++; }
                    }
                    Mapper(plugin)?.GetType().GetMethod("SaveSettings", Type.EmptyTypes)?.Invoke(Mapper(plugin), null);
                    var fmt = Nettoyage.Member(plugin.Companion.Instance, "Settings");
                    (Nettoyage.Member(fmt, "Prepared") as IList)?.Clear();
                    (Nettoyage.Member(fmt, "Reused") as IDictionary)?.Clear();
                    SaveFmt(plugin);
                });
                Report.Log("repartir", wipe.Count + " touches LMU effacées (" + left.Count + " restées), " + unlinked + " liaisons Control Mapper retirées, " + keptButtons.Count + " sorties gardées");
                if (left.Count > 0)
                {
                    Say("LMU n'a pas effacé " + left.Count + " touche(s) (" + string.Join(", ", left.Take(5)) + (left.Count > 5 ? "…" : "") + ") : rien n'a été recréé. Envoie le journal à Claude ; « Restaurer » remet tout comme avant.");
                    return;
                }

                // 3. Recréer : « Préparer LMU » de FMT companion.
                Say("Recréer les commandes… ne touche pas au volant.");
                waitPrepare = true;
                plugin.CompanionPage.Act("bbPrepare", null, false);
                await Task.Delay(500);
            }
        }

        // ── Restaurer ───────────────────────────────────────────────────────────────────────────────────────────────
        public static object Restore(EnduranceManagerPlugin plugin)
        {
            lock (gate) { if (busy) return new { ok = false, message = "Attends la fin de l'opération en cours." }; }
            var last = Last();
            if (last == null) return new { ok = false, message = "Aucune sauvegarde." };
            if (Process.GetProcessesByName("Le Mans Ultimate").Length > 0) return new { ok = false, message = "Ferme LMU d'abord : il réécrit ses touches en quittant." };
            try
            {
                var snapshot = JObject.Parse(File.ReadAllText(Path.Combine(last.FullName, "control-mapper.json")));
                string player = (string)snapshot["lmu"];
                int files = 0;
                if (player != null && Directory.Exists(player))
                    foreach (var name in LmuFiles)
                        if (File.Exists(Path.Combine(last.FullName, name))) { File.Copy(Path.Combine(last.FullName, name), Path.Combine(player, name), true); files++; }
                string error = null;
                OnScreen(() =>
                {
                    var settings = MapperSettings(plugin);
                    if (settings == null) { error = "Control Mapper absent."; return; }
                    var sources = Items(Nettoyage.Member(settings, "ControllerMappings")).ToList();
                    foreach (var link in snapshot["links"] ?? new JArray())
                    {
                        var source = sources.ElementAtOrDefault((int)link["s"]);
                        if (source == null || Name(source) != (string)link["name"]) continue;
                        var item = Items(Nettoyage.Member(Nettoyage.Member(source, "ControllerMapping"), (string)link["list"])).ElementAtOrDefault((int)link["n"]);
                        if (item != null) Nettoyage.Set(item, "TargetRole", (string)link["role"]);
                    }
                    var outputs = Items(Nettoyage.Member(Nettoyage.Member(Nettoyage.Member(settings, "OutputMapping"), "ControllerMapping"), "Buttons")).ToList();
                    foreach (var o in snapshot["outputs"] ?? new JArray())
                    {
                        var map = outputs.ElementAtOrDefault((int)o["n"]);
                        if (map != null) Nettoyage.Set(map, "TargetRole", (string)o["role"]);
                    }
                    Mapper(plugin)?.GetType().GetMethod("SaveSettings", Type.EmptyTypes)?.Invoke(Mapper(plugin), null);
                    var fmt = Nettoyage.Member(plugin.Companion.Instance, "Settings");
                    if (fmt != null)
                    {
                        foreach (var name in new[] { "Prepared", "Reused" })
                        {
                            var field = fmt.GetType().GetField(name);
                            if (field != null && snapshot[name.ToLowerInvariant()] is JToken saved) field.SetValue(fmt, saved.ToObject(field.FieldType));
                        }
                        SaveFmt(plugin);
                    }
                });
                if (error != null) return new { ok = false, message = error };
                Say("Sauvegarde du " + last.Name + " remise (" + files + " fichier(s) LMU et le Control Mapper).");
                return new { ok = true, message = (string)null };
            }
            catch (Exception e) { return new { ok = false, message = "Restauration interrompue : " + (e.InnerException ?? e).Message }; }
        }

        // ── What is kept ────────────────────────────────────────────────────────────────────────────────────────────
        // Direction, accélérateur, frein, embrayage, palettes ; et tout ce qui est sur un axe (pédales, frein à main…).
        static bool Keep(string control, string input)
        {
            string c = (control ?? "").ToLowerInvariant();
            if (c.Contains("steer") || c.Contains("accelerat") || c.Contains("throttle") || c.Contains("clutch") || c.Contains("shift up") || c.Contains("shift down")
                || c.Contains("upshift") || c.Contains("downshift") || c.Contains("handbrake") || c == "brake" || c == "brakes" || c == "brake pedal") return true;
            return input != null && Regex.IsMatch(input, "axis|slider|^[+-]?R?[XYZ]\\b|^[+-]", RegexOptions.IgnoreCase);
        }

        static int? Button(string input) { var m = Regex.Match(input ?? "", "^B(\\d+)$"); return m.Success ? int.Parse(m.Groups[1].Value) : (int?)null; }

        static bool Output(string device, bool bridge) => device != null && (bridge ? device.IndexOf("Remapper Bridge", StringComparison.OrdinalIgnoreCase) >= 0 : device.StartsWith("vJoy Device", StringComparison.OrdinalIgnoreCase));

        static string InputName(JObject di, string device, int id)
        {
            var names = di["Devices"]?[device]?["Input Names"] as JArray;
            return names != null && id >= 0 && id < names.Count ? (string)names[id] : null;
        }

        // ── LMU ─────────────────────────────────────────────────────────────────────────────────────────────────────
        static string LmuPlayer()
        {
            try
            {
                foreach (var process in Process.GetProcessesByName("Le Mans Ultimate"))
                    using (process) return Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "UserData", "player");
            }
            catch { }
            return null;
        }

        static async Task<JObject> Get(HttpClient client, string path)
        {
            using (var r = await client.GetAsync(path))
            {
                r.EnsureSuccessStatusCode();
                string text = await r.Content.ReadAsStringAsync();
                return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
            }
        }

        static JObject DirectInput(JObject controls)
        {
            var di = controls?["allControls"]?["directInput"] as JObject;
            if (di == null || !(di["Input"] is JObject) || !(di["Devices"] is JObject)) throw new InvalidOperationException("Écran des commandes de LMU inconnu.");
            if (!(di["Alternative Input"] is JObject)) di["Alternative Input"] = new JObject();
            return di;
        }

        // ── Control Mapper ──────────────────────────────────────────────────────────────────────────────────────────
        static object Mapper(EnduranceManagerPlugin plugin)
        {
            var pm = plugin.PluginManager;
            var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => { try { return a.GetType(PluginType, false); } catch { return null; } }).FirstOrDefault(t => t != null);
            var get = pm?.GetType().GetMethods().FirstOrDefault(m => m.Name == "GetPlugin" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
            return type == null ? null : get?.MakeGenericMethod(type).Invoke(pm, null);
        }

        static object MapperSettings(EnduranceManagerPlugin plugin) => Nettoyage.Member(Mapper(plugin), "controlMapperPluginSettings");

        static IEnumerable<object> Items(object list) => list is IEnumerable items ? items.Cast<object>() : Enumerable.Empty<object>();

        static string Name(object source) => Nettoyage.Member(Nettoyage.Member(source, "ControllerDescription"), "DisplayName") as string ?? "?";

        static JArray Links(object settings)
        {
            var links = new JArray();
            int s = 0;
            foreach (var source in Items(Nettoyage.Member(settings, "ControllerMappings")))
            {
                var map = Nettoyage.Member(source, "ControllerMapping");
                foreach (var list in new[] { "Buttons", "POVDirections", "AxisToButtons" })
                {
                    int n = 0;
                    foreach (var item in Items(Nettoyage.Member(map, list)))
                        links.Add(new JObject { ["s"] = s, ["name"] = Name(source), ["list"] = list, ["n"] = n++, ["role"] = Nettoyage.Member(item, "TargetRole") as string });
                }
                s++;
            }
            return links;
        }

        static JArray Outputs(object settings)
        {
            var outputs = new JArray();
            int n = 0;
            foreach (var map in Items(Nettoyage.Member(Nettoyage.Member(Nettoyage.Member(settings, "OutputMapping"), "ControllerMapping"), "Buttons")))
                outputs.Add(new JObject { ["n"] = n++, ["role"] = Nettoyage.Member(map, "TargetRole") as string });
            return outputs;
        }

        static void SaveFmt(EnduranceManagerPlugin plugin)
        {
            var fmt = plugin.Companion.Instance;
            fmt?.GetType().GetMethod("Save", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.Invoke(fmt, null);
        }

        static void OnScreen(Action action)
        {
            var dispatcher = System.Windows.Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action(); else dispatcher.Invoke(action);
        }
    }
}
