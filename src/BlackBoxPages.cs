// Les 7 pages de la black box de LMU sur l'écran du volant (BlackBoxDash.cs), d'après les captures de Nathan du
// 23/11/2026 : Relatif, Séance, Chrono, Pneus, Carburant, Électronique, Arrêt aux stands, dans l'ordre du jeu.
// ◂ ▸ changent de page (le tour dans les deux sens). Sur chaque page, les boutons font ce que fait la black box du jeu
// (Nathan) :
// - Arrêt aux stands : ▲ ▼ changent de ligne, − + la valeur (BlackBox, envoyée au jeu par son API) ;
// - Électronique : ▲ ▼ changent de ligne, − + passent par les commandes de FMT companion (TC, ABS, carto…), comme ses
//   boutons directs : le jeu reçoit la touche, rien n'est écrit à sa place ;
// - Chrono : − + passent de Delta à Secteur à Split, ▲ ▼ de Timing (meilleur tour, tour optimal) à Relatif (dernier
//   tour) ;
// - Carburant : − + passent de Virtual Energy à Carburant, ▲ un tour de plus pour l'objectif de sauvegarde, ▼ un de
//   moins ;
// - Relatif, Séance, Pneus : rien à régler.
// Le jeu ne donne ni ces pages ni leur état : tout est calculé ici avec ce que le plugin lit déjà (mémoire du jeu,
// Live.cs ; chiffres de course, Course.cs). Ce qui n'a pas été vu dans le jeu est marqué « à vérifier ».
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace EnduranceManager.SimHub
{
    // One line of a page as the dash draws it: a name on the left, a value on the right, or three columns (Chrono,
    // Carburant); a small header line; the chosen line (white and red); a line that − + adjust when touched; on Relatif,
    // the place, the class badge and our own car.
    public class BlackBoxRow
    {
        public string Nom = "", Valeur = "", C1 = "", C2 = "", C3 = "", Pos = "", Classe = "", Couleur = "";
        public bool Petit, Choisie, Reglable, Moi;
    }

    public class BlackBoxPages
    {
        public static readonly string[] Titles = { "Relatif", "Séance", "Chrono", "Pneus", "Carburant", "Électronique", "Arrêt aux stands" };
        public const int Relative = 0, Session = 1, Timing = 2, Tyres = 3, Fuel = 4, Electronics = 5, Stands = 6;
        static readonly string[] ChronoModes = { "Delta", "Secteur", "Split" };
        static readonly string[] Corners = { "AVG", "AVD", "ARG", "ARD" };
        const int ScoringInfo = 1632, VehScoring = 2192, VehSize = 584, MaxVehicles = 104;
        static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        readonly BlackBox stands;
        // A FMT companion command by its id (« TC.Next »…): null when sent, otherwise why not.
        readonly Func<string, string> command;
        readonly object gate = new object();

        volatile int page = Stands;
        int chrono;
        bool relative;
        bool? energy;
        int save;
        string elecChosen;
        int elecTop;

        // What the pages are made of, the last reading (10 a second while driving).
        Sample? sample;
        CourseView view;
        List<Car> cars = new List<Car>();
        readonly Laps laps = new Laps();
        volatile BlackBoxRow[] rows = Blank();
        volatile List<Setting> settings = new List<Setting>();
        string message = "";
        DateTime messageUntil;

        public BlackBoxPages(BlackBox stands, Func<string, string> command = null) { this.stands = stands; this.command = command; }

        public int Page => page;
        public string Message => DateTime.UtcNow < messageUntil ? message : stands.Message;

        static BlackBoxRow[] Blank() => Enumerable.Range(0, BlackBox.Rows).Select(_ => new BlackBoxRow()).ToArray();

        // ── The buttons ─────────────────────────────────────────────────────────────────────────────────────────────

        // ◂ ▸. Nathan (23/11): both buttons seemed to do the same thing. A second turn within 250 ms is taken as the same
        // press arriving twice and ignored; the presses received are listed in BlackBox.Appuis to see what SimHub sends.
        DateTime lastTurn;
        public void Turn(int step)
        {
            Note(step > 0 ? "page suivante" : "page précédente");
            lock (gate)
            {
                var now = DateTime.UtcNow;
                if ((now - lastTurn).TotalMilliseconds < 250) return;
                lastTurn = now;
                page = ((page + step) % Titles.Length + Titles.Length) % Titles.Length;
                // Back on Relatif: its cars read again at the next reading, they may be old.
                carsStale = true;
            }
            Rebuild();
        }

        // The last presses received (time, button), newest first.
        readonly List<string> presses = new List<string>();
        public void Note(string what)
        {
            lock (presses)
            {
                presses.Insert(0, DateTime.Now.ToString("HH:mm:ss.fff", Inv) + " " + what);
                if (presses.Count > 6) presses.RemoveAt(6);
            }
        }
        public string Presses { get { lock (presses) return string.Join(" · ", presses); } }

        // ▲ (-1) ▼ (+1).
        public void Move(int step)
        {
            Note(step < 0 ? "monter" : "descendre");
            switch (page)
            {
                case Stands: stands.Move(step); return;
                case Electronics: lock (gate) MoveSetting(step); break;
                case Timing: lock (gate) relative = !relative; break;
                // ▲ one lap more to save, ▼ one less.
                case Fuel: lock (gate) save = Math.Max(0, save - step); break;
                default: return;
            }
            Rebuild();
        }

        // − (-1) + (+1).
        public void Change(int step)
        {
            Note(step > 0 ? "plus" : "moins");
            switch (page)
            {
                case Stands: stands.Change(step); return;
                case Electronics: Press(step); return;
                case Timing: lock (gate) chrono = ((chrono + step) % ChronoModes.Length + ChronoModes.Length) % ChronoModes.Length; break;
                case Fuel: lock (gate) energy = !(energy ?? HasEnergy); break;
                default: return;
            }
            Rebuild();
        }

        // A touch on a line that is not adjustable: on Arrêt aux stands and Électronique, it becomes the chosen one.
        public void Select(int row)
        {
            if (page == Stands) { stands.Select(row); return; }
            if (page != Electronics) return;
            lock (gate)
            {
                var list = settings; int k = elecTop + row;
                if (k < 0 || k >= list.Count) return;
                elecChosen = list[k].Key;
                if (row == BlackBox.Rows - 1 && elecTop + BlackBox.Rows < list.Count) elecTop++;
                else if (row == 0 && elecTop > 0) elecTop--;
            }
            Rebuild();
        }

        // A touch on the left (-1) or right (+1) half of an adjustable line.
        public void Touch(int row, int step)
        {
            switch (page)
            {
                case Stands:
                case Electronics: Change(step); return;
                case Timing: if (row == 0) Change(step); else if (row == 2) Move(step); return;
                // The goal line: the right half one lap more, the left half one less (as ▲ ▼).
                case Fuel: if (row == 0) Change(step); else if (row == 1) Move(-step); return;
            }
        }

        void Say(string text) { message = text ?? ""; messageUntil = DateTime.UtcNow.AddSeconds(3); }

        // ── Électronique ────────────────────────────────────────────────────────────────────────────────────────────

        // One setting of the car: its name as the game's page shows it, its value, and FMT companion's commands for − and +.
        public class Setting { public string Key, Name, Value, Down, Up; }

        void MoveSetting(int step)
        {
            var list = settings;
            if (list.Count == 0) return;
            int at = Math.Max(0, list.FindIndex(x => x.Key == elecChosen));
            elecChosen = list[Math.Max(0, Math.Min(list.Count - 1, at + step))].Key;
            FollowSetting();
        }

        void FollowSetting()
        {
            var list = settings;
            int at = list.FindIndex(x => x.Key == elecChosen);
            if (at < 0) { elecChosen = list.FirstOrDefault()?.Key; at = 0; }
            if (at < elecTop) elecTop = at;
            if (at >= elecTop + BlackBox.Rows) elecTop = at - BlackBox.Rows + 1;
            elecTop = Math.Max(0, Math.Min(elecTop, list.Count - BlackBox.Rows));
        }

        void Press(int step)
        {
            var setting = settings.FirstOrDefault(x => x.Key == elecChosen);
            if (setting == null) return;
            string why = command == null ? "FMT companion absent" : command(step > 0 ? setting.Up : setting.Down);
            if (why != null) Say(why);
        }

        // The car's settings in the game's order (Nathan's capture), those the car has (a number of steps above 0).
        public static List<Setting> Settings(Sample s, CourseView v)
        {
            var list = new List<Setting>();
            void Add(bool has, string key, string name, string value, string down, string up)
            {
                if (has) list.Add(new Setting { Key = key, Name = name, Value = value, Down = down, Up = up });
            }
            string N(int value) => value.ToString(Inv);
            Add(s.TCMax > 0, "TC", "Contrôle de traction", N(s.TC), "TC.Previous", "TC.Next");
            Add(s.TCCutMax > 0, "TC2", "TC Power cut", N(s.TCCut), "TC2.Previous", "TC2.Next");
            Add(s.TCSlipMax > 0, "TCSlip", "TC Slip angle", N(s.TCSlip), "TCSlip.Previous", "TCSlip.Next");
            Add(s.ABSMax > 0, "ABS", "ABS", N(s.ABS), "ABS.Previous", "ABS.Next");
            // « P4 », « P1 » in the game: the level from 1 (to check in game).
            Add(s.AntiSwayFrontMax > 0, "FrontARB", "ARB avant", "P" + N(s.AntiSwayFront + 1), "FrontARB.Previous", "FrontARB.Next");
            Add(s.AntiSwayRearMax > 0, "RearARB", "ARB arrière", "P" + N(s.AntiSwayRear + 1), "RearARB.Previous", "RearARB.Next");
            if (s.RearBrakeBias > 0 && s.RearBrakeBias < 1)
            {
                double front = Math.Round((1 - s.RearBrakeBias) * 100, 1, MidpointRounding.AwayFromZero);
                Add(true, "Bias", "Répartition des freins", front.ToString("0.0", Inv) + ":" + (100 - front).ToString("0.0", Inv), "Bias.Rearward", "Bias.Forward");
            }
            // The game writes « 1.5% F », « 120kW », « 200kW »: the memory only has the level (to refine).
            Add(s.MigrationMax > 0, "Migration", "Répartition des freins", N(s.Migration), "Migration.Rearward", "Migration.Forward");
            Add(s.MotorMapMax > 0, "MotorMap", "Cartographie moteur", N(s.MotorMap), "MotorMap.Previous", "MotorMap.Next");
            Add(s.MotorState != 0, "Regen", "Niveau de recharge", v?.RegenLevel is int regen ? N(regen) : "-", "Regen.Previous", "Regen.Next");
            return list;
        }

        // ── Reading ─────────────────────────────────────────────────────────────────────────────────────────────────

        // Each reading of the game's memory while driving (Plugin.cs), with the race figures of the last second.
        // The cars are read again only for Relatif and only when the game wrote new standings (its scoring clock moved,
        // about 5 times a second): the same lines from the same figures are not worked out twice.
        double carsET = double.NaN;
        volatile bool carsStale = true;

        public void Update(byte[] b, Sample s, CourseView v)
        {
            double et = b != null && b.Length >= ScoringInfo + 76 ? F64(b, ScoringInfo + 68) : double.NaN;
            bool onRelative = page == Relative;
            bool fresh = onRelative && (carsStale || double.IsNaN(et) || et != carsET);
            var field = fresh ? Cars(b, s) : null;
            lock (gate)
            {
                sample = s; view = v;
                if (field != null) { cars = field; carsET = et; carsStale = false; }
                laps.Update(b, s);
            }
            if (!onRelative || fresh) Rebuild();
        }

        bool HasEnergy => sample is Sample s && s.Energy > 0;

        void Rebuild()
        {
            lock (gate)
            {
                var made = Blank();
                if (sample is Sample s)
                {
                    switch (page)
                    {
                        case Relative: RelativeRows(made, s); break;
                        case Session: SessionRows(made, s, view); break;
                        case Timing: TimingRows(made); break;
                        case Fuel: FuelRows(made, s, view); break;
                        case Electronics:
                            settings = Settings(s, view);
                            FollowSetting();
                            for (int k = 0; k < BlackBox.Rows && elecTop + k < settings.Count; k++)
                            {
                                var x = settings[elecTop + k];
                                made[k] = new BlackBoxRow { Nom = x.Name, Valeur = x.Value, Choisie = x.Key == elecChosen, Reglable = x.Key == elecChosen };
                            }
                            break;
                    }
                }
                rows = made;
            }
        }

        // ── Relatif ─────────────────────────────────────────────────────────────────────────────────────────────────

        public struct Car { public int Place; public string Name, Class; public double Dist; public bool Pits, Me; }

        static double F64(byte[] b, int at) => BitConverter.ToDouble(b, at);
        static int I32(byte[] b, int at) => BitConverter.ToInt32(b, at);
        static string Text(byte[] b, int at, int size)
        {
            int end = Array.IndexOf(b, (byte)0, at, size);
            return Encoding.UTF8.GetString(b, at, (end < 0 ? at + size : end) - at).Trim();
        }

        static List<Car> Cars(byte[] b, Sample s)
        {
            var list = new List<Car>();
            if (b == null || b.Length < VehScoring + MaxVehicles * VehSize) return list;
            int count = Math.Min(I32(b, ScoringInfo + 104), MaxVehicles);
            for (int i = 0; i < count; i++)
            {
                int at = VehScoring + i * VehSize;
                list.Add(new Car
                {
                    Place = b[at + 199], Name = Text(b, at + 4, 32), Class = Text(b, at + 200, 32), Dist = F64(b, at + 104),
                    Pits = b[at + 198] != 0 || b[at + 507] != 0, Me = I32(b, at) == s.Id
                });
            }
            return list;
        }

        // « Nathan Boulanger » as the game writes it: « N Boulanger ».
        public static string Short(string name)
        {
            var parts = (name ?? "").Trim().Split(new[] { ' ' }, 2, StringSplitOptions.RemoveEmptyEntries);
            return parts.Length < 2 ? (name ?? "").Trim() : parts[0].Substring(0, 1) + " " + parts[1];
        }

        // The class badge and its colour (Hypercar red as in the capture; the others to check in game).
        public static (string badge, string color) Badge(string cls)
        {
            string c = (cls ?? "").ToLowerInvariant();
            if (c.Contains("hyper")) return ("HY", "#FFE83A4C");
            if (c.Contains("lmp2") || c.Contains("p2")) return ("P2", "#FF2F7BEB");
            if (c.Contains("lmp3") || c.Contains("p3")) return ("P3", "#FF8E44D6");
            if (c.Contains("gt3")) return ("GT3", "#FF22A84A");
            if (c.Contains("gte")) return ("GTE", "#FF2DB45A");
            return (c.Length > 3 ? c.Substring(0, 3).ToUpperInvariant() : c.ToUpperInvariant(), "#FF8A909C");
        }

        // The cars around us on the track, our car on the 4th line: the three next ahead above, the three next behind below,
        // each car once on each side as the game does (with few cars the same one shows above and below: its time ahead
        // plus its time behind make a lap). The time is the distance on the lap at the pace of a lap; ahead « -54.6 »,
        // behind « +36.3 », the place in the class, « PIT » in the middle (Nathan's captures).
        void RelativeRows(BlackBoxRow[] made, Sample s)
        {
            var me = cars.FirstOrDefault(c => c.Me);
            if (me.Name == null) return;
            double length = view?.TrackLength > 100 ? view.TrackLength.Value : 0;
            if (length <= 0) length = Math.Max(1000, cars.Max(c => c.Dist));
            double lap = view?.LapTimeAvg10 > 0 ? view.LapTimeAvg10.Value : view?.BestLap > 0 ? view.BestLap.Value : s.LastLap > 0 ? s.LastLap : 120;
            // How far ahead on the lap, 0 to 1.
            double Ahead(Car c) { double d = (c.Dist - me.Dist) / length; return d - Math.Floor(d); }
            var others = cars.Where(c => !c.Me).ToList();
            var ahead = others.Select(c => (car: c, gap: Ahead(c) * lap)).OrderBy(o => o.gap).Take(3).ToList();
            var behind = others.Select(c => (car: c, gap: (1 - Ahead(c)) * lap)).OrderBy(o => o.gap).Take(3).ToList();
            int InClass(Car c) => 1 + cars.Count(o => o.Class == c.Class && o.Place > 0 && o.Place < c.Place);
            void Put(int k, Car c, string gap)
            {
                var (badge, color) = Badge(c.Class);
                made[k] = new BlackBoxRow
                {
                    Pos = InClass(c).ToString(Inv), Classe = badge, Couleur = color, Nom = Short(c.Name), Moi = c.Me,
                    C2 = c.Pits ? "PIT" : "", Valeur = gap
                };
            }
            for (int i = 0; i < ahead.Count; i++) Put(2 - i, ahead[i].car, "-" + ahead[i].gap.ToString("0.0", Inv));
            Put(3, me, "");
            for (int i = 0; i < behind.Count; i++) Put(4 + i, behind[i].car, "+" + behind[i].gap.ToString("0.0", Inv));
        }

        // ── Séance ──────────────────────────────────────────────────────────────────────────────────────────────────

        static readonly Dictionary<string, string> Skies = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["clear"] = "Dégagé", ["light clouds"] = "Légèrement nuageux", ["partially cloudy"] = "Partiellement nuageux",
            ["mostly cloudy"] = "Nuageux", ["overcast"] = "Couvert", ["cloudy"] = "Nuageux", ["light rain"] = "Pluie légère",
            ["rain"] = "Pluie", ["heavy rain"] = "Forte pluie", ["storm"] = "Orage", ["drizzle"] = "Bruine"
        };

        static string Sky(object text)
        {
            string t = (text as string ?? "").Trim();
            return Skies.TryGetValue(t, out var fr) ? fr : t;
        }

        static string Deg(double? c) => c is double v && !double.IsNaN(v) ? v.ToString("0.0", Inv) + "°C" : "";

        static string Join(string a, string b) => a != "" && b != "" ? a + ", " + b : a + b;

        void SessionRows(BlackBoxRow[] made, Sample s, CourseView v)
        {
            var weather = v?.Weather ?? new Dictionary<string, object>();
            object W(string key) => weather.TryGetValue(key, out var x) ? x : null;
            string clock = s.TimeOfDay >= 0 && s.TimeOfDay <= 86400 ? TimeSpan.FromSeconds(s.TimeOfDay).ToString(@"hh\:mm", Inv) : "";
            string now = Sky(W("Weather.CURRENTNODE_WNV_SKYStr"));
            // The next point of the forecast after the current one.
            string[] nodes = { "START", "NODE_25", "NODE_50", "NODE_75", "FINISH" };
            int at = Array.IndexOf(nodes, W("Weather.CurrentNodeName") as string);
            string next = at >= 0 && at + 1 < nodes.Length ? nodes[at + 1] : null;
            string forecast = next == null ? "" : Join(Sky(W($"Weather.{next}_WNV_SKYStr")), W($"Weather.{next}_WNV_TEMPERATURE") is double t ? Deg(t) : "");
            // Grip as the game counts it (to check in game: 0 green to 3 high), wet track first.
            string[] grips = { "Piste verte", "Adhérence faible", "Adhérence moyenne", "Adhérence haute" };
            string grip = s.WetAverage > 0.05 ? "Piste humide" : s.TrackGrip >= 0 && s.TrackGrip < grips.Length ? grips[s.TrackGrip] : "";
            double? points = v?.TrackLimitsPoints;
            object allowed = v?.Session != null && v.Session.TryGetValue("TrackLimits.PointsAllowed", out var p) ? p : null;
            string limit = allowed is double a && a > 0 ? a.ToString("0", Inv) : "∞";
            var penalties = new List<string>();
            if (v?.PenaltiesSG > 0) penalties.Add("Stop/Go" + (v.PenaltiesSG > 1 ? " ×" + v.PenaltiesSG : ""));
            if (v?.PenaltiesDT > 0) penalties.Add("Drive-through" + (v.PenaltiesDT > 1 ? " ×" + v.PenaltiesDT : ""));
            if (v?.PenaltiesTime > 0) penalties.Add("Temps" + (v.PenaltiesTime > 1 ? " ×" + v.PenaltiesTime : ""));
            if (penalties.Count == 0 && v?.Penalties > 0) penalties.Add(v.Penalties.ToString());
            string[,] lines =
            {
                { "Horloge", clock }, { "Météo", Join(now, Deg(s.AirTemp)) }, { "Prévisions", forecast },
                { "Circuit", Join(grip, Deg(s.TrackTemp)) }, { "Raccourcis", (points is double q ? Math.Floor(q).ToString("0", Inv) : "0") + "/" + limit },
                { "Pénalités", string.Join(", ", penalties) }
            };
            for (int k = 0; k < lines.GetLength(0); k++) made[k] = new BlackBoxRow { Nom = lines[k, 0], Valeur = lines[k, 1] };
        }

        // ── Chrono ──────────────────────────────────────────────────────────────────────────────────────────────────

        // Our sectors: the lap being driven, the last lap, the best lap and the best of each sector this session.
        public class Laps
        {
            public double?[] Current = new double?[3], Last = new double?[3], Best = new double?[3], Optimal = new double?[3];
            double bestLap;
            int seenLaps = -1, session = -1;
            string track;

            public void Update(byte[] b, Sample s)
            {
                if (s.Session != session || s.Track != track || s.LapsDone < seenLaps)
                {
                    Current = new double?[3]; Last = new double?[3]; Best = new double?[3]; Optimal = new double?[3];
                    bestLap = 0; seenLaps = s.LapsDone; session = s.Session; track = s.Track;
                }
                if (s.LapsDone != seenLaps)
                {
                    seenLaps = s.LapsDone;
                    var lap = Split(s.LastSector1, s.LastSector2, s.LastLap);
                    if (lap != null)
                    {
                        Last = lap;
                        for (int i = 0; i < 3; i++) if (Optimal[i] == null || lap[i] < Optimal[i]) Optimal[i] = lap[i];
                        if (bestLap <= 0 || s.LastLap < bestLap) { bestLap = s.LastLap; Best = lap; }
                    }
                }
                // The lap being driven: the sectors done (the game counts 1, 2, then 0 for the last one).
                int me = -1, count = b != null && b.Length > ScoringInfo + 108 ? Math.Min(I32(b, ScoringInfo + 104), MaxVehicles) : 0;
                for (int i = 0; i < count; i++) if (I32(b, VehScoring + i * VehSize) == s.Id) { me = VehScoring + i * VehSize; break; }
                var now = new double?[3];
                if (me >= 0)
                {
                    double c1 = F64(b, me + 176), c2 = F64(b, me + 184);
                    if (s.Sector != 1 && c1 > 0) now[0] = c1;
                    if (s.Sector == 0 && c2 > c1 && c1 > 0) now[1] = c2 - c1;
                }
                Current = now;
            }

            static double?[] Split(double end1, double end2, double lap) =>
                end1 > 0 && end2 > end1 && lap > end2 ? new double?[] { end1, end2 - end1, lap - end2 } : null;
        }

        public static string Time(double? t)
        {
            if (!(t is double v) || v <= 0 || double.IsNaN(v)) return "-";
            return v < 60 ? v.ToString("0.000", Inv) : ((int)(v / 60)).ToString(Inv) + ":" + (v % 60).ToString("00.000", Inv);
        }

        static string Delta(double? a, double? b) =>
            a is double x && b is double y ? (x - y >= 0 ? "+" : "-") + Math.Abs(x - y).ToString("0.000", Inv) : "-";

        static double?[] Sum(double?[] s)
        {
            var r = new double?[3]; double? run = 0;
            for (int i = 0; i < 3; i++) { run = run != null && s[i] != null ? run + s[i] : null; r[i] = run; }
            return r;
        }

        // Delta: the lap being driven, then the gap of each sector to the reference lines. Secteur: each sector's time.
        // Split: the time at the end of each sector. Timing: best lap and optimal lap; Relatif: last lap (to check in
        // game: the capture only shows empty times).
        void TimingRows(BlackBoxRow[] made)
        {
            int mode = chrono;
            made[0] = new BlackBoxRow { Nom = "◂ ▸  " + ChronoModes[mode], C1 = "S1", C2 = "S2", C3 = "S3", Petit = true, Reglable = true };
            var refs = relative ? new[] { ("Dernier", laps.Last) } : new[] { ("Meilleur", laps.Best), ("Optimal", laps.Optimal) };
            BlackBoxRow Line(string name, Func<int, string> cell) => new BlackBoxRow { Nom = name, C1 = cell(0), C2 = cell(1), C3 = cell(2) };
            var cur = laps.Current;
            made[1] = mode == 2 ? Line("Actuel", i => Time(Sum(cur)[i])) : Line("Actuel", i => Time(cur[i]));
            made[2] = new BlackBoxRow { Nom = "▴ ▾  " + (relative ? "Relatif" : "Timing"), Petit = true, Reglable = true };
            for (int r = 0; r < refs.Length; r++)
            {
                var (name, lap) = refs[r];
                made[3 + r] = mode == 0 ? Line(name, i => cur[i] != null ? Delta(cur[i], lap[i]) : Time(lap[i]))
                    : mode == 2 ? Line(name, i => Time(Sum(lap)[i])) : Line(name, i => Time(lap[i]));
            }
        }

        // ── Carburant ───────────────────────────────────────────────────────────────────────────────────────────────

        // What is left (litres or virtual energy %) lasts so many laps at each usage: flat out (the higher of the last lap
        // and the average of five), the estimate (average of five), the last lap. With a goal of N laps more, the usage
        // per lap that reaches it, and a warning on the estimate when it falls short.
        void FuelRows(BlackBoxRow[] made, Sample s, CourseView v)
        {
            bool ve = energy ?? HasEnergy;
            if (ve && !HasEnergy) ve = false;
            double left = ve ? s.Energy : s.Fuel;
            double? last = ve ? v?.VeLastLap : v?.FuelLastLap, avg = ve ? v?.VeAvg5 : v?.FuelAvg5;
            double? push = last > 0 && avg > 0 ? Math.Max(last.Value, avg.Value) : avg > 0 ? avg : last;
            double lapTime = v?.LapTimeAvg10 > 0 ? v.LapTimeAvg10.Value : v?.LastLap > 0 ? v.LastLap.Value : 0;
            string Use(double? u) => u > 0 ? (ve ? u.Value.ToString("0.00", Inv) + "%" : u.Value.ToString("0.00", Inv) + "L") : "-";
            string LapsOf(double? n) => n > 0 ? n.Value.ToString("0.0", Inv) : "-";
            string Mins(double? n) => n > 0 && lapTime > 0 ? Math.Round(n.Value * lapTime / 60).ToString("0", Inv) + " mins" : "-";
            BlackBoxRow Line(string name, double? usage, double? n) => new BlackBoxRow { Nom = name, C1 = LapsOf(n), C2 = Mins(n), C3 = Use(usage) };
            double? Lasts(double? u) => u > 0 ? left / u : null;

            made[0] = new BlackBoxRow { Nom = "◂ ▸  " + (ve ? "Virtual Energy" : "Carburant"), Valeur = ve ? s.Energy.ToString("0", Inv) + "%" : s.Fuel.ToString("0.00", Inv) + "L", Petit = true, Reglable = true };
            made[1] = new BlackBoxRow { Nom = "Objectif de sauvegarde", Valeur = (save > 0 ? "+" + save + " Tours" : "Aucun") + "  ▴▾", Reglable = true };
            made[2] = new BlackBoxRow { C1 = "Tours", C2 = "Temps", C3 = "Usage", Petit = true };
            double? estimate = Lasts(avg);
            if (save > 0 && Lasts(push) is double flat)
            {
                double goal = flat + save;
                made[3] = Line("Objectif", left / goal, goal);
                made[4] = Line(estimate < goal ? "Estimation  ⚠" : "Estimation", avg, estimate);
            }
            else
            {
                made[3] = Line("Pousser", push, Lasts(push));
                made[4] = Line("Estimation", avg, estimate);
            }
            made[5] = Line("Dernier", last, Lasts(last));
            made[6] = ve ? new BlackBoxRow { Nom = "Ratio carburant", Valeur = v?.RatioLastLap is double r ? r.ToString("0.00", Inv) : "-" }
                : new BlackBoxRow { Nom = "Batterie", Valeur = s.MotorState != 0 && s.Battery > 0 ? (s.Battery * 100).ToString("0", Inv) + "%" : "-" };
        }

        // ── Pneus ───────────────────────────────────────────────────────────────────────────────────────────────────

        // One wheel as the game's page shows it (0 front left … 3 rear right): what is left of the tyre, its compound's
        // letter, pressure, temperature, brake temperature; « Crevé » or « Perdue » instead (drawn our way: never seen in
        // the game).
        object Wheel(int i, string what)
        {
            if (!(sample is Sample s) || s.Wheels == null) return what == "Etat" ? "" : what == "Couleur" ? "#FFFFFFFF" : (object)"";
            var w = s.Wheels[i];
            string state = w.Off ? "Perdue" : w.Flat ? "Crevé" : "";
            switch (what)
            {
                case "Usure": return w.Off ? "—" : Math.Round(w.Wear * 100).ToString("0", Inv) + "%";
                case "Gomme": string c = i < 2 ? s.FrontCompound : s.RearCompound; return w.Off || string.IsNullOrEmpty(c) ? "" : c.Substring(0, 1).ToUpperInvariant();
                case "Pression": return w.Off ? "-" : w.Pressure.ToString("0.0", Inv) + "kPa";
                // The game shows about 2 °C above the carcass (Nathan's capture, 23/11): the inner layer's mean, to check.
                case "Temp": return w.Off ? "-" : ((w.InnerIn + w.InnerMid + w.InnerOut) / 3).ToString("0.0", Inv) + "°C";
                case "Frein": return w.Brake.ToString("0.0", Inv) + "°C";
                case "Etat": return state;
                // The ring: white, red when the tyre is gone (worn out, flat or lost).
                case "Couleur": return state != "" || w.Wear < 0.005 ? "#FFE83A4C" : "#FFFFFFFF";
            }
            return "";
        }

        // « ∞ Pneus » as in the game when the session does not limit the sets (practice); otherwise the new sets left.
        string Sets()
        {
            object limit = view?.Session != null && view.Session.TryGetValue("TiresAvailable", out var x) ? x : null;
            bool limited = limit is double l && l > 0 && l < 100;
            return (limited && view?.NewTyres is int k && k >= 0 ? k.ToString(Inv) : "∞") + " Pneus";
        }

        // ── For SimHub ──────────────────────────────────────────────────────────────────────────────────────────────

        BlackBoxRow Row(int k)
        {
            if (page != Stands) { var r = rows; return k < r.Length ? r[k] : new BlackBoxRow(); }
            var item = stands.Row(k);
            if (item == null) return new BlackBoxRow();
            bool chosen = item.Name == stands.Chosen;
            return new BlackBoxRow { Nom = BlackBox.Label(item), Valeur = BlackBox.Text(item), Choisie = chosen, Reglable = chosen };
        }

        bool Up => page == Stands ? stands.Top > 0 : page == Electronics && elecTop > 0;
        bool Down => page == Stands ? stands.Top + BlackBox.Rows < stands.Items.Count : page == Electronics && elecTop + BlackBox.Rows < settings.Count;

        // What the page shows when it has nothing: the stands' menu not read, nobody driving.
        string Waiting => page == Stands ? (stands.Items.Count == 0 ? "Menu des stands non lu" : "")
            : page == Tyres ? (sample == null ? "Pas en piste" : "") : rows.All(r => r.Nom == "" && r.C1 == "") ? (sample == null ? "Pas en piste" : page == Electronics ? "Aucun réglage sur cette voiture" : "") : "";

        // BlackBox.Page (title), .PageNo (0 Relatif … 6 Arrêt aux stands), .Message, .Attente, .Haut / .Bas, the 7 lines
        // BlackBox.1.* … BlackBox.7.* and the wheels BlackBox.Pneus.AVG.* … ARD.*, BlackBox.Pneus.Jeux.
        public IEnumerable<(string name, Func<object> get)> Properties()
        {
            yield return ("BlackBox.Page", () => Titles[page]);
            yield return ("BlackBox.PageNo", () => page);
            yield return ("BlackBox.Message", () => Message);
            yield return ("BlackBox.Appuis", () => Presses);
            yield return ("BlackBox.Attente", () => Waiting);
            yield return ("BlackBox.Lignes", () => stands.Items.Count);
            yield return ("BlackBox.Choisie", () => stands.Chosen);
            yield return ("BlackBox.Haut", () => Up);
            yield return ("BlackBox.Bas", () => Down);
            for (int k = 0; k < BlackBox.Rows; k++)
            {
                int row = k; string at = "BlackBox." + (row + 1) + ".";
                yield return (at + "Nom", () => Row(row).Nom);
                yield return (at + "Valeur", () => Row(row).Valeur);
                yield return (at + "C1", () => Row(row).C1);
                yield return (at + "C2", () => Row(row).C2);
                yield return (at + "C3", () => Row(row).C3);
                yield return (at + "Pos", () => Row(row).Pos);
                yield return (at + "Classe", () => Row(row).Classe);
                yield return (at + "Couleur", () => Row(row).Couleur == "" ? "#00000000" : Row(row).Couleur);
                yield return (at + "Petit", () => Row(row).Petit);
                yield return (at + "Choisie", () => Row(row).Choisie);
                yield return (at + "Reglable", () => Row(row).Reglable);
                yield return (at + "Moi", () => Row(row).Moi);
            }
            for (int i = 0; i < 4; i++)
            {
                int wheel = i;
                foreach (string what in new[] { "Usure", "Gomme", "Pression", "Temp", "Frein", "Etat", "Couleur" })
                {
                    string w = what;
                    yield return ("BlackBox.Pneus." + Corners[wheel] + "." + w, () => Wheel(wheel, w));
                }
            }
            yield return ("BlackBox.Pneus.Jeux", () => Sets());
        }
    }
}
