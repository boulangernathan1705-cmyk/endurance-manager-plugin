using System;

namespace EnduranceManager.SimHub
{
    // FMT returns null when a command succeeds; only a missing sender is an absent companion.
    public static class ProfileCommand
    {
        public static string Send(Func<string, string> sender, string id)
            => sender == null ? "FMT companion absent" : sender(id);
    }
}
