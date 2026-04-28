using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class MoveTroopPopupRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.FieldInfo CurrentStateField =
            AccessTools.Field(typeof(TroopHUDEntryMovable), "_currentState");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            TroopHUDEntryMovable[] movables = Resources.FindObjectsOfTypeAll<TroopHUDEntryMovable>();
            for (int i = 0; i < movables.Length; i++)
            {
                TroopHUDEntryMovable movable = movables[i];
                if (!IsPresent(movable))
                {
                    continue;
                }

                MoveTroopPopupAdapter adapter = new MoveTroopPopupAdapter(movable);
                screens.Add(new MoveTroopPopupScreen(adapter));
            }
        }

        private static bool IsPresent(TroopHUDEntryMovable movable)
        {
            if (movable == null || !((Component)movable).gameObject.activeInHierarchy)
            {
                return false;
            }

            object value = CurrentStateField != null ? CurrentStateField.GetValue(movable) : null;
            return value != null && value.ToString() == "Deciding";
        }
    }
}
