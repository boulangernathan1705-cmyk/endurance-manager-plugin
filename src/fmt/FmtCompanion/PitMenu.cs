using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FmtCompanion;

public sealed class PitMenu
{
	public readonly JArray Payload;

	public string[] MountedCompounds;

	public static readonly string[] KnownRows = new string[20]
	{
		"DAMAGE:", "DRIVER:", "VIRTUAL ENERGY:", "FUEL:", "FUEL RATIO:", "TIRES:", "FL TIRE:", "FR TIRE:", "RL TIRE:", "RR TIRE:",
		"R WING:", "GRILLE:", "FL PRESS:", "FR PRESS:", "RL PRESS:", "RR PRESS:", "F BRAKE DUCT:", "R BRAKE DUCT:", "REPLACE BRAKES:", "STOP/GO:"
	};

	public IEnumerable<JObject> Rows => Payload.OfType<JObject>();

	public PitMenu(string json)
	{
		Payload = JArray.Parse(json);
		if (Payload.Count == 0)
		{
			throw new InvalidOperationException("Menu LMU vide.");
		}
		HashSet<string> hashSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
		foreach (JToken item in Payload)
		{
			if (!(item is JObject jObject) || jObject["name"] == null || !hashSet.Add((string?)jObject["name"]))
			{
				throw new InvalidOperationException("Structure du menu LMU inconnue ou noms dupliqués.");
			}
			if (!(jObject["settings"] is JArray jArray) || jObject["currentSetting"] == null || jObject["currentSetting"].Type != JTokenType.Integer)
			{
				throw new InvalidOperationException("Réglage LMU incomplet : " + jObject["name"]);
			}
			int num = (int)jObject["currentSetting"];
			if (jArray.Count > 0 && (num < 0 || num >= jArray.Count))
			{
				throw new InvalidOperationException("Indice LMU invalide : " + jObject["name"]);
			}
		}
	}

	public string[] UnknownRows()
	{
		return (from r in Rows
			select (string?)r["name"] into n
			where !KnownRows.Contains(n, StringComparer.OrdinalIgnoreCase)
			select n).ToArray();
	}

	public PitMenu Copy()
	{
		PitMenu pitMenu = new PitMenu(ToJson());
		pitMenu.MountedCompounds = ((MountedCompounds == null) ? null : ((string[])MountedCompounds.Clone()));
		return pitMenu;
	}

	public string ToJson()
	{
		return Payload.ToString(Formatting.None);
	}

	public JObject Find(string name)
	{
		JObject jObject = Rows.SingleOrDefault((JObject r) => string.Equals((string?)r["name"], name, StringComparison.OrdinalIgnoreCase));
		if (jObject == null)
		{
			throw new InvalidOperationException("Option absente de LMU : " + name);
		}
		return jObject;
	}

