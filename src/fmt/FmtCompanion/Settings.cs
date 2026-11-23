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
}
