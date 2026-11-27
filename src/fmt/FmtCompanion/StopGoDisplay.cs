using System.Text.RegularExpressions;

namespace FmtCompanion
{
    // The selected STOP/GO menu choice contains the game's countdown, not the duration of the stop.
    public static class StopGoDisplay
    {
        public static int Laps(string value)
        {
            Match m = Regex.Match(value ?? "", @"^\s*(?:OUI|NON|YES|NO)\s*\(\s*(\d+)\s*(?:(?:laps?|tours?)(?:\s+(?:remaining|left|restants?))?)?\s*\)\s*$", RegexOptions.IgnoreCase);
            return m.Success && int.TryParse(m.Groups[1].Value, out int n) && n >= 0 && n <= 3 ? n : -1;
        }
    }
}
