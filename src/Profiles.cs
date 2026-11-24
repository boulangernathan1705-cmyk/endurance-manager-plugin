// Profils de réglages : chaque pilote garde, pour chaque combo voiture + circuit, ses réglages de black box (Sec, Inter,
// Pluie…). Le combo est lu dans LMU, rien à choisir ; un combo jamais vu part du dernier circuit réglé avec la voiture. Un
// bouton du volant par profil : appui court, le plugin remet la voiture dans ce profil ; appui de 2 s, il mémorise l'état
// actuel dans ce profil.
//
// En roulant (TC, coupure, slip, ABS, répartition, migration, carto, barres) : LMU ne laisse pas écrire ces réglages, le
// plugin appuie donc sur les commandes du jeu par les rôles du Control Mapper de SimHub (sa sortie vJoy ou bridge USB, comme
// les boutons du volant), et relit la valeur dans la mémoire du jeu après chaque appui jusqu'à la bonne.
// Au stand : les lignes du menu des stands sont écrites par l'API locale du jeu (lu, modifié, renvoyé entier, relu).
//
// Rien n'est automatique : deux rappels seulement, publiés en propriétés pour les dashs (profil plus actif, piste hors de
// la plage d'humidité du profil). Les profils restent sur le PC du pilote (profils.json).
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;

namespace EnduranceManager.SimHub
{
    public class Profile
    {
        public string Name = "";
        // The track wetness range it is meant for (% of the racing line, LMU's path wetness): from WetMin to below WetMax.
        public double WetMin, WetMax = 100;
        // The in-car settings memorised (the game's levels; brake bias as % at the front), by Setting key.
        public Dictionary<string, double> Drive = new Dictionary<string, double>();
        // The pit menu lines memorised (line name → choice text), and the ones it applies (damage and driver left out by default).
        public Dictionary<string, string> Pit = new Dictionary<string, string>();
        public List<string> PitApplied = new List<string>();
        // The place of each memorised choice in its line, used when the game shows the choice with another text.
        public Dictionary<string, int> PitIndex = new Dictionary<string, int>();
    }

    public class ProfileFile
    {
        // Car model → its profiles, in button order (button 1 = the first). Since the combos: the car's last memorised
        // profiles (on any track), where a track never driven with it starts from. Also used when LMU gives no track.
        public Dictionary<string, List<Profile>> Cars = new Dictionary<string, List<Profile>>();
        // Car model → track → its profiles: one set per car + track combo.
        public Dictionary<string, Dictionary<string, List<Profile>>> Tracks = new Dictionary<string, Dictionary<string, List<Profile>>>();
        // Car model → the track its Cars profiles were last memorised on.
        public Dictionary<string, string> LastTrack = new Dictionary<string, string>();
        // Car model → track → the track its profiles were copied from, until memorised there ("" = the car's first profiles).
        public Dictionary<string, Dictionary<string, string>> From = new Dictionary<string, Dictionary<string, string>>();
        // The last combo seen in LMU, shown on the screen when the game is closed.
        public string SeenCar, SeenTrack;
        // Command (Setting key + "+" or "-") → a role kept from older versions (chosen by hand). Roles are found by themselves now.
        public Dictionary<string, string> Roles = new Dictionary<string, string>();
    }

    // One setting changed with a pair of LMU commands.
    public class Setting
    {
        public string Key, Label;
        public Func<Sample, double?> Read;
        // How close counts as reached (brake bias moves in fractions of a percent).
        public double Tolerance = 0.5;
        // Words of the pilot's own Control Mapper roles (used before 24/11; the profiles now press through FMT companion).
        public string[] Words;

