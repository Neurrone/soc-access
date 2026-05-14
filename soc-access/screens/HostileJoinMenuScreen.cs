using System;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class HostileJoinMenuScreen : Screen
    {
        private const int ArmyExchangeGridIndex = 2;
        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(HostileJoinMenuInstaller), "Container");

        private readonly HostileJoinMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;

        public HostileJoinMenuScreen(HostileJoinMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
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

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            GridFocus gridFocus = CaptureArmyGridFocus();

            RootWidget = BuildRoot(_adapter);
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

        private GridFocus CaptureArmyGridFocus()
        {
            ArmyExchangeGridWidget grid = RootWidget != null
                ? RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget
                : null;
            return grid != null ? new GridFocus(grid.FocusedColumnIndex, grid.FocusedRowIndex) : null;
        }

        private void RestoreArmyGridFocus(GridFocus focus)
        {
            if (focus == null || RootWidget == null)
            {
                return;
            }

            ArmyExchangeGridWidget grid = RootWidget.GetChildAt(ArmyExchangeGridIndex) as ArmyExchangeGridWidget;
            grid?.SetFocusedCell(focus.ColumnIndex, focus.RowIndex);
        }

        private static ContainerWidget BuildRoot(HostileJoinMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("hostile-join-menu", "Troops want to join");
            if (adapter == null)
            {
                return root;
            }

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
                "joining army",
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

            return root;
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
            return string.IsNullOrWhiteSpace(name) ? "wielder army" : name + "'s army";
        }

        private static TroopHudAdapter.DropResult DropArmySlot(TroopHudAdapter.SlotItem source, TroopHudAdapter.SlotItem target)
        {
            return source != null ? source.DropTo(target) : TroopHudAdapter.DropResult.None;
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

        private sealed class GridFocus
        {
            public GridFocus(int columnIndex, int rowIndex)
            {
                ColumnIndex = columnIndex;
                RowIndex = rowIndex;
            }

            public int ColumnIndex { get; private set; }
            public int RowIndex { get; private set; }
        }
    }
}
