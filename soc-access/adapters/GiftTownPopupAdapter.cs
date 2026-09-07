using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
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

        /// <summary>The cross the popup draws at its top right. The game only draws it outside gamepad
        /// mode (<c>GiftTownPopup.Show</c>), so it is absent rather than merely refusing there.</summary>
        public Component CloseButton
        {
            get { return GetField<UIButton>(CloseButtonField) as Component; }
        }

        public bool IsCloseVisible()
        {
            return IsPresent() && MenuButtonAdapterBase.IsButtonVisible(GetField<UIButton>(CloseButtonField));
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(GetField<UIButton>(CloseButtonField));
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
                if (!string.IsNullOrWhiteSpace(item.TypeName))
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

            /// <summary>The button the popup draws this town as.</summary>
            public Component Component
            {
                get { return _button; }
            }

            /// <summary>The town's own type name, which the game writes as the tooltip's TITLE
            /// (<c>GiftTownButton.SetTown</c> passes the entity's name key).</summary>
            public string TypeName
            {
                get
                {
                    IList<string> lines = TooltipLines;
                    return lines.Count > 0 ? lines[0] : string.Empty;
                }
            }

            /// <summary>The name the player gave the town, drawn under the type name. Empty where the
            /// town has none.</summary>
            public string CustomName
            {
                get
                {
                    IList<string> lines = TooltipLines;
                    int end = CustomNameEnd(lines);
                    return end > 1 ? lines[1] : string.Empty;
                }
            }

            /// <summary>Why the game is refusing this town, which it appends to the tooltip in its
            /// negative colour. Empty while the button is taking clicks.</summary>
            public string Reason
            {
                get
                {
                    IList<string> lines = TooltipLines;
                    int end = CustomNameEnd(lines);
                    return end < lines.Count ? lines[end] : string.Empty;
                }
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

            // The tooltip as the game wrote it, one drawn line at a time: the title, the custom name,
            // and - only where the game is refusing - the reason, which it appends to the custom name
            // behind a newline rather than as a line of its own.
            private IList<string> TooltipLines
            {
                get
                {
                    Tooltip tooltip = Tooltip;
                    return SpokenLines.Of(tooltip != null ? tooltip.TextLines : null);
                }
            }

            // Where the name ends and the refusal begins. The reason is always the LAST line and is
            // only ever there while the button refuses: the custom name key can resolve to nothing, so
            // reading either off a fixed index would hand the reason out as the town's name.
            private int CustomNameEnd(IList<string> lines)
            {
                return !IsEnabled && lines.Count > 1 ? lines.Count - 1 : lines.Count;
            }
        }
    }
}
