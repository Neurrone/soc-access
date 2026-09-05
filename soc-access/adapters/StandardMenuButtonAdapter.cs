using System;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    public class StandardMenuButtonAdapter : MenuButtonAdapterBase
    {
        public StandardMenuButtonAdapter(
            UIButton button,
            Func<bool> isVisible = null,
            Func<bool> activate = null)
            : base(button, isVisible, activate)
        {
        }

        protected override string BuildLabel()
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(Button);
        }
    }
}
