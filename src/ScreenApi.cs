// L'écran du plugin en page web (22/11/2026, maquette validée par Nathan) : src/ecran/index.html, servi par WallServer
// sur ce PC seulement et affiché par SimHub (WebView2, Shell dans Guide.cs). La page lit /etat (tout ce qu'elle montre,
// en JSON) et envoie ses clics à /action. Ce fichier fait les deux, avec les mêmes méthodes du plugin que l'ancien écran.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using Newtonsoft.Json.Linq;

namespace EnduranceManager.SimHub
{
    public class ScreenApi
    {
        readonly EnduranceManagerPlugin plugin;
        DateTime pastedAt;

        public ScreenApi(EnduranceManagerPlugin plugin) { this.plugin = plugin; }

        static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        // ── What the page shows ─────────────────────────────────────────────────────────────────────────────────────
        public object State(string query = null)
        {
            var s = plugin.Settings;
            var code = plugin.Link;
            var saved = LinkCode.Parse(s.Code);
            // Not linked yet: the code copied on the site is taken from the clipboard by itself, as on the old screen.
            if (saved == null && plugin.TeamOn && DateTime.Now - pastedAt > TimeSpan.FromSeconds(2)) { pastedAt = DateTime.Now; OnScreen(() => Paste(true), wait: false); }
            plugin.Updater.Check(code);
            var uploader = plugin.Uploader;
            string host = code == null ? "" : new Uri(code.Origin).Host;
            int waiting = uploader?.Waiting(code) ?? 0;
            int held = uploader?.Held(code) ?? 0;
            string kept = waiting > 0 ? $" {waiting} séance{(waiting > 1 ? "s" : "")} en attente, rien n'est perdu." : "";

            (string state, string title, string detail) site;
            if (!plugin.TeamOn) site = ("off", "Pas utilisé", "Aucun module d'équipe allumé." + kept);
            else if (code == null) site = ("wait", "Pas encore relié", "Copie ton code sur le site (Fiches combo, Relier SimHub) : le plugin le trouve tout seul.");
            else if (uploader?.Refused is string refused) site = ("bad", "Liaison refusée", refused + kept);
            else if (uploader?.Reached == false) site = ("wait", "Injoignable", "Nouvel essai dans une minute." + kept);
            else if (uploader?.Reached == true) site = ("ok", "Relié", host + (uploader.LastSent is DateTime last ? $" · dernier envoi à {last:HH:mm}" : ""));
            else site = ("ok", "Relié", host);

            (string state, string title, string detail) game;
            switch (plugin.GameState)
            {
                case "track": game = ("ok", "En piste", plugin.Profiles?.Car ?? ""); break;
                case "menu": game = ("idle", "Ouvert", "Au menu ou au garage"); break;
                case "paused": game = ("idle", "Pas lu", "Tous les modules sont coupés"); break;
                default: game = ("idle", "Fermé", "Lance le jeu et roule"); break;
            }

