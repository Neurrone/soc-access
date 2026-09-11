using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using UnityEngine;

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
    public sealed class OptionsMenuAdapter : IPresent
    {
        private static readonly FieldInfo FactoryField = AccessTools.Field(typeof(OptionsMenu), "_factory");
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(OptionsMenu), "_settings");
        private static readonly FieldInfo TabsField = AccessTools.Field(typeof(OptionsMenu), "_tabs");
        private static readonly FieldInfo ContentTabsField = AccessTools.Field(typeof(OptionsMenu), "_contentTabs");
        private static readonly FieldInfo CurrentContentField = AccessTools.Field(typeof(OptionsMenu), "_currentContent");

        // The Controls page keeps each rebindable action's reference off its widget, on
        // OptionsMenuKeyBindContent._keyBinders; the input manager beside it answers the current
        // override text. Both are resolved through the controls content tab.
        private static readonly FieldInfo KeyBindContentField = AccessTools.Field(typeof(OptionsMenuControlsContent), "_keyBindContent");
        private static readonly FieldInfo KeyBindersField = AccessTools.Field(typeof(OptionsMenuKeyBindContent), "_keyBinders");
        private static readonly FieldInfo KeyBindInputManagerField = AccessTools.Field(typeof(OptionsMenuKeyBindContent), "_inputManager");

        private readonly OptionsMenu _menu;

        // Per-menu key-binding context: the reverse widget -> action map (rebuilt once per frame, not
        // once per row) and the row whose capture the mod last started. It outlives the per-frame row
        // records the reader mints, so it lives here on the adapter.
        private readonly KeyBindingSource _keyBindings;
        private Dictionary<IUIKeyBinding, ActionReference> _reverse;
        private IInputManager _inputManager;
        private int _reverseFrame = -1;

        public OptionsMenuAdapter(OptionsMenu menu)
        {
            _menu = menu;
            _keyBindings = new KeyBindingSource { Resolve = ResolveBinding, RefreshScroll = RefreshScroll };
        }

        // The window's own scroller, measured again after a rebind replaced a row's chip - what
        // DrawContent does once after drawing the page.
        private void RefreshScroll()
        {
            OptionsMenu.Settings settings = Settings;
            if (settings != null && settings.autoScroller != null)
            {
                settings.autoScroller.Refresh();
            }
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

            if (_rows == null)
            {
                IMenuFactoryCollection factory = Factory;
                if (factory == null)
                {
                    return new MenuRow[0];
                }

                _rows = new MenuRowMemo(factory, settings.contentParent.MonoTransform, _keyBindings);
            }

            return _rows.Rows;
        }

        // The rows of the page showing, re-read only when the column is redrawn (a tab switch).
        private MenuRowMemo _rows;

        // widget -> the binding it holds, for a row that needs the input manager's own text as a
        // fallback. Null for an unknown widget; the reverse map is rebuilt at most once per frame.
        private BindingContainer ResolveBinding(IUIKeyBinding widget)
        {
            EnsureReverseMap();
            ActionReference action;
            if (_reverse == null || widget == null || !_reverse.TryGetValue(widget, out action))
            {
                return null;
            }

            BindingContainer container;
            return _inputManager != null && _inputManager.TryGetOverride(action, out container) ? container : null;
        }

        private void EnsureReverseMap()
        {
            int frame = Time.frameCount;
            if (_reverseFrame == frame)
            {
                return;
            }

            _reverseFrame = frame;
            _reverse = null;
            _inputManager = null;

            OptionsMenuKeyBindContent content = KeyBindContent();
            if (content == null)
            {
                return;
            }

            _inputManager = KeyBindInputManagerField != null ? KeyBindInputManagerField.GetValue(content) as IInputManager : null;
            Dictionary<ActionReference, IUIKeyBinding> binders = KeyBindersField != null
                ? KeyBindersField.GetValue(content) as Dictionary<ActionReference, IUIKeyBinding>
                : null;
            if (binders == null)
            {
                return;
            }

            Dictionary<IUIKeyBinding, ActionReference> reverse = new Dictionary<IUIKeyBinding, ActionReference>();
            foreach (KeyValuePair<ActionReference, IUIKeyBinding> binder in binders)
            {
                if (binder.Value != null)
                {
                    reverse[binder.Value] = binder.Key;
                }
            }

            _reverse = reverse;
        }

        private OptionsMenuKeyBindContent KeyBindContent()
        {
            List<IOptionsContent> tabs = GetField<List<IOptionsContent>>(_menu, ContentTabsField);
            if (tabs == null)
            {
                return null;
            }

            for (int i = 0; i < tabs.Count; i++)
            {
                OptionsMenuControlsContent controls = tabs[i] as OptionsMenuControlsContent;
                if (controls != null)
                {
                    return KeyBindContentField != null ? KeyBindContentField.GetValue(controls) as OptionsMenuKeyBindContent : null;
                }
            }

            return null;
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
