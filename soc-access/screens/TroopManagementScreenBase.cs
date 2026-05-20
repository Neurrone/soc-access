using System;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal abstract class TroopManagementScreenBase : Screen
    {
        private const int WielderTroopMenuIndex = 3;

        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;
        private Action<ResourceUpdatedPayload> _resourceUpdatedHandler;
        private Action _recruitmentPoolUpdatedHandler;

        protected TroopManagementScreenBase(ITroopManagementHostAdapter host)
            : base(new ContainerWidget(host != null ? host.IdPrefix : "troop-management", host != null ? host.Title : string.Empty))
        {
            Host = host;
            RootWidget = BuildRoot();
        }

        protected ITroopManagementHostAdapter Host { get; private set; }

        public string HostIdPrefix
        {
            get { return Host != null ? Host.IdPrefix : string.Empty; }
        }

        protected abstract string ScreenSuffix { get; }
        protected abstract string ScreenTitle { get; }
        protected abstract bool IsContentPresent();
        protected abstract void AddContentWidgets(ContainerWidget root);

        public override bool IsPresent()
        {
            return Host != null && IsContentPresent();
        }

        public override void OnPush()
        {
            AttachListeners();
        }

        public override void OnUnfocus()
        {
            Host?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            DetachListeners();
            Host?.HideNativeTooltip();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

                if (Host != null && Host.IsBackVisible() && Host.Back())
                {
                    return true;
                }

                return Host != null && Host.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh()
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int troopMenuFocusedIndex = GetWielderTroopMenuFocusedIndex();
            RootWidget = BuildRoot();
            RestoreWielderTroopMenuFocus(troopMenuFocusedIndex);
            RootWidget?.SetFocusByIndexSilently(focusedIndex);
        }

        private int GetWielderTroopMenuFocusedIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(WielderTroopMenuIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private void RestoreWielderTroopMenuFocus(int focusedIndex)
        {
            if (focusedIndex < 0 || RootWidget == null)
            {
                return;
            }

            MenuWidget menu = RootWidget.GetChildAt(WielderTroopMenuIndex) as MenuWidget;
            menu?.SetFocusByIndexSilently(focusedIndex);
        }

        private void AttachListeners()
        {
            IClientAdventureFacade facade = Host != null ? Host.Facade : null;
            if (facade == null || facade.Commands == null)
            {
                return;
            }

            _troopsUpdatedHandler = HandleTroopsUpdated;
            _resourceUpdatedHandler = HandleResourceUpdated;
            _recruitmentPoolUpdatedHandler = HandleRecruitmentPoolUpdated;

            IClientCommandsFacade commands = facade.Commands;
            commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Combine(commands.OnTroopsUpdated, _troopsUpdatedHandler);
            commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Combine(commands.OnResourceUpdated, _resourceUpdatedHandler);
            commands.OnRecruitmentPoolUpdated = (Action)Delegate.Combine(commands.OnRecruitmentPoolUpdated, _recruitmentPoolUpdatedHandler);
        }

        private void DetachListeners()
        {
            IClientAdventureFacade facade = Host != null ? Host.Facade : null;
            if (facade == null || facade.Commands == null)
            {
                return;
            }

            IClientCommandsFacade commands = facade.Commands;
            if (_troopsUpdatedHandler != null)
            {
                commands.OnTroopsUpdated = (Action<OnTroopsUpdatedPayload>)Delegate.Remove(commands.OnTroopsUpdated, _troopsUpdatedHandler);
                _troopsUpdatedHandler = null;
            }

            if (_resourceUpdatedHandler != null)
            {
                commands.OnResourceUpdated = (Action<ResourceUpdatedPayload>)Delegate.Remove(commands.OnResourceUpdated, _resourceUpdatedHandler);
                _resourceUpdatedHandler = null;
            }

            if (_recruitmentPoolUpdatedHandler != null)
            {
                commands.OnRecruitmentPoolUpdated = (Action)Delegate.Remove(commands.OnRecruitmentPoolUpdated, _recruitmentPoolUpdatedHandler);
                _recruitmentPoolUpdatedHandler = null;
            }
        }

        private void HandleTroopsUpdated(OnTroopsUpdatedPayload payload)
        {
            if (Host != null && Host.ShouldRefreshForTroops(payload))
            {
                RefreshIfTop();
            }
        }

        private void HandleResourceUpdated(ResourceUpdatedPayload payload)
        {
            if (Host != null && Host.ShouldRefreshForResource(payload))
            {
                RefreshIfTop();
            }
        }

        private void HandleRecruitmentPoolUpdated()
        {
            if (Host != null && Host.ShouldRefreshForRecruitmentPool())
            {
                RefreshIfTop();
            }
        }

        private void RefreshIfTop()
        {
            if (ReferenceEquals(SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this))
            {
                Refresh();
            }
        }

        private ContainerWidget BuildRoot()
        {
            string prefix = Host != null ? Host.IdPrefix + "-" + ScreenSuffix : "troop-management";
            ContainerWidget root = new ContainerWidget(prefix, ScreenTitle);
            if (Host == null)
            {
                return root;
            }

            root.AddChild(new ButtonWidget(
                prefix + "-tutorial",
                () => Host.TutorialLabel,
                Host.ActivateTutorial,
                Host.HideNativeTooltip,
                Host.IsTutorialVisible,
                Host.IsTutorialVisible));

            root.AddChild(new TextWidget(
                prefix + "-title",
                () => Host.Title,
                Host.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            if (Host.HasWielderArmy)
            {
                root.AddChild(Portrait.Static(
                    prefix + "-wielder",
                    () => Host.WielderName,
                    Host.HideNativeTooltip,
                    () => Host.WielderTooltip));

                root.AddChild(TroopHudMenu.Build(
                    prefix + "-troops",
                    GameText.Get("Commanders/Tooltip/Troops", string.Empty),
                    Host.WielderTroops,
                    () => true));
            }

            AddContentWidgets(root);

            root.AddChild(new ButtonWidget(
                prefix + "-back",
                ModText.Get(ModStrings.Screens.Back),
                Host.Back,
                Host.HideNativeTooltip,
                Host.IsBackVisible,
                Host.IsBackVisible));

            root.AddChild(new ButtonWidget(
                prefix + "-close",
                ModText.Get(ModStrings.Screens.Close),
                Host.Close,
                Host.HideNativeTooltip,
                IsContentPresent));

            return root;
        }
    }
}
