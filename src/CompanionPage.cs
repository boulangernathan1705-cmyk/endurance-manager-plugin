// La page « Black box sur boutons » de l'écran du plugin (maquette validée par Nathan le 23/11/2026,
// maquettes/black-box-boutons/) : tout l'écran de FMT companion 0.5, refait dans la page web. FMT companion tourne tel
// quel (sa DLL, compilée depuis src/fmt/, Companion.cs) ; elle reste un assemblage à part, donc cette page lui parle par réflexion, avec exactement les
// appels que fait son propre écran (CompanionControl, src/fmt/) : cocher une commande (SetCommandVisible),
// Tester (Mapper.Test), Préparer LMU (Preparer.Prepare des commandes LMU cochées), Annuler (Preparer.Undo), ouvrir le
// Control Mapper, exporter le profil Stream Deck, sauvegarder le mapping, valider une nouvelle version de LMU. Rien de ce
// que fait FMT companion n'est réécrit. Si une version de FMT companion n'a plus ces noms, la page le dit et propose son
// ancien écran.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;

namespace EnduranceManager.SimHub
{
    public class CompanionPage
    {
        const BindingFlags Any = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;
        // FMT companion's kinds of command (its FunctionKind): the pit menu and LMU's API act at once; an LMU control
        // needs « Préparer LMU »; a keyboard combo is sent to LMU when it is in front.
        const int Pit = 0, LmuKey = 1, Api = 2, Combo = 3;

        readonly Companion companion;
        readonly object gate = new object();
        // LMU's controls (Preparer.Controls → LmuControlsModel.DirectInput), read again every few seconds, as its screen does.
        object directInput;
        string lmuError;
        DateTime readAt = DateTime.MinValue;
        int reading;
        // « Préparer LMU » / « Annuler »: its lines, shown under the button while it runs and after.
        readonly List<string> log = new List<string>();
        string summary;
        bool busy;
        string lastTest;

        public CompanionPage(Companion companion) { this.companion = companion; }

        // « Préparer LMU » seen from elsewhere (Repartir.cs, menu « Mes boutons ») : running, and its last line.
        public bool Preparing { get { lock (gate) return busy; } }
        public string PrepareSummary { get { lock (gate) return summary; } }

        object P => companion.Instance;

        // ── Reflection, kept to the names FMT companion 0.5 uses ────────────────────────────────────────────────────
        static object Get(object o, string name)
        {
            if (o == null) return null;
            var type = o as Type ?? o.GetType();
            var target = o is Type ? null : o;
            var field = type.GetField(name, Any);
            if (field != null) return field.GetValue(target);
            var property = type.GetProperty(name, Any);
            return property?.GetValue(target, null);
        }

        static object Call(object o, string name, params object[] args)
        {
            var type = o as Type ?? o.GetType();
            var method = type.GetMethods(Any).FirstOrDefault(m => m.Name == name && m.GetParameters().Length == args.Length)
                ?? throw new MissingMethodException(type.Name, name);
            return method.Invoke(o is Type ? null : o, args);
        }

        Type Kind(string name) => P.GetType().Assembly.GetType("FmtCompanion." + name, true);

        static string Text(object o) => o?.ToString() ?? "";

        IEnumerable<object> Functions() => ((IEnumerable)Get(Kind("Catalog"), "All")).Cast<object>();

        // Its keys of LMU are listed as « Key.TC.Next »: found with or without « Key. ».
        object Function(string id) => Functions().FirstOrDefault(f => Text(Get(f, "Id")) == id) ?? Functions().FirstOrDefault(f => Text(Get(f, "Id")) == "Key." + id)
            ?? throw new ArgumentException("Commande inconnue : " + id);

        // ── What the page shows ─────────────────────────────────────────────────────────────────────────────────────
        public object State()
        {
            if (P == null) return null;
            // The page shows the pit menu FMT companion read: kept fresh while it is open.
            companion.Touch();
            try
            {
                Read();
                object state = null;
                OnScreen(() => state = Build());
                return state;
            }
            catch (Exception e)
            {
                return new { error = "La page ne sait pas lire cette version de FMT companion : " + ((e as TargetInvocationException)?.InnerException ?? e).Message };
            }
        }

        object Build()
        {
            var p = P;
            var settings = Get(p, "Settings");
            var visible = new HashSet<string>(((IEnumerable)Get(settings, "VisibleCommands") ?? new string[0]).Cast<object>().Select(Text));
            var output = Get(p, "Output");
            var mapper = Get(p, "Mapper");
            var service = Get(p, "Service");
            var preparer = Get(p, "Preparer");
            bool bridge = output != null && (bool)Get(output, "Bridge");
            var deck = new HashSet<string>(((IEnumerable)SafeCall(p, "StreamDeckRoles") ?? new string[0]).Cast<object>().Select(Text));
            object controls; string error;
            lock (gate) { controls = directInput; error = lmuError; }
            var stateMethod = Kind("Preparer").GetMethod("State", Any);
            Func<int, string> roleOf = i => Text(Call(output, "OutputRole", i));

