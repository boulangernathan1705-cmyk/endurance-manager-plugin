using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Threading;
using Newtonsoft.Json.Linq;
using SimHub.Plugins;
using SimHub.Plugins.OutputPlugins.ControlRemapper;
using SimHub.Plugins.OutputPlugins.ControlRemapper.Models;

namespace FmtCompanion;

internal sealed class ControlMapperBridge : IDisposable
{
	public const string Suspend = "FMT - Commandes - Suspendre";

	public const string Resume = "FMT - Commandes - Activer";

	private readonly PluginManager manager;

	private readonly Settings settings;

	private readonly MapperOutput output;

	private readonly Func<FmtFunction, bool> combo;

	public Func<FmtFunction, bool> testCombo;

	public Action comboReleased;

	private readonly HashSet<string> comboRoles = new HashSet<string>(StringComparer.Ordinal);

	private readonly Dictionary<string, Action> handlers = new Dictionary<string, Action>(StringComparer.Ordinal);

	private readonly Dictionary<string, string> labels = new Dictionary<string, string>(StringComparer.Ordinal);

	private readonly HashSet<string> owned = new HashSet<string>(StringComparer.Ordinal);

	private readonly DispatcherTimer timer;

	private bool disposed;

	private bool subscribed;

	public Action<string> Pressed;

	public string Status { get; private set; }

	public bool Ready => subscribed;

	public int RoleCount => output.Settings?.OutputMapping.ControllerMapping.VirtualButtons.Count((VirtualButtonMap r) => owned.Contains(r.TargetRole)) ?? 0;

	public ControlMapperBridge(PluginManager manager, PitService service, Settings settings, MapperOutput output, Action<FmtFunction> api, Func<FmtFunction, bool> combo)
	{
		this.manager = manager;
		this.settings = settings;
		this.output = output;
		this.combo = combo;
		timer = new DispatcherTimer(DispatcherPriority.Background, Application.Current.Dispatcher)
		{
			Interval = TimeSpan.FromSeconds(1.0)
		};
		Dictionary<string, PitCommand> dictionary = Commands.Create().ToDictionary((PitCommand c) => c.Name);
		foreach (FmtFunction item in Catalog.All)
		{
			FmtFunction f = item;
			owned.Add(f.Role);
			labels[f.Role] = f.Family + " - " + f.Label;
			if (f.Kind == FunctionKind.Pit)
			{
				PitCommand c3 = dictionary[f.PitCommand];
				handlers[f.Role] = delegate
				{
					service.Enqueue(c3);
				};
			}
			else if (f.Kind == FunctionKind.Api)
			{
				handlers[f.Role] = delegate
				{
					api(f);
				};
			}
			else if (f.Kind == FunctionKind.Combo)
			{
				handlers[f.Role] = delegate
				{
					combo(f);
				};
				comboRoles.Add(f.Role);
			}
		}
		foreach (PitCommand item2 in dictionary.Values.Where((PitCommand c) => Catalog.LegacyCommands.Contains(c.Name)))
		{
			PitCommand c2 = item2;
			string text = Catalog.LegacyRole(c2.Name);
			owned.Add(text);
			labels[text] = Catalog.LegacyLabel(c2.Name);
			handlers[text] = delegate
			{
				service.Enqueue(c2);
			};
		}
		handlers["FMT - Commandes - Suspendre"] = delegate
		{
			service.EnableWrites(value: false);
		};
		handlers["FMT - Commandes - Activer"] = delegate
		{
			service.EnableWrites(value: true);
		};
		owned.Add("FMT - Commandes - Suspendre");
		owned.Add("FMT - Commandes - Activer");
		Status = "Préparation des rôles Control Mapper…";
		timer.Tick += Initialize;
		timer.Start();
	}

	internal static int EnsureRoles(ObservableCollection<VirtualButtonMap> roles, IEnumerable<string> names)
	{
		HashSet<string> hashSet = new HashSet<string>(roles.Select((VirtualButtonMap r) => r.TargetRole), StringComparer.Ordinal);
		int num = 0;
		foreach (string name in names)
		{
			if (hashSet.Add(name))
			{
				roles.Add(new VirtualButtonMap
				{
					TargetRole = name
				});
				num++;
			}
		}
		return num;
	}

