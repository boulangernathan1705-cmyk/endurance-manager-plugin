using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace FmtCompanion;

public static class Commands
{
	public static readonly string[] Wheels = new string[4] { "FL", "FR", "RL", "RR" };

	private static readonly string[] NoChangeLabels = new string[14]
	{
		"NO CHANGE", "DO NOT CHANGE", "NONE", "NO", "OFF", "DON'T REPAIR", "DO NOT REPAIR", "NO REPAIRS", "NE PAS CHANGER", "AUCUN",
		"AUCUNE", "AUCUN CHANGEMENT", "NON", "NE PAS RÉPARER"
	};

	public static IList<PitCommand> Create()
	{
		List<PitCommand> list = new List<PitCommand>();
		int[] array = new int[6] { -10, -5, -1, 1, 5, 10 };
		foreach (int num in array)
		{
			int delta = num;
			list.Add(new PitCommand("Refuel." + ((delta > 0) ? "Plus" : "Minus") + Math.Abs(delta), delegate(PitMenu m)
			{
				string name3 = (m.Rows.Any((JObject r) => (string?)r["name"] == "VIRTUAL ENERGY:") ? "VIRTUAL ENERGY:" : "FUEL:");
				AdjustNumber(m, name3, delta);
			}));
		}
		list.Add(new PitCommand("Refuel.Minimum", delegate(PitMenu m)
		{
			m.Set(m.Rows.Any((JObject r) => (string?)r["name"] == "VIRTUAL ENERGY:") ? "VIRTUAL ENERGY:" : "FUEL:", 0);
		}));
		list.Add(new PitCommand("Refuel.Maximum", delegate(PitMenu m)
		{
			string name2 = (m.Rows.Any((JObject r) => (string?)r["name"] == "VIRTUAL ENERGY:") ? "VIRTUAL ENERGY:" : "FUEL:");
			JArray choices = (JArray)m.Find(name2)["settings"];
			int[] array5 = (from i in Enumerable.Range(0, choices.Count)
				where PitMenu.IsAvailableChoice(choices[i])
				select i).ToArray();
			if (array5.Length == 0)
			{
				throw new InvalidOperationException("Ravitaillement indisponible.");
			}
			m.Set(name2, array5.Last());
		}));
		list.Add(new PitCommand("Ratio.Plus", delegate(PitMenu m)
		{
			AdjustNumber(m, "FUEL RATIO:", 0.01m);
		}));
		list.Add(new PitCommand("Ratio.Minus", delegate(PitMenu m)
		{
			AdjustNumber(m, "FUEL RATIO:", -0.01m);
		}));
		list.Add(new PitCommand("Driver.Next", delegate(PitMenu m)
		{
			m.Step("DRIVER:", 1);
		}));
		list.Add(new PitCommand("Driver.Previous", delegate(PitMenu m)
		{
			m.Step("DRIVER:", -1);
		}));
		list.Add(new PitCommand("StopGo.Serve", delegate(PitMenu m)
		{
			SelectStopGo(m, serve: true);
		}));
		list.Add(new PitCommand("StopGo.Defer", delegate(PitMenu m)
		{
			SelectStopGo(m, serve: false);
		}));
		string[] wheels = Wheels;
		foreach (string text in wheels)
		{
			string name = text + " TIRE:";
			string text2 = text;
			list.Add(new PitCommand("Tyres." + text2 + ".Next", delegate(PitMenu m)
			{
				m.Step(name, 1);
			}));
			list.Add(new PitCommand("Tyres." + text2 + ".Previous", delegate(PitMenu m)
			{
				m.Step(name, -1);
			}));
			list.Add(new PitCommand("Tyres." + text2 + ".None", delegate(PitMenu m)
			{
				SelectNone(m, name);
			}));
			int wheelIndex = Array.IndexOf(Wheels, text);
			list.Add(new PitCommand("Tyres." + text2 + ".Same", delegate(PitMenu m)
			{
				SetNewCompound(m, name, MountedType(m, wheelIndex));
			}, requiresMountedTyres: true));
		}
		list.Add(new PitCommand("Tyres.All.Same", delegate(PitMenu m)
		{
			for (int num6 = 0; num6 < Wheels.Length; num6++)
			{
				SetNewCompound(m, Wheels[num6] + " TIRE:", MountedType(m, num6));
			}
		}, requiresMountedTyres: true));
		list.Add(new PitCommand("Tyres.All.Next", delegate(PitMenu m)
		{
			StepAll(m, 1);
		}));
		list.Add(new PitCommand("Tyres.All.Previous", delegate(PitMenu m)
		{
			StepAll(m, -1);
		}));
		list.Add(new PitCommand("Tyres.All.None", delegate(PitMenu m)
		{
			string[] wheels5 = Wheels;
			foreach (string text9 in wheels5)
			{
				SelectNone(m, text9 + " TIRE:");
			}
			if (m.Rows.Any((JObject r) => (string?)r["name"] == "TIRES:"))
			{
				SelectNone(m, "TIRES:");
			}
		}));
		string[] wheels2 = Wheels;
		foreach (string text3 in wheels2)
		{
			string row3 = text3 + " TIRE:";
			list.Add(new PitCommand("Tyres." + text3 + ".Cycle", delegate(PitMenu m)
			{
				CycleTyre(m, row3);
			}));
		}
		list.Add(new PitCommand("Tyres.All.Cycle", delegate(PitMenu m)
		{
			string text8 = (m.Rows.Any((JObject r) => (string?)r["name"] == "TIRES:") ? "TIRES:" : "FL TIRE:");
			CycleTyre(m, text8);
			SyncWheels(m, text8);
		}));
		string[] array2 = new string[4] { "Soft", "Medium", "Hard", "Wet" };
		foreach (string text4 in array2)
		{
			string type = text4;
			list.Add(new PitCommand("Tyres.All." + type, delegate(PitMenu m)
			{
				SetNewCompound(m, "TIRES:", type);
			}));
			string[] wheels3 = Wheels;
			foreach (string text5 in wheels3)
			{
				string wheel = text5;
				list.Add(new PitCommand("Tyres." + wheel + "." + type, delegate(PitMenu m)
				{
					SetNewCompound(m, wheel + " TIRE:", type);
				}));
			}
		}
		list.Add(new PitCommand("Repairs.Next", delegate(PitMenu m)
		{
			m.Step("DAMAGE:", 1);
		}));
		list.Add(new PitCommand("Repairs.Previous", delegate(PitMenu m)
		{
			m.Step("DAMAGE:", -1);
		}));
		list.Add(new PitCommand("Repairs.None", delegate(PitMenu m)
		{
			SelectNone(m, "DAMAGE:");
		}));
		list.Add(new PitCommand("Repairs.Bodywork", delegate(PitMenu m)
		{
			SelectLabel(m, "DAMAGE:", new string[4] { "RÉPARER LA CARROSSERIE", "REPAIR BODYWORK", "BODYWORK", "BODYWORK ONLY" });
		}));
		list.Add(new PitCommand("Repairs.All", delegate(PitMenu m)
		{
			SelectLabel(m, "DAMAGE:", new string[4] { "TOUT RÉPARER", "REPAIR ALL", "ALL", "REPAIR ALL DAMAGE" });
		}));
		list.Add(new PitCommand("Brakes.Next", delegate(PitMenu m)
		{
			m.Step("REPLACE BRAKES:", 1);
		}));
		list.Add(new PitCommand("Brakes.Previous", delegate(PitMenu m)
		{
			m.Step("REPLACE BRAKES:", -1);
		}));
		list.Add(new PitCommand("Brakes.Replace", delegate(PitMenu m)
		{
			SelectLabel(m, "REPLACE BRAKES:", new string[3] { "OUI", "YES", "ON" });
		}));
		list.Add(new PitCommand("Brakes.Keep", delegate(PitMenu m)
		{
			SelectNone(m, "REPLACE BRAKES:");
		}));
		list.Add(new PitCommand("Refuel.Next", delegate(PitMenu m)
		{
			m.Step(EnergyRow(m), 1);
		}));
		list.Add(new PitCommand("Refuel.Previous", delegate(PitMenu m)
		{
			m.Step(EnergyRow(m), -1);
		}));
		list.Add(new PitCommand("StopGo.Next", delegate(PitMenu m)
		{
			m.Step("STOP/GO:", 1);
		}));
		list.Add(new PitCommand("StopGo.Previous", delegate(PitMenu m)
		{
			m.Step("STOP/GO:", -1);
		}));
		list.Add(new PitCommand("Repairs.Cycle", delegate(PitMenu m)
		{
			CycleRow(m, "DAMAGE:");
		}));
		list.Add(new PitCommand("Brakes.Toggle", delegate(PitMenu m)
		{
			CycleRow(m, "REPLACE BRAKES:");
		}));
		list.Add(new PitCommand("StopGo.Toggle", delegate(PitMenu m)
		{
			CycleRow(m, "STOP/GO:");
		}));
		list.Add(new PitCommand("Driver.Cycle", delegate(PitMenu m)
		{
			CycleRow(m, "DRIVER:");
		}));
		list.Add(new PitCommand("Grille.Cycle", delegate(PitMenu m)
		{
			CycleRow(m, "GRILLE:");
		}));
		list.Add(new PitCommand("BrakeDuct.Front.Cycle", delegate(PitMenu m)
		{
			CycleRow(m, "F BRAKE DUCT:");
		}));
		list.Add(new PitCommand("BrakeDuct.Rear.Cycle", delegate(PitMenu m)
		{
			CycleRow(m, "R BRAKE DUCT:");
		}));
		string[][] array3 = new string[4][]
		{
			new string[2] { "Wing", "R WING:" },
			new string[2] { "Grille", "GRILLE:" },
			new string[2] { "BrakeDuct.Front", "F BRAKE DUCT:" },
			new string[2] { "BrakeDuct.Rear", "R BRAKE DUCT:" }
		};
		foreach (string[] array4 in array3)
		{
			string text6 = array4[0];
			string row2 = array4[1];
			list.Add(new PitCommand(text6 + ".Next", delegate(PitMenu m)
			{
				m.Step(row2, 1);
			}));
			list.Add(new PitCommand(text6 + ".Previous", delegate(PitMenu m)
			{
				m.Step(row2, -1);
			}));
		}
		string[] wheels4 = Wheels;
		foreach (string text7 in wheels4)
		{
			string row = text7 + " PRESS:";
			list.Add(new PitCommand("Pressure." + text7 + ".Next", delegate(PitMenu m)
			{
				m.Step(row, 1);
			}));
			list.Add(new PitCommand("Pressure." + text7 + ".Previous", delegate(PitMenu m)
			{
				m.Step(row, -1);
			}));
		}
		list.Add(new PitCommand("Pressure.All.Next", delegate(PitMenu m)
		{
			StepPressures(m, 1);
		}));
		list.Add(new PitCommand("Pressure.All.Previous", delegate(PitMenu m)
		{
			StepPressures(m, -1);
		}));
		return list;
	}

