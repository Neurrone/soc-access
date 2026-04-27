namespace SongsOfConquestAccess.Input
{
    internal enum InputRepeatMode
    {
        // Fires once when the binding is pressed and will not fire again until
        // the physical key has been confirmed released.
        OneShotUntilRelease,

        // Future repeat policy for actions like list or map movement where
        // holding a key should repeat at an accessibility-friendly rate
        // controlled by this mod. It should not rely on Unity or OS raw
        // key-repeat events.
        // TimedRepeat
    }

    internal sealed class InputRepeatPolicy
    {
        private InputRepeatPolicy(InputRepeatMode mode)
        {
            Mode = mode;
        }

        public InputRepeatMode Mode { get; private set; }

        public static InputRepeatPolicy OneShotUntilRelease()
        {
            return new InputRepeatPolicy(InputRepeatMode.OneShotUntilRelease);
        }

        // Future TimedRepeat support should add timing fields here, for example:
        // public float InitialDelaySeconds { get; private set; }
        // public float IntervalSeconds { get; private set; }
        //
        // The fields above should only be used by TimedRepeat once that policy
        // is implemented. OneShotUntilRelease does not need timing data.
    }
}
