namespace SongsOfConquestAccess.Adapters
{
    internal enum MenuButtonFocusMode
    {
        NativeAndSemantic,

        // Hover-driven submenus should not move native selection on accessibility focus,
        // or the game can immediately reopen/close the submenu underneath us.
        SemanticOnly
    }
}
