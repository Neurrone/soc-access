namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The wording of a key-binding row's chip - the one piece of the Controls table that is pure
    /// text and so is decided here rather than in the Unity-bound sheet: the hotkey the game draws,
    /// or the mod's "not bound" where the game draws an empty chip.
    /// </summary>
    public static class KeyBindingText
    {
        /// <param name="binding">The hotkey the game's chip draws, or blank when it draws none.</param>
        /// <param name="notBound">The localized "not bound" wording to speak for an empty chip.</param>
        public static string Display(string binding, string notBound)
        {
            return string.IsNullOrWhiteSpace(binding) ? notBound : binding;
        }
    }
}
