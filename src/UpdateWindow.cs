// La petite fenêtre de mise à jour (maquette validée par Nathan le 22/11/2026) : par-dessus SimHub, la page de l'écran
// (src/ecran/index.html?fenetre=…) dans WebView2. « maj » : une version est sortie, ses changements, Mettre à jour /
// Plus tard. « installee » : après le redémarrage, la version qu'on a et où trouver ses nouveautés (« Voir »).
// Sans WebView2, pas de fenêtre : l'accueil et la page Mises à jour de l'écran disent la même chose.
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EnduranceManager.SimHub
{
    public static class UpdateWindow
    {
        static Window open;

        public static void Show(EnduranceManagerPlugin plugin, string mode)
        {
            var dispatcher = Application.Current?.Dispatcher;
            var server = plugin.WallServer;
            if (dispatcher == null || server == null) return;
            dispatcher.BeginInvoke(new Action(() =>
            {
                try
                {
                    if (open != null) { open.Close(); open = null; }
                    Window window = null;
                    var view = WebScreen.Make(server.ScreenUrl + "?fenetre=" + mode, () => dispatcher.BeginInvoke(new Action(() => window?.Close())));
                    if (view == null) return;
                    window = new Window
                    {
                        Title = "Endurance Manager",
                        Content = new Border { Width = 520, Height = 600, Child = view },
                        SizeToContent = SizeToContent.WidthAndHeight,
                        ResizeMode = ResizeMode.NoResize,
                        Background = new SolidColorBrush(Color.FromRgb(7, 9, 10)),
                        ShowInTaskbar = true,
                    };
                    var owner = Application.Current.MainWindow;
                    if (owner != null && owner.IsVisible && owner.WindowState != WindowState.Minimized) { window.Owner = owner; window.WindowStartupLocation = WindowStartupLocation.CenterOwner; }
                    else window.WindowStartupLocation = WindowStartupLocation.CenterScreen;
                    window.Closed += (sender, args) => { if (open == window) open = null; (view as IDisposable)?.Dispose(); };
                    open = window;
                    window.Show();
                }
                catch (Exception e) { Report.Error("Fenêtre de mise à jour", e); }
            }));
        }

        public static void Close()
        {
            Application.Current?.Dispatcher.BeginInvoke(new Action(() => { open?.Close(); open = null; }));
        }
    }
}