	public static string EnergyRow(PitMenu m)
	{
		if (!m.Rows.Any((JObject r) => (string?)r["name"] == "VIRTUAL ENERGY:"))
		{
			return "FUEL:";
		}
		return "VIRTUAL ENERGY:";
	}

	private static void StepPressures(PitMenu menu, int delta)
	{
		string[] array = Wheels.Select((string w) => w + " PRESS:").ToArray();
		string[] array2 = array;
		foreach (string name in array2)
		{
			if (!menu.CanStep(name, delta))
			{
				throw new InvalidOperationException("Une pression est en butée : aucune roue modifiée.");
			}
		}
		string[] array3 = array;
		foreach (string name2 in array3)
		{
			menu.Step(name2, delta);
		}
	}

	private static decimal Number(JToken choice)
	{
		string text = PitMenu.Text(choice).Trim();
		if (Regex.IsMatch(text, "\\bgal", RegexOptions.IgnoreCase))
		{
			throw new InvalidOperationException("Passer LMU en litres pour les commandes de carburant.");
		}
		Match match = Regex.Match(text, "^[+-]?\\d+(?:[.,]\\d+)?");
		if (!match.Success || !decimal.TryParse(match.Value.Replace(',', '.'), NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out var result))
		{
			throw new InvalidOperationException("Valeur numérique LMU non reconnue : " + text);
		}
		return result;
	}

