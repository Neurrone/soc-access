using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    public sealed class HostileJoinMenuScreen : Screen
    {
        private const int ArmyExchangeGridIndex = 2;
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(HostileJoinMenuInstaller), "Container");

        private readonly HostileJoinMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;
        private HostileJoinMenuStage _stage;

        public HostileJoinMenuScreen(HostileJoinMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
            _stage = adapter != null ? adapter.Stage : HostileJoinMenuStage.None;
        }

        public static Screen TryBuildActiveScreen()
        {
            HostileJoinMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<HostileJoinMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                HostileJoinMenu menu = TryResolveHostileJoinMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new HostileJoinMenuScreen(adapter);
                }

                adapter.Dispose();
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            AttachListeners();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            DetachListeners();
            _adapter?.Dispose();
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            HostileJoinMenuStage currentStage = _adapter.Stage;
            bool stageChanged = currentStage != _stage;
            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            ArmyExchangeGridWidget.FocusState gridFocus = CaptureArmyGridFocus();

            RootWidget = BuildRoot(_adapter);
            _stage = currentStage;
            if (stageChanged)
            {
                RootWidget?.SetFocusByIndex(0);
                return;
            }

            RestoreArmyGridFocus(gridFocus);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private void AttachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null)
            {
                return;
            }

            _troopsUpdatedHandler = HandleTroopsUpdated;
            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Combine(
                commands.OnTroopsUpdated,
                _troopsUpdatedHandler);
        }

        private void DetachListeners()
        {
            if (_adapter == null || _adapter.Facade == null || _adapter.Facade.Commands == null || _troopsUpdatedHandler == null)
            {
                return;
            }

            IClientCommandsFacade commands = _adapter.Facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Remove(
                commands.OnTroopsUpdated,
                _troopsUpdatedHandler);
            _troopsUpdatedHandler = null;
        }

        private void HandleTroopsUpdated(OnTroopsUpdatedPayload payload)
        {
            if (payload == null || _adapter == null)
            {
                return;
            }

            if (payload.ParentId != _adapter.AttackingCommanderId && payload.ParentId != _adapter.JoiningCommanderId)
            {
                return;
            }

            Refresh();
        }

        private ArmyExchangeGridWidget.FocusState CaptureArmyGridFocus()
        {
            if (_stage != HostileJoinMenuStage.Join)
            {
                return null;
            }

            ArmyExchangeGridWidget grid = RootWidget != null
                ? RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget
                : null;
            return grid != null ? grid.CaptureFocusState() : null;
        }

        private void RestoreArmyGridFocus(ArmyExchangeGridWidget.FocusState focus)
        {
            if (focus == null || RootWidget == null || _stage != HostileJoinMenuStage.Join)
            {
                return;
            }

            ArmyExchangeGridWidget grid = RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget;
            grid?.RestoreFocusState(focus);
        }

        private static ContainerWidget BuildRoot(HostileJoinMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("hostile-join-menu", adapter != null ? adapter.Title : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            if (adapter.Stage == HostileJoinMenuStage.Choice)
            {
                BuildChoiceRoot(root, adapter);
                return root;
            }

            BuildJoinRoot(root, adapter);
            return root;
        }

        private static void BuildChoiceRoot(ContainerWidget root, HostileJoinMenuAdapter adapter)
        {
            root.AddChild(new TextWidget(
                "hostile-join-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(TroopHudMenu.Build(
                "hostile-join-wielder-troops",
                BuildWielderArmyLabel(adapter),
                adapter.WielderTroops,
                () => true,
                readOnly: true));

            root.AddChild(TroopHudMenu.Build(
                "hostile-join-joining-troops",
                ModText.Get(ModStrings.UI.JoiningArmy),
                adapter.JoiningTroops,
                () => true,
                readOnly: true));

            root.AddChild(new TextWidget(
                "hostile-join-choice-body",
                () => adapter.ChoiceBody,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new ButtonWidget(
                "hostile-join-reject",
                () => adapter.RejectLabel,
                adapter.ActivateReject,
                adapter.FocusReject,
                adapter.IsRejectEnabled));

            root.AddChild(new ButtonWidget(
                "hostile-join-accept",
                () => adapter.AcceptLabel,
                adapter.ActivateAccept,
                adapter.FocusAccept,
                adapter.IsAcceptEnabled));
        }

        private static void BuildJoinRoot(ContainerWidget root, HostileJoinMenuAdapter adapter)
        {
            root.AddChild(new TextWidget(
                "hostile-join-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "hostile-join-instructions",
                () => adapter.Instructions,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildArmyExchangeGrid(
                "hostile-join-army-exchange-grid",
                BuildWielderArmyLabel(adapter),
                ModText.Get(ModStrings.UI.JoiningArmy),
                adapter.WielderTroops,
                adapter.JoiningTroops));

            root.AddChild(new ButtonWidget(
                "hostile-join-discard",
                adapter.DiscardLabel,
                adapter.ActivateDiscard,
                adapter.HideNativeTooltip,
                adapter.IsDiscardEnabled));

            root.AddChild(new ButtonWidget(
                "hostile-join-mass-move",
                adapter.MassMoveLabel,
                adapter.ActivateMassMove,
                adapter.FocusMassMove,
                adapter.IsMassMoveEnabled,
                tooltip: adapter.MassMoveTooltip));
        }

        private static ArmyExchangeGridWidget BuildArmyExchangeGrid(
            string id,
            string leftArmyLabel,
            string rightArmyLabel,
            TroopHudAdapter left,
            TroopHudAdapter right)
        {
            System.Collections.Generic.IReadOnlyList<TroopHudAdapter.SlotItem> leftSlots = left != null
                ? left.GetSlots()
                : new TroopHudAdapter.SlotItem[0];
            System.Collections.Generic.IReadOnlyList<TroopHudAdapter.SlotItem> rightSlots = right != null
                ? right.GetSlots()
                : new TroopHudAdapter.SlotItem[0];
            return new ArmyExchangeGridWidget(
                id,
                leftArmyLabel,
                rightArmyLabel,
                leftSlots,
                rightSlots,
                DropArmySlot);
        }

        private static string BuildWielderArmyLabel(HostileJoinMenuAdapter adapter)
        {
            string name = adapter != null ? adapter.AttackingCommanderName : string.Empty;
            return string.IsNullOrWhiteSpace(name)
                ? ModText.Get(ModStrings.Screens.WielderArmy)
                : ModText.Get(ModStrings.Screens.WielderArmyPossessive, name);
        }

        private static TroopHudAdapter.DropResult DropArmySlot(TroopHudAdapter.SlotItem source, TroopHudAdapter.SlotItem target)
        {
            return source != null ? source.CompleteDropTo(target) : TroopHudAdapter.DropResult.None;
        }

        private static HostileJoinMenu TryResolveHostileJoinMenu(HostileJoinMenuInstaller installer)
        {
            if (!IsLiveSceneInstaller(installer) || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<HostileJoinMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(HostileJoinMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
