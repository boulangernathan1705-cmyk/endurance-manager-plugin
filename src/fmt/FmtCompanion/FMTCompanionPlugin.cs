using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;
using SimHub.Plugins.OutputPlugins.ControlRemapper.Models;

namespace FmtCompanion;

[PluginName("FMT companion")]
[PluginDescription("Stand, voiture, caméra et poste LMU depuis Control Mapper et Stream Deck.")]
[PluginAuthor("FMT")]
public sealed class FMTCompanionPlugin : IPlugin, IWPFSettingsV2, IWPFSettings
{
	public const string Version = "0.6.0";

	internal PitService Service;

	internal Settings Settings;

	internal ControlMapperBridge Mapper;

	internal MapperOutput Output;

	internal Preparer Preparer;

	internal KeyboardCombo Combo;

	private CompanionControl view;

	private Timer guard;

	private readonly HashSet<string> keyLabels = new HashSet<string>(StringComparer.Ordinal);

	private string lastLabel = "";

	private bool lastIsPit;

	private int lastCount;

	internal volatile string LmuVersion;

	internal volatile string UpdateNotice;

	internal volatile string ApiStatus = "";

	private volatile bool lmuRunning = true;

	private int lmuPid;

	private HashSet<string> streamDeckRoles = new HashSet<string>(StringComparer.Ordinal);

	private DateTime streamDeckRead = DateTime.MinValue;

	private readonly object memoLock = new object();

	private PitMenu memoMenu;

	private readonly Dictionary<string, object> memo = new Dictionary<string, object>();

	public PluginManager PluginManager { get; set; }

	public string LeftMenuTitle => "FMT companion";

	public ImageSource PictureIcon => null;

	internal string LastLabel => lastLabel;

