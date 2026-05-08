using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ConfirmPopupRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            ConfirmPopup[] popups = Resources.FindObjectsOfTypeAll<ConfirmPopup>();
            ConfirmPopupAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < popups.Length; i++)
            {
                ConfirmPopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                ConfirmPopupAdapter adapter = new ConfirmPopupAdapter(popup);
                if (!adapter.IsPresent())
                {
                    continue;
                }

                int siblingIndex = popup.transform != null ? popup.transform.GetSiblingIndex() : 0;
                if (bestAdapter == null || siblingIndex > bestSiblingIndex)
                {
                    bestAdapter = adapter;
                    bestSiblingIndex = siblingIndex;
                }
            }

            if (bestAdapter != null)
            {
                screens.Add(new MessageDialogScreen(bestAdapter));
            }
        }

        private static bool IsLiveScenePopup(ConfirmPopup popup)
        {
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
