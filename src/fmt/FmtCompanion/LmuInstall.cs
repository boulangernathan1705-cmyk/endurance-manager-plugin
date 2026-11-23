using System;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace FmtCompanion;

public static class LmuInstall
{
	public static string Version()
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("Le Mans Ultimate");
			foreach (Process process in processesByName)
			{
				using (process)
				{
					string path = Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "Core", "Version.txt");
					if (File.Exists(path))
					{
						return (from l in File.ReadLines(path)
							select l.Trim()).FirstOrDefault((string l) => l.Length > 0);
					}
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}

	public static string UserDataPlayer()
	{
		try
		{
			Process[] processesByName = Process.GetProcessesByName("Le Mans Ultimate");
			int num = 0;
			if (num < processesByName.Length)
			{
				Process process = processesByName[num];
				using (process)
				{
					return Path.Combine(Path.GetDirectoryName(process.MainModule.FileName), "UserData", "player");
				}
			}
		}
		catch (Exception)
		{
		}
		return null;
	}
}
