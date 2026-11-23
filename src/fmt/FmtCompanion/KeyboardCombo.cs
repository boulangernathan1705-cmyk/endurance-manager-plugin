using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace FmtCompanion;

internal sealed class KeyboardCombo
{
	private struct MOUSEINPUT
	{
		public int dx;

		public int dy;

		public uint mouseData;

		public uint dwFlags;

		public uint time;

		public IntPtr dwExtraInfo;
	}

	private struct KEYBDINPUT
	{
		public ushort wVk;

		public ushort wScan;

		public uint dwFlags;

		public uint time;

		public IntPtr dwExtraInfo;
	}

	[StructLayout(LayoutKind.Explicit)]
	private struct InputUnion
	{
		[FieldOffset(0)]
		public MOUSEINPUT mi;

		[FieldOffset(0)]
		public KEYBDINPUT ki;
	}

	private struct INPUT
	{
		public uint type;

		public InputUnion u;
	}

	private const uint ScanCode = 8u;

	private const uint KeyUp = 2u;

	private readonly MapperOutput output;

	private int busy;

	public volatile string Status = "";

	private volatile bool held;

	[DllImport("user32.dll", SetLastError = true)]
	private static extern uint SendInput(uint n, INPUT[] inputs, int size);

	[DllImport("user32.dll")]
	private static extern IntPtr GetForegroundWindow();

	[DllImport("user32.dll")]
	private static extern uint GetWindowThreadProcessId(IntPtr h, out uint p);

	public KeyboardCombo(MapperOutput output)
	{
		this.output = output;
	}

	public static ushort[] Scancodes(string modifiers)
	{
		return (from m in modifiers.Split('+')
			select m.Trim() switch
			{
				"Ctrl" => 29, 
				"Shift" => 42, 
				"Alt" => 56, 
				"CtrlD" => 285, 
				"AltGr" => 312, 
				_ => throw new ArgumentException("Touche inconnue : " + m), 
			}).ToArray();
	}

	public static string Help(FmtFunction f)
	{
		FmtFunction fmtFunction = Catalog.Find(f.SeatId);
		return "Secours : tenir " + f.Modifiers.Replace("+", " + ") + " au clavier et appuyer « " + ((fmtFunction == null) ? f.SeatId : fmtFunction.Label) + " »";
	}

	private static bool LmuInFront()
	{
		GetWindowThreadProcessId(GetForegroundWindow(), out var p);
		try
		{
			using Process process = Process.GetProcessById((int)p);
			return process.ProcessName == "Le Mans Ultimate";
		}
		catch (ArgumentException)
		{
			return false;
		}
	}

	private static uint Send(ushort scan, bool up)
	{
		INPUT[] array = new INPUT[1];
		array[0].type = 1u;
		array[0].u.ki.wScan = (ushort)(scan & 0xFFu);
		array[0].u.ki.dwFlags = 8u | (up ? 2u : 0u) | (((scan & 0x100u) != 0) ? 1u : 0u);
		return SendInput(1u, array, Marshal.SizeOf(typeof(INPUT)));
	}

	public void Release()
	{
		held = false;
	}

	public bool Run(FmtFunction f, int waitSeconds = 0)
	{
		FmtFunction seat = Catalog.Find(f.SeatId);
		if (seat == null)
		{
			return false;
		}
		if (waitSeconds == 0 && !LmuInFront())
		{
			Status = f.Label + " : LMU doit être au premier plan. " + Help(f) + ".";
			return false;
		}
		if (Interlocked.Exchange(ref busy, 1) == 1)
		{
			return false;
		}
		held = waitSeconds == 0;
		ushort[] keys = Scancodes(f.Modifiers);
		Task.Run(delegate
		{
			uint num = 0u;
			bool flag = true;
			try
			{
				Stopwatch stopwatch = Stopwatch.StartNew();
				if (waitSeconds > 0)
				{
					Status = f.Label + " : retourne dans LMU, envoi dès qu'il est au premier plan (" + waitSeconds + " s max).";
				}
				while (!LmuInFront())
				{
					if (stopwatch.Elapsed.TotalSeconds > (double)waitSeconds)
					{
						Status = f.Label + " : LMU n'est pas revenu au premier plan, rien envoyé.";
						return;
					}
					Thread.Sleep(100);
				}
				if (waitSeconds > 0)
				{
					Thread.Sleep(800);
				}
				ushort[] array = keys;
				foreach (ushort scan in array)
				{
					num += Send(scan, up: false);
				}
				Thread.Sleep(50);
				Stopwatch stopwatch2 = Stopwatch.StartNew();
				flag = Application.Current.Dispatcher.Invoke(() => output.Trigger(seat.Role, 80));
				while (held && stopwatch2.ElapsedMilliseconds < 400)
				{
					Thread.Sleep(10);
				}
				while (flag && held && stopwatch2.ElapsedMilliseconds < 8000)
				{
					flag = Application.Current.Dispatcher.Invoke(() => output.Trigger(seat.Role, 80));
					Thread.Sleep(150);
				}
				Thread.Sleep(120);
				Status = ((!flag) ? (f.Label + " : « " + seat.Label + " » n'a pas de bouton FMT. Préparer LMU d'abord. " + Help(f) + ".") : ((num != keys.Length) ? (f.Label + " : Windows a refusé " + (keys.Length - num) + " touche(s) clavier. " + Help(f) + ".") : (f.Label + " : envoyé (" + f.Modifiers + " + " + seat.Label + ", " + (double)(stopwatch2.ElapsedMilliseconds / 100) / 10.0 + " s).")));
			}
			catch (Exception ex)
			{
				Status = f.Label + " : " + ex.Message;
			}
			finally
			{
				held = false;
				foreach (ushort item in keys.Reverse())
				{
					Send(item, up: true);
				}
				Volatile.Write(ref busy, 0);
			}
		});
		return true;
	}
}
