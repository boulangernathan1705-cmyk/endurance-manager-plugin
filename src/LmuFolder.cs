// Le dossier d'installation de LMU (pour UserData\player).
// Process.MainModule échoue quand LMU tourne en administrateur, ou avec une autre architecture que SimHub
// (« Lance LMU » affiché alors que LMU est ouvert, Nathan le 24/11/2026). QueryFullProcessImageName avec le droit limité
// marche dans ces cas ; à défaut, la bibliothèque Steam (application 2399420).
using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace EnduranceManager.SimHub
{
    public static class LmuFolder
    {
        const uint QueryLimited = 0x1000;

        [DllImport("kernel32.dll", SetLastError = true)] static extern IntPtr OpenProcess(uint access, bool inherit, int id);
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Unicode)] static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder name, ref int size);
        [DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);

        // The folder holding « Le Mans Ultimate.exe », null when not found.
        public static string Install()
        {
            foreach (var process in Process.GetProcessesByName("Le Mans Ultimate"))
                using (process)
                {
                    string exe = ImagePath(process.Id);
                    if (exe == null) try { exe = process.MainModule.FileName; } catch { }
                    if (exe != null) return Path.GetDirectoryName(exe);
                }
            return Steam();
        }

        public static string Player()
        {
            string install = Install();
            return install == null ? null : Path.Combine(install, "UserData", "player");
        }

        static string ImagePath(int id)
        {
            IntPtr handle = IntPtr.Zero;
            try
            {
                handle = OpenProcess(QueryLimited, false, id);
                if (handle == IntPtr.Zero) return null;
                var name = new StringBuilder(1024);
                int size = name.Capacity;
                return QueryFullProcessImageName(handle, 0, name, ref size) ? name.ToString(0, size) : null;
            }
            catch { return null; }
            finally { if (handle != IntPtr.Zero) CloseHandle(handle); }
        }

        static string Steam()
        {
            try
            {
                string steam = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(@"Software\Valve\Steam")?.GetValue("SteamPath") as string;
                if (string.IsNullOrEmpty(steam)) return null;
                string vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
                var libraries = new System.Collections.Generic.List<string> { steam };
                if (File.Exists(vdf))
                    foreach (Match m in Regex.Matches(File.ReadAllText(vdf), "\"path\"\\s+\"([^\"]+)\""))
                        libraries.Add(m.Groups[1].Value.Replace(@"\\", @"\"));
                foreach (var library in libraries)
                {
                    string folder = Path.Combine(library, "steamapps", "common", "Le Mans Ultimate");
                    if (File.Exists(Path.Combine(folder, "Le Mans Ultimate.exe"))) return folder;
                }
            }
            catch { }
            return null;
        }
    }
}
