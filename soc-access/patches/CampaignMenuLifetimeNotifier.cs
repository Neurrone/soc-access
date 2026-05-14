using SongsOfConquest.Client.Menu;
using UnityEngine;

namespace SongsOfConquestAccess
{
    internal sealed class CampaignMenuLifetimeNotifier : MonoBehaviour
    {
        private CampaignMenu _campaignMenu;

        public static void Attach(CampaignMenu campaignMenu)
        {
            if (campaignMenu == null)
            {
                return;
            }

            GameObject gameObject = campaignMenu.gameObject;
            if (gameObject == null || gameObject.GetComponent<CampaignMenuLifetimeNotifier>() != null)
            {
                return;
            }

            CampaignMenuLifetimeNotifier notifier = gameObject.AddComponent<CampaignMenuLifetimeNotifier>();
            notifier._campaignMenu = campaignMenu;
        }

        private void OnDestroy()
        {
            SocAccessPlugin.Instance?.ScreenDetector?.OnCampaignMenuClosed(_campaignMenu);
        }
    }
}
