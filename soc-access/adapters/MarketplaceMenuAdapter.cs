using System;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class MarketplaceMenuAdapter
    {
        private static readonly FieldInfo ButtonsField = AccessTools.Field(typeof(MarketplaceMenu), "_buttons");
        private static readonly FieldInfo TitleTextField = AccessTools.Field(typeof(MarketplaceMenu), "_titleText");
        private static readonly FieldInfo NumberOfMarketplacesTextField = AccessTools.Field(typeof(MarketplaceMenu), "_numberOfMarketplacesText");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(MarketplaceMenu), "_facade");
        private static readonly FieldInfo MarketplaceSystemField = AccessTools.Field(typeof(MarketplaceMenu), "_marketplaceSystem");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(MarketplaceMenu), "_localizationHandler");
        private static readonly FieldInfo TeamIdField = AccessTools.Field(typeof(MarketplaceMenu), "_teamId");
        private static readonly FieldInfo MarketplaceTypeField = AccessTools.Field(typeof(MarketplaceMenu), "_marketplaceType");

        private readonly MarketplaceMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly IMarketplaceSystem _marketplaceSystem;
        private readonly ILocalizationHandler _localization;
        private ResourceType _selectedResourceType = ResourceType.Gold;

        public MarketplaceMenuAdapter(MarketplaceMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(menu, FacadeField);
            _marketplaceSystem = GetField<IMarketplaceSystem>(menu, MarketplaceSystemField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public MarketplaceMenu Source
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public ResourceType SelectedResourceType
        {
            get { return _selectedResourceType; }
        }

        public bool IsPresent()
        {
            return _menu != null && ((Component)_menu).gameObject.activeInHierarchy;
        }

        public string Title
        {
            get
            {
                string title = GetText(GetField<UITextMesh>(_menu, TitleTextField));
                return string.IsNullOrWhiteSpace(title) ? string.Empty : title;
            }
        }

        public string OwningSummary
        {
            get { return GetText(GetField<UITextMesh>(_menu, NumberOfMarketplacesTextField)); }
        }

        public string Summary
        {
            get { return MenuButtonTextUtility.JoinParts(Title, OwningSummary); }
        }

        public IReadOnlyList<ResourceItem> GetResources()
        {
            return new[]
            {
                BuildResourceItem(ResourceType.Gold),
                BuildResourceItem(ResourceType.Stone),
                BuildResourceItem(ResourceType.Wood),
                BuildResourceItem(ResourceType.Glimmerweave),
                BuildResourceItem(ResourceType.AncientAmber),
                BuildResourceItem(ResourceType.CelestialOre)
            };
        }

        public void SelectResource(ResourceType resourceType)
        {
            _selectedResourceType = resourceType;
            if (resourceType == ResourceType.Gold)
            {
                HideNativeTooltip();
                return;
            }

            MarketplaceButton button = FindButton(resourceType, isBuyButton: false, amount: 1)
                ?? FindButton(resourceType, isBuyButton: false, amount: 5)
                ?? FindButton(resourceType, isBuyButton: true, amount: 1)
                ?? FindButton(resourceType, isBuyButton: true, amount: 5);
            NativeSelectionUtility.Select(button as Component);
        }

        public TradeActionItem GetTradeAction(bool isBuyButton, int amount)
        {
            return new TradeActionItem(
                this,
                isBuyButton,
                amount,
                () => FindButton(_selectedResourceType, isBuyButton, amount) != null,
                () => IsButtonEnabled(FindButton(_selectedResourceType, isBuyButton, amount)),
                () => ActivateButton(FindButton(_selectedResourceType, isBuyButton, amount)),
                () => FocusButton(FindButton(_selectedResourceType, isBuyButton, amount)));
        }

        public string TipText
        {
            get
            {
                if (_menu == null)
                {
                    return string.Empty;
                }

                UITextMesh[] textMeshes = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: false);
                for (int i = 0; i < textMeshes.Length; i++)
                {
                    UITextMesh textMesh = textMeshes[i];
                    if (textMesh != null && string.Equals(textMesh.gameObject.name, "TipText", StringComparison.OrdinalIgnoreCase))
                    {
                        return GetText(textMesh);
                    }
                }

                return string.Empty;
            }
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private ResourceItem BuildResourceItem(ResourceType resourceType)
        {
            string name = FormatResource(resourceType);
            int amount = GetResourceAmount(resourceType);
            return new ResourceItem(
                resourceType,
                name,
                amount);
        }

        public string GetResourceName(ResourceType resourceType)
        {
            return FormatResource(resourceType);
        }

        public int GetTradeGoldAmount(ResourceType resourceType, bool isBuyButton, int amount)
        {
            ResourceType from = isBuyButton ? ResourceType.Gold : resourceType;
            ResourceType to = isBuyButton ? resourceType : ResourceType.Gold;
            return GetGoldAmount(from, to, amount);
        }

        private int GetGoldAmount(ResourceType from, ResourceType to, int amount)
        {
            if (_marketplaceSystem == null)
            {
                return 0;
            }

            MarketplaceConversionResult conversion = _marketplaceSystem.GetConversionRate(MarketplaceType, TeamId, from, to, amount);
            return Math.Max(conversion.cost, conversion.gain);
        }

        private bool ActivateButton(MarketplaceButton button)
        {
            return NativeSelectionUtility.Click(button);
        }

        private bool FocusButton(MarketplaceButton button)
        {
            return NativeSelectionUtility.Select(button as Component);
        }

        private MarketplaceButton FindButton(ResourceType resourceType, bool isBuyButton, int amount)
        {
            if (resourceType == ResourceType.Gold)
            {
                return null;
            }

            IReadOnlyList<MarketplaceButton> buttons = GetButtons();
            for (int i = 0; i < buttons.Count; i++)
            {
                MarketplaceButton button = buttons[i];
                if (button != null
                    && button.ResourceType == resourceType
                    && button.IsBuyButton == isBuyButton
                    && button.Amount == amount)
                {
                    return button;
                }
            }

            return null;
        }

        private IReadOnlyList<MarketplaceButton> GetButtons()
        {
            List<MarketplaceButton> buttons = GetField<List<MarketplaceButton>>(_menu, ButtonsField);
            return buttons ?? new List<MarketplaceButton>();
        }

        private int GetResourceAmount(ResourceType resourceType)
        {
            ITeamState team = _facade != null && _facade.Teams != null ? _facade.Teams.Get(TeamId) : null;
            Resource resource = team != null && team.Resources != null ? team.Resources.GetResource(resourceType) : null;
            return resource != null ? resource.Amount : 0;
        }

        private int TeamId
        {
            get { return GetFieldValue(_menu, TeamIdField, _facade != null && _facade.Teams != null ? _facade.Teams.LocalTeamInControlId : -1); }
        }

        private MarketplaceType MarketplaceType
        {
            get { return GetFieldValue(_menu, MarketplaceTypeField, MarketplaceType.PlayerOwned); }
        }

        private string FormatResource(ResourceType resourceType)
        {
            string key = "Common/Resource/" + resourceType;
            string text = _localization != null ? _localization.GetText(key) : string.Empty;
            if (!string.IsNullOrWhiteSpace(text) && text != key)
            {
                return SpeechTextSanitizer.Normalize(text);
            }

            switch (resourceType)
            {
                case ResourceType.AncientAmber:
                    return "Ancient Amber";
                case ResourceType.CelestialOre:
                    return "Celestial Ore";
                default:
                    return resourceType.ToString();
            }
        }

        private static string FormatAmount(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture);
        }

        private static bool IsButtonEnabled(MarketplaceButton button)
        {
            return button != null && button.Active && button.Interactable;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        private static T GetFieldValue<T>(object owner, FieldInfo field, T fallback)
        {
            if (owner == null || field == null)
            {
                return fallback;
            }

            object value = field.GetValue(owner);
            return value is T ? (T)value : fallback;
        }

        internal sealed class ResourceItem
        {
            public ResourceItem(ResourceType resourceType, string resourceName, int amount)
            {
                ResourceType = resourceType;
                ResourceName = resourceName ?? string.Empty;
                Amount = amount;
            }

            public ResourceType ResourceType { get; private set; }
            public string ResourceName { get; private set; }
            public int Amount { get; private set; }
        }

        internal sealed class TradeActionItem
        {
            private readonly MarketplaceMenuAdapter _adapter;

            public TradeActionItem(
                MarketplaceMenuAdapter adapter,
                bool isBuyButton,
                int amount,
                Func<bool> isVisible,
                Func<bool> isEnabled,
                Func<bool> activate,
                Action focus)
            {
                _adapter = adapter;
                IsBuyButton = isBuyButton;
                Amount = amount;
                IsVisible = isVisible;
                IsEnabled = isEnabled;
                Activate = activate;
                Focus = focus;
            }

            public bool IsBuyButton { get; private set; }
            public int Amount { get; private set; }
            public ResourceType ResourceType { get { return _adapter != null ? _adapter.SelectedResourceType : ResourceType.Gold; } }
            public string ResourceName { get { return _adapter != null ? _adapter.GetResourceName(ResourceType) : string.Empty; } }
            public string GoldResourceName { get { return _adapter != null ? _adapter.GetResourceName(ResourceType.Gold) : string.Empty; } }
            public int GoldAmount { get { return _adapter != null ? _adapter.GetTradeGoldAmount(ResourceType, IsBuyButton, Amount) : 0; } }
            public Func<bool> IsVisible { get; private set; }
            public Func<bool> IsEnabled { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Action Focus { get; private set; }
        }
    }
}
