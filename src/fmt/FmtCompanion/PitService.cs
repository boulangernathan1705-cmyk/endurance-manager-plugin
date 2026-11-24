using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace FmtCompanion;

public sealed class PitService : IDisposable
{
	private sealed class Pending
	{
		public PitCommand Command;

		public DateTime Created = DateTime.UtcNow;

		public int Generation;
	}

	private readonly IPitTransport transport;

	private readonly BlockingCollection<Pending> queue = new BlockingCollection<Pending>(64);

	private readonly CancellationTokenSource cancel = new CancellationTokenSource();

	private readonly SemaphoreSlim signal = new SemaphoreSlim(0);

	private Task worker;

	private volatile ServiceState state = new ServiceState(null, "En attente de LMU", connected: false);

	private int enabled;

	private int generation;

	private int disposed;

	public volatile string ReadOnlyReason;

	public Func<bool> GameRunning = () => true;

	public int IdleMilliseconds = 1500;

	// Endurance Manager (23/11/2026): the menu is read between two commands only while something shows it (Wanted: its
	// properties read, its screen open), and not when Endurance Manager read it a moment ago (Offer). The commands still
	// read it themselves before and after writing, as before.
	public Func<bool> Wanted = () => true;

	// Each menu read before a command (FMTCompanionPlugin.CheckMenu: unknown lines make the menu read only).
	public Action<PitMenu> Seen;

	private long offeredTicks;

	public void Offer(string json)
	{
		try
		{
			PitMenu menu = new PitMenu(json);
			ServiceState current = state;
			state = new ServiceState(menu, (current.Status == "En attente de LMU") ? "Lecture du menu active" : current.Status, connected: true);
			Interlocked.Exchange(ref offeredTicks, DateTime.UtcNow.Ticks);
		}
		catch (Exception)
		{
		}
	}

	private bool OfferedLately => DateTime.UtcNow.Ticks - Interlocked.Read(ref offeredTicks) < TimeSpan.FromMilliseconds(IdleMilliseconds).Ticks;

	public int AbsentMilliseconds = 5000;

	public volatile string LastCommand = "";

	public int LastOk = -1;

	public int LastCount;

	public ServiceState State => state;

	public bool WritesEnabled => Volatile.Read(ref enabled) == 1;

	public void EnableWrites(bool value)
	{
		Interlocked.Increment(ref generation);
		Interlocked.Exchange(ref enabled, value ? 1 : 0);
	}

	public PitService(IPitTransport transport)
	{
		this.transport = transport;
	}

	public void Start()
	{
		if (worker == null)
		{
			worker = Task.Run((Func<Task>)Run);
		}
	}

	public void Enqueue(PitCommand command)
	{
		if (Volatile.Read(ref disposed) == 0)
		{
			if (!WritesEnabled)
			{
				state = new ServiceState(state.Menu, "Commandes désactivées : activer dans FMT companion", state.Connected);
			}
			else if (!queue.TryAdd(new Pending
			{
				Command = command,
				Generation = Volatile.Read(ref generation)
			}))
			{
				state = new ServiceState(state.Menu, "Trop de commandes : entrée ignorée", state.Connected);
			}
			else
			{
				signal.Release();
			}
		}
	}