	private static string MountedType(PitMenu menu, int index)
	{
		if (menu.MountedCompounds == null || menu.MountedCompounds.Length != 4 || string.IsNullOrWhiteSpace(menu.MountedCompounds[index]))
		{
			throw new InvalidOperationException("Composé monté non identifié : choisir Soft, Medium, Hard ou Pluie.");
		}
		return menu.MountedCompounds[index];
	}

	private static void AdjustNumber(PitMenu menu, string name, decimal delta)
	{
		JObject jObject = menu.Find(name);
		JArray choices = (JArray)jObject["settings"];
		int[] array = (from i in Enumerable.Range(0, choices.Count)
			where PitMenu.IsAvailableChoice(choices[i])
			select i).ToArray();
		if (array.Length == 0)
		{
			throw new InvalidOperationException("Réglage indisponible : " + name);
		}
		decimal target = Number(choices[(int)jObject["currentSetting"]]) + delta;
		decimal val = array.Min((int i) => Number(choices[i]));
		decimal val2 = array.Max((int i) => Number(choices[i]));
		target = Math.Max(val, Math.Min(val2, target));
		int[] array2 = array.Where((int i) => Number(choices[i]) == target).ToArray();
		if (array2.Length != 1)
		{
			throw new InvalidOperationException("Ce pas n’est pas proposé par LMU : " + name);
		}
		menu.Set(name, array2[0]);
	}