            var rows = new List<object>();
            foreach (var f in Functions())
            {
                string id = Text(Get(f, "Id")), role = Text(Get(f, "Role"));
                int kind = Convert.ToInt32(Get(f, "Kind"));
                var buttons = new List<string>();
                if (output != null)
                    foreach (var pair in ((IEnumerable)Call(output, "AssignedButtons", role) ?? new object[0]).Cast<object>())
                    {
                        string key = Text(Get(pair, "Key"));
                        int value = Convert.ToInt32(Get(pair, "Value"));
                        buttons.Add(key == "croix" || value <= 0 ? key : "B" + value);
                    }
                if (deck.Contains(role)) buttons.Add("Stream Deck");
                bool? ready = null;
                string lmu = null;
                if (kind == LmuKey && controls != null && output != null)
                {
                    var args = new object[] { controls, f, roleOf, bridge, false };
                    lmu = Text(stateMethod.Invoke(null, args));
                    ready = (bool)args[4];
                }
                rows.Add(new
                {
                    id,
                    family = Text(Get(f, "Family")),
                    group = Text(Get(f, "Group")),
                    label = Text(Get(f, "Label")),
                    kind,
                    role,
                    on = visible.Contains(id),
                    buttons,
                    ready,
                    lmu,
                });
            }

            var serviceState = Get(service, "State");
            bool connected = serviceState != null && (bool)Get(serviceState, "Connected");
            bool menu = serviceState != null && Get(serviceState, "Menu") != null;
            string readOnly = Text(Get(service, "ReadOnlyReason"));
            string notice = Text(Get(p, "UpdateNotice"));
            var checks = ((IEnumerable)SafeCall(p, "SelfCheck") ?? new object[0]).Cast<object>().Select(c => new { ok = (bool)Get(c, "Ok"), text = Text(Get(c, "Text")) }).ToList();
            List<string> lines;
            lock (gate) lines = log.ToList();
            return new
            {
                lmu = new
                {
                    connected,
                    menu,
                    readOnly = string.IsNullOrEmpty(readOnly) ? null : readOnly,
                    notice = string.IsNullOrEmpty(notice) ? null : notice,
                    version = Text(Get(p, "LmuVersion")),
                    controls = controls != null,
                    error,
                },
                writes = service != null && (bool)Get(service, "WritesEnabled"),
                mapper = new { ready = mapper != null && (bool)Get(mapper, "Ready"), roles = mapper == null ? 0 : Convert.ToInt32(Get(mapper, "RoleCount")), status = Text(mapper == null ? null : Get(mapper, "Status")) },
                output = new { usable = output != null && (bool)Get(output, "Usable"), bridge, name = output == null ? "" : Text(Call(output, "Describe")) },
                checks,
                rows,
                prepare = new { busy = busy || (preparer != null && (bool)Get(preparer, "Running")), summary, log = lines },
                test = lastTest,
                version = Text(Get(p, "Version")),
            };
        }

        static object SafeCall(object o, string name) { try { return Call(o, name); } catch { return null; } }

        // LMU's controls, read off the screen's thread (LMU's REST API can be slow or closed).
        void Read()
        {
            if (DateTime.UtcNow - readAt < TimeSpan.FromSeconds(4) || Interlocked.Exchange(ref reading, 1) == 1) return;
            readAt = DateTime.UtcNow;
            var preparer = Get(P, "Preparer");
            var model = Kind("LmuControlsModel");
            Task.Run(async () =>
            {
                try
                {
                    var task = (Task)Call(preparer, "Controls", CancellationToken.None);
                    await task.ConfigureAwait(false);
                    var all = task.GetType().GetProperty("Result").GetValue(task, null);
                    var di = Call(model, "DirectInput", all);
                    lock (gate) { directInput = di; lmuError = null; }
                }
                catch (Exception e)
                {
                    lock (gate) { directInput = null; lmuError = ((e as TargetInvocationException)?.InnerException ?? e).Message; }
                }
                finally { Volatile.Write(ref reading, 0); }
            });
        }

        // ── What the page asks ──────────────────────────────────────────────────────────────────────────────────────
        // The answer { ok, message } as the rest of the screen.
        public object Act(string what, string id, bool on)
        {
            if (P == null) return new { ok = false, message = companion.Status };
            object answer = null;
            switch (what)
            {
                case "bbCheck":
                    OnScreen(() => Call(P, "SetCommandVisible", id, on));
                    return new { ok = true, message = (string)null };
                case "bbTest": OnScreen(() => answer = Test(Function(id))); return answer;
                case "bbPrepare": return Prepare(false);
                case "bbUndo": return Prepare(true);
                case "bbMapper":
                    OnScreen(() => { var mapper = Get(P, "Mapper"); if (mapper != null) Call(mapper, "OpenMapper"); });
                    return new { ok = true, message = (string)null };
                case "bbDeck":
                    OnScreen(() => answer = new { ok = true, message = "Profil Stream Deck exporté : " + Text(Call(P, "ExportStreamDeckProfile")) });
                    return answer;
                case "bbBackup":
                    OnScreen(() => { Call(P, "BackupMapping"); answer = new { ok = true, message = "Mapping sauvegardé dans " + Text(Get(P.GetType(), "BackupRoot")) + "." }; });
                    return answer;
                case "bbGarage":
                    OnScreen(() => Call(P, "AcknowledgeLmuVersion"));
                    return new { ok = true, message = "Commandes du stand réactivées pour cette version de LMU." };
                case "bbWrites":
                    OnScreen(() => Call(Get(P, "Service"), "EnableWrites", on));
                    return new { ok = true, message = on ? "Commandes du stand actives." : "Commandes du stand suspendues." };
                default: return new { ok = false, message = "Inconnu : " + what };
            }
        }

