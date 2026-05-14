namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerDirectionStep
    {
        public ScannerDirectionStep(int count, string direction)
        {
            Count = count;
            Direction = direction;
        }

        public int Count { get; private set; }

        public string Direction { get; private set; }
    }
}
