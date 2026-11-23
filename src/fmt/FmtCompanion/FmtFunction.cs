namespace FmtCompanion;

public sealed class FmtFunction
{
	public string Id;

	public string Family;

	public string Group;

	public string Label;

	public FunctionKind Kind;

	public string LmuControl;

	public string PitCommand;

	public string ApiPath;

	public string Modifiers;

	public string SeatId;

	public bool DefaultVisible;

	public string Role => "FMT - " + Family + " - " + Label;
}
