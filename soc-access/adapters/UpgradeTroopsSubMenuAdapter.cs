using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class UpgradeTroopsSubMenuAdapter
    {
        private static readonly FieldInfo EntriesField = AccessTools.Field(typeof(UpgradeTroopsSubMenu), "_entries");
        private static readonly FieldInfo NoUpgradableTroopsField = AccessTools.Field(typeof(UpgradeTroopsSubMenu), "_noUpgradableTroops");

        private readonly UpgradeTroopsSubMenu _subMenu;
        private readonly ILocalizationHandler _localization;

        public UpgradeTroopsSubMenuAdapter(UpgradeTroopsSubMenu subMenu, ILocalizationHandler localization)
        {
            _subMenu = subMenu;
            _localization = localization;
        }

        public bool IsPresent()
        {
            return _subMenu != null && _subMenu.gameObject != null && _subMenu.gameObject.activeInHierarchy;
        }

        public bool IsNoUpgradableTroopsVisible
        {
            get
            {
                GameObject container = GetField<GameObject>(_subMenu, NoUpgradableTroopsField);
                return container != null && container.activeInHierarchy;
            }
        }

        public string NoUpgradableTroopsText
        {
            get { return GetVisibleText(GetField<GameObject>(_subMenu, NoUpgradableTroopsField)); }
        }

        public IReadOnlyList<UpgradeEntry> GetEntries()
        {
            object dictionary = EntriesField != null && _subMenu != null ? EntriesField.GetValue(_subMenu) : null;
            IEnumerable enumerable = dictionary as IEnumerable;
            if (enumerable == null)
            {
                return new UpgradeEntry[0];
            }

            List<UpgradeEntry> result = new List<UpgradeEntry>();
            foreach (object pair in enumerable)
            {
                object value = GetPropertyValue(pair, "Value");
                UpgradeTroopsEntry entry = value as UpgradeTroopsEntry;
                if (entry != null && entry.gameObject != null && entry.gameObject.activeInHierarchy)
                {
                    result.Add(new UpgradeEntry(entry, _localization));
                }
            }

            return result;
        }

        private static object GetPropertyValue(object owner, string propertyName)
        {
            PropertyInfo property = owner != null ? owner.GetType().GetProperty(propertyName) : null;
            return property != null ? property.GetValue(owner, null) : null;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetVisibleText(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            UITextMesh[] textMeshes = root.GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                string text = GetText(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            return string.Join(". ", parts.ToArray());
        }

        internal sealed class UpgradeEntry
        {
            private static readonly FieldInfo CurrentTextField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_currentText");
            private static readonly FieldInfo CurrentAmountTextField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_currentTroopAmountText");
            private static readonly FieldInfo CurrentButtonField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_currentTroopButton");
            private static readonly FieldInfo TargetTextField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_targetTroopText");
            private static readonly FieldInfo TargetAmountTextField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_targetTroopAmountText");
            private static readonly FieldInfo TargetButtonField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_targetTroopButton");
            private static readonly FieldInfo SliderField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_slider");
            private static readonly FieldInfo SliderValueField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_sliderValue");
            private static readonly FieldInfo PurchaseButtonField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButton");
            private static readonly FieldInfo PurchaseMessageContainerField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButtonMessageContainer");
            private static readonly FieldInfo PurchaseMessageTextField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButtonMessageText");
            private static readonly FieldInfo CurrentTroopField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_currentTroop");
            private static readonly FieldInfo RecruitmentPoolField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_recruitmentPool");
            private static readonly FieldInfo FactionLookupField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_factionLookup");
            private static readonly FieldInfo TeamStateField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_teamState");
            private static readonly FieldInfo TargetUpgradeLevelField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_targetUpgradeLevel");
            private static readonly PropertyInfo TargetUpgradeLevelProperty = AccessTools.Property(typeof(UpgradeTroopsEntry), "TargetUpgradeLevel");

            private static readonly MethodInfo HandleSliderChangedMethod = AccessTools.Method(typeof(UpgradeTroopsEntry), "HandleSliderChanged");
            private static readonly MethodInfo HandlePurchaseClickedMethod = AccessTools.Method(typeof(UpgradeTroopsEntry), "HandlePurchaseClicked");

            private readonly UpgradeTroopsEntry _entry;
            private readonly ILocalizationHandler _localization;

            public UpgradeEntry(UpgradeTroopsEntry entry, ILocalizationHandler localization)
            {
                _entry = entry;
                _localization = localization;
            }

            public string IdPrefix
            {
                get { return "upgrade-troop-" + (_entry != null ? _entry.GetInstanceID().ToString() : "unknown"); }
            }

            public string CurrentTroopText
            {
                get { return MenuButtonTextUtility.JoinParts(GetText(GetField<UITextMesh>(_entry, CurrentTextField)), GetText(GetField<UITextMesh>(_entry, CurrentAmountTextField))); }
            }

            public string CurrentTroopName
            {
                get { return GetText(GetField<UITextMesh>(_entry, CurrentTextField)); }
            }

            public string TargetTroopText
            {
                get { return MenuButtonTextUtility.JoinParts(GetText(GetField<UITextMesh>(_entry, TargetTextField)), GetText(GetField<UITextMesh>(_entry, TargetAmountTextField))); }
            }

            public string TargetTroopName
            {
                get { return GetText(GetField<UITextMesh>(_entry, TargetTextField)); }
            }

            public string SliderLabel
            {
                get { return TargetTroopText; }
            }

            public int SliderValue
            {
                get
                {
                    object value = SliderValueField != null && _entry != null ? SliderValueField.GetValue(_entry) : null;
                    return value is int ? (int)value : Mathf.RoundToInt(GetSliderValue());
                }
            }

            public int SliderMinimum
            {
                get { UISlider slider = GetSlider(); return slider != null ? Mathf.RoundToInt(slider.SliderMinValue) : 0; }
            }

            public int SliderMaximum
            {
                get { UISlider slider = GetSlider(); return slider != null ? Mathf.RoundToInt(slider.SliderMaxValue) : 0; }
            }

            public int AvailableTroops
            {
                get { return SliderMaximum; }
            }

            public bool IsSliderEnabled
            {
                get { UISlider slider = GetSlider(); return slider != null && slider.Interactable; }
            }

            public bool IsSliderVisible
            {
                get
                {
                    UISlider slider = GetSlider();
                    Component component = slider as Component;
                    return component != null && component.gameObject.activeInHierarchy;
                }
            }

            public bool SetSliderValue(int value)
            {
                UISlider slider = GetSlider();
                if (slider == null || HandleSliderChangedMethod == null)
                {
                    return false;
                }

                int clamped = Mathf.Clamp(value, SliderMinimum, SliderMaximum);
                if (Mathf.RoundToInt(slider.SliderValue) == clamped)
                {
                    return false;
                }

                slider.SliderValue = clamped;
                HandleSliderChangedMethod.Invoke(_entry, new object[] { slider });
                return true;
            }

            public bool IsUpgradeVisible
            {
                get
                {
                    UIButton button = GetPurchaseButton();
                    Component component = button as Component;
                    return button != null && button.Active && component != null && component.gameObject.activeInHierarchy;
                }
            }

            public bool IsUpgradeEnabled
            {
                get
                {
                    UIButton button = GetPurchaseButton();
                    return button != null && button.Active && button.Interactable;
                }
            }

            public string UpgradeLabel
            {
                get
                {
                    string message = IsMessageVisible()
                        ? GetText(GetField<UITextMesh>(_entry, PurchaseMessageTextField))
                        : string.Empty;
                    if (!string.IsNullOrWhiteSpace(message))
                    {
                        return message;
                    }

                    IReadOnlyList<ResourceCostLine> costs = UpgradeCosts;
                    return costs.Count == 0
                        ? ModText.Get(_localization, ModStrings.Draft.Upgrade)
                        : ModText.Get(
                            _localization,
                            ModStrings.Draft.UpgradeForResources,
                            FormatCostLines(costs));
                }
            }

            private IReadOnlyList<ResourceCostLine> UpgradeCosts
            {
                get
                {
                    Cost cost = GetUpgradeCost();
                    if (cost == null || cost.CostEntries == null)
                    {
                        return new ResourceCostLine[0];
                    }

                    List<ResourceCostLine> lines = new List<ResourceCostLine>();
                    for (int i = 0; i < cost.SortedCostEntries.Count; i++)
                    {
                        Cost.CostEntry entry = cost.SortedCostEntries[i];
                        if (entry.Amount == 0 && entry.Type != ResourceType.Gold)
                        {
                            continue;
                        }

                        ITeamState team = GetField<ITeamState>(_entry, TeamStateField);
                        bool canAfford = team == null
                            || team.Resources == null
                            || team.Resources.CanAffordResource(entry.Type, entry.Amount);
                        lines.Add(new ResourceCostLine(entry.Type, entry.Amount, canAfford));
                    }

                    return lines;
                }
            }

            public Tooltip UpgradeTooltip
            {
                get { return Tooltip.ForComponent(GetPurchaseButton() as Component, _localization); }
            }

            public Tooltip CurrentTooltip
            {
                get { return Tooltip.ForComponent(GetField<UIButton>(_entry, CurrentButtonField) as Component, _localization); }
            }

            public Tooltip TargetTooltip
            {
                get { return Tooltip.ForComponent(GetField<UIButton>(_entry, TargetButtonField) as Component, _localization); }
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(_entry != null ? _entry.GetSelectable() : null);
            }

            public void FocusTarget()
            {
                NativeSelectionUtility.Select(GetField<UIButton>(_entry, TargetButtonField));
            }

            public bool Upgrade()
            {
                if (!IsUpgradeEnabled || HandlePurchaseClickedMethod == null)
                {
                    return false;
                }

                HandlePurchaseClickedMethod.Invoke(_entry, null);
                return true;
            }

            private bool IsMessageVisible()
            {
                Component container = GetField<Component>(_entry, PurchaseMessageContainerField);
                return container != null && container.gameObject.activeInHierarchy;
            }

            private float GetSliderValue()
            {
                UISlider slider = GetSlider();
                return slider != null ? slider.SliderValue : 0;
            }

            private Cost GetUpgradeCost()
            {
                ITroopState troop = GetField<ITroopState>(_entry, CurrentTroopField);
                IRecruitmentPoolComponent recruitmentPool = GetField<IRecruitmentPoolComponent>(_entry, RecruitmentPoolField);
                IFactionLookup factionLookup = GetField<IFactionLookup>(_entry, FactionLookupField);
                if (troop == null || troop.Reference == null || recruitmentPool == null || factionLookup == null)
                {
                    return null;
                }

                Cost cost = factionLookup.GetBaseTroopUpgradeCost(troop.Reference, SliderValue, TargetUpgradeLevel);
                if (cost != null)
                {
                    cost.Multiply(recruitmentPool.UnitCostMultiplier);
                }

                return cost;
            }

            private TroopUpgradeType TargetUpgradeLevel
            {
                get
                {
                    object propertyValue = TargetUpgradeLevelProperty != null && _entry != null
                        ? TargetUpgradeLevelProperty.GetValue(_entry, null)
                        : null;
                    if (propertyValue is TroopUpgradeType)
                    {
                        return (TroopUpgradeType)propertyValue;
                    }

                    object fieldValue = TargetUpgradeLevelField != null && _entry != null
                        ? TargetUpgradeLevelField.GetValue(_entry)
                        : null;
                    if (fieldValue is TroopUpgradeType)
                    {
                        return (TroopUpgradeType)fieldValue;
                    }

                    ITroopState troop = GetField<ITroopState>(_entry, CurrentTroopField);
                    return troop != null && troop.Reference != null
                        ? troop.Reference.UpgradeType + 1
                        : TroopUpgradeType.Upgraded;
                }
            }

            private string FormatCostLines(IReadOnlyList<ResourceCostLine> costs)
            {
                List<string> parts = new List<string>();
                for (int i = 0; i < costs.Count; i++)
                {
                    ResourceCostLine cost = costs[i];
                    if (cost != null)
                    {
                        parts.Add(ModText.Get(
                            _localization,
                            ModStrings.Common.ResourceAmount,
                            cost.Amount,
                            GetResourceName(cost.ResourceType)));
                    }
                }

                return ModText.JoinList(_localization, parts);
            }

            private string GetResourceName(ResourceType resourceType)
            {
                string fallback = FormatEnumName(resourceType.ToString());
                if (_localization == null)
                {
                    return fallback;
                }

                string key = "Common/Resource/" + resourceType;
                string text = _localization.GetText(key);
                return string.IsNullOrWhiteSpace(text) || text == key ? fallback : text;
            }

            private static string FormatEnumName(string value)
            {
                if (string.IsNullOrWhiteSpace(value))
                {
                    return string.Empty;
                }

                List<char> chars = new List<char>();
                for (int i = 0; i < value.Length; i++)
                {
                    char c = value[i];
                    if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                    {
                        chars.Add(' ');
                    }

                    chars.Add(char.ToLowerInvariant(c));
                }

                return new string(chars.ToArray());
            }

            private UISlider GetSlider()
            {
                return GetField<UISlider>(_entry, SliderField);
            }

            private UIButton GetPurchaseButton()
            {
                return GetField<UIButton>(_entry, PurchaseButtonField);
            }

            private sealed class ResourceCostLine
            {
                public ResourceCostLine(ResourceType resourceType, int amount, bool canAfford)
                {
                    ResourceType = resourceType;
                    Amount = amount;
                    CanAfford = canAfford;
                }

                public ResourceType ResourceType { get; private set; }
                public int Amount { get; private set; }
                public bool CanAfford { get; private set; }
            }
        }
    }
}
