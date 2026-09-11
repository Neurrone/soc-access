using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class GiftTownPopupAdapter : IPresent
    {
        private static readonly FieldInfo GiftButtonParentField = AccessTools.Field(typeof(GiftTownPopup), "_giftButtonParent");
        private static readonly FieldInfo RequestButtonParentField = AccessTools.Field(typeof(GiftTownPopup), "_requestButtonParent");
        private static readonly FieldInfo CloseButtonField = AccessTools.Field(typeof(GiftTownPopup), "_closeButton");
        private static readonly FieldInfo GiftHeaderField = AccessTools.Field(typeof(GiftTownPopup), "_giftHeader");
        private static readonly FieldInfo RequestHeaderField = AccessTools.Field(typeof(GiftTownPopup), "_requestHeader");

        // The town buttons under each of the popup's two rows, walked at most once a frame: the
        // build asks for the gift row and the request row, and each button's live readout asks the
        // adapter again. Keyed on the frame rather than held, because the rows are rebuilt for
        // whichever player the popup was opened for.
        private readonly FrameSweep<GiftTownButton> _townButtons =
            new FrameSweep<GiftTownButton>("gift town popup rows", inactiveToo: false);

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
            get { return GetText(Reflect.Get<IUITextMesh>(_popup, GiftHeaderField)); }
        }

        public string RequestHeader
        {
            get { return GetText(Reflect.Get<IUITextMesh>(_popup, RequestHeaderField)); }
        }

        public bool IsRequestMenuVisible()
        {
            return IsPresent() && IsGameObjectVisible(Reflect.Get<Component>(_popup, RequestButtonParentField));
        }

        public IReadOnlyList<TownItem> GetGiftTowns()
        {
            return GetTownItems("gift", Reflect.Get<Component>(_popup, GiftButtonParentField));
        }

        public IReadOnlyList<TownItem> GetRequestTowns()
        {
            return GetTownItems("request", Reflect.Get<Component>(_popup, RequestButtonParentField));
        }

        /// <summary>The cross the popup draws at its top right. The game only draws it outside gamepad
        /// mode (<c>GiftTownPopup.Show</c>), so it is absent rather than merely refusing there.</summary>
        public Component CloseButton
        {
            get { return Reflect.Get<UIButton>(_popup, CloseButtonField) as Component; }
        }

        public bool IsCloseVisible()
        {
            return IsPresent() && MenuButtonAdapterBase.IsButtonVisible(Reflect.Get<UIButton>(_popup, CloseButtonField));
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(_popup, CloseButtonField));
        }

        public Tooltip CloseTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_popup, CloseButtonField) as Component, _localization); }
        }

        public void FocusClose()
        {
            NativeSelectionUtility.Select(Reflect.Get<UIButton>(_popup, CloseButtonField));
        }

        private IReadOnlyList<TownItem> GetTownItems(string rowId, Component parent)
        {
            List<TownItem> items = new List<TownItem>();
            if (!IsPresent() || parent == null || !IsGameObjectVisible(parent))
            {
                return items;
            }

            GiftTownButton[] buttons = _townButtons.Under(parent);
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

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
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
            // behind a newline rather than as a line of its own. Captured ONCE per item: the name,
            // the custom name and the reason are three readings of the same capture, and the item
            // itself is built anew on every build.
            private IList<string> _lines;

            private IList<string> TooltipLines
            {
                get
                {
                    if (_lines == null)
                    {
                        Tooltip tooltip = Tooltip;
                        _lines = SpokenLines.Of(tooltip != null ? tooltip.TextLines : null);
                    }

                    return _lines;
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