        // Its « Tester » beside each command: what it says, word for word.
        object Test(object f)
        {
            var mapper = Get(P, "Mapper");
            string label = Text(Get(f, "Label"));
            if (mapper == null) return Said("Control Mapper pas encore prêt.", false);
            bool sent = (bool)Call(mapper, "Test", f);
            int kind = Convert.ToInt32(Get(f, "Kind"));
            if (kind == Combo)
                return Said(Text(Get(Get(P, "Combo"), "Status")) is string status && status != "" ? status : "Test « " + label + " » : retourne dans LMU en vue cockpit, envoi dès qu'il est au premier plan (10 s max).", sent);
            string end = !sent ? (kind == Pit || kind == Api ? "commande indisponible." : "rien envoyé, préparer LMU d'abord ou vérifier la sortie.")
                : kind == Pit || kind == Api ? "commande envoyée." : "impulsion envoyée par la sortie Control Mapper.";
            return Said("Test « " + label + " » : " + end, sent);
        }

        // One of FMT companion's commands by its id (« TC.Next »…), as its Tester does, for the wheel's black box
        // (BlackBoxPages.cs, page Électronique): null when sent, otherwise a short reason for the dash's title bar.
        public string Send(string id)
        {
            if (P == null) return "FMT companion absent";
            string why = null;
            try
            {
                OnScreen(() =>
                {
                    var mapper = Get(P, "Mapper");
                    if (mapper == null) { why = "Control Mapper pas prêt"; return; }
                    if (!(bool)Call(mapper, "Test", Function(id))) why = "Préparer LMU dans FMT";
                });
            }
            catch (Exception e) { why = Message(e); }
            return why;
        }

        object Said(string text, bool ok) { lastTest = text; return new { ok, message = text }; }

        // « Préparer LMU »: the LMU controls ticked (as its screen picks them); « Annuler »: what FMT added, only that.
        object Prepare(bool undo)
        {
            lock (gate)
            {
                if (busy) return new { ok = false, message = "Préparation en cours : attendre." };
                busy = true;
                log.Clear();
                summary = undo ? "Annulation…" : "Préparation… ne touche pas au volant pendant l'écoute de LMU.";
            }
            Task work = null;
            try
            {
                OnScreen(() =>
                {
                    var p = P;
                    var preparer = Get(p, "Preparer");
                    Action<string> line = text => { lock (gate) { log.Add(text); summary = text; } };
                    if (undo) { work = Task.Run(() => (Task)Call(preparer, "Undo", line, CancellationToken.None)); return; }
                    var mapper = Get(p, "Mapper");
                    if (mapper != null) Call(mapper, "RefreshRoles");
                    var visible = new HashSet<string>(((IEnumerable)Get(Get(p, "Settings"), "VisibleCommands")).Cast<object>().Select(Text));
                    var wanted = Functions().Where(f => Convert.ToInt32(Get(f, "Kind")) == LmuKey && visible.Contains(Text(Get(f, "Id")))).ToList();
                    // Its Prepare takes IList<FmtFunction>: a list of that very type.
                    var list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(Kind("FmtFunction")));
                    foreach (var f in wanted) list.Add(f);
                    work = Task.Run(() => (Task)Call(preparer, "Prepare", list, line, CancellationToken.None));
                });
            }
            catch (Exception e) { lock (gate) { busy = false; summary = Message(e); } return new { ok = false, message = summary }; }
            work?.ContinueWith(t =>
            {
                lock (gate)
                {
                    busy = false;
                    if (t.IsFaulted) summary = (undo ? "Annulation interrompue : " : "Préparation interrompue : ") + Message(t.Exception);
                }
                readAt = DateTime.MinValue;
            });
            return new { ok = true, message = (string)null };
        }

        static string Message(Exception e)
        {
            while (e is AggregateException || e is TargetInvocationException) { if (e.InnerException == null) break; e = e.InnerException; }
            return e.Message;
        }

        static void OnScreen(Action action)
        {
            var dispatcher = Application.Current?.Dispatcher;
            if (dispatcher == null || dispatcher.CheckAccess()) action();
            else dispatcher.Invoke(action);
        }
    }
}
