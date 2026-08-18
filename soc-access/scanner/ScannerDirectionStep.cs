namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// One run of identical steps along a path. The direction stays a value
    /// rather than resolved text so the short and long spoken forms are chosen
    /// at speech time, from the current setting and language.
    /// </summary>
    internal sealed class ScannerDirectionStep
    {
        public ScannerDirectionStep(int count, ScannerDirection direction)
        {
            Count = count;
            Direction = direction;
        }

        public int Count { get; private set; }

        public ScannerDirection Direction { get; private set; }
    }
}
