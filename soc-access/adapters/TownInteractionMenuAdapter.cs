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
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TownInteractionMenuAdapter
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
        private static readonly FieldInfo MoveToDefenceButtonField = AccessTools.Field(typeof(TownInteractDefencePanel), "_moveToDefenceButton");
        private static readonly FieldInfo MoveToWielderButtonField = AccessTools.Field(typeof(TownInteractDefencePanel), "_moveToWielderButton");
        private static readonly FieldInfo UpgradesAvailableIndicatorField = AccessTools.Field(typeof(TownInteractionMenu), "_upgradesAvailableIndicator");
        private static readonly FieldInfo UpgradesAvailableNumberField = AccessTools.Field(typeof(TownInteractionMenu), "_upgradesAvailableNumber");
        private static readonly FieldInfo GarrisonTroopsField = AccessTools.Field(typeof(TownInteractDefencePanel), "_garrisonTroops");
        private static readonly FieldInfo BallistaTroopsField = AccessTools.Field(typeof(TownInteractDefencePanel), "_ballistaTroops");

        private readonly TownInteractionMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private WielderInteract _wielder;
        private DefencePanelWielderAdapter _defendingWielder;
        private DefenceSlotListAdapter _garrison;
        private DefenceSlotListAdapter _ballistae;
        private PurchaseTroopsSubMenuAdapter _purchaseTroops;
        private UpgradeTroopsSubMenuAdapter _upgradeTroops;
        private TroopHudAdapter _settlementTroops;

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
                return SpokenLines.Clean(name);
            }
        }

        /// <summary>The band the menu hangs across its top: the visiting wielder, their army and the
        /// close cross.</summary>
        public WielderInteract Wielder
        {
            get
            {
                WielderInteractHeader header = GetHeader();
                if (_wielder == null || !ReferenceEquals(_wielder.Header, header))
                {
                    _wielder = new WielderInteract(header, _facade, _localization);
                }

                return _wielder;
            }
        }

        public Component TutorialButton
        {
            get { return GetTutorialButton() as Component; }
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetTutorialButton();
            return button != null && IsVisible(button as Component);
        }

        public string GetTutorialButtonLabel()
        {
            return GetButtonLabel(GetTutorialButton());
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
            get { return new TroopHudAdapter(GetField<TroopHUD>(GetHeader(), HeaderTroopHudField), _facade, _localization); }
        }

        /// <summary>The settlement's own army. Kept: the adapter wakes the game's drag ghost when it
        /// is made, and the rows are rebuilt on every navigation operation.</summary>
        public TroopHudAdapter SettlementTroops
        {
            get
            {
                TroopHUD hud = GetField<TroopHUD>(GetDefencePanelTroops(), SettlementTroopHudField);
                if (hud == null)
                {
                    return null;
                }

                if (_settlementTroops == null || !ReferenceEquals(_settlementTroops.Hud, hud))
                {
                    _settlementTroops = new TroopHudAdapter(hud, _facade, _localization);
                }

                return _settlementTroops;
            }
        }

        /// <summary>The panel that draws the settlement's defences, whose paint state vouches for the
        /// rows read out of it.</summary>
        public Component TroopsPanel
        {
            get { return GetDefencePanelTroops(); }
        }

        /// <summary>The button that hands the visiting wielder's whole army to the defences, and the
        /// one that takes it back. The game draws both only for a visiting wielder and turns them off
        /// when it would refuse the move.</summary>
        public Component MoveToDefenceButton
        {
            get { return GetMoveToDefenceButton() as Component; }
        }

        public Component MoveToWielderButton
        {
            get { return GetMoveToWielderButton() as Component; }
        }

        public bool IsMoveToDefenceEnabled()
        {
            return IsButtonEnabled(GetMoveToDefenceButton());
        }

        public bool IsMoveToWielderEnabled()
        {
            return IsButtonEnabled(GetMoveToWielderButton());
        }

        public Tooltip MoveToDefenceTooltip
        {
            get { return Tooltip.ForComponent(GetMoveToDefenceButton() as Component, _localization); }
        }

        public Tooltip MoveToWielderTooltip
        {
            get { return Tooltip.ForComponent(GetMoveToWielderButton() as Component, _localization); }
        }

        public bool ActivateMoveToDefence()
        {
            return NativeSelectionUtility.Click(GetMoveToDefenceButton());
        }

        public bool ActivateMoveToWielder()
        {
            return NativeSelectionUtility.Click(GetMoveToWielderButton());
        }

        public void FocusMoveToDefence()
        {
            NativeSelectionUtility.Select(GetMoveToDefenceButton());
        }

        public void FocusMoveToWielder()
        {
            NativeSelectionUtility.Select(GetMoveToWielderButton());
        }

        public string DraftLabel
        {
            get { return GetButtonLabel(GetDraftButton()); }
        }

        public string UpgradeLabel
        {
            get { return GetButtonLabel(GetUpgradeButton()); }
        }

        /// <summary>The line the menu draws under each button, always: what drafting or upgrading here
        /// would do, or the game's own reason there is nothing to do.</summary>
        public string DraftDescription
        {
            get { return GetText(GetField<UITextMesh>(_menu, PurchaseTroopsDescriptionField)); }
        }

        public string UpgradeDescription
        {
            get { return GetText(GetField<UITextMesh>(_menu, UpgradeTroopsDescriptionField)); }
        }

        /// <summary>The number the menu stamps on the Upgrade button while something can be upgraded;
        /// empty while the game hides that indicator.</summary>
        public string UpgradesAvailableNumber
        {
            get
            {
                return IsVisible(GetField<GameObject>(_menu, UpgradesAvailableIndicatorField))
                    ? GetText(GetField<UITextMesh>(_menu, UpgradesAvailableNumberField))
                    : string.Empty;
            }
        }

        public Component DraftButton
        {
            get { return GetDraftButton() as Component; }
        }

        public Component UpgradeButton
        {
            get { return GetUpgradeButton() as Component; }
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

        /// <summary>The button the menu draws over a sub-page to get back to its landing page, and the
        /// word the prefab has written on it. The menu hides it on the landing page itself.</summary>
        public Component BackButton
        {
            get { return GetField<UIButton>(_menu, BackToTopButtonField) as Component; }
        }

        public string BackLabel
        {
            get { return GetButtonLabel(GetField<UIButton>(_menu, BackToTopButtonField)); }
        }

        public bool IsBackVisible()
        {
            return IsVisible(BackButton);
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

        /// <summary>The draft sub-page. Kept, so the page's build reads one adapter rather than a new
        /// one every frame.</summary>
        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get
            {
                PurchaseTroopsSubMenu subMenu = GetPurchaseSubMenu();
                if (_purchaseTroops == null || !ReferenceEquals(_purchaseTroops.SubMenu, subMenu))
                {
                    _purchaseTroops = new PurchaseTroopsSubMenuAdapter(subMenu, _facade, _localization);
                }

                return _purchaseTroops;
            }
        }

        /// <summary>The upgrade sub-page. Kept for the same reason as the draft one.</summary>
        public UpgradeTroopsSubMenuAdapter UpgradeTroops
        {
            get
            {
                UpgradeTroopsSubMenu subMenu = GetUpgradeSubMenu();
                if (_upgradeTroops == null || !ReferenceEquals(_upgradeTroops.SubMenu, subMenu))
                {
                    _upgradeTroops = new UpgradeTroopsSubMenuAdapter(subMenu, _localization);
                }

                return _upgradeTroops;
            }
        }

        /// <summary>The band the menu draws for the wielder defending the settlement. Kept, so the
        /// army it holds is read off one adapter rather than a new one per operation.</summary>
        public DefencePanelWielderAdapter DefendingWielder
        {
            get
            {
                DefencePanelWielder panel = GetDefencePanelWielder();
                if (_defendingWielder == null || !ReferenceEquals(_defendingWielder.Panel, panel))
                {
                    _defendingWielder = new DefencePanelWielderAdapter(panel, _facade, _localization);
                }

                return _defendingWielder;
            }
        }

        /// <summary>The garrison's slots. Kept while the game hands back the same list, so the page's
        /// build does not allocate a slot list adapter and a slot per entry every frame.</summary>
        public IReadOnlyList<DefenceSlotListAdapter.Slot> GetGarrisonSlots()
        {
            List<TroopHUDEntry> entries = GetField<List<TroopHUDEntry>>(GetDefencePanelTroops(), GarrisonTroopsField);
            if (_garrison == null || !ReferenceEquals(_garrison.Entries, entries))
            {
                _garrison = new DefenceSlotListAdapter(entries, _localization);
            }

            return _garrison.GetSlots();
        }

        public IReadOnlyList<DefenceSlotListAdapter.Slot> GetBallistaSlots()
        {
            List<TroopHUDEntry> entries = GetField<List<TroopHUDEntry>>(GetDefencePanelTroops(), BallistaTroopsField);
            if (_ballistae == null || !ReferenceEquals(_ballistae.Entries, entries))
            {
                _ballistae = new DefenceSlotListAdapter(entries, _localization);
            }

            return _ballistae.GetSlots();
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

        private UIButton GetMoveToDefenceButton()
        {
            return GetField<UIButton>(GetDefencePanelTroops(), MoveToDefenceButtonField);
        }

        private UIButton GetMoveToWielderButton()
        {
            return GetField<UIButton>(GetDefencePanelTroops(), MoveToWielderButtonField);
        }

        private int GetInteractingCommanderId()
        {
            object value = InteractingCommanderIdField != null ? InteractingCommanderIdField.GetValue(_menu) : null;
            return value is int ? (int)value : -1;
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpokenLines.Clean(MenuButtonTextUtility.GetAllVisibleText(button));
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
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

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable && IsVisible(button as Component);
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

    }
}