        public static readonly Setting[] All =
        {
            // Names and order of the in-car menu « Électronique » of LMU in French (screenshots by Nathan, 24/11/2026).
            new Setting { Key = "TC", Label = "Contrôle de traction", Words = new[] { "tc" }, Read = s => s.TCMax > 0 ? s.TC : (double?)null },
            new Setting { Key = "TCCut", Label = "TC Power cut", Words = new[] { "tc cut", "tc2", "coupure" }, Read = s => s.TCCutMax > 0 ? s.TCCut : (double?)null },
            new Setting { Key = "TCSlip", Label = "TC Slip angle", Words = new[] { "tc angle", "tc slip", "tcslip", "slip", "tc glisse", "glisse" }, Read = s => s.TCSlipMax > 0 ? s.TCSlip : (double?)null },
            new Setting { Key = "ABS", Label = "ABS", Words = new[] { "abs" }, Read = s => s.ABSMax > 0 ? s.ABS : (double?)null },
            new Setting { Key = "ArbFront", Label = "ARB avant", Words = new[] { "arb av", "arb front", "front arb", "frontarb", "barre av", "antiroulis av" }, Read = s => s.AntiSwayFrontMax > 0 ? s.AntiSwayFront : (double?)null },
            new Setting { Key = "ArbRear", Label = "ARB arrière", Words = new[] { "arb arr", "arb rear", "rear arb", "reararb", "barre arr", "antiroulis arr" }, Read = s => s.AntiSwayRearMax > 0 ? s.AntiSwayRear : (double?)null },
            new Setting { Key = "Bias", Label = "Répartition des freins", Words = new[] { "bb", "bias", "répartition", "repartition" }, Tolerance = 0.05,
                Read = s => s.RearBrakeBias > 0 && s.RearBrakeBias < 1 ? Math.Round((1 - s.RearBrakeBias) * 100, 1) : (double?)null },
            new Setting { Key = "Migration", Label = "Répartition des freins (migration)", Words = new[] { "migration" }, Read = s => s.MigrationMax > 0 ? s.Migration : (double?)null },
            new Setting { Key = "MotorMap", Label = "Cartographie moteur", Words = new[] { "carto", "motor map", "motormap", "map" }, Read = s => s.MotorMapMax > 0 ? s.MotorMap : (double?)null },
        };
    }

    public class Profiles
    {
        // The wheel buttons: one SimHub action per place in the list (« Profil1 » … « Profil6 »).
        public const int Slots = 6;
        // A press held this long memorises instead of applying.
        const int HoldMs = 2000;
        const string MenuUrl = "http://localhost:6397/rest/garage/PitMenu/receivePitMenu";
        const string LoadUrl = "http://localhost:6397/rest/garage/PitMenu/loadPitMenu";
        static readonly string[] PitOffByDefault = { "DAMAGE", "DRIVER" };

        readonly string path;
        readonly Func<Sample?> latest;
        readonly Func<PluginManager> manager;
        readonly Func<bool> enabled;
        readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        readonly object gate = new object();
        readonly DateTime[] pressedAt = new DateTime[Slots];
        readonly Timer[] holds = new Timer[Slots];
        int busy;

        public ProfileFile File = new ProfileFile();
        // The car being driven (or the last one), whose profiles the screen and the buttons use.
        public volatile string Car;
        // The track of the session (empty if LMU gives none): with the car, the combo whose profiles are used.
        public volatile string Track;
        // The profile last applied by the plugin, and what the last button did (shown on the screen and the dashs).
        public volatile string Applied;
        public volatile string Message = "";

        public Profiles(string folder, Func<Sample?> latest, Func<PluginManager> manager, Func<bool> enabled)
        {
            path = Path.Combine(folder, "profils.json");
            this.latest = latest;
            this.manager = manager;
            this.enabled = enabled;
            try { if (System.IO.File.Exists(path)) File = JsonConvert.DeserializeObject<ProfileFile>(System.IO.File.ReadAllText(path)) ?? new ProfileFile(); } catch { }
            if (File.Cars == null) File.Cars = new Dictionary<string, List<Profile>>();
            if (File.Roles == null) File.Roles = new Dictionary<string, string>();
            if (File.Tracks == null) File.Tracks = new Dictionary<string, Dictionary<string, List<Profile>>>();
            if (File.LastTrack == null) File.LastTrack = new Dictionary<string, string>();
            if (File.From == null) File.From = new Dictionary<string, Dictionary<string, string>>();
        }

