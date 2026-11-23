using System.Collections.Generic;
using System.Linq;

namespace FmtCompanion;

public sealed class Settings
{
	public List<Shortcut> Shortcuts = (from i in Enumerable.Range(0, 16)
		select new Shortcut()).ToList();

	public List<string> VisibleCommands;

	public List<PreparedKey> Prepared = new List<PreparedKey>();

	public string ValidatedLmuVersion;

	public int CatalogVersion;

	// A function whose LMU command already has a button of the pilot's own (a role of his bound in LMU on the
	// Control Mapper output): FMT presses that role instead of taking a new button (FMT role → pilot's role).
	public Dictionary<string, string> Reused = new Dictionary<string, string>();
}
