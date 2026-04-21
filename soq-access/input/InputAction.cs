namespace SongsOfConquestAccess.Input
{
    internal sealed class InputAction
    {
        public InputAction(string key, string label)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
        }

        public string Key { get; private set; }

        public string Label { get; private set; }
    }
}