        public void Save()
        {
            lock (gate)
                try { System.IO.File.WriteAllText(path, JsonConvert.SerializeObject(File, Formatting.Indented)); } catch { }
        }

        // The car's profiles, made on first sight: Sec, Inter and Pluie with the usual wetness ranges, values empty until
        // memorised. Renamed, removed or added to on the plugin's screen.
        public List<Profile> For(string car)
        {
            if (string.IsNullOrEmpty(car)) return new List<Profile>();
            lock (gate)
            {
                if (!File.Cars.TryGetValue(car, out var list))
                {
                    list = new List<Profile>
                    {
                        new Profile { Name = "Sec", WetMin = 0, WetMax = 5 },
                        new Profile { Name = "Inter", WetMin = 5, WetMax = 12 },
                        new Profile { Name = "Pluie", WetMin = 12, WetMax = 100 },
                    };
                    File.Cars[car] = list;
                    Save();
                }
                return list;
            }
        }

        // The combo's profiles, made on first sight from the car's last memorised ones (another track), so a new track
        // never starts from zero. No track known: the car's own profiles.
        public List<Profile> For(string car, string track)
        {
            if (string.IsNullOrEmpty(track)) return For(car);
            if (string.IsNullOrEmpty(car)) return new List<Profile>();
            lock (gate)
            {
                if (!File.Tracks.TryGetValue(car, out var tracks)) File.Tracks[car] = tracks = new Dictionary<string, List<Profile>>();
                if (!tracks.TryGetValue(track, out var list))
                {
                    list = Copy(For(car));
                    tracks[track] = list;
                    if (!File.From.TryGetValue(car, out var from)) File.From[car] = from = new Dictionary<string, string>();
                    from[track] = File.LastTrack.TryGetValue(car, out var last) ? last ?? "" : "";
                    Save();
                }
                return list;
            }
        }

        static List<Profile> Copy(List<Profile> list) =>
            JsonConvert.DeserializeObject<List<Profile>>(JsonConvert.SerializeObject(list)) ?? new List<Profile>();

        // The tracks that have profiles for this car, A to Z.
        public List<string> TracksOf(string car)
        {
            lock (gate)
                return car != null && File.Tracks.TryGetValue(car, out var tracks) ? tracks.Keys.OrderBy(t => t, StringComparer.CurrentCultureIgnoreCase).ToList() : new List<string>();
        }

        // The track a combo's profiles were copied from, while not yet memorised there (null once memorised, "" = the car's
        // first profiles).
        public string FromOf(string car, string track)
        {
            lock (gate)
                return car != null && track != null && File.From.TryGetValue(car, out var from) && from.TryGetValue(track, out var t) ? t : null;
        }

        public List<Profile> Current => For(Car, Track);

        // The combo the screen shows: the one in LMU, else the last one seen (the game closed).
        public (string Car, string Track) Shown
        {
            get
            {
                if (Car != null) return (Car, Track);
                lock (gate)
                    return File.SeenCar != null ? (File.SeenCar, File.SeenTrack) : (File.Cars.Keys.LastOrDefault(), null);
            }
        }

        // Each reading of the game: the car driven, for the buttons and the screen.
        // Also in the garage and the menus with a car loaded: its profiles are made and can be memorised from there.
        public void Seen(Sample s)
        {
            if (string.IsNullOrEmpty(s.Car)) return;
            seen = s;
            string track = (s.Track ?? "").Trim();
            if (s.Car != Car || track != (Track ?? "")) { Car = s.Car; Track = track; Applied = null; }
            if (File.SeenCar != s.Car || (File.SeenTrack ?? "") != track) { File.SeenCar = s.Car; File.SeenTrack = track; Save(); }
        }
        volatile object seen;
        // The car as last read, on track or not: what « Mémoriser » takes.
        Sample? Last => latest() ?? (seen is Sample s ? s : (Sample?)null);

