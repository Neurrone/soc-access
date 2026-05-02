using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TaleSelectRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            TaleSelectAdapter adapter = FindActiveTaleSelect();
            if (adapter != null)
            {
                screens.Add(new TaleSelectScreen(adapter));
            }
        }

        public static TaleSelectAdapter FindActiveTaleSelect()
        {
            TaleButtonLayoutCoordinator[] coordinators = Resources.FindObjectsOfTypeAll<TaleButtonLayoutCoordinator>();
            for (int i = 0; i < coordinators.Length; i++)
            {
                TaleButtonLayoutCoordinator coordinator = coordinators[i];
                if (!IsLiveSceneCoordinator(coordinator))
                {
                    continue;
                }

                TaleSelectAdapter adapter = new TaleSelectAdapter(coordinator);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneCoordinator(TaleButtonLayoutCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)coordinator).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
