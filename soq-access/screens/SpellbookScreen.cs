using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class SpellbookScreen : Screen
    {
        private readonly SpellbookAdapter _adapter;

        public SpellbookScreen(SpellbookAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            _adapter?.HideNativeTooltip();
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

        private static ContainerWidget BuildRoot(SpellbookAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("spellbook-screen", "Spellbook");
            if (adapter == null)
            {
                return root;
            }

            if (adapter.IsTutorialButtonVisible())
            {
                root.AddChild(new ButtonWidget(
                    "spellbook-tutorial",
                    adapter.GetTutorialButtonLabel(),
                    adapter.ActivateTutorial,
                    adapter.HideNativeTooltip,
                    adapter.IsTutorialButtonVisible,
                    adapter.IsTutorialButtonVisible));
            }

            root.AddChild(BuildQuickbarMenu(adapter));
            root.AddChild(new CheckboxWidget(
                "spellbook-auto-populate",
                adapter.GetAutoPopulateLabel(),
                adapter.ToggleAutoPopulate,
                adapter.IsAutoPopulateChecked,
                adapter.IsAutoPopulateVisible));
            root.AddChild(BuildSchoolSummary(adapter));
            root.AddChild(BuildSpellMenu(adapter, SpellbookSpellGroup.Order, "Order spells"));
            root.AddChild(BuildSpellMenu(adapter, SpellbookSpellGroup.Chaos, "Chaos spells"));
            root.AddChild(BuildSpellMenu(adapter, SpellbookSpellGroup.Destruction, "Destruction spells"));
            root.AddChild(BuildSpellMenu(adapter, SpellbookSpellGroup.Creation, "Creation spells"));
            root.AddChild(BuildSpellMenu(adapter, SpellbookSpellGroup.Arcana, "Arcana spells"));
            root.AddChild(BuildSpellMenu(adapter, SpellbookSpellGroup.Multi, "Multi-essence spells"));

            root.AddChild(new ButtonWidget(
                "spellbook-close",
                "Close",
                adapter.Close,
                adapter.HideNativeTooltip,
                () => true));
            return root;
        }

        private static MenuWidget BuildSchoolSummary(SpellbookAdapter adapter)
        {
            MenuWidget menu = new MenuWidget("spellbook-school-summary", "Schools");
            IReadOnlyList<SpellbookAdapter.SchoolSummaryItem> items = SafeGet("school summary", adapter.GetSchoolSummary);
            for (int i = 0; i < items.Count; i++)
            {
                SpellbookAdapter.SchoolSummaryItem item = items[i];
                menu.AddItem(new MenuItemWidget(
                    "spellbook-school-" + item.Id,
                    () => item.Label,
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true,
                    adapter.GetTierTooltip(item)));
            }

            return menu;
        }

        private static MenuWidget BuildSpellMenu(SpellbookAdapter adapter, SpellbookSpellGroup group, string label)
        {
            MenuWidget menu = new MenuWidget("spellbook-" + group.ToString().ToLowerInvariant() + "-spells", label);
            IReadOnlyList<SpellbookAdapter.SpellItem> items = SafeGet(label, () => adapter.GetSpells(group));
            if (items.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    menu.Id + "-none",
                    () => "None",
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true));
                return menu;
            }

            for (int i = 0; i < items.Count; i++)
            {
                SpellbookAdapter.SpellItem item = items[i];
                menu.AddItem(new MenuItemWidget(
                    "spellbook-spell-" + item.Id,
                    () => item.Label,
                    null,
                    item.Activate,
                    item.Focus,
                    () => true,
                    item.Tooltip,
                    item.Unfocus));
            }

            return menu;
        }

        private static MenuWidget BuildQuickbarMenu(SpellbookAdapter adapter)
        {
            Dictionary<MenuItemWidget, SpellbookAdapter.QuickbarItem> itemByWidget = new Dictionary<MenuItemWidget, SpellbookAdapter.QuickbarItem>();
            DraggableMenuWidget menu = null;
            menu = new DraggableMenuWidget(
                "spellbook-quickbar",
                "Quickbar",
                (source, target) =>
                {
                    SpellbookAdapter.QuickbarItem sourceItem;
                    SpellbookAdapter.QuickbarItem targetItem;
                    return itemByWidget.TryGetValue(source, out sourceItem)
                        && itemByWidget.TryGetValue(target, out targetItem)
                        && sourceItem.DropTo(targetItem);
                });
            IReadOnlyList<SpellbookAdapter.QuickbarItem> items = SafeGet("quickbar", adapter.GetQuickbarItems);
            for (int i = 0; i < items.Count; i++)
            {
                SpellbookAdapter.QuickbarItem item = items[i];
                DraggableMenuItemWidget widget = null;
                widget = new DraggableMenuItemWidget(
                    item.Id,
                    () => item.Label,
                    null,
                    item.Activate,
                    item.Focus,
                    () => true,
                    () => item.CanDrag,
                    () => ReferenceEquals(menu.DragSource, widget),
                    () => item.Tooltip,
                    item.Unfocus);
                itemByWidget.Add(widget, item);
                menu.AddItem(widget);
            }

            if (items.Count == 0)
            {
                menu.AddItem(new MenuItemWidget(
                    "spellbook-quickbar-none",
                    () => "No quickbar slots",
                    null,
                    () => false,
                    adapter.HideNativeTooltip,
                    () => true));
            }

            return menu;
        }

        private static IReadOnlyList<T> SafeGet<T>(string section, Func<IReadOnlyList<T>> getter)
        {
            try
            {
                IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("SpellbookScreen section " + section + " failed to build: " + exception);
                return new T[0];
            }
        }
    }
}
