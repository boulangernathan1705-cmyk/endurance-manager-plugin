using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FmtCompanion;

public static class LmuControlsModel
{
	public const string Primary = "Input";

	public const string Secondary = "Alternative Input";

	public static JObject DirectInput(JObject controls)
	{
		JObject jObject = ((controls == null || controls["allControls"] == null) ? null : (controls["allControls"]["directInput"] as JObject));
		if (jObject == null || !(jObject["Input"] is JObject) || !(jObject["Devices"] is JObject))
		{
			throw new InvalidOperationException("Écran des commandes LMU inconnu : rien n'est modifié.");
		}
		if (!(jObject["Alternative Input"] is JObject))
		{
			jObject["Alternative Input"] = new JObject();
		}
		return jObject;
	}

	public static LmuBinding Binding(JObject di, string column, string control)
	{
		return LmuBinding.From(di[column][control]);
	}

	public static string[] InputNames(JObject di, string device)
	{
		JArray jArray = ((!(di["Devices"][device] is JObject jObject)) ? null : (jObject["Input Names"] as JArray));
		if (jArray != null)
		{
			return jArray.Select((JToken n) => (string?)n).ToArray();
		}
		return new string[0];
	}

	public static int ButtonNumber(JObject di, LmuBinding b)
	{
		if (b == null)
		{
			return 0;
		}
		string[] array = InputNames(di, b.Device);
		if (b.Id < 0 || b.Id >= array.Length)
		{
			return 0;
		}
		Match match = Regex.Match(array[b.Id] ?? "", "^B(\\d+)$");
		if (!match.Success)
		{
			return 0;
		}
		return int.Parse(match.Groups[1].Value);
	}

	public static int LmuId(JObject di, string device, int button)
	{
		return Array.IndexOf(InputNames(di, device), "B" + button);
	}

	public static bool IsOutputDevice(string device, bool bridge)
	{
		if (device == null)
		{
			return false;
		}
		if (!bridge)
		{
			return device.StartsWith("vJoy Device", StringComparison.OrdinalIgnoreCase);
		}
		return device.IndexOf("Remapper Bridge", StringComparison.OrdinalIgnoreCase) >= 0;
	}

	public static IEnumerable<string> OutputDevices(JObject di, bool bridge)
	{
		return from p in ((JObject)di["Devices"]).Properties()
			select p.Name into n
			where IsOutputDevice(n, bridge)
			select n;
	}

	public static HashSet<int> UsedButtons(JObject di, bool bridge)
	{
		HashSet<int> hashSet = new HashSet<int>();
		string[] array = new string[2] { "Input", "Alternative Input" };
		foreach (string propertyName in array)
		{
			foreach (JProperty item in ((JObject)di[propertyName]).Properties())
			{
				LmuBinding lmuBinding = LmuBinding.From(item.Value);
				if (lmuBinding != null && IsOutputDevice(lmuBinding.Device, bridge))
				{
					int num = ButtonNumber(di, lmuBinding);
					if (num > 0)
					{
						hashSet.Add(num);
					}
				}
			}
		}
		return hashSet;
	}

	public static AssignPhase Phase(JObject status)
	{
		JToken jToken = status?["Status"];
		if (jToken != null && jToken.Type == JTokenType.Integer)
		{
			return (AssignPhase)(int)jToken;
		}
		return AssignPhase.None;
	}

	public static LmuBinding RecordedInput(JObject status)
	{
		JObject jObject = ((status == null) ? null : (status["Info"] as JObject));
		if (jObject != null)
		{
			return LmuBinding.From(jObject["input"]);
		}
		return null;
	}
}
