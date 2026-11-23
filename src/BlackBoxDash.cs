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
        const int Header = 66, Pad = 14;
        // The game's black box: blue-grey panel, lighter title bar, grey labels, white values; the chosen line white with
        // red text and its arrows.
        const string Panel = "#FF1D2330", Bar = "#FF262D3B", Separator = "#FF4A5160", Label = "#FFB9BEC8", White = "#FFFFFFFF", Red = "#FFE83A4C",
            Arrow = "#FFCFD3DA";
        const string Font = "Segoe UI";

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

        public static JArray Items()
        {
            double row = (Height - Header) / (double)BlackBox.Rows;
            string page = P("PageNo"), tyres = page + " == " + BlackBoxPages.Tyres, message = P("Message") + " != ''";
            var items = new JArray
            {
                Rect("Fond", 0, 0, Width, Height, Panel),
                Rect("Barre", 0, 0, Width, Header, Bar),
                Text("Titre", Pad, 0, 400, Header, 30, P("Page"), White, 0),
                // A change that did not go through: a short word in place of the icons, for 3 s.
                Text("Message", 400, 0, Width - 400 - Pad, Header, 20, P("Message"), Red, 2, message),
                // Nothing to show (menu not read, nobody driving): one line instead of an empty page.
                Text("Attente", Pad, Header, Width - 2 * Pad, row, 26, P("Attente"), Label, 0, P("Attente") + " != ''")
            };
            for (int k = 0; k < Icons.Length; k++)
            {
                var icon = Text("Icone" + (k + 1), 418 + k * 52, 0, 48, Header, 26, "'" + Icons[k] + "'", $"if({page} == {k}, '{White}', '#FF8A909C')", 1, "!(" + message + ")");
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
            foreach (var item in TyreItems(tyres)) items.Add(item);
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
        static IEnumerable<JObject> TyreItems(string shown)
        {
            const string Worn = "#FF3A3F48";
            string[] corners = { "AVG", "AVD", "ARG", "ARD" };
            for (int i = 0; i < 4; i++)
            {
                string at = "Pneus." + corners[i] + ".";
                double x = i % 2 == 0 ? 20 : Width - 20 - 156, y = i < 2 ? 80 : 304;
                bool right = i % 2 == 1, front = i < 2;
                // The ring wears round as in the game: 90 small dots clockwise from the top, lit (white, red when lost or
                // flat) up to what is left, grey after; close enough to look like one smooth band (36 big ones looked
                // notched, Nathan 23/11). The inner panel hides their inner halves.
                const int Dots = 90;
                for (int k = 0; k < Dots; k++)
                {
                    double angle = k * Math.PI * 2 / Dots;
                    double dx = x + 78 + 73 * Math.Sin(angle) - 6, dy = y + 78 - 73 * Math.Cos(angle) - 6;
                    string lit = ((k + 1) * 100.0 / Dots).ToString("0.##", System.Globalization.CultureInfo.InvariantCulture);
                    yield return Rect("Anneau" + (i + 1) + "_" + k, dx, dy, 12, 12, $"if({P(at + "Reste")} >= {lit}, {P(at + "Couleur")}, '{Worn}')", shown, 6);
                }
                yield return Rect("AnneauFond" + (i + 1), x + 10, y + 10, 136, 136, Panel, shown, 68);
                yield return Text("Usure" + (i + 1), x, y + 32, 156, 54, 38, P(at + "Usure"), White, 1, shown);
                // The compound's letter in its circle, in the game's colour (soft white, medium yellow, hard red, wet blue).
                string whole = $"{shown} && {P(at + "Gomme")} != '' && {P(at + "Etat")} == ''";
                yield return Rect("GommeRond" + (i + 1), x + 64, y + 92, 28, 28, P(at + "GommeCouleur"), whole, 14);
                yield return Rect("GommeFond" + (i + 1), x + 66, y + 94, 24, 24, Panel, whole, 12);
                yield return Text("Gomme" + (i + 1), x + 64, y + 92, 28, 28, 16, P(at + "Gomme"), P(at + "GommeCouleur"), 1, $"{shown} && {P(at + "Etat")} == ''");
                yield return Text("Etat" + (i + 1), x, y + 88, 156, 34, 20, P(at + "Etat"), Red, 1, $"{shown} && {P(at + "Etat")} != ''");
                // The brake's sign beside its temperature, toward the middle, as in the game.
                var disc = Text("Disque" + (i + 1), right ? 410 : 360, front ? 176 : 300, 30, 40, 22, "'⊚'", Label, 1, shown);
                disc["Font"] = "Segoe UI Symbol";
                yield return disc;
                double cx = right ? 410 : 190;
                int align = right ? 2 : 0;
                double[] lines = front ? new double[] { 80, 122, 176 } : new double[] { 300, 342, 396 };
                string[] what = front ? new[] { "Pression", "Temp", "Frein" } : new[] { "Frein", "Temp", "Pression" };
                for (int l = 0; l < 3; l++)
                {
                    bool big = what[l] == "Temp";
                    yield return Text(what[l] + (i + 1), cx, lines[l], 200, big ? 52 : 40, big ? 32 : 22, P(at + what[l]), big ? White : Label, align, shown);
                }
            }
            foreach (double y in new double[] { 120, 172, 298, 350 }) yield return Rect("PneusTrait" + y, 190, y, 420, 1, Separator, shown);
            yield return Text("Jeux", 190, 228, 420, 48, 26, P("Pneus.Jeux"), White, 1, shown);
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

        public static (JObject dash, JObject metadata) Make()
        {
            var layer = new JObject
            {
                ["$type"] = "SimHub.Plugins.OutputPlugins.GraphicalDash.Models.Layer, SimHub.Plugins", ["Group"] = true, ["Repetitions"] = 0, ["Visible"] = true,
                ["Name"] = "Black box", ["RenderingSkip"] = 0, ["MinimumRefreshIntervalMS"] = RefreshMs, ["Top"] = 0, ["Left"] = 0, ["Height"] = Height, ["Width"] = Width,
                ["BackgroundColor"] = "#00FFFFFF", ["Childrens"] = Items()
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
                ["ShowBoundingRectangles"] = false, ["GridSize"] = 2, ["Images"] = new JArray(), ["Metadata"] = metadata, ["ShowOnScreenControls"] = false,
                ["IsOverlay"] = false, ["EnableClickThroughOverlay"] = false, ["EnableOnDashboardMessaging"] = true
            };
            return (dash, metadata);
        }
    }
}
