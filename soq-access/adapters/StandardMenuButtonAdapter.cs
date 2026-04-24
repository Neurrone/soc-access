using System;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal class StandardMenuButtonAdapter : MenuButtonAdapterBase
    {
        public StandardMenuButtonAdapter(
            string id,
            UIButton button,
            Func<bool> isVisible = null,
            Func<bool> activate = null)
            : base(id, button, isVisible, activate)
        {
        }

        protected override string BuildLabel()
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(Button);
        }
    }
}
