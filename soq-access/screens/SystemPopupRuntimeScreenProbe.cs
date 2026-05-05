using System.Collections.Generic;
using SongsOfConquest.Client;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SystemPopupRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            SystemPopup[] popups = Resources.FindObjectsOfTypeAll<SystemPopup>();
            SystemPopupAdapter bestAdapter = null;
            int bestSiblingIndex = int.MinValue;

            for (int i = 0; i < popups.Length; i++)
            {
                SystemPopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                SystemPopupAdapter adapter = new SystemPopupAdapter(popup);
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
                screens.Add(new QuestionDialogScreen(bestAdapter));
            }
        }

        private static bool IsLiveScenePopup(SystemPopup popup)
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
