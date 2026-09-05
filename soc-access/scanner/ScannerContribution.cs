using System;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Runs one adapter's contribution to a scanner snapshot in isolation. A
    /// contribution that throws costs its own category, not the whole scanner.
    /// </summary>
    public static class ScannerContribution
    {
        public static void Run(string name, Action contribute)
        {
            if (contribute == null)
            {
                return;
            }

            try
            {
                contribute();
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning(
                    "Scanner contribution '" + name + "' failed and was skipped: " + exception);
            }
        }
    }
}
