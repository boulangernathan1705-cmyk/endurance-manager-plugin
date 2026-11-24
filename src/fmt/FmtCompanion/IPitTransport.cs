using System;
using System.Threading;
using System.Threading.Tasks;

namespace FmtCompanion;

public interface IPitTransport : IDisposable
{
	Task<PitMenu> Read(CancellationToken ct);

	Task Write(PitMenu menu, CancellationToken ct);
}
