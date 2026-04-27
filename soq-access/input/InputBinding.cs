namespace SongsOfConquestAccess.Input
{
    internal abstract class InputBinding
    {
        // Stable identifier for this physical binding, derived from binding data
        // and used only for active/release tracking. It is not user-facing.
        public abstract string Id { get; }
    }
}
