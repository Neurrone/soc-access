using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MapMessagePopupRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            MapMessagePopup[] popups = Resources.FindObjectsOfTypeAll<MapMessagePopup>();
            for (int i = 0; i < popups.Length; i++)
            {
                MapMessagePopup popup = popups[i];
                if (!IsLiveScenePopup(popup))
                {
                    continue;
                }

                MapMessagePopupAdapter adapter = new MapMessagePopupAdapter(popup);
                if (adapter.IsPresent())
                {
                    screens.Add(new MessageDialogScreen(adapter));
                    return;
                }
            }
        }

        private static bool IsLiveScenePopup(MapMessagePopup popup)
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