            var server = plugin.WallServer;
            int files = Report.WaitingCount;
            string memo = !s.Enabled ? "Module coupé : aucune séance enregistrée." : plugin.LastSession is string recorded ? "Dernière séance : " + recorded : "Aucune séance depuis le lancement de SimHub.";
            if (held > 0) memo += $" {held} séance(s) conservées sur ce PC, sans envoi avec ce code.";
            if (waiting > 0) memo += kept;
            if (uploader?.LastRefusal is string refusal) memo += " Une séance a été refusée par le site " + refusal + ".";
            return new
            {
                version = Updater.Current.ToString(3),
                build = Updater.Build,
                update = plugin.Updater.Available?.ToString(3),
                upd = new { changes = Lines(plugin.Updater.Changes), phase = plugin.Updater.Phase, error = plugin.Updater.Error, checkedOk = plugin.Updater.CheckSucceeded, checkError = plugin.Updater.CheckError },
                installed = plugin.Updater.Installed() is var done && done != null ? Lines(done.Value.Changes) : null,
                go = new { n = plugin.GoCount, page = plugin.GoPage },
                guideDone = s.GuideDone,
                site = new { site.state, site.title, site.detail, linked = saved != null, origin = code?.Origin },
                game = new { game.state, game.title, game.detail },
                modules = new { relais = s.Overlays, profils = s.Profiles, bb = s.CompanionOn, mur = s.Wall, memo = s.Enabled, equipe = s.Share, dev = s.Report },
                laps = s.StintLaps,
                bb = new { running = plugin.Companion.Running, pending = s.CompanionOn != plugin.Companion.StartAttempted, status = plugin.Companion.Status, page = plugin.Companion.Running && (query ?? "").Contains("bb=1") ? plugin.CompanionPage.State() : null, buttons = Repartir.State(plugin) },
                mur = new
                {
                    running = server?.Running == true && s.Wall,
                    address = s.Wall && server?.Running == true ? (server.NetworkUrl ?? server.LocalUrl) : null,
                    status = !s.Wall ? "Module coupé." : plugin.Wall.Status + (server?.Error is string error && server.Running != true ? " · Page du mur impossible à lancer : " + error : ""),
                },
                memo,
                equipe = !s.Share ? "Module coupé : ta voiture n'est pas partagée." : code == null ? "Pas encore relié au site." : plugin.Live.Status + (plugin.Live.SentAt is DateTime sent ? $" · dernier envoi à {sent:HH:mm:ss}" : ""),
                dev = !s.Report ? "Module coupé : rien n'est noté." : "Journal en cours, écrit à la fin de chaque séance." + (files > 0 ? $" {files} fichier{(files > 1 ? "s" : "")} en attente." : "") + (Report.LastSent is string at ? $" Dernier envoi à {at}." : ""),
                recording = Tape.Recording,
                record = Tape.Status,
                profiles = Profiles(),
            };
        }

        // The track picked in the « Circuit » list to look at another combo of the car (null = the one in LMU).
        string viewTrack;

        // The combo shown: the car with the track in LMU, or the one picked in the list (only to look and rename).
        (string Car, string Track, bool Other) Shown()
        {
            var (car, track) = plugin.Profiles.Shown;
            if (viewTrack != null && viewTrack != track && plugin.Profiles.TracksOf(car).Contains(viewTrack)) return (car, viewTrack, true);
            return (car, track, false);
        }

        List<Profile> ShownList()
        {
            var (car, track, _) = Shown();
            return car == null ? null : plugin.Profiles.For(car, track);
        }

        object Profiles()
        {
            var p = plugin.Profiles;
            if (p == null) return null;
            var (car, track, other) = Shown();
            var list = car == null ? new List<Profile>() : p.For(car, track);
            string from = car == null ? null : p.FromOf(car, track);
            string active = p.Car != null && !other ? p.Active : null;
            var lines = new List<string>();
            if (plugin.Settings.Profiles)
            {
                if (p.ToChange) lines.Add("La piste correspond au profil « " + p.ForWeather + " ».");
                if (!p.AnyRole) lines.Add("Moteur des touches (FMT) pas lancé : redémarre SimHub.");
                if (!string.IsNullOrEmpty(p.Message)) lines.Add(p.Message);
            }
            if (!string.IsNullOrEmpty(track) && from != null && !other)
                lines.Insert(0, "Premier passage sur ce circuit avec cette voiture : profils repris " + (from == "" ? "de tes profils de la voiture" : "de « " + from + " »") + ". Mémorise pour les garder ici.");
            return new
            {
                car,
                track,
                tracks = car == null ? new List<string>() : p.TracksOf(car),
                here = p.Shown.Track,
                other,
                driving = p.Car != null,
                active,
                wet = p.Wetness,
                notes = lines,
                slots = list.Select((profile, i) => new
                {
                    i,
                    name = profile.Name,
                    active = profile.Name == active,
                    drive = Setting.All.Where(x => profile.Drive.ContainsKey(x.Key)).Select(x => new { k = x.Label, v = x.Key == "Bias" ? profile.Drive[x.Key].ToString("0.0", Fr) : profile.Drive[x.Key].ToString("0") }),
                    pit = profile.Pit.Select(line => new { key = line.Key, k = EnduranceManager.SimHub.Profiles.Pretty(line.Key), v = line.Value, on = profile.PitApplied.Contains(line.Key) }),
                }),
            };
        }

