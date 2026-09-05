using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Skills;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class LevelUpMenuPatches
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(CommanderLevelUpMenu), "_async");

        [HarmonyPatch(typeof(CommanderLevelUpMenu), "Open", new[]
        {
            typeof(ITeamState),
            typeof(ICommanderState),
            typeof(List<SkillReference>)
        })]
        [HarmonyPostfix]
        private static void CommanderLevelUpMenuOpenPostfix(CommanderLevelUpMenu __instance)
        {
            SocAccessMod.Instance?.ScreenDetector?.OnLevelUpMenuReady(__instance);
        }

        [HarmonyPatch(typeof(CommanderLevelUpMenu), "ConfirmSkill")]
        [HarmonyPrefix]
        private static void CommanderLevelUpMenuConfirmSkillPrefix(CommanderLevelUpMenu __instance)
        {
            NotifyClosingIfActive(__instance);
        }

        [HarmonyPatch(typeof(CommanderLevelUpMenu), "ForceClose")]
        [HarmonyPrefix]
        private static void CommanderLevelUpMenuForceClosePrefix(CommanderLevelUpMenu __instance)
        {
            NotifyClosingIfActive(__instance);
        }

        private static void NotifyClosingIfActive(CommanderLevelUpMenu menu)
        {
            if (menu != null && AsyncField != null && AsyncField.GetValue(menu) is Async<SkillReference?>)
            {
                SocAccessMod.Instance?.ScreenDetector?.OnLevelUpMenuClosed(menu);
            }
        }
    }
}
