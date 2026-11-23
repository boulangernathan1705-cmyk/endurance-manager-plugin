using System;

namespace FmtCompanion;

public sealed class ServiceState
{
	public readonly PitMenu Menu;

	public readonly string Status;

	public readonly DateTime UpdatedUtc;

	public readonly bool Connected;

	public ServiceState(PitMenu menu, string status, bool connected)
	{
		Menu = menu;
		Status = status;
		Connected = connected;
		UpdatedUtc = DateTime.UtcNow;
	}
}
