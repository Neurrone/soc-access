namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>Stepping round a ring of things, where walking off one end arrives at the other.
    /// </summary>
    public static class Cycle
    {
        /// <summary>
        /// The place in a ring of <paramref name="length"/> that <paramref name="value"/> names.
        ///
        /// A positive modulo, which is the whole point: C#'s <c>%</c> keeps the sign of its left side,
        /// so a step back off the front of a list answers a NEGATIVE index and an unguarded caller
        /// throws instead of wrapping. Every ring in the mod - the scanner's scopes, its instances, a
        /// search's results, a compass arc - asks this same question, and asking it in one place is
        /// what stops one of them wrapping differently from the rest.
        ///
        /// A ring of nothing answers 0: there is no place to be, and 0 is what an empty list's caller
        /// tests for anyway.
        /// </summary>
        public static int Wrap(int value, int length)
        {
            return length <= 0 ? 0 : ((value % length) + length) % length;
        }
    }
}
