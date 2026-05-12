using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class RallyPointScreen : Screen
    {
        private const int TroopMenuIndex = 3;
        private const int SourceMenuIndex = 4;

        private readonly RallyPointInteractionMenuAdapter _adapter;
        private Action<OnTroopsUpdatedPayload> _troopsUpdatedHandler;
        private Action<ResourceUpdatedPayload> _resourceUpdatedHandler;
        private Action _recruitmentPoolUpdatedHandler;

        public RallyPointScreen(RallyPointInteractionMenuAdapter adapter)
            : base(new ContainerWidget("rally-point", adapter != null ? adapter.Title : "Rally point"))
        {
            _adapter = adapter;
            RootWidget = BuildRoot();
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
            _adapter?.HideNativeTooltip();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (RootWidget != null && RootWidget.HandleAction(action))
                {
                    return true;
                }

                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        public void Refresh(bool focusAfterRefresh)
        {
            if (!IsPresent())
            {
                return;
            }

            int focusedIndex = RootWidget != null ? RootWidget.FocusedIndex : -1;
            int troopMenuFocusedIndex = GetTroopMenuFocusedIndex();
            string focusedSourceId = GetFocusedSourceId();

            RootWidget = BuildRoot();
            RestoreTroopMenuFocus(troopMenuFocusedIndex);
            RestoreSourceFocus(focusedSourceId);

            if (!focusAfterRefresh)
            {
                return;
            }

            if (RootWidget == null || !RootWidget.SetFocusByIndex(focusedIndex))
            {
                RootWidget?.Focus();
            }
        }

        private void AttachListeners()
        {
            IClientAdventureFacade facade = _adapter != null ? _adapter.Facade : null;
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
            IClientAdventureFacade facade = _adapter != null ? _adapter.Facade : null;
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
            if (payload == null || _adapter == null)
            {
                return;
            }

            if (payload.ParentId != _adapter.InteractingCommanderId && payload.ParentId != _adapter.RallyPointMapEntityId)
            {
                return;
            }

            RefreshIfTop();
        }

        private void HandleResourceUpdated(ResourceUpdatedPayload payload)
        {
            if (payload != null)
            {
                RefreshIfTop();
            }
        }

        private void HandleRecruitmentPoolUpdated()
        {
            RefreshIfTop();
        }

        private void RefreshIfTop()
        {
            if (ReferenceEquals(SoqAccessPlugin.Instance?.ScreenManager?.CurrentScreen, this))
            {
                Refresh(true);
            }
        }

        private int GetTroopMenuFocusedIndex()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(TroopMenuIndex) as MenuWidget : null;
            return menu != null ? menu.FocusedIndex : -1;
        }

        private void RestoreTroopMenuFocus(int focusedIndex)
        {
            if (focusedIndex < 0 || RootWidget == null)
            {
                return;
            }

            MenuWidget menu = RootWidget.GetChildAt(TroopMenuIndex) as MenuWidget;
            menu?.SetFocusByIndex(focusedIndex);
        }

        private string GetFocusedSourceId()
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(SourceMenuIndex) as MenuWidget : null;
            return menu != null && menu.FocusedItem != null ? menu.FocusedItem.Id : null;
        }

        private void RestoreSourceFocus(string focusedSourceId)
        {
            MenuWidget menu = RootWidget != null ? RootWidget.GetChildAt(SourceMenuIndex) as MenuWidget : null;
            if (menu == null)
            {
                return;
            }

            if (!menu.SetFocusedItemById(focusedSourceId))
            {
                menu.SetFocusedItemById(BuildSelectedSourceId());
            }
        }

        private ContainerWidget BuildRoot()
        {
            ContainerWidget root = new ContainerWidget("rally-point", _adapter != null ? _adapter.Title : "Rally point");
            if (_adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "rally-point-title",
                () => _adapter.Title,
                _adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "rally-point-selected-source",
                () => BuildSelectedSourceLabel(),
                _adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => !string.IsNullOrWhiteSpace(_adapter.SelectedSourceName)));

            root.AddChild(Portrait.Static(
                "rally-point-wielder",
                () => BuildWielderName(),
                _adapter.HideNativeTooltip,
                () => _adapter.WielderTooltip));

            root.AddChild(TroopHudMenu.Build(
                "rally-point-troops",
                BuildWielderArmyLabel(),
                _adapter.Troops,
                () => true));

            root.AddChild(BuildSourceMenu());
            AddRecruitWidgets(root);

            root.AddChild(new ButtonWidget(
                "rally-point-close",
                () => BuildCloseLabel(),
                _adapter.Close,
                _adapter.HideNativeTooltip,
                () => _adapter.IsPresent()));

            return root;
        }

        private string BuildSelectedSourceLabel()
        {
            string source = _adapter != null ? _adapter.SelectedSourceName : string.Empty;
            return string.IsNullOrWhiteSpace(source) ? string.Empty : "Recruiting from " + source;
        }

        private string BuildWielderName()
        {
            string name = _adapter != null ? _adapter.WielderName : string.Empty;
            return string.IsNullOrWhiteSpace(name) ? "Wielder" : name;
        }

        private string BuildWielderArmyLabel()
        {
            string name = _adapter != null ? _adapter.WielderName : string.Empty;
            return string.IsNullOrWhiteSpace(name) ? "wielder army" : name + "'s army";
        }

        private string BuildCloseLabel()
        {
            string label = _adapter != null ? _adapter.CloseLabel : string.Empty;
            return string.IsNullOrWhiteSpace(label) ? "Close" : label;
        }

        private MenuWidget BuildSourceMenu()
        {
            MenuWidget menu = new MenuWidget("rally-point-sources", "Recruit from");
            IReadOnlyList<RallyPointInteractionMenuAdapter.SourceItem> sources = _adapter.GetSourceItems();
            for (int i = 0; i < sources.Count; i++)
            {
                RallyPointInteractionMenuAdapter.SourceItem source = sources[i];
                // Do not click from focus. Clicking a source invokes the native rally selection,
                // our native-selection hook refreshes this screen, and focus restoration focuses
                // this item again, causing a focus -> click -> refresh -> focus loop.
                // Keep source changes on explicit Enter activation.
                menu.AddItem(new MenuItemWidget(
                    BuildSourceId(source),
                    () => BuildSourceLabel(source),
                    () => source.IsSelected ? "selected" : string.Empty,
                    () => SelectSource(source),
                    source.Focus,
                    () => true,
                    () => source.Tooltip));
            }

            menu.SetFocusedItemById(BuildSelectedSourceId());
            return menu;
        }

        private string BuildSelectedSourceId()
        {
            if (_adapter == null)
            {
                return string.Empty;
            }

            IReadOnlyList<RallyPointInteractionMenuAdapter.SourceItem> sources = _adapter.GetSourceItems();
            int selectedIndex = _adapter.SelectedSourceIndex;
            for (int i = 0; i < sources.Count; i++)
            {
                if (sources[i] != null && sources[i].Index == selectedIndex)
                {
                    return BuildSourceId(sources[i]);
                }
            }

            return string.Empty;
        }

        private static string BuildSourceId(RallyPointInteractionMenuAdapter.SourceItem source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            return source.MapEntityId >= 0
                ? "rally-source-town-" + source.MapEntityId
                : "rally-source-all-" + source.Index;
        }

        private bool SelectSource(RallyPointInteractionMenuAdapter.SourceItem source)
        {
            if (source == null)
            {
                return false;
            }

            bool selected = source.Select();
            if (selected)
            {
                Refresh(true);
            }

            return selected;
        }

        private static string BuildSourceLabel(RallyPointInteractionMenuAdapter.SourceItem source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (!source.IsLevelVisible || string.IsNullOrWhiteSpace(source.Level))
            {
                return BuildSourceName(source);
            }

            return BuildSourceName(source) + ", level " + source.Level;
        }

        private static string BuildSourceName(RallyPointInteractionMenuAdapter.SourceItem source)
        {
            if (source == null)
            {
                return string.Empty;
            }

            if (!string.IsNullOrWhiteSpace(source.Name))
            {
                return source.Name;
            }

            return source.IsAllSources ? "All settlements" : "Settlement";
        }

        private void AddRecruitWidgets(ContainerWidget root)
        {
            if (root == null || _adapter == null || _adapter.PurchaseTroops == null)
            {
                return;
            }

            IReadOnlyList<PurchaseTroopsSubMenuAdapter.RecruitEntry> entries = _adapter.PurchaseTroops.GetRecruitEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                AddRecruitWidgets(root, entries[i]);
            }
        }

        private void AddRecruitWidgets(ContainerWidget root, PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            if (root == null || entry == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-name",
                () => entry.TroopName,
                entry.Focus,
                includeParentLabelInAnnouncement: false,
                () => entry.Tooltip));

            if (entry.IsEssenceMenuVisible)
            {
                root.AddChild(BuildEssenceMenu(entry));
            }

            root.AddChild(new TextWidget(
                entry.IdPrefix + "-no-troops",
                () => entry.NoTroopsText,
                _adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => entry.IsNoTroopsVisible));

            root.AddChild(new SliderWidget(
                entry.IdPrefix + "-quantity",
                "quantity",
                () => entry.SliderLabel,
                () => entry.SliderValue,
                () => entry.SliderMinimum,
                () => entry.SliderMaximum,
                () => 1,
                entry.SetSliderValue,
                () => entry.IsSliderEnabled,
                () => entry.IsSliderVisible));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-purchase",
                () => BuildPurchaseLabel(entry.PurchaseCosts),
                entry.Purchase,
                entry.Focus,
                () => entry.IsPurchaseEnabled,
                () => entry.IsPurchaseVisible,
                () => entry.PurchaseTooltip));

            root.AddChild(new ButtonWidget(
                entry.IdPrefix + "-upgrade-in-pool",
                () => "Upgrade available troops",
                entry.UpgradeInPool,
                entry.Focus,
                () => entry.IsUpgradeInPoolEnabled,
                () => entry.IsUpgradeInPoolVisible,
                () => entry.UpgradeInPoolTooltip));
        }

        private static MenuWidget BuildEssenceMenu(PurchaseTroopsSubMenuAdapter.RecruitEntry entry)
        {
            MenuWidget menu = new MenuWidget(entry.IdPrefix + "-essence", "Essence variants");
            AddEssenceItem(menu, entry, TroopUpgradeType.ArcanaUpgraded, "Arcana");
            AddEssenceItem(menu, entry, TroopUpgradeType.CreationUpgraded, "Creation");
            AddEssenceItem(menu, entry, TroopUpgradeType.OrderUpgraded, "Order");
            menu.SetFocusedItemById(entry.IdPrefix + "-essence-" + entry.CurrentEssenceVariant.ToString().ToLowerInvariant());
            return menu;
        }

        private static void AddEssenceItem(
            MenuWidget menu,
            PurchaseTroopsSubMenuAdapter.RecruitEntry entry,
            TroopUpgradeType upgradeType,
            string label)
        {
            menu.AddItem(new MenuItemWidget(
                entry.IdPrefix + "-essence-" + upgradeType.ToString().ToLowerInvariant(),
                () => label,
                null,
                () => entry.SelectEssenceVariant(upgradeType),
                () => entry.SelectEssenceVariant(upgradeType),
                () => true));
        }

        private static string BuildPurchaseLabel(IReadOnlyList<PurchaseTroopsSubMenuAdapter.ResourceCostLine> costs)
        {
            List<string> parts = new List<string>();
            if (costs != null)
            {
                for (int i = 0; i < costs.Count; i++)
                {
                    PurchaseTroopsSubMenuAdapter.ResourceCostLine cost = costs[i];
                    if (cost != null)
                    {
                        parts.Add(cost.Amount + " " + GetResourceName(cost.ResourceType));
                    }
                }
            }

            return parts.Count == 0 ? "Purchase" : "Purchase for " + JoinWithAnd(parts);
        }

        private static string GetResourceName(ResourceType resourceType)
        {
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            string fallback = FormatEnumName(resourceType.ToString());
            if (localization == null)
            {
                return fallback;
            }

            string key = "Common/Resource/" + resourceType;
            string text = localization.GetText(key);
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
        }

        private static string JoinWithAnd(List<string> parts)
        {
            if (parts.Count == 1)
            {
                return parts[0];
            }

            if (parts.Count == 2)
            {
                return parts[0] + " and " + parts[1];
            }

            return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + ", and " + parts[parts.Count - 1];
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(char.ToLowerInvariant(c));
            }

            return new string(chars.ToArray());
        }
    }
}
