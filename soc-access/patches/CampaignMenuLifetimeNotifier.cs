using SongsOfConquest.Client.Menu;
using UnityEngine;
using System.Collections.Generic;

namespace SongsOfConquestAccess
{
    internal sealed class CampaignMenuLifetimeNotifier : MonoBehaviour
    {
        private CampaignMenu _campaignMenu;
        private static readonly List<CampaignMenuLifetimeNotifier> Instances =
            new List<CampaignMenuLifetimeNotifier>();

        internal static void DetachAll()
        {
            foreach (CampaignMenuLifetimeNotifier notifier in Instances.ToArray())
            {
                if (notifier == null) continue;
                notifier._campaignMenu = null;
                Destroy(notifier);
            }
            Instances.Clear();
        }

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
            Instances.Add(notifier);
        }

        private void OnDestroy()
        {
            Instances.Remove(this);
            if (_campaignMenu != null)
                SocAccessMod.Instance?.ScreenDetector?.OnCampaignMenuClosed(_campaignMenu);
        }
    }
}
