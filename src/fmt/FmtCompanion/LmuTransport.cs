using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace FmtCompanion;

public sealed class LmuTransport : IPitTransport, IDisposable, IMountedTyreTransport
{
	private readonly HttpClient client = new HttpClient(new HttpClientHandler
	{
		UseProxy = false,
		AllowAutoRedirect = false
	})
	{
		BaseAddress = new Uri("http://127.0.0.1:6397/"),
		Timeout = TimeSpan.FromSeconds(2.0)
	};

	public async Task<PitMenu> Read(CancellationToken ct)
	{
		using HttpResponseMessage response = await client.GetAsync("rest/garage/PitMenu/receivePitMenu", ct).ConfigureAwait(continueOnCapturedContext: false);
		response.EnsureSuccessStatusCode();
		return new PitMenu(await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	public async Task Write(PitMenu menu, CancellationToken ct)
	{
		using StringContent content = new StringContent(menu.ToJson(), Encoding.UTF8, "application/json");
		using HttpResponseMessage response = await client.PostAsync("rest/garage/PitMenu/loadPitMenu", content, ct).ConfigureAwait(continueOnCapturedContext: false);
		response.EnsureSuccessStatusCode();
	}

	public async Task<MountedTyreState> ReadMountedTyres(CancellationToken ct)
	{
		using HttpResponseMessage response = await client.GetAsync("rest/garage/UIScreen/TireManagement", ct).ConfigureAwait(continueOnCapturedContext: false);
		response.EnsureSuccessStatusCode();
		return MountedTyreState.Parse(await response.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false));
	}

	public void Dispose()
	{
		client.Dispose();
	}
}
