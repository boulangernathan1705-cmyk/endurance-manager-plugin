// Un écran qui lit des propriétés du plugin (dash, overlay, mur) : ce qu'il affiche n'est calculé que s'il les a lues
// depuis peu. Fermé, rien n'est calculé pour lui ; rouvert, ses chiffres reviennent au calcul suivant.
using System;
using System.Threading;

namespace EnduranceManager.SimHub
{
    public class Watched
    {
        // How long a screen counts as open after its last read: SimHub reads a shown dash many times a second.
        const int Keep = 3000;
        int at = Environment.TickCount - Keep - 1;

        // Called by each property of the screen, on SimHub's thread: one clock read, nothing else.
        public void Seen() => Volatile.Write(ref at, Environment.TickCount);

        public bool Now => unchecked(Environment.TickCount - Volatile.Read(ref at)) < Keep;

        // A property read through this watch.
        public Func<object> Wrap(Func<object> get) => () => { Seen(); return get(); };
    }
}
