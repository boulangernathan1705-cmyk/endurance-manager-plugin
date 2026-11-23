// Photos de l'écran du plugin, sans SimHub : chaque écran du guide et les onglets des modules, rendus par WPF en PNG
// (thème du site, Theme.cs). Faites par la CI Windows (.github/workflows/simhub-plugin.yml), rangées dans apercus/.
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace EnduranceManager.SimHub.Preview
{
    public static class Program
    {
        const int Width = 1100, Height = 760;

        [STAThread]
        public static int Main(string[] args)
        {
            string dir = args.Length > 0 ? args[0] : "apercus";
            Directory.CreateDirectory(dir);
            int made = 0;
            void Shot(string name, Func<FrameworkElement> make, Action<FrameworkElement> then = null)
            {
                try
                {
                    var element = make();
                    var window = new Window { Content = element, Width = Width, Height = Height, WindowStyle = WindowStyle.None, ShowInTaskbar = false, ShowActivated = false, Left = -20000, Top = 0, Background = Theme.Bg };
                    window.Show();
                    Idle();
                    then?.Invoke(element);
                    Idle();
                    var shown = (FrameworkElement)window.Content;
                    var bitmap = new RenderTargetBitmap((int)(shown.ActualWidth * 1.5), (int)(shown.ActualHeight * 1.5), 144, 144, PixelFormats.Pbgra32);
                    bitmap.Render(shown);
                    var png = new PngBitmapEncoder();
                    png.Frames.Add(BitmapFrame.Create(bitmap));
                    using (var file = File.Create(Path.Combine(dir, name + ".png"))) png.Save(file);
                    window.Close();
                    made++;
                    Console.WriteLine("OK    " + name);
                }
                catch (Exception e) { Console.WriteLine("ÉCHEC " + name + " : " + e); }
            }

            // A pilot who has just installed it: overlay, team telemetry and sharing on (the defaults), the mur too.
            EnduranceManagerPlugin Plugin(bool guideDone) { var p = new EnduranceManagerPlugin(); p.Preview(new Settings { GuideDone = guideDone, Wall = true }); return p; }
            Guide GuideOf(FrameworkElement e) => (Guide)((Shell)e).Page;

            Shot("guide-1-modules", () => new Shell(Plugin(false)));
            Shot("guide-2-relier-au-site", () => new Shell(Plugin(false)), e => GuideOf(e).Jump(1, 0));
            Shot("guide-2-overlay-relais", () => new Shell(Plugin(false)), e => GuideOf(e).Jump(1, 1));
            Shot("guide-2-mur-des-stands", () => new Shell(Plugin(false)), e => GuideOf(e).Jump(1, 2));
            Shot("guide-3-pret", () => new Shell(Plugin(false)), e => GuideOf(e).Jump(2));
            Shot("onglet-general", () => new Shell(Plugin(true)));
            Shot("onglet-pour-moi-overlay-relais", () => new Shell(Plugin(true)), e => Open(e, 1, 0));
            // Module on, one car with its three profiles memorised (example values).
            EnduranceManagerPlugin WithProfiles()
            {
                var p = Plugin(true);
                p.Settings.Profiles = true;
                var list = p.Profiles.For("Porsche 963 LMDh");
                void Fill(int i, double tc, double abs, double bias, double map) { list[i].Drive = new System.Collections.Generic.Dictionary<string, double> { ["TC"] = tc, ["TCCut"] = tc + 1, ["ABS"] = abs, ["Bias"] = bias, ["MotorMap"] = map }; }
                Fill(0, 3, 4, 54.2, 1); Fill(1, 6, 6, 53.6, 2); Fill(2, 9, 8, 52.8, 3);
                return p;
            }
            Shot("onglet-pour-moi-reglages-voiture", () => new Shell(WithProfiles()), e => Open(e, 1, 1));
            Shot("onglet-pour-moi-mur-des-stands", () => new Shell(Plugin(true)), e => Open(e, 1, 3));
            Shot("onglet-equipe-relier-au-site", () => new Shell(Plugin(true)), e => Open(e, 2, 0));
            Shot("onglet-equipe-aide-au-developpement", () => new Shell(Plugin(true)), e => Open(e, 2, 3));
            // The profiles' popup after a first press (example values).
            Shot("popup-profil-1er-appui", () =>
            {
                var view = new Profiles.PopupView { Title = "« Pluie »", Help = "Rappuie sur le même bouton pour confirmer.", Confirm = true, Since = DateTime.UtcNow, Until = DateTime.UtcNow.AddSeconds(5) };
                view.Drive.Add(("TC", "5", "8")); view.Drive.Add(("ABS", "4", "7")); view.Drive.Add(("Répartition", "54,2 %", "52,8 %"));
                view.Pit.Add(("Pneus", "Medium", "Wet")); view.Pit.Add(("Aileron arrière", "P5", "P7"));
                var card = ProfilePopup.Card(view, out var bar);
                bar.Width = 308 * 0.6;
                card.HorizontalAlignment = HorizontalAlignment.Left; card.VerticalAlignment = VerticalAlignment.Top;
                return card;
            });
            // The mur's page is in the DLL: its files answer (WallServer.cs).
            bool page = false;
            try
            {
                var server = new WallServer(() => new { vue = new WallView() }, (x, y, w, h) => { });
                server.Start();
                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(5) })
                {
                    string html = http.GetStringAsync(server.LocalUrl).GetAwaiter().GetResult();
                    var css = http.GetAsync(server.LocalUrl + "pit-wall.css").GetAwaiter().GetResult();
                    var font = http.GetAsync(server.LocalUrl + "fonts/jetbrains-mono.woff2").GetAwaiter().GetResult();
                    string vue = http.GetStringAsync(server.LocalUrl + "vue").GetAwaiter().GetResult();
                    page = html.Contains("<title>Mur des stands</title>") && css.IsSuccessStatusCode && font.IsSuccessStatusCode && vue.Contains("\"vue\"");
                    Console.WriteLine((page ? "OK    " : "ÉCHEC ") + $"page du mur : html {html.Length} o, css {(int)css.StatusCode}, police {(int)font.StatusCode}");
                }
                server.Stop();
            }
            catch (Exception e) { Console.WriteLine("ÉCHEC page du mur : " + e.Message); }
            // The plugin's screen as a web page (src/ecran/, ScreenApi.cs): /etat answers this PC's page only, and Edge
            // (headless) takes its pictures, page by page.
            bool screen = false;
            int shots = 0;
            try
            {
                var plugin = WithProfiles();
                var server = plugin.WallServer;
                server.Start(false);
                using (var http = new System.Net.Http.HttpClient { Timeout = TimeSpan.FromSeconds(10) })
                {
                    string html = http.GetStringAsync(server.ScreenUrl).GetAwaiter().GetResult();
                    var refused = http.GetAsync(server.LocalUrl + "etat").GetAwaiter().GetResult();
                    var ask = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, server.LocalUrl + "etat");
                    ask.Headers.Add("X-EM", "1");
                    string state = http.SendAsync(ask).GetAwaiter().GetResult().Content.ReadAsStringAsync().GetAwaiter().GetResult();
                    screen = html.Contains("<title>Endurance Manager</title>") && (int)refused.StatusCode == 403 && state.Contains("\"modules\"") && state.Contains("Porsche 963");
                    Console.WriteLine((screen ? "OK    " : "ÉCHEC ") + $"écran web : html {html.Length} o, /etat sans en-tête {(int)refused.StatusCode}, /etat {state.Length} o");
                }
                string browser = WallServer.Browser();
                if (browser == null) Console.WriteLine("Pas d'Edge ni de Chrome : pas de photos de l'écran web.");
                else
                {
                    // The update window (UpdateWindow.cs): a version as if the site gave it, and the one just installed.
                    var changes = Updater.Read(Newtonsoft.Json.Linq.JArray.Parse("[{type:'nouveau',text:'Le mur des stands solo',page:'mur'},{type:'mieux',text:'Profils : appliquer en un appui',page:'profils'},{type:'corrige',text:'Le popup restait affiché en sortant des stands'}]"));
                    plugin.Updater.Pretend(new Version(9, 9, 0), changes);
                    string kept = Path.Combine(Path.GetTempPath(), "EnduranceManagerPreview", "update");
                    Directory.CreateDirectory(kept);
                    File.WriteAllText(Path.Combine(kept, "changements.json"), new Newtonsoft.Json.Linq.JObject { ["version"] = Updater.Current.ToString(3), ["changes"] = Newtonsoft.Json.Linq.JArray.Parse("[{type:'nouveau',text:'Le nouvel écran du plugin',page:'home'},{type:'nouveau',text:'Un guide pour bien démarrer',page:'guide'}]") }.ToString());
                    foreach (var (name, hash, w, h) in new[] { ("ecran-accueil", "#home", 1280, 820), ("ecran-reglages-voiture", "#profils", 1280, 820), ("ecran-mur-des-stands", "#mur", 1280, 820),
                        ("ecran-overlay-relais", "#relais", 1280, 820), ("ecran-guide-modules", "#guide:1", 1280, 820), ("ecran-etroit", "#home", 860, 820), ("ecran-mises-a-jour", "#maj", 1280, 820),
                        ("fenetre-maj", "?fenetre=maj", 520, 600), ("fenetre-installee", "?fenetre=installee", 520, 600) })
                    {
                        string file = Path.GetFullPath(Path.Combine(dir, name + ".png"));
                        var run = System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(browser,
                            $"--headless=new --disable-gpu --hide-scrollbars --no-first-run --user-data-dir=\"{Path.Combine(Path.GetTempPath(), "em-ecran-photos")}\" --window-size={w},{h} --force-device-scale-factor=1.5 --virtual-time-budget=4000 --screenshot=\"{file}\" {server.ScreenUrl}{hash}")
                            { UseShellExecute = false, CreateNoWindow = true });
                        run.WaitForExit(30000);
                        bool ok = File.Exists(file);
                        if (ok) shots++;
                        Console.WriteLine((ok ? "OK    " : "ÉCHEC ") + name);
                    }
                }
                server.Stop();
            }
            catch (Exception e) { Console.WriteLine("ÉCHEC écran web : " + e.Message); }
            Console.WriteLine(made + shots + " photos dans " + Path.GetFullPath(dir));
            // The plugin's threads (sharing, updates) must not keep the program open.
            Environment.Exit(made == 12 && page && screen ? 0 : 1);
            return 0;
        }

        // The main tab, then the module's tab inside it.
        static void Open(FrameworkElement shell, int tab, int module)
        {
            var tabs = Find<TabControl>(shell).First();
            tabs.SelectedIndex = tab;
            Idle();
            var inner = Find<TabControl>((DependencyObject)((TabItem)tabs.Items[tab]).Content).First();
            inner.SelectedIndex = module;
        }

        static System.Collections.Generic.IEnumerable<T> Find<T>(DependencyObject root) where T : DependencyObject
        {
            if (root is T found) yield return found;
            foreach (var child in LogicalTreeHelper.GetChildren(root).OfType<DependencyObject>())
                foreach (var item in Find<T>(child)) yield return item;
        }

        static void Idle()
        {
            for (int i = 0; i < 3; i++) Dispatcher.CurrentDispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
        }
    }
}
