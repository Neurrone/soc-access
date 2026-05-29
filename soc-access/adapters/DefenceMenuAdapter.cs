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
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DefenceMenuAdapter
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(DefenceMenu), "_async");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(DefenceMenu), "_tutorialButton");
        private static readonly FieldInfo MainTitleField = AccessTools.Field(typeof(DefenceMenu), "_mainTitle");
        private static readonly FieldInfo SubTitleField = AccessTools.Field(typeof(DefenceMenu), "_subTitle");
        private static readonly FieldInfo DefencePanelField = AccessTools.Field(typeof(DefenceMenu), "_defencePanel");
        private static readonly FieldInfo PurchaseTroopsSubMenuField = AccessTools.Field(typeof(DefenceMenu), "_purchaseTroopsSubMenu");
        private static readonly FieldInfo UpgradeTroopsSubMenuField = AccessTools.Field(typeof(DefenceMenu), "_upgradeTroopsSubMenu");
        private static readonly FieldInfo PurchaseTroopsButtonField = AccessTools.Field(typeof(DefenceMenu), "_purchaseTroopsButton");
        private static readonly FieldInfo UpgradeTroopsButtonField = AccessTools.Field(typeof(DefenceMenu), "_upgradeTroopsButton");
        private static readonly FieldInfo BackButtonField = AccessTools.Field(typeof(DefenceMenu), "_backButton");
        private static readonly FieldInfo MapEntityField = AccessTools.Field(typeof(DefenceMenu), "_mapEntity");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(DefenceMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(DefenceMenu), "_localizationHandler");

        private static readonly FieldInfo DefencePanelTroopsField = AccessTools.Field(typeof(DefencePanel), "_defencePanelTroops");
        private static readonly FieldInfo DefencePanelWielderField = AccessTools.Field(typeof(DefencePanel), "_defencePanelWielder");

        private static readonly FieldInfo SettlementTroopHudField = AccessTools.Field(typeof(DefencePanelTroops), "_mapEntityTroopHUD");
        private static readonly FieldInfo SettlementTroopsContainerField = AccessTools.Field(typeof(DefencePanelTroops), "_mapEntityTroopsContainer");
        private static readonly FieldInfo GarrisonTroopsField = AccessTools.Field(typeof(DefencePanelTroops), "_garrisonTroops");
        private static readonly FieldInfo BallistaTroopsField = AccessTools.Field(typeof(DefencePanelTroops), "_ballistaTroops");
        private static readonly FieldInfo TowerInfoTextField = AccessTools.Field(typeof(DefencePanelTroops), "_towerInfoText");
        private static readonly FieldInfo TowersLevelTextField = AccessTools.Field(typeof(DefencePanelTroops), "_towersLevelText");
        private static readonly FieldInfo TowerContainerField = AccessTools.Field(typeof(DefencePanelTroops), "_towerContainer");
        private static readonly FieldInfo NoTowersContainerField = AccessTools.Field(typeof(DefencePanelTroops), "_noTowersContainer");
        private static readonly FieldInfo TowerInfoContainerField = AccessTools.Field(typeof(DefencePanelTroops), "_towerInfoContainer");

        private static readonly FieldInfo TowerTooltipAreaField = AccessTools.Field(typeof(DefenceTowerEntry), "_tooltipArea");

        private static readonly MethodInfo ShowTopLevelMethod = AccessTools.Method(typeof(DefenceMenu), "ShowTopLevel");

        private readonly DefenceMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public DefenceMenuAdapter(DefenceMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public DefenceMenu Source
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int MapEntityId
        {
            get
            {
                IMapEntity mapEntity = GetField<IMapEntity>(_menu, MapEntityField);
                return mapEntity != null ? mapEntity.Id : -1;
            }
        }

        public bool IsTopLevelPresent()
        {
            return IsMenuOpen() && IsVisible(GetDefencePanel() as Component);
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
            get { return GetText(GetField<UITextMesh>(_menu, MainTitleField)); }
        }

        public string Subtitle
        {
            get { return GetText(GetField<UITextMesh>(_menu, SubTitleField)); }
        }

        public string SettlementDefendingTroopsLabel
        {
            get
            {
                string name = Title;
                if (string.IsNullOrWhiteSpace(name))
                {
                    name = Subtitle;
                }

                return string.IsNullOrWhiteSpace(name) ? "settlement defending troops" : name + " defending troops";
            }
        }

        public string DefendingTroopsLabel
        {
            get { return GetLocalizedText("Adventure/TroopManagementMenu/DefendingTroopsHeader", "Defending troops"); }
        }

        public DefencePanelWielderAdapter DefendingWielder
        {
            get { return new DefencePanelWielderAdapter(GetDefencePanelWielder(), _facade, _localization); }
        }

        public TroopHudAdapter SettlementTroops
        {
            get { return new TroopHudAdapter(GetField<TroopHUD>(GetDefencePanelTroops(), SettlementTroopHudField), _facade, _localization); }
        }

        public bool IsSettlementTroopsVisible()
        {
            return IsVisible(GetField<GameObject>(GetDefencePanelTroops(), SettlementTroopsContainerField));
        }

        public string GetTutorialButtonLabel()
        {
            string label = GetButtonLabel(GetTutorialButton());
            return label;
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetTutorialButton();
            return button != null && IsVisible(button as Component);
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        public string DraftLabel
        {
            get
            {
                return GetButtonLabel(GetDraftButton());
            }
        }

        public string UpgradeLabel
        {
            get
            {
                return GetButtonLabel(GetUpgradeButton());
            }
        }

        public bool IsDraftEnabled()
        {
            return IsButtonEnabled(GetDraftButton());
        }

        public bool IsUpgradeEnabled()
        {
            return IsButtonEnabled(GetUpgradeButton());
        }

        public bool IsDraftVisible()
        {
            return IsVisible(GetDraftButton() as Component);
        }

        public bool IsUpgradeVisible()
        {
            return IsVisible(GetUpgradeButton() as Component);
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
            if (_menu == null || ShowTopLevelMethod == null)
            {
                return false;
            }

            ShowTopLevelMethod.Invoke(_menu, null);
            return true;
        }

        public bool Close()
        {
            if (_menu == null || !IsMenuOpen())
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get { return new PurchaseTroopsSubMenuAdapter(GetPurchaseSubMenu(), _facade, _localization); }
        }

        public UpgradeTroopsSubMenuAdapter UpgradeTroops
        {
            get { return new UpgradeTroopsSubMenuAdapter(GetUpgradeSubMenu(), _localization); }
        }

        public string TowerSummary
        {
            get
            {
                return GetText(GetField<UITextMesh>(GetDefencePanelTroops(), TowersLevelTextField));
            }
        }

        public bool HasVisibleTowerSummary()
        {
            return IsVisible(GetField<GameObject>(GetDefencePanelTroops(), TowerInfoContainerField))
                && IsVisible(GetField<UITextMesh>(GetDefencePanelTroops(), TowersLevelTextField) as Component)
                && !string.IsNullOrWhiteSpace(TowerSummary);
        }

        public string TowerInfoText
        {
            get { return GetText(GetField<UITextMesh>(GetDefencePanelTroops(), TowerInfoTextField)); }
        }

        public bool HasVisibleNoTowersHelp()
        {
            return IsVisible(GetField<GameObject>(GetDefencePanelTroops(), NoTowersContainerField))
                && IsVisible(GetField<UITextMesh>(GetDefencePanelTroops(), TowerInfoTextField) as Component)
                && !string.IsNullOrWhiteSpace(TowerInfoText);
        }

        public IReadOnlyList<TowerItem> GetTowerItems()
        {
            Transform container = GetField<Transform>(GetDefencePanelTroops(), TowerContainerField);
            if (container == null)
            {
                return new TowerItem[0];
            }

            List<TowerItem> result = new List<TowerItem>();
            DefenceTowerEntry[] entries = container.GetComponentsInChildren<DefenceTowerEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                result.Add(new TowerItem("defences-tower-" + (i + 1), i + 1, entries[i], _localization));
            }

            return result;
        }

        public IReadOnlyList<DefenceSlotListAdapter.Slot> GetGarrisonSlots()
        {
            return new DefenceSlotListAdapter(GetField<List<TroopHUDEntry>>(GetDefencePanelTroops(), GarrisonTroopsField), _localization).GetSlots();
        }

        public IReadOnlyList<DefenceSlotListAdapter.Slot> GetBallistaSlots()
        {
            return new DefenceSlotListAdapter(GetField<List<TroopHUDEntry>>(GetDefencePanelTroops(), BallistaTroopsField), _localization).GetSlots();
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private bool IsMenuOpen()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && GetField<Async>(_menu, AsyncField) != null;
        }

        private DefencePanel GetDefencePanel()
        {
            return GetField<DefencePanel>(_menu, DefencePanelField);
        }

        private DefencePanelTroops GetDefencePanelTroops()
        {
            return GetField<DefencePanelTroops>(GetDefencePanel(), DefencePanelTroopsField);
        }

        private DefencePanelWielder GetDefencePanelWielder()
        {
            return GetField<DefencePanelWielder>(GetDefencePanel(), DefencePanelWielderField);
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

        private UIButton GetUpgradeButton()
        {
            return GetField<UIButton>(_menu, UpgradeTroopsButtonField);
        }

        private UIButton GetTutorialButton()
        {
            return GetField<UIButton>(_menu, TutorialButtonField);
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpeechTextSanitizer.Normalize(MenuButtonTextUtility.GetAllVisibleText(button));
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback));
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

        internal sealed class TowerItem
        {
            private readonly DefenceTowerEntry _entry;
            private readonly ILocalizationHandler _localization;
            private readonly int _number;

            public TowerItem(string id, int number, DefenceTowerEntry entry, ILocalizationHandler localization)
            {
                Id = id ?? string.Empty;
                _number = number;
                _entry = entry;
                _localization = localization;
            }

            public string Id { get; private set; }

            public string Label
            {
                get
                {
                    Component tooltipArea = GetField<Component>(_entry, TowerTooltipAreaField);
                    IDetails details;
                    if (NativeTooltipUtility.TryGetUiDetails(tooltipArea, out details) && details is DefenceTowerDetails towerDetails)
                    {
                        return SpeechTextSanitizer.Normalize(towerDetails.Header);
                    }

                    return ModText.Get(ModStrings.Screens.Tower, _number);
                }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(GetField<Component>(_entry, TowerTooltipAreaField), _localization); }
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(GetField<UIImage>(_entry, TowerTooltipAreaField));
            }
        }
    }
}
