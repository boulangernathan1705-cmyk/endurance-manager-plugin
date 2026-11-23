// L'écran du plugin en page web dans SimHub : le contrôle WebView2 (le moteur d'Edge) que SimHub installe avec lui.
// Pris par réflexion : le plugin ne l'emporte pas et compile sans lui. Absent, ou s'il ne démarre pas, Shell (Guide.cs)
// remet l'écran WPF : le pilote a toujours un écran.
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;

namespace EnduranceManager.SimHub
{
    public static class WebScreen
    {
        const string Wpf = "Microsoft.Web.WebView2.Wpf";

        // The page at url in a WebView2, or null when SimHub has none. failed: it could not start (called on its thread).
        public static FrameworkElement Make(string url, Action failed)
        {
            try
            {
                var assembly = Find();
                var type = assembly?.GetType(Wpf + ".WebView2");
                var propertiesType = assembly?.GetType(Wpf + ".CoreWebView2CreationProperties");
                if (type == null || propertiesType == null) return null;
                var view = (FrameworkElement)Activator.CreateInstance(type);
                // Its own folder: never SimHub's, never the pilot's Edge.
                var properties = Activator.CreateInstance(propertiesType);
                string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnduranceManager", "ecran");
                Directory.CreateDirectory(folder);
                propertiesType.GetProperty("UserDataFolder")?.SetValue(properties, folder);
                type.GetProperty("CreationProperties")?.SetValue(view, properties);
                // The site's background while the page loads, not a white flash.
                var background = type.GetProperty("DefaultBackgroundColor");
                var fromArgb = background?.PropertyType.GetMethod("FromArgb", new[] { typeof(int), typeof(int), typeof(int), typeof(int) });
                if (fromArgb != null) background.SetValue(view, fromArgb.Invoke(null, new object[] { 255, 7, 9, 10 }));
                // Started or not: CoreWebView2InitializationCompleted, IsSuccess.
                var started = type.GetEvent("CoreWebView2InitializationCompleted");
                if (started != null)
                {
                    var argsType = started.EventHandlerType.GetGenericArguments().FirstOrDefault();
                    if (argsType != null)
                    {
                        var watch = new Watcher(failed);
                        var method = typeof(Watcher).GetMethod(nameof(Watcher.Done)).MakeGenericMethod(argsType);
                        started.AddEventHandler(view, Delegate.CreateDelegate(started.EventHandlerType, watch, method));
                    }
                }
                type.GetProperty("Source")?.SetValue(view, new Uri(url));
                return view;
            }
            catch (Exception e)
            {
                Report.Log("écran", "WebView2 : " + e.Message);
                return null;
            }
        }

        class Watcher
        {
            readonly Action failed;
            public Watcher(Action failed) { this.failed = failed; }
            public void Done<T>(object sender, T args)
            {
                bool ok = args?.GetType().GetProperty("IsSuccess")?.GetValue(args) as bool? ?? true;
                if (!ok)
                {
                    Report.Log("écran", "WebView2 n'a pas démarré : " + args.GetType().GetProperty("InitializationException")?.GetValue(args));
                    failed?.Invoke();
                }
            }
        }

        // Already loaded by SimHub, else next to SimHub.exe.
        static Assembly Find()
        {
            var loaded = AppDomain.CurrentDomain.GetAssemblies().FirstOrDefault(a => a.GetName().Name == Wpf);
            if (loaded != null) return loaded;
            string path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, Wpf + ".dll");
            if (!File.Exists(path)) return null;
            try { return Assembly.LoadFrom(path); } catch { return null; }
        }
    }
}
