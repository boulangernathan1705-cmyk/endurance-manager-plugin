// La page « Arrêt aux stands » de la black box de LMU sur un petit écran (VoCore du volant), pour le pilote qui la masque
// sur son écran principal. Le jeu ne sait pas l'afficher ailleurs : on la redessine d'après les captures de Nathan
// (titre, 7 lignes visibles, ligne choisie en blanc et rouge, flèches de défilement), avec les lignes du menu lues par
// l'API locale (PitMenu, Course.cs), dans le même ordre. Les lignes que le jeu ajoute quand il les faut (dommages,
// pénalités, pilote suivant) arrivent toutes seules.
// On navigue dedans sans ouvrir la black box du jeu (0.1.5, demande de Nathan) : 6 actions SimHub pour le volant,
// ▲ ▼ changent de ligne (le curseur est celui du dash, rien n'est envoyé au jeu), ◂ ▸ changent de page (une seule pour
// l'instant), − + changent la valeur de la ligne : le menu est lu, la ligne passe au choix d'avant ou d'après, le menu
// entier est renvoyé au jeu puis relu (comme les profils, Profiles.WriteMenu). Les choix font le tour dans les deux
// sens et « Pneus » règle les 4 roues, comme la black box du jeu ; la valeur s'affiche dès l'appui. Une ligne changée ailleurs (bouton FMT
// companion, black box du jeu) amène le curseur sur elle.
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
            if (k >= 0 && k < list.Count) chosen = list[k].Name;
            Follow();
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
            if (count < 2) return false;
            int next = (((int?)line["currentSetting"] ?? 0) + step) % count;
            if (next < 0) next += count;
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

        MenuItem At(int row) { var list = items; int k = top + row; return k >= 0 && k < list.Count ? list[k] : null; }

        // For SimHub: BlackBox.Lignes, .Choisie, .Haut / .Bas (more lines above / below), and the 7 lines in view,
        // BlackBox.1.Nom / .Valeur / .Choisie … BlackBox.7.*.
        public IEnumerable<(string name, System.Func<object> get)> Properties()
        {
            yield return ("BlackBox.Lignes", () => items.Count);
            yield return ("BlackBox.Choisie", () => Chosen);
            yield return ("BlackBox.Page", () => "Arrêt aux stands");
            yield return ("BlackBox.Message", () => Message);
            yield return ("BlackBox.Haut", () => top > 0);
            yield return ("BlackBox.Bas", () => top + Rows < items.Count);
            for (int k = 0; k < Rows; k++)
            {
                int row = k;
                yield return ("BlackBox." + (row + 1) + ".Nom", () => At(row) is MenuItem i ? Label(i) : "");
                yield return ("BlackBox." + (row + 1) + ".Valeur", () => At(row) is MenuItem i ? Text(i) : "");
                yield return ("BlackBox." + (row + 1) + ".Choisie", () => At(row) is MenuItem i && chosen != null && i.Name == chosen);
            }
        }
    }

    public static class BlackBoxDash
    {
        public const int Width = 800, Height = 480;
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

        static JObject Rect(string name, double x, double y, double w, double h, string color, string visible = null)
        {
            var box = Dashes.Box(name, x, y, w, h, color, color, 0);
            if (visible != null) box["Bindings"] = new JObject { ["Visible"] = Dashes.Bind("Visible", visible) };
            return box;
        }

        public static JArray Items()
        {
            double row = (Height - Header) / (double)BlackBox.Rows;
            var items = new JArray
            {
                Rect("Fond", 0, 0, Width, Height, Panel),
                Rect("Barre", 0, 0, Width, Header, Bar),
                Text("Titre", Pad, 0, 500, Header, 30, P("Page"), White, 0),
                // A change that did not go through: a short word on the right of the title bar, for 3 s.
                Text("Message", 420, 0, Width - 420 - Pad, Header, 20, P("Message"), Red, 2),
                // Until the game's menu is read (game closed, module off): one line instead of an empty page.
                Text("Attente", Pad, Header, Width - 2 * Pad, row, 26, "'Menu des stands non lu'", Label, 0, P("Lignes") + " == 0")
            };
            for (int k = 0; k < BlackBox.Rows; k++)
            {
                string at = (k + 1) + ".", chosen = P(at + "Choisie"), plain = "!" + chosen;
                double y = Header + k * row;
                items.Add(Rect("Separation" + (k + 1), 0, y + row - 1, Width, 1, Separator, P(at + "Nom") + " != ''"));
                items.Add(Rect("Choix" + (k + 1), 0, y, Width, row - 1, White, chosen));
                items.Add(Text("Nom" + (k + 1), Pad, y, 420, row, 30, P(at + "Nom"), $"if({chosen}, '{Red}', '{Label}')", 0));
                items.Add(Text("Valeur" + (k + 1), 360, y, Width - 360 - Pad, row, 30, P(at + "Valeur"), White, 2, plain));
                // The chosen line: its value in red with « ◂ ▸ » after it, « ▴ ▾ » in the middle, as in the game.
                items.Add(Text("ValeurChoisie" + (k + 1), 360, y, Width - 360 - Pad - 52, row, 30, P(at + "Valeur"), Red, 2, chosen));
                items.Add(Text("Fleches" + (k + 1), Width - Pad - 48, y, 48, row, 20, "'◂ ▸'", Red, 2, chosen));
                items.Add(Text("Haut" + (k + 1), Width / 2 - 20, y + 4, 40, row / 2 - 4, 14, "'▴'", Red, 1, chosen));
                items.Add(Text("Bas" + (k + 1), Width / 2 - 20, y + row / 2, 40, row / 2 - 4, 14, "'▾'", Red, 1, chosen));
            }
            items.Add(Text("PlusHaut", Width / 2 - 20, Header, 40, 22, 16, "'▲'", Arrow, 1, P("Haut")));
            items.Add(Text("PlusBas", Width / 2 - 20, Height - 24, 40, 22, 16, "'▼'", Arrow, 1, P("Bas")));
            // Touch (format of Dash Studio's buttons, read in Lovely Dashboard; Dash Studio has no swipe, only touches): a
            // line touched is chosen; on the chosen line the left of the value lowers it, the right raises it; the arrows
            // ▲ ▼ move by one line; a touch on the far left or far right edge changes page. Transparent: the page looks the same.
            const int Edge = 60;
            for (int k = 0; k < BlackBox.Rows; k++)
                items.Add(Button("ToucherLigne" + (k + 1), Edge, Header + k * row, Width - 2 * Edge, row, "BlackBoxLigne" + (k + 1)));
            for (int k = 0; k < BlackBox.Rows; k++)
            {
                double y = Header + k * row;
                string chosen = P((k + 1) + ".Choisie");
                items.Add(Button("ToucherMoins" + (k + 1), 400, y, 170, row, "BlackBoxMoins", chosen));
                items.Add(Button("ToucherPlus" + (k + 1), 570, y, Width - Edge - 570, row, "BlackBoxPlus", chosen));
            }
            items.Add(Button("ToucherMonter", Width / 2 - 60, Header, 120, 30, "BlackBoxMonter", P("Haut")));
            items.Add(Button("ToucherDescendre", Width / 2 - 60, Height - 30, 120, 30, "BlackBoxDescendre", P("Bas")));
            items.Add(Button("ToucherPagePrecedente", 0, 0, Edge, Height, "BlackBoxPagePrecedente"));
            items.Add(Button("ToucherPageSuivante", Width - Edge, 0, Edge, Height, "BlackBoxPageSuivante"));
            return items;
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
                ["Name"] = "Black box", ["RenderingSkip"] = 0, ["MinimumRefreshIntervalMS"] = 0.0, ["Top"] = 0, ["Left"] = 0, ["Height"] = Height, ["Width"] = Width,
                ["BackgroundColor"] = "#00FFFFFF", ["Childrens"] = Items()
            };
            var metadata = new JObject
            {
                ["Category"] = "Endurance Manager", ["Title"] = "Endurance Manager · Black box", ["Author"] = "Endurance Manager",
                ["Description"] = "La page « Arrêt aux stands » de la black box de LMU sur l'écran du volant (VoCore 800 × 480). On y navigue avec 6 boutons du volant (actions BlackBox*). Plugin Endurance Manager requis, module « Black box sur boutons » activé.",
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
                    ["Name"] = "Arrêt aux stands", ["InGameScreen"] = true, ["IdleScreen"] = true, ["PitScreen"] = false, ["ScreenId"] = "0b5c6c9e-4f0a-4d39-9d3e-6a1d2b7c1a08", ["IsForegroundLayer"] = false,
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