	public static string Text(JToken choice)
	{
		string text = ((choice is JObject) ? (((string?)(choice["text"] ?? choice["type"])) ?? choice.ToString(Formatting.None)) : choice.ToString());
		if (text.IndexOf('Ã') >= 0 || text.IndexOf('Â') >= 0)
		{
			try
			{
				Encoding encoding = Encoding.GetEncoding(28591, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
				return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(encoding.GetBytes(text));
			}
			catch (ArgumentException)
			{
			}
		}
		return text;
	}

	public string Value(string name)
	{
		JObject jObject = Find(name);
		JArray jArray = (JArray)jObject["settings"];
		if (jArray.Count != 0)
		{
			return Text(jArray[(int)jObject["currentSetting"]]);
		}
		return "—";
	}

	public static bool IsAvailableChoice(JToken choice)
	{
		string text = Text(choice).Trim().ToUpperInvariant();
		if (text.Length > 0 && text != "INVALID" && text != "N/A" && text != "PNEUS MIXTES" && text != "MIXED TYRES")
		{
			return text != "MIXED TIRES";
		}
		return false;
	}

	public void Set(string name, int index)
	{
		JObject jObject = Find(name);
		JArray jArray = (JArray)jObject["settings"];
		if (index < 0 || index >= jArray.Count)
		{
			throw new InvalidOperationException("Choix indisponible : " + name);
		}
		if (!IsAvailableChoice(jArray[index]))
		{
			throw new InvalidOperationException("Choix LMU indisponible : " + Text(jArray[index]));
		}
		jObject["currentSetting"] = index;
	}

	public bool CanStep(string name, int delta)
	{
		JObject jObject = Find(name);
		JArray jArray = (JArray)jObject["settings"];
		for (int i = (int)jObject["currentSetting"] + Math.Sign(delta); i >= 0 && i < jArray.Count; i += Math.Sign(delta))
		{
			if (IsAvailableChoice(jArray[i]))
			{
				return true;
			}
		}
		return false;
	}

	public void Step(string name, int delta)
	{
		JObject jObject = Find(name);
		JArray jArray = (JArray)jObject["settings"];
		int num = (int)jObject["currentSetting"];
		for (int i = 0; i < Math.Abs(delta); i++)
		{
			int j;
			for (j = num + Math.Sign(delta); j >= 0 && j < jArray.Count && !IsAvailableChoice(jArray[j]); j += Math.Sign(delta))
			{
			}
			if (j < 0 || j >= jArray.Count)
			{
				break;
			}
			num = j;
		}
		Set(name, num);
	}

	public static string ChoiceTextKey(string name, string text)
	{
		if (name == "STOP/GO:")
		{
			Match match = Regex.Match(text.Trim(), "^(OUI|NON|YES|NO)\\s*(?:\\([^)]*\\))?$", RegexOptions.IgnoreCase);
			if (match.Success)
			{
				return match.Groups[1].Value.ToUpperInvariant();
			}
		}
		switch (name)
		{
		case "VIRTUAL ENERGY:":
		case "FUEL:":
		case "FUEL RATIO:":
		{
			Match match2 = Regex.Match(text, "^\\s*[+-]?\\d+(?:[.,]\\d+)?\\s*(?:%|[lL]\\b|gal\\b)?");
			if (match2.Success)
			{
				return match2.Value.Trim();
			}
			break;
		}
		}
		return text.Trim();
	}

	private static bool EquivalentChoices(JObject left, JObject right)
	{
		JArray jArray = (JArray)left["settings"];
		JArray jArray2 = (JArray)right["settings"];
		if (jArray.Count != jArray2.Count)
		{
			return false;
		}
		string name = (string?)left["name"];
		for (int i = 0; i < jArray.Count; i++)
		{
			JToken jToken = jArray[i].DeepClone();
			JToken jToken2 = jArray2[i].DeepClone();
			if (jToken is JObject && jToken["text"] != null)
			{
				jToken["text"] = ChoiceTextKey(name, Text(jToken));
			}
			if (jToken2 is JObject && jToken2["text"] != null)
			{
				jToken2["text"] = ChoiceTextKey(name, Text(jToken2));
			}
			if (!JToken.DeepEquals(jToken, jToken2))
			{
				return false;
			}
		}
		return true;
	}

	public bool SameSettings(PitMenu other)
	{
		if (Rows.Count() != other.Rows.Count())
		{
			return false;
		}
		foreach (JObject row in Rows)
		{
			IEnumerable<JObject> rows = other.Rows;
			Func<JObject, bool> predicate = (JObject r) => (string?)r["name"] == (string?)row["name"];
			JObject jObject = rows.FirstOrDefault(predicate);
			if (jObject == null || !EquivalentChoices(row, jObject) || !JToken.DeepEquals(row["currentSetting"], jObject["currentSetting"]))
			{
				return false;
			}
		}
		return true;
	}

	public IDictionary<string, int> ChangesFrom(PitMenu before)
	{
		return Rows.Where((JObject r) => (int)r["currentSetting"] != (int)before.Find((string?)r["name"])["currentSetting"]).ToDictionary((JObject r) => (string?)r["name"], (JObject r) => (int)r["currentSetting"]);
	}

	public void Verify(PitMenu actual, IDictionary<string, int> expected)
	{
		foreach (JObject row in Rows)
		{
			string text = (string?)row["name"];
			if (!(text == "TIRES:") || expected.ContainsKey(text))
			{
				JObject jObject = actual.Find(text);
				if ((int)row["currentSetting"] != (int)jObject["currentSetting"])
				{
					throw new InvalidOperationException("Réglage différent après écriture : " + text + " — vérifier le menu du jeu.");
				}
			}
		}
		foreach (KeyValuePair<string, int> item in expected)
		{
			JObject left = Find(item.Key);
			JObject jObject2 = actual.Find(item.Key);
			if ((int)jObject2["currentSetting"] != item.Value || !EquivalentChoices(left, jObject2))
			{
				throw new InvalidOperationException("Résultat non confirmé pour " + item.Key + " — vérifier le menu du jeu.");
			}
		}
	}
}
