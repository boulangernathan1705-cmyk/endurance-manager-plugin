using System;
using System.Net.Http;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FmtCompanion;

public sealed class LmuOptionsClient : IDisposable
{
	private readonly HttpClient client = new HttpClient(new HttpClientHandler
	{
		UseProxy = false,
		AllowAutoRedirect = false
	})
	{
		BaseAddress = new Uri("http://127.0.0.1:6397/"),
		Timeout = TimeSpan.FromSeconds(3.0)
	};

	private async Task<JObject> Get(string path, CancellationToken ct)
	{
		using HttpResponseMessage r = await client.GetAsync(path, ct).ConfigureAwait(continueOnCapturedContext: false);
		r.EnsureSuccessStatusCode();
		string text = await r.Content.ReadAsStringAsync().ConfigureAwait(continueOnCapturedContext: false);
		return string.IsNullOrWhiteSpace(text) ? new JObject() : JObject.Parse(text);
	}

	private async Task Send(HttpMethod method, string path, string body, CancellationToken ct)
	{
		using HttpRequestMessage request = new HttpRequestMessage(method, path);
		if (body != null)
		{
			request.Content = new StringContent(body, Encoding.UTF8, "application/json");
		}
		using HttpResponseMessage r = await client.SendAsync(request, ct).ConfigureAwait(continueOnCapturedContext: false);
		r.EnsureSuccessStatusCode();
	}

	private static string Config(string control, int category, int slot)
	{
		JObject jObject = new JObject();
		jObject.Add("Category", category);
		jObject.Add("Control", control);
		jObject.Add("Slot", slot);
		return jObject.ToString(Formatting.None);
	}

	public Task<JObject> Controls(CancellationToken ct)
	{
		return Get("rest/options/UIScreen/Controls", ct);
	}

	public Task<JObject> AssignStatus(CancellationToken ct)
	{
		return Get("rest/options/assign/changestatus", ct);
	}

	public Task Listen(string control, CancellationToken ct)
	{
		return Send(HttpMethod.Post, "rest/options/setConfigControl", Config(control, 2, 1), ct);
	}

	public Task Unset(string control, CancellationToken ct)
	{
		return Send(HttpMethod.Post, "rest/options/unsetConfigControl", Config(control, 2, 1), ct);
	}

	public Task Cancel(CancellationToken ct)
	{
		return Send(HttpMethod.Post, "rest/options/assign/cancel", "null", ct);
	}

	public Task Put(string path, CancellationToken ct)
	{
		return Send(HttpMethod.Put, path, null, ct);
	}

	public void Dispose()
	{
		client.Dispose();
	}
}