	internal static int SynchronizeRoles(ObservableCollection<VirtualButtonMap> roles, IEnumerable<string> desired, IEnumerable<string> owned, IEnumerable<string> assigned)
	{
		HashSet<string> hashSet = new HashSet<string>(desired, StringComparer.Ordinal);
		HashSet<string> hashSet2 = new HashSet<string>(owned, StringComparer.Ordinal);
		HashSet<string> hashSet3 = new HashSet<string>(assigned, StringComparer.Ordinal);
		int num = EnsureRoles(roles, hashSet);
		VirtualButtonMap[] array = roles.ToArray();
		foreach (VirtualButtonMap virtualButtonMap in array)
		{
			if (hashSet2.Contains(virtualButtonMap.TargetRole) && !hashSet.Contains(virtualButtonMap.TargetRole) && !hashSet3.Contains(virtualButtonMap.TargetRole))
			{
				roles.Remove(virtualButtonMap);
				num++;
			}
		}
		return num;
	}

	private HashSet<string> AssignedRoles(ControlMapperPluginSettings mapperSettings)
	{
		JToken jToken = JToken.FromObject(mapperSettings.ControllerMappings);
		HashSet<string> hashSet = new HashSet<string>(from p in ((JContainer)jToken).Descendants().OfType<JProperty>()
			where p.Name == "TargetRole" && p.Value.Type == JTokenType.String
			select (string?)p.Value, StringComparer.Ordinal);
		hashSet.UnionWith(output.OutputRoles());
		return hashSet;
	}

	public void RefreshRoles()
	{
		try
		{
			ControlMapperPluginSettings controlMapperPluginSettings = output.Settings;
			if (controlMapperPluginSettings != null)
			{
				ObservableCollection<VirtualButtonMap> roles = controlMapperPluginSettings.OutputMapping.ControllerMapping.VirtualButtons;
				string[] desired = (from f in Catalog.All
					where settings.VisibleCommands.Contains(f.Id)
					select f.Role).Concat(new string[2] { "FMT - Commandes - Suspendre", "FMT - Commandes - Activer" }).ToArray();
				HashSet<string> assigned = AssignedRoles(controlMapperPluginSettings);
				if (desired.Any((string n) => !roles.Any((VirtualButtonMap r) => r.TargetRole == n)) || roles.Any((VirtualButtonMap r) => owned.Contains(r.TargetRole) && !desired.Contains(r.TargetRole) && !assigned.Contains(r.TargetRole)))
				{
					output.Backup();
					SynchronizeRoles(roles, desired, owned, assigned);
					output.Save();
				}
				Status = roles.Count((VirtualButtonMap r) => owned.Contains(r.TargetRole)) + " rôles FMT dans Control Mapper.";
			}
		}
		catch (Exception ex)
		{
			Status = "Intégration Control Mapper : " + ex.Message;
		}
	}

	private void Initialize(object sender, EventArgs args)
	{
		if (disposed)
		{
			return;
		}
		try
		{
			if (output.Settings == null)
			{
				Status = "Active Control Mapper dans SimHub pour affecter tes touches.";
				return;
			}
			RefreshRoles();
			manager.InputPressed += OnPressed;
			manager.InputReleased += OnReleased;
			subscribed = true;
			timer.Stop();
		}
		catch (Exception ex)
		{
			Status = "Intégration Control Mapper : " + ex.Message;
			timer.Stop();
		}
	}

	private bool OnPressed(string input)
	{
		if (disposed || input == null || !input.StartsWith("ControlMapperPlugin.", StringComparison.Ordinal))
		{
			return false;
		}
		string key = input.Substring("ControlMapperPlugin.".Length);
		if (labels.TryGetValue(key, out var value) && Pressed != null)
		{
			Pressed(value);
		}
		if (handlers.TryGetValue(key, out var value2))
		{
			value2();
		}
		return false;
	}

	private bool OnReleased(string input)
	{
		if (!disposed && input != null && input.StartsWith("ControlMapperPlugin.", StringComparison.Ordinal) && comboRoles.Contains(input.Substring("ControlMapperPlugin.".Length)) && comboReleased != null)
		{
			comboReleased();
		}
		return false;
	}

	public bool Test(FmtFunction f)
	{
		if (f.Kind == FunctionKind.LmuKey)
		{
			return output.Trigger(f.Role, 200);
		}
		if (!handlers.TryGetValue(f.Role, out var value))
		{
			return false;
		}
		if (Pressed != null)
		{
			Pressed(f.Family + " - " + f.Label);
		}
		if (f.Kind == FunctionKind.Combo)
		{
			return testCombo(f);
		}
		value();
		return true;
	}

	public void OpenMapper()
	{
		manager.ShowPluginUI<ControlMapperPlugin>();
	}

	public void Dispose()
	{
		disposed = true;
		timer.Stop();
		if (subscribed)
		{
			manager.InputPressed -= OnPressed;
			manager.InputReleased -= OnReleased;
		}
	}
}
