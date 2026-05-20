namespace SongsOfConquestAccess.Input
{
    internal enum InputClaimScope
    {
        FocusedWidget,
        Screen
    }

    internal sealed class InputAction
    {
        private readonly System.Collections.Generic.List<InputBinding> _bindings =
            new System.Collections.Generic.List<InputBinding>();
        private readonly System.Func<string> _getLabel;

        public InputAction(string key, string label, InputClaimScope claimScope)
            : this(key, label, claimScope, InputRepeatPolicy.OneShotUntilRelease())
        {
        }

        public InputAction(string key, string label, InputClaimScope claimScope, InputRepeatPolicy repeatPolicy)
            : this(key, () => label ?? string.Empty, claimScope, repeatPolicy)
        {
        }

        public InputAction(string key, System.Func<string> getLabel, InputClaimScope claimScope, InputRepeatPolicy repeatPolicy)
        {
            Key = key ?? string.Empty;
            _getLabel = getLabel ?? (() => string.Empty);
            ClaimScope = claimScope;
            RepeatPolicy = repeatPolicy ?? InputRepeatPolicy.OneShotUntilRelease();
        }

        public string Key { get; private set; }

        public string Label
        {
            get { return _getLabel(); }
        }

        public InputClaimScope ClaimScope { get; private set; }

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
