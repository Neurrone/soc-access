using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Common.Battle;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class PostBattleResultScreen : Screen
    {
        private static readonly System.Reflection.FieldInfo PostBattleMenuResultField =
            AccessTools.Field(typeof(PostBattleMenu), "_result");
        private static readonly System.Reflection.FieldInfo PostBattleMenuOnHideField =
            AccessTools.Field(typeof(PostBattleMenu), "OnHidePostBattle");

        private readonly PostBattleResultAdapter _adapter;

        public PostBattleResultScreen(PostBattleResultAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            return FindActivePostBattleResultScreen();
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public PostBattleResultScreen Rebuild()
        {
            return new PostBattleResultScreen(_adapter);
        }

        private static PostBattleResultScreen FindActivePostBattleResultScreen()
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

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        private static ContainerWidget BuildRoot(PostBattleResultAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("post-battle-result", ModText.Get(ModStrings.Screens.BattleResult));
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "post-battle-result-header",
                () => adapter.HeaderText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            CommanderHudPortraitAdapter attackerPortrait = adapter.AttackerCommanderPortrait;
            root.AddChild(Portrait.Static(
                "post-battle-attacker-commander",
                () => attackerPortrait != null ? attackerPortrait.Name : adapter.AttackerCommanderText,
                () =>
                {
                    if (attackerPortrait != null)
                    {
                        attackerPortrait.Focus();
                    }
                    else
                    {
                        adapter.HideNativeTooltip();
                    }
                },
                () => BuildPortraitTooltip(attackerPortrait)));

            if (adapter.XpBelongsToAttacker)
            {
                AddXpWidget(root, adapter);
            }

            AddEntryMenu(root, "post-battle-attacker-troops-lost", adapter.AttackerCommanderText, adapter.AttackerTroopsLost, adapter, addNoneWhenEmpty: true);

            root.AddChild(new TextWidget(
                "post-battle-attacker-returned-troops",
                () => adapter.AttackerReturnedTroopsText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.AttackerReturnedTroopsVisible));

            CommanderHudPortraitAdapter defenderPortrait = adapter.DefenderCommanderPortrait;
            root.AddChild(Portrait.Static(
                "post-battle-defender-commander",
                () => defenderPortrait != null ? defenderPortrait.Name : adapter.DefenderCommanderText,
                () =>
                {
                    if (defenderPortrait != null)
                    {
                        defenderPortrait.Focus();
                    }
                    else
                    {
                        adapter.HideNativeTooltip();
                    }
                },
                () => BuildPortraitTooltip(defenderPortrait)));

            if (!adapter.XpBelongsToAttacker)
            {
                AddXpWidget(root, adapter);
            }

            AddEntryMenu(root, "post-battle-defender-troops-lost", adapter.DefenderCommanderText, adapter.DefenderTroopsLost, adapter, addNoneWhenEmpty: true);

            root.AddChild(new TextWidget(
                "post-battle-defender-returned-troops",
                () => adapter.DefenderReturnedTroopsText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.DefenderReturnedTroopsVisible));

            AddEntryMenu(root, "post-battle-loot", GameText.Get("Adventure/AdventurePostBattleMenu/BattleLoot", string.Empty), adapter.Loot, adapter, addNoneWhenEmpty: false);

            root.AddChild(new ButtonWidget(
                "post-battle-accept",
                adapter.AcceptButtonLabel,
                adapter.Accept,
                adapter.HideNativeTooltip,
                adapter.IsAcceptButtonEnabled,
                adapter.IsAcceptButtonVisible,
                adapter.AcceptButtonTooltip));

            root.AddChild(new ButtonWidget(
                "post-battle-redo-manual-battle",
                adapter.RedoManualBattleButtonLabel,
                adapter.RedoManualBattle,
                adapter.HideNativeTooltip,
                adapter.IsRedoManualBattleButtonEnabled,
                adapter.IsRedoManualBattleButtonVisible,
                adapter.RedoManualBattleButtonTooltip));
            return root;
        }

        private static void AddXpWidget(ContainerWidget root, PostBattleResultAdapter adapter)
        {
            root.AddChild(new TextWidget(
                "post-battle-xp",
                () => adapter.XpText,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.XpVisible));
        }

        private static Tooltip BuildPortraitTooltip(CommanderHudPortraitAdapter portrait)
        {
            return portrait != null
                ? Portrait.BuildNativeTooltip(
                    () => portrait.TooltipTarget,
                    portrait.Localization,
                    portrait.RefreshTooltip)
                : null;
        }

        private static void AddEntryMenu(
            ContainerWidget root,
            string id,
            string label,
            IReadOnlyList<PostBattleResultAdapter.ResultEntry> entries,
            PostBattleResultAdapter adapter,
            bool addNoneWhenEmpty)
        {
            if (root == null)
            {
                return;
            }

            MenuWidget menu = new MenuWidget(id, label);
            if (entries == null || entries.Count == 0)
            {
                if (!addNoneWhenEmpty)
                {
                    return;
                }

                menu.AddItem(new MenuItemWidget(
                    id + "-none",
                    () => ModText.Get(ModStrings.Screens.None),
                    getStatus: null,
                    activate: null,
                    onFocus: adapter.HideNativeTooltip,
                    isVisible: () => true));
                root.AddChild(menu);
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PostBattleResultAdapter.ResultEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    id + "-" + i,
                    () => BuildResultEntryLabel(entry),
                    getStatus: null,
                    activate: null,
                    onFocus: adapter.HideNativeTooltip,
                    isVisible: () => entry.IsVisible,
                    tooltip: entry.Tooltip));
            }

            root.AddChild(menu);
        }

        private static string BuildResultEntryLabel(PostBattleResultAdapter.ResultEntry entry)
        {
            if (entry == null)
            {
                return string.Empty;
            }

            if (!entry.IsLostTroop)
            {
                return entry.Name;
            }

            string label;
            if (string.IsNullOrWhiteSpace(entry.Amount))
            {
                label = entry.Name;
            }
            else if (string.IsNullOrWhiteSpace(entry.Name))
            {
                label = entry.Amount;
            }
            else
            {
                label = entry.Amount + " " + entry.Name;
            }

            return string.IsNullOrWhiteSpace(label) ? string.Empty : ModText.Get(ModStrings.Screens.TroopLost, label);
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
