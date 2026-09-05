using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class MapEntityMiniMenuScreen : Screen
    {
        private readonly MapEntityMiniMenuAdapter _adapter;

        public MapEntityMiniMenuScreen(MapEntityMiniMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            MapEntityMiniMenu[] menus = Resources.FindObjectsOfTypeAll<MapEntityMiniMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new MapEntityMiniMenuScreen(adapter);
                }
            }

            return null;
        }

        public MapEntityMiniMenuAdapter Adapter
        {
            get { return _adapter; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.HideNativeTooltip();
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null && _adapter.Close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(MapEntityMiniMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("map-entity-mini-menu", adapter != null ? adapter.EntityName : string.Empty);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "map-entity-name",
                () => adapter.EntityName,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(new TextWidget(
                "map-entity-custom-name",
                () => adapter.CustomName,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsCustomNameVisible));

            root.AddChild(new TextWidget(
                "map-entity-blueprint-description",
                () => adapter.BlueprintDescription,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsBlueprintDescriptionVisible));

            root.AddChild(new TextWidget(
                "map-entity-stored-wielder",
                () => adapter.StoredWielderName,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                () => adapter.StoredWielderTooltip,
                () => adapter.IsStoredWielderVisible && !string.IsNullOrWhiteSpace(adapter.StoredWielderName)));

            root.AddChild(new ButtonWidget(
                "map-entity-eject-wielder",
                ModText.Get(ModStrings.Screens.EjectWielder),
                adapter.ActivateEjectWielder,
                adapter.HideNativeTooltip,
                adapter.IsEjectWielderEnabled,
                () => adapter.IsStoredWielderVisible));

            root.AddChild(BuildDescriptionRowsMenu(adapter));

            root.AddChild(new TextWidget(
                "map-entity-upgrades",
                () => adapter.UpgradeSummary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsUpgradeSummaryVisible));

            root.AddChild(new TextWidget(
                "map-entity-siege-state",
                () => adapter.SiegeState,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsSiegeStateVisible));

            root.AddChild(new TextWidget(
                "map-entity-town-status",
                () => adapter.TownStatus,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: () => adapter.IsTownStatusVisible));

            AddActionButtons(root, adapter);

            root.AddChild(new ButtonWidget(
                "map-entity-close",
                ModText.Get(ModStrings.Screens.Close),
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildDescriptionRowsMenu(MapEntityMiniMenuAdapter adapter)
        {
            IReadOnlyList<MapEntityMiniMenuAdapter.DescriptionRow> rows = adapter.GetDescriptionRows();
            MenuWidget menu = new MenuWidget("map-entity-description-rows", ModText.Get(ModStrings.Screens.Description), () => adapter.GetDescriptionRows().Count > 0);
            for (int i = 0; i < rows.Count; i++)
            {
                MapEntityMiniMenuAdapter.DescriptionRow row = rows[i];
                menu.AddItem(new MenuItemWidget(
                    row.Id,
                    () => row.Label,
                    null,
                    () => false,
                    row.Focus,
                    () => true,
                    row.GetTooltip));
            }

            return menu;
        }

        private static void AddActionButtons(ContainerWidget root, MapEntityMiniMenuAdapter adapter)
        {
            IReadOnlyList<MapEntityMiniMenuAdapter.ActionButton> buttons = adapter.GetActions();
            for (int i = 0; i < buttons.Count; i++)
            {
                MapEntityMiniMenuAdapter.ActionButton button = buttons[i];
                root.AddChild(new ButtonWidget(
                    button.Id,
                    () => button.Label,
                    button.Activate,
                    button.Focus,
                    button.IsEnabled,
                    () => true,
                    button.GetTooltip));
            }
        }
    }
}
