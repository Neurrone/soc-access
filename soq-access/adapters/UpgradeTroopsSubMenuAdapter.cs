using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
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
            private static readonly FieldInfo PurchaseGoldAmountField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButtonGoldAmount");
            private static readonly FieldInfo PurchaseExoticAmountField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButtonExoticAmount");
            private static readonly FieldInfo PurchaseMessageContainerField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButtonMessageContainer");
            private static readonly FieldInfo PurchaseMessageTextField = AccessTools.Field(typeof(UpgradeTroopsEntry), "_purchaseButtonMessageText");

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

            public string TargetTroopText
            {
                get { return MenuButtonTextUtility.JoinParts(GetText(GetField<UITextMesh>(_entry, TargetTextField)), GetText(GetField<UITextMesh>(_entry, TargetAmountTextField))); }
            }

            public string SliderLabel
            {
                get { return "Amount to upgrade, " + TargetTroopText; }
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

            public bool IsSliderEnabled
            {
                get { UISlider slider = GetSlider(); return slider != null && slider.Interactable; }
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

                    string gold = GetText(GetField<UITextMesh>(_entry, PurchaseGoldAmountField));
                    string exotic = GetText(GetField<UITextMesh>(_entry, PurchaseExoticAmountField));
                    return string.IsNullOrWhiteSpace(exotic)
                        ? "Upgrade for " + gold + " gold"
                        : "Upgrade for " + gold + " gold and " + exotic;
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