        // ── The wheel buttons ────────────────────────────────────────────────────────────────────────────────────────
        // Pressed: a timer starts; still held after 2 s, the profile memorises the car's state. Released before: a first
        // press shows in the popup what the profile would change; the same button again within 5 s applies it.
        public void Press(int slot)
        {
            if (!enabled() || slot < 0 || slot >= Slots) return;
            Report.Log("bouton", "Profil" + (slot + 1) + " appuyé");
            pressedAt[slot] = DateTime.UtcNow;
            holds[slot]?.Dispose();
            holds[slot] = new Timer(_ => { pressedAt[slot] = DateTime.MinValue; pending = -1; Memorise(slot); }, null, HoldMs, Timeout.Infinite);
        }

        public void Release(int slot)
        {
            if (!enabled() || slot < 0 || slot >= Slots) return;
            holds[slot]?.Dispose();
            holds[slot] = null;
            if (pressedAt[slot] == DateTime.MinValue) { Report.Log("bouton", "Profil" + (slot + 1) + " relâché après 2 s : mémorisé"); return; }
            pressedAt[slot] = DateTime.MinValue;
            if (pending == slot && DateTime.UtcNow < pendingUntil)
            {
                pending = -1;
                Report.Log("bouton", "Profil" + (slot + 1) + " : 2e appui, appliqué");
                Apply(slot);
                return;
            }
            Report.Log("bouton", "Profil" + (slot + 1) + " : 1er appui, aperçu");
            // The pit menu answers over HTTP: read aside, never on SimHub's thread.
            ThreadPool.QueueUserWorkItem(_ => Ask(slot));
        }

        // ── The popup ────────────────────────────────────────────────────────────────────────────────────────────────
        // What a first press shows: each setting the profile would change (now → profile), in the car and for the next stop.
        // Shown by ProfilePopup.cs above the game and on the dashes (properties Profils.Popup.*).
        public class PopupView
        {
            public string Title = "";
            public List<(string Label, string Now, string Next)> Drive = new List<(string, string, string)>();
            public List<(string Label, string Now, string Next)> Pit = new List<(string, string, string)>();
            public string Help = "";
            // A confirmation is waiting: the bar empties until Until.
            public bool Confirm;
            public DateTime Since, Until;
        }
        public volatile PopupView Popup;
        public const int ConfirmMs = 5000;
        volatile int pending = -1;
        DateTime pendingUntil;

        void Show(string title, string help, double seconds, PopupView view = null)
        {
            view = view ?? new PopupView();
            view.Title = title; view.Help = help; view.Since = DateTime.UtcNow; view.Until = view.Since.AddSeconds(seconds);
            Popup = view;
        }

        public void Ask(int slot)
        {
            try
            {
                var list = Current;
                if (slot >= list.Count) { Message = "Pas de profil n° " + (slot + 1) + " pour cette voiture."; Show("Profil" + (slot + 1), Message, 3); return; }
                var profile = list[slot];
                if (profile.Drive.Count == 0 && profile.PitApplied.Count == 0)
                {
                    Message = $"« {profile.Name} » est vide : maintiens le bouton 2 s pour le mémoriser.";
                    Show($"« {profile.Name} » est vide", "Maintiens le bouton 2 s pour mémoriser tes réglages.", 4);
                    return;
                }
                var view = Preview(profile, Last, ReadMenuTexts());
                if (view.Drive.Count == 0 && view.Pit.Count == 0) { pending = -1; Show($"« {profile.Name} » déjà en place", "Rien à changer.", 3, view); return; }
                view.Confirm = true;
                pendingUntil = DateTime.UtcNow.AddMilliseconds(ConfirmMs);
                pending = slot;
                Show($"« {profile.Name} »", "Rappuie sur le même bouton pour confirmer.", ConfirmMs / 1000.0, view);
            }
            catch { pending = -1; }
        }