	private async Task Run()
	{
		while (!cancel.IsCancellationRequested)
		{
			bool pauseAfterFailure = false;
			try
			{
				if (queue.TryTake(out var pending))
				{
					if (WritesEnabled && pending.Generation == Volatile.Read(ref generation) && DateTime.UtcNow - pending.Created < TimeSpan.FromSeconds(2.0))
					{
						LastCommand = pending.Command.Name;
						try
						{
							await Execute(pending.Command, cancel.Token, pending.Generation).ConfigureAwait(continueOnCapturedContext: false);
							Record(1);
						}
						catch (Exception)
						{
							Record(0);
							throw;
						}
					}
					else if (WritesEnabled)
					{
						state = new ServiceState(state.Menu, "Commande expirée ou invalidée : entrée ignorée", state.Connected);
					}
				}
				else
				{
					if (!GameRunning())
					{
						if (state.Connected || state.Menu != null)
						{
							state = new ServiceState(null, "En attente de LMU", connected: false);
						}
						await signal.WaitAsync(AbsentMilliseconds, cancel.Token).ConfigureAwait(continueOnCapturedContext: false);
						continue;
					}
					if (!Wanted() || OfferedLately)
					{
						await signal.WaitAsync(IdleMilliseconds, cancel.Token).ConfigureAwait(continueOnCapturedContext: false);
						continue;
					}
					state = new ServiceState(await transport.Read(cancel.Token).ConfigureAwait(continueOnCapturedContext: false), (state.Status == "En attente de LMU") ? "Lecture du menu active" : state.Status, connected: true);
					await signal.WaitAsync(IdleMilliseconds, cancel.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
			}
			catch (OperationCanceledException)
			{
				if (cancel.IsCancellationRequested)
				{
					break;
				}
				Fail("Délai dépassé — résultat incertain, vérifier le menu LMU");
			}
			catch (Exception ex3)
			{
				Fail(ex3.Message);
				pauseAfterFailure = true;
			}
			if (pauseAfterFailure)
			{
				try
				{
					await signal.WaitAsync(AbsentMilliseconds, cancel.Token).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (OperationCanceledException)
				{
					break;
				}
			}
		}
	}

	private void Record(int ok)
	{
		Volatile.Write(ref LastOk, ok);
		Interlocked.Increment(ref LastCount);
	}

	private void Fail(string message)
	{
		Pending item;
		while (queue.TryTake(out item))
		{
		}
		state = new ServiceState(null, message, connected: false);
	}

	public async Task Execute(PitCommand command, CancellationToken ct, int expectedGeneration)
	{
		PitMenu before = await transport.Read(ct).ConfigureAwait(continueOnCapturedContext: false);
		// The menu checked against the LMU version validated (unknown lines: read only) before anything is written.
		Seen?.Invoke(before);
		if (command.RequiresMountedTyres)
		{
			if (!(transport is IMountedTyreTransport tyreTransport))
			{
				throw new InvalidOperationException("Lecture du composé monté indisponible.");
			}
			MountedTyreState mounted = await tyreTransport.ReadMountedTyres(ct).ConfigureAwait(continueOnCapturedContext: false);
			if (!before.SameSettings(mounted.Menu))
			{
				throw new InvalidOperationException("Menu changé pendant la lecture des pneus : recommencer.");
			}
			before.MountedCompounds = mounted.Compounds;
		}
		PitMenu desired = before.Copy();
		command.Apply(desired);
		IDictionary<string, int> changes = desired.ChangesFrom(before);
		if (changes.Count == 0)
		{
			state = new ServiceState(before, "Déjà réglé : " + command.Name, connected: true);
			return;
		}
		if (!before.SameSettings(await transport.Read(ct).ConfigureAwait(continueOnCapturedContext: false)))
		{
			throw new InvalidOperationException("Menu modifié entre-temps : commande annulée, recommencer.");
		}
		if (!WritesEnabled || expectedGeneration != Volatile.Read(ref generation))
		{
			return;
		}
		string readOnly = ReadOnlyReason;
		if (readOnly != null)
		{
			throw new InvalidOperationException("Menu des stands en lecture seule : " + readOnly);
		}
		await transport.Write(desired, ct).ConfigureAwait(continueOnCapturedContext: false);
		PitMenu actual = null;
		for (int attempt = 0; attempt < 4; attempt++)
		{
			await Task.Delay(100, ct).ConfigureAwait(continueOnCapturedContext: false);
			actual = await transport.Read(ct).ConfigureAwait(continueOnCapturedContext: false);
			try
			{
				desired.Verify(actual, changes);
			}
			catch (InvalidOperationException)
			{
				if (attempt == 3)
				{
					throw;
				}
				continue;
			}
			break;
		}
		state = new ServiceState(status: "Vérifié : " + string.Join(" | ", changes.Keys.Select((string k) => k + " " + actual.Value(k))), menu: actual, connected: true);
	}

	public void Dispose()
	{
		if (Interlocked.Exchange(ref disposed, 1) != 0)
		{
			return;
		}
		EnableWrites(value: false);
		cancel.Cancel();
		if (worker == null)
		{
			transport.Dispose();
			cancel.Dispose();
			queue.Dispose();
			return;
		}
		worker.ContinueWith(delegate
		{
			transport.Dispose();
			cancel.Dispose();
			queue.Dispose();
		}, TaskScheduler.Default);
	}
}
