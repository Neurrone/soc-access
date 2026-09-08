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
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class DefenceMenuAdapter
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
        private static readonly FieldInfo MoveToDefenceButtonField = AccessTools.Field(typeof(DefencePanelTroops), "_moveToDefenceButton");
        private static readonly FieldInfo MoveToWielderButtonField = AccessTools.Field(typeof(DefencePanelTroops), "_moveToWielderButton");
        private static readonly FieldInfo CloseButtonField = AccessTools.Field(typeof(DefenceMenu), "_closeButton");
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
        private DefencePanelWielderAdapter _defendingWielder;
        private TroopHudAdapter _settlementTroops;
        private DefenceSlotListAdapter _garrison;
        private DefenceSlotListAdapter _ballistae;
        private int _towerItemsFrame = -1;
        private List<TowerItem> _towerItems;

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

        /// <summary>The band the menu draws for the wielder stored in the settlement. Kept, so the
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

        /// <summary>The button that hands the stored wielder's whole army to the defences, and the one
        /// that takes it back. The game hides both while no wielder is stored and while it would
        /// refuse the move.</summary>
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

        /// <summary>The cross the menu draws at its top right. The game only turns it on for a player
        /// on mouse and keyboard.</summary>
        public Component CloseButton
        {
            get { return GetCloseButton() as Component; }
        }

        public bool IsCloseVisible()
        {
            return IsVisible(GetCloseButton() as Component);
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(GetCloseButton());
        }

        public bool IsSettlementTroopsVisible()
        {
            return IsVisible(GetField<GameObject>(GetDefencePanelTroops(), SettlementTroopsContainerField));
        }

        public Component TutorialButton
        {
            get { return GetTutorialButton() as Component; }
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

        public Component DraftButton
        {
            get { return GetDraftButton() as Component; }
        }

        public Component UpgradeButton
        {
            get { return GetUpgradeButton() as Component; }
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

        /// <summary>The button the menu fades in over a sub-page to get back to its landing page, and
        /// the word the prefab has written on it.</summary>
        public Component BackButton
        {
            get { return GetField<UIButton>(_menu, BackButtonField) as Component; }
        }

        public string BackLabel
        {
            get { return GetButtonLabel(GetField<UIButton>(_menu, BackButtonField)); }
        }

        public bool IsBackVisible()
        {
            return IsVisible(BackButton);
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

        /// <summary>The towers the panel is drawing, walked at most once a frame.</summary>
        public IReadOnlyList<TowerItem> GetTowerItems()
        {
            int frame = Time.frameCount;
            if (_towerItems != null && _towerItemsFrame == frame)
            {
                return _towerItems;
            }

            _towerItemsFrame = frame;
            Transform container = GetField<Transform>(GetDefencePanelTroops(), TowerContainerField);
            if (container == null)
            {
                _towerItems = new List<TowerItem>();
                return _towerItems;
            }

            List<TowerItem> result = new List<TowerItem>();
            DefenceTowerEntry[] entries = container.GetComponentsInChildren<DefenceTowerEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                result.Add(new TowerItem("defences-tower-" + (i + 1), i + 1, entries[i], _localization));
            }

            _towerItems = result;
            return result;
        }

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

        private UIButton GetCloseButton()
        {
            return GetField<UIButton>(_menu, CloseButtonField);
        }

        private UIButton GetMoveToDefenceButton()
        {
            return GetField<UIButton>(GetDefencePanelTroops(), MoveToDefenceButtonField);
        }

        private UIButton GetMoveToWielderButton()
        {
            return GetField<UIButton>(GetDefencePanelTroops(), MoveToWielderButtonField);
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpokenLines.Clean(MenuButtonTextUtility.GetAllVisibleText(button));
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpokenLines.Clean(GameText.Get(_localization, key, fallback));
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

        public sealed class TowerItem
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

            /// <summary>The tooltip area the game draws for the tower, which is what a row about it
            /// stands on.</summary>
            public Component Source
            {
                get { return GetField<Component>(_entry, TowerTooltipAreaField); }
            }

            public string Label
            {
                get
                {
                    Component tooltipArea = GetField<Component>(_entry, TowerTooltipAreaField);
                    IDetails details;
                    if (NativeTooltipUtility.TryGetUiDetails(tooltipArea, out details) && details is DefenceTowerDetails towerDetails)
                    {
                        return SpokenLines.Clean(towerDetails.Header);
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
