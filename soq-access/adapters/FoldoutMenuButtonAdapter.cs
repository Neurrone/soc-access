using System;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class FoldoutMenuButtonAdapter : StandardMenuButtonAdapter
    {
        public FoldoutMenuButtonAdapter(string id, UIButton button, Func<bool> isVisible, Func<bool> activate)
            : base(id, button, isVisible, activate, MenuButtonFocusMode.SemanticOnly)
        {
        }
    }
}
