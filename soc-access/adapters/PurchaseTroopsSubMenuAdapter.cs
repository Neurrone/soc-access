using System;
using System.Collections.Generic;
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
using SongsOfConquest.Common.Gamestate.Faction;
using SongsOfConquest.Common.Gamestate.Unit;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PurchaseTroopsSubMenuAdapter
    {
        private static readonly FieldInfo CurrentEntriesField = AccessTools.Field(typeof(PurchaseTroopsSubMenu), "_currentEntries");

        private readonly PurchaseTroopsSubMenu _subMenu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public PurchaseTroopsSubMenuAdapter(PurchaseTroopsSubMenu subMenu, IClientAdventureFacade facade, ILocalizationHandler localization)
        {
            _subMenu = subMenu;
            _facade = facade;
            _localization = localization;
        }

        public bool IsPresent()
        {
            return _subMenu != null && _subMenu.gameObject != null && _subMenu.gameObject.activeInHierarchy;
        }

        public IReadOnlyList<RecruitEntry> GetRecruitEntries()
        {
            List<IPurchaseTroopsEntry> entries = GetField<List<IPurchaseTroopsEntry>>(_subMenu, CurrentEntriesField);
            if (entries == null || entries.Count == 0)
            {
                return new RecruitEntry[0];
            }

            List<RecruitEntry> result = new List<RecruitEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                IPurchaseTroopsEntry entry = entries[i];
                if (entry == null || entry.transform == null || !entry.transform.gameObject.activeInHierarchy)
                {
                    continue;
                }

                PurchaseTroopsEntry active = entry as PurchaseTroopsEntry;
                if (active != null)
                {
                    result.Add(new ActiveRecruitEntry(active, _facade, _localization));
                    continue;
                }

                PurchaseTroopsInactiveEntry inactive = entry as PurchaseTroopsInactiveEntry;
                if (inactive != null)
                {
                    result.Add(new InactiveRecruitEntry(inactive, _facade, _localization));
                }
            }

            return result;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        internal abstract class RecruitEntry
        {
            protected RecruitEntry(IPurchaseTroopsEntry entry, IClientAdventureFacade facade, ILocalizationHandler localization)
            {
                Entry = entry;
                Facade = facade;
                Localization = localization;
            }

            protected IPurchaseTroopsEntry Entry { get; private set; }
            protected IClientAdventureFacade Facade { get; private set; }
            protected ILocalizationHandler Localization { get; private set; }

            public abstract string IdPrefix { get; }
            public abstract string TroopName { get; }
            public abstract Tooltip Tooltip { get; }
            public abstract string NoTroopsText { get; }
            public abstract bool IsNoTroopsVisible { get; }

            public virtual bool IsSliderVisible { get { return false; } }
            public virtual bool IsSliderEnabled { get { return false; } }
            public virtual string SliderLabel { get { return string.Empty; } }
            public virtual int SliderValue { get { return 0; } }
            public virtual int SliderMinimum { get { return 0; } }
            public virtual int SliderMaximum { get { return 0; } }
            public virtual int AvailableTroops { get { return SliderMaximum; } }
            public virtual bool SetSliderValue(int value) { return false; }
            public virtual bool IsPurchaseVisible { get { return false; } }
            public virtual bool IsPurchaseEnabled { get { return false; } }
            public virtual Tooltip PurchaseTooltip { get { return null; } }
            public virtual IReadOnlyList<ResourceCostLine> PurchaseCosts { get { return new ResourceCostLine[0]; } }
            public virtual bool Purchase() { return false; }
            public virtual bool IsUpgradeInPoolVisible { get { return false; } }
            public virtual bool IsUpgradeInPoolEnabled { get { return false; } }
            public virtual Tooltip UpgradeInPoolTooltip { get { return null; } }
            public virtual bool UpgradeInPool() { return false; }
            public virtual bool IsEssenceMenuVisible { get { return false; } }
            public virtual TroopUpgradeType CurrentEssenceVariant { get { return TroopUpgradeType.Vanilla; } }
            public virtual bool SelectEssenceVariant(TroopUpgradeType upgradeType) { return false; }

            public void Focus()
            {
                NativeSelectionUtility.Select(Entry != null ? Entry.GetSelectable() : null);
            }

            protected TroopReference TroopReference
            {
                get { return Entry != null ? Entry.TroopReference : default(TroopReference); }
            }

            protected string BuildIdPrefix()
            {
                TroopReference reference = TroopReference;
                return "recruit-"
                    + reference.FactionIndex
                    + "-"
                    + reference.UnitIndex
                    + "-"
                    + reference.UpgradeType.ToString().ToLowerInvariant();
            }

            protected string ResolveTroopName()
            {
                try
                {
                    IUnitDefinition unit = GetNativeUnitDefinition();
                    string name = unit != null && Localization != null
                        ? Localization.GetText(unit.NameKey)
                        : string.Empty;
                    return string.IsNullOrWhiteSpace(name)
                        ? TroopReference.UpgradeType + " troop"
                        : SpeechTextSanitizer.Normalize(name);
                }
                catch (Exception)
                {
                    return "Troop";
                }
            }

            protected Tooltip BuildEntryTooltip()
            {
                Component target = Entry != null ? Entry.IntegrateTooltipable as Component : null;
                return Tooltip.ForComponent(target, Localization);
            }

            protected static bool IsVisible(Component component)
            {
                return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
            }

            private IUnitDefinition GetNativeUnitDefinition()
            {
                PurchaseTroopsEntry active = Entry as PurchaseTroopsEntry;
                if (active != null)
                {
                    FieldInfo field = AccessTools.Field(typeof(PurchaseTroopsEntry), "_unitDefinition");
                    return field != null ? field.GetValue(active) as IUnitDefinition : null;
                }

                PurchaseTroopsInactiveEntry inactive = Entry as PurchaseTroopsInactiveEntry;
                if (inactive != null)
                {
                    FieldInfo field = AccessTools.Field(typeof(PurchaseTroopsInactiveEntry), "_factionLookup");
                    IFactionLookup lookup = field != null ? field.GetValue(inactive) as IFactionLookup : null;
                    return lookup != null ? lookup.GetUnit(TroopReference) : null;
                }

                return null;
            }
        }

        internal sealed class ActiveRecruitEntry : RecruitEntry
        {
            private static readonly FieldInfo NoTroopsContainerField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_noTroopsContainer");
            private static readonly FieldInfo NoTroopsTextField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_noTroopsText");
            private static readonly FieldInfo SliderField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_slider");
            private static readonly FieldInfo AmountTextField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_amountText");
            private static readonly FieldInfo TotalAmountTextField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_totalAmountText");
            private static readonly FieldInfo PurchaseButtonField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_purchaseButton");
            private static readonly FieldInfo UpgradeButtonField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_upgradeButton");
            private static readonly FieldInfo EssenceTabsField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_essenceTabs");
            private static readonly FieldInfo GoldCostField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_goldCost");
            private static readonly FieldInfo ExoticCostField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_exoticCost");
            private static readonly FieldInfo SliderValueField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_sliderValue");
            private static readonly FieldInfo TeamStateField = AccessTools.Field(typeof(PurchaseTroopsEntry), "_teamState");

            private static readonly MethodInfo HandleSliderChangedMethod = AccessTools.Method(typeof(PurchaseTroopsEntry), "HandleSliderChanged");
            private static readonly MethodInfo HandlePurchaseClickedMethod = AccessTools.Method(typeof(PurchaseTroopsEntry), "HandlePurchaseClicked");
            private static readonly MethodInfo HandleUpgradeClickedMethod = AccessTools.Method(typeof(PurchaseTroopsEntry), "HandleUpgradeClicked");
            private static readonly MethodInfo HandleEssenceButtonClickedMethod = AccessTools.Method(typeof(PurchaseTroopsEntry), "HandleEssenceButtonClicked");

            private readonly PurchaseTroopsEntry _entry;

            public ActiveRecruitEntry(PurchaseTroopsEntry entry, IClientAdventureFacade facade, ILocalizationHandler localization)
                : base(entry, facade, localization)
            {
                _entry = entry;
            }

            public override string IdPrefix { get { return BuildIdPrefix(); } }
            public override string TroopName { get { return ResolveTroopName(); } }
            public override Tooltip Tooltip { get { return BuildEntryTooltip(); } }
            public override string NoTroopsText { get { return GetText(GetField<UITextMesh>(_entry, NoTroopsTextField)); } }
            public override bool IsNoTroopsVisible { get { return IsVisible(GetField<Component>(_entry, NoTroopsContainerField)); } }
            public override bool IsSliderVisible { get { return IsVisible(GetSlider()); } }
            public override bool IsSliderEnabled { get { UISlider slider = GetSlider(); return slider != null && slider.Interactable; } }

            public override string SliderLabel
            {
                get
                {
                    string amount = GetText(GetField<UITextMesh>(_entry, AmountTextField));
                    string total = GetText(GetField<UITextMesh>(_entry, TotalAmountTextField));
                    return SpeechTextSanitizer.Normalize((amount + " " + total).Trim());
                }
            }

            public override int SliderValue
            {
                get
                {
                    object value = SliderValueField != null ? SliderValueField.GetValue(_entry) : null;
                    return value is int ? (int)value : 0;
                }
            }

            public override int SliderMinimum { get { UISlider slider = GetSlider(); return slider != null ? Mathf.RoundToInt(slider.SliderMinValue) : 0; } }
            public override int SliderMaximum { get { UISlider slider = GetSlider(); return slider != null ? Mathf.RoundToInt(slider.SliderMaxValue) : 0; } }

            public override bool SetSliderValue(int value)
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

            public override bool IsPurchaseVisible { get { UIButton button = GetPurchaseButton(); return button != null && button.Active && IsVisible(button as Component); } }
            public override bool IsPurchaseEnabled { get { UIButton button = GetPurchaseButton(); return button != null && button.Active && button.Interactable; } }
            public override Tooltip PurchaseTooltip { get { return Tooltip.ForComponent(GetPurchaseButton() as Component, Localization); } }

            public override IReadOnlyList<ResourceCostLine> PurchaseCosts
            {
                get
                {
                    List<ResourceCostLine> lines = new List<ResourceCostLine>();
                    AddCostLine(lines, GetCost(GoldCostField), includeZeroGold: true);
                    AddCostLine(lines, GetCost(ExoticCostField), includeZeroGold: false);
                    return lines;
                }
            }

            public override bool Purchase()
            {
                if (!IsPurchaseEnabled || HandlePurchaseClickedMethod == null)
                {
                    return false;
                }

                HandlePurchaseClickedMethod.Invoke(_entry, null);
                return true;
            }

            public override bool IsUpgradeInPoolVisible { get { UIButton button = GetUpgradeButton(); return button != null && button.Active && IsVisible(button as Component); } }
            public override bool IsUpgradeInPoolEnabled { get { UIButton button = GetUpgradeButton(); return button != null && button.Active && button.Interactable; } }
            public override Tooltip UpgradeInPoolTooltip { get { return Tooltip.ForComponent(GetUpgradeButton() as Component, Localization); } }

            public override bool UpgradeInPool()
            {
                if (!IsUpgradeInPoolEnabled || HandleUpgradeClickedMethod == null)
                {
                    return false;
                }

                HandleUpgradeClickedMethod.Invoke(_entry, null);
                return true;
            }

            public override bool IsEssenceMenuVisible { get { return IsVisible(GetField<Component>(_entry, EssenceTabsField)); } }
            public override TroopUpgradeType CurrentEssenceVariant { get { return TroopReference.UpgradeType; } }

            public override bool SelectEssenceVariant(TroopUpgradeType upgradeType)
            {
                if (HandleEssenceButtonClickedMethod == null)
                {
                    return false;
                }

                HandleEssenceButtonClickedMethod.Invoke(_entry, new object[] { upgradeType });
                return true;
            }

            private UISlider GetSlider() { return GetField<UISlider>(_entry, SliderField); }
            private UIButton GetPurchaseButton() { return GetField<UIButton>(_entry, PurchaseButtonField); }
            private UIButton GetUpgradeButton() { return GetField<UIButton>(_entry, UpgradeButtonField); }

            private Cost.CostEntry? GetCost(FieldInfo field)
            {
                object value = field != null ? field.GetValue(_entry) : null;
                return value is Cost.CostEntry ? (Cost.CostEntry?)value : null;
            }

            private void AddCostLine(List<ResourceCostLine> lines, Cost.CostEntry? cost, bool includeZeroGold)
            {
                if (!cost.HasValue)
                {
                    return;
                }

                Cost.CostEntry value = cost.Value;
                int amount = value.Amount * SliderValue;
                if (amount == 0 && (!includeZeroGold || value.Type != ResourceType.Gold))
                {
                    return;
                }

                ITeamState team = GetField<ITeamState>(_entry, TeamStateField);
                bool canAfford = team == null || team.Resources == null || team.Resources.CanAffordResource(value.Type, amount);
                lines.Add(new ResourceCostLine(value.Type, amount, canAfford));
            }
        }

        internal sealed class InactiveRecruitEntry : RecruitEntry
        {
            private static readonly FieldInfo NoTroopsTextField = AccessTools.Field(typeof(PurchaseTroopsInactiveEntry), "_noTroopsText");

            private readonly PurchaseTroopsInactiveEntry _entry;

            public InactiveRecruitEntry(PurchaseTroopsInactiveEntry entry, IClientAdventureFacade facade, ILocalizationHandler localization)
                : base(entry, facade, localization)
            {
                _entry = entry;
            }

            public override string IdPrefix { get { return BuildIdPrefix(); } }
            public override string TroopName { get { return ResolveTroopName(); } }
            public override Tooltip Tooltip { get { return BuildEntryTooltip(); } }
            public override string NoTroopsText { get { return GetText(GetField<UITextMesh>(_entry, NoTroopsTextField)); } }
            public override bool IsNoTroopsVisible { get { return true; } }
        }

        internal sealed class ResourceCostLine
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
