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
    public sealed class UpgradeTroopsSubMenuAdapter
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

        /// <summary>The panel that line is drawn in, which is what proves it is on the screen.
        /// </summary>
        public Component NoUpgradableTroopsContainer
        {
            get
            {
                GameObject container = GetField<GameObject>(_subMenu, NoUpgradableTroopsField);
                return container != null ? container.transform : null;
            }
        }

        public IReadOnlyList<UpgradeEntry> GetEntries()
        {
            object dictionary = EntriesField != null && _subMenu != null ? EntriesField.GetValue(_subMenu) : null;
            IEnumerable enumerable = dictionary as IEnumerable;
            if (enumerable == null)
            {
                return new UpgradeEntry[0];
            }

            List<UpgradeTroopsEntry> drawn = new List<UpgradeTroopsEntry>();
            foreach (object pair in enumerable)
            {
                object value = GetPropertyValue(pair, "Value");
                UpgradeTroopsEntry entry = value as UpgradeTroopsEntry;
                if (entry != null && entry.gameObject != null && entry.gameObject.activeInHierarchy)
                {
                    drawn.Add(entry);
                }
            }

            // The game keeps its entries in a dictionary and pools the cards, so the order they come
            // back in is not the order they are drawn in; the sibling order is.
            drawn.Sort((left, right) => left.transform.GetSiblingIndex().CompareTo(right.transform.GetSiblingIndex()));

            List<UpgradeEntry> result = new List<UpgradeEntry>(drawn.Count);
            for (int i = 0; i < drawn.Count; i++)
            {
                result.Add(new UpgradeEntry(drawn[i], _localization));
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

        public sealed class UpgradeEntry
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

            /// <summary>The card itself, which is what the menu draws for this upgrade.</summary>
            public Component Card
            {
                get { return _entry != null ? _entry.transform : null; }
            }

            public string CurrentTroopName
            {
                get { return GetText(GetField<UITextMesh>(_entry, CurrentTextField)); }
            }

            public string TargetTroopName
            {
                get { return GetText(GetField<UITextMesh>(_entry, TargetTextField)); }
            }

            /// <summary>The two numbers the card draws under its portraits: how many of the troop
            /// would be left as they are, and how many would be upgraded. The game writes them only
            /// when it recalculates, so a shortcut that moves the slider without notifying leaves
            /// them behind until it does.</summary>
            public string CurrentAmountText
            {
                get { return GetText(GetField<UITextMesh>(_entry, CurrentAmountTextField)); }
            }

            public string TargetAmountText
            {
                get { return GetText(GetField<UITextMesh>(_entry, TargetAmountTextField)); }
            }

            /// <summary>The two portraits, which the game wires as the shortcuts to none of them and
            /// as many as can be afforded.</summary>
            public Component CurrentTroopButton
            {
                get { return GetField<UIButton>(_entry, CurrentButtonField) as Component; }
            }

            public Component TargetTroopButton
            {
                get { return GetField<UIButton>(_entry, TargetButtonField) as Component; }
            }

            public Component Slider
            {
                get { return GetSlider() as Component; }
            }

            public Component UpgradeButton
            {
                get { return GetPurchaseButton() as Component; }
            }

            public bool ClickCurrentTroop()
            {
                return NativeSelectionUtility.Click(GetField<UIButton>(_entry, CurrentButtonField));
            }

            public bool ClickTargetTroop()
            {
                return NativeSelectionUtility.Click(GetField<UIButton>(_entry, TargetButtonField));
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

            /// <summary>The game's own reason it will not upgrade, which it writes over the price on
            /// the button rather than in a tooltip - and only for the one reason it explains at all,
            /// a troop with nowhere to go.</summary>
            public bool IsRefusalVisible
            {
                get
                {
                    Component container = GetField<Component>(_entry, PurchaseMessageContainerField);
                    return container != null && container.gameObject.activeInHierarchy;
                }
            }

            public string RefusalText
            {
                get { return GetText(GetField<UITextMesh>(_entry, PurchaseMessageTextField)); }
            }

            /// <summary>What upgrading the amount the slider is set to would cost.</summary>
            public IReadOnlyList<PurchaseTroopsSubMenuAdapter.ResourceCostLine> UpgradeCosts
            {
                get
                {
                    Cost cost = GetUpgradeCost();
                    if (cost == null || cost.CostEntries == null)
                    {
                        return new PurchaseTroopsSubMenuAdapter.ResourceCostLine[0];
                    }

                    List<PurchaseTroopsSubMenuAdapter.ResourceCostLine> lines = new List<PurchaseTroopsSubMenuAdapter.ResourceCostLine>();
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
                        lines.Add(new PurchaseTroopsSubMenuAdapter.ResourceCostLine(entry.Type, entry.Amount, canAfford));
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

            public bool Upgrade()
            {
                if (!IsUpgradeEnabled || HandlePurchaseClickedMethod == null)
                {
                    return false;
                }

                HandlePurchaseClickedMethod.Invoke(_entry, null);
                return true;
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

            private UISlider GetSlider()
            {
                return GetField<UISlider>(_entry, SliderField);
            }

            private UIButton GetPurchaseButton()
            {
                return GetField<UIButton>(_entry, PurchaseButtonField);
            }
        }
    }
}
