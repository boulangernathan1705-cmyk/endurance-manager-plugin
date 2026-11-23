// Module FMT companion : le plugin FMT companion de Nathan, porté par ce plugin. Son code (retrouvé le 23/11/2026 en
// décompilant sa DLL 0.5) est dans src/fmt/ et compilé par la CI en FMTCompanion.dll, embarquée dans la DLL du plugin
// (ressource « fmt/FMTCompanion.dll ») : la mise à jour du plugin la donne à chaque pilote. Au lancement elle est écrite à
// part (dossier EnduranceManager de SimHub, que SimHub ne charge pas tout seul, sinon %LOCALAPPDATA%\EnduranceManager si
// ce dossier demande les droits admin) et lancée d'ici quand le module est activé : SimHub ne voit qu'un plugin, FMT
// companion garde tout ce qu'il fait, ses réglages, ses rôles du Control Mapper et ses propriétés (même nom de classe,
// donc mêmes noms pour SimHub). Depuis 0.6.0, il ne lit plus le menu des stands en boucle : il prend celui que ce plugin
// lit (Offer), sait par ce plugin si LMU tourne (LmuOpen), et ne lit lui-même le menu que si un écran montre ses valeurs.
// Activé ou coupé, le changement prend effet au prochain lancement de SimHub.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using GameReaderCommon;
using SimHub.Plugins;

namespace EnduranceManager.SimHub
{
    public class Companion
    {
        public const string FileName = "FMTCompanion.dll";
        IPlugin plugin;

        public const string Resource = "fmt/FMTCompanion.dll";
        // Where the DLL is run from: SimHub\EnduranceManager\FMTCompanion.dll, written there from the copy in this plugin
        // (or left there by an older installer); %LOCALAPPDATA%\EnduranceManager when SimHub's folder needs admin rights.
        public static string Folder => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "EnduranceManager");
        public static string Kept => Path.Combine(Folder, FileName);
        public static string Spare => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnduranceManager", FileName);
        // The old way: FMT companion as its own plugin in SimHub's folder. Both at once would run it twice.
        public static string Alone => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, FileName);

        public string Status { get; private set; } = "Module coupé.";
        public bool Running => plugin != null;
        // FMT companion itself, for its page in the plugin's screen (CompanionPage.cs): read and asked, never changed.
        public object Instance => plugin;
        // A failed start is not a change waiting for the next launch.
        public bool StartAttempted { get; private set; }

        // Endurance Manager's side (0.6.0): whether LMU runs, the pit menu each time this plugin reads it, and its page open.
        Action<string> offer;
        Action touch;

        public void Offer(string menu) { try { offer?.Invoke(menu); } catch { } }
        public void Touch() { try { touch?.Invoke(); } catch { } }

        public void Start(PluginManager manager, Func<bool> lmuOpen = null)
        {
            StartAttempted = true;
            try
            {
                if (File.Exists(Alone)) { Status = "FMT companion est encore installé seul dans SimHub : relance l'installeur du plugin, il le range ici."; return; }
                var path = Unpack();
                if (path == null) { Status = "FMTCompanion.dll n'a pas pu être écrit dans " + Folder + " ni dans " + Path.GetDirectoryName(Spare) + "."; return; }
                var assembly = Assembly.LoadFrom(path);
                var type = Loadable(assembly).FirstOrDefault(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsAbstract && t.GetConstructor(Type.EmptyTypes) != null);
                if (type == null) { Status = "Aucun plugin SimHub trouvé dans FMTCompanion.dll."; return; }
                var instance = (IPlugin)Activator.CreateInstance(type);
                instance.PluginManager = manager;
                instance.Init(manager);
                plugin = instance;
                Hook(instance, lmuOpen);
                Status = "FMT companion " + (assembly.GetName().Version?.ToString(3) ?? "") + " lancé.";
            }
            catch (Exception e)
            {
                plugin = null;
                Status = "FMT companion n'a pas pu démarrer : " + (e.InnerException ?? e).Message;
            }
        }

        // The DLL carried in this plugin, written where it can run (same bytes already there: left as it is). Without the
        // carried copy (a build without it), the one an installer left.
        static string Unpack()
        {
            byte[] carried;
            using (var stream = typeof(Companion).Assembly.GetManifestResourceStream(Resource))
            {
                if (stream == null) return File.Exists(Kept) ? Kept : null;
                using (var memory = new MemoryStream()) { stream.CopyTo(memory); carried = memory.ToArray(); }
            }
            foreach (var path in new[] { Kept, Spare })
            {
                try
                {
                    if (File.Exists(path) && File.ReadAllBytes(path).SequenceEqual(carried)) return path;
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    File.WriteAllBytes(path, carried);
                    return path;
                }
                catch { }
            }
            return File.Exists(Kept) ? Kept : null;
        }

        // FMT companion's hooks (FMTCompanionPlugin.LmuOpen, Touch, Service.Offer), by name: an older FMT companion without
        // them runs as it always did.
        void Hook(IPlugin instance, Func<bool> lmuOpen)
        {
            const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
            try
            {
                var type = instance.GetType();
                if (lmuOpen != null) type.GetField("LmuOpen", Any)?.SetValue(instance, lmuOpen);
                var touchMethod = type.GetMethod("Touch", Any, null, Type.EmptyTypes, null);
                if (touchMethod != null) touch = (Action)Delegate.CreateDelegate(typeof(Action), instance, touchMethod);
                var service = type.GetField("Service", Any)?.GetValue(instance);
                var offerMethod = service?.GetType().GetMethod("Offer", Any, null, new[] { typeof(string) }, null);
                if (offerMethod != null) offer = (Action<string>)Delegate.CreateDelegate(typeof(Action<string>), service, offerMethod);
            }
            catch { }
        }

        static Type[] Loadable(Assembly assembly)
        {
            try { return assembly.GetTypes(); }
            catch (ReflectionTypeLoadException e) { return e.Types.Where(t => t != null).ToArray(); }
        }

        // SimHub's sixty updates a second, passed on as SimHub would.
        public void DataUpdate(PluginManager manager, ref GameData data)
        {
            if (plugin is IDataPlugin data2)
                try { data2.DataUpdate(manager, ref data); } catch { }
        }

        // FMT companion's own screen, inside our « FMT companion » tab.
        public System.Windows.Controls.Control Screen(PluginManager manager)
        {
            if (plugin is IWPFSettings settings)
                try { return settings.GetWPFSettingsControl(manager); } catch (Exception e) { Status = "Écran de FMT companion : " + e.Message; }
            return null;
        }

        public void End(PluginManager manager)
        {
            offer = null; touch = null;
            try { plugin?.End(manager); } catch { }
            plugin = null;
        }
    }
}
