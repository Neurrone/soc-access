using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TownInteractionMenuAdapter
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(TownInteractionMenu), "_async");
        private static readonly FieldInfo HeaderField = AccessTools.Field(typeof(TownInteractionMenu), "_wielderInteractHeader");
        private static readonly FieldInfo BackToTopButtonField = AccessTools.Field(typeof(TownInteractionMenu), "_backToTopButton");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(TownInteractionMenu), "_tutorialButton");
        private static readonly FieldInfo LandingPageContainerField = AccessTools.Field(typeof(TownInteractionMenu), "_landingPageContainer");
        private static readonly FieldInfo BuildingNameField = AccessTools.Field(typeof(TownInteractionMenu), "_buildingName");
        private static readonly FieldInfo PurchaseTroopsButtonField = AccessTools.Field(typeof(TownInteractionMenu), "_purchaseTroopsButton");
        private static readonly FieldInfo PurchaseTroopsSubMenuField = AccessTools.Field(typeof(TownInteractionMenu), "_purchaseTroopsSubMenu");
        private static readonly FieldInfo PurchaseTroopsDescriptionField = AccessTools.Field(typeof(TownInteractionMenu), "_purchaseTroopsDescriptionText");
        private static readonly FieldInfo UpgradeTroopsButtonField = AccessTools.Field(typeof(TownInteractionMenu), "_upgradeTroopsButton");
        private static readonly FieldInfo UpgradeTroopsSubMenuField = AccessTools.Field(typeof(TownInteractionMenu), "_upgradeTroopsSubMenu");
        private static readonly FieldInfo UpgradeTroopsDescriptionField = AccessTools.Field(typeof(TownInteractionMenu), "_upgraderTroopsDescriptionText");
        private static readonly FieldInfo DefencePanelTroopsField = AccessTools.Field(typeof(TownInteractionMenu), "_defencePanelTroops");
        private static readonly FieldInfo DefencePanelWielderField = AccessTools.Field(typeof(TownInteractionMenu), "_defencePanelWielder");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(TownInteractionMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(TownInteractionMenu), "_localizationHandler");
        private static readonly FieldInfo InteractingCommanderIdField = AccessTools.Field(typeof(TownInteractionMenu), "_interactingCommanderId");
        private static readonly FieldInfo MapEntityField = AccessTools.Field(typeof(TownInteractionMenu), "_mapEntity");

        private static readonly FieldInfo HeaderCloseButtonField = AccessTools.Field(typeof(WielderInteractHeader), "_closeButton");
        private static readonly FieldInfo HeaderPortraitField = AccessTools.Field(typeof(WielderInteractHeader), "_wielderPortrait");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");
        private static readonly FieldInfo HeaderCustomNameContainerField = AccessTools.Field(typeof(WielderInteractHeader), "_customNameContainer");
        private static readonly FieldInfo HeaderCustomNameTextField = AccessTools.Field(typeof(WielderInteractHeader), "_customNameText");

        private static readonly FieldInfo SettlementTroopHudField = AccessTools.Field(typeof(TownInteractDefencePanel), "_mapEntityTroopHUD");
        private static readonly FieldInfo GarrisonTroopsField = AccessTools.Field(typeof(TownInteractDefencePanel), "_garrisonTroops");
        private static readonly FieldInfo BallistaTroopsField = AccessTools.Field(typeof(TownInteractDefencePanel), "_ballistaTroops");

        private static readonly FieldInfo StoredCommanderField = AccessTools.Field(typeof(DefencePanelWielder), "_storedCommander");
        private static readonly FieldInfo NoStoredWielderContainerField = AccessTools.Field(typeof(DefencePanelWielder), "_noStoredWielderContainer");
        private static readonly FieldInfo StoredWielderContainerField = AccessTools.Field(typeof(DefencePanelWielder), "_storedWielderContainer");
        private static readonly FieldInfo StoreButtonField = AccessTools.Field(typeof(DefencePanelWielder), "_storeButton");
        private static readonly FieldInfo EjectButtonField = AccessTools.Field(typeof(DefencePanelWielder), "_ejectButton");
        private static readonly FieldInfo TradeButtonField = AccessTools.Field(typeof(DefencePanelWielder), "_tradeButton");

        private readonly TownInteractionMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public TownInteractionMenuAdapter(TownInteractionMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int VisitingCommanderId
        {
            get { return GetInteractingCommanderId(); }
        }

        public int SettlementMapEntityId
        {
            get
            {
                IMapEntity mapEntity = GetField<IMapEntity>(_menu, MapEntityField);
                return mapEntity != null ? mapEntity.Id : -1;
            }
        }

        public bool IsTopLevelPresent()
        {
            return IsMenuOpen() && IsVisible(GetField<GameObject>(_menu, LandingPageContainerField));
        }

        public bool IsDraftPresent()
        {
            PurchaseTroopsSubMenu subMenu = GetPurchaseSubMenu();
            return IsMenuOpen() && subMenu != null && subMenu.gameObject.activeInHierarchy;
        }

        public bool IsUpgradePresent()
        {
            UpgradeTroopsSubMenu subMenu = GetUpgradeSubMenu();
            return IsMenuOpen() && subMenu != null && subMenu.gameObject.activeInHierarchy;
        }

        public string Title
        {
            get { return GetText(GetField<UITextMesh>(_menu, BuildingNameField)); }
        }

        public bool IsCustomNameVisible
        {
            get { return IsVisible(GetField<GameObject>(GetHeader(), HeaderCustomNameContainerField)); }
        }

        public string CustomName
        {
            get { return GetText(GetField<UITextMesh>(GetHeader(), HeaderCustomNameTextField)); }
        }

        public string VisitingWielderName
        {
            get
            {
                int commanderId = GetInteractingCommanderId();
                string name = commanderId >= 0 && _facade != null ? _facade.Commanders.GetName(commanderId) : string.Empty;
                return SpeechTextSanitizer.Normalize(name);
            }
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetTutorialButton();
            return button != null && IsVisible(button as Component);
        }

        public string GetTutorialButtonLabel()
        {
            string label = GetButtonLabel(GetTutorialButton());
            return string.IsNullOrWhiteSpace(label) ? "Tutorial available" : label;
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        public Tooltip VisitingWielderTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIImage>(GetHeader(), HeaderPortraitField) as Component, _localization); }
        }

        public TroopHudAdapter VisitingTroops
        {
            get { return new TroopHudAdapter(GetField<TroopHUD>(GetHeader(), HeaderTroopHudField), _facade, _localization, BuildVisitingArmyLabel()); }
        }

        public TroopHudAdapter SettlementTroops
        {
            get { return new TroopHudAdapter(GetField<TroopHUD>(GetDefencePanelTroops(), SettlementTroopHudField), _facade, _localization, "settlement troops"); }
        }

        public string DraftLabel
        {
            get { return MenuButtonTextUtility.JoinParts(GetButtonLabel(GetDraftButton()), GetText(GetField<UITextMesh>(_menu, PurchaseTroopsDescriptionField))); }
        }

        public string UpgradeLabel
        {
            get { return MenuButtonTextUtility.JoinParts(GetButtonLabel(GetUpgradeButton()), GetText(GetField<UITextMesh>(_menu, UpgradeTroopsDescriptionField))); }
        }

        public bool IsDraftEnabled()
        {
            return IsButtonEnabled(GetDraftButton());
        }

        public bool IsUpgradeEnabled()
        {
            return IsButtonEnabled(GetUpgradeButton());
        }

        public Tooltip DraftTooltip
        {
            get { return Tooltip.ForComponent(GetDraftButton() as Component, _localization); }
        }

        public Tooltip UpgradeTooltip
        {
            get { return Tooltip.ForComponent(GetUpgradeButton() as Component, _localization); }
        }

        public bool ActivateDraft()
        {
            return NativeSelectionUtility.Click(GetDraftButton());
        }

        public bool ActivateUpgrade()
        {
            return NativeSelectionUtility.Click(GetUpgradeButton());
        }

        public void FocusDraft()
        {
            NativeSelectionUtility.Select(GetDraftButton());
        }

        public void FocusUpgrade()
        {
            NativeSelectionUtility.Select(GetUpgradeButton());
        }

        public bool BackToTop()
        {
            return NativeSelectionUtility.Click(GetField<UIButton>(_menu, BackToTopButtonField));
        }

        public bool Close()
        {
            if (_menu == null || !IsMenuOpen())
            {
                return false;
            }

            _menu.Close();
            return true;
        }

        public string CloseLabel
        {
            get
            {
                string label = GetButtonLabel(GetField<UIButton>(GetHeader(), HeaderCloseButtonField));
                return string.IsNullOrWhiteSpace(label) ? "Close" : label;
            }
        }

        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get { return new PurchaseTroopsSubMenuAdapter(GetPurchaseSubMenu(), _facade, _localization); }
        }

        public UpgradeTroopsSubMenuAdapter UpgradeTroops
        {
            get { return new UpgradeTroopsSubMenuAdapter(GetUpgradeSubMenu(), _localization); }
        }

        public ArmyExchangeGridWidget BuildArmyExchangeGrid()
        {
            return new ArmyExchangeGridWidget(
                "settlement-army-exchange-grid",
                BuildVisitingArmyLabel(),
                VisitingTroops.BuildArmyExchangeSlots(),
                SettlementTroops.BuildArmyExchangeSlots(),
                Drop);
        }

        public string DefendingWielderStatus
        {
            get
            {
                DefencePanelWielder panel = GetDefencePanelWielder();
                ICommanderState storedCommander = GetField<ICommanderState>(panel, StoredCommanderField);
                if (storedCommander != null && _facade != null)
                {
                    string name = SpeechTextSanitizer.Normalize(_facade.Commanders.GetName(storedCommander.Id));
                    return string.IsNullOrWhiteSpace(name) ? string.Empty : "Defending wielder: " + name;
                }

                return GetVisibleText(GetField<GameObject>(panel, NoStoredWielderContainerField));
            }
        }

        public string StoreLabel
        {
            get { return GetButtonLabel(GetStoreButton()); }
        }

        public string EjectLabel
        {
            get { return GetButtonLabel(GetEjectButton()); }
        }

        public string TradeLabel
        {
            get { return GetButtonLabel(GetTradeButton()); }
        }

        public bool IsStoreVisible()
        {
            return IsVisible(GetStoreButton() as Component);
        }

        public bool IsEjectVisible()
        {
            return IsVisible(GetEjectButton() as Component);
        }

        public bool IsTradeVisible()
        {
            return IsVisible(GetTradeButton() as Component);
        }

        public bool IsStoreEnabled()
        {
            return IsButtonEnabled(GetStoreButton());
        }

        public bool IsEjectEnabled()
        {
            return IsButtonEnabled(GetEjectButton());
        }

        public bool IsTradeEnabled()
        {
            return IsButtonEnabled(GetTradeButton());
        }

        public Tooltip StoreTooltip
        {
            get { return Tooltip.ForComponent(GetStoreButton() as Component, _localization); }
        }

        public Tooltip EjectTooltip
        {
            get { return Tooltip.ForComponent(GetEjectButton() as Component, _localization); }
        }

        public Tooltip TradeTooltip
        {
            get { return Tooltip.ForComponent(GetTradeButton() as Component, _localization); }
        }

        public bool ActivateStore()
        {
            return NativeSelectionUtility.Click(GetStoreButton());
        }

        public bool ActivateEject()
        {
            return NativeSelectionUtility.Click(GetEjectButton());
        }

        public bool ActivateTrade()
        {
            return NativeSelectionUtility.Click(GetTradeButton());
        }

        public void FocusStore()
        {
            NativeSelectionUtility.Select(GetStoreButton());
        }

        public void FocusEject()
        {
            NativeSelectionUtility.Select(GetEjectButton());
        }

        public void FocusTrade()
        {
            NativeSelectionUtility.Select(GetTradeButton());
        }

        public IReadOnlyList<DefenseSlot> GetGarrisonSlots()
        {
            return BuildDefenseSlots(GetField<List<TroopHUDEntry>>(GetDefencePanelTroops(), GarrisonTroopsField), "garrison");
        }

        public IReadOnlyList<DefenseSlot> GetBallistaSlots()
        {
            return BuildDefenseSlots(GetField<List<TroopHUDEntry>>(GetDefencePanelTroops(), BallistaTroopsField), "ballista");
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private bool Drop(ArmyExchangeGridWidget.SlotWidget source, ArmyExchangeGridWidget.SlotWidget target)
        {
            TroopHudAdapter.SlotItem sourceItem = source != null ? source.NativeSource as TroopHudAdapter.SlotItem : null;
            TroopHudAdapter.SlotItem targetItem = target != null ? target.NativeSource as TroopHudAdapter.SlotItem : null;
            return sourceItem != null && sourceItem.DropTo(targetItem);
        }

        private bool IsMenuOpen()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && GetField<Async>(_menu, AsyncField) != null;
        }

        private WielderInteractHeader GetHeader()
        {
            return GetField<WielderInteractHeader>(_menu, HeaderField);
        }

        private TownInteractDefencePanel GetDefencePanelTroops()
        {
            return GetField<TownInteractDefencePanel>(_menu, DefencePanelTroopsField);
        }

        private DefencePanelWielder GetDefencePanelWielder()
        {
            return GetField<DefencePanelWielder>(_menu, DefencePanelWielderField);
        }

        private PurchaseTroopsSubMenu GetPurchaseSubMenu()
        {
            return GetField<PurchaseTroopsSubMenu>(_menu, PurchaseTroopsSubMenuField);
        }

        private UpgradeTroopsSubMenu GetUpgradeSubMenu()
        {
            return GetField<UpgradeTroopsSubMenu>(_menu, UpgradeTroopsSubMenuField);
        }

        private UIButton GetDraftButton()
        {
            return GetField<UIButton>(_menu, PurchaseTroopsButtonField);
        }

        private UIButton GetTutorialButton()
        {
            return GetField<UIButton>(_menu, TutorialButtonField);
        }

        private UIButton GetUpgradeButton()
        {
            return GetField<UIButton>(_menu, UpgradeTroopsButtonField);
        }

        private UIButton GetStoreButton()
        {
            return GetField<UIButton>(GetDefencePanelWielder(), StoreButtonField);
        }

        private UIButton GetEjectButton()
        {
            return GetField<UIButton>(GetDefencePanelWielder(), EjectButtonField);
        }

        private UIButton GetTradeButton()
        {
            return GetField<UIButton>(GetDefencePanelWielder(), TradeButtonField);
        }

        private int GetInteractingCommanderId()
        {
            object value = InteractingCommanderIdField != null ? InteractingCommanderIdField.GetValue(_menu) : null;
            return value is int ? (int)value : -1;
        }

        private string BuildVisitingArmyLabel()
        {
            string name = VisitingWielderName;
            return string.IsNullOrWhiteSpace(name) ? "visiting wielder army" : name + "'s army";
        }

        private IReadOnlyList<DefenseSlot> BuildDefenseSlots(List<TroopHUDEntry> entries, string idPrefix)
        {
            List<DefenseSlot> slots = new List<DefenseSlot>();
            if (entries == null)
            {
                return slots;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                TroopHUDEntry entry = entries[i];
                slots.Add(new DefenseSlot(idPrefix + "-slot-" + (i + 1), idPrefix, i + 1, entry, _localization));
            }

            return slots;
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpeechTextSanitizer.Normalize(MenuButtonTextUtility.GetAllVisibleText(button));
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

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable && IsVisible(button as Component);
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class DefenseSlot
        {
            private readonly TroopHUDEntry _entry;
            private readonly ILocalizationHandler _localization;
            private readonly string _slotType;
            private readonly int _slotNumber;

            public DefenseSlot(string id, string slotType, int slotNumber, TroopHUDEntry entry, ILocalizationHandler localization)
            {
                Id = id ?? string.Empty;
                _slotType = slotType ?? string.Empty;
                _slotNumber = slotNumber;
                _entry = entry;
                _localization = localization;
            }

            public string Id { get; private set; }

            public string Label
            {
                get
                {
                    Tooltip tooltip = Tooltip;
                    if (tooltip != null && tooltip.TextLines != null && tooltip.TextLines.Count > 0)
                    {
                        return SpeechTextSanitizer.Normalize(string.Join(". ", tooltip.TextLines));
                    }

                    return "Empty " + _slotType + " slot " + _slotNumber;
                }
            }

            public Tooltip Tooltip
            {
                get
                {
                    if (!IsVisible(_entry as Component))
                    {
                        return null;
                    }

                    return Tooltip.ForComponent(_entry != null ? _entry.GetSelectable() : null, _localization);
                }
            }

            public void Focus()
            {
                if (IsVisible(_entry as Component))
                {
                    NativeSelectionUtility.Select(_entry.GetSelectable());
                }
            }
        }
    }
}
