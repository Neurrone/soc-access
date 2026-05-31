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
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class SendResourcePopupAdapter
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
            get { return GetText(GetField<IUITextMesh>(SendTextField)); }
        }

        public string RequestHeader
        {
            get { return GetText(GetField<IUITextMesh>(RequestTextField)); }
        }

        public bool IsRequestMenuVisible()
        {
            return IsPresent() && IsGameObjectVisible(GetField<Component>(RequestButtonsContainerField));
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

        private ResourceItem BuildSendItem(ResourceType type, FieldInfo buttonField, FieldInfo tooltipButtonField)
        {
            return new ResourceItem(this, "send", type, GetField<UIButton>(buttonField), GetField<UIButton>(tooltipButtonField));
        }

        private ResourceItem BuildRequestItem(ResourceType type, FieldInfo buttonField, FieldInfo tooltipButtonField)
        {
            return new ResourceItem(this, "request", type, GetField<UIButton>(buttonField), GetField<UIButton>(tooltipButtonField));
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _popup != null && field != null ? field.GetValue(_popup) as T : null;
        }

        private string GetResourceName(ResourceType type)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, "Common/Resource/" + type, string.Empty));
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

        internal sealed class ResourceItem
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

            public string Label
            {
                get
                {
                    string name = _adapter != null ? _adapter.GetResourceName(Type) : string.Empty;
                    string amount = MenuButtonTextUtility.GetDirectButtonText(_button);
                    return string.IsNullOrWhiteSpace(amount) ? name : name + " " + amount;
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
        }
    }
}
