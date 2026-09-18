using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PlatformUserMenuAdapter : IPresent
    {
        private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(PlatformUserMenu), "_container");
        private static readonly FieldInfo UserButtonsField = AccessTools.Field(typeof(PlatformUserMenu), "_userButtons");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(PlatformUserMenu), "_localization");

        private readonly PlatformUserMenu _menu;

        public PlatformUserMenuAdapter(PlatformUserMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return SpokenLines.Clean(GameText.Get(GetLocalization(), "Lobby/LobbyPlayerMenu/ShowPlayerActions", string.Empty)); }
        }

        public string CancelLabel
        {
            get { return SpokenLines.Clean(GameText.Get(GetLocalization(), "Common/Cancel", string.Empty)); }
        }

        public bool IsPresent()
        {
            GameObject container = GetContainer();
            return _menu != null
                && GameObjects.IsLiveSceneObject(((Component)_menu).gameObject)
                && container != null
                && container.activeInHierarchy;
        }

        /// <summary>The actions the menu is drawing, listed at most once a frame: the build asks for
        /// the list and each row reads the game when it is asked. One item per game entry for the
        /// life of the adapter, so the tooltip behind a row - which remembers whether the game's
        /// details are a long dossier - is built once and not once per build (AGENTS.md,
        /// Performance).</summary>
        public IReadOnlyList<ActionItem> GetActions()
        {
            int frame = Time.frameCount;
            if (_actions != null && _actionsFrame == frame)
            {
                return _actions;
            }

            _actionsFrame = frame;
            SweepDestroyed();
            List<ActionItem> items = new List<ActionItem>();
            List<PlatformUserButtonEntry> entries = GetUserButtons();
            for (int i = 0; i < entries.Count; i++)
            {
                PlatformUserButtonEntry entry = entries[i];
                if (entry == null || !GameObjects.IsLive(entry as Component))
                {
                    continue;
                }

                ActionItem item;
                if (!_actionsByEntry.TryGetValue(entry, out item))
                {
                    item = new ActionItem(this, entry, i);
                    _actionsByEntry[entry] = item;
                }

                items.Add(item);
            }

            _actions = items;
            return _actions;
        }

        private readonly Dictionary<PlatformUserButtonEntry, ActionItem> _actionsByEntry =
            new Dictionary<PlatformUserButtonEntry, ActionItem>();
        private IReadOnlyList<ActionItem> _actions;
        private int _actionsFrame = -1;

        // The menu destroys every entry it drew each time it opens (decompiled PlatformUserMenu.Show
        // calls Clear, which destroys them and starts a new list), and this adapter is over the
        // project container's one menu, so an entry whose object has gone would sit in the table for
        // the rest of the session. Dead keys go whenever the table has grown by another SweepStep,
        // which costs one walk per that many new entries.
        private const int SweepStep = 64;
        private int _sweepAt = SweepStep;

        private void SweepDestroyed()
        {
            if (_actionsByEntry.Count < _sweepAt)
            {
                return;
            }

            List<PlatformUserButtonEntry> destroyed = new List<PlatformUserButtonEntry>();
            foreach (KeyValuePair<PlatformUserButtonEntry, ActionItem> pair in _actionsByEntry)
            {
                if (pair.Key == null)
                {
                    destroyed.Add(pair.Key);
                }
            }

            for (int i = 0; i < destroyed.Count; i++)
            {
                _actionsByEntry.Remove(destroyed[i]);
            }

            _sweepAt = _actionsByEntry.Count + SweepStep;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public bool Cancel()
        {
            if (_menu == null || !IsPresent())
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        private GameObject GetContainer()
        {
            return _menu != null && ContainerField != null
                ? ContainerField.GetValue(_menu) as GameObject
                : null;
        }

        private ILocalizationHandler GetLocalization()
        {
            return _menu != null && LocalizationField != null
                ? LocalizationField.GetValue(_menu) as ILocalizationHandler
                : null;
        }

        private List<PlatformUserButtonEntry> GetUserButtons()
        {
            return _menu != null && UserButtonsField != null
                ? UserButtonsField.GetValue(_menu) as List<PlatformUserButtonEntry> ?? new List<PlatformUserButtonEntry>()
                : new List<PlatformUserButtonEntry>();
        }

        public sealed class ActionItem
        {
            private static readonly FieldInfo ButtonLabelField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_buttonLabel");
            private static readonly FieldInfo ButtonField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_button");
            private static readonly FieldInfo UserButtonTypeField = AccessTools.Field(typeof(PlatformUserButtonEntry), "_userButtonType");

            private readonly PlatformUserMenuAdapter _adapter;
            private readonly PlatformUserButtonEntry _entry;
            private readonly int _index;

            public ActionItem(PlatformUserMenuAdapter adapter, PlatformUserButtonEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                _index = index;
            }

            public string Id
            {
                get { return "platform-user-action-" + _index; }
            }

            /// <summary>The row the game draws for this action, so a screen can key a control on it
            /// and ask whether it is still painted.</summary>
            public Component Entry
            {
                get { return _entry as Component; }
            }

            public string Label
            {
                get
                {
                    UITextMesh label = _entry != null && ButtonLabelField != null
                        ? ButtonLabelField.GetValue(_entry) as UITextMesh
                        : null;
                    return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(label));
                }
            }

            public string TypeName
            {
                get
                {
                    object value = _entry != null && UserButtonTypeField != null ? UserButtonTypeField.GetValue(_entry) : null;
                    return value != null ? value.ToString() : string.Empty;
                }
            }

            public bool IsVisible
            {
                get { return GameObjects.IsLive(_entry as Component) && MenuButtonAdapterBase.IsButtonVisible(Button); }
            }

            public bool IsEnabled
            {
                get { return Button != null && Button.Interactable; }
            }

            /// <summary>The row's native tooltip, built once for this row: a tooltip remembers
            /// whether the game's details behind it are a long dossier, and a fresh one per build
            /// asked the game again every frame.</summary>
            public Tooltip Tooltip
            {
                get
                {
                    if (!_tooltipBuilt)
                    {
                        _tooltipBuilt = true;
                        _tooltip = Tooltip.ForComponent(Button as Component, _adapter != null ? _adapter.GetLocalization() : null);
                    }

                    return _tooltip;
                }
            }

            private Tooltip _tooltip;
            private bool _tooltipBuilt;

            private UIButton Button
            {
                get { return _entry != null && ButtonField != null ? ButtonField.GetValue(_entry) as UIButton : null; }
            }

            public void FocusNative()
            {
                NativeSelectionUtility.Select(Button);
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(Button);
            }
        }
    }
}