        // The lines that differ between the car as read now and the profile. A setting the game does not show is listed
        // with « ? » now; the pit menu unread (game not answering) lists every line the profile applies.
        public static PopupView Preview(Profile profile, Sample? car, Dictionary<string, string> menu)
        {
            var view = new PopupView();
            foreach (var setting in Setting.All)
            {
                if (!profile.Drive.TryGetValue(setting.Key, out double target)) continue;
                double? now = car is Sample s ? setting.Read(s) : null;
                if (now is double value && Math.Abs(value - target) <= setting.Tolerance) continue;
                view.Drive.Add((setting.Label, now is double v ? Format(setting, v) : "?", Format(setting, target)));
            }
            foreach (var name in profile.PitApplied)
            {
                if (!profile.Pit.TryGetValue(name, out string text)) continue;
                string now = null;
                if (menu != null && menu.TryGetValue(name, out now) && now == text) continue;
                view.Pit.Add((Pretty(name), now ?? "?", text));
            }
            return view;
        }

        static string Format(Setting setting, double value) =>
            setting.Key == "Bias" ? value.ToString("0.0", System.Globalization.CultureInfo.GetCultureInfo("fr-FR")) + " %" : value.ToString("0");

        // The pit menu lines with the names of the in-game « Arrêt aux stands » in French (screenshots by Nathan, 24/11/2026).
        // DAMAGE and DRIVER are not shown in that menu: names of ours.
        static readonly Dictionary<string, string> PitNames = new Dictionary<string, string>
        {
            ["DAMAGE"] = "Dégâts", ["DRIVER"] = "Pilote", ["VIRTUAL ENERGY"] = "Énergie Virtuelle", ["FUEL RATIO"] = "Ratio carburant",
            ["TIRES"] = "Pneus", ["FL TIRE"] = "Pneu AvG", ["FR TIRE"] = "Pneu AvD", ["RL TIRE"] = "Pneu ArrG", ["RR TIRE"] = "Pneu ArrD",
            ["R WING"] = "Aileron", ["GRILLE"] = "Grille",
            ["FL PRESS"] = "Pression AvG", ["FR PRESS"] = "Pression AvD", ["RL PRESS"] = "Pression ArrG", ["RR PRESS"] = "Pression ArrD",
            ["F BRAKE DUCT"] = "Conduit frein avant", ["R BRAKE DUCT"] = "Conduit frein arr", ["REPLACE BRAKES"] = "Remplacer les freins",
        };

        public static string Pretty(string name) =>
            PitNames.TryGetValue(name ?? "", out var fr) ? fr : string.IsNullOrEmpty(name) ? "" : name.Substring(0, 1) + name.Substring(1).ToLowerInvariant();

