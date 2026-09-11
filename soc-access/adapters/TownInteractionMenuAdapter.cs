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
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class TownInteractionMenuAdapter : IPresent
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(TownInteractionMenu), "_async");
        private static readonly FieldInfo HeaderField = AccessTools.Field(typeof(TownInteractionMenu), "_wielderInteractHeader");
        private static readonly FieldInfo BackToTopButtonField = AccessTools.Field(typeof(TownInteractionMenu), "_backToTopButton");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(TownInteractionMenu), "_tutorialButton");
        private static readonly FieldInfo RecruitmentPoolField = AccessTools.Field(typeof(TownInteractionMenu), "_recruitmentPool");
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

        private static readonly FieldInfo HeaderCloseButtonField = AccessTools.Field(typeof(WielderInteractHeader), "_closeButton");
        private static readonly FieldInfo HeaderCustomNameContainerField = AccessTools.Field(typeof(WielderInteractHeader), "_customNameContainer");
        private static readonly FieldInfo HeaderCustomNameTextField = AccessTools.Field(typeof(WielderInteractHeader), "_customNameText");

        private static readonly FieldInfo SettlementTroopHudField = AccessTools.Field(typeof(TownInteractDefencePanel), "_mapEntityTroopHUD");
        private static readonly FieldInfo SettlementTroopsContainerField = AccessTools.Field(typeof(TownInteractDefencePanel), "_mapEntityTroopsContainer");
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
            _facade = Reflect.Get<IClientAdventureFacade>(menu, AdventureFacadeField);
            _localization = Reflect.Get<ILocalizationHandler>(menu, LocalizationField);
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int VisitingCommanderId
        {
            get { return GetInteractingCommanderId(); }
        }

        /// <summary>The menu's own LANDING page is drawn: the page this adapter reads. A troop
        /// sub-page over it is a page of its own (<c>IsDraftPresent</c>, <c>IsUpgradePresent</c>),
        /// read by <c>TroopManagementScreenBase</c> through the host interface.</summary>
        public bool IsPresent()
        {
            return IsMenuOpen() && GameObjects.IsLive(Reflect.Get<GameObject>(_menu, LandingPageContainerField));
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
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, BuildingNameField)); }
        }

        public bool IsCustomNameVisible
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(GetHeader(), HeaderCustomNameContainerField)); }
        }

        public string CustomName
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(GetHeader(), HeaderCustomNameTextField)); }
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
            return button != null && GameObjects.IsLive(button as Component);
        }

        public string GetTutorialButtonLabel()
        {
            return GetButtonLabel(GetTutorialButton());
        }

        public bool ActivateTutorial()
        {
            return NativeSelectionUtility.Click(GetTutorialButton());
        }

        /// <summary>The settlement's own army. Kept: the adapter wakes the game's drag ghost when it
        /// is made, and the rows are rebuilt on every navigation operation.</summary>
        public TroopHudAdapter SettlementTroops
        {
            get
            {
                TroopHUD hud = Reflect.Get<TroopHUD>(GetDefencePanelTroops(), SettlementTroopHudField);
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

        /// <summary>Whether the settlement's own troop rows are drawn at all. The panel turns the
        /// container off for a map entity with no troop storage
        /// (<c>TownInteractDefencePanel.Show</c>).</summary>
        public bool IsSettlementTroopsVisible()
        {
            return GameObjects.IsLive(Reflect.Get<GameObject>(GetDefencePanelTroops(), SettlementTroopsContainerField));
        }

        /// <summary>The header the game writes over a settlement's own troops, from the same key the
        /// defence menu reads.</summary>
        public string DefendingTroopsLabel
        {
            get
            {
                return SpokenLines.Clean(
                    GameText.Get(_localization, "Adventure/TroopManagementMenu/DefendingTroopsHeader", string.Empty));
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
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetMoveToDefenceButton());
        }

        public bool IsMoveToWielderEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetMoveToWielderButton());
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
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, PurchaseTroopsDescriptionField)); }
        }

        public string UpgradeDescription
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, UpgradeTroopsDescriptionField)); }
        }

        /// <summary>The number the menu stamps on the Upgrade button while something can be upgraded;
        /// empty while the game hides that indicator.</summary>
        public string UpgradesAvailableNumber
        {
            get
            {
                return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, UpgradesAvailableIndicatorField))
                    ? UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, UpgradesAvailableNumberField))
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
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetDraftButton());
        }

        public bool IsUpgradeEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetUpgradeButton());
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
            get { return Reflect.Get<UIButton>(_menu, BackToTopButtonField) as Component; }
        }

        public string BackLabel
        {
            get { return GetButtonLabel(Reflect.Get<UIButton>(_menu, BackToTopButtonField)); }
        }

        public bool IsBackVisible()
        {
            return GameObjects.IsLive(BackButton);
        }

        public bool BackToTop()
        {
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(_menu, BackToTopButtonField));
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
            List<TroopHUDEntry> entries = Reflect.Get<List<TroopHUDEntry>>(GetDefencePanelTroops(), GarrisonTroopsField);
            if (_garrison == null || !ReferenceEquals(_garrison.Entries, entries))
            {
                _garrison = new DefenceSlotListAdapter(entries, _localization);
            }

            return _garrison.GetSlots();
        }

        public IReadOnlyList<DefenceSlotListAdapter.Slot> GetBallistaSlots()
        {
            List<TroopHUDEntry> entries = Reflect.Get<List<TroopHUDEntry>>(GetDefencePanelTroops(), BallistaTroopsField);
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

        /// <summary>Whether the building this menu is about is still in the game. A menu holds its
        /// recruitment pool - and with it the map entity - from one Show to the next and does not
        /// clear it on Close, so a menu left open over an entity that has gone (a save loaded under
        /// it, the building destroyed) keeps every other sign of being up: its object is still
        /// active, its sub-page is still drawn, and its Async is still uncompleted because Close
        /// never ran. The entity's own IsDisposed is what <c>AbstractMapEntity.Dispose</c> changes,
        /// but <c>DestroyMapEntityCommand</c> only removes the entity from the map without disposing
        /// it (verified in-game 2026-09-12: IsDisposed stayed false on the destroyed rally point), so
        /// the map facade is asked whether the entity still exists as well.</summary>
        private bool IsEntityAlive()
        {
            IRecruitmentPoolComponent pool = Reflect.Get<IRecruitmentPoolComponent>(_menu, RecruitmentPoolField);
            IMapEntity entity = pool != null ? pool.MapEntity : null;
            return entity != null
                && !entity.IsDisposed
                && (_facade == null || _facade.MapEntities == null || _facade.MapEntities.Exists(entity.Id));
        }

        /// <summary>The menu object is up AND it is still about a building that exists: the landing
        /// page, the draft page and the upgrade page are all pages of THIS town, so a town that has
        /// gone takes all three with it.</summary>
        private bool IsMenuOpen()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && Reflect.Get<Async>(_menu, AsyncField) != null
                && IsEntityAlive();
        }

        private WielderInteractHeader GetHeader()
        {
            return Reflect.Get<WielderInteractHeader>(_menu, HeaderField);
        }

        private TownInteractDefencePanel GetDefencePanelTroops()
        {
            return Reflect.Get<TownInteractDefencePanel>(_menu, DefencePanelTroopsField);
        }

        private DefencePanelWielder GetDefencePanelWielder()
        {
            return Reflect.Get<DefencePanelWielder>(_menu, DefencePanelWielderField);
        }

        private PurchaseTroopsSubMenu GetPurchaseSubMenu()
        {
            return Reflect.Get<PurchaseTroopsSubMenu>(_menu, PurchaseTroopsSubMenuField);
        }

        private UpgradeTroopsSubMenu GetUpgradeSubMenu()
        {
            return Reflect.Get<UpgradeTroopsSubMenu>(_menu, UpgradeTroopsSubMenuField);
        }

        private UIButton GetDraftButton()
        {
            return Reflect.Get<UIButton>(_menu, PurchaseTroopsButtonField);
        }

        private UIButton GetTutorialButton()
        {
            return Reflect.Get<UIButton>(_menu, TutorialButtonField);
        }

        private UIButton GetUpgradeButton()
        {
            return Reflect.Get<UIButton>(_menu, UpgradeTroopsButtonField);
        }

        private UIButton GetMoveToDefenceButton()
        {
            return Reflect.Get<UIButton>(GetDefencePanelTroops(), MoveToDefenceButtonField);
        }

        private UIButton GetMoveToWielderButton()
        {
            return Reflect.Get<UIButton>(GetDefencePanelTroops(), MoveToWielderButtonField);
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

    }
}
