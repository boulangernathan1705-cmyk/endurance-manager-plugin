// La page « Arrêt aux stands » de la black box de LMU sur un petit écran (VoCore du volant), pour le pilote qui la masque
// sur son écran principal. Le jeu ne sait pas l'afficher ailleurs : on la redessine d'après les captures de Nathan
// (titre, 7 lignes visibles, ligne choisie en blanc et rouge, flèches de défilement), avec les lignes du menu lues par
// l'API locale (PitMenu, Course.cs), dans le même ordre. Les lignes que le jeu ajoute quand il les faut (dommages,
// pénalités, pilote suivant) arrivent toutes seules.
// Le jeu ne dit pas où est son curseur : la ligne choisie est la dernière dont le réglage a changé (bouton FMT companion
// ou autre), gardée jusqu'au changement suivant ; la fenêtre de 7 lignes défile pour la garder visible, comme le jeu.
// Propriétés EnduranceManagerPlugin.BlackBox.* et dash « Endurance Manager · Black box » (800 × 480). Rien n'y est
// calculé, rien ne modifie le jeu.
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

        List<MenuItem> seen;
        volatile List<MenuItem> items = new List<MenuItem>();
        volatile string chosen;
        volatile int top;

        public List<MenuItem> Items => items;
        public string Chosen => chosen ?? "";
        public int Top => top;

        // Called after each read of the game's menu. A line whose choice moved becomes the chosen one (several at once,
        // a profile applied: the first, as the game lists them). A line added or removed (a penalty, another car) keeps
        // the chosen line while it is still there.
        public void Update(List<MenuItem> read)
        {
            if (read == null || ReferenceEquals(read, seen)) return;
            if (seen != null)
            {
                var before = seen.GroupBy(i => i.Name).ToDictionary(g => g.Key, g => g.First().Index);
                var moved = read.FirstOrDefault(i => before.TryGetValue(i.Name, out var index) && index != i.Index);
                if (moved != null) chosen = moved.Name;
            }
            if (chosen != null && !read.Any(i => i.Name == chosen)) chosen = null;
            int at = chosen == null ? -1 : read.FindIndex(i => i.Name == chosen);
            int first = top;
            if (at >= 0 && at < first) first = at;
            if (at >= first + Rows) first = at - Rows + 1;
            first = System.Math.Max(0, System.Math.Min(first, read.Count - Rows));
            seen = read;
            items = read;
            top = first;
        }

        MenuItem At(int row) { var list = items; int k = top + row; return k >= 0 && k < list.Count ? list[k] : null; }

        // For SimHub: BlackBox.Lignes, .Choisie, .Haut / .Bas (more lines above / below), and the 7 lines in view,
        // BlackBox.1.Nom / .Valeur / .Choisie … BlackBox.7.*.
        public IEnumerable<(string name, System.Func<object> get)> Properties()
        {
            yield return ("BlackBox.Lignes", () => items.Count);
            yield return ("BlackBox.Choisie", () => Chosen);
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
                Text("Titre", Pad, 0, 500, Header, 30, "'Arrêt aux stands'", White, 0),
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
            return items;
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
                ["Description"] = "La page « Arrêt aux stands » de la black box de LMU sur l'écran du volant (VoCore 800 × 480). La ligne en rouge est la dernière changée. Plugin Endurance Manager requis, module « Black box sur boutons » activé.",
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
