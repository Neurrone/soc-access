using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The game's options window: its category tabs, the rows of the category showing, and the OK
    /// button along the bottom.
    ///
    /// The rows themselves are read by <see cref="MenuRows"/> over the window's own
    /// <c>IMenuFactoryCollection</c>, which is the same reader the mod's own options dialog uses:
    /// both forms are drawn by a <c>MenuFactoryController</c>, so both are read the same way.
    /// </summary>
    public sealed class OptionsMenuAdapter
    {
        private static readonly FieldInfo FactoryField = AccessTools.Field(typeof(OptionsMenu), "_factory");
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(OptionsMenu), "_settings");
        private static readonly FieldInfo TabsField = AccessTools.Field(typeof(OptionsMenu), "_tabs");
        private static readonly FieldInfo ContentTabsField = AccessTools.Field(typeof(OptionsMenu), "_contentTabs");
        private static readonly FieldInfo CurrentContentField = AccessTools.Field(typeof(OptionsMenu), "_currentContent");

        private readonly OptionsMenu _menu;

        public OptionsMenuAdapter(OptionsMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public bool IsPresent()
        {
            OptionsMenu.Settings settings = Settings;
            return _menu != null
                && settings != null
                && settings.parent != null
                && settings.parent.Active;
        }

        public IReadOnlyList<TabItem> GetTabs()
        {
            List<TabItem> result = new List<TabItem>();
            List<UIButton> tabs = GetField<List<UIButton>>(_menu, TabsField);
            if (tabs == null)
            {
                return result;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                UIButton button = tabs[i];
                if (button == null)
                {
                    continue;
                }

                int index = i;
                result.Add(new TabItem(
                    "options-tab-" + index,
                    () => MenuRows.Label(button),
                    () => SelectTab(index),
                    () => button.gameObject.activeInHierarchy));
            }

            return result;
        }

        public int GetActiveTabIndex()
        {
            List<IOptionsContent> contentTabs = GetField<List<IOptionsContent>>(_menu, ContentTabsField);
            IOptionsContent current = GetField<IOptionsContent>(_menu, CurrentContentField);
            if (contentTabs == null || current == null)
            {
                return 0;
            }

            int index = contentTabs.IndexOf(current);
            return index >= 0 ? index : 0;
        }

        public bool SelectTab(int index)
        {
            List<UIButton> tabs = GetField<List<UIButton>>(_menu, TabsField);
            if (tabs == null || index < 0 || index >= tabs.Count)
            {
                return false;
            }

            UIButton button = tabs[index];
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        public IReadOnlyList<MenuRow> GetCurrentContentControls()
        {
            OptionsMenu.Settings settings = Settings;
            if (settings == null || settings.contentParent == null)
            {
                return new MenuRow[0];
            }

            return MenuRows.Read(Factory);
        }

        public MenuRowButton GetOkButton()
        {
            OptionsMenu.Settings settings = Settings;
            return MenuRows.Button("options-ok", settings != null ? settings.okButton : null);
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Close();
            return true;
        }

        private IMenuFactoryCollection Factory
        {
            get { return GetField<IMenuFactoryCollection>(_menu, FactoryField); }
        }

        private OptionsMenu.Settings Settings
        {
            get { return GetField<OptionsMenu.Settings>(_menu, SettingsField); }
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class TabItem
        {
            public TabItem(string id, Func<string> getLabel, Func<bool> select, Func<bool> isVisible)
            {
                Id = id;
                GetLabel = getLabel;
                Select = select;
                IsVisible = isVisible;
            }

            public string Id { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<bool> Select { get; private set; }
            public Func<bool> IsVisible { get; private set; }
        }
    }
}
