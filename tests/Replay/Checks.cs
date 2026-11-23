// Les tests : pour chaque séance de fixtures/, la mémoire de LMU refaite (Synth), rejouée dans le plugin (Replayer),
// puis comparée à ce que le plugin avait vraiment envoyé ce jour-là : mêmes tours, mêmes arrêts, mêmes chiffres.
// Ensuite le mur : les messages du plugin passent par le salon (room-bridge.mjs, le code du site) et arrivent au mur
// dans SimHub (Wall), dont on vérifie les cases.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace EnduranceManager.SimHub.Replay
{
    public static class Checks
    {
        static readonly CultureInfo Fr = CultureInfo.GetCultureInfo("fr-FR");

        class Report
        {
            public readonly List<string> Failures = new List<string>();
            public int Passed;
            public void Is(bool ok, string what) { if (ok) Passed++; else Failures.Add(what); }
            public void Near(double? expected, double? actual, double tolerance, string what)
            {
                bool ok = expected.HasValue == actual.HasValue && (!expected.HasValue || Math.Abs(expected.Value - actual.Value) <= tolerance);
                Is(ok, $"{what} : attendu {Show(expected)}, obtenu {Show(actual)}");
            }
            static string Show(double? x) => x.HasValue ? x.Value.ToString("0.###", CultureInfo.InvariantCulture) : "rien";
        }

        public static bool RunAll(List<string> fixtures, string outDir)
        {
            bool allOk = Recorder() & BlackBoxLines();
            foreach (var file in fixtures)
            {
                var f = Fixture.Load(file);
                var report = new Report();
                var watch = Stopwatch.StartNew();
                string tape = Path.Combine(outDir, f.File + ".emrec");
                Synth.Write(f, tape);
                var service = f.Service?.ToObject<ServiceTimes>();
                var run = Replayer.Run(tape, service, outDir, f.File);
                foreach (var e in run.Errors) report.Is(false, "Erreur dans le plugin : " + e);
                Session(f, run, report);
                Computed(f, run, report);
                Wall(f, run, outDir, report);
                Console.WriteLine($"{(report.Failures.Count == 0 ? "OK   " : "ÉCHEC")} {f.Name} : {report.Passed} vérifications, {report.Failures.Count} écart(s), {run.Frames} lectures, {watch.Elapsed.TotalSeconds:0.0} s");
                foreach (var failure in report.Failures.Take(40)) Console.WriteLine("      " + failure);
                if (report.Failures.Count > 40) Console.WriteLine($"      … et {report.Failures.Count - 40} autres");
                allOk &= report.Failures.Count == 0;
            }
            return allOk;
        }

        // The wheel's black box (BlackBoxDash.cs): the line changed last is the chosen one, the 7-line window follows it,
        // the game's English names and texts are shown as its French screen writes them.
        static bool BlackBoxLines()
        {
            var failures = new List<string>();
            void Is(bool ok, string what) { if (!ok) failures.Add(what); }
            string[] names = { "DAMAGE", "VIRTUAL ENERGY", "FUEL RATIO", "TIRES", "FL TIRE", "FR TIRE", "RL TIRE", "RR TIRE", "R WING", "GRILLE",
                "FL PRESS", "FR PRESS", "RL PRESS", "RR PRESS", "F BRAKE DUCT", "R BRAKE DUCT", "REPLACE BRAKES" };
            List<MenuItem> Menu(params (string name, int index)[] changes) => names.Select(n => new MenuItem
            {
                Name = n, Label = n, Index = changes.Where(c => c.name == n).Select(c => c.index).DefaultIfEmpty(0).First(),
                Text = n == "FL TIRE" ? "No Change" : n == "REPLACE BRAKES" ? "No" : "1"
            }).ToList();
            var box = new BlackBox();
            var props = new BlackBoxPages(box).Properties().ToDictionary(p => p.name, p => p.get);
            object V(string name) => props[name]();
            box.Update(Menu());
            Is(box.Chosen == "DAMAGE" && box.Top == 0, "Premier menu lu : curseur sur la 1re ligne, fenêtre en haut");
            Is((string)V("BlackBox.1.Nom") == "Dommages" && (string)V("BlackBox.2.Nom") == "Énergie Virtuelle:", "Noms du jeu en français");
            Is((string)V("BlackBox.5.Valeur") == "Aucun changement", "« No Change » écrit « Aucun changement »");
            Is((bool)V("BlackBox.Bas") && !(bool)V("BlackBox.Haut"), "Flèche du bas seulement");
            box.Update(Menu(("VIRTUAL ENERGY", 3)));
            Is(box.Chosen == "VIRTUAL ENERGY" && (bool)V("BlackBox.2.Choisie") && !(bool)V("BlackBox.1.Choisie"), "Énergie changée : 2e ligne en rouge");
            box.Update(Menu(("VIRTUAL ENERGY", 3), ("REPLACE BRAKES", 1)));
            Is(box.Chosen == "REPLACE BRAKES" && box.Top == names.Length - BlackBox.Rows, "Freins changés : la fenêtre descend jusqu'à la dernière ligne");
            Is((bool)V("BlackBox.7.Choisie") && (string)V("BlackBox.7.Nom") == "Remplacer les freins", "Freins en 7e ligne visible");
            Is((bool)V("BlackBox.Haut") && !(bool)V("BlackBox.Bas"), "Flèche du haut seulement");
            box.Update(Menu(("VIRTUAL ENERGY", 3), ("REPLACE BRAKES", 1), ("FL PRESS", 2)));
            Is(box.Chosen == "FL PRESS" && box.Top == names.Length - BlackBox.Rows, "Pression AvG visible : la fenêtre ne bouge pas");
            box.Update(Menu(("VIRTUAL ENERGY", 4), ("REPLACE BRAKES", 1), ("FL PRESS", 2)));
            Is(box.Chosen == "VIRTUAL ENERGY" && box.Top == 1, "Énergie au-dessus : la fenêtre remonte juste assez");
            // ▲ ▼ du volant : le curseur du dash bouge, s'arrête aux bouts, la fenêtre le suit.
            box.Move(-1);
            Is(box.Chosen == "DAMAGE" && box.Top == 0, "Monter : énergie puis dommages, fenêtre tout en haut");
            box.Move(-1);
            Is(box.Chosen == "DAMAGE", "Monter sur la 1re ligne : on y reste");
            for (int k = 0; k < 10; k++) box.Move(1);
            Is(box.Chosen == "FL PRESS" && box.Top == 4, "Descendre 10 fois : Pression AvG, la fenêtre descend juste assez");
            Is((bool)V("BlackBox.7.Choisie"), "Pression AvG en 7e ligne visible");
            for (int k = 0; k < 20; k++) box.Move(1);
            Is(box.Chosen == "REPLACE BRAKES" && box.Top == names.Length - BlackBox.Rows, "Descendre au bout : dernière ligne");
            int before = box.Top;
            box.Select(0);
            Is(box.Chosen == names[before] && box.Top == before - 1, "Toucher la 1re ligne visible la choisit et remonte d'une ligne");
            Is((string)V("BlackBox.Page") == "Arrêt aux stands" && (string)V("BlackBox.Message") == "", "Titre de la page, pas de message");
            // − + comme la black box du jeu : tour complet dans les deux sens, « Pneus » règle les 4 roues.
            JObject Line(string name, int current, params string[] choices) => new JObject
            {
                ["name"] = name, ["currentSetting"] = current, ["settings"] = new JArray(choices.Select(c => new JObject { ["text"] = c }))
            };
            string[] tyres = { "No Change", "Soft", "Medium", "Hard", "Wet", "INVALID" };
            var menu = new JArray(Line("TIRES:", 0, tyres), Line("FL TIRE:", 0, tyres), Line("FR TIRE:", 0, tyres), Line("RL TIRE:", 0, tyres), Line("RR TIRE:", 0, tyres),
                Line("VIRTUAL ENERGY:", 2, "98%", "99%", "100%"), Line("DAMAGE:", 0, "N/A"));
            int At(string name) => (int)menu.OfType<JObject>().First(l => PitMenu.Key(l) == name)["currentSetting"];
            Is(BlackBox.Step(menu, "TIRES", -1) && At("TIRES") == 4, "Pneus : moins depuis « Aucun changement » donne pluie (« INVALID » sauté)");
            Is(At("FL TIRE") == 4 && At("FR TIRE") == 4 && At("RL TIRE") == 4 && At("RR TIRE") == 4, "Pneus : les 4 roues suivent");
            Is(BlackBox.Step(menu, "TIRES", 1) && At("TIRES") == 0 && At("RR TIRE") == 0, "Pneus : plus depuis pluie revient à « Aucun changement »");
            Is(BlackBox.Step(menu, "FL TIRE", 1) && At("FL TIRE") == 1 && At("FR TIRE") == 0, "Une roue seule : les autres ne bougent pas");
            Is(BlackBox.Step(menu, "VIRTUAL ENERGY", 1) && At("VIRTUAL ENERGY") == 0, "Énergie : plus après le dernier choix revient au premier");
            Is(!BlackBox.Step(menu, "DAMAGE", 1), "Un seul choix : rien ne bouge");
            // Les 7 pages (BlackBoxPages.cs) : ◂ ▸ font le tour, chaque page ses boutons, comme la black box du jeu.
            var sent = new List<string>();
            var pages = new BlackBoxPages(box, id => { sent.Add(id); return null; });
            var pv = pages.Properties().ToDictionary(p => p.name, p => p.get);
            object PV(string name) => pv[name]();
            Is(pages.Page == BlackBoxPages.Stands, "Première page : Arrêt aux stands");
            pages.Turn(1);
            Is((string)PV("BlackBox.Page") == "Relatif" && (int)PV("BlackBox.PageNo") == 0, "Page suivante après Arrêt aux stands : Relatif (le tour)");
            pages.Turn(-1);
            Is((string)PV("BlackBox.Page") == "Relatif", "Le même appui reçu deux fois de suite (moins de 250 ms) : compté une fois");
            System.Threading.Thread.Sleep(300); pages.Turn(-1); System.Threading.Thread.Sleep(300); pages.Turn(-1);
            Is((string)PV("BlackBox.Page") == "Électronique", "Page précédente deux fois : Électronique");
            Is(pages.Presses.Contains("page précédente"), "Appuis notés");
            var car = new Sample { Fuel = 60, Energy = 80, MotorState = 2, Battery = 0.5, TC = 3, TCMax = 9, RearBrakeBias = 0.477, LapsDone = 3, Wheels = new Wheel[4],
                FrontCompound = "Medium", RearCompound = "Medium" };
            car.Wheels[0] = new Wheel { Wear = 0.99, Pressure = 122.7, Temp = 22.4, InnerIn = 33, InnerMid = 34, InnerOut = 35, Brake = 85.5 };
            car.Wheels[1] = new Wheel { Wear = 0.5, Off = true };
            var cv = new CourseView { FuelAvg5 = 3, FuelLastLap = 2.5, VeAvg5 = 4, VeLastLap = 5, LapTimeAvg10 = 120, RegenLevel = 2 };
            pages.Update(null, car, cv);
            Is((string)PV("BlackBox.1.Nom") == "Contrôle de traction" && (string)PV("BlackBox.1.Valeur") == "3" && (bool)PV("BlackBox.1.Choisie"), "Électronique : TC en 1re ligne, choisi");
            Is((string)PV("BlackBox.2.Nom") == "Répartition des freins" && (string)PV("BlackBox.2.Valeur") == "52.3:47.7", "Électronique : répartition avant:arrière");
            Is((string)PV("BlackBox.3.Nom") == "Niveau de recharge" && (string)PV("BlackBox.4.Nom") == "", "Électronique : seulement les réglages de la voiture");
            pages.Move(1); pages.Change(1); pages.Change(-1);
            System.Threading.Thread.Sleep(300);
            Is(sent.SequenceEqual(new[] { "Bias.Forward", "Bias.Rearward" }), "Électronique : − + passent par les commandes de FMT companion");
            pages.Turn(-1);
            Is((string)PV("BlackBox.1.Nom") == "◂ ▸  Virtual Energy" && (string)PV("BlackBox.1.Valeur") == "80%", "Carburant : Virtual Energy d'abord");
            Is((string)PV("BlackBox.4.Nom") == "Pousser" && (string)PV("BlackBox.4.C1") == "16.0" && (string)PV("BlackBox.4.C2") == "32 mins" && (string)PV("BlackBox.4.C3") == "5.00%", "Carburant : pousser");
            pages.Change(1);
            Is((string)PV("BlackBox.1.Nom") == "◂ ▸  Carburant" && (string)PV("BlackBox.1.Valeur") == "60.00L", "Carburant : − + passent au carburant");
            pages.Move(-1);
            Is((string)PV("BlackBox.2.Valeur") == "+1 Tours  ▴▾" && (string)PV("BlackBox.4.Nom") == "Objectif" && (string)PV("BlackBox.4.C1") == "21.0", "Carburant : ▲ un tour de plus à sauver");
            Is((string)PV("BlackBox.5.Nom") == "Estimation  ⚠", "Carburant : estimation trop courte signalée");
            pages.Move(1); pages.Move(1);
            Is((string)PV("BlackBox.2.Valeur") == "Aucun  ▴▾", "Carburant : ▼ jusqu'à « Aucun », pas en dessous");
            System.Threading.Thread.Sleep(300);
            pages.Turn(-1);
            Is((string)PV("BlackBox.Pneus.AVG.Usure") == "99%" && (string)PV("BlackBox.Pneus.AVG.Gomme") == "M" && (string)PV("BlackBox.Pneus.AVG.Pression") == "122.7kPa"
                && (string)PV("BlackBox.Pneus.AVG.Temp") == "34.0°C" && (string)PV("BlackBox.Pneus.Jeux") == "∞ Pneus", "Pneus : usure, gomme, pression, température, jeux");
            Is((string)PV("BlackBox.Pneus.AVD.Etat") == "Perdue" && (string)PV("BlackBox.Pneus.AVD.Couleur") == "#FFE83A4C", "Pneus : roue perdue en rouge");
            System.Threading.Thread.Sleep(300);
            pages.Turn(-1);
            Is((string)PV("BlackBox.1.Nom") == "◂ ▸  Delta" && (string)PV("BlackBox.3.Nom") == "▴ ▾  Timing" && (string)PV("BlackBox.4.Nom") == "Meilleur", "Chrono : Delta et Timing d'abord");
            pages.Change(1); pages.Move(1);
            car.LapsDone = 4; car.LastSector1 = 30; car.LastSector2 = 65; car.LastLap = 100;
            pages.Update(null, car, cv);
            Is((string)PV("BlackBox.1.Nom") == "◂ ▸  Secteur" && (string)PV("BlackBox.4.Nom") == "Dernier" && (string)PV("BlackBox.4.C2") == "35.000", "Chrono : Secteur, Relatif, dernier tour par secteur");
            pages.Change(1);
            Is((string)PV("BlackBox.4.C3") == "1:40.000", "Chrono : Split, temps à la fin de chaque secteur");
            Is(BlackBoxPages.Short("Nathan Boulanger") == "N Boulanger" && BlackBoxPages.Badge("Hypercar").badge == "HY", "Relatif : nom court et catégorie");
            // Watched.cs : rien n'est calculé pour un écran fermé ; il compte comme ouvert dès qu'il lit une propriété.
            var screen = new Watched(); bool closed = !screen.Now; screen.Wrap(() => 1)();
            Is(closed && screen.Now, "Écran : fermé au départ, ouvert dès qu'il lit");
            foreach (var failure in failures) Console.WriteLine("ÉCHEC Black box : " + failure);
            if (failures.Count == 0) Console.WriteLine("OK    Black box : ligne choisie, défilement et noms");
            return failures.Count == 0;
        }

        // The recorder's format (Tape.cs): each copy rebuilt exactly from the one before and its changes.
        static bool Recorder()
        {
            var random = new Random(7);
            byte[] before = null, now = new byte[4096], copy = new byte[4096];
            for (int round = 0; round < 200; round++)
            {
                now = (byte[])now.Clone();
                for (int k = random.Next(0, 60); k > 0; k--) now[random.Next(now.Length)] = (byte)random.Next(256);
                Tape.Apply(copy, Tape.Delta(before, now));
                if (!copy.SequenceEqual(now)) { Console.WriteLine($"ÉCHEC Enregistreur : copie {round} mal refaite"); return false; }
                before = now;
            }
            Console.WriteLine("OK    Enregistreur : 200 copies refaites à l'identique");
            return true;
        }

        // What the plugin sends to the fiches combo: the same laps and stops as the day the session was driven.
        static void Session(Fixture f, Run run, Report r)
        {
            r.Is(run.Sessions.Count == 1, $"Une séance attendue, {run.Sessions.Count} obtenue(s)");
            var s = run.Sessions.FirstOrDefault();
            if (s == null) return;
            r.Is(s.Track == f.Track && s.Car == f.Car && s.Class == f.Class, $"Circuit, voiture, catégorie : {s.Track} · {s.Car} · {s.Class}");
            r.Is(s.Laps.Count == f.Laps.Count, $"Tours : attendu {f.Laps.Count}, obtenu {s.Laps.Count}");
            foreach (var (want, got) in f.Laps.Zip(s.Laps, (a, b) => (a, b)))
            {
                string at = $"Tour {want.N}";
                r.Is(got.N == want.N, $"{at} : numéro {got.N}");
                r.Near(want.T ?? 0, got.Time, 0.002, at + " temps");
                r.Near(want.S1, got.S1, 0.002, at + " S1");
                r.Near(want.S2, got.S2, 0.002, at + " S2");
                r.Near(want.S3, got.S3, 0.002, at + " S3");
                r.Is(got.Pit == want.Pit, $"{at} : stands {got.Pit}");
                r.Near(want.Fuel, got.Fuel, 0.01, at + " carburant");
                r.Near(want.Ve, got.Energy, 0.01, at + " énergie");
                // A lap ends between two readings (ten a second): up to a tenth of a second of the lap before or after falls in it.
                int index = f.Laps.IndexOf(want);
                double Neighbour(int i) => Math.Max(want.Wear[i], Math.Max(index > 0 ? f.Laps[index - 1].Wear[i] : 0, index + 1 < f.Laps.Count ? f.Laps[index + 1].Wear[i] : 0));
                for (int i = 0; i < 4; i++) r.Near(want.Wear[i], got.Wear[i], 0.01 + 0.001 * Neighbour(i), at + " usure " + "AVG AVD ARG ARD".Split(' ')[i]);
                if (!want.Pit || got.Temp[0] > 0) r.Near(want.Temp, got.Temp[0], 0.05, at + " température pneu");
                r.Near(want.Kpa, got.Pressure[0], 0.05, at + " pression");
                r.Is(got.Compound == want.Compound, $"{at} : gomme {got.Compound}");
                r.Near(want.TrackTemp, got.TrackTemp, 0.05, at + " piste °C");
                r.Near(want.Rain, got.Rain, 0.005, at + " pluie");
                r.Near(want.Wet, got.Wet, 0.01, at + " humidité");
                r.Near(want.Top, got.Top, 0.15, at + " vitesse max");
            }
            r.Is(s.Stops.Count == f.Stops.Count, $"Arrêts : attendu {f.Stops.Count}, obtenu {s.Stops.Count}");
            foreach (var (want, got) in f.Stops.Zip(s.Stops, (a, b) => (a, b)))
            {
                string at = $"Arrêt du tour {want.Lap}";
                r.Is(got.Lap == want.Lap, $"{at} : noté au tour {got.Lap}");
                r.Near(want.Lane, got.Lane, 0.25, at + " voie");
                r.Near(want.Stopped, got.Stationary, 0.25, at + " arrêté");
                r.Near(want.Fuel, got.Fuel, 0.1, at + " carburant mis");
                r.Near(want.Ve, got.Energy, 0.1, at + " énergie mise");
                r.Is(got.Tyres == want.Tyres, $"{at} : {got.Tyres} pneus changés au lieu de {want.Tyres}");
            }
            r.Is(s.Trace?.N == f.TraceLap, $"Trace du meilleur tour : tour {s.Trace?.N} au lieu de {f.TraceLap}");
            r.Is(s.Expert != null, "Chiffres expert de la séance absents");
        }

        // Our own calculations (Computed.cs): optimal lap, micro-sectors, rejoin after a stop.
        static void Computed(Fixture f, Run run, Report r)
        {
            var clean = f.Laps.Where(l => l.S1 > 0 && l.S2 > 0 && l.S3 > 0 && !l.Invalid).ToList();
            double optimal = Math.Round(clean.Min(l => l.S1.Value) + clean.Min(l => l.S2.Value) + clean.Min(l => l.S3.Value), 3);
            r.Near(optimal, run.Optimal, 0.002, "Tour optimal (meilleurs secteurs)");
            double best = f.Laps.Where(l => l.T > 0 && !l.Pit).Min(l => l.T.Value);
            r.Is(run.MicroOptimal.HasValue && Math.Abs(run.MicroOptimal.Value - best) < 3, $"Micro-secteurs : optimal {run.MicroOptimal} pour un meilleur tour de {best}");
            if (f.Stops.Count > 0)
            {
                var stop = f.Stops[0];
                r.Is(run.RejoinLoss > stop.Stopped && run.RejoinLoss < stop.Lane + stop.Stopped + 10, $"Sortie des stands : temps perdu {run.RejoinLoss} (arrêt {stop.Stopped} s, voie {stop.Lane} s)");
                r.Is(run.RejoinPlace >= 1 && run.RejoinPlace <= 4, $"Sortie des stands : place {run.RejoinPlace} sur 4");
            }
            var c = run.LastCourse;
            r.Is(c != null && c.Track == f.Track && c.Car == f.Car, "Chiffres de course : circuit et voiture");
        }

        // The mur: the plugin's messages through the room (the site's code), then into the dash in SimHub.
        static readonly string[] Inputs = { "Mur.Entrees." };

        static void Wall(Fixture f, Run run, string outDir, Report r)
        {
            r.Is(run.Messages.Count > 0, "Aucun message pour le mur");
            if (run.Messages.Count == 0) return;
            string plugin = Path.Combine(outDir, f.File + ".plugin.jsonl"), room = Path.Combine(outDir, f.File + ".room.jsonl");
            string bridge = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "room-bridge.mjs"));
            if (!File.Exists(bridge)) bridge = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "room-bridge.mjs"));
            var p = Process.Start(new ProcessStartInfo("node", $"\"{bridge}\" \"{plugin}\" \"{room}\"") { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false });
            string err = p.StandardError.ReadToEnd();
            p.StandardOutput.ReadToEnd();
            p.WaitForExit();
            r.Is(p.ExitCode == 0, "Salon (room-bridge.mjs) : " + err.Trim());
            if (p.ExitCode != 0) return;

            var wall = new Wall();
            long now = 0;
            Tape.Clock = () => DateTimeOffset.FromUnixTimeMilliseconds(now).LocalDateTime;
            int errors = 0;
            try
            {
                var lines = File.ReadAllLines(room);
                int index = 0;
                foreach (var line in lines)
                {
                    // The wall halfway through the session too, car on track (for the picture, dash-render.mjs).
                    if (index++ == lines.Length / 2)
                        File.WriteAllText(Path.Combine(outDir, f.File + ".mur-milieu.json"), JsonConvert.SerializeObject(WallProperties.All.ToDictionary(x => x.Name, x => x.Get(wall.View)), Formatting.Indented));
                    var item = JObject.Parse(line);
                    now = (long)item["at"];
                    bool whole = wall.Hear((string)item["message"]);
                    if (!whole) r.Is(false, $"Mur : changement sans base à {now}");
                    if (wall.LastError != null && errors++ < 5) r.Is(false, "Mur : " + wall.LastError.GetType().Name + " " + wall.LastError.Message + " " + wall.LastError.StackTrace?.Split('\n').FirstOrDefault()?.Trim());
                }
                var v = wall.View;
                File.WriteAllText(Path.Combine(outDir, f.File + ".mur.json"), JsonConvert.SerializeObject(WallProperties.All.ToDictionary(x => x.Name, x => x.Get(v)), Formatting.Indented));
                r.Is(v.Live, $"Mur : pas « en direct » ({v.Status})");
                r.Is(v.Track == f.Track, $"Mur : circuit « {v.Track} »");
                r.Is(v.Class != null, "Mur : catégorie vide");
                var last = f.Laps.Last(l => l.T > 0);
                string lap = (int)(last.T.Value / 60) + ":" + (last.T.Value - (int)(last.T.Value / 60) * 60).ToString("00.000", Fr);
                r.Is(v.LastLap == lap, $"Mur : dernier tour « {v.LastLap} » au lieu de « {lap} »");
                r.Is(v.Standing.Count(x => x.Visible) == 4, $"Mur : {v.Standing.Count(x => x.Visible)} lignes au classement de la catégorie au lieu de 4");
                r.Is(v.Standing.Count(x => x.Visible && x.Us) == 1, "Mur : notre voiture absente du classement");
                // The mur des stands on the PC alone (LocalRoom) shows the same as through the site's room, inputs aside
                // (the room passes our car's frames ten times a second, the PC its reading of the moment).
                var viaSite = WallProperties.All.ToDictionary(x => x.Name, x => JsonConvert.SerializeObject(x.Get(v)));
                var solo = WallProperties.All.ToDictionary(x => x.Name, x => JsonConvert.SerializeObject(x.Get(run.Solo.View)));
                File.WriteAllText(Path.Combine(outDir, f.File + ".mur-solo.json"), JsonConvert.SerializeObject(WallProperties.All.ToDictionary(x => x.Name, x => x.Get(run.Solo.View)), Formatting.Indented));
                var differ = viaSite.Keys.Where(k => viaSite[k] != solo[k] && !Inputs.Any(k.Contains)).ToList();
                r.Is(differ.Count == 0, "Mur solo différent du mur par le site : " + string.Join(", ", differ.Take(8).Select(k => $"{k} {solo[k]} au lieu de {viaSite[k]}")));
                // The mur's page (WallServer.cs) answers /vue with this wall, as the window and a tablet read it.
                var server = new WallServer(() => new { vue = run.Solo.View, profil = "Sec" }, (x, y, w, h) => { });
                server.Start();
                r.Is(server.Running, "Page du mur : serveur pas lancé (" + server.Error + ")");
                if (server.Running)
                    try
                    {
                        using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                        {
                            string json = http.GetStringAsync(server.LocalUrl + "vue").GetAwaiter().GetResult();
                            File.WriteAllText(Path.Combine(outDir, f.File + ".vue.json"), json);
                            var page = JObject.Parse(json);
                            r.Is((string)page["vue"]?["Track"] == f.Track, "Page du mur : circuit « " + (string)page["vue"]?["Track"] + " »");
                            r.Is((string)page["vue"]?["LastLap"] == lap, "Page du mur : dernier tour « " + (string)page["vue"]?["LastLap"] + " »");
                            r.Is((string)page["profil"] == "Sec", "Page du mur : profil absent");
                        }
                    }
                    catch (Exception e) { r.Is(false, "Page du mur : " + e.Message); }
                    finally { server.Stop(); }
            }
            finally { Tape.Clock = null; }
        }
    }
}
