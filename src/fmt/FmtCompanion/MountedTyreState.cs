using System;
using System.Linq;
using Newtonsoft.Json.Linq;

namespace FmtCompanion;

public sealed class MountedTyreState
{
	public PitMenu Menu;

	public string[] Compounds;

	public static MountedTyreState Parse(string json)
	{
		JObject jObject = JObject.Parse(json);
		PitMenu pitMenu = new PitMenu(jObject["pitMenu"]["pitMenu"].ToString());
		JArray jArray = jObject["wheelInfo"]["wheelLocs"] as JArray;
		JArray jArray2 = jObject["tireInvGarageOptions"]["tireOptions"] as JArray;
		if (jArray == null || jArray2 == null || jArray.Count != 4 || jArray2.Count != 4)
		{
			throw new InvalidOperationException("Données des quatre pneus montés indisponibles.");
		}
		string[] array = new string[4];
		for (int i = 0; i < 4; i++)
		{
			JToken mounted = jArray[i]["compound"];
			if (mounted == null || mounted.Type != JTokenType.Integer)
			{
				throw new InvalidOperationException("Indice du composé monté indisponible.");
			}
			JArray jArray3 = (JArray)pitMenu.Find(Commands.Wheels[i] + " TIRE:")["settings"];
			JObject[] array2 = (from o in ((JArray)jArray2[i]).OfType<JObject>()
				where JToken.DeepEquals(o["compoundIndex"], mounted) && o["isUsed"] != null && o["isUsed"].Type == JTokenType.Boolean && !(bool)o["isUsed"]
				select o).ToArray();
			if (array2.Length != 1 || array2[0]["index"] == null || array2[0]["index"].Type != JTokenType.Integer)
			{
				throw new InvalidOperationException("Composé monté absent ou ambigu.");
			}
			int num = (int)array2[0]["index"];
			if (num < 0 || num >= jArray3.Count || !PitMenu.IsAvailableChoice(jArray3[num]) || jArray3[num]["isUsed"] == null || jArray3[num]["isUsed"].Type != JTokenType.Boolean || (bool)jArray3[num]["isUsed"])
			{
				throw new InvalidOperationException("Pneu neuf correspondant indisponible.");
			}
			array[i] = (string?)jArray3[num]["type"];
			if (string.IsNullOrWhiteSpace(array[i]))
			{
				throw new InvalidOperationException("Type du composé non reconnu.");
			}
		}
		MountedTyreState mountedTyreState = new MountedTyreState();
		mountedTyreState.Menu = pitMenu;
		mountedTyreState.Compounds = array;
		return mountedTyreState;
	}
}
