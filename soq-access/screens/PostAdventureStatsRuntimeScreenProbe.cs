using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostAdventureStatsRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly FieldInfo StatsMenuField = AccessTools.Field(typeof(PostAdventureMenu), "_statsMenu");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            PostAdventureMenu[] resultMenus = Resources.FindObjectsOfTypeAll<PostAdventureMenu>();
            for (int i = 0; i < resultMenus.Length; i++)
            {
                PostAdventureStatsMenu statsMenu = GetStatsMenu(resultMenus[i]);
                PostAdventureStatsAdapter adapter = new PostAdventureStatsAdapter(statsMenu);
                if (adapter.IsPresent())
                {
                    screens.Add(new PostAdventureStatsScreen(adapter));
                }
            }
        }

        private static PostAdventureStatsMenu GetStatsMenu(PostAdventureMenu resultMenu)
        {
            return resultMenu != null && StatsMenuField != null
                ? StatsMenuField.GetValue(resultMenu) as PostAdventureStatsMenu
                : null;
        }
    }
}