	internal static string BackupRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "FMT companion", "Sauvegardes");

	public void Init(PluginManager pluginManager)
	{
		PluginManager = pluginManager;
		Settings = this.ReadCommonSettings("Settings", () => new Settings());
		if (Settings == null)
		{
			Settings = new Settings();
		}
		if (Settings.Shortcuts == null)
		{
			Settings.Shortcuts = new List<Shortcut>();
		}
		while (Settings.Shortcuts.Count < 16)
		{
			Settings.Shortcuts.Add(new Shortcut());
		}
		for (int i = 0; i < 16; i++)
		{
			if (Settings.Shortcuts[i] == null)
			{
				Settings.Shortcuts[i] = new Shortcut();
			}
		}
		if (Settings.Prepared == null)
		{
			Settings.Prepared = new List<PreparedKey>();
		}
		if (Settings.VisibleCommands == null)
		{
			Settings.VisibleCommands = (from f in Catalog.All
				where f.DefaultVisible
				select f.Id).ToList();
		}
		else if (Settings.CatalogVersion < 5)
		{
			Settings.VisibleCommands.AddRange(from f in Catalog.All
				where f.DefaultVisible && !Catalog.LegacyCommands.Contains(f.Id) && !Settings.VisibleCommands.Contains(f.Id)
				select f.Id);
		}
		Settings.CatalogVersion = 5;
		foreach (FmtFunction item in Catalog.All)
		{
			if (item.Kind != 0)
			{
				keyLabels.Add(item.Family + " - " + item.Label);
			}
		}
		Service = new PitService(new LmuTransport());
		Output = new MapperOutput(pluginManager);
		Preparer = new Preparer(Output, Settings, Save);
		Combo = new KeyboardCombo(Output);
		foreach (PitCommand item2 in Commands.Create())
		{
			PitCommand command = item2;
			this.AddAction(command.Name, delegate
			{
				Service.Enqueue(command);
			});
			if (Catalog.LegacyCommands.Contains(command.Name))
			{
				this.AddAction(Catalog.LegacyLabel(command.Name), delegate
				{
					Service.Enqueue(command);
				});
			}
		}
		foreach (FmtFunction item3 in Catalog.All.Where((FmtFunction f) => f.Kind != FunctionKind.Pit))
		{
			FmtFunction f2 = item3;
			this.AddAction(f2.Id, delegate
			{
				if (Mapper != null)
				{
					Mapper.Test(f2);
				}
			});
		}
		this.AddAction("Commands.Disable", delegate
		{
			Service.EnableWrites(value: false);
		});
		this.AddAction("Commandes.Suspendre", delegate
		{
			Service.EnableWrites(value: false);
		});
		this.AddAction("Commandes.Activer", delegate
		{
			Service.EnableWrites(value: true);
		});
		for (int j = 0; j < 16; j++)
		{
			int slot = j;
			this.AddAction("Custom." + (slot + 1).ToString("00"), delegate
			{
				RunShortcut(slot);
			});
			this.AttachDelegate("Custom." + (slot + 1).ToString("00") + ".Label", () => Settings.Shortcuts[slot].Label);
		}
		Show("Connected", () => Service.State.Connected);
		this.AttachDelegate("CommandsEnabled", () => Service.WritesEnabled);
		Show("Status", () => Service.State.Status);
		Show("LastReadUtc", () => Service.State.UpdatedUtc.ToString("O"));
		this.AttachDelegate("ReadOnly", () => Service.ReadOnlyReason != null);
		this.AttachDelegate("LmuVersion", () => LmuVersion ?? "");
		string[] array = new string[20]
		{
			"FL TIRE:", "FR TIRE:", "RL TIRE:", "RR TIRE:", "FUEL:", "VIRTUAL ENERGY:", "FUEL RATIO:", "DRIVER:", "STOP/GO:", "DAMAGE:",
			"REPLACE BRAKES:", "TIRES:", "R WING:", "GRILLE:", "F BRAKE DUCT:", "R BRAKE DUCT:", "FL PRESS:", "FR PRESS:", "RL PRESS:", "RR PRESS:"
		};
		foreach (string text in array)
		{
			string row2 = text;
			string text2 = row2.Replace(":", "").Replace(" ", "");
			Show("Pit." + text2, () => ReadValue(row2));
		}
		Show("Pit.VE.Percent", () => (int)Number("VIRTUAL ENERGY:"));
		Show("Pit.Fuel.Litres", () => Number("FUEL:"));
		Show("Pit.Ratio", () => Number("FUEL RATIO:"));
		string[] wheels = Commands.Wheels;
		foreach (string text3 in wheels)
		{
			string row = text3 + " TIRE:";
			Show("Pit.Tyre." + text3 + ".Compound", () => Compound(row));
			Show("Pit.Tyre." + text3 + ".Change", delegate
			{
				int num2 = Compound(row);
				return (num2 >= 0) ? ((num2 != 0) ? 1 : 0) : (-1);
			});
			Show("Pit.Tyre." + text3 + ".Name", () => CompoundName(row));
			Show("Pit.Tyre." + text3 + ".Badge", () => Badge(CompoundName(row)));
		}
		Show("Pit.Tyres.Name", () => CompoundName("TIRES:"));
		Show("Pit.Tyres.Badge", () => Badge(CompoundName("TIRES:")));
		Show("Pit.VE.Step", delegate
		{
			double num = Number("VIRTUAL ENERGY:");
			return (!(num < 0.0)) ? Math.Min(3, (int)(num / 25.0)) : (-1);
		});
		Show("Pit.Brakes.Replace", () => Yes("REPLACE BRAKES:"));
		Show("Pit.StopGo.Serve", () => Yes("STOP/GO:"));
		// Read the actual countdown from the selected LMU menu choice; never replace it with a guessed value.
		Show("Pit.StopGo.LapsLeft", () => StopGoDisplay.Laps(ReadValue("STOP/GO:")));
		Show("Pit.StopGo.DeckText", () => {
			string value = ReadValue("STOP/GO:");
			int laps = StopGoDisplay.Laps(value);
			if (laps < 0) return "";
			string status = Regex.IsMatch(value.Trim(), @"^(OUI|YES)\b", RegexOptions.IgnoreCase) ? "OUI" : "NON";
			return "SG\n" + (laps == 0 ? "URGENT" : laps == 1 ? "CE TOUR" : laps.ToString(CultureInfo.InvariantCulture) + " T") + "\n" + status;
		});
		this.AttachDelegate("Last.Command", () => lastLabel);
		this.AttachDelegate("Last.Ok", () => (!lastIsPit) ? ((lastCount > 0) ? 1 : (-1)) : Volatile.Read(ref Service.LastOk));
		this.AttachDelegate("Last.Count", () => lastCount);
		this.AttachDelegate("Last.Result", () => (lastIsPit ? Volatile.Read(ref Service.LastOk) : ((lastCount > 0) ? 1 : (-1))) switch
		{
			0 => "REFUS", 
			1 => "OK", 
			_ => "—", 
		});
		Show("Pit.Driver.Short", delegate
		{
			string text5 = ReadValue("DRIVER:");
			if (text5 == "Indisponible" || text5 == "Non proposé")
			{
				return "—";
			}
			string[] array2 = text5.Split(' ');
			string text6 = array2[array2.Length - 1];
			return (text6.Length <= 9) ? text6 : text6.Substring(0, 9);
		});
		Show("Pit.Damage.Short", delegate
		{
			string text4 = ReadValue("DAMAGE:").ToUpperInvariant();
			if (text4 == "INDISPONIBLE" || text4 == "NON PROPOSÉ")
			{
				return "—";
			}
			return (!text4.Contains("CARROSS") && !text4.Contains("BODY")) ? ((!text4.Contains("TOUT") && !text4.Contains("ALL")) ? ((!text4.Contains("NE PAS") && !text4.Contains("DON") && (!text4.Contains("NO") || text4.Contains("N/A"))) ? ((text4.Length <= 8) ? text4 : text4.Substring(0, 8)) : "AUCUNE") : "TOUT") : "CARROSS.";
		});
		Show("Pit.StopGo.Short", () => Yes("STOP/GO:") switch
		{
			0 => "NON", 
			1 => "OUI", 
			_ => "—", 
		});
		Show("Pit.Brakes.Short", () => Yes("REPLACE BRAKES:") switch
		{
			0 => "GARDER", 
			1 => "NEUFS", 
			_ => "—", 
		});
		Service.EnableWrites(value: true);
		Service.Start();
		Mapper = new ControlMapperBridge(pluginManager, Service, Settings, Output, CallApi, (FmtFunction f) => Combo.Run(f));
		Mapper.testCombo = (FmtFunction f) => Combo.Run(f, 10);
		Mapper.comboReleased = delegate
		{
			Combo.Release();
		};
		Mapper.Pressed = delegate(string label)
		{
			lastLabel = label;
			lastIsPit = !keyLabels.Contains(label);
			Interlocked.Increment(ref lastCount);
		};
		Service.GameRunning = () => lmuRunning;
		Service.Wanted = () => Shown;
		Service.Seen = CheckMenu;
		guard = new Timer(delegate
		{
			CheckLmu();
		}, null, 0, 10000);
	}

	// Endurance Manager (23/11/2026). Whether LMU runs: told by Endurance Manager (its shared memory) when it is set, so
	// LMU's processes are looked at only when the game appears (for its version), no longer every 10 s.
	public Func<bool> LmuOpen;

	// The menu's values are read by a screen (Stream Deck, dash, its own screen): the menu is then read between two
	// commands (PitService.Wanted). Nobody shows them: nothing is read for them.
	private long shownTicks;

	public void Touch()
	{
		Interlocked.Exchange(ref shownTicks, DateTime.UtcNow.Ticks);
	}

	private bool Shown => DateTime.UtcNow.Ticks - Interlocked.Read(ref shownTicks) < TimeSpan.FromSeconds(3.0).Ticks;

	private void Show<T>(string name, Func<T> get)
	{
		this.AttachDelegate(name, delegate
		{
			Touch();
			return get();
		});
	}

	private void CheckLmu()
	{
		try
		{
			Func<bool> open = LmuOpen;
			int num;
			if (open != null && !open())
			{
				num = 0;
			}
			else if (open != null && lmuPid != 0)
			{
				num = lmuPid;
			}
			else
			{
				Process[] processesByName = Process.GetProcessesByName("Le Mans Ultimate");
				num = ((processesByName.Length > 0) ? processesByName[0].Id : 0);
				Process[] array = processesByName;
				foreach (Process process in array)
				{
					process.Dispose();
				}
			}
			lmuRunning = num != 0;
			if (num != 0 && num != lmuPid)
			{
				string text = LmuInstall.Version();
				if (text != null)
				{
					LmuVersion = text;
				}
			}
			lmuPid = num;
			CheckMenu(Service.State.Menu);
		}
		catch (Exception)
		{
		}
	}

	// A menu read (by the 10 s check, or before a command): lines LMU added that FMT companion does not know make the menu
	// read only until this LMU version is validated.
	private void CheckMenu(PitMenu menu)
	{
		try
		{
			if (menu != null && LmuVersion != null)
			{
				string[] array2 = menu.UnknownRows();
				if (Settings.ValidatedLmuVersion == null && array2.Length == 0)
				{
					Settings.ValidatedLmuVersion = LmuVersion;
					Save();
				}
				bool flag = Settings.ValidatedLmuVersion == LmuVersion;
				UpdateNotice = ((Settings.ValidatedLmuVersion != null && !flag) ? ("LMU a été mis à jour (" + Settings.ValidatedLmuVersion + " → " + LmuVersion + ").") : null);
				Service.ReadOnlyReason = ((array2.Length > 0 && !flag) ? ("ligne(s) inconnue(s) : " + string.Join(", ", array2)) : null);
			}
		}
		catch (Exception)
		{
		}
	}

	internal void AcknowledgeLmuVersion()
	{
		if (LmuVersion != null)
		{
			Settings.ValidatedLmuVersion = LmuVersion;
			Save();
			UpdateNotice = null;
			Service.ReadOnlyReason = null;
		}
	}

	private void CallApi(FmtFunction f)
	{
		Task.Run(async delegate
		{
			int num = default(int);
			_ = num;
			_ = 0;
			try
			{
				await Preparer.Put(f.ApiPath, CancellationToken.None);
				ApiStatus = f.Label + " : envoyé.";
			}
			catch (Exception ex)
			{
				ApiStatus = f.Label + " : " + ex.Message;
			}
		});
	}

	internal void SetCommandVisible(string id, bool visible)
	{
		if (visible && !Settings.VisibleCommands.Contains(id))
		{
			Settings.VisibleCommands.Add(id);
		}
		if (!visible)
		{
			Settings.VisibleCommands.RemoveAll((string n) => n == id);
		}
		Save();
		if (Mapper != null)
		{
			Mapper.RefreshRoles();
		}
	}

	internal IList<CheckLine> SelfCheck()
	{
		List<CheckLine> list = new List<CheckLine>();
		ControlMapperPluginSettings settings = Output.Settings;
		list.Add(new CheckLine
		{
			Ok = (settings != null),
			Text = ((settings != null) ? "Control Mapper chargé" : "Control Mapper absent : l'activer dans SimHub")
		});
		if (settings == null)
		{
			return list;
		}
		list.Add(new CheckLine
		{
			Ok = Output.Usable,
			Text = "Sortie Control Mapper : " + Output.Describe() + (Output.Usable ? "" : " — choisir vJoy ou le bridge pour les fonctions Voiture, Caméra et Poste")
		});
		if (settings.OutputMode == OutputMode.VJoy)
		{
			VJoyMapping vJoyMapping = settings.VJoyMapping;
			bool ok = vJoyMapping.IsVJoyInstalled && !vJoyMapping.VJoyIsMissing && !vJoyMapping.VJoyIsBusy;
			list.Add(new CheckLine
			{
				Ok = ok,
				Text = ((!vJoyMapping.IsVJoyInstalled) ? "vJoy non installé" : (vJoyMapping.VJoyIsMissing ? ("vJoy n°" + vJoyMapping.TargetVJoyId + " absent") : (vJoyMapping.VJoyIsBusy ? ("vJoy n°" + vJoyMapping.TargetVJoyId + " occupé par un autre programme") : ("vJoy n°" + vJoyMapping.TargetVJoyId + " piloté par SimHub, " + Output.ButtonLimit + " boutons"))))
			});
		}
		else if (settings.OutputMode == OutputMode.Bridge)
		{
			bool isConnected = settings.SimHubControllerRemapperBridgeMapping.IsConnected;
			list.Add(new CheckLine
			{
				Ok = isConnected,
				Text = (isConnected ? "Bridge SimHub connecté" : "Bridge SimHub non connecté")
			});
		}
		int num = ((Mapper != null) ? Mapper.RoleCount : 0);
		list.Add(new CheckLine
		{
			Ok = (Mapper != null && Mapper.Ready && num > 0),
			Text = num + " rôles FMT dans Control Mapper"
		});
		ServiceState state = Service.State;
		list.Add(new CheckLine
		{
			Ok = state.Connected,
			Text = (state.Connected ? ("LMU joignable" + ((LmuVersion != null) ? (" (version " + LmuVersion + ")") : "")) : "LMU non joignable : lancer le jeu")
		});
		return list;
	}

	internal string BackupMapping()
	{
		string text = Path.Combine(BackupRoot, DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss"));
		Directory.CreateDirectory(text);
		Save();
		string[] array = new string[1] { "ControlMapperPlugin.GeneralSettingsV2.json" };
		foreach (string text2 in array)
		{
			string commonStoragePath = PluginManager.GetCommonStoragePath(text2);
			if (File.Exists(commonStoragePath))
			{
				File.Copy(commonStoragePath, Path.Combine(text, text2));
			}
		}
		string directoryName = Path.GetDirectoryName(PluginManager.GetCommonStoragePath("x.json"));
		if (directoryName != null && Directory.Exists(directoryName))
		{
			string[] files = Directory.GetFiles(directoryName, "FMTCompanion*.json");
			foreach (string text3 in files)
			{
				File.Copy(text3, Path.Combine(text, Path.GetFileName(text3)));
			}
		}
		string text4 = LmuInstall.UserDataPlayer();
		if (text4 != null)
		{
			string[] array2 = new string[3] { "direct input.json", "keyboard.json", "current controls.json" };
			foreach (string text5 in array2)
			{
				if (File.Exists(Path.Combine(text4, text5)))
				{
					File.Copy(Path.Combine(text4, text5), Path.Combine(text, "LMU " + text5));
				}
			}
		}
		return text;
	}

	internal HashSet<string> StreamDeckRoles()
	{
		if (DateTime.UtcNow - streamDeckRead < TimeSpan.FromSeconds(15.0))
		{
			return streamDeckRoles;
		}
		streamDeckRead = DateTime.UtcNow;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		try
		{
			string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Elgato", "StreamDeck");
			string[] array = new string[2] { "ProfilesV3", "ProfilesV2" };
			foreach (string path2 in array)
			{
				string path3 = Path.Combine(path, path2);
				if (!Directory.Exists(path3))
				{
					continue;
				}
				string[] files = Directory.GetFiles(path3, "manifest.json", SearchOption.AllDirectories);
				foreach (string path4 in files)
				{
					try
					{
						foreach (JProperty item in ((JContainer)JToken.Parse(File.ReadAllText(path4))).Descendants().OfType<JProperty>())
						{
							if (item.Name == "rolename" && item.Value.Type == JTokenType.String)
							{
								hashSet.Add((string?)item.Value);
							}
						}
					}
					catch (Exception)
					{
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return streamDeckRoles = hashSet;
	}

	internal static DateTime? LastBackup()
	{
		try
		{
			return Directory.Exists(BackupRoot) ? Directory.GetDirectories(BackupRoot).Select(Directory.GetCreationTime).Cast<DateTime?>()
				.Max() : null;
		}
		catch (Exception)
		{
			return null;
		}
	}

	internal string ExportStreamDeckProfile()
	{
		Assembly assembly = typeof(FMTCompanionPlugin).Assembly;
		using (Stream stream = assembly.GetManifestResourceStream("fonctions-du-profil.txt"))
		{
			if (stream != null)
			{
				IEnumerable<string> source = from l in new StreamReader(stream).ReadToEnd().Split('\n')
					select l.Trim() into l
					where l.Length > 0 && Catalog.Find(l) != null
					select l;
				List<string> list = source.Where((string id) => !Settings.VisibleCommands.Contains(id)).ToList();
				if (list.Count > 0)
				{
					Settings.VisibleCommands.AddRange(list);
					Save();
					if (Mapper != null)
					{
						Mapper.RefreshRoles();
					}
				}
			}
		}
		string text = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Personal), "FMT companion");
		Directory.CreateDirectory(text);
		string text2 = Path.Combine(text, "FMT-companion-0.5.streamDeckProfile");
		using (Stream stream2 = assembly.GetManifestResourceStream("FMT-companion-0.5.streamDeckProfile"))
		{
			if (stream2 == null)
			{
				throw new InvalidOperationException("Profil Stream Deck absent de cette version.");
			}
			using FileStream destination = File.Create(text2);
			stream2.CopyTo(destination);
		}
		Process.Start(text2);
		return text2;
	}

	private T Memo<T>(string key, Func<T> compute)
	{
		PitMenu menu = Service.State.Menu;
		lock (memoLock)
		{
			if (!object.ReferenceEquals(menu, memoMenu))
			{
				memo.Clear();
				memoMenu = menu;
			}
			if (!memo.TryGetValue(key, out var value))
			{
				value = compute();
				memo[key] = value;
			}
			return (T)value;
		}
	}

	private double Number(string row)
	{
		return Memo("n" + row, () => NumberRaw(row));
	}

	private int Compound(string row)
	{
		return Memo("c" + row, () => CompoundRaw(row));
	}

	private string CompoundName(string row)
	{
		return Memo("m" + row, () => CompoundNameRaw(row));
	}

	private int Yes(string row)
	{
		return Memo("y" + row, () => YesRaw(row));
	}

	private string ReadValue(string name)
	{
		return Memo("v" + name, () => ReadValueRaw(name));
	}

	private double NumberRaw(string row)
	{
		PitMenu menu = Service.State.Menu;
		if (menu == null || !menu.Rows.Any((JObject r) => (string?)r["name"] == row))
		{
			return -1.0;
		}
		Match match = Regex.Match(menu.Value(row), "^\\s*([+-]?\\d+(?:[.,]\\d+)?)");
		if (!match.Success || !double.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result))
		{
			return -1.0;
		}
		return result;
	}

	private int CompoundRaw(string row)
	{
		PitMenu menu = Service.State.Menu;
		if (menu == null || !menu.Rows.Any((JObject r) => (string?)r["name"] == row))
		{
			return -1;
		}
		JObject jObject = menu.Find(row);
		JToken jToken = ((JArray)jObject["settings"])[(int)jObject["currentSetting"]];
		string text = ((jToken is JObject) ? ((string?)jToken["type"]) : null);
		if (text == null)
		{
			return 0;
		}
		return text switch
		{
			"Soft" => 1, 
			"Medium" => 2, 
			"Hard" => 3, 
			"Wet" => 4, 
			_ => 9, 
		};
	}

	private string CompoundNameRaw(string row)
	{
		PitMenu menu = Service.State.Menu;
		if (menu == null || !menu.Rows.Any((JObject r) => (string?)r["name"] == row))
		{
			return "—";
		}
		if (menu.Value(row).IndexOf("mixte", StringComparison.OrdinalIgnoreCase) >= 0 || menu.Value(row).IndexOf("mixed", StringComparison.OrdinalIgnoreCase) >= 0)
		{
			return "MIXTE";
		}
		return Compound(row) switch
		{
			0 => "GARDER", 
			1 => "SOFT", 
			2 => "MEDIUM", 
			3 => "HARD", 
			4 => "PLUIE", 
			_ => "AUTRE", 
		};
	}

	private static string Badge(string name)
	{
		return name switch
		{
			"SOFT" => "⚪ SOFT", 
			"MEDIUM" => "\ud83d\udfe0 MED", 
			"HARD" => "\ud83d\udd34 HARD", 
			"PLUIE" => "\ud83d\udd35 PLUIE", 
			_ => name, 
		};
	}

	private int YesRaw(string row)
	{
		PitMenu menu = Service.State.Menu;
		if (menu == null || !menu.Rows.Any((JObject r) => (string?)r["name"] == row))
		{
			return -1;
		}
		string text = menu.Value(row).Trim().ToUpperInvariant();
		if (!text.StartsWith("OUI") && !text.StartsWith("YES"))
		{
			return 0;
		}
		return 1;
	}

	private string ReadValueRaw(string name)
	{
		ServiceState state = Service.State;
		if (!state.Connected || state.Menu == null)
		{
			return "Indisponible";
		}
		try
		{
			return state.Menu.Value(name);
		}
		catch (InvalidOperationException)
		{
			return "Non proposé";
		}
	}

	internal void RunShortcut(int slot)
	{
		Shortcut shortcut = Settings.Shortcuts[slot];
		if (shortcut.Row != null)
		{
			string row = shortcut.Row;
			string text = shortcut.Text;
			string type = shortcut.Type;
			Service.Enqueue(new PitCommand("Custom." + (slot + 1), delegate(PitMenu m)
			{
				Commands.SetExact(m, row, text, type);
			}));
		}
	}

	internal void Save()
	{
		this.SaveCommonSettings("Settings", Settings);
	}

	public Control GetWPFSettingsControl(PluginManager manager)
	{
		return view ?? (view = new CompanionControl(this));
	}

	public void End(PluginManager manager)
	{
		if (guard != null)
		{
			guard.Dispose();
		}
		if (view != null)
		{
			view.Stop();
		}
		if (Mapper != null)
		{
			Mapper.Dispose();
		}
		if (Preparer != null)
		{
			Preparer.Dispose();
		}
		if (Service != null)
		{
			Service.Dispose();
		}
		Save();
	}
}
