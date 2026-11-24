// « Tout nettoyer » (Réglages voiture en course, demande de Nathan 24/11/2026) : repartir propre pour relier ses boutons.
// Avec une sauvegarde du mapping avant (celle de FMT companion) :
// - LMU : les touches que FMT companion a ajoutées sont retirées (son « Annuler », qui ne touche qu'à ce qu'il a mis) ;
// - Control Mapper : tout ce qui vise un rôle « FMT - … » est délié (boutons du volant, croix, axes, sorties) et les
//   boutons virtuels de ces rôles sont retirés ; les liaisons « EM - … » (profils, black box) restent ;
// - FMT companion oublie les boutons du pilote qu'il réutilisait.
// LMU n'est pas remis entièrement par défaut : le volant, les pédales et les touches du pilote restent. Ensuite :
// « Préparer LMU ». Tout passe par réflexion, comme BlackBoxRoles.cs.
using System;
using System.Collections;
using System.Linq;
using System.Reflection;

namespace EnduranceManager.SimHub
{
    public static class Nettoyage
    {
        const string PluginType = "SimHub.Plugins.OutputPlugins.ControlRemapper.ControlMapperPlugin";

        // Only FMT companion's (Nathan 24/11 : « retirer que les touches FMT ») : the « EM - … » links stay.
        static bool Ours(object item) => Member(item, "TargetRole") is string role && role.StartsWith("FMT - ", StringComparison.OrdinalIgnoreCase);

        public static object Run(EnduranceManagerPlugin plugin)
        {
            try
            {
                var page = plugin.CompanionPage;
                bool fmt = plugin.Companion.Running;
                if (fmt) page.Act("bbBackup", null, false);
                if (fmt) page.Act("bbUndo", null, false);
                int unlinked = 0, removed = 0;
                string error = null;
                void Clean()
                {
                    var pm = plugin.PluginManager;
                    var type = AppDomain.CurrentDomain.GetAssemblies().Select(a => { try { return a.GetType(PluginType, false); } catch { return null; } }).FirstOrDefault(t => t != null);
                    var get = pm?.GetType().GetMethods().FirstOrDefault(m => m.Name == "GetPlugin" && m.IsGenericMethodDefinition && m.GetParameters().Length == 0);
                    var mapper = type == null ? null : get?.MakeGenericMethod(type).Invoke(pm, null);
                    var settings = Member(mapper, "controlMapperPluginSettings");
                    if (settings == null) { error = "Control Mapper absent."; return; }
                    void Unlink(object list)
                    {
                        if (!(list is IEnumerable items)) return;
                        foreach (var item in items.Cast<object>().Where(Ours).ToList()) { Set(item, "TargetRole", null); unlinked++; }
                    }
                    if (Member(settings, "ControllerMappings") is IEnumerable sources)
                        foreach (var source in sources)
                        {
                            var map = Member(source, "ControllerMapping");
                            Unlink(Member(map, "Buttons")); Unlink(Member(map, "POVDirections")); Unlink(Member(map, "AxisToButtons"));
                        }
                    var output = Member(Member(settings, "OutputMapping"), "ControllerMapping");
                    Unlink(Member(output, "Buttons"));
                    if (Member(output, "VirtualButtons") is IList virtuals)
                        foreach (var item in virtuals.Cast<object>().Where(Ours).ToList()) { virtuals.Remove(item); removed++; }
                    type.GetMethod("SaveSettings", Type.EmptyTypes)?.Invoke(mapper, null);
                    // FMT companion: the pilot's buttons it reused are forgotten.
                    var fmtSettings = Member(plugin.Companion.Instance, "Settings");
                    if (Member(fmtSettings, "Reused") is IDictionary reused) { reused.Clear(); plugin.Companion.Instance.GetType().GetMethod("Save", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic, null, Type.EmptyTypes, null)?.Invoke(plugin.Companion.Instance, null); }
                }
                var dispatcher = System.Windows.Application.Current?.Dispatcher;
                if (dispatcher != null && !dispatcher.CheckAccess()) dispatcher.Invoke(new Action(Clean)); else Clean();
                if (error != null) return new { ok = false, message = error };
                Report.Log("nettoyage", unlinked + " liaisons retirées, " + removed + " boutons virtuels retirés, FMT " + (fmt ? "annulé" : "absent"));
                return new
                {
                    ok = true,
                    message = "Nettoyé : " + unlinked + " liaison(s) et " + removed + " bouton(s) FMT retirés" + (fmt ? ", touches FMT retirées de LMU (sauvegarde faite)" : "") +
                        ". Clique ensuite « Préparer LMU » dans Black box sur boutons."
                };
            }
            catch (Exception e) { return new { ok = false, message = "Nettoyage interrompu : " + (e.InnerException ?? e).Message }; }
        }

        static int Depth(Type type) { int depth = 0; for (; type != null; type = type.BaseType) depth++; return depth; }

        internal static object Member(object target, string name)
        {
            if (target == null) return null;
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, flags);
            if (field != null) return field.GetValue(target);
            return type.GetProperties(flags).Where(p => p.Name == name && p.GetIndexParameters().Length == 0)
                .OrderByDescending(p => Depth(p.DeclaringType)).FirstOrDefault()?.GetValue(target);
        }

        internal static void Set(object target, string name, object value)
        {
            var type = target.GetType();
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var field = type.GetField(name, flags);
            if (field != null) { field.SetValue(target, value); return; }
            type.GetProperties(flags).Where(p => p.Name == name && p.CanWrite).OrderByDescending(p => Depth(p.DeclaringType)).FirstOrDefault()?.SetValue(target, value);
        }
    }
}