        // ── Memorise ─────────────────────────────────────────────────────────────────────────────────────────────────
        // The car's settings as they are now, and the pit menu as it is set, into the profile at this place.
        public void Memorise(int slot)
        {
            var list = Current;
            if (slot >= list.Count) { Message = "Pas de profil n° " + (slot + 1) + " pour cette voiture."; return; }
            if (!(Last is Sample s)) { Message = "Mémorisation impossible : lance une séance avec la voiture."; return; }
            var profile = list[slot];
            var drive = new Dictionary<string, double>();
            foreach (var setting in Setting.All)
                if (setting.Read(s) is double value) drive[setting.Key] = value;
            var indexes = new Dictionary<string, int>();
            var pit = ReadMenuTexts(indexes);
            lock (gate)
            {
                profile.Drive = drive;
                if (pit != null)
                {
                    bool first = profile.Pit.Count == 0;
                    profile.Pit = pit;
                    profile.PitIndex = indexes;
                    // The first time, every line applies except the damage and the driver; later, the pilot's own choice stays.
                    if (first) profile.PitApplied = pit.Keys.Where(name => !PitOffByDefault.Contains(name)).ToList();
                }
                // The combo is now set on this track: the car's next new track starts from these profiles.
                string car = Car, track = Track;
                if (!string.IsNullOrEmpty(car) && !string.IsNullOrEmpty(track))
                {
                    File.Cars[car] = Copy(list);
                    File.LastTrack[car] = track;
                    if (File.From.TryGetValue(car, out var from)) from.Remove(track);
                }
            }
            Save();
            Applied = profile.Name;
            Message = $"« {profile.Name} » mémorisé" + (pit == null ? " (menu des stands non lu)." : ".");
            Show($"« {profile.Name} » mémorisé", pit == null ? "Menu des stands non lu : seuls les réglages en roulant sont pris." : "Réglages en roulant et menu des stands pris.", 3);
        }

        // ── Apply ────────────────────────────────────────────────────────────────────────────────────────────────────
        // One at a time, on its own thread: a press waits for the game to show the new value before the next.
        public void Apply(int slot)
        {
            var list = Current;
            if (slot >= list.Count) { Message = "Pas de profil n° " + (slot + 1) + " pour cette voiture."; return; }
            var profile = list[slot];
            if (profile.Drive.Count == 0 && profile.PitApplied.Count == 0) { Message = $"« {profile.Name} » est vide : maintiens le bouton 2 s pour le mémoriser."; return; }
            pending = -1;
            if (Interlocked.Exchange(ref busy, 1) == 1) return;
            ThreadPool.QueueUserWorkItem(_ =>
            {
                try
                {
                    Message = $"« {profile.Name} » en cours…";
                    Show($"« {profile.Name} » en cours…", "", 20);
                    var problems = new List<string>();
                    foreach (var setting in Setting.All)
                        if (profile.Drive.TryGetValue(setting.Key, out double target)) { string problem = Reach(setting, target); if (problem != null) problems.Add(problem); }
                    if (profile.PitApplied.Count > 0) { string problem = WriteMenu(profile); if (problem != null) problems.Add(problem); }
                    Applied = profile.Name;
                    Message = problems.Count == 0 ? $"« {profile.Name} » appliqué." : $"« {profile.Name} » appliqué sauf : " + string.Join(" · ", problems);
                    // Same look as the first press: what is still not as the profile, line by line (now → profile), not a
                    // paragraph of role names (Nathan 23/11). The reasons stay on the plugin's screen (Message).
                    if (problems.Count == 0) Show($"« {profile.Name} » appliqué", "", 2);
                    else Show($"« {profile.Name} » appliqué en partie", "Pas changé : détails sur l'écran du plugin.", 6, Preview(profile, latest(), profile.PitApplied.Count > 0 ? ReadMenuTexts() : null));
                }
                catch { Message = $"« {profile.Name} » : erreur, rien de plus n'a été changé."; Show($"« {profile.Name} » : erreur", "Rien de plus n'a été changé.", 4); }
                finally { busy = 0; }
            });
        }

