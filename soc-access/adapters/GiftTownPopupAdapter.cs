using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class GiftTownPopupAdapter
    {
        private static readonly FieldInfo GiftButtonParentField = AccessTools.Field(typeof(GiftTownPopup), "_giftButtonParent");
        private static readonly FieldInfo RequestButtonParentField = AccessTools.Field(typeof(GiftTownPopup), "_requestButtonParent");
        private static readonly FieldInfo CloseButtonField = AccessTools.Field(typeof(GiftTownPopup), "_closeButton");
        private static readonly FieldInfo GiftHeaderField = AccessTools.Field(typeof(GiftTownPopup), "_giftHeader");
        private static readonly FieldInfo RequestHeaderField = AccessTools.Field(typeof(GiftTownPopup), "_requestHeader");

        private readonly GiftTownPopup _popup;
        private readonly ILocalizationHandler _localization;

        public GiftTownPopupAdapter(GiftTownPopup popup)
        {
            _popup = popup;
            _localization = GlobalLocalizationVariables.LocalizationHandler;
        }

        public bool IsPresent()
        {
            return _popup != null
                && IsLiveSceneObject(((Component)_popup).gameObject)
                && ((Component)_popup).gameObject.activeInHierarchy;
        }

        public string Title
        {
            get { return GiftHeader; }
        }

        public string GiftHeader
        {
            get { return GetText(GetField<IUITextMesh>(GiftHeaderField)); }
        }

        public string RequestHeader
        {
            get { return GetText(GetField<IUITextMesh>(RequestHeaderField)); }
        }

        public bool IsRequestMenuVisible()
        {
            return IsPresent() && IsGameObjectVisible(GetField<Component>(RequestButtonParentField));
        }

        public IReadOnlyList<TownItem> GetGiftTowns()
        {
            return GetTownItems("gift", GetField<Component>(GiftButtonParentField));
        }

        public IReadOnlyList<TownItem> GetRequestTowns()
        {
            return GetTownItems("request", GetField<Component>(RequestButtonParentField));
        }

        public bool Close()
        {
            if (_popup == null)
            {
                return false;
            }

            _popup.Hide();
            return true;
        }

        public bool CanClose()
        {
            return IsPresent();
        }

        public Tooltip CloseTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIButton>(CloseButtonField) as Component, _localization); }
        }

        public void FocusClose()
        {
            NativeSelectionUtility.Select(GetField<UIButton>(CloseButtonField));
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private IReadOnlyList<TownItem> GetTownItems(string rowId, Component parent)
        {
            List<TownItem> items = new List<TownItem>();
            if (!IsPresent() || parent == null || !IsGameObjectVisible(parent))
            {
                return items;
            }

            GiftTownButton[] buttons = parent.GetComponentsInChildren<GiftTownButton>(includeInactive: false);
            for (int i = 0; i < buttons.Length; i++)
            {
                GiftTownButton button = buttons[i];
                if (button == null || !IsGameObjectVisible(button))
                {
                    continue;
                }

                TownItem item = new TownItem(this, rowId, i, button);
                if (!string.IsNullOrWhiteSpace(item.Label))
                {
                    items.Add(item);
                }
            }

            return items;
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _popup != null && field != null ? field.GetValue(_popup) as T : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsGameObjectVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static string GetTooltipLabel(Tooltip tooltip)
        {
            IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
            if (lines == null || lines.Count == 0)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                string line = SpeechTextSanitizer.Normalize(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    parts.Add(line);
                }
            }

            return MenuButtonTextUtility.JoinParts(parts.ToArray());
        }

        public sealed class TownItem
        {
            private readonly GiftTownPopupAdapter _adapter;
            private readonly GiftTownButton _button;

            public TownItem(GiftTownPopupAdapter adapter, string rowId, int index, GiftTownButton button)
            {
                _adapter = adapter;
                _button = button;
                Id = "gift-town-popup-" + rowId + "-" + index;
            }

            public string Id { get; private set; }

            public string Label
            {
                get { return GetTooltipLabel(Tooltip); }
            }

            public bool IsVisible
            {
                get { return _button != null && IsGameObjectVisible(_button); }
            }

            public bool IsEnabled
            {
                get { return _button != null && _button.Button != null && _button.Button.Interactable; }
            }

            public Tooltip Tooltip
            {
                get
                {
                    Component component = IsEnabled
                        ? _button != null ? _button.Button as Component : null
                        : _button != null ? _button.ToolTipButton as Component : null;
                    return Tooltip.ForComponent(component, _adapter != null ? _adapter._localization : null);
                }
            }

            public void Focus()
            {
                if (IsEnabled)
                {
                    NativeSelectionUtility.Select(_button != null ? _button.Button : null);
                    return;
                }

                NativeSelectionUtility.Select(_button != null ? _button.ToolTipButton : null);
            }

            public bool Activate()
            {
                return _button != null && NativeSelectionUtility.Click(_button.Button);
            }
        }
    }
}
