using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SpellbookRuntimeScreenProbe : IRuntimeScreenProbe
    {
        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            SpellBook[] spellbooks = Resources.FindObjectsOfTypeAll<SpellBook>();
            for (int i = 0; i < spellbooks.Length; i++)
            {
                SpellBook spellbook = spellbooks[i];
                if (spellbook == null)
                {
                    continue;
                }

                SpellbookAdapter adapter = new SpellbookAdapter(spellbook);
                if (adapter.IsPresent())
                {
                    screens.Add(new SpellbookScreen(adapter));
                    return;
                }
            }
        }
    }
}