	private static void SelectStopGo(PitMenu menu, bool serve)
	{
		JArray choices = (JArray)menu.Find("STOP/GO:")["settings"];
		string[] accepted = (serve ? new string[2] { "OUI", "YES" } : new string[2] { "NON", "NO" });
		int[] array = (from i in Enumerable.Range(0, choices.Count)
			where accepted.Contains(PitMenu.ChoiceTextKey("STOP/GO:", PitMenu.Text(choices[i])))
			select i).ToArray();
		if (array.Length != 1)
		{
			throw new InvalidOperationException("Choix Stop & Go absent ou ambigu.");
		}
		menu.Set("STOP/GO:", array[0]);
	}

	private static void CycleRow(PitMenu menu, string name)
	{
		JObject jObject = menu.Find(name);
		JArray choices = (JArray)jObject["settings"];
		List<int> list = (from i in Enumerable.Range(0, choices.Count)
			where PitMenu.IsAvailableChoice(choices[i])
			select i).ToList();
		if (list.Count < 2)
		{
			throw new InvalidOperationException("Aucun autre choix proposé : " + name);
		}
		int num = list.IndexOf((int)jObject["currentSetting"]);
		menu.Set(name, list[(num + 1) % list.Count]);
	}

	private static void CycleTyre(PitMenu menu, string name)
	{
		JObject jObject = menu.Find(name);
		JArray choices = (JArray)jObject["settings"];
		List<int> list = (from i in Enumerable.Range(0, choices.Count)
			where PitMenu.IsAvailableChoice(choices[i]) && (NoChangeLabels.Contains(PitMenu.Text(choices[i]).Trim().ToUpperInvariant()) || (choices[i] is JObject && choices[i]["isUsed"] != null && choices[i]["isUsed"].Type == JTokenType.Boolean && !(bool)choices[i]["isUsed"]))
			select i).ToList();
		if (list.Count < 2)
		{
			throw new InvalidOperationException("Aucune autre gomme proposée : " + name);
		}
		int num = list.IndexOf((int)jObject["currentSetting"]);
		menu.Set(name, list[(num + 1) % list.Count]);
	}

