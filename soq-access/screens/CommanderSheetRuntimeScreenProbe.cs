using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CommanderSheetRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            CommanderSheet[] sheets = Resources.FindObjectsOfTypeAll<CommanderSheet>();
            for (int i = 0; i < sheets.Length; i++)
            {
                CommanderSheet sheet = sheets[i];
                CommanderSheetAdapter adapter = new CommanderSheetAdapter(sheet);
                if (adapter.IsPresent())
                {
                    screens.Add(new CommanderSheetScreen(adapter));
                    return;
                }
            }
        }
    }
}
