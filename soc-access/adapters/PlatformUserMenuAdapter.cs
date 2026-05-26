using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PlatformUserMenuAdapter
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
            get { return SpeechTextSanitizer.Normalize(GameText.Get(GetLocalization(), "Lobby/LobbyPlayerMenu/ShowPlayerActions", string.Empty)); }
        }

        public string CancelLabel
        {
            get { return SpeechTextSanitizer.Normalize(GameText.Get(GetLocalization(), "Common/Cancel", string.Empty)); }
        }

        public bool IsPresent()
        {
            GameObject container = GetContainer();
            return _menu != null
                && IsLiveSceneObject(((Component)_menu).gameObject)
                && container != null
                && container.activeInHierarchy;
        }

        public IReadOnlyList<ActionItem> GetActions()
        {
            List<ActionItem> items = new List<ActionItem>();
            List<PlatformUserButtonEntry> entries = GetUserButtons();
            for (int i = 0; i < entries.Count; i++)
            {
                PlatformUserButtonEntry entry = entries[i];
                if (entry != null && IsVisible(entry as Component))
                {
                    items.Add(new ActionItem(this, entry, i));
                }
            }

            return items;
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

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        internal sealed class ActionItem
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

            public string Label
            {
                get
                {
                    UITextMesh label = _entry != null && ButtonLabelField != null
                        ? ButtonLabelField.GetValue(_entry) as UITextMesh
                        : null;
                    return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(label));
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
                get { return PlatformUserMenuAdapter.IsVisible(_entry as Component) && MenuButtonAdapterBase.IsButtonVisible(Button); }
            }

            public bool IsEnabled
            {
                get { return Button != null && Button.Interactable; }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(Button as Component, _adapter != null ? _adapter.GetLocalization() : null); }
            }

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
