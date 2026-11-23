using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Newtonsoft.Json.Linq;
using SimHub.Plugins.OutputPlugins.ControlRemapper.Models;

namespace FmtCompanion;

internal sealed class Preparer
{
	private readonly MapperOutput output;

	private readonly Settings settings;

	private readonly Action save;

	private readonly LmuOptionsClient lmu = new LmuOptionsClient();

	private int running;

	public bool Running => Volatile.Read(ref running) == 1;

	public Preparer(MapperOutput output, Settings settings, Action save)
	{
		this.output = output;
		this.settings = settings;
		this.save = save;
	}

	private static T Ui<T>(Func<T> f)
	{
		return Application.Current.Dispatcher.Invoke(f);
	}

	private static void Ui(Action a)
	{
		Application.Current.Dispatcher.Invoke(a);
	}

	public Task<JObject> Controls(CancellationToken ct)
	{
		return lmu.Controls(ct);
	}

	public Task Put(string path, CancellationToken ct)
	{
		return lmu.Put(path, ct);
	}

	// The pilot's own role already bound in LMU to this function's command, on the Control Mapper output (any column):
	// FMT can press it and needs no new button. Null when there is none.
	public static string ReuseRole(JObject di, FmtFunction f, Func<int, string> outputRole, bool bridge)
	{
		foreach (string column in new string[2] { "Input", "Alternative Input" })
		{
			LmuBinding binding = LmuControlsModel.Binding(di, column, f.LmuControl);
			if (binding == null || !LmuControlsModel.IsOutputDevice(binding.Device, bridge))
			{
				continue;
			}
			int num = LmuControlsModel.ButtonNumber(di, binding);
			string role = num > 0 ? outputRole(num) : null;
			if (!string.IsNullOrEmpty(role) && role != f.Role)
			{
				return role;
			}
		}
		return null;
	}

	public static string State(JObject di, FmtFunction f, Func<int, string> outputRole, bool bridge, out bool ready)
	{
		ready = false;
		string text = null;
		string[] array = new string[2] { "Alternative Input", "Input" };
		foreach (string text2 in array)
		{
			LmuBinding lmuBinding = LmuControlsModel.Binding(di, text2, f.LmuControl);
			if (lmuBinding != null)
			{
				int num = LmuControlsModel.ButtonNumber(di, lmuBinding);
				string text3 = ((text2 == "Input") ? "Principal" : "Secondaire") + " ";
				if (LmuControlsModel.IsOutputDevice(lmuBinding.Device, bridge) && num > 0 && outputRole(num) == f.Role)
				{
					ready = true;
					return "Prêt — " + text3 + "B" + num;
				}
				if (text == null)
				{
					string[] array2 = LmuControlsModel.InputNames(di, lmuBinding.Device);
					string text4 = ((lmuBinding.Id >= 0 && lmuBinding.Id < array2.Length) ? array2[lmuBinding.Id] : ("#" + lmuBinding.Id));
					text = "LMU : " + text3 + ShortDevice(lmuBinding.Device) + " " + text4 + " — sans rôle FMT";
				}
			}
		}
		string reuse = ReuseRole(di, f, outputRole, bridge);
		if (reuse != null)
		{
			ready = true;
			return "Prêt — ton bouton « " + reuse + " »";
		}
		return text ?? "À préparer";
	}

	public static string ShortDevice(string device)
	{
		int num = device?.LastIndexOf('-') ?? (-1);
		if (num <= 0)
		{
			return device;
		}
		return device.Substring(0, num);
	}

