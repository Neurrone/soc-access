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
    internal sealed class AdventureLobbyInviteProvidersAdapter
    {
        private static readonly FieldInfo InviteDropdownContainerField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_inviteDropdownContainer");
        private static readonly FieldInfo InviteFriendButtonField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_inviteFriendButton");
        private static readonly FieldInfo InviteFromSocialButtonsField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_inviteFromSocialButtons");
        private static readonly FieldInfo InviteUiBlockerField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_inviteUiBlocker");
        private static readonly MethodInfo CancelMethod =
            AccessTools.Method(typeof(LobbyMultiplayerPanel), "HandleCancelInvitePopup");

        private readonly LobbyMultiplayerPanel _panel;
        private readonly ILocalizationHandler _localization;

        public AdventureLobbyInviteProvidersAdapter(LobbyMultiplayerPanel panel)
        {
            _panel = panel;
            _localization = GlobalLocalizationVariables.LocalizationHandler;
        }

        public object SourceKey
        {
            get { return _panel; }
        }

        public string Title
        {
            get
            {
                UIButton button = GetField<UIButton>(InviteFriendButtonField);
                string label = MenuButtonTextUtility.GetStandardButtonLabel(button);
                return string.IsNullOrWhiteSpace(label) ? ModText.Get(ModStrings.Screens.InviteFriend) : label;
            }
        }

        public string CancelLabel
        {
            get { return ModText.Get(ModStrings.Actions.Cancel); }
        }

        public bool IsPresent()
        {
            GameObject panelObject = _panel != null ? ((Component)_panel).gameObject : null;
            GameObject container = GetField<GameObject>(InviteDropdownContainerField);
            return IsLiveSceneObject(panelObject)
                && panelObject.activeInHierarchy
                && container != null
                && container.activeInHierarchy;
        }

        public IReadOnlyList<ProviderButtonItem> GetProviderButtons()
        {
            List<ProviderButtonItem> items = new List<ProviderButtonItem>();
            UIButton[] buttons = GetField<UIButton[]>(InviteFromSocialButtonsField);
            if (buttons == null)
            {
                return items;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (button != null)
                {
                    items.Add(new ProviderButtonItem(i, button, _localization));
                }
            }

            items.Sort((left, right) => left.SortIndex.CompareTo(right.SortIndex));
            return items;
        }

        public bool Cancel()
        {
            if (_panel == null)
            {
                return false;
            }

            if (CancelMethod != null)
            {
                CancelMethod.Invoke(_panel, new object[0]);
                return true;
            }

            UIButton blocker = GetField<UIButton>(InviteUiBlockerField);
            return NativeSelectionUtility.Click(blocker);
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _panel != null && field != null ? field.GetValue(_panel) as T : null;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        internal sealed class ProviderButtonItem
        {
            private readonly int _index;
            private readonly UIButton _button;
            private readonly ILocalizationHandler _localization;

            public ProviderButtonItem(int index, UIButton button, ILocalizationHandler localization)
            {
                _index = index;
                _button = button;
                _localization = localization;
            }

            public int SortIndex
            {
                get
                {
                    Component component = _button as Component;
                    return component != null ? component.transform.GetSiblingIndex() : _index;
                }
            }

            public string Id
            {
                get { return "invite-provider-" + _index; }
            }

            public string Label
            {
                get
                {
                    string label = MenuButtonTextUtility.GetStandardButtonLabel(_button);
                    return string.IsNullOrWhiteSpace(label)
                        ? SpeechTextSanitizer.Normalize(_button != null ? _button.Text : string.Empty)
                        : label;
                }
            }

            public bool IsVisible
            {
                get { return MenuButtonAdapterBase.IsButtonVisible(_button); }
            }

            public bool IsEnabled
            {
                get { return _button != null && _button.Interactable; }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(_button as Component, _localization); }
            }

            public void FocusNative()
            {
                NativeSelectionUtility.Select(_button);
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(_button);
            }
        }
    }
}
