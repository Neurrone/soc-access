namespace SongsOfConquestAccess.Input
{
    internal sealed class InputAction
    {
        private readonly System.Collections.Generic.List<InputBinding> _bindings =
            new System.Collections.Generic.List<InputBinding>();

        public InputAction(string key, string label)
            : this(key, label, InputRepeatPolicy.OneShotUntilRelease())
        {
        }

        public InputAction(string key, string label, InputRepeatPolicy repeatPolicy)
        {
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            RepeatPolicy = repeatPolicy ?? InputRepeatPolicy.OneShotUntilRelease();
        }

        public string Key { get; private set; }

        public string Label { get; private set; }

        public InputRepeatPolicy RepeatPolicy { get; private set; }

        public System.Collections.Generic.IReadOnlyList<InputBinding> Bindings
        {
            get { return _bindings; }
        }

        public InputAction AddBinding(InputBinding binding)
        {
            if (binding != null)
            {
                _bindings.Add(binding);
            }

            return this;
        }
    }
}
