using System.Collections.Generic;

namespace SongsOfConquestAccess.Input
{
    public enum InputClaimScope
    {
        FocusedWidget,
        Screen
    }

    public sealed class InputAction
    {
        // The compiled-in bindings, built once by AddBinding as the action is constructed at static
        // init and never mutated after. A player override REPLACES them for matching without touching
        // them, so the default is always here to restore. Statics re-init on every hot reload, so this
        // is the compiled default again each load and ModSettings re-applies any saved override.
        private readonly List<InputBinding> _defaults = new List<InputBinding>();

        // Null while the action is on its compiled-in default; a player-chosen set otherwise (an empty
        // list is a valid override - the action is then unbound - so null and empty are distinct).
        private List<InputBinding> _override;

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

        /// <summary>The effective bindings the router matches against every keydown: the player's
        /// override where one is set, otherwise the compiled-in defaults. The router reads this live,
        /// so a rebind needs no change there.</summary>
        public IReadOnlyList<InputBinding> Bindings
        {
            get { return _override ?? (IReadOnlyList<InputBinding>)_defaults; }
        }

        /// <summary>The compiled-in defaults, whatever override is in force - what
        /// <see cref="ResetToDefault"/> restores and what a persisted override is layered over.</summary>
        public IReadOnlyList<InputBinding> Defaults
        {
            get { return _defaults; }
        }

        /// <summary>Whether a player override is in force rather than the compiled-in default.</summary>
        public bool HasOverride
        {
            get { return _override != null; }
        }

        public InputAction AddBinding(InputBinding binding)
        {
            if (binding != null)
            {
                _defaults.Add(binding);
            }

            return this;
        }

        /// <summary>Replace the effective bindings with a player-chosen set (copied). null clears the
        /// override back to the default; an empty list is a genuine "unbound" override.</summary>
        public void SetOverride(IList<InputBinding> bindings)
        {
            _override = bindings == null ? null : new List<InputBinding>(bindings);
        }

        /// <summary>Drop any override, returning the action to its compiled-in default.</summary>
        public void ClearOverride()
        {
            _override = null;
        }

        /// <summary>The same as <see cref="ClearOverride"/>, named for the intent a row's clear or a
        /// reset-all expresses.</summary>
        public void ResetToDefault()
        {
            _override = null;
        }
    }
}