        // ── What the page asks ──────────────────────────────────────────────────────────────────────────────────────
        // One JSON object { do: "…", … }; the answer { ok, message }.
        public object Act(string body)
        {
            JObject a;
            try { a = JObject.Parse(body); } catch { return new { ok = false, message = "Demande illisible." }; }
            string what = (string)a["do"] ?? "";
            bool on = (bool?)a["on"] ?? false;
            int i = (int?)a["i"] ?? -1;
            try
            {
                switch (what)
                {
                    case "module": return Module((string)a["key"], on);
                    case "laps": plugin.SetStintLaps((int?)a["value"] ?? plugin.Settings.StintLaps); return Ok();
                    case "paste": { string said = null; OnScreen(() => said = Paste(false)); return new { ok = said == null, message = said ?? "Code enregistré. Tes séances partiront dans la minute." }; }
                    case "site": return OpenSite((string)a["path"] ?? "/");
                    case "update": return Update();
                    case "later": UpdateWindow.Close(); return Ok();
                    case "see": plugin.See((string)a["page"] ?? "home"); return Ok();
                    case "wall": plugin.OpenWall(on); return Ok();
                    case "copyWall": { var url = plugin.WallServer?.NetworkUrl; if (url != null) OnScreen(() => Clipboard.SetText(url)); return Ok(); }
                    case "popup": plugin.PlacePopup(); return Ok();
                    case "rename": return Rename(i, (string)a["name"]);
                    case "cleanup": return Nettoyage.Run(plugin);
                    case "zero": return Repartir.Zero(plugin);
                    case "restore": return Repartir.Restore(plugin);
                    case "track": { string t = (string)a["track"]; viewTrack = string.IsNullOrEmpty(t) ? null : t; return Ok(); }
                    case "memorise": System.Threading.ThreadPool.QueueUserWorkItem(_ => plugin.Profiles.Memorise(i)); return Ok();
                    case "apply": plugin.Profiles.Apply(i); return Ok();
                    case "pit": return Pit(i, (string)a["key"], on);
                    case "signal":
                        {
                            string note = ((string)a["note"] ?? "").Trim();
                            if (note == "") return new { ok = false, message = "Écris d'abord en une phrase ce qui ne va pas." };
                            plugin.Signal(note);
                            return new { ok = true, message = plugin.Link != null ? "Merci, c'est noté : il part sur le site dès que tu n'es plus en piste." : "Merci, c'est noté dans le dossier des retours." };
                        }
                    case "folder": System.IO.Directory.CreateDirectory(Report.Folder); Process.Start(new ProcessStartInfo(Report.Folder) { UseShellExecute = true }); return Ok();
                    case "record": plugin.SetRecording(on); return Ok();
                    case "guideDone": plugin.SetModule(x => x.GuideDone = true); return Ok();
                    case "companion": return Companion();
                    case "bbCheck": case "bbTest": case "bbPrepare": case "bbUndo": case "bbMapper": case "bbDeck": case "bbBackup": case "bbGarage": case "bbWrites":
                        return plugin.CompanionPage.Act(what, (string)a["id"], on);
                    case "oldScreen": OnScreen(() => plugin.Shell?.ShowModules()); return Ok();
                    default: return new { ok = false, message = "Inconnu : " + what };
                }
            }
            catch (Exception e)
            {
                Report.Error("Écran", e);
                return new { ok = false, message = (e as System.Reflection.TargetInvocationException)?.InnerException?.Message ?? e.Message };
            }
        }

