// Le popup des profils : au 1er appui sur le bouton d'un profil, il montre ce qui va changer (en roulant et au prochain
// arrêt) et attend le 2e appui pendant 5 s. Une petite fenêtre toujours au premier plan, que le clic traverse et qui ne
// prend jamais le focus du jeu. Sa place se choisit avec « Placer le popup » (onglet Réglages voiture en course) et reste.
// Au-dessus de LMU seulement en mode fenêtré sans bordure ; les mêmes textes sont aussi en propriétés pour les dashs.
using System;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Threading;

namespace EnduranceManager.SimHub
{
    public sealed class ProfilePopup : Window
    {
        readonly EnduranceManagerPlugin plugin;
        readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
        Profiles.PopupView shown;
        Border bar;
        bool placing;

        // Made once, on SimHub's window thread, when the plugin starts.
        public static ProfilePopup Start(EnduranceManagerPlugin plugin)
        {
            ProfilePopup popup = null;
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null) return null;
            dispatcher.Invoke(() => popup = new ProfilePopup(plugin));
            return popup;
        }

        ProfilePopup(EnduranceManagerPlugin plugin)
        {
            this.plugin = plugin;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            Topmost = true;
            ShowInTaskbar = false;
            ShowActivated = false;
            Focusable = false;
            SizeToContent = SizeToContent.WidthAndHeight;
            ResizeMode = ResizeMode.NoResize;
            Title = "Endurance Manager · Profil";
            SourceInitialized += (sender, args) => ClickThrough(true);
            timer.Tick += (sender, args) => Tick();
            timer.Start();
        }

        public void Stop() => Dispatcher.Invoke(() => { timer.Stop(); Close(); });

        // Every 100 ms: the profile's popup shown while it lasts, the bar of the confirmation emptying.
        void Tick()
        {
            if (placing) return;
            var view = plugin.Settings.Profiles ? plugin.Profiles?.Popup : null;
            var now = DateTime.UtcNow;
            if (view == null || now >= view.Until) { if (IsVisible) Hide(); return; }
            if (view != shown) { shown = view; Content = Card(view, out bar); }
            if (bar != null) bar.Width = 308 * Math.Max(0, Math.Min(1, (view.Until - now).TotalMilliseconds / (view.Until - view.Since).TotalMilliseconds));
            if (!IsVisible) { Show(); PlaceAtSaved(); Topmost = false; Topmost = true; }
        }

        void PlaceAtSaved()
        {
            UpdateLayout();
            Left = plugin.Settings.PopupLeft ?? (SystemParameters.PrimaryScreenWidth - ActualWidth) / 2;
            Top = plugin.Settings.PopupTop ?? 80;
        }

        // « Placer le popup »: an example shown where it is, to drag with the mouse; « OK » keeps the place.
        public void Place()
        {
            Dispatcher.Invoke(() =>
            {
                placing = true;
                var example = new Profiles.PopupView { Title = "« Pluie »", Help = "Fais-le glisser où tu veux, puis clique OK.", Confirm = false };
                example.Drive.Add(("TC", "5", "7"));
                example.Drive.Add(("ABS", "4", "6"));
                example.Pit.Add(("Pneus", "Medium", "Wet"));
                var card = Card(example, out _);
                var ok = Theme.Primary("OK");
                ok.Margin = new Thickness(0, 10, 0, 0);
                ok.HorizontalAlignment = HorizontalAlignment.Right;
                ok.Click += (sender, args) =>
                {
                    plugin.SetModule(x => { x.PopupLeft = Left; x.PopupTop = Top; });
                    placing = false; shown = null;
                    ClickThrough(true);
                    Hide();
                };
                ((StackPanel)((Border)card).Child).Children.Add(ok);
                Content = card;
                Cursor = Cursors.SizeAll;
                Show();
                PlaceAtSaved();
                ClickThrough(false);
                Activate();
            });
        }

        protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
        {
            base.OnMouseLeftButtonDown(e);
            if (placing && !(e.OriginalSource is FrameworkElement source && source.TemplatedParent is Button)) try { DragMove(); } catch { }
        }

        // The card: title, the lines « setting  now → profile » in the car and for the next stop, the help, the bar.
        public static FrameworkElement Card(Profiles.PopupView view, out Border bar)
        {
            var inside = new StackPanel { Width = 308 };
            inside.Children.Add(new TextBlock { Text = view.Title, FontSize = 19, FontWeight = FontWeights.Bold, Foreground = Theme.Text, TextTrimming = TextTrimming.CharacterEllipsis });
            Section(inside, "En roulant", view.Drive);
            Section(inside, "Prochain arrêt", view.Pit);
            if (!string.IsNullOrEmpty(view.Help))
                inside.Children.Add(new TextBlock { Text = view.Help, FontSize = 13, Foreground = view.Confirm ? Theme.Accent : Theme.Muted, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 0) });
            bar = null;
            if (view.Confirm)
            {
                bar = new Border { Height = 4, Width = 308, Background = Theme.Accent, CornerRadius = new CornerRadius(2), HorizontalAlignment = HorizontalAlignment.Left };
                inside.Children.Add(new Border { Height = 4, Background = Theme.Border, CornerRadius = new CornerRadius(2), Margin = new Thickness(0, 8, 0, 0), Child = bar });
            }
            return new Border
            {
                Background = Theme.Panel, BorderBrush = Theme.Accent, BorderThickness = new Thickness(2), CornerRadius = Theme.Medium,
                Padding = new Thickness(16, 14, 16, 14), Child = inside, Margin = new Thickness(10),
                Effect = new System.Windows.Media.Effects.DropShadowEffect { BlurRadius = 18, ShadowDepth = 0, Opacity = 0.7 },
            };
        }

        static void Section(StackPanel inside, string title, System.Collections.Generic.List<(string Label, string Now, string Next)> lines)
        {
            if (lines.Count == 0) return;
            inside.Children.Add(new TextBlock { Text = title.ToUpperInvariant(), FontSize = 11, FontWeight = FontWeights.Bold, Foreground = Theme.Muted, Margin = new Thickness(0, 10, 0, 4) });
            var grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            foreach (var (line, row) in lines.Take(8).Select((line, row) => (line, row)))
            {
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                Cell(grid, row, 0, line.Label, Theme.Text, FontWeights.Normal);
                Cell(grid, row, 1, line.Now + "  →  ", Theme.Faint, FontWeights.Normal);
                Cell(grid, row, 2, line.Next, Theme.Accent, FontWeights.Bold);
            }
            inside.Children.Add(grid);
        }

        static void Cell(Grid grid, int row, int column, string text, Brush color, FontWeight weight)
        {
            var cell = new TextBlock { Text = text, FontSize = 15, Foreground = color, FontWeight = weight, Margin = new Thickness(0, 1, 0, 1),
                HorizontalAlignment = column == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, TextTrimming = TextTrimming.CharacterEllipsis };
            Grid.SetRow(cell, row);
            Grid.SetColumn(cell, column);
            grid.Children.Add(cell);
        }

        // Clicks go through to the game and the window never takes its focus; off while the pilot places it.
        void ClickThrough(bool on)
        {
            var handle = new WindowInteropHelper(this).Handle;
            if (handle == IntPtr.Zero) return;
            int style = GetWindowLong(handle, ExStyle) | ToolWindow | NoActivate;
            style = on ? style | Transparent : style & ~Transparent;
            if (!on) style &= ~NoActivate;
            SetWindowLong(handle, ExStyle, style);
        }

        const int ExStyle = -20, Transparent = 0x20, ToolWindow = 0x80, NoActivate = 0x08000000;
        [DllImport("user32.dll")] static extern int GetWindowLong(IntPtr hwnd, int index);
        [DllImport("user32.dll")] static extern int SetWindowLong(IntPtr hwnd, int index, int value);
    }
}
