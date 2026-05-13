using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class PurchaseWielderScreen : Screen
    {
        private readonly PurchaseWielderMenuAdapter _adapter;

        public PurchaseWielderScreen(PurchaseWielderMenuAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            PurchaseWielderMenu[] menus = Resources.FindObjectsOfTypeAll<PurchaseWielderMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                PurchaseWielderMenuAdapter adapter = new PurchaseWielderMenuAdapter(menus[i]);
                if (adapter.IsPresent())
                {
                    return new PurchaseWielderScreen(adapter);
                }
            }

            return null;
        }

        public PurchaseWielderMenuAdapter Adapter
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
            RootWidget = BuildRoot(_adapter);
            if (focusAfterRefresh)
            {
                RootWidget?.SetFocusByIndexSilently(focusedIndex);
            }
        }

        private static ContainerWidget BuildRoot(PurchaseWielderMenuAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("purchase-wielder", "Purchase wielders");
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(new TextWidget(
                "purchase-wielder-title",
                () => adapter.Title,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildWielderMenu(adapter));

            root.AddChild(new TextWidget(
                "purchase-wielder-summary",
                () => adapter.SelectedSummary,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false));

            root.AddChild(BuildStatsMenu(adapter));
            root.AddChild(BuildTroopsMenu(adapter));
            root.AddChild(BuildSkillsMenu(adapter));

            root.AddChild(new TextWidget(
                "purchase-wielder-specialization",
                () => adapter.Specialization,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.HasSpecialization));

            root.AddChild(new TextWidget(
                "purchase-wielder-status",
                () => adapter.PurchaseStatus,
                adapter.HideNativeTooltip,
                includeParentLabelInAnnouncement: false,
                isVisible: adapter.HasPurchaseStatus));

            root.AddChild(new ButtonWidget(
                "purchase-wielder-purchase",
                () => adapter.PurchaseLabel,
                adapter.ActivatePurchase,
                adapter.FocusPurchase,
                adapter.IsPurchaseEnabled,
                adapter.IsPurchaseVisible,
                () => adapter.PurchaseTooltip));

            root.AddChild(new ButtonWidget(
                "purchase-wielder-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));

            return root;
        }

        private static MenuWidget BuildWielderMenu(PurchaseWielderMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("purchase-wielder-list", "Wielders");
            IReadOnlyList<PurchaseWielderMenuAdapter.EntryItem> entries = adapter.GetEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                PurchaseWielderMenuAdapter.EntryItem entry = entries[i];
                menu.AddItem(new MenuItemWidget(
                    entry.Id,
                    () => entry.Label,
                    () => entry.Status,
                    entry.Select,
                    entry.Focus,
                    () => entry.IsVisible));
            }

            if (entries.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "purchase-wielder-list-empty",
                    () => "No wielders",
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true));
            }
            else
            {
                menu.SetFocusedItemById(adapter.SelectedEntryId);
            }

            return menu;
        }

        private static MenuWidget BuildStatsMenu(PurchaseWielderMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("purchase-wielder-stats", adapter.StatsHeader);
            menu.AddItem(BuildReadOnlyItem("purchase-wielder-offence", () => adapter.OffenceHeader + " " + adapter.Offence, adapter.HideNativeTooltip));
            menu.AddItem(BuildReadOnlyItem("purchase-wielder-defence", () => adapter.DefenceHeader + " " + adapter.Defence, adapter.HideNativeTooltip));
            menu.AddItem(BuildReadOnlyItem("purchase-wielder-movement", () => adapter.MovementHeader + " " + adapter.Movement, adapter.HideNativeTooltip));
            menu.AddItem(BuildReadOnlyItem("purchase-wielder-view-radius", () => adapter.ViewRadiusHeader + " " + adapter.ViewRadius, adapter.HideNativeTooltip));
            return menu;
        }

        private static MenuWidget BuildTroopsMenu(PurchaseWielderMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("purchase-wielder-troops", adapter.TroopsHeader, adapter.HasTroops);
            int count = adapter.TroopSlotCount;
            for (int i = 0; i < count; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "purchase-wielder-troop-" + capturedIndex,
                    () => BuildTroopLabel(adapter, capturedIndex),
                    null,
                    () => false,
                    () => adapter.FocusTroop(capturedIndex),
                    () => adapter.IsTroopVisible(capturedIndex),
                    () => adapter.GetTroopTooltip(capturedIndex)));
            }

            return menu;
        }

        private static string BuildTroopLabel(PurchaseWielderMenuAdapter adapter, int index)
        {
            if (adapter == null)
            {
                return string.Empty;
            }

            string name = adapter.GetTroopName(index);
            if (string.IsNullOrWhiteSpace(name))
            {
                name = "Troop slot " + (index + 1);
            }

            int amount = adapter.GetTroopAmount(index);
            return amount > 0 ? amount + " " + name : name;
        }

        private static MenuWidget BuildSkillsMenu(PurchaseWielderMenuAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("purchase-wielder-skills", adapter.SkillsHeader, () => HasVisibleSkills(adapter));
            int count = adapter.SkillSlotCount;
            for (int i = 0; i < count; i++)
            {
                int capturedIndex = i;
                menu.AddItem(new MenuItemWidget(
                    "purchase-wielder-skill-" + capturedIndex,
                    () => BuildSkillLabel(adapter, capturedIndex),
                    null,
                    () => false,
                    () => adapter.FocusSkill(capturedIndex),
                    () => adapter.IsSkillVisible(capturedIndex),
                    () => adapter.GetSkillTooltip(capturedIndex)));
            }

            return menu;
        }

        private static string BuildSkillLabel(PurchaseWielderMenuAdapter adapter, int index)
        {
            if (adapter == null)
            {
                return string.Empty;
            }

            string name = adapter.GetSkillName(index);
            return string.IsNullOrWhiteSpace(name) ? "Skill " + (index + 1) : name;
        }

        private static bool HasVisibleSkills(PurchaseWielderMenuAdapter adapter)
        {
            if (adapter == null)
            {
                return false;
            }

            for (int i = 0; i < adapter.SkillSlotCount; i++)
            {
                if (adapter.IsSkillVisible(i))
                {
                    return true;
                }
            }

            return false;
        }

        private static MenuItemWidget BuildReadOnlyItem(string id, System.Func<string> getLabel, System.Action onFocus)
        {
            return new MenuItemWidget(
                id,
                getLabel,
                null,
                () => false,
                onFocus,
                () => true);
        }
    }
}