	private static void SelectNone(PitMenu menu, string name)
	{
		string[] accepted = new string[14]
		{
			"NO CHANGE", "DO NOT CHANGE", "NONE", "NO", "OFF", "DON'T REPAIR", "DO NOT REPAIR", "NO REPAIRS", "NE PAS CHANGER", "AUCUN",
			"AUCUNE", "AUCUN CHANGEMENT", "NON", "NE PAS RÉPARER"
		};
		SelectLabel(menu, name, accepted);
	}

	private static void SelectLabel(PitMenu menu, string name, string[] accepted)
	{
		JArray settings = (JArray)menu.Find(name)["settings"];
		int[] array = (from i in Enumerable.Range(0, settings.Count)
			where accepted.Contains(PitMenu.Text(settings[i]).Trim().ToUpperInvariant())
			select i).ToArray();
		if (array.Length != 1)
		{
			throw new InvalidOperationException("Choix indisponible ou ambigu sur cette voiture : commande annulée.");
		}
		menu.Set(name, array[0]);
	}

	private static void SetNewCompound(PitMenu menu, string name, string type)
	{
		JArray source = (JArray)menu.Find(name)["settings"];
		JObject[] array = (from c in source.OfType<JObject>()
			where (string?)c["type"] == type && c["isUsed"] != null && c["isUsed"].Type == JTokenType.Boolean && !(bool)c["isUsed"] && PitMenu.IsAvailableChoice(c)
			select c).ToArray();
		if (array.Length != 1)
		{
			throw new InvalidOperationException("Pneu neuf indisponible ou ambigu : " + type);
		}
		SetExact(menu, name, PitMenu.Text(array[0]), type);
	}

	public static void SetExact(PitMenu menu, string name, string text, string type)
	{
		JArray choices = (JArray)menu.Find(name)["settings"];
		int[] array = (from i in Enumerable.Range(0, choices.Count)
			where PitMenu.ChoiceTextKey(name, PitMenu.Text(choices[i])) == PitMenu.ChoiceTextKey(name, text) && (type == null || (choices[i] is JObject && (string?)choices[i]["type"] == type))
			select i).ToArray();
		if (array.Length != 1)
		{
			throw new InvalidOperationException("Choix absent ou ambigu sur cette voiture : " + text);
		}
		menu.Set(name, array[0]);
		if (name == "TIRES:")
		{
			SyncWheels(menu, name);
		}
	}

	private static void StepAll(PitMenu menu, int delta)
	{
		string text = (menu.Rows.Any((JObject r) => (string?)r["name"] == "TIRES:") ? "TIRES:" : "FL TIRE:");
		menu.Step(text, delta);
		SyncWheels(menu, text);
	}

	private static void SyncWheels(PitMenu menu, string master)
	{
		JObject jObject = menu.Find(master);
		JToken selected = ((JArray)jObject["settings"])[(int)jObject["currentSetting"]];
		string type = ((selected is JObject) ? ((string?)selected["type"]) : null);
		string[] wheels = Wheels;
		foreach (string text in wheels)
		{
			string text2 = text + " TIRE:";
			JArray choices = (JArray)menu.Find(text2)["settings"];
			int[] source = Enumerable.Range(0, choices.Count).Where(delegate(int i)
			{
				if (string.IsNullOrWhiteSpace(type))
				{
					return PitMenu.Text(choices[i]) == PitMenu.Text(selected);
				}
				return choices[i] is JObject && (string?)choices[i]["type"] == type;
			}).ToArray();
			source = source.Where((int i) => PitMenu.IsAvailableChoice(choices[i]) && PitMenu.Text(choices[i]) == PitMenu.Text(selected) && (!(selected is JObject) || JToken.DeepEquals(choices[i]["isUsed"], selected["isUsed"]))).ToArray();
			if (source.Length != 1)
			{
				throw new InvalidOperationException("Choix ambigu pour " + text2 + " : sélectionner chaque roue dans le panneau.");
			}
			menu.Set(text2, source[0]);
		}
	}
}