        static object Ok(string message = null) => new { ok = true, message };

        object Module(string key, bool on)
        {
            switch (key)
            {
                case "relais": plugin.SetModule(x => x.Overlays = on); break;
                case "profils": plugin.SetModule(x => x.Profiles = on); break;
                case "bb": plugin.SetModule(x => x.Companion = on); return Ok("Pris en compte au prochain lancement de SimHub.");
                case "mur": plugin.SetWall(on); break;
                case "memo": plugin.SetEnabled(on); break;
                case "equipe": plugin.SetShare(on); break;
                case "dev": plugin.SetModule(x => x.Report = on); break;
                default: return new { ok = false, message = "Module inconnu." };
            }
            return Ok();
        }

        object OpenSite(string path)
        {
            var code = plugin.Link ?? LinkCode.Parse(plugin.Settings.Code);
            string origin = code?.Origin ?? Updater.PublicSite;
            if (!path.StartsWith("/")) path = "/";
            Process.Start(new ProcessStartInfo(origin + path) { UseShellExecute = true });
            return Ok();
        }

        // Never while driving: the installer closes SimHub.
        object Update()
        {
            if (plugin.GameState == "track") return new { ok = false, message = "Termine d'abord ta séance : la mise à jour redémarre SimHub." };
            plugin.Updater.Install(plugin.Link, () => OnScreen(() => Application.Current?.Shutdown(), wait: false));
            return Ok();
        }

        static object Lines(List<Updater.Change> changes) => (changes ?? new List<Updater.Change>()).Select(c => new { type = c.Type, text = c.Text, page = c.Page }).ToList();

        object Rename(int i, string name)
        {
            var p = plugin.Profiles;
            var list = ShownList();
            if (list == null) return new { ok = false, message = "Pas de voiture." };
            if (i < 0 || i >= list.Count) return new { ok = false, message = "Pas de profil." };
            name = (name ?? "").Trim();
            list[i].Name = name == "" ? "Profil " + (i + 1) : name.Length > 24 ? name.Substring(0, 24) : name;
            p.Save();
            return Ok();
        }

        object Pit(int i, string key, bool on)
        {
            var p = plugin.Profiles;
            var list = ShownList();
            if (list == null || i < 0 || i >= list.Count || key == null) return new { ok = false, message = "Pas de profil." };
            var profile = list[i];
            if (on && !profile.PitApplied.Contains(key)) profile.PitApplied.Add(key);
            if (!on) profile.PitApplied.Remove(key);
            p.Save();
            return Ok();
        }

        // FMT companion's own screen, in a window of its own (« Ancien écran » on its page, if ever needed).
        object Companion()
        {
            if (!plugin.Companion.Running) return new { ok = false, message = plugin.Companion.Status };
            OnScreen(() =>
            {
                var screen = plugin.Companion.Screen(plugin.PluginManager);
                if (screen == null) return;
                var window = new Window { Title = "Black box sur boutons · FMT companion", Width = 1100, Height = 760, Content = new System.Windows.Controls.ScrollViewer { Content = screen }, Owner = Application.Current?.MainWindow };
                Theme.Apply(window);
                window.Show();
            });
            return Ok();
        }

        // The code copied on the site, from the clipboard (SimHub's window thread). null when saved, else why not.
        string Paste(bool automatic)
        {
            string text = "";
            try { text = Clipboard.GetText(); } catch { }
            var code = LinkCode.Parse(text);
            if (code == null) return automatic ? "" : "Aucun code dans le presse-papiers. Sur le site : Fiches combo, « Relier SimHub », « Créer mon code », puis reclique ici.";
            plugin.SaveCode(code.ToString());
            return null;
        }

        static void OnScreen(Action action, bool wait = true)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return;
            if (wait) dispatcher.Invoke(action); else dispatcher.BeginInvoke(action);
        }
    }
}
