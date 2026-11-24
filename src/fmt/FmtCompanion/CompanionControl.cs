using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;

namespace FmtCompanion;

public sealed class CompanionControl : UserControl
{
	private sealed class Row
	{
		public FmtFunction Function;

		public Border Element;

		public CheckBox Check;

		public Card Card;

		public Border Button;

		public Button Test;

		public bool Ready;

		public bool Assigned;

		public Ellipse Dot;
	}

	private sealed class Card
	{
		public string Title;

		public Border Element;

		public TextBlock Name;

		public TextBlock Count;

		public bool Collapsible;

		public bool Open;

		public StackPanel Content;

		public List<Row> Rows = new List<Row>();

		public List<Fold> Folds = new List<Fold>();

		public List<TextBlock> SubHeads = new List<TextBlock>();
	}

	private sealed class Fold
	{
		public TextBlock Header;

		public StackPanel Body;

		public bool Open;

		public List<Row> Rows = new List<Row>();
	}

	private sealed class CardSpec
	{
		public string Title;

		public Func<FmtFunction, bool> Match;

		public bool Collapsible;

		public Func<FmtFunction, string> SubGroup;

		public Func<FmtFunction, bool> InFold;

		public string FoldTitle;
	}

	private static readonly Brush Bg = Hex("#0A0B0C");

	private static readonly Brush CardBg = Hex("#121416");

	private static readonly Brush RowHover = Hex("#181B1D");

	private static readonly Brush Field = Hex("#202326");

	private static readonly Brush Line = Hex("#303638");

	private static readonly Brush Accent = Hex("#35B978");

	private static readonly Brush AccentDark = Hex("#1F8F5A");

	private static readonly Brush Ink = Hex("#06140D");

	private static readonly Brush Text = Hex("#F0F3F2");

	private static readonly Brush Dim = Hex("#99A3A0");

	private static readonly Brush BannerBg = Hex("#17140C");

	private static readonly Brush BannerLine = Hex("#6E5A2A");

	private static readonly Brush Amber = Hex("#D9A640");

	private static readonly Brush Red = Hex("#E23A3A");

	private static readonly Brush GoodBg = Hex("#12301F");

	private static readonly Brush WarnBg = Hex("#2A2414");

	private static readonly Brush BlueBg = Hex("#12242E");

	private static readonly Brush Blue = Hex("#5FB7E8");

	private static readonly Brush RotaryBg = Hex("#2A2214");

	private static readonly Brush Rotary = Hex("#E4A341");

	private static readonly FontFamily Font = new FontFamily("Segoe UI");

	private static readonly FontFamily Numbers = new FontFamily("Bahnschrift Condensed, Bahnschrift SemiBold Condensed, Segoe UI");

	private static readonly CornerRadius Radius = new CornerRadius(10.0);

	private readonly FMTCompanionPlugin plugin;

	private readonly DispatcherTimer timer = new DispatcherTimer
	{
		Interval = TimeSpan.FromSeconds(1.0)
	};

	private readonly List<Row> rows = new List<Row>();

	private readonly List<Card> cards = new List<Card>();

	private readonly Dictionary<string, Row> byId = new Dictionary<string, Row>();

	private readonly Border lmuPill = Pill();

	private readonly Border mapperPill = Pill();

	private readonly CheckBox writes = Switch("Commandes actives");

	private readonly Border banner = new Border
	{
		Background = new SolidColorBrush(Color.FromArgb(20, 217, 166, 64)),
		BorderBrush = Hex("#D9A640"),
		BorderThickness = new Thickness(4.0, 0.0, 0.0, 0.0),
		Padding = new Thickness(16.0, 12.0, 16.0, 12.0),
		Margin = new Thickness(0.0, 16.0, 0.0, 0.0),
		Visibility = Visibility.Collapsed
	};

	private readonly TextBlock bannerText = Label("", 13.0, Hex("#FFE2A8"));

	private readonly TextBlock bannerDetails = Label("", 12.0, Hex("#FFE2A8"));

	private readonly TextBox search = new TextBox
	{
		FontFamily = Font,
		FontSize = 14.0,
		Foreground = Text,
		Background = Field,
		BorderBrush = Line,
		BorderThickness = new Thickness(1.0),
		Padding = new Thickness(10.0, 6.0, 10.0, 6.0),
		CaretBrush = Brushes.White,
		Width = 280.0,
		VerticalContentAlignment = VerticalAlignment.Center
	};

	private readonly TextBlock counter = new TextBlock
	{
		FontSize = 16.0,
		Foreground = Dim,
		FontFamily = Numbers,
		FontWeight = FontWeights.Bold,
		VerticalAlignment = VerticalAlignment.Center,
		Margin = new Thickness(16.0, 0.0, 16.0, 0.0)
	};

	private readonly Button choose = Flat("Choisir mes commandes", primary: false);

	private readonly Button prepare = Flat("Préparer LMU", primary: true);

	private readonly Button undo = Flat("Annuler ce que FMT a ajouté", primary: false);

	private readonly Button detail = Flat("Voir le détail", primary: false);

	private readonly Border prepareBox = new Border
	{
		Background = CardBg,
		BorderBrush = Line,
		BorderThickness = new Thickness(1.0),
		CornerRadius = Radius,
		Padding = new Thickness(14.0, 10.0, 14.0, 10.0),
		Margin = new Thickness(0.0, 12.0, 0.0, 0.0),
		Visibility = Visibility.Collapsed
	};

	private readonly TextBlock prepareSummary = Label("", 14.0, Text);

	private readonly TextBlock prepareLog = Label("", 13.0, Dim);

	private readonly TextBlock testText = new TextBlock
	{
		FontSize = 13.0,
		Foreground = Text,
		FontFamily = Font,
		Text = "—",
		TextTrimming = TextTrimming.CharacterEllipsis
	};

	private readonly TextBlock footer = Label("", 13.0, Dim);

	private readonly TextBlock checkSummary = Label("", 13.0, Dim);

	private string lastPressed = "";

	private readonly Grid columns = new Grid
	{
		Margin = new Thickness(0.0, 14.0, 0.0, 0.0)
	};

	private bool choosing;

	private int columnCount;

	private JObject directInput;

	private string lmuError;

	private int tick;

	private int fetching;

	private string lastCombo = "";

	private static readonly Brush PillLine = new SolidColorBrush(Color.FromArgb(41, byte.MaxValue, byte.MaxValue, byte.MaxValue));

	private static readonly Brush PillFill = new SolidColorBrush(Color.FromArgb(10, byte.MaxValue, byte.MaxValue, byte.MaxValue));

	private static readonly Brush PillText = Hex("#DBE3E4");

	private static readonly Brush HeadFill = Hex("#181B1D");