        // One way only (Nathan 24/11, choice A): FMT companion's own command, as the black box's Électronique page sends
        // it. It knows which button LMU listens to, its own or the pilot's one that « Préparer LMU » reused, and holds it
        // 200 ms; nothing to link in the Control Mapper for the profiles. The game is read, the clicks needed are sent one
        // by one (6 → 4 = two « − »), each read back; a click that moves nothing twice stops, said with the values.
        string Reach(Setting setting, double target)
        {
            if (SendFmt == null) return setting.Label + " (moteur FMT absent)";
            // A « + » can lower the game's level on some cars (TC counted the other way): seen once, then swapped.
            bool swapped = false;
            for (int presses = 0, still = 0; presses < 60; presses++)
            {
                if (!(latest() is Sample s)) return setting.Label + " (voiture plus en piste)";
                if (!(setting.Read(s) is double now)) return null;
                if (Math.Abs(now - target) < setting.Tolerance) return null;
                string id = FmtId(setting.Key + ((target > now) != swapped ? "+" : "-"));
                if (id == null) return setting.Label + " (pas de commande)";
                string why = SendFmt(id);
                if (why != null) return setting.Label + " (" + why + ")";
                // The game takes a moment to show the new level: up to 1.2 s, then the next click.
                double? after = now;
                for (int wait = 0; wait < 15 && after == now; wait++) { Thread.Sleep(80); after = latest() is Sample t ? setting.Read(t) : null; }
                still = after == now ? still + 1 : 0;
                if (after is double moved && Math.Abs(moved - target) > Math.Abs(now - target)) swapped = !swapped;
                if (still >= 2)
                {
                    Report.Log("profils", setting.Label + " : « " + id + " » sans effet, " + Format(setting, now) + " au lieu de " + Format(setting, target));
                    return setting.Label + " (reste à " + Format(setting, now) + " au lieu de " + Format(setting, target) + " : « Préparer LMU » dans Black box sur boutons)";
                }
            }
            return setting.Label + " (pas atteint)";
        }

        // FMT companion's command by its id (« TC.Next »…): null when sent, else why not. Set by the plugin.
        public Func<string, string> SendFmt;
        // FMT companion runs (started with the plugin while this module is on).
        public Func<bool> FmtRunning;
        public bool AnyRole => FmtRunning?.Invoke() == true;

        // FMT companion's command for a setting and a sign (its Catalog.cs ids), null when it has none.
        static string FmtId(string command)
        {
            bool up = command.EndsWith("+");
            switch (command.Substring(0, command.Length - 1))
            {
                case "TC": return up ? "TC.Next" : "TC.Previous";
                case "TCCut": return up ? "TC2.Next" : "TC2.Previous";
                case "TCSlip": return up ? "TCSlip.Next" : "TCSlip.Previous";
                case "ABS": return up ? "ABS.Next" : "ABS.Previous";
                case "Bias": return up ? "Bias.Forward" : "Bias.Rearward";
                case "Migration": return up ? "Migration.Forward" : "Migration.Rearward";
                case "MotorMap": return up ? "MotorMap.Next" : "MotorMap.Previous";
                case "ArbFront": return up ? "FrontARB.Next" : "FrontARB.Previous";
                case "ArbRear": return up ? "RearARB.Next" : "RearARB.Previous";
                default: return null;
            }
        }

        // The pit menu as the game sends it: name of each line → text of its choice. Null when the game does not answer.
        Dictionary<string, string> ReadMenuTexts(Dictionary<string, int> indexes = null)
        {
            try
            {
                var lines = JArray.Parse(Tape.Get(client, MenuUrl));
                var texts = new Dictionary<string, string>();
                foreach (var line in lines.OfType<JObject>())
                {
                    string name = Name(line), text = Choice(line, (int?)line["currentSetting"] ?? -1);
                    if (name != "" && text != null) { texts[name] = text; if (indexes != null) indexes[name] = (int?)line["currentSetting"] ?? -1; }
                }
                return texts;
            }
            catch { return null; }
        }

