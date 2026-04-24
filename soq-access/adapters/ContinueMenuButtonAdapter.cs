using System;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class ContinueMenuButtonAdapter : MenuButtonAdapterBase
    {
        public ContinueMenuButtonAdapter(string id, UIButton button, Func<bool> isVisible = null)
            : base(id, button, isVisible, null)
        {
        }

        protected override string BuildLabel()
        {
            string completed = MenuButtonTextUtility.GetVisibleTextByNodeName(Button, "CompletedCampaignText");
            string title = MenuButtonTextUtility.GetVisibleTextByNodeName(Button, "Title");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = MenuButtonTextUtility.GetDirectButtonText(Button);
            }

            string description = MenuButtonTextUtility.GetVisibleTextByNodeName(Button, "Description");
            return MenuButtonTextUtility.JoinParts(completed, title, description);
        }
    }
}
