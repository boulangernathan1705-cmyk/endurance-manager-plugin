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
        [DllImport("iphlpapi.dll")] static extern uint GetExtendedTcpTable(IntPtr table, ref int size, bool sort, int family, int tableClass, uint reserved);

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
            // The process that listens on LMU's local port 6397 (whatever its name).
            int owner = PortOwner(6397);
            if (owner > 0) { string exe = ImagePath(owner); if (exe != null) return Path.GetDirectoryName(exe); }
            return Steam();
        }

        // TCP_TABLE_OWNER_PID_LISTENER (3), IPv4: rows of 6 uint (state, local address, local port, remote address, remote port, pid).
        static int PortOwner(int port)
        {
            IntPtr table = IntPtr.Zero;
            try
            {
                int size = 0;
                GetExtendedTcpTable(IntPtr.Zero, ref size, false, 2, 3, 0);
                table = Marshal.AllocHGlobal(size);
                if (GetExtendedTcpTable(table, ref size, false, 2, 3, 0) != 0) return 0;
                int rows = Marshal.ReadInt32(table);
                for (int i = 0; i < rows; i++)
                {
                    IntPtr row = table + 4 + i * 24;
                    int local = Marshal.ReadInt32(row + 8);
                    int p = ((local & 0xFF) << 8) | ((local >> 8) & 0xFF);
                    if (p == port) return Marshal.ReadInt32(row + 20);
                }
            }
            catch { }
            finally { if (table != IntPtr.Zero) Marshal.FreeHGlobal(table); }
            return 0;
        }

        public static string Player()
        {
            string install = Install();
            return install == null ? null : Path.Combine(install, "UserData", "player");
        }

        // For the log when nothing is found: the processes that look like LMU, and what each way gave.
        public static string Diagnose()
        {
            var parts = new System.Collections.Generic.List<string>();
            try
            {
                foreach (var process in Process.GetProcesses())
                    using (process)
                    {
                        string name = process.ProcessName;
                        if (name.IndexOf("mans", StringComparison.OrdinalIgnoreCase) < 0 && name.IndexOf("lmu", StringComparison.OrdinalIgnoreCase) < 0) continue;
                        string how;
                        try { how = ImagePath(process.Id) ?? "accès refusé (" + Marshal.GetLastWin32Error() + ")"; } catch (Exception e) { how = e.Message; }
                        parts.Add(name + " → " + how);
                    }
            }
            catch (Exception e) { parts.Add(e.Message); }
            int owner = PortOwner(6397);
            parts.Add("port 6397 : " + (owner > 0 ? owner + " → " + (ImagePath(owner) ?? "accès refusé") : "personne"));
            parts.Add("Steam : " + (Steam() ?? "rien"));
            parts.Add("SimHub " + (Environment.Is64BitProcess ? "64" : "32") + " bits");
            return string.Join(" ; ", parts);
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
