namespace FmtCompanion;

public sealed class Shortcut
{
	public string Row;

	public string Text;

	public string Type;

	public string Label
	{
		get
		{
			if (Row != null)
			{
				return Row + " " + Text;
			}
			return "Non affecté";
		}
	}
}