        // The menu is read, the chosen lines set to the profile's choice (found by its text: the game's indexes change with
        // the car), sent back whole, then read again to check.
        // A choice whose text changed (energy or fuel shown with laps, wing…) is found again by its place when memorised.
        // Lines that hang together (virtual energy and fuel ratio) can be refused once: a second send for what is left.
        string WriteMenu(Profile profile)
        {
            try
            {
                var missing = new List<string>();
                var wanted = new Dictionary<string, int>();
                for (int round = 0; round < 2; round++)
                {
                    var lines = JArray.Parse(Tape.Get(client, MenuUrl));
                    wanted.Clear();
                    foreach (var line in lines.OfType<JObject>())
                    {
                        string name = Name(line);
                        if (!profile.PitApplied.Contains(name) || !profile.Pit.TryGetValue(name, out string text)) continue;
                        var settings = line["settings"] as JArray;
                        int count = settings?.Count ?? 0, index = -1;
                        for (int i = 0; i < count; i++) if (Choice(line, i) == text) { index = i; break; }
                        if (index < 0 && profile.PitIndex != null && profile.PitIndex.TryGetValue(name, out int kept) && kept >= 0 && kept < count) index = kept;
                        if (index < 0) { if (round == 0) missing.Add(name); continue; }
                        if (((int?)line["currentSetting"] ?? -1) != index) { line["currentSetting"] = index; wanted[name] = index; }
                    }
                    if (wanted.Count == 0) break;
                    var body = new StringContent(lines.ToString(Formatting.None), Encoding.UTF8, "application/json");
                    client.PostAsync(LoadUrl, body).GetAwaiter().GetResult().EnsureSuccessStatusCode();
                    for (int check = 0; check < 4 && wanted.Count > 0; check++)
                    {
                        Thread.Sleep(250);
                        foreach (var line in JArray.Parse(Tape.Get(client, MenuUrl)).OfType<JObject>())
                            if (wanted.TryGetValue(Name(line), out int index) && ((int?)line["currentSetting"] ?? -1) == index) wanted.Remove(Name(line));
                    }
                    if (wanted.Count == 0) break;
                }
                missing.AddRange(wanted.Keys);
                return missing.Count == 0 ? null : "stand : " + string.Join(", ", missing.Select(x => x.ToLowerInvariant()));
            }
            catch { return "menu des stands (le jeu ne répond pas)"; }
        }

        static string Name(JObject line) => ((string)line["name"] ?? "").Replace(":", "").Trim().ToUpperInvariant();

        static string Choice(JObject line, int index)
        {
            var settings = line["settings"] as JArray;
            return settings != null && index >= 0 && index < settings.Count ? ((string)settings[index]?["text"] ?? "").Trim() : null;
        }

        // ── Reminders, for the dashs ─────────────────────────────────────────────────────────────────────────────────
        // The profile whose in-car settings are all what the car has now: the one applied first, else the first that fits.
        public string Active
        {
            get
            {
                if (!(latest() is Sample s)) return null;
                var list = Current;
                bool Fits(Profile p) => p.Drive.Count > 0 && Setting.All.All(x => !p.Drive.TryGetValue(x.Key, out double v) || !(x.Read(s) is double now) || Math.Abs(now - v) < x.Tolerance);
                var applied = list.FirstOrDefault(p => p.Name == Applied);
                if (applied != null && Fits(applied)) return applied.Name;
                return list.FirstOrDefault(Fits)?.Name;
            }
        }

        // A profile was applied and the car no longer matches it (a setting changed by hand since).
        public bool NotActive => Applied != null && Active != Applied;

        // The track's wetness now (%), and the profile whose range holds it.
        public double? Wetness => latest() is Sample s && s.Wet >= 0 && s.Wet <= 1 ? Math.Round(s.Wet * 100, 0) : (double?)null;

        public string ForWeather
        {
            get
            {
                if (!(Wetness is double wet)) return null;
                return Current.FirstOrDefault(p => wet >= p.WetMin && (wet < p.WetMax || p.WetMax >= 100))?.Name;
            }
        }

        // The track no longer fits the active profile's range: time to change.
        public bool ToChange
        {
            get { string active = Active ?? Applied, weather = ForWeather; return active != null && weather != null && active != weather; }
        }

        public string NameAt(int slot) { var list = Current; return slot < list.Count ? list[slot].Name : ""; }
    }
}
