using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class SendResourcePopupAdapter : IPresent
    {
        private static readonly FieldInfo GoldButtonField = AccessTools.Field(typeof(SendResourcePopup), "_goldButton");
        private static readonly FieldInfo StoneButtonField = AccessTools.Field(typeof(SendResourcePopup), "_stoneButton");
        private static readonly FieldInfo WoodButtonField = AccessTools.Field(typeof(SendResourcePopup), "_woodButton");
        private static readonly FieldInfo GlimmerWeaveButtonField = AccessTools.Field(typeof(SendResourcePopup), "_glimmerWeaveButton");
        private static readonly FieldInfo AmberButtonField = AccessTools.Field(typeof(SendResourcePopup), "_amberButton");
        private static readonly FieldInfo CelestialButtonField = AccessTools.Field(typeof(SendResourcePopup), "_celestialButton");
        private static readonly FieldInfo RequestGoldButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestGoldButton");
        private static readonly FieldInfo RequestStoneButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestStoneButton");
        private static readonly FieldInfo RequestWoodButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestWoodButton");
        private static readonly FieldInfo RequestGlimmerWeaveButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestGlimmerWeaveButton");
        private static readonly FieldInfo RequestAmberButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestAmberButton");
        private static readonly FieldInfo RequestCelestialButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestCelestialButton");
        private static readonly FieldInfo GoldTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_goldTooltipButton");
        private static readonly FieldInfo StoneTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_stoneTooltipButton");
        private static readonly FieldInfo WoodTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_woodTooltipButton");
        private static readonly FieldInfo GlimmerWeaveTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_glimmerWeaveTooltipButton");
        private static readonly FieldInfo AmberTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_amberTooltipButton");
        private static readonly FieldInfo CelestialTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_celestialTooltipButton");
        private static readonly FieldInfo RequestGoldTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestGoldTooltipButton");
        private static readonly FieldInfo RequestStoneTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestStoneTooltipButton");
        private static readonly FieldInfo RequestWoodTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestWoodTooltipButton");
        private static readonly FieldInfo RequestGlimmerWeaveTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestGlimmerWeaveTooltipButton");
        private static readonly FieldInfo RequestAmberTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestAmberTooltipButton");
        private static readonly FieldInfo RequestCelestialTooltipButtonField = AccessTools.Field(typeof(SendResourcePopup), "_requestCelestialTooltipButton");
        private static readonly FieldInfo CloseButtonField = AccessTools.Field(typeof(SendResourcePopup), "_closeButton");
        private static readonly FieldInfo SendTextField = AccessTools.Field(typeof(SendResourcePopup), "_sendText");
        private static readonly FieldInfo RequestTextField = AccessTools.Field(typeof(SendResourcePopup), "_requestText");
        private static readonly FieldInfo RequestButtonsContainerField = AccessTools.Field(typeof(SendResourcePopup), "_requestButtonsContainer");

        private readonly SendResourcePopup _popup;
        private readonly ILocalizationHandler _localization;

        public SendResourcePopupAdapter(SendResourcePopup popup)
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
            get { return SendHeader; }
        }

        public string SendHeader
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<IUITextMesh>(_popup, SendTextField)); }
        }

        public string RequestHeader
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<IUITextMesh>(_popup, RequestTextField)); }
        }

        public bool IsRequestMenuVisible()
        {
            return IsPresent() && IsGameObjectVisible(Reflect.Get<Component>(_popup, RequestButtonsContainerField));
        }

        public IReadOnlyList<ResourceItem> GetSendResources()
        {
            return new[]
            {
                BuildSendItem(ResourceType.Gold, GoldButtonField, GoldTooltipButtonField),
                BuildSendItem(ResourceType.Stone, StoneButtonField, StoneTooltipButtonField),
                BuildSendItem(ResourceType.Wood, WoodButtonField, WoodTooltipButtonField),
                BuildSendItem(ResourceType.Glimmerweave, GlimmerWeaveButtonField, GlimmerWeaveTooltipButtonField),
                BuildSendItem(ResourceType.AncientAmber, AmberButtonField, AmberTooltipButtonField),
                BuildSendItem(ResourceType.CelestialOre, CelestialButtonField, CelestialTooltipButtonField)
            };
        }

        public IReadOnlyList<ResourceItem> GetRequestResources()
        {
            return new[]
            {
                BuildRequestItem(ResourceType.Gold, RequestGoldButtonField, RequestGoldTooltipButtonField),
                BuildRequestItem(ResourceType.Stone, RequestStoneButtonField, RequestStoneTooltipButtonField),
                BuildRequestItem(ResourceType.Wood, RequestWoodButtonField, RequestWoodTooltipButtonField),
                BuildRequestItem(ResourceType.Glimmerweave, RequestGlimmerWeaveButtonField, RequestGlimmerWeaveTooltipButtonField),
                BuildRequestItem(ResourceType.AncientAmber, RequestAmberButtonField, RequestAmberTooltipButtonField),
                BuildRequestItem(ResourceType.CelestialOre, RequestCelestialButtonField, RequestCelestialTooltipButtonField)
            };
        }

        /// <summary>The cross the popup draws at its top right. The game only draws it outside gamepad
        /// mode (<c>SendResourcePopup.Show</c>), so it is absent rather than merely refusing there.
        /// </summary>
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

        private ResourceItem BuildSendItem(ResourceType type, FieldInfo buttonField, FieldInfo tooltipButtonField)
        {
            return new ResourceItem(this, "send", type, Reflect.Get<UIButton>(_popup, buttonField), Reflect.Get<UIButton>(_popup, tooltipButtonField));
        }

        private ResourceItem BuildRequestItem(ResourceType type, FieldInfo buttonField, FieldInfo tooltipButtonField)
        {
            return new ResourceItem(this, "request", type, Reflect.Get<UIButton>(_popup, buttonField), Reflect.Get<UIButton>(_popup, tooltipButtonField));
        }

        private string GetResourceName(ResourceType type)
        {
            return SpokenLines.Clean(GameText.Get(_localization, "Common/Resource/" + type, string.Empty));
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsGameObjectVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        public sealed class ResourceItem
        {
            private readonly SendResourcePopupAdapter _adapter;
            private readonly UIButton _button;
            private readonly UIButton _tooltipButton;

            public ResourceItem(SendResourcePopupAdapter adapter, string rowId, ResourceType type, UIButton button, UIButton tooltipButton)
            {
                _adapter = adapter;
                _button = button;
                _tooltipButton = tooltipButton;
                Type = type;
                Id = "resource-popup-" + rowId + "-" + type.ToString().ToLowerInvariant();
            }

            public string Id { get; private set; }

            public ResourceType Type { get; private set; }

            /// <summary>The button the popup draws this entry as.</summary>
            public Component Component
            {
                get { return _button as Component; }
            }

            /// <summary>The resource's own name, from the game's resource table.</summary>
            public string Name
            {
                get { return _adapter != null ? _adapter.GetResourceName(Type) : string.Empty; }
            }

            /// <summary>How much one press moves, which the game fixes from its config and DRAWS on
            /// the button (<c>SendResourcePopup.SetupResourceButtons</c>).</summary>
            public string Amount
            {
                get { return MenuButtonTextUtility.GetDirectButtonText(_button); }
            }

            /// <summary>Why the game is refusing the transfer, which it puts in place of the
            /// resource's name in the tooltip. Empty while the button is taking clicks.</summary>
            public string Reason
            {
                get
                {
                    if (IsEnabled)
                    {
                        return string.Empty;
                    }

                    IList<string> lines = TooltipLines;
                    return lines.Count > 0 ? lines[0] : string.Empty;
                }
            }

            /// <summary>What the ally is holding of this resource, which the game writes under the
            /// name on a REQUEST button only (<c>SendResourcePopup.RefreshRequestResourceButton</c>).
            /// </summary>
            public string OtherTeamAmount
            {
                get
                {
                    if (!IsEnabled)
                    {
                        return string.Empty;
                    }

                    IList<string> lines = TooltipLines;
                    return lines.Count > 1 ? lines[1] : string.Empty;
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
                get
                {
                    Component component = IsEnabled ? _button as Component : _tooltipButton as Component;
                    return Tooltip.ForComponent(component, _adapter != null ? _adapter._localization : null);
                }
            }

            public void Focus()
            {
                if (IsEnabled)
                {
                    NativeSelectionUtility.Select(_button);
                    return;
                }

                NativeSelectionUtility.Select(_tooltipButton);
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(_button);
            }

            // The tooltip as the game wrote it, one drawn line at a time. Captured ONCE per item:
            // the reason and the ally's holding are two readings of the same capture, and the item
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
        }
    }
}
