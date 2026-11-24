using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SimHub.Plugins;
using SimHub.Plugins.OutputPlugins.ControlRemapper;
using SimHub.Plugins.OutputPlugins.ControlRemapper.Models;

namespace FmtCompanion;

internal sealed class MapperOutput
{
	private readonly PluginManager manager;

	public ControlMapperPlugin Plugin
	{
		get
		{
			if (manager != null)
			{
				return manager.GetPlugin<ControlMapperPlugin>();
			}
			return null;
		}
	}

	public ControlMapperPluginSettings Settings => Plugin?.controlMapperPluginSettings;

	public OutputMode Mode => Settings?.OutputMode ?? OutputMode.Disabled;

	public bool Usable
	{
		get
		{
			if (Mode != OutputMode.VJoy)
			{
				return Mode == OutputMode.Bridge;
			}
			return true;
		}
	}

	public bool Bridge => Mode == OutputMode.Bridge;

	public int ButtonLimit
	{
		get
		{
			ControlMapperPluginSettings settings = Settings;
			if (settings == null)
			{
				return 0;
			}
			if (settings.OutputMode == OutputMode.Bridge)
			{
				return 128;
			}
			if (settings.VJoyMapping.ConfiguredButtons <= 0)
			{
				return 128;
			}
			return settings.VJoyMapping.ConfiguredButtons;
		}
	}

	public MapperOutput(PluginManager manager)
	{
		this.manager = manager;
	}

	public string Describe()
	{
		ControlMapperPluginSettings settings = Settings;
		if (settings == null)
		{
			return "Control Mapper absent";
		}
		return settings.OutputMode switch
		{
			OutputMode.VJoy => "vJoy n°" + settings.VJoyMapping.TargetVJoyId, 
			OutputMode.Bridge => "Bridge SimHub", 
			_ => "aucune sortie manette", 
		};
	}

	public ButtonMap Output(int button)
	{
		return Settings?.OutputMapping.ControllerMapping.Buttons.FirstOrDefault((ButtonMap b) => b.ButtonId == button - 1);
	}

	public string OutputRole(int button)
	{
		return Output(button)?.TargetRole;
	}

	public int ButtonFor(string role)
	{
		ControlMapperPluginSettings settings = Settings;
		if (settings == null)
		{
			return 0;
		}
		ButtonMap buttonMap = settings.OutputMapping.ControllerMapping.Buttons.FirstOrDefault((ButtonMap x) => x.TargetRole == role);
		if (buttonMap != null)
		{
			return buttonMap.ButtonId + 1;
		}
		return 0;
	}

	public HashSet<string> OutputRoles()
	{
		ControlMapperPluginSettings settings = Settings;
		HashSet<string> hashSet = new HashSet<string>(StringComparer.Ordinal);
		if (settings == null)
		{
			return hashSet;
		}
		foreach (ButtonMap button in settings.OutputMapping.ControllerMapping.Buttons)
		{
			if (!string.IsNullOrEmpty(button.TargetRole))
			{
				hashSet.Add(button.TargetRole);
			}
		}
		return hashSet;
	}

	public string AssignedInputs(string role)
	{
		ControlMapperPluginSettings settings = Settings;
		if (settings == null)
		{
			return "";
		}
		List<string> list = new List<string>();
		foreach (ControllerSourceMapping controllerMapping2 in settings.ControllerMappings)
		{
			ControllerMapping controllerMapping = controllerMapping2.ControllerMapping;
			if (controllerMapping == null)
			{
				continue;
			}
			string text = ((controllerMapping2.ControllerDescription == null) ? "?" : controllerMapping2.ControllerDescription.DisplayName);
			foreach (ButtonMap button in controllerMapping.Buttons)
			{
				if (button.TargetRole == role)
				{
					list.Add(text + " bouton " + (button.ButtonId + 1));
				}
			}
			foreach (POVDirectionMap pOVDirection in controllerMapping.POVDirections)
			{
				if (pOVDirection.TargetRole == role)
				{
					list.Add(text + " croix");
				}
			}
			foreach (AxisToButtonInputMap axisToButton in controllerMapping.AxisToButtons)
			{
				if (axisToButton.TargetRole == role)
				{
					list.Add(text + " axe");
				}
			}
		}
		return string.Join(", ", list);
	}

	public List<KeyValuePair<string, int>> AssignedButtons(string role)
	{
		List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>();
		ControlMapperPluginSettings settings = Settings;
		if (settings == null)
		{
			return list;
		}
		foreach (ControllerSourceMapping controllerMapping2 in settings.ControllerMappings)
		{
			ControllerMapping controllerMapping = controllerMapping2.ControllerMapping;
			if (controllerMapping == null)
			{
				continue;
			}
			string key = ((controllerMapping2.ControllerDescription == null) ? "?" : controllerMapping2.ControllerDescription.DisplayName);
			foreach (ButtonMap button in controllerMapping.Buttons)
			{
				if (button.TargetRole == role)
				{
					list.Add(new KeyValuePair<string, int>(key, button.ButtonId));
				}
			}
			foreach (POVDirectionMap pOVDirection in controllerMapping.POVDirections)
			{
				if (pOVDirection.TargetRole == role)
				{
					list.Add(new KeyValuePair<string, int>(key, -1));
				}
			}
			foreach (AxisToButtonInputMap axisToButton in controllerMapping.AxisToButtons)
			{
				if (axisToButton.TargetRole == role)
				{
					list.Add(new KeyValuePair<string, int>(key, -1));
				}
			}
		}
		return list;
	}

	public bool Trigger(string role, int milliseconds)
	{
		return Plugin?.TriggerRole(role, milliseconds) ?? false;
	}

	public void Save()
	{
		Plugin?.SaveSettings();
	}

	public void Backup()
	{
		string commonStoragePath = manager.GetCommonStoragePath("ControlMapperPlugin.GeneralSettingsV2.json");
		if (File.Exists(commonStoragePath))
		{
			File.Copy(commonStoragePath, commonStoragePath + ".FMT-" + DateTime.UtcNow.ToString("yyyyMMdd-HHmmss-fff") + ".bak", overwrite: false);
		}
	}
}
