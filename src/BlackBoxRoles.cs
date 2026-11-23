// Les six boutons de la black box du VoCore en rôles du Control Mapper de SimHub (« EM - Black box - Monter »…), comme
// les rôles « FMT - … » de FMT companion : le pilote y relie ses boutons du volant, plus besoin de « Contrôles et
// événements » (demande de Nathan, 23/11/2026). Même méthode que FMT companion 0.5, lue dans sa DLL : un rôle est un
// bouton virtuel (VirtualButtonMap, TargetRole) de la sortie du Control Mapper, enregistré par SaveSettings ; un appui
// sur un bouton relié arrive par l'événement InputPressed de SimHub sous le nom « ControlMapperPlugin.<rôle> ».
// Tout passe par réflexion : rien de nouveau dans la façade (reference/), et un SimHub sans Control Mapper n'empêche
// pas le plugin de tourner. Les rôles ne commencent pas par « FMT » : FMT companion range les siens et retirerait les
// autres.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SimHub.Plugins;

namespace EnduranceManager.SimHub
{
    public class BlackBoxRoles
    {
        const string Prefix = "ControlMapperPlugin.";
        const string PluginType = "SimHub.Plugins.OutputPlugins.ControlRemapper.ControlMapperPlugin";
        const string ButtonType = "SimHub.Plugins.OutputPlugins.ControlRemapper.Models.VirtualButtonMap";

        readonly Func<PluginManager> manager;
        readonly Dictionary<string, Action> handlers;
        System.Threading.Timer retry;
        int tries;
        bool listening;

        public string Status { get; private set; } = "Rôles du Control Mapper pas encore créés.";

        // Role names and what a press does.
        public BlackBoxRoles(Func<PluginManager> manager, IDictionary<string, Action> roles)
        {
            this.manager = manager;
            handlers = new Dictionary<string, Action>(roles, StringComparer.OrdinalIgnoreCase);
        }

        // Listens to the presses at once; creates the roles when the Control Mapper is ready (it loads after the plugins:
        // tried again every 5 s for two minutes, as FMT companion does).
        public void Start()
        {
            Listen();
            retry = new System.Threading.Timer(_ => Tick(), null, 0, 5000);
        }

        public void Stop()
        {
            retry?.Dispose();
            retry = null;
        }

        void Tick()
        {
            if (!listening) Listen();
            if (Ensure() || ++tries >= 24) Stop();
        }

        // SimHub's InputPressed (PluginManager.InputTriggeredDelegate, bool (string)), handed our OnPressed.
        void Listen()
        {
            try
            {
                var pm = manager();
                var evt = pm?.GetType().GetEvent("InputPressed");
                if (evt == null) return;
                var handler = Delegate.CreateDelegate(evt.EventHandlerType, this, typeof(BlackBoxRoles).GetMethod(nameof(OnPressed), BindingFlags.Instance | BindingFlags.NonPublic));
                evt.AddEventHandler(pm, handler);
                listening = true;
            }
            catch (Exception e) { Status = "Appuis du Control Mapper non écoutés : " + (e.InnerException ?? e).Message; }
        }

        bool OnPressed(string input)
        {
            try
            {
                if (input != null && input.StartsWith(Prefix, StringComparison.Ordinal) && handlers.TryGetValue(input.Substring(Prefix.Length), out var act)) act();
            }
            catch { }
            return false;
        }

        // Our roles added to the Control Mapper's virtual buttons when missing, then saved. True once done (or nothing to do).
        bool Ensure()
        {
            try
            {
                var pm = manager();
                if (pm == null) return false;
                var pluginType = FindType(PluginType);
                var buttonType = FindType(ButtonType);
                if (pluginType == null || buttonType == null) { Status = "Control Mapper absent : active-le dans SimHub."; return false; }
                var get = pm.GetType().GetMethods().FirstOrDefault(m => m.Name == "GetPlugin" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                var plugin = get?.MakeGenericMethod(pluginType).Invoke(pm, null);
                var settings = Member(plugin, "controlMapperPluginSettings");
                var list = Member(Member(Member(settings, "OutputMapping"), "ControllerMapping"), "VirtualButtons") as IList;
                if (list == null) { Status = "Control Mapper pas encore prêt."; return false; }
                bool added = false;
                void Add()
                {
                    var have = new HashSet<string>(list.Cast<object>().Select(b => Member(b, "TargetRole") as string).Where(r => r != null), StringComparer.Ordinal);
                    foreach (var role in handlers.Keys.Where(r => !have.Contains(r)))
                    {
                        var button = Activator.CreateInstance(buttonType);
                        buttonType.GetProperties().Where(p => p.Name == "TargetRole" && p.CanWrite).OrderByDescending(p => Depth(p.DeclaringType)).FirstOrDefault()?.SetValue(button, role);
                        list.Add(button);
                        added = true;
                    }
                    if (added) pluginType.GetMethod("SaveSettings", Type.EmptyTypes)?.Invoke(plugin, null);
                }
                // The list is shown by the Control Mapper's screen: changed on its thread.
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.Invoke(new Action(Add)); else Add();
                Status = handlers.Count + " rôles « EM - Black box » dans le Control Mapper" + (added ? " (créés)." : ".");
                return true;
            }
            catch (Exception e) { Status = "Rôles du Control Mapper : " + (e.InnerException ?? e).Message; return false; }
        }

        static int Depth(Type type) { int depth = 0; for (; type != null; type = type.BaseType) depth++; return depth; }

        static Type FindType(string name) =>
            AppDomain.CurrentDomain.GetAssemblies().Select(a => { try { return a.GetType(name, false); } catch { return null; } }).FirstOrDefault(t => t != null);

        // A public field or property, null when absent.
        static object Member(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(target);
            // The most derived one when a property is redeclared (GetProperty would find two).
            return type.GetProperties(flags).Where(p => p.Name == name && p.GetIndexParameters().Length == 0)
                .OrderByDescending(p => Depth(p.DeclaringType)).FirstOrDefault()?.GetValue(target);
        }
    }
}
