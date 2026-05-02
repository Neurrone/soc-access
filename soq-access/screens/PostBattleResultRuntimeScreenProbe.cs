using System.Collections.Generic;
using System;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Battle;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PostBattleResultRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly System.Reflection.FieldInfo PostBattleMenuResultField =
            AccessTools.Field(typeof(PostBattleMenu), "_result");
        private static readonly System.Reflection.FieldInfo PostBattleMenuOnHideField =
            AccessTools.Field(typeof(PostBattleMenu), "OnHidePostBattle");

        public void AddActiveScreens(List<Screen> screens)
        {
            if (screens == null)
            {
                return;
            }

            PostBattleResultScreen screen = FindActivePostBattleResultScreen();
            if (screen != null)
            {
                screens.Add(screen);
            }
        }

        public static PostBattleResultScreen FindActivePostBattleResultScreen()
        {
            PostBattleMenu menu = FindActivePostBattleMenu();
            if (!IsActive(menu) || GetResult(menu) == null)
            {
                return null;
            }

            AdventureBattleMenu battleMenu = ResolveOwningBattleMenu(menu);
            PostBattleResultAdapter adapter = new PostBattleResultAdapter(battleMenu, menu);
            return adapter.IsPresent() ? new PostBattleResultScreen(adapter) : null;
        }

        private static PostBattleMenu FindActivePostBattleMenu()
        {
            PostBattleMenu[] menus = Resources.FindObjectsOfTypeAll<PostBattleMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                if (IsActive(menus[i]) && GetResult(menus[i]) != null)
                {
                    return menus[i];
                }
            }

            return null;
        }

        private static IBattleResult GetResult(PostBattleMenu menu)
        {
            return menu != null && PostBattleMenuResultField != null
                ? PostBattleMenuResultField.GetValue(menu) as IBattleResult
                : null;
        }

        private static AdventureBattleMenu ResolveOwningBattleMenu(PostBattleMenu menu)
        {
            Action<PostBattleMenu.HideAction> onHidePostBattle = menu != null && PostBattleMenuOnHideField != null
                ? PostBattleMenuOnHideField.GetValue(menu) as Action<PostBattleMenu.HideAction>
                : null;
            if (onHidePostBattle == null)
            {
                return null;
            }

            Delegate[] invocationList = onHidePostBattle.GetInvocationList();
            for (int i = 0; i < invocationList.Length; i++)
            {
                AdventureBattleMenu battleMenu = invocationList[i]?.Target as AdventureBattleMenu;
                if (battleMenu != null)
                {
                    return battleMenu;
                }
            }

            return null;
        }

        private static bool IsActive(PostBattleMenu menu)
        {
            return menu != null
                && menu.gameObject != null
                && menu.gameObject.activeInHierarchy;
        }
    }
}
