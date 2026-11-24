// La black box de LMU sur un petit écran (VoCore du volant), pour le pilote qui la masque sur son écran principal. Le jeu
// ne sait pas l'afficher ailleurs : on la redessine d'après les captures de Nathan (titre et icônes des 7 pages, 7 lignes
// visibles, ligne choisie en blanc et rouge, flèches de défilement). Les pages et leurs boutons : BlackBoxPages.cs.
// Ici, la page « Arrêt aux stands » : les lignes du menu lues par l'API locale (PitMenu, Course.cs), dans le même
// ordre ; celles que le jeu ajoute quand il les faut (dommages, pénalités, pilote suivant) arrivent toutes seules.
// On y navigue sans ouvrir la black box du jeu (demande de Nathan) : ▲ ▼ changent de ligne (le curseur est celui du
// dash, rien n'est envoyé au jeu), − + changent la valeur de la ligne : le menu est lu, la ligne passe au choix d'avant
// ou d'après, le menu entier est renvoyé au jeu puis relu (comme les profils, Profiles.WriteMenu). Les choix font le
// tour dans les deux sens et « Pneus » règle les 4 roues, comme la black box du jeu ; la valeur s'affiche dès l'appui.
// Une ligne changée ailleurs (bouton FMT companion, black box du jeu) amène le curseur sur elle.
// Propriétés EnduranceManagerPlugin.BlackBox.* et dash « Endurance Manager · Black box » (800 × 480).
using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace EnduranceManager.SimHub
{
    public class BlackBox
    {
        // Lines visible at once, as in the game.
        public const int Rows = 7;

        // The game's names when its API answers in English, written as its French screen shows them (Nathan's captures).
        static readonly Dictionary<string, string> Names = new Dictionary<string, string>
        {
            ["DAMAGE"] = "Dommages", ["DRIVER"] = "Pilote", ["VIRTUAL ENERGY"] = "Énergie Virtuelle:", ["FUEL RATIO"] = "Ratio carburant",
            ["TIRES"] = "Pneus:", ["FL TIRE"] = "Pneu AvG", ["FR TIRE"] = "Pneu AvD", ["RL TIRE"] = "Pneu ArrG", ["RR TIRE"] = "Pneu ArrD",
            ["R WING"] = "Aileron", ["GRILLE"] = "Grille", ["FL PRESS"] = "Pression AvG", ["FR PRESS"] = "Pression AvD", ["RL PRESS"] = "Pression ArrG",
            ["RR PRESS"] = "Pression ArrD", ["F BRAKE DUCT"] = "CONDUIT FREIN AVANT :", ["R BRAKE DUCT"] = "CONDUIT FREIN ARR :",
            ["REPLACE BRAKES"] = "Remplacer les freins"
        };
        static readonly Dictionary<string, string> Texts = new Dictionary<string, string>
        {
            ["NO CHANGE"] = "Aucun changement", ["OPEN"] = "Ouvert", ["CLOSED"] = "Fermé", ["NO"] = "Non", ["YES"] = "Oui"
        };

        public static string Label(MenuItem item) => Names.TryGetValue(item.Name, out var fr) ? fr : string.IsNullOrEmpty(item.Label) ? item.Name : item.Label;
        public static string Text(MenuItem item) => Texts.TryGetValue((item.Text ?? "").Trim().ToUpperInvariant(), out var fr) ? fr : item.Text ?? "";

        const string LoadUrl = "http://localhost:6397/rest/garage/PitMenu/loadPitMenu";
        static readonly string[] Wheels = { "FL TIRE", "FR TIRE", "RL TIRE", "RR TIRE" };
        readonly HttpClient client = new HttpClient { Timeout = TimeSpan.FromSeconds(3) };
        // The menu as last read from the game (PitMenu.Raw), and a fresh read handed to Update after a change (Plugin.cs).
        readonly Func<JArray> source;
        readonly Action refresh;
        readonly object gate = new object();
        // The menu as the pilot wants it, not yet sent; its version, and the version the game has.
        JArray wanted, basis;
        int version, sent, writing;
        // Reads of the game are not shown for a moment after a press: they could still hold the value before it.
        DateTime holdUntil;

        List<MenuItem> seen;
        volatile List<MenuItem> items = new List<MenuItem>();
        volatile string chosen;
        volatile int top;
        volatile string message = "";
        DateTime messageUntil;

        public BlackBox(Func<JArray> source = null, Action refresh = null) { this.source = source; this.refresh = refresh; }

        public List<MenuItem> Items => items;
        public string Chosen => chosen ?? "";
        public int Top => top;
        // A short word in the title bar when a change did not go through (3 s).
        public string Message => DateTime.UtcNow < messageUntil ? message : "";

        // Called after each read of the game's menu. A line whose choice moved elsewhere becomes the chosen one (several
        // at once, a profile applied: the first, as the game lists them). Without one yet, the first line is chosen.
        public void Update(List<MenuItem> read)
        {
            if (read == null || ReferenceEquals(read, seen)) return;
            lock (gate)
            {
                if (wanted != null || writing != 0 || DateTime.UtcNow < holdUntil) return;
                // The game's own menu again: the next change starts from it.
                basis = null;
            }
            if (seen != null)
            {
                var before = seen.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => g.First().Index);
                var moved = read.FirstOrDefault(i => before.TryGetValue(i.Name, out var index) && index != i.Index);
                if (moved != null) chosen = moved.Name;
            }
            if (chosen == null || !read.Any(i => i.Name == chosen)) chosen = read.FirstOrDefault()?.Name;
            seen = read;
            items = read;
            Follow();
        }

        // The 7-line window moves just enough to show the chosen line, as the game's does.
        void Follow()
        {
            var list = items;
            int at = chosen == null ? -1 : list.FindIndex(i => i.Name == chosen);
            int first = top;
            if (at >= 0 && at < first) first = at;
            if (at >= first + Rows) first = at - Rows + 1;
            top = Math.Max(0, Math.Min(first, list.Count - Rows));
        }

        // ▲ ▼ : the line above or below (stops at the ends). Nothing is sent to the game.
        public void Move(int step)
        {
            var list = items;
            if (list.Count == 0) return;
            int at = Math.Max(0, list.FindIndex(i => i.Name == chosen));
            chosen = list[Math.Max(0, Math.Min(list.Count - 1, at + step))].Name;
            Follow();
        }

        // A touch on a line of the dash (1 to 7, as shown): that line becomes the chosen one.
        public void Select(int row)
        {
            var list = items; int k = top + row;
            if (k < 0 || k >= list.Count) return;
            chosen = list[k].Name;
            // The first or last line shown: the page moves by one so the next line comes into view, as with ▲ ▼.
            if (row == Rows - 1 && top + Rows < list.Count) top++;
            else if (row == 0 && top > 0) top--;
        }

        // − + : the chosen line one choice down or up, round the list both ways as in the game's black box (« Aucun
        // changement » − gives the last choice). « Pneus » sets the four wheels with it, as the game does. Shown at once;
        // sent to the game aside, the latest wanted menu only (quick presses are not lost, nor sent one by one).
        public void Change(int step)
        {
            string name = chosen;
            if (name == null) return;
            lock (gate)
            {
                var menu = (wanted ?? basis ?? source?.Invoke())?.DeepClone() as JArray;
                if (menu == null || !Step(menu, name, step)) return;
                wanted = menu;
                version++;
                holdUntil = DateTime.UtcNow.AddMilliseconds(800);
                items = seen = PitMenu.Parse(menu);
                Follow();
                if (writing != 0) return;
                writing = 1;
            }
            ThreadPool.QueueUserWorkItem(_ => Send());
        }

        // The line moved by one choice, round the list; « TIRES » also moves the four wheels to the same text.
        public static bool Step(JArray menu, string name, int step)
        {
            var line = menu.OfType<JObject>().FirstOrDefault(l => PitMenu.Key(l) == name);
            int count = (line?["settings"] as JArray)?.Count ?? 0;
            int now = (int?)line?["currentSetting"] ?? 0, next = now;
            // Round the list, past the choices the game never shows (« INVALID », empty).
            // « Pneus » never offers the mixed set (« Mixte »): the game shows it only when the wheels differ, its keys skip it.
            bool all = name == "TIRES";
            bool Fits(int i) => Usable(Choice(line, i)) && !(all && Choice(line, i).IndexOf("mix", StringComparison.OrdinalIgnoreCase) >= 0);
            for (int k = 0; k < count; k++)
            {
                next = ((next + step) % count + count) % count;
                if (Fits(next)) break;
            }
            if (count < 2 || next == now || !Fits(next)) return false;
            line["currentSetting"] = next;
            if (name == "TIRES")
            {
                string text = Choice(line, next);
                foreach (var wheel in menu.OfType<JObject>().Where(l => Wheels.Contains(PitMenu.Key(l))))
                {
                    var settings = (JArray)wheel["settings"];
                    for (int i = 0; settings != null && i < settings.Count; i++)
                        if (string.Equals(Choice(wheel, i), text, StringComparison.OrdinalIgnoreCase)) { wheel["currentSetting"] = i; break; }
                }
            }
            return true;
        }

        static bool Usable(string text) => text != "" && !string.Equals(text, "INVALID", StringComparison.OrdinalIgnoreCase);

        static string Choice(JObject line, int index) => ((string)((line["settings"] as JArray)?[index]?["text"]) ?? "").Trim();

        // Sends the latest wanted menu until the game has it, then reads the game again.
        void Send()
        {
            try
            {
                while (true)
                {
                    JArray menu; int v;
                    lock (gate) { if (wanted == null || sent == version) { wanted = null; writing = 0; break; } menu = wanted; v = version; }
                    try
                    {
                        var body = new StringContent(menu.ToString(Formatting.None), Encoding.UTF8, "application/json");
                        client.PostAsync(LoadUrl, body).GetAwaiter().GetResult().EnsureSuccessStatusCode();
                    }
                    catch { Say("Le jeu ne répond pas"); lock (gate) { wanted = null; writing = 0; holdUntil = DateTime.MinValue; } break; }
                    lock (gate) { sent = v; basis = menu; }
                }
            }
            finally { lock (gate) writing = 0; }
            try { refresh?.Invoke(); } catch { }
        }

        void Say(string text) { if (string.IsNullOrEmpty(text)) return; message = text; messageUntil = DateTime.UtcNow.AddSeconds(3); }

        // The line shown at 0 to 6, null when none (BlackBoxPages.cs makes the dash's lines from it).
        public MenuItem Row(int row) { var list = items; int k = top + row; return k >= 0 && k < list.Count ? list[k] : null; }
    }

    public static class BlackBoxDash
    {
        public const int Width = 800, Height = 480;
        // At most 20 images a second (none before: each image of SimHub, its 127 formulas again). The pages change 10
        // times a second at most: twice as often keeps the relatif, the tyres and the presses as quick as before.
        public const double RefreshMs = 50;
        const int Header = 45, Pad = 15;
        // The game's black box (colours read on Nathan's capture of 24/11): dark slate panel, darker title bar, grey labels,
        // white values; the chosen line white with red text and its arrows. Roboto as the game (in SimHub's DashFonts),
        // Segoe UI where it is missing.
        const string Panel = "#FF21242E", Bar = "#FF171925", Separator = "#FF4E5260", Label = "#FFA9ACB4", White = "#FFFFFFFF", Red = "#FFE83A4C",
            Arrow = "#FFCFD3DA";
        const string Font = "Roboto, Segoe UI";

        static string P(string name) => "[EnduranceManagerPlugin.BlackBox." + name + "]";

        static JObject Text(string name, double x, double y, double w, double h, double size, string expr, string color, int align, string visible = null)
        {
            bool formula = !color.StartsWith("#");
            var item = Dashes.Label(name, x, y, w, h, Font, size, "Bold", expr, formula ? null : color, align, visible);
            if (formula) ((JObject)item["Bindings"])["TextColor"] = Dashes.Bind("TextColor", color);
            return item;
        }

        static JObject Rect(string name, double x, double y, double w, double h, string color, string visible = null, int radius = 0, string border = null)
        {
            bool formula = !color.StartsWith("#");
            var box = Dashes.Box(name, x, y, w, h, formula ? "#00000000" : color, border ?? (formula ? "#00000000" : color), radius);
            var bindings = new JObject();
            if (formula) bindings["BackgroundColor"] = Dashes.Bind("BackgroundColor", color);
            if (visible != null) bindings["Visible"] = Dashes.Bind("Visible", visible);
            if (bindings.Count > 0) box["Bindings"] = bindings;
            return box;
        }

        // The page icons of the title bar, in the game's order (BlackBoxPages.Titles): the page shown in white.
        static readonly string[] Icons = { "⇅", "ⓘ", "⏱", "◎", "⛽", "⚡", "⚙" };
        // Their centres, as in the game (measured on Nathan's capture of 24/11, scaled to 800 px).
        static readonly double[] IconX = { 422, 482, 541, 600, 659, 712, 766 };
        const double IconSize = 35;

        // The same icons drawn in SVG as the game draws them (SimHub 9.13.6+ shows an SVG wherever an image goes): filled
        // grey glyphs, the page shown in white. 48 × 48; {0} = the glyph's colour, {1} = the title bar behind it.
        static readonly string[] IconBodies =
        {
            // Relatif: two cars seen from above, a dashed line between them.
            "<rect x='7' y='9' width='12' height='32' rx='5' fill='none' stroke='{0}' stroke-width='2.5'/><rect x='9.5' y='15' width='7' height='5' rx='1.5' fill='{0}'/><rect x='9.5' y='31' width='7' height='4' rx='1.5' fill='{0}'/>"
            + "<path d='M24 4v40' stroke='{0}' stroke-width='2' stroke-dasharray='3 3'/><rect x='29' y='4' width='12' height='30' rx='5' fill='none' stroke='{0}' stroke-width='2.5'/><rect x='31.5' y='10' width='7' height='5' rx='1.5' fill='{0}'/><rect x='31.5' y='25' width='7' height='4' rx='1.5' fill='{0}'/>",
            // Séance: a disc with an « i ».
            "<circle cx='24' cy='24' r='20' fill='{0}'/><circle cx='24' cy='14.5' r='3.3' fill='{1}'/><rect x='20.8' y='20' width='6.4' height='17' rx='3.2' fill='{1}'/>",
            // Chrono: a stopwatch, open at the bottom right where a small square sits.
            "<path d='M38 27A15 15 0 1 0 29.3 40.6' fill='none' stroke='{0}' stroke-width='3.5' stroke-linecap='round'/><path d='M23 27v-9' stroke='{0}' stroke-width='3.5' stroke-linecap='round'/>"
            + "<rect x='18' y='3' width='10' height='3.5' rx='1' fill='{0}'/><rect x='21.3' y='6' width='3.4' height='6' fill='{0}'/><rect x='32' y='34' width='10' height='10' fill='{0}'/>",
            // Pneus: a wheel, tyre and eight spokes.
            "<circle cx='24' cy='24' r='21' fill='{0}'/><circle cx='24' cy='24' r='15' fill='{1}'/><circle cx='24' cy='24' r='13' fill='none' stroke='{0}' stroke-width='1.5'/>"
            + "<path d='M28 24h8.5M26.8 26.8l6 6M24 28v8.5M21.2 26.8l-6 6M20 24h-8.5M21.2 21.2l-6-6M24 20v-8.5M26.8 21.2l6-6' stroke='{0}' stroke-width='2.4' stroke-linecap='round'/>"
            + "<circle cx='24' cy='24' r='4.5' fill='{0}'/><circle cx='24' cy='24' r='1.8' fill='{1}'/>",
            // Carburant: a fuel pump and its hose.
            "<path d='M9 41V8a3 3 0 0 1 3-3h14a3 3 0 0 1 3 3v33z' fill='{0}'/><rect x='12.5' y='9' width='13' height='10' rx='1.5' fill='{1}'/><rect x='6' y='40' width='26' height='4' rx='1' fill='{0}'/>"
            + "<path d='M29 13l6 6v15a3 3 0 0 0 6 0V16l-5-6' fill='none' stroke='{0}' stroke-width='3' stroke-linejoin='round' stroke-linecap='round'/>",
            // Électronique: a lightning bolt.
            "<path d='M28 2L9 28h12l-4 18L39 18H26z' fill='{0}'/>",
            // Arrêt aux stands: a gear and a wrench.
            "<path d='M29.8 15.9L29.8 20.1L26.7 20.3L25.5 23.2L27.5 25.6L24.6 28.5L22.2 26.5L19.3 27.7L19.1 30.8L14.9 30.8L14.7 27.7L11.8 26.5L9.4 28.5L6.5 25.6L8.5 23.2L7.3 20.3L4.2 20.1L4.2 15.9L7.3 15.7L8.5 12.8L6.5 10.4L9.4 7.5L11.8 9.5L14.7 8.3L14.9 5.2L19.1 5.2L19.3 8.3L22.2 9.5L24.6 7.5L27.5 10.4L25.5 12.8L26.7 15.7z' fill='{0}'/><circle cx='17' cy='18' r='4.5' fill='{1}'/>"
            + "<path d='M44.5 9.5a8 8 0 0 1-10.4 10.4L19.6 34.4a3.2 3.2 0 0 1-4.5-4.5l14.5-14.5A8 8 0 0 1 40 5l-5 5 1 4 4 1z' fill='{0}' stroke='{1}' stroke-width='1.5' transform='translate(2 6)'/>"
        };

        // The brake's sign as the game draws it: a drilled disc and its hub, the calliper over the top (on the left for
        // the left wheels, on the right for the right ones), grey on the panel.
        const string BrakeBody = "<circle cx='28' cy='28' r='19' fill='{0}'/><circle cx='28' cy='28' r='12.2' fill='{1}'/><circle cx='28' cy='28' r='10.7' fill='{0}'/>"
            + "<circle cx='34.2' cy='28' r='1.5' fill='{1}'/><circle cx='31.1' cy='33.37' r='1.5' fill='{1}'/><circle cx='24.9' cy='33.37' r='1.5' fill='{1}'/><circle cx='21.8' cy='28' r='1.5' fill='{1}'/><circle cx='24.9' cy='22.63' r='1.5' fill='{1}'/><circle cx='31.1' cy='22.63' r='1.5' fill='{1}'/><circle cx='28' cy='28' r='2.6' fill='{1}'/>"
            + "<path d='M7.3 39A23.5 23.5 0 0 1 23.1 5L25.6 16.8A11.5 11.5 0 0 0 17.8 33.4z' fill='{1}'/><path d='M7.6 36.2A22 22 0 0 1 21.2 7.1L24 15.6A13 13 0 0 0 15.9 32.9z' fill='{0}' stroke='{0}' stroke-width='1.5' stroke-linejoin='round'/>"
            + "<path d='M8.8 32.4A19.7 19.7 0 0 1 19.1 10.4L21.1 14.4A15.3 15.3 0 0 0 13.1 31.4z' fill='{1}' stroke='{1}' stroke-width='0.8' stroke-linejoin='round'/>";
        const string Grey = "#8A8D96", BrakeGrey = "#9C9EA7";

        static byte[] Svg(string body, string color, string behind, bool mirror = false) => Encoding.UTF8.GetBytes(
            "<svg xmlns='http://www.w3.org/2000/svg' width='48' height='48' viewBox='0 0 48 48'>" + (mirror ? "<g transform='translate(48 0) scale(-1 1)'>" : "")
            + string.Format(body, color, behind) + (mirror ? "</g>" : "") + "</svg>");

        static string Hex(string argb) => "#" + argb.Substring(3);

        static string IconName(int page, bool shown) => "bb_page" + (page + 1) + (shown ? "_on" : "");

        // The image files of the dash (its .djson.ressources, a zip as Dash Studio writes it) and their list in the dash.
        public static (JArray list, Dictionary<string, byte[]> files) Images()
        {
            var list = new JArray();
            var files = new Dictionary<string, byte[]>();
            void Add(string name, byte[] bytes)
            {
                files[name + ".svg"] = bytes;
                string md5;
                using (var h = System.Security.Cryptography.MD5.Create()) md5 = string.Concat(h.ComputeHash(bytes).Select(b => b.ToString("x2")));
                list.Add(new JObject { ["Name"] = name, ["Extension"] = ".svg", ["Modified"] = false, ["Optimized"] = false, ["Width"] = 48, ["Height"] = 48, ["Length"] = bytes.Length, ["MD5"] = md5 });
            }
            for (int k = 0; k < IconBodies.Length; k++)
                foreach (bool shown in new[] { false, true })
                    Add(IconName(k, shown), Svg(IconBodies[k], shown ? "#FFFFFF" : Grey, Hex(Bar)));
            Add("bb_frein_g", Svg(BrakeBody, BrakeGrey, Hex(Panel)));
            Add("bb_frein_d", Svg(BrakeBody, BrakeGrey, Hex(Panel), true));
            return (list, files);
        }

        static JObject Picture(string name, double x, double y, double w, double h, string image, string imageFormula, string visible)
        {
            var bindings = new JObject { ["Image"] = Dashes.Bind("Image", imageFormula) };
            if (visible != null) bindings["Visible"] = Dashes.Bind("Visible", visible);
            return new JObject
            {
                ["$type"] = "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.ImageItem, SimHub.Plugins", ["Image"] = image, ["AutoSize"] = false,
                ["BackgroundColor"] = "#00FFFFFF", ["Left"] = x, ["Top"] = y, ["Width"] = w, ["Height"] = h, ["Visible"] = true, ["Name"] = name, ["Bindings"] = bindings
            };
        }

        public static JArray Items(bool svg = false)
        {
            double row = (Height - Header) / (double)BlackBox.Rows;
            string page = P("PageNo"), tyres = page + " == " + BlackBoxPages.Tyres, message = P("Message") + " != ''";
            var items = new JArray
            {
                Rect("Fond", 0, 0, Width, Height, Panel),
                Rect("Barre", 0, 0, Width, Header, Bar),
                Text("Titre", Pad, 0, 380, Header, 23, P("Page"), White, 0),
                // A change that did not go through: a short word in place of the icons, for 3 s.
                Text("Message", 390, 0, Width - 390 - Pad, Header, 18, P("Message"), Red, 2, message),
                // Nothing to show (menu not read, nobody driving): one line instead of an empty page.
                Text("Attente", Pad, Header, Width - 2 * Pad, row, 26, P("Attente"), Label, 0, P("Attente") + " != ''")
            };
            for (int k = 0; k < Icons.Length; k++)
            {
                if (svg)
                {
                    items.Add(Picture("Icone" + (k + 1), IconX[k] - IconSize / 2, (Header - IconSize) / 2.0, IconSize, IconSize, IconName(k, false), $"if({page} == {k}, '{IconName(k, true)}', '{IconName(k, false)}')", "!(" + message + ")"));
                    continue;
                }
                var icon = Text("Icone" + (k + 1), IconX[k] - 24, 0, 48, Header, 24, "'" + Icons[k] + "'", $"if({page} == {k}, '{White}', '#FF8A8D96')", 1, "!(" + message + ")");
                icon["Font"] = "Segoe UI Symbol";
                items.Add(icon);
            }
            for (int k = 0; k < BlackBox.Rows; k++)
            {
                string at = (k + 1) + ".", chosen = P(at + "Choisie"), plain = "!" + chosen, small = P(at + "Petit"), place = P(at + "Pos") + " != ''";
                double y = Header + k * row;
                items.Add(Rect("Separation" + (k + 1), 0, y + row - 1, Width, 1, Separator, $"{P(at + "Nom")} != '' || {P(at + "C1")} != ''"));
                // Relatif: our car on a lighter line; the place in the class colour and the class badge.
                items.Add(Rect("Moi" + (k + 1), 0, y, Width, row - 1, "#FF6E7482", P(at + "Moi")));
                items.Add(Rect("Choix" + (k + 1), 0, y, Width, row - 1, White, chosen));
                items.Add(Rect("Place" + (k + 1), Pad, y + 9, 46, row - 18, P(at + "Couleur"), place, 4));
                items.Add(Text("PlaceTexte" + (k + 1), Pad, y, 46, row, 26, P(at + "Pos"), White, 1, place));
                items.Add(Rect("Classe" + (k + 1), Pad + 54, y + 9, 56, row - 18, White, P(at + "Classe") + " != ''", 4));
                items.Add(Text("ClasseTexte" + (k + 1), Pad + 54, y, 56, row, 20, P(at + "Classe"), P(at + "Couleur"), 1, P(at + "Classe") + " != ''"));
                items.Add(Text("Pilote" + (k + 1), Pad + 124, y, 440, row, 30, P(at + "Nom"), White, 0, place));
                items.Add(Text("Nom" + (k + 1), Pad, y, 560, row, 30, P(at + "Nom"), $"if({chosen}, '{Red}', '{Label}')", 0, $"!({place}) && !{small}"));
                items.Add(Text("NomPetit" + (k + 1), Pad, y, 560, row, 20, P(at + "Nom"), Label, 0, small));
                items.Add(Text("Valeur" + (k + 1), 300, y, Width - 300 - Pad, row, 30, P(at + "Valeur"), White, 2, plain));
                // Three columns (Chrono S1 S2 S3, Carburant Tours Temps Usage), right-aligned.
                double[] ends = { 520, 650, Width - Pad };
                for (int c = 0; c < 3; c++)
                    items.Add(Text("Colonne" + (c + 1) + "_" + (k + 1), ends[c] - 124, y, 124, row, 26, P(at + "C" + (c + 1)), $"if({small}, '{Label}', '{White}')", 2, P(at + "C" + (c + 1)) + " != ''"));
                // The chosen line: its value in red with « ◂ ▸ » after it, « ▴ ▾ » in the middle, as in the game.
                items.Add(Text("ValeurChoisie" + (k + 1), 300, y, Width - 300 - Pad - 52, row, 30, P(at + "Valeur"), Red, 2, chosen));
                items.Add(Text("Fleches" + (k + 1), Width - Pad - 48, y, 48, row, 20, "'◂ ▸'", Red, 2, chosen));
                items.Add(Text("Haut" + (k + 1), Width / 2 - 20, y + 4, 40, row / 2 - 4, 14, "'▴'", Red, 1, chosen));
                items.Add(Text("Bas" + (k + 1), Width / 2 - 20, y + row / 2, 40, row / 2 - 4, 14, "'▾'", Red, 1, chosen));
            }
            items.Add(Text("PlusHaut", Width / 2 - 20, Header, 40, 22, 16, "'▲'", Arrow, 1, P("Haut")));
            items.Add(Text("PlusBas", Width / 2 - 20, Height - 24, 40, 22, 16, "'▼'", Arrow, 1, P("Bas")));
            foreach (var item in TyreItems(tyres, svg)) items.Add(item);
            // Touch (format of Dash Studio's buttons, read in Lovely Dashboard; Dash Studio has no swipe, only touches, and
            // buttons on top of each other all fire: none overlap). A line touched is chosen (the first or last one shown
            // also scrolls by one); a line that − + adjust (the chosen one, Chrono's and Carburant's headers): left half −,
            // right half +; the far left or far right edge changes page. Transparent: the page looks the same.
            const int Edge = 60;
            double half = (Width - 2 * Edge) / 2.0;
            for (int k = 0; k < BlackBox.Rows; k++)
            {
                double y = Header + k * row;
                string adjust = P((k + 1) + ".Reglable");
                items.Add(Button("ToucherLigne" + (k + 1), Edge, y, Width - 2 * Edge, row, "BlackBoxLigne" + (k + 1), "!" + adjust));
                items.Add(Button("ToucherMoins" + (k + 1), Edge, y, half, row, "BlackBoxToucheMoins" + (k + 1), adjust));
                items.Add(Button("ToucherPlus" + (k + 1), Edge + half, y, half, row, "BlackBoxTouchePlus" + (k + 1), adjust));
            }
            items.Add(Button("ToucherPagePrecedente", 0, 0, Edge, Height, "BlackBoxPagePrecedente"));
            items.Add(Button("ToucherPageSuivante", Width - Edge, 0, Edge, Height, "BlackBoxPageSuivante"));
            return items;
        }

        // The Pneus page as in the game: a ring per wheel in the corners (what is left, the compound's letter), and between
        // them pressure, temperature and brake temperature, the fronts above, the rears below (mirrored), the sets left in
        // the middle.
        static IEnumerable<JObject> TyreItems(string shown, bool svg)
        {
            // Measured on Nathan's capture of the game's page next to ours (24/11), scaled to 800 px wide: ring centres
            // x 95 / 705, y 143 / 399, radius 80, band 10 px; text from x 197 to 603, the lines of each wheel at y 78 / 142 /
            // 208 (front) and 337 / 398 / 459 (rear), separators at 109 / 174 / 238 / 302 / 367 / 430 from x 182 to 618,
            // the brake sign (46 px) at x 346 / 454, « ∞ Pneus » at y 268.
            const string Worn = "#FF4E525C";
            const double R = 80, Band = 10, Brake = 46;
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            string[] corners = { "AVG", "AVD", "ARG", "ARD" };
            for (int i = 0; i < 4; i++)
            {
                string at = "Pneus." + corners[i] + ".";
                bool right = i % 2 == 1, front = i < 2;
                double cx = right ? Width - 95 : 95, cy = front ? 143 : 399;
                // The ring as the game: worn from the top clockwise (grey), the rest white (red when lost or flat). 100 small
                // dots, one per percent, close enough for a smooth band; the inner panel hides their inner halves.
                for (int k = 0; k < 100; k++)
                {
                    double angle = k * Math.PI * 2 / 100;
                    double dx = cx + (R - Band / 2) * Math.Sin(angle) - Band / 2, dy = cy - (R - Band / 2) * Math.Cos(angle) - Band / 2;
                    yield return Rect("Anneau" + (i + 1) + "_" + k, dx, dy, Band, Band, $"if({P(at + "Reste")} >= {(100 - k).ToString(inv)} - 0.5, {P(at + "Couleur")}, '{Worn}')", shown, (int)Math.Ceiling(Band / 2));
                }
                yield return Rect("AnneauFond" + (i + 1), cx - R + Band, cy - R + Band, 2 * (R - Band), 2 * (R - Band), Panel, shown, (int)(R - Band));
                yield return Text("Usure" + (i + 1), cx - R, cy - 40, 2 * R, 58, 46, P(at + "Usure"), White, 1, shown);
                // The compound's letter in its circle, in the game's colour (soft white, medium yellow, hard red, wet blue).
                string whole = $"{shown} && {P(at + "Gomme")} != '' && {P(at + "Etat")} == ''";
                double gy = cy + 34;
                yield return Rect("GommeRond" + (i + 1), cx - 16, gy - 16, 32, 32, P(at + "GommeCouleur"), whole, 16);
                yield return Rect("GommeFond" + (i + 1), cx - 13.3, gy - 13.3, 26.6, 26.6, Panel, whole, 14);
                yield return Text("Gomme" + (i + 1), cx - 16, gy - 16, 32, 32, 20, P(at + "Gomme"), P(at + "GommeCouleur"), 1, $"{shown} && {P(at + "Etat")} == ''");
                yield return Text("Etat" + (i + 1), cx - R, gy - 17, 2 * R, 34, 20, P(at + "Etat"), Red, 1, $"{shown} && {P(at + "Etat")} != ''");
                double bx = right ? 454 : 346, by = front ? 208 : 337;
                if (svg)
                    yield return Picture("Disque" + (i + 1), bx - Brake / 2, by - Brake / 2, Brake, Brake, right ? "bb_frein_d" : "bb_frein_g", right ? "'bb_frein_d'" : "'bb_frein_g'", shown);
                else
                    foreach (var part in BrakeSign("Disque" + (i + 1), bx, by, shown)) yield return part;
                double tx = right ? 603 - 220 : 197;
                int align = right ? 2 : 0;
                double[] lines = front ? new double[] { 78, 142, 208 } : new double[] { 337, 398, 459 };
                string[] what = front ? new[] { "Pression", "Temp", "Frein" } : new[] { "Frein", "Temp", "Pression" };
                for (int l = 0; l < 3; l++)
                {
                    bool big = what[l] == "Temp";
                    double h = big ? 48 : 36;
                    yield return Text(what[l] + (i + 1), tx, lines[l] - h / 2, 220, h, big ? 34 : 25, P(at + what[l]), big ? White : Label, align, shown);
                }
            }
            foreach (double y in new double[] { 109, 174, 238, 302, 367, 430 }) yield return Rect("PneusTrait" + y, 182, y, 436, 1.5, Separator, shown);
            yield return Text("Jeux", 182, 268 - 22, 436, 44, 28, P("Pneus.Jeux"), White, 1, shown);
        }

        // A brake disc sign, about 30 px, centred on (x, y): the disc, its hub, the calliper over its top right.
        static IEnumerable<JObject> BrakeSign(string name, double x, double y, string shown)
        {
            yield return Rect(name + "Disque", x - 14, y - 14, 28, 28, "#FF8A909C", shown, 14);
            yield return Rect(name + "Moyeu", x - 6, y - 6, 12, 12, Panel, shown, 6);
            yield return Rect(name + "Point", x - 2, y - 2, 4, 4, "#FF8A909C", shown, 2);
            yield return Rect(name + "Etrier", x + 3, y - 17, 13, 17, "#FF8A909C", shown, 5, Panel);
        }

        // A transparent Dash Studio button that runs one of this plugin's actions when touched or clicked.
        static JObject Button(string name, double x, double y, double w, double h, string action, string visible = null)
        {
            var button = new JObject
            {
                ["$type"] = "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.ButtonItem, SimHub.Plugins",
                ["AutoSize"] = false, ["SimulatedKey"] = 0,
                ["SimulatedKeyV2"] = new JObject { ["KeyCode"] = 0, ["Win"] = false, ["Ctrl"] = false, ["Alt"] = false, ["Shift"] = false },
                ["SimulateKey"] = false, ["TriggerAction"] = "EnduranceManagerPlugin." + action, ["TriggerSimHubInputName"] = "", ["TriggerControlMapperRoleName"] = "",
                ["ButtonStyle"] = 0, ["IsEnabled"] = true, ["DisabledOpacity"] = 50, ["PressedOpacity"] = 100, ["BackgroundColor"] = "#00FFFFFF",
                ["Height"] = h, ["Left"] = x, ["Top"] = y, ["Width"] = w, ["Visible"] = true, ["Name"] = name
            };
            if (visible != null) button["Bindings"] = new JObject { ["Visible"] = Dashes.Bind("Visible", visible) };
            return button;
        }

        // svg: the page icons as SVG images (SimHub 9.13.6 or newer, Dashes.SvgImages), else as text in Segoe UI Symbol.
        public static (JObject dash, JObject metadata) Make(bool svg = false)
        {
            var layer = new JObject
            {
                ["$type"] = "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.Layer, SimHub.Plugins", ["Group"] = true, ["Repetitions"] = 0, ["Visible"] = true,
                ["Name"] = "Black box", ["RenderingSkip"] = 0, ["MinimumRefreshIntervalMS"] = RefreshMs, ["Top"] = 0, ["Left"] = 0, ["Height"] = Height, ["Width"] = Width,
                ["BackgroundColor"] = "#00FFFFFF", ["Childrens"] = Items(svg)
            };
            var metadata = new JObject
            {
                ["Category"] = "Endurance Manager", ["Title"] = "Endurance Manager · Black box", ["Author"] = "Endurance Manager",
                ["Description"] = "Les 7 pages de la black box de LMU sur l'écran du volant (VoCore 800 × 480), comme dans le jeu. On y navigue avec 6 boutons du volant (actions BlackBox*) ou au toucher. Plugin Endurance Manager requis, module « Black box sur boutons » activé.",
                ["ScreenCount"] = 1.0, ["InGameScreensIndexs"] = new JArray(0), ["IdleScreensIndexs"] = new JArray(0), ["MainPreviewIndex"] = 0, ["IsOverlay"] = false,
                ["Width"] = (double)Width, ["Height"] = (double)Height, ["OverlaySizeWarning"] = false, ["MetadataVersion"] = 2.0, ["EnableOnDashboardMessaging"] = true,
                ["PitScreensIndexs"] = new JArray()
            };
            var dash = new JObject
            {
                ["DashboardDebugManager"] = new JObject { ["Maximized"] = false }, ["Version"] = 2, ["Id"] = "0b5c6c9e-4f0a-4d39-9d3e-6a1d2b7c1a07", ["BaseHeight"] = Height, ["BaseWidth"] = Width,
                ["BackgroundColor"] = Panel,
                ["Screens"] = new JArray(new JObject
                {
                    ["Name"] = "Black box", ["InGameScreen"] = true, ["IdleScreen"] = true, ["PitScreen"] = false, ["ScreenId"] = "0b5c6c9e-4f0a-4d39-9d3e-6a1d2b7c1a08", ["IsForegroundLayer"] = false,
                    ["IsBackgroundLayer"] = false, ["BackgroundColor"] = Panel, ["Items"] = new JArray(layer)
                }),
                ["SnapToGrid"] = false, ["HideLabels"] = true, ["ShowForeground"] = true, ["ForegroundOpacity"] = 100.0, ["ShowBackground"] = true, ["BackgroundOpacity"] = 100.0,
                ["ShowBoundingRectangles"] = false, ["GridSize"] = 2, ["Images"] = svg ? Images().list : new JArray(), ["Metadata"] = metadata, ["ShowOnScreenControls"] = false,
                ["IsOverlay"] = false, ["EnableClickThroughOverlay"] = false, ["EnableOnDashboardMessaging"] = true
            };
            return (dash, metadata);
        }
    }
}
