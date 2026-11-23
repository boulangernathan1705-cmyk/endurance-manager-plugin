using System.Threading;
using System.Threading.Tasks;

namespace FmtCompanion;

public interface IMountedTyreTransport
{
	Task<MountedTyreState> ReadMountedTyres(CancellationToken ct);
}
