using System;

namespace FmtCompanion;

public sealed class PitCommand
{
	public readonly string Name;

	public readonly Action<PitMenu> Apply;

	public readonly bool RequiresMountedTyres;

	public PitCommand(string name, Action<PitMenu> apply, bool requiresMountedTyres = false)
	{
		Name = name;
		Apply = apply;
		RequiresMountedTyres = requiresMountedTyres;
	}
}
