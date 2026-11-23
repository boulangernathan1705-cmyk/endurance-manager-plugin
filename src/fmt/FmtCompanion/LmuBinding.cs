using Newtonsoft.Json.Linq;

namespace FmtCompanion;

public sealed class LmuBinding
{
	public string Device;

	public int Id;

	public static LmuBinding From(JToken token)
	{
		if (!(token is JObject jObject) || jObject["device"] == null || jObject["id"] == null || jObject["id"].Type != JTokenType.Integer)
		{
			return null;
		}
		LmuBinding lmuBinding = new LmuBinding();
		lmuBinding.Device = (string?)jObject["device"];
		lmuBinding.Id = (int)jObject["id"];
		return lmuBinding;
	}

	public bool Same(LmuBinding other)
	{
		if (other != null && other.Device == Device)
		{
			return other.Id == Id;
		}
		return false;
	}
}