	public async Task Prepare(IList<FmtFunction> functions, Action<string> log, CancellationToken ct)
	{
		if (Interlocked.Exchange(ref running, 1) == 1)
		{
			log("Préparation déjà en cours.");
			return;
		}
		try
		{
			if (!Ui(() => output.Usable))
			{
				log("Aucune sortie manette dans Control Mapper (vJoy ou bridge SimHub). Active-la dans Control Mapper puis recommence. Le clavier n'est pas utilisé automatiquement : LMU n'a pas de colonne Secondaire pour le clavier.");
				return;
			}
			bool bridge = Ui(() => output.Bridge);
			AssignPhase phase = LmuControlsModel.Phase(await lmu.AssignStatus(ct).ConfigureAwait(continueOnCapturedContext: false));
			if (phase == AssignPhase.BeginInput || phase == AssignPhase.DetectInput)
			{
				log("LMU est déjà en train d'affecter une commande : termine ou annule dans LMU, puis recommence.");
				return;
			}
			JObject di = LmuControlsModel.DirectInput(await lmu.Controls(ct).ConfigureAwait(continueOnCapturedContext: false));
			// The commands LMU already has on one of the pilot's own buttons: FMT presses those (no new button).
			int reusedCount = 0;
			foreach (FmtFunction fn in functions.Where((FmtFunction x) => x.Kind == FunctionKind.LmuKey))
			{
				string role = ReuseRole(di, fn, (int n) => Ui(() => output.OutputRole(n)), bridge);
				bool fmtButton = Ui(() => output.ButtonFor(fn.Role)) > 0;
				if (role != null && !fmtButton)
				{
					settings.Reused[fn.Role] = role;
					reusedCount++;
				}
				else
				{
					settings.Reused.Remove(fn.Role);
				}
			}
			save();
			if (reusedCount > 0)
			{
				log(reusedCount + " fonction(s) passent par tes boutons déjà reliés dans LMU (aucun nouveau bouton).");
			}
			HashSet<int> used = LmuControlsModel.UsedButtons(di, bridge);
			int limit = Ui(() => output.ButtonLimit);
			Queue<int> free = new Queue<int>(from n in Enumerable.Range(1, limit)
				where !used.Contains(n) && Ui(() => output.Output(n) != null && string.IsNullOrEmpty(output.OutputRole(n)))
				select n);
			int todo = functions.Where((FmtFunction x) => x.Kind == FunctionKind.LmuKey).Where(delegate(FmtFunction x)
			{
				State(di, x, (int n) => Ui(() => output.OutputRole(n)), bridge, out var ready2);
				return !ready2 && LmuControlsModel.Binding(di, "Alternative Input", x.LmuControl) == null;
			}).Count();
			log(free.Count + " bouton(s) libre(s) sur la sortie " + Ui(() => output.Describe()) + " ; " + todo + " fonction(s) cochée(s) à préparer.");
			if (todo > free.Count)
			{
				log("Pas assez de boutons libres : décoche au moins " + (todo - free.Count) + " fonction(s), puis recommence. Rien n'a été modifié.");
				return;
			}
			if (todo == 0)
			{
				log("Rien à préparer.");
				return;
			}
			int done = 0;
			int skipped = 0;
			bool backedUp = false;
			foreach (FmtFunction f in functions.Where((FmtFunction x) => x.Kind == FunctionKind.LmuKey))
			{
				ct.ThrowIfCancellationRequested();
				State(di, f, (int n) => Ui(() => output.OutputRole(n)), bridge, out var ready);
				if (ready)
				{
					continue;
				}
				string reuse = ReuseRole(di, f, (int n) => Ui(() => output.OutputRole(n)), bridge);
				if (reuse != null)
				{
					continue;
				}
				LmuBinding secondary = LmuControlsModel.Binding(di, "Alternative Input", f.LmuControl);
				if (secondary != null)
				{
					log(f.Label + " : Secondaire déjà occupée dans LMU (" + ShortDevice(secondary.Device) + "), rien remplacé.");
					skipped++;
					continue;
				}
				if (free.Count == 0)
				{
					log("Plus de bouton libre sur la sortie : arrêt. " + done + " fonction(s) préparée(s).");
					return;
				}
				int button = free.Dequeue();
				if (!backedUp)
				{
					Ui(delegate
					{
						output.Backup();
					});
					backedUp = true;
				}
				string role = f.Role;
				Ui(delegate
				{
					output.Output(button).TargetRole = role;
				});
				string error;
				try
				{
					error = await AssignOne(f, button, bridge, di, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				catch (Exception ex)
				{
					error = ex.Message;
				}
				if (error != null)
				{
					Ui(delegate
					{
						ButtonMap buttonMap = output.Output(button);
						if (buttonMap != null && buttonMap.TargetRole == role)
						{
							buttonMap.TargetRole = null;
						}
						output.Save();
					});
					log(f.Label + " : " + error + " Préparation arrêtée, " + done + " fonction(s) préparée(s).");
					return;
				}
				di = LmuControlsModel.DirectInput(await lmu.Controls(ct).ConfigureAwait(continueOnCapturedContext: false));
				LmuBinding got = LmuControlsModel.Binding(di, "Alternative Input", f.LmuControl);
				settings.Prepared.Add(new PreparedKey
				{
					Control = f.LmuControl,
					Role = role,
					Device = got.Device,
					LmuId = got.Id,
					Button = button,
					Bridge = bridge,
					Utc = DateTime.UtcNow
				});
				Ui(delegate
				{
					output.Save();
				});
				save();
				done++;
				log(f.Label + " : Secondaire = B" + button + ".");
				await Task.Delay(150, ct).ConfigureAwait(continueOnCapturedContext: false);
			}
			log("Terminé : " + done + " fonction(s) préparée(s)" + ((skipped > 0) ? (", " + skipped + " laissée(s) telle(s) quelle(s)") : "") + ".");
		}
		catch (OperationCanceledException)
		{
			log("Préparation interrompue.");
		}
		catch (Exception ex3)
		{
			log("Préparation arrêtée : " + ex3.Message);
		}
		finally
		{
			Volatile.Write(ref running, 0);
		}
	}

	private async Task<string> AssignOne(FmtFunction f, int button, bool bridge, JObject before, CancellationToken ct)
	{
		LmuBinding primaryBefore = LmuControlsModel.Binding(before, "Input", f.LmuControl);
		await lmu.Listen(f.LmuControl, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (await WaitFor(ct, AssignPhase.DetectInput) != AssignPhase.DetectInput)
		{
			await SafeCancel();
			return "LMU ne s'est pas mis en écoute.";
		}
		await Task.Delay(300, ct).ConfigureAwait(continueOnCapturedContext: false);
		if (!Ui(() => output.Trigger(f.Role, 200)))
		{
			await SafeCancel();
			return "Control Mapper a refusé l'impulsion.";
		}
		switch (await WaitFor(ct, AssignPhase.AlreadyAssigned, AssignPhase.Done, AssignPhase.Finished, AssignPhase.Cancelled))
		{
		case AssignPhase.AlreadyAssigned:
			await SafeCancel();
			return "LMU signale ce bouton comme déjà utilisé (non confirmé).";
		default:
			await SafeCancel();
			return "Aucune impulsion reçue par LMU : vérifier la sortie de Control Mapper.";
		case AssignPhase.Done:
		case AssignPhase.Finished:
		{
			JObject jObject = LmuControlsModel.DirectInput(await lmu.Controls(ct).ConfigureAwait(continueOnCapturedContext: false));
			JObject di = jObject;
			LmuBinding got = LmuControlsModel.Binding(di, "Alternative Input", f.LmuControl);
			if (got == null || !LmuControlsModel.IsOutputDevice(got.Device, bridge) || LmuControlsModel.ButtonNumber(di, got) != button)
			{
				if (got != null)
				{
					await lmu.Unset(f.LmuControl, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				}
				return "LMU a enregistré une autre entrée que B" + button + " (retirée).";
			}
			LmuBinding primaryAfter = LmuControlsModel.Binding(di, "Input", f.LmuControl);
			if (!(primaryBefore?.Same(primaryAfter) ?? (primaryAfter == null)))
			{
				await lmu.Unset(f.LmuControl, CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
				return "La colonne Principal a changé pendant l'affectation : Secondaire retirée, vérifier dans LMU.";
			}
			return null;
		}
		}
	}

	private async Task<AssignPhase> WaitFor(CancellationToken ct, params AssignPhase[] wanted)
	{
		AssignPhase phase = AssignPhase.None;
		for (int i = 0; i < 40; i++)
		{
			AssignPhase assignPhase = LmuControlsModel.Phase(await lmu.AssignStatus(ct).ConfigureAwait(continueOnCapturedContext: false));
			phase = assignPhase;
			if (wanted.Contains(phase))
			{
				return phase;
			}
			await Task.Delay(100, ct).ConfigureAwait(continueOnCapturedContext: false);
		}
		return phase;
	}

	private async Task SafeCancel()
	{
		int num = default(int);
		_ = num;
		_ = 0;
		try
		{
			await lmu.Cancel(CancellationToken.None).ConfigureAwait(continueOnCapturedContext: false);
		}
		catch (Exception)
		{
		}
	}

	public async Task Undo(Action<string> log, CancellationToken ct)
	{
		if (Interlocked.Exchange(ref running, 1) == 1)
		{
			log("Préparation en cours : attendre.");
			return;
		}
		try
		{
			int removed = 0;
			int kept = 0;
			JObject jObject = LmuControlsModel.DirectInput(await lmu.Controls(ct).ConfigureAwait(continueOnCapturedContext: false));
			JObject di = jObject;
			foreach (PreparedKey entry in settings.Prepared.ToList())
			{
				LmuBinding current = LmuControlsModel.Binding(di, "Alternative Input", entry.Control);
				if (current != null && current.Device == entry.Device && current.Id == entry.LmuId)
				{
					await lmu.Unset(entry.Control, ct).ConfigureAwait(continueOnCapturedContext: false);
				}
				else if (current != null)
				{
					kept++;
				}
				PreparedKey e = entry;
				Ui(delegate
				{
					ButtonMap buttonMap = output.Output(e.Button);
					if (buttonMap != null && buttonMap.TargetRole == e.Role)
					{
						buttonMap.TargetRole = null;
					}
				});
				settings.Prepared.Remove(entry);
				removed++;
			}
			Ui(delegate
			{
				output.Save();
			});
			save();
			log("Annulé : " + removed + " affectation(s) FMT retirée(s)" + ((kept > 0) ? (" ; " + kept + " Secondaire(s) modifiée(s) depuis, laissée(s) dans LMU") : "") + ".");
		}
		catch (Exception ex)
		{
			log("Annulation interrompue : " + ex.Message);
		}
		finally
		{
			Volatile.Write(ref running, 0);
		}
	}

	public void Dispose()
	{
		lmu.Dispose();
	}
}