	private static readonly string[][] Pairs = new string[7][]
	{
		new string[2] { "Next", "Previous" },
		new string[2] { "Plus", "Minus" },
		new string[2] { ".Up", ".Down" },
		new string[2] { ".In", ".Out" },
		new string[2] { "Forward", "Rearward" },
		new string[2] { "Fore", "Aft" },
		new string[2] { "Left", "Right" }
	};

	private static string simHubVersion;

	private static Brush Hex(string hex)
	{
		SolidColorBrush solidColorBrush = (SolidColorBrush)new BrushConverter().ConvertFromString(hex);
		solidColorBrush.Freeze();
		return solidColorBrush;
	}

	private static string Spaced(string text)
	{
		return string.Join("\u200a", from c in text.ToUpperInvariant()
			select c.ToString());
	}

	public CompanionControl(FMTCompanionPlugin plugin)
	{
		CompanionControl companionControl = this;
		this.plugin = plugin;
		base.FontFamily = Font;
		base.Foreground = Text;
		StackPanel stackPanel = new StackPanel
		{
			Margin = new Thickness(16.0)
		};
		DockPanel dockPanel = new DockPanel();
		CheckBox checkBox = writes;
		RoutedEventHandler value = delegate
		{
			plugin.Service.EnableWrites(companionControl.writes.IsChecked == true);
			companionControl.Refresh();
		};
		checkBox.Click += value;
		DockPanel.SetDock(writes, Dock.Right);
		dockPanel.Children.Add(writes);
		WrapPanel wrapPanel = new WrapPanel
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		wrapPanel.Children.Add(new TextBlock
		{
			Text = "FMT Companion",
			FontSize = 26.0,
			FontWeight = FontWeights.Bold,
			Foreground = Text,
			Margin = new Thickness(0.0, 0.0, 16.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		wrapPanel.Children.Add(lmuPill);
		wrapPanel.Children.Add(mapperPill);
		dockPanel.Children.Add(wrapPanel);
		stackPanel.Children.Add(dockPanel);
		StackPanel stackPanel2 = new StackPanel();
		DockPanel dockPanel2 = new DockPanel();
		StackPanel stackPanel3 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		Button button = Flat("Tester au garage", primary: true);
		button.Click += delegate
		{
			if (MessageBox.Show("Ouvre le menu des stands au garage et vérifie qu'il est normal.\n\nTout est correct ? FMT réactive alors les commandes du stand pour cette version de LMU.", "FMT Companion", MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes)
			{
				plugin.AcknowledgeLmuVersion();
				companionControl.Refresh();
			}
		};
		Button button2 = Flat("Détails", primary: false);
		button2.Click += delegate
		{
			bannerDetails.Visibility = ((bannerDetails.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible);
		};
		stackPanel3.Children.Add(button);
		stackPanel3.Children.Add(button2);
		DockPanel.SetDock(stackPanel3, Dock.Right);
		dockPanel2.Children.Add(stackPanel3);
		dockPanel2.Children.Add(bannerText);
		bannerText.VerticalAlignment = VerticalAlignment.Center;
		stackPanel2.Children.Add(dockPanel2);
		bannerDetails.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		bannerDetails.Visibility = Visibility.Collapsed;
		stackPanel2.Children.Add(bannerDetails);
		banner.Child = stackPanel2;
		stackPanel.Children.Add(banner);
		DockPanel dockPanel3 = new DockPanel
		{
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0)
		};
		StackPanel stackPanel4 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		choose.Click += delegate
		{
			choosing = !choosing;
			ApplyMode();
		};
		prepare.Click += async delegate
		{
			await RunPrepare(cancel: false);
		};
		undo.Click += async delegate
		{
			await RunPrepare(cancel: true);
		};
		Button button3 = Flat("Ouvrir Control Mapper", primary: false);
		button3.Click += delegate
		{
			if (plugin.Mapper != null)
			{
				plugin.Mapper.OpenMapper();
			}
		};
		Button button4 = Flat("Exporter le profil Stream Deck", primary: false);
		button4.Click += delegate
		{
			try
			{
				companionControl.testText.Text = "Profil Stream Deck exporté : " + plugin.ExportStreamDeckProfile();
			}
			catch (Exception ex2)
			{
				companionControl.testText.Text = "Export Stream Deck impossible : " + ex2.Message;
			}
		};
		stackPanel4.Children.Add(choose);
		stackPanel4.Children.Add(prepare);
		stackPanel4.Children.Add(button3);
		stackPanel4.Children.Add(button4);
		DockPanel.SetDock(stackPanel4, Dock.Right);
		dockPanel3.Children.Add(stackPanel4);
		Grid grid = new Grid
		{
			VerticalAlignment = VerticalAlignment.Center
		};
		TextBlock hint = Label("Rechercher une commande…", 14.0, Dim);
		hint.Margin = new Thickness(12.0, 0.0, 0.0, 0.0);
		hint.VerticalAlignment = VerticalAlignment.Center;
		hint.IsHitTestVisible = false;
		grid.Children.Add(search);
		grid.Children.Add(hint);
		search.TextChanged += delegate
		{
			hint.Visibility = ((companionControl.search.Text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
			companionControl.ApplyMode();
		};
		StackPanel stackPanel5 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		stackPanel5.Children.Add(grid);
		stackPanel5.Children.Add(counter);
		dockPanel3.Children.Add(stackPanel5);
		stackPanel.Children.Add(dockPanel3);
		DockPanel dockPanel4 = new DockPanel();
		StackPanel stackPanel6 = new StackPanel
		{
			Orientation = Orientation.Horizontal
		};
		detail.Click += delegate
		{
			prepareLog.Visibility = ((prepareLog.Visibility == Visibility.Visible) ? Visibility.Collapsed : Visibility.Visible);
			detail.Content = ((prepareLog.Visibility == Visibility.Visible) ? "Masquer le détail" : "Voir le détail");
		};
		stackPanel6.Children.Add(detail);
		stackPanel6.Children.Add(undo);
		DockPanel.SetDock(stackPanel6, Dock.Right);
		dockPanel4.Children.Add(stackPanel6);
		dockPanel4.Children.Add(prepareSummary);
		prepareSummary.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel7 = new StackPanel
		{
			Children = { (UIElement)dockPanel4 }
		};
		prepareLog.Margin = new Thickness(0.0, 8.0, 0.0, 0.0);
		prepareLog.Visibility = Visibility.Collapsed;
		stackPanel7.Children.Add(prepareLog);
		prepareBox.Child = stackPanel7;
		stackPanel.Children.Add(prepareBox);
		BuildCards();
		stackPanel.Children.Add(columns);
		DockPanel dockPanel5 = new DockPanel();
		Button button5 = Flat("Sauvegarder", primary: false);
		button5.Click += delegate
		{
			try
			{
				plugin.BackupMapping();
				companionControl.testText.Text = "Mapping sauvegardé dans " + FMTCompanionPlugin.BackupRoot + ".";
				companionControl.Refresh();
			}
			catch (Exception ex)
			{
				companionControl.testText.Text = "Sauvegarde impossible : " + ex.Message;
			}
		};
		DockPanel.SetDock(button5, Dock.Right);
		dockPanel5.Children.Add(button5);
		StackPanel stackPanel8 = new StackPanel
		{
			Orientation = Orientation.Horizontal,
			VerticalAlignment = VerticalAlignment.Center
		};
		stackPanel8.Children.Add(footer);
		stackPanel8.Children.Add(checkSummary);
		stackPanel8.Children.Add(Label("  ·  Dernière commande : ", 13.0, Dim));
		stackPanel8.Children.Add(testText);
		dockPanel5.Children.Add(stackPanel8);
		stackPanel.Children.Add(new Border
		{
			BorderBrush = Line,
			BorderThickness = new Thickness(0.0, 1.0, 0.0, 0.0),
			Padding = new Thickness(0.0, 12.0, 0.0, 0.0),
			Margin = new Thickness(0.0, 18.0, 0.0, 0.0),
			Child = dockPanel5
		});
		DockPanel dockPanel6 = new DockPanel
		{
			Background = Bg
		};
		Grid element = Band();
		DockPanel.SetDock(element, Dock.Top);
		dockPanel6.Children.Add(element);
		dockPanel6.Children.Add(new ScrollViewer
		{
			Content = stackPanel,
			VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
			HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
		});
		base.Content = dockPanel6;
		base.SizeChanged += delegate
		{
			Layout(force: false);
		};
		timer.Tick += delegate
		{
			Refresh();
		};
		base.Loaded += delegate
		{
			ApplyMode();
			Refresh();
			timer.Start();
		};
		base.Unloaded += delegate
		{
			timer.Stop();
		};
		ApplyMode();
	}

	private static Grid Band()
	{
		Grid grid = new Grid();
		grid.Height = 4.0;
		Grid grid2 = grid;
		for (int i = 0; i < 3; i++)
		{
			grid2.ColumnDefinitions.Add(new ColumnDefinition
			{
				Width = new GridLength((i != 1) ? 1 : 2, GridUnitType.Star)
			});
		}
		LinearGradientBrush linearGradientBrush = new LinearGradientBrush();
		linearGradientBrush.StartPoint = new Point(0.0, 0.0);
		linearGradientBrush.EndPoint = new Point(1.0, 0.0);
		LinearGradientBrush linearGradientBrush2 = linearGradientBrush;
		linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromRgb(24, 55, 42), 0.0));
		linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromRgb(42, 163, 106), 0.5));
		linearGradientBrush2.GradientStops.Add(new GradientStop(Color.FromRgb(24, 55, 42), 1.0));
		for (int j = 0; j < 3; j++)
		{
			Border border = new Border();
			border.Background = ((j == 1) ? linearGradientBrush2 : Hex("#202527"));
			Border element = border;
			Grid.SetColumn(element, j);
			grid2.Children.Add(element);
		}
		return grid2;
	}

	private static TextBlock Label(string text, double size, Brush color)
	{
		TextBlock textBlock = new TextBlock();
		textBlock.Text = text;
		textBlock.FontSize = size;
		textBlock.Foreground = color;
		textBlock.FontFamily = Font;
		textBlock.TextWrapping = TextWrapping.Wrap;
		return textBlock;
	}

	private static Border Pill()
	{
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(new Ellipse
		{
			Width = 8.0,
			Height = 8.0,
			Margin = new Thickness(0.0, 0.0, 8.0, 0.0),
			VerticalAlignment = VerticalAlignment.Center
		});
		stackPanel2.Children.Add(new TextBlock
		{
			FontSize = 13.0,
			FontFamily = Font,
			Foreground = Text,
			VerticalAlignment = VerticalAlignment.Center
		});
		Border border = new Border();
		border.CornerRadius = new CornerRadius(7.0);
		border.Padding = new Thickness(10.0, 5.0, 10.0, 5.0);
		border.Margin = new Thickness(0.0, 0.0, 8.0, 0.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		border.Background = HeadFill;
		border.BorderBrush = PillLine;
		border.BorderThickness = new Thickness(1.0);
		border.Child = stackPanel2;
		return border;
	}

	private static void SetHeadPill(Border pill, string text, Brush dot)
	{
		StackPanel stackPanel = (StackPanel)pill.Child;
		((Ellipse)stackPanel.Children[0]).Fill = dot;
		((TextBlock)stackPanel.Children[1]).Text = text;
	}

	private static Border TagPill()
	{
		Border border = new Border();
		border.CornerRadius = new CornerRadius(7.0);
		border.Padding = new Thickness(8.0, 3.0, 8.0, 3.0);
		border.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		border.VerticalAlignment = VerticalAlignment.Center;
		border.BorderThickness = new Thickness(1.0);
		border.Child = new TextBlock
		{
			FontSize = 12.0,
			FontFamily = Font,
			VerticalAlignment = VerticalAlignment.Center
		};
		return border;
	}

	private static void SetPill(Border pill, string text, Brush color)
	{
		pill.BorderBrush = color;
		pill.Background = PillFill;
		TextBlock textBlock = (TextBlock)pill.Child;
		textBlock.Text = text;
		textBlock.Foreground = color;
	}

	private static Button Flat(string text, bool primary)
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border));
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, Radius);
		frameworkElementFactory.SetBinding(Border.BackgroundProperty, new Binding("Background")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		frameworkElementFactory.SetBinding(Border.PaddingProperty, new Binding("Padding")
		{
			RelativeSource = RelativeSource.TemplatedParent
		});
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory2.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Center);
		frameworkElementFactory2.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		ControlTemplate controlTemplate = new ControlTemplate(typeof(Button));
		controlTemplate.VisualTree = frameworkElementFactory;
		ControlTemplate controlTemplate2 = controlTemplate;
		Trigger trigger = new Trigger();
		trigger.Property = UIElement.IsEnabledProperty;
		trigger.Value = false;
		Trigger trigger2 = trigger;
		trigger2.Setters.Add(new Setter(UIElement.OpacityProperty, 0.45));
		controlTemplate2.Triggers.Add(trigger2);
		Button button = new Button
		{
			Content = text,
			Template = controlTemplate2,
			Background = (primary ? Accent : Field),
			Foreground = (primary ? Ink : Text),
			FontFamily = Font,
			FontSize = 13.0,
			FontWeight = (primary ? FontWeights.SemiBold : FontWeights.Normal),
			Padding = new Thickness(14.0, 7.0, 14.0, 7.0),
			Margin = new Thickness(8.0, 0.0, 0.0, 0.0),
			Cursor = Cursors.Hand
		};
		button.MouseEnter += delegate
		{
			if (button.Background != Brushes.Transparent)
			{
				button.Background = (primary ? AccentDark : RowHover);
			}
		};
		button.MouseLeave += delegate
		{
			if (button.Background != Brushes.Transparent)
			{
				button.Background = (primary ? Accent : Field);
			}
		};
		return button;
	}

	private static CheckBox Box()
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(Border), "box");
		frameworkElementFactory.SetValue(FrameworkElement.WidthProperty, 16.0);
		frameworkElementFactory.SetValue(FrameworkElement.HeightProperty, 16.0);
		frameworkElementFactory.SetValue(Border.CornerRadiusProperty, new CornerRadius(4.0));
		frameworkElementFactory.SetValue(Border.BorderThicknessProperty, new Thickness(1.5));
		frameworkElementFactory.SetValue(Border.BorderBrushProperty, Hex("#4A5254"));
		frameworkElementFactory.SetValue(Border.BackgroundProperty, Brushes.Transparent);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(System.Windows.Shapes.Path), "mark");
		frameworkElementFactory2.SetValue(System.Windows.Shapes.Path.DataProperty, Geometry.Parse("M 3,8 L 6.5,11.5 L 13,4.5"));
		frameworkElementFactory2.SetValue(Shape.StrokeProperty, Ink);
		frameworkElementFactory2.SetValue(Shape.StrokeThicknessProperty, 2.0);
		frameworkElementFactory2.SetValue(UIElement.VisibilityProperty, Visibility.Collapsed);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		ControlTemplate controlTemplate = new ControlTemplate(typeof(CheckBox));
		controlTemplate.VisualTree = frameworkElementFactory;
		ControlTemplate controlTemplate2 = controlTemplate;
		Trigger trigger = new Trigger();
		trigger.Property = ToggleButton.IsCheckedProperty;
		trigger.Value = true;
		Trigger trigger2 = trigger;
		trigger2.Setters.Add(new Setter(Border.BackgroundProperty, Accent, "box"));
		trigger2.Setters.Add(new Setter(Border.BorderBrushProperty, Accent, "box"));
		trigger2.Setters.Add(new Setter(UIElement.VisibilityProperty, Visibility.Visible, "mark"));
		controlTemplate2.Triggers.Add(trigger2);
		CheckBox checkBox = new CheckBox();
		checkBox.Template = controlTemplate2;
		checkBox.VerticalAlignment = VerticalAlignment.Center;
		checkBox.Cursor = Cursors.Hand;
		return checkBox;
	}

	private static CheckBox Switch(string text)
	{
		FrameworkElementFactory frameworkElementFactory = new FrameworkElementFactory(typeof(StackPanel));
		frameworkElementFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
		frameworkElementFactory.SetValue(Panel.BackgroundProperty, Brushes.Transparent);
		FrameworkElementFactory frameworkElementFactory2 = new FrameworkElementFactory(typeof(Border), "track");
		frameworkElementFactory2.SetValue(FrameworkElement.WidthProperty, 38.0);
		frameworkElementFactory2.SetValue(FrameworkElement.HeightProperty, 20.0);
		frameworkElementFactory2.SetValue(Border.CornerRadiusProperty, new CornerRadius(4.0));
		frameworkElementFactory2.SetValue(Border.BackgroundProperty, Line);
		FrameworkElementFactory frameworkElementFactory3 = new FrameworkElementFactory(typeof(Rectangle), "thumb");
		frameworkElementFactory3.SetValue(Rectangle.RadiusXProperty, 3.0);
		frameworkElementFactory3.SetValue(Rectangle.RadiusYProperty, 3.0);
		frameworkElementFactory3.SetValue(FrameworkElement.WidthProperty, 14.0);
		frameworkElementFactory3.SetValue(FrameworkElement.HeightProperty, 14.0);
		frameworkElementFactory3.SetValue(Shape.FillProperty, Brushes.White);
		frameworkElementFactory3.SetValue(FrameworkElement.MarginProperty, new Thickness(3.0));
		frameworkElementFactory3.SetValue(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Left);
		frameworkElementFactory2.AppendChild(frameworkElementFactory3);
		FrameworkElementFactory frameworkElementFactory4 = new FrameworkElementFactory(typeof(ContentPresenter));
		frameworkElementFactory4.SetValue(FrameworkElement.MarginProperty, new Thickness(8.0, 0.0, 0.0, 0.0));
		frameworkElementFactory4.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
		frameworkElementFactory.AppendChild(frameworkElementFactory2);
		frameworkElementFactory.AppendChild(frameworkElementFactory4);
		ControlTemplate controlTemplate = new ControlTemplate(typeof(CheckBox));
		controlTemplate.VisualTree = frameworkElementFactory;
		ControlTemplate controlTemplate2 = controlTemplate;
		Trigger trigger = new Trigger();
		trigger.Property = ToggleButton.IsCheckedProperty;
		trigger.Value = true;
		Trigger trigger2 = trigger;
		trigger2.Setters.Add(new Setter(Border.BackgroundProperty, Accent, "track"));
		trigger2.Setters.Add(new Setter(FrameworkElement.HorizontalAlignmentProperty, HorizontalAlignment.Right, "thumb"));
		controlTemplate2.Triggers.Add(trigger2);
		CheckBox checkBox = new CheckBox();
		checkBox.Template = controlTemplate2;
		checkBox.Content = new TextBlock
		{
			Text = text,
			FontSize = 13.0,
			Foreground = Text,
			FontFamily = Font
		};
		checkBox.VerticalAlignment = VerticalAlignment.Center;
		checkBox.Cursor = Cursors.Hand;
		return checkBox;
	}

	private static IEnumerable<CardSpec> Specs()
	{
		Func<string, Func<FmtFunction, bool>> pit = (string g) => (FmtFunction f) => f.Family == "Stand" && f.Group == g;
		Func<string, Func<FmtFunction, bool>> cam = (string g) => (FmtFunction f) => f.Family == "Caméra" && f.Group == g;
		yield return new CardSpec
		{
			Title = "Ravitaillement",
			Match = (FmtFunction f) => pit("Énergie")(f) || pit("Ratio carburant")(f)
		};
		yield return new CardSpec
		{
			Title = "Pneus",
			Match = (FmtFunction f) => pit("Pneus")(f) || pit("Pneus par roue")(f),
			InFold = pit("Pneus par roue"),
			FoldTitle = "Par roue"
		};
		yield return new CardSpec
		{
			Title = "Pressions",
			Match = pit("Pressions"),
			InFold = (FmtFunction f) => !f.Id.Contains(".All."),
			FoldTitle = "Par roue"
		};
		yield return new CardSpec
		{
			Title = "Aéro et refroidissement",
			Match = pit("Aéro et refroidissement")
		};
		yield return new CardSpec
		{
			Title = "Réparations et freins",
			Match = (FmtFunction f) => pit("Réparations")(f) || pit("Freins")(f)
		};
		yield return new CardSpec
		{
			Title = "Pilote et Stop & Go",
			Match = (FmtFunction f) => pit("Pilote")(f) || pit("Stop & Go")(f)
		};
		yield return new CardSpec
		{
			Title = "Voiture",
			Match = (FmtFunction f) => f.Family == "Voiture",
			SubGroup = (FmtFunction f) => f.Group
		};
		yield return new CardSpec
		{
			Title = "Caméra — vues",
			Match = (FmtFunction f) => cam("Vues")(f) || cam("Swingman")(f),
			SubGroup = (FmtFunction f) => f.Group
		};
		yield return new CardSpec
		{
			Title = "Caméra — TV",
			Match = cam("TV")
		};
		yield return new CardSpec
		{
			Title = "Caméra libre",
			Match = cam("Caméra libre"),
			Collapsible = true
		};
		yield return new CardSpec
		{
			Title = "Objectif libre",
			Match = cam("Objectif libre"),
			Collapsible = true
		};
		yield return new CardSpec
		{
			Title = "Replay",
			Match = cam("Replay"),
			Collapsible = true
		};
		yield return new CardSpec
		{
			Title = "Poste",
			Match = (FmtFunction f) => f.Family == "Poste" && f.Group != "Rétroviseurs",
			SubGroup = (FmtFunction f) => f.Group
		};
		yield return new CardSpec
		{
			Title = "Rétroviseurs",
			Match = (FmtFunction f) => f.Family == "Poste" && f.Group == "Rétroviseurs",
			Collapsible = true
		};
	}

	private void BuildCards()
	{
		foreach (CardSpec item in Specs())
		{
			List<FmtFunction> list = Catalog.All.Where(item.Match).ToList();
			if (list.Count == 0)
			{
				continue;
			}
			Card card = new Card
			{
				Title = item.Title,
				Collapsible = item.Collapsible
			};
			DockPanel dockPanel = new DockPanel();
			dockPanel.Background = Brushes.Transparent;
			DockPanel dockPanel2 = dockPanel;
			card.Count = new TextBlock
			{
				FontSize = 14.0,
				Foreground = Dim,
				FontFamily = Numbers,
				FontWeight = FontWeights.Bold,
				VerticalAlignment = VerticalAlignment.Center
			};
			DockPanel.SetDock(card.Count, Dock.Right);
			dockPanel2.Children.Add(card.Count);
			card.Name = new TextBlock
			{
				FontSize = 12.0,
				FontWeight = FontWeights.SemiBold,
				Foreground = Dim,
				FontFamily = Font,
				VerticalAlignment = VerticalAlignment.Center
			};
			dockPanel2.Children.Add(card.Name);
			card.Content = new StackPanel();
			string text = null;
			Fold fold = null;
			foreach (FmtFunction item2 in list)
			{
				Row row = MakeRow(item2, card);
				if (item.SubGroup != null && item.SubGroup(item2) != text)
				{
					text = item.SubGroup(item2);
					TextBlock textBlock = Label(Spaced(text), 11.0, Dim);
					textBlock.Margin = new Thickness(0.0, (card.Content.Children.Count != 0) ? 10 : 0, 0.0, 4.0);
					textBlock.Tag = text;
					card.SubHeads.Add(textBlock);
					card.Content.Children.Add(textBlock);
				}
				if (item.InFold != null && item.InFold(item2))
				{
					if (fold == null)
					{
						Fold fold2 = new Fold();
						fold2.Body = new StackPanel();
						fold2.Header = Label("", 12.0, Dim);
						Fold current;
						fold = (current = fold2);
						string foldTitle = item.FoldTitle;
						int count = list.Count(item.InFold);
						current.Header.Cursor = Cursors.Hand;
						current.Header.Margin = new Thickness(0.0, 6.0, 0.0, 4.0);
						current.Header.Text = "▸  " + foldTitle + " (" + count + ")";
						current.Header.MouseLeftButtonUp += delegate
						{
							current.Open = !current.Open;
							current.Header.Text = (current.Open ? "▾  " : "▸  ") + foldTitle + " (" + count + ")";
							ApplyMode();
						};
						card.Folds.Add(current);
						card.Content.Children.Add(current.Header);
						card.Content.Children.Add(current.Body);
					}
					fold.Body.Children.Add(row.Element);
					fold.Rows.Add(row);
				}
				else
				{
					card.Content.Children.Add(row.Element);
				}
			}
			if (item.Collapsible)
			{
				dockPanel2.Cursor = Cursors.Hand;
				dockPanel2.MouseLeftButtonUp += delegate
				{
					if (choosing)
					{
						card.Open = !card.Open;
						ApplyMode();
					}
				};
			}
			StackPanel stackPanel = new StackPanel();
			stackPanel.Children.Add(new Border
			{
				BorderBrush = Line,
				BorderThickness = new Thickness(0.0, 0.0, 0.0, 1.0),
				Padding = new Thickness(0.0, 0.0, 0.0, 10.0),
				Margin = new Thickness(0.0, 0.0, 0.0, 8.0),
				Child = dockPanel2
			});
			stackPanel.Children.Add(card.Content);
			card.Element = new Border
			{
				Background = CardBg,
				BorderBrush = Line,
				BorderThickness = new Thickness(1.0),
				CornerRadius = Radius,
				Padding = new Thickness(16.0),
				Margin = new Thickness(0.0, 0.0, 0.0, 16.0),
				Child = stackPanel
			};
			cards.Add(card);
		}
	}

	private Row MakeRow(FmtFunction f, Card card)
	{
		Grid grid = new Grid();
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = new GridLength(1.0, GridUnitType.Star)
		});
		grid.ColumnDefinitions.Add(new ColumnDefinition
		{
			Width = GridLength.Auto
		});
		CheckBox check = Box();
		check.IsChecked = plugin.Settings.VisibleCommands.Contains(f.Id);
		check.Margin = new Thickness(0.0, 0.0, 10.0, 0.0);
		Ellipse ellipse = new Ellipse();
		ellipse.Width = 7.0;
		ellipse.Height = 7.0;
		ellipse.Margin = new Thickness(0.0, 0.0, 9.0, 0.0);
		ellipse.VerticalAlignment = VerticalAlignment.Center;
		ellipse.Visibility = Visibility.Collapsed;
		Ellipse ellipse2 = ellipse;
		TextBlock textBlock = new TextBlock();
		textBlock.Text = f.Label;
		textBlock.FontSize = 14.0;
		textBlock.Foreground = Text;
		textBlock.FontFamily = Font;
		textBlock.TextTrimming = TextTrimming.CharacterEllipsis;
		textBlock.VerticalAlignment = VerticalAlignment.Center;
		TextBlock textBlock2 = textBlock;
		StackPanel stackPanel = new StackPanel();
		stackPanel.Orientation = Orientation.Horizontal;
		stackPanel.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel2 = stackPanel;
		stackPanel2.Children.Add(ellipse2);
		stackPanel2.Children.Add(textBlock2);
		StackPanel stackPanel3 = new StackPanel();
		stackPanel3.Orientation = Orientation.Horizontal;
		stackPanel3.VerticalAlignment = VerticalAlignment.Center;
		StackPanel stackPanel4 = stackPanel3;
		Border border = TagPill();
		Button test = Flat("Tester", primary: false);
		test.Background = Brushes.Transparent;
		test.Foreground = Dim;
		test.FontSize = 11.0;
		test.Padding = new Thickness(8.0, 1.0, 8.0, 1.0);
		test.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
		test.Visibility = Visibility.Hidden;
		test.MouseEnter += delegate
		{
			test.Foreground = Accent;
		};
		test.MouseLeave += delegate
		{
			test.Foreground = Dim;
		};
		test.Click += delegate
		{
			Test(f);
		};
		stackPanel4.Children.Add(test);
		stackPanel4.Children.Add(border);
		Grid.SetColumn(stackPanel2, 1);
		Grid.SetColumn(stackPanel4, 2);
		grid.Children.Add(check);
		grid.Children.Add(stackPanel2);
		grid.Children.Add(stackPanel4);
		Border cell = new Border
		{
			CornerRadius = new CornerRadius(4.0),
			Padding = new Thickness(6.0, 0.0, 6.0, 0.0),
			Margin = new Thickness(-6.0, 0.0, -6.0, 0.0),
			MinHeight = 32.0,
			Background = Brushes.Transparent,
			Child = grid
		};
		cell.MouseEnter += delegate
		{
			cell.Background = RowHover;
			test.Visibility = Visibility.Visible;
		};
		cell.MouseLeave += delegate
		{
			cell.Background = Brushes.Transparent;
			test.Visibility = Visibility.Hidden;
		};
		Row row = new Row();
		row.Function = f;
		row.Element = cell;
		row.Check = check;
		row.Card = card;
		row.Button = border;
		row.Dot = ellipse2;
		row.Test = test;
		Row row2 = row;
		string id = f.Id;
		Action toggle = delegate
		{
			plugin.SetCommandVisible(id, check.IsChecked == true);
			ApplyMode();
		};
		check.Click += delegate
		{
			toggle();
		};
		textBlock2.MouseLeftButtonUp += delegate
		{
			if (choosing)
			{
				check.IsChecked = check.IsChecked != true;
				toggle();
			}
		};
		rows.Add(row2);
		card.Rows.Add(row2);
		byId[f.Id] = row2;
		UpdateRow(row2, null, full: false);
		return row2;
	}

	private bool Checked(Row r)
	{
		return r.Check.IsChecked == true;
	}

	private bool Matches(Row r, string q)
	{
		if (q.Length != 0)
		{
			return (r.Function.Label + " " + r.Function.Group + " " + r.Card.Title).ToLowerInvariant().Contains(q);
		}
		return true;
	}

	private void ApplyMode()
	{
		string text = search.Text.Trim().ToLowerInvariant();
		choose.Content = (choosing ? "Terminé" : "Choisir mes commandes");
		foreach (Card card in cards)
		{
			bool flag = !card.Collapsible || !choosing || card.Open || text.Length > 0;
			int num = 0;
			foreach (Row row in card.Rows)
			{
				bool flag2 = (choosing || Checked(row)) && Matches(row, text);
				row.Element.Visibility = ((!flag2) ? Visibility.Collapsed : Visibility.Visible);
				row.Check.Visibility = ((!choosing) ? Visibility.Collapsed : Visibility.Visible);
				row.Element.Opacity = ((choosing && !Checked(row)) ? 0.45 : 1.0);
				if (flag2)
				{
					num++;
				}
			}
			foreach (Fold fold in card.Folds)
			{
				bool flag3 = fold.Rows.Any((Row r) => r.Element.Visibility == Visibility.Visible);
				fold.Header.Visibility = ((!choosing || !flag3 || text.Length != 0) ? Visibility.Collapsed : Visibility.Visible);
				fold.Body.Visibility = ((choosing && !fold.Open && text.Length <= 0) ? Visibility.Collapsed : Visibility.Visible);
			}
			foreach (TextBlock sub in card.SubHeads)
			{
				sub.Visibility = ((!card.Rows.Any((Row r) => r.Function.Group == (string)sub.Tag && r.Element.Visibility == Visibility.Visible)) ? Visibility.Collapsed : Visibility.Visible);
			}
			card.Content.Visibility = ((!flag) ? Visibility.Collapsed : Visibility.Visible);
			card.Name.Text = ((card.Collapsible && choosing) ? (flag ? "▾  " : "▸  ") : "") + Spaced(card.Title);
			card.Element.Visibility = ((num <= 0) ? Visibility.Collapsed : Visibility.Visible);
		}
		UpdateCounts();
		Layout(force: true);
	}

	private void Layout(bool force)
	{
		double num = ((base.ActualWidth > 0.0) ? (base.ActualWidth - 32.0) : 1300.0);
		int num2 = ((num >= 1700.0) ? 4 : ((num >= 1150.0) ? 3 : ((!(num >= 760.0)) ? 1 : 2)));
		if (!force && num2 == columnCount)
		{
			return;
		}
		columnCount = num2;
		foreach (UIElement child in columns.Children)
		{
			((StackPanel)child).Children.Clear();
		}
		columns.Children.Clear();
		columns.ColumnDefinitions.Clear();
		StackPanel[] array = new StackPanel[num2];
		for (int i = 0; i < num2; i++)
		{
			columns.ColumnDefinitions.Add(new ColumnDefinition());
			array[i] = new StackPanel
			{
				Margin = new Thickness((i != 0) ? 8 : 0, 0.0, (i != num2 - 1) ? 8 : 0, 0.0)
			};
			Grid.SetColumn(array[i], i);
			columns.Children.Add(array[i]);
		}
		double[] array2 = new double[num2];
		foreach (Card item in cards.Where((Card c) => c.Element.Visibility == Visibility.Visible))
		{
			int num3 = Array.IndexOf(array2, array2.Min());
			array[num3].Children.Add(item.Element);
			array2[num3] += 2.5 + (double)((item.Content.Visibility == Visibility.Visible) ? item.Rows.Count((Row r) => r.Element.Visibility == Visibility.Visible) : 0);
		}
	}

	private void UpdateCounts()
	{
		foreach (Card card in cards)
		{
			card.Count.Text = (choosing ? (card.Rows.Count(Checked) + " / " + card.Rows.Count) : card.Rows.Count(Checked).ToString());
		}
		int num = rows.Count(Checked);
		int num2 = rows.Count((Row r) => Checked(r) && r.Assigned);
		counter.Text = (choosing ? (num + " cochées sur " + rows.Count) : (num + " commandes  ·  " + num2 + " sur un bouton"));
	}

	private static string Partner(string id)
	{
		string[][] pairs = Pairs;
		foreach (string[] array in pairs)
		{
			if (id.EndsWith(array[0]))
			{
				return id.Substring(0, id.Length - array[0].Length) + array[1];
			}
			if (id.EndsWith(array[1]))
			{
				return id.Substring(0, id.Length - array[1].Length) + array[0];
			}
		}
		return null;
	}

	private void UpdateRow(Row row, JObject di, bool full)
	{
		if (!full)
		{
			row.Button.Visibility = Visibility.Collapsed;
			return;
		}
		FmtFunction function = row.Function;
		List<KeyValuePair<string, int>> list = plugin.Output.AssignedButtons(function.Role);
		bool flag = plugin.StreamDeckRoles().Contains(function.Role);
		row.Assigned = list.Count > 0 || flag;
		row.Button.Visibility = ((!row.Assigned) ? Visibility.Collapsed : Visibility.Visible);
		if (list.Count > 0)
		{
			bool flag2 = false;
			string text = Partner(function.Id);
			FmtFunction fmtFunction = ((text == null) ? null : Catalog.Find(text));
			if (fmtFunction != null)
			{
				List<KeyValuePair<string, int>> other = plugin.Output.AssignedButtons(fmtFunction.Role);
				flag2 = list.Any((KeyValuePair<string, int> a) => a.Value >= 0 && other.Any((KeyValuePair<string, int> b) => b.Key == a.Key && Math.Abs(b.Value - a.Value) == 1));
			}
			KeyValuePair<string, int> keyValuePair = list[0];
			string text2 = ((keyValuePair.Value >= 0) ? ("B" + (keyValuePair.Value + 1)) : "croix") + ((list.Count > 1) ? (" +" + (list.Count - 1)) : "");
			SetPill(row.Button, (flag2 ? "rotatif " : "") + text2 + (flag ? " · Stream Deck" : ""), flag2 ? Rotary : Blue);
		}
		else if (flag)
		{
			SetPill(row.Button, "Stream Deck", Blue);
		}
		string text3 = function.Role;
		if (list.Count > 0)
		{
			text3 = text3 + Environment.NewLine + "Bouton : " + string.Join(", ", list.Select((KeyValuePair<string, int> a) => a.Key + ((a.Value >= 0) ? (" bouton " + (a.Value + 1)) : "")));
		}
		if (flag)
		{
			text3 = text3 + Environment.NewLine + "Touche Stream Deck";
		}
		row.Ready = true;
		switch (function.Kind)
		{
		case FunctionKind.Pit:
			text3 = text3 + Environment.NewLine + "Menu des stands de LMU";
			row.Dot.Visibility = Visibility.Collapsed;
			break;
		case FunctionKind.Api:
			text3 = text3 + Environment.NewLine + "API de LMU";
			row.Dot.Visibility = Visibility.Collapsed;
			break;
		case FunctionKind.Combo:
		{
			byId.TryGetValue(function.SeatId, out var value);
			row.Ready = value?.Ready ?? false;
			text3 = text3 + Environment.NewLine + KeyboardCombo.Help(function);
			ShowStatus(row);
			break;
		}
		default:
		{
			if (di == null)
			{
				row.Ready = false;
				row.Dot.Visibility = Visibility.Collapsed;
				text3 = text3 + Environment.NewLine + ((lmuError != null) ? "LMU non lu" : "Lecture de LMU…");
				break;
			}
			text3 = text3 + Environment.NewLine + Preparer.State(di, function, (int n) => plugin.Output.OutputRole(n), plugin.Output.Bridge, out var ready);
			row.Ready = ready;
			ShowStatus(row);
			break;
		}
		}
		row.Element.ToolTip = ((function.Kind == FunctionKind.LmuKey || function.Kind == FunctionKind.Combo) ? ((row.Ready ? "Prêt dans LMU" : "À préparer dans LMU") + Environment.NewLine) : "") + text3;
	}

	private static void ShowStatus(Row row)
	{
		row.Dot.Visibility = Visibility.Visible;
		row.Dot.Fill = (row.Ready ? Accent : Amber);
	}

	private void Test(FmtFunction f)
	{
		if (plugin.Mapper == null)
		{
			testText.Text = "Control Mapper pas encore prêt.";
			return;
		}
		bool flag = plugin.Mapper.Test(f);
		if (f.Kind == FunctionKind.Combo)
		{
			testText.Text = (flag ? ("Test « " + f.Label + " » : retourne dans LMU en vue cockpit, envoi dès qu'il est au premier plan (10 s max).") : plugin.Combo.Status);
		}
		else
		{
			testText.Text = "Test « " + f.Label + " » : " + ((!flag) ? ((f.Kind == FunctionKind.LmuKey) ? "rien envoyé, préparer LMU d'abord ou vérifier la sortie." : "commande indisponible.") : ((f.Kind == FunctionKind.LmuKey) ? "impulsion envoyée par la sortie Control Mapper." : "commande envoyée."));
		}
	}

	private async Task RunPrepare(bool cancel)
	{
		Button button = prepare;
		bool isEnabled = (undo.IsEnabled = false);
		button.IsEnabled = isEnabled;
		prepareBox.Visibility = Visibility.Visible;
		prepareLog.Text = "";
		prepareSummary.Text = (cancel ? "Annulation…" : "Préparation… ne touche pas au volant pendant l'écoute de LMU.");
		prepareSummary.Foreground = Text;
		Action<string> log = delegate(string line)
		{
			base.Dispatcher.BeginInvoke((Action)delegate
			{
				TextBlock textBlock = prepareLog;
				textBlock.Text = textBlock.Text + ((prepareLog.Text.Length > 0) ? Environment.NewLine : "") + line;
				Match match = Regex.Match(line, "^Terminé : (\\d+) fonction");
				bool flag3 = line.Contains("arrêt") || line.Contains("interrompu") || line.Contains("impossible") || line.Contains("Pas assez") || line.Contains("Aucune sortie");
				if (match.Success)
				{
					prepareSummary.Text = match.Groups[1].Value + " commandes préparées";
					prepareSummary.Foreground = Accent;
				}
				else if (line.StartsWith("Annulé"))
				{
					prepareSummary.Text = line;
					prepareSummary.Foreground = Accent;
				}
				else if (flag3)
				{
					prepareSummary.Text = line;
					prepareSummary.Foreground = Red;
				}
				else if (line.StartsWith("Rien à préparer"))
				{
					prepareSummary.Text = "Tout est déjà prêt";
					prepareSummary.Foreground = Accent;
				}
				else
				{
					prepareSummary.Text = line;
				}
			});
		};
		try
		{
			if (cancel)
			{
				await Task.Run(() => plugin.Preparer.Undo(log, CancellationToken.None));
				return;
			}
			if (plugin.Mapper != null)
			{
				plugin.Mapper.RefreshRoles();
			}
			List<FmtFunction> wanted = Catalog.All.Where((FmtFunction f) => f.Kind == FunctionKind.LmuKey && plugin.Settings.VisibleCommands.Contains(f.Id)).ToList();
			await Task.Run(() => plugin.Preparer.Prepare(wanted, log, CancellationToken.None));
		}
		finally
		{
			Button button2 = prepare;
			bool isEnabled2 = (undo.IsEnabled = true);
			button2.IsEnabled = isEnabled2;
			FetchLmu();
		}
	}

	private void FetchLmu()
	{
		if (Interlocked.Exchange(ref fetching, 1) == 1)
		{
			return;
		}
		Task.Run(async delegate
		{
			int num = default(int);
			_ = num;
			_ = 0;
			try
			{
				_ = num;
				_ = 0;
				try
				{
					JObject jObject = LmuControlsModel.DirectInput(await plugin.Preparer.Controls(CancellationToken.None));
					JObject di = jObject;
					directInput = di;
					lmuError = null;
				}
				catch (Exception ex)
				{
					directInput = null;
					lmuError = ex.Message;
				}
			}
			finally
			{
				Volatile.Write(ref fetching, 0);
			}
		});
	}

	private void Refresh()
	{
		if (!base.IsVisible)
		{
			return;
		}
		plugin.Touch();
		ServiceState state = plugin.Service.State;
		bool flag = tick++ % 3 == 0;
		if (flag)
		{
			FetchLmu();
		}
		string readOnlyReason = plugin.Service.ReadOnlyReason;
		if (!state.Connected)
		{
			SetHeadPill(lmuPill, "LMU non joignable", Red);
		}
		else if (state.Menu == null || readOnlyReason != null)
		{
			SetHeadPill(lmuPill, "LMU connecté · " + ((state.Menu == null) ? "menu des stands non lu" : "menu des stands en lecture seule"), Amber);
		}
		else
		{
			SetHeadPill(lmuPill, "LMU connecté · menu des stands lu", Accent);
		}
		int num = ((plugin.Mapper != null) ? plugin.Mapper.RoleCount : 0);
		if (plugin.Output.Settings != null && plugin.Mapper != null && plugin.Mapper.Ready)
		{
			SetHeadPill(mapperPill, "Control Mapper relié · " + num + " rôles", Accent);
		}
		else
		{
			SetHeadPill(mapperPill, "Control Mapper non relié", Red);
		}
		writes.IsChecked = plugin.Service.WritesEnabled;
		string updateNotice = plugin.UpdateNotice;
		banner.Visibility = ((updateNotice == null && readOnlyReason == null) ? Visibility.Collapsed : Visibility.Visible);
		bannerText.Text = (updateNotice ?? "Le menu des stands a changé.") + ((readOnlyReason != null) ? " Menu des stands en lecture seule jusqu'à vérification." : " Vérifie le menu des stands au garage.");
		bannerDetails.Text = ((readOnlyReason != null) ? ("Lecture seule : " + readOnlyReason + Environment.NewLine) : "") + ((state.Menu == null) ? "Menu des stands non lu." : ("Menu actuel :" + Environment.NewLine + string.Join(Environment.NewLine, state.Menu.Rows.Select((JObject r) => "  " + (string?)r["name"] + " " + state.Menu.Value((string?)r["name"]))))) + Environment.NewLine + "Dernier résultat : " + state.Status;
		if (flag)
		{
			JObject jObject = directInput;
			foreach (Row item in rows.OrderBy((Row r) => (r.Function.Kind == FunctionKind.Combo) ? 1 : 0))
			{
				UpdateRow(item, jObject, full: true);
			}
			UpdateCounts();
			List<CheckLine> list = plugin.SelfCheck().ToList();
			if (jObject == null && lmuError != null)
			{
				list.Add(new CheckLine
				{
					Ok = false,
					Text = "Écran des commandes LMU : " + lmuError
				});
			}
			else if (jObject != null && plugin.Output.Usable)
			{
				HashSet<int> used = LmuControlsModel.UsedButtons(jObject, plugin.Output.Bridge);
				int num2 = Enumerable.Range(1, plugin.Output.ButtonLimit).Count((int n) => !used.Contains(n) && plugin.Output.Output(n) != null && string.IsNullOrEmpty(plugin.Output.OutputRole(n)));
				list.Add(new CheckLine
				{
					Ok = (num2 > 0),
					Text = num2 + " boutons libres sur la sortie"
				});
			}
			int num3 = list.Count((CheckLine l) => !l.Ok);
			checkSummary.Text = "  ·  Auto-contrôle : " + ((num3 == 0) ? "OK" : (num3 + " alerte" + ((num3 > 1) ? "s" : "")));
			checkSummary.Foreground = ((num3 == 0) ? Accent : Red);
			checkSummary.ToolTip = string.Join(Environment.NewLine, list.Select((CheckLine l) => (l.Ok ? "✓ " : "✗ ") + l.Text));
			DateTime? dateTime = FMTCompanionPlugin.LastBackup();
			footer.Text = "SimHub " + SimHubVersion() + "  ·  LMU " + (plugin.LmuVersion ?? "?") + "  ·  FMT " + FMTCompanionPlugin.Version + "  ·  Mapping sauvegardé : " + (dateTime.HasValue ? dateTime.Value.ToString("dd/MM/yyyy HH:mm") : "jamais");
		}
		if (plugin.LastLabel != lastPressed)
		{
			lastPressed = plugin.LastLabel;
			testText.Text = lastPressed;
		}
		if (plugin.Combo != null && plugin.Combo.Status != lastCombo)
		{
			lastCombo = plugin.Combo.Status;
			testText.Text = lastCombo;
		}
	}

	private static string SimHubVersion()
	{
		if (simHubVersion != null)
		{
			return simHubVersion;
		}
		try
		{
			simHubVersion = FileVersionInfo.GetVersionInfo(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(typeof(PluginManager).Assembly.Location), "unins000.exe")).ProductVersion.Trim();
		}
		catch (Exception)
		{
			simHubVersion = "?";
		}
		return simHubVersion;
	}

	public void Stop()
	{
		timer.Stop();
	}
}
