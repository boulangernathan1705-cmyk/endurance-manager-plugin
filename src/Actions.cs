// The plugin's actions (SimHub, Contrôles et événements), each tied to its module: an action of a module turned off is
// taken out of SimHub's list, so a pilot only sees the buttons he can use. SimHub 9.13.6 added RemoveAction to the
// plugin SDK; it is called by reflection, so the plugin still runs on an older SimHub, where every action stays listed
// as before (no RemoveAction = nothing hidden). A mapping kept in SimHub comes back as soon as the module is on again:
// the action is added again under the same name.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimHub.Plugins;

namespace EnduranceManager.SimHub
{
    public class Actions
    {
        class Entry
        {
            public string Name;
            public Func<bool> On;
            public Action<PluginManager, string> Start, End;
            public bool Added;
        }

        // The plugin's own type, never IPlugin: SimHub names an action after the type it is added with
        // (« EnduranceManagerPlugin.Profil1 »), and pilots' mappings use that name.
        readonly EnduranceManagerPlugin plugin;
        readonly List<Entry> entries = new List<Entry>();
        readonly MethodInfo remove;

        public Actions(EnduranceManagerPlugin plugin)
        {
            this.plugin = plugin;
            // IPluginExtensions.RemoveAction<TPlugin>(this TPlugin plugin, string actionName), read in SimHub 9.13.7's
            // SimHub.Plugins.dll (Nathan's PC, 24/11/2026). Same name handling as the AddAction extension.
            try
            {
                var generic = typeof(IPluginExtensions).GetMethods(BindingFlags.Public | BindingFlags.Static)
                    .FirstOrDefault(m => m.Name == "RemoveAction" && m.IsGenericMethodDefinition && m.GetParameters().Length == 2
                        && m.GetParameters()[1].ParameterType == typeof(string));
                remove = generic?.MakeGenericMethod(typeof(EnduranceManagerPlugin));
            }
            catch { remove = null; }
        }

        // Whether this SimHub can take an action out of its list (9.13.6 or newer).
        public bool CanHide => remove != null;

        // Declares an action of a module; Sync adds it to SimHub when its module is on.
        public void Add(string name, Func<bool> on, Action<PluginManager, string> start, Action<PluginManager, string> end = null)
            => entries.Add(new Entry { Name = name, On = on, Start = start, End = end });

        // Puts SimHub's list in line with the modules: at start, and after each module switch.
        public void Sync()
        {
            foreach (var e in entries)
            {
                bool want = !CanHide || SafeOn(e);
                if (want && !e.Added)
                {
                    plugin.AddAction(e.Name, e.Start, e.End);
                    e.Added = true;
                }
                else if (!want && e.Added)
                {
                    try { remove.Invoke(null, new object[] { plugin, e.Name }); e.Added = false; }
                    catch (Exception x) { Report.Log("actions", e.Name + " : " + (x.InnerException ?? x).Message); }
                }
            }
        }

        static bool SafeOn(Entry e) { try { return e.On(); } catch { return true; } }

        // For the tests (Replay): the actions SimHub lists now.
        public IEnumerable<string> Listed => entries.Where(e => e.Added).Select(e => e.Name);
    }
}
