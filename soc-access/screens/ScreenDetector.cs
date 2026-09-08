using System.Collections;
using _8_UILayer.ClientView.Menu.Paus;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Facade;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// THE READINESS LAYER: when a menu the mod knows about is ready to be worked, and when the game
    /// has taken it away. The patches under <c>patches/</c> call the handlers here; each one points a
    /// registered screen's SLOT at the menu (<see cref="LiveScreen{TAdapter}.Live"/>) or clears it.
    ///
    /// Nothing here decides which screen the player is on: that is the poll's job
    /// (<see cref="ScreenManager"/>), which asks every registered screen every frame and sorts the
    /// answers by layer. A handler that used to push, pop or refresh a screen now only writes what
    /// the screen is reading, which is why a menu the game closed without telling us cannot strand
    /// the mod on a dead page.
    /// </summary>
    public sealed class ScreenDetector
    {
        private readonly ScreenManager _screens;
        private AdventureViewInstaller _adventureViewInstaller;
        private BattleSceneInstaller _battleSceneInstaller;
        private IconDropdown _deferredAdventureLobbyDropdownClose;
        private bool _deferredAdventureLobbyDropdownHidden;
        private float _deferredAdventureLobbyDropdownDeadline;
        private bool _communityMapsHomeContentRefreshPending;

        public ScreenDetector(ScreenManager screens)
        {
            _screens = screens;
        }

        /// <summary>Whether a story sequence is running - the camera and the keyboard are the story's,
        /// and the map stands down for it (<see cref="AdventureMapScreen.IsActive"/>).</summary>
        public bool StorySequenceActive { get; private set; }

        public void Update()
        {
            if (_deferredAdventureLobbyDropdownClose == null || !_deferredAdventureLobbyDropdownHidden)
            {
                return;
            }

            if (UnityEngine.Time.realtimeSinceStartup >= _deferredAdventureLobbyDropdownDeadline)
            {
                CompleteDeferredAdventureLobbyDropdownClose();
            }
        }

        // ---- the slots ----

        /// <summary>The one registered instance of a screen, whether or not it is showing.</summary>
        private TScreen Reg<TScreen>() where TScreen : Screen
        {
            return _screens == null ? null : _screens.Registered<TScreen>();
        }

        /// <summary>Let go of every screen's slot but one - a new root state, with nothing from the
        /// previous one left to read.</summary>
        private void ForgetAllExcept(Screen keep)
        {
            if (_screens == null)
            {
                return;
            }

            System.Collections.Generic.IReadOnlyList<Screen> registered = _screens.RegisteredScreens;
            for (int i = 0; i < registered.Count; i++)
            {
                if (!ReferenceEquals(registered[i], keep))
                {
                    registered[i].Forget();
                }
            }
        }

        // ---- loading ----

        public void OnLoadingScreenReady(LoadingScreenMenu menu)
        {
            LoadingScreenAdapter adapter = new LoadingScreenAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            LoadingCompleteScreen screen = Reg<LoadingCompleteScreen>();
            // The loading-complete prompt is a root screen; nothing from the previous game state
            // should remain readable beneath it.
            ForgetAllExcept(screen);
            screen?.Show(adapter);
        }

        public void OnLoadingScreenClosed(LoadingScreenMenu menu)
        {
            Reg<LoadingCompleteScreen>()?.Forget();
        }

        public void OnLoadingScreenOpening(LoadingScreenMenu menu)
        {
            NativeTooltipUtility.HideTooltip();
            ForgetAllExcept(null);
        }

        // ---- the story ----

        public void OnStorySequenceTrigger(OnTriggerPayload payload, IClientAdventureFacade facade)
        {
            if (!IsLocalStoryTrigger(payload, facade))
            {
                return;
            }

            StorySequenceActive = true;
        }

        public void OnStorySequenceCompleted()
        {
            StorySequenceActive = false;
            Reg<StoryTextScreen>()?.Forget();
        }

        // ---- the pause menu and its pages ----

        public void OnPauseMenuReady(PauseMenu pauseMenu)
        {
            Reg<PauseMenuScreen>()?.Show(new PauseMenuAdapter(pauseMenu));
        }

        /// <summary><paramref name="handsOver"/>: the menu closed to open options, a save or load
        /// menu or the codex, which the game shows a frame or more later; the pause screen stays
        /// active across that gap (<see cref="PauseMenuScreen.BeginHandover"/>) so the map is not
        /// handed back for it.</summary>
        public void OnPauseMenuClosed(PauseMenu pauseMenu, bool handsOver)
        {
            PauseMenuScreen screen = Reg<PauseMenuScreen>();
            if (screen == null)
            {
                return;
            }

            screen.Forget();
            if (handsOver)
            {
                screen.BeginHandover();
            }
        }

        public void OnOptionsMenuReady(OptionsMenu optionsMenu)
        {
            Reg<PauseMenuScreen>()?.EndHandover();
            OptionsMenuAdapter adapter = new OptionsMenuAdapter(optionsMenu);
            if (adapter.IsPresent())
            {
                Reg<OptionsScreen>()?.Show(adapter);
            }
        }

        public void OnOptionsMenuClosed(OptionsMenu optionsMenu)
        {
            Reg<OptionsScreen>()?.Forget();
        }

        public void OnSaveLoadGameMenuReady(SaveLoadGameMenu menu)
        {
            Reg<PauseMenuScreen>()?.EndHandover();
            SaveLoadGameMenuAdapter adapter = new SaveLoadGameMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<SaveLoadGameScreen>()?.Show(adapter);
            }
        }

        public void OnSaveLoadGameMenuClosed(SaveLoadGameMenu menu)
        {
            SaveLoadGameScreen screen = Reg<SaveLoadGameScreen>();
            if (screen != null && screen.Matches(menu))
            {
                screen.Forget();
            }
        }

        public void OnCodexReady(CodexMenu codexMenu)
        {
            Reg<PauseMenuScreen>()?.EndHandover();
            CodexMenuAdapter adapter = new CodexMenuAdapter(codexMenu);
            if (adapter.IsPresent())
            {
                Reg<CodexScreen>()?.Show(adapter);
            }
        }

        public void OnCodexClosed(CodexMenu codexMenu)
        {
            Reg<CodexScreen>()?.Forget();
        }

        // ---- the in-game panels ----

        public void OnOwnedEntitiesReady(KingdomEntityOverviewMenu menu)
        {
            KingdomEntityOverviewAdapter adapter = new KingdomEntityOverviewAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<OwnedEntitiesScreen>()?.Show(adapter);
            }
        }

        public void OnOwnedEntitiesClosed(KingdomEntityOverviewMenu menu)
        {
            Reg<OwnedEntitiesScreen>()?.Forget();
        }

        public void OnTroopOverviewReady(KingdomTroopOverviewMenu menu)
        {
            KingdomTroopOverviewAdapter adapter = new KingdomTroopOverviewAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<TroopOverviewScreen>()?.Show(adapter);
            }
        }

        public void OnTroopOverviewClosed(KingdomTroopOverviewMenu menu)
        {
            Reg<TroopOverviewScreen>()?.Forget();
        }

        public void OnAdventurePlayerMenuReady(AdventurePlayerMenu menu)
        {
            AdventurePlayerMenuAdapter adapter = new AdventurePlayerMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventurePlayerMenuScreen>()?.Show(adapter);
            }
        }

        public void OnAdventurePlayerMenuClosed(AdventurePlayerMenu menu)
        {
            AdventurePlayerMenuScreen screen = Reg<AdventurePlayerMenuScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        public void OnSendResourcePopupReady(SendResourcePopup popup)
        {
            SendResourcePopupAdapter adapter = new SendResourcePopupAdapter(popup);
            if (adapter.IsPresent())
            {
                Reg<SendResourcePopupScreen>()?.Show(adapter);
            }
        }

        public void OnSendResourcePopupHidden()
        {
            // SendResourcePopup.Hide is not a reliable close signal by itself: the game also calls it
            // during injection-time initialization and can call it redundantly when the popup is
            // already inactive. Letting go of the slot is safe either way - the poll answers from the
            // popup's own drawn state, and a Hide that meant nothing is followed by a Ready.
            Reg<SendResourcePopupScreen>()?.Forget();
        }

        public void OnGiftTownPopupReady(GiftTownPopup popup)
        {
            GiftTownPopupAdapter adapter = new GiftTownPopupAdapter(popup);
            if (adapter.IsPresent())
            {
                Reg<GiftTownPopupScreen>()?.Show(adapter);
            }
        }

        public void OnGiftTownPopupHidden()
        {
            // GiftTownPopup.Hide is not a reliable close signal by itself, exactly as above.
            Reg<GiftTownPopupScreen>()?.Forget();
        }

        public void OnMarketplaceReady(MarketplaceMenu menu)
        {
            MarketplaceMenuAdapter adapter = new MarketplaceMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<MarketplaceScreen>()?.Show(adapter);
            }
        }

        public void OnMarketplaceClosed(MarketplaceMenu menu)
        {
            Reg<MarketplaceScreen>()?.Forget();
        }

        public void OnArtifactMarketReady(ArtifactMarketMenu menu)
        {
            ArtifactMarketMenuAdapter adapter = new ArtifactMarketMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<ArtifactMarketScreen>()?.Show(adapter);
            }
        }

        public void OnArtifactMarketClosed(ArtifactMarketMenu menu)
        {
            // ArtifactMarketMenu.Close is a possible-close signal, not proof that the window was open:
            // the game also calls it from Start() and HideAll() while cleaning up inactive menus. The
            // slot going empty costs nothing when the window was never up.
            Reg<ArtifactMarketScreen>()?.Forget();
        }

        public void OnMapEntityMiniMenuReady(MapEntityMiniMenu menu)
        {
            MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<MapEntityMiniMenuScreen>()?.Show(adapter);
            }
        }

        public void OnMapEntityMiniMenuClosed(MapEntityMiniMenu menu)
        {
            // Selling a building closes the native mini menu from inside the confirm popup's async
            // callback, while the dialog is still up over it.
            Reg<MapEntityMiniMenuScreen>()?.Forget();
        }

        public void OnTradingMenuReady(TradingMenu menu)
        {
            Reg<TradingScreen>()?.Show(new TradingMenuAdapter(menu));
        }

        public void OnTradingMenuClosed(TradingMenu menu)
        {
            Reg<TradingScreen>()?.Forget();
        }

        public void OnCommanderSheetReady(CommanderSheet commanderSheet)
        {
            Reg<CommanderSheetScreen>()?.Show(new CommanderSheetAdapter(commanderSheet));
        }

        public void OnCommanderSheetClosed(CommanderSheet commanderSheet)
        {
            Reg<CommanderSheetScreen>()?.Forget();
        }

        public void OnSpellbookReady(SpellBook spellbook)
        {
            Reg<SpellbookScreen>()?.Show(new SpellbookAdapter(spellbook));
        }

        public void OnSpellbookClosed(SpellBook spellbook)
        {
            Reg<SpellbookScreen>()?.Forget();
        }

        public void OnLevelUpMenuReady(CommanderLevelUpMenu menu)
        {
            Reg<LevelUpScreen>()?.Show(new LevelUpMenuAdapter(menu));
        }

        public void OnLevelUpMenuClosed(CommanderLevelUpMenu menu)
        {
            Reg<LevelUpScreen>()?.Forget();
        }

        public void OnHostileJoinMenuChanged(HostileJoinMenu menu)
        {
            HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                adapter.Dispose();
                return;
            }

            HostileJoinMenuScreen screen = Reg<HostileJoinMenuScreen>();
            if (screen == null)
            {
                adapter.Dispose();
                return;
            }

            if (screen.Live == null)
            {
                screen.Live = adapter;
                return;
            }

            // The menu walks through its stages in place: the screen keeps the adapter it has and is
            // told the stage moved, which drops the cursor onto the new page.
            adapter.Dispose();
            screen.Refresh();
        }

        public void OnHostileJoinMenuClosed(HostileJoinMenu menu)
        {
            Reg<HostileJoinMenuScreen>()?.Forget();
        }

        public void OnMoveTroopPopupReady(TroopHUDEntryMovable movable)
        {
            Reg<MoveTroopPopupScreen>()?.Show(new MoveTroopPopupAdapter(movable));
        }

        public void OnMoveTroopPopupClosed(TroopHUDEntryMovable movable)
        {
            // The game calls TroopHUDEntryMovable.Reset even when the troop move popup is not open,
            // such as during HUD teardown and refresh; an empty slot emptied again costs nothing.
            Reg<MoveTroopPopupScreen>()?.Forget();
        }

        public void OnWorldChoiceMenuReady(WorldChoiceMenu menu)
        {
            Reg<WorldChoiceMenuScreen>()?.Show(new WorldChoiceMenuAdapter(menu));
        }

        public void OnWorldChoiceMenuClosed(WorldChoiceMenu menu)
        {
            Reg<WorldChoiceMenuScreen>()?.Forget();
        }

        public void OnWorldConfirmMenuReady(WorldConfirmMenu menu)
        {
            Reg<WorldConfirmMenuScreen>()?.Show(new WorldConfirmMenuAdapter(menu));
        }

        public void OnWorldConfirmMenuClosed(WorldConfirmMenu menu)
        {
            Reg<WorldConfirmMenuScreen>()?.Forget();
        }

        public void OnClaimMenuReady(ClaimMenu menu)
        {
            ClaimMenuAdapter adapter = new ClaimMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<ClaimMenuScreen>()?.Show(adapter);
            }
        }

        public void OnClaimMenuClosed(ClaimMenu menu)
        {
            ClaimMenuScreen screen = Reg<ClaimMenuScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        public void OnPlayerStatsReady(PlayerStatsMenuNavigation menu)
        {
            PlayerStatsAdapter adapter = new PlayerStatsAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<PlayerStatsScreen>()?.Show(adapter);
            }
        }

        public void OnPlayerStatsClosed(PlayerStatsMenuNavigation menu)
        {
            Reg<PlayerStatsScreen>()?.Forget();
        }

        public void OnPurchaseWielderReady(PurchaseWielderMenu menu)
        {
            PurchaseWielderMenuAdapter adapter = new PurchaseWielderMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<PurchaseWielderScreen>()?.Show(adapter);
            }
        }

        public void OnPurchaseWielderClosed(PurchaseWielderMenu menu)
        {
            PurchaseWielderScreen screen = Reg<PurchaseWielderScreen>();
            if (screen != null
                && (menu == null || (screen.Live != null && ReferenceEquals(screen.Live.Source, menu))))
            {
                screen.Forget();
            }
        }

        public void OnBuildMenuReady(BuildMenu menu)
        {
            Reg<BuildMenuScreen>()?.Show(new BuildMenuAdapter(menu));
        }

        public void OnBuildMenuClosed(BuildMenu menu)
        {
            Reg<BuildMenuScreen>()?.Forget();
        }

        public void OnResearchMenuReady(ResearchMenu menu)
        {
            ResearchMenuAdapter adapter = new ResearchMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<ResearchScreen>()?.Show(adapter);
            }
        }

        public void OnResearchMenuClosed(ResearchMenu menu)
        {
            Reg<ResearchScreen>()?.Forget();
        }

        // ---- the settlement, the dwelling and the defence menu, with their two sub-pages ----

        public void OnSettlementReady(TownInteractionMenu menu)
        {
            Reg<SettlementScreen>()?.Show(new TownInteractionMenuAdapter(menu));
        }

        public void OnSettlementDraftReady(TownInteractionMenu menu)
        {
            Reg<DraftTroopsScreen>()?.Show(
                new SettlementTroopManagementHostAdapter(new TownInteractionMenuAdapter(menu)));
        }

        public void OnSettlementUpgradeReady(TownInteractionMenu menu)
        {
            Reg<UpgradeTroopsScreen>()?.Show(
                new SettlementTroopManagementHostAdapter(new TownInteractionMenuAdapter(menu)));
        }

        public void OnSettlementBackToTop(TownInteractionMenu menu)
        {
            ForgetTroopPages("settlement");
            Reg<SettlementScreen>()?.Show(new TownInteractionMenuAdapter(menu));
        }

        public void OnSettlementClosed(TownInteractionMenu menu)
        {
            ForgetTroopPages("settlement");
            Reg<SettlementScreen>()?.Forget();
        }

        public void OnDefenceMenuReady(DefenceMenu menu)
        {
            Reg<DefenceMenuScreen>()?.Show(new DefenceMenuAdapter(menu));
        }

        public void OnDefenceDraftReady(DefenceMenu menu)
        {
            Reg<DraftTroopsScreen>()?.Show(
                new DefenceTroopManagementHostAdapter(new DefenceMenuAdapter(menu)));
        }

        public void OnDefenceUpgradeReady(DefenceMenu menu)
        {
            Reg<UpgradeTroopsScreen>()?.Show(
                new DefenceTroopManagementHostAdapter(new DefenceMenuAdapter(menu)));
        }

        public void OnDefenceMenuBackToTop(DefenceMenu menu)
        {
            ForgetTroopPages("defences");
            Reg<DefenceMenuScreen>()?.Show(new DefenceMenuAdapter(menu));
        }

        public void OnDefenceMenuClosed(DefenceMenu menu)
        {
            ForgetTroopPages("defences");
            Reg<DefenceMenuScreen>()?.Forget();
        }

        public void OnDwellingInteractionReady(DwellingInteractionMenu menu)
        {
            Reg<DraftTroopsScreen>()?.Show(
                new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu)));
        }

        public void OnDwellingUpgradeReady(DwellingInteractionMenu menu)
        {
            Reg<UpgradeTroopsScreen>()?.Show(
                new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu)));
        }

        public void OnDwellingBackToTop(DwellingInteractionMenu menu)
        {
            ForgetTroopPages("dwelling");
            Reg<DraftTroopsScreen>()?.Show(
                new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu)));
        }

        public void OnDwellingInteractionClosed(DwellingInteractionMenu menu)
        {
            ForgetTroopPages("dwelling");
        }

        /// <summary>Let go of the draft and upgrade pages this host drew - the host has gone back to
        /// its landing page, or closed. A page drawn by a DIFFERENT host is left alone: the two
        /// screens are shared between the town, the dwelling and the defence menu.</summary>
        private void ForgetTroopPages(string hostIdPrefix)
        {
            DraftTroopsScreen draft = Reg<DraftTroopsScreen>();
            if (draft != null && draft.HostIdPrefix == hostIdPrefix)
            {
                draft.Forget();
            }

            UpgradeTroopsScreen upgrade = Reg<UpgradeTroopsScreen>();
            if (upgrade != null && upgrade.HostIdPrefix == hostIdPrefix)
            {
                upgrade.Forget();
            }
        }

        public void OnRallyPointReady(RallyPointInteractionMenu menu)
        {
            Reg<RallyPointScreen>()?.Show(new RallyPointInteractionMenuAdapter(menu));
        }

        public void OnRallyPointClosed(RallyPointInteractionMenu menu)
        {
            Reg<RallyPointScreen>()?.Forget();
        }

        // ---- the dialogs ----

        public void OnConfirmPopupReady(ConfirmPopup popup)
        {
            ConfirmPopupAdapter adapter = new ConfirmPopupAdapter(popup);
            if (adapter.IsPresent())
            {
                Reg<MessageDialogScreen>()?.Show(adapter);
            }
        }

        public void OnConfirmPopupClosed(ConfirmPopup popup)
        {
            ForgetMessageDialog(popup);
        }

        public void OnSystemPopupReady(SystemPopup popup)
        {
            SystemPopupAdapter adapter = new SystemPopupAdapter(popup);
            if (adapter.IsPresent())
            {
                Reg<MessageDialogScreen>()?.Show(adapter);
            }
        }

        public void OnSystemPopupClosed(SystemPopup popup)
        {
            ForgetMessageDialog(popup);
        }

        public void OnPopupMenuReady(object sourceKey, PopupMenu.Settings settings)
        {
            if (settings == null)
            {
                SocAccessMod.Instance?.LogWarning("ScreenDetector.OnPopupMenuReady received null settings");
                return;
            }

            object resolvedSourceKey = sourceKey ?? (object)settings.ContainerTransform;
            Reg<MessageDialogScreen>()?.Show(new PopupMenuAdapter(resolvedSourceKey, settings));
        }

        public void OnPopupMenuClosed(object sourceKey)
        {
            ForgetMessageDialog(sourceKey);
        }

        public void OnMapMessagePopupReady(MapMessagePopup popup)
        {
            MapMessagePopupAdapter adapter = new MapMessagePopupAdapter(popup);
            if (adapter.IsPresent())
            {
                Reg<MessageDialogScreen>()?.Show(adapter);
            }
        }

        public void OnMapMessagePopupClosed(MapMessagePopup popup)
        {
            ForgetMessageDialog(popup);
        }

        public void OnRandomEventMenuReady(RandomEventMenu menu)
        {
            RandomEventMenuAdapter adapter = new RandomEventMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<MessageDialogScreen>()?.Show(adapter);
            }
        }

        public void OnRandomEventMenuClosed(RandomEventMenu menu)
        {
            ForgetMessageDialog(menu);
        }

        public void OnCustomMessageMenuReady(CustomMessageMenu menu)
        {
            CustomMessageMenuAdapter adapter = new CustomMessageMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<MessageDialogScreen>()?.Show(adapter);
            }
        }

        public void OnCustomMessageMenuClosed(CustomMessageMenu menu)
        {
            ForgetMessageDialog(menu);
        }

        /// <summary>One slot, six sources: a close is only this dialog's when the slot is reading the
        /// source that closed. A null source key means "whatever is in there".</summary>
        private void ForgetMessageDialog(object sourceKey)
        {
            MessageDialogScreen screen = Reg<MessageDialogScreen>();
            if (screen == null || screen.Live == null)
            {
                return;
            }

            object current = screen.SourceKey;
            if (sourceKey == null || current == null || ReferenceEquals(sourceKey, current))
            {
                screen.Forget();
            }
        }

        public void OnQuitToDesktopPopupReady(QuitToDesktopPopup popup)
        {
            QuitToDesktopPopupAdapter adapter = new QuitToDesktopPopupAdapter(popup);
            if (adapter.IsPresent())
            {
                Reg<QuitToDesktopPopupScreen>()?.Show(adapter);
            }
        }

        public void OnQuitToDesktopPopupClosed(QuitToDesktopPopup popup)
        {
            Reg<QuitToDesktopPopupScreen>()?.Forget();
        }

        // ---- the tutorials ----

        public void OnTutorialReady(TutorialMenu tutorialMenu)
        {
            ShowTutorial(tutorialMenu);
        }

        public void OnTutorialChanged(TutorialMenu tutorialMenu)
        {
            ShowTutorial(tutorialMenu);
        }

        public void OnTutorialClosed(TutorialMenu tutorialMenu)
        {
            Reg<TutorialSlideshowScreen>()?.Forget();
            Reg<TutorialSimpleScreen>()?.Forget();
        }

        /// <summary>A tutorial popup is one of two shapes, and the menu says which by what it has
        /// drawn. Whichever it is, the other's slot is emptied so a menu that changed shape does not
        /// leave the old page standing.</summary>
        private void ShowTutorial(TutorialMenu tutorialMenu)
        {
            TutorialSlideshowAdapter slideshow = new TutorialSlideshowAdapter(tutorialMenu);
            if (slideshow.IsPresent())
            {
                Reg<TutorialSimpleScreen>()?.Forget();
                Reg<TutorialSlideshowScreen>()?.Show(slideshow);
                return;
            }

            TutorialSimpleAdapter simple = new TutorialSimpleAdapter(tutorialMenu);
            if (simple.IsPresent())
            {
                Reg<TutorialSlideshowScreen>()?.Forget();
                Reg<TutorialSimpleScreen>()?.Show(simple);
            }
        }

        // ---- the story text ----

        public void OnLetterboxStoryTextReady(LetterboxStoryText storyText)
        {
            Reg<StoryTextScreen>()?.Show(new LetterboxStoryTextAdapter(storyText));
        }

        public void OnLetterboxStoryTextClosed(LetterboxStoryText storyText)
        {
            Reg<StoryTextScreen>()?.Forget();
        }

        public void OnStoryTextReady(StoryText storyText)
        {
            Reg<StoryTextScreen>()?.Show(new StoryTextAdapter(storyText));
        }

        public void OnStoryTextClosed(StoryText storyText)
        {
            Reg<StoryTextScreen>()?.Forget();
        }

        public void OnDialogueMenuChanged(DialogueMenu dialogueMenu)
        {
            Reg<StoryTextScreen>()?.Show(new DialogueMenuAdapter(dialogueMenu));
        }

        public void OnDialogueMenuClosed(DialogueMenu dialogueMenu)
        {
            Reg<StoryTextScreen>()?.Forget();
        }

        // ---- the main menu and its pages ----

        public void OnMainMenuReady(MainMenu mainMenu)
        {
            MainMenuScreen screen = Reg<MainMenuScreen>();
            // The main menu is a root screen; letting go of everything else avoids reading state
            // from the game or load that has just ended.
            ForgetAllExcept(screen);
            screen?.Show(new MainMenuAdapter(mainMenu));
        }

        public void OnMainMenuClosed(MainMenu mainMenu)
        {
            Reg<MainMenuScreen>()?.Forget();
        }

        public void OnCampaignMenuReady(CampaignMenu campaignMenu)
        {
            Reg<CampaignMenuScreen>()?.Show(new CampaignMenuAdapter(campaignMenu));
        }

        public void OnCampaignMenuClosed(CampaignMenu campaignMenu)
        {
            Reg<CampaignMenuScreen>()?.Forget();
        }

        public void OnTaleSelectLayoutRebuilt(TaleButtonLayoutCoordinator coordinator)
        {
            Reg<TaleSelectScreen>()?.Show(new TaleSelectAdapter(coordinator));
        }

        public void OnTaleSelectClosed(TaleButtonLayoutCoordinator coordinator)
        {
            Reg<TaleSelectScreen>()?.Forget();
        }

        public void OnCustomCampaignSelectRepopulated(CustomCampaignSelectMenuBehavior behavior)
        {
            CustomCampaignSelectAdapter adapter = new CustomCampaignSelectAdapter(behavior);
            if (adapter.IsPresent())
            {
                Reg<CustomCampaignSelectScreen>()?.Show(adapter);
            }
        }

        public void OnCustomCampaignSelectClosed(CustomCampaignSelectMenuBehavior behavior)
        {
            Reg<CustomCampaignSelectScreen>()?.Forget();
        }

        public void OnCampaignMapSelectShown(CampaignMapSelectMenu menu, CampaignMapSelectedInformationView informationView)
        {
            CampaignMapSelectScreen screen = Reg<CampaignMapSelectScreen>();
            if (screen == null)
            {
                return;
            }

            // Taking a difficulty redraws the page. Where the redraw also took the page off the stack,
            // the cursor is seated afresh and belongs back on the difficulty rather than at the top.
            screen.FocusDifficulty = CampaignMapSelectScreen.ConsumeFocusDifficultyAfterNextRebuild();
            screen.Show(new CampaignMapSelectAdapter(menu, informationView));
        }

        public void OnCampaignMapSelectClosed(CampaignMapSelectedInformationView informationView)
        {
            Reg<CampaignMapSelectScreen>()?.Forget();
        }

        public void OnOnlineGameListReady(GameListMenu menu)
        {
            OnlineGameListAdapter adapter = new OnlineGameListAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<OnlineGameListScreen>()?.Show(adapter);
            }
        }

        public void OnOnlineGameListChanged(GameListMenu menu)
        {
            OnlineGameListScreen screen = Reg<OnlineGameListScreen>();
            if (screen != null && screen.Live != null && screen.Matches(menu))
            {
                return;
            }

            OnOnlineGameListReady(menu);
        }

        public void OnOnlineGameListClosed(GameListMenu menu)
        {
            Reg<OnlineGameListScreen>()?.Forget();
        }

        public void OnOnlineHostGameReady(GameListMenu menu)
        {
            OnlineHostGameAdapter adapter = new OnlineHostGameAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<OnlineHostGameScreen>()?.Show(adapter);
            }
        }

        public void OnOnlineHostGameClosed(GameListMenu menu)
        {
            OnlineHostGameScreen screen = Reg<OnlineHostGameScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        public void OnPlatformUserMenuReady(PlatformUserMenu menu)
        {
            PlatformUserMenuAdapter adapter = new PlatformUserMenuAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<PlatformUserMenuScreen>()?.Show(adapter);
            }
        }

        public void OnPlatformUserMenuClosed(PlatformUserMenu menu)
        {
            PlatformUserMenuScreen screen = Reg<PlatformUserMenuScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        // ---- the community maps browser ----

        public void OnCommunityMapsChanged()
        {
            ShowCommunityMapsHome();
            ShowCommunityMapsCollection();
            ShowCommunityMaps<CommunityMapsDetailsScreen, CommunityMapsDetailsAdapter>(
                CommunityMapsDetailsScreen.FindActive());
        }

        public void OnCommunityMapsHomeContentChanged()
        {
            if (_communityMapsHomeContentRefreshPending)
            {
                return;
            }

            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin == null)
            {
                ShowCommunityMapsHome();
                return;
            }

            // The game fills the home panel's rows across the frame boundary; read a frame later, so
            // what the slot points at is a panel with its content in it.
            _communityMapsHomeContentRefreshPending = true;
            plugin.StartCoroutine(RefreshCommunityMapsHomeContentNextFrame());
        }

        private IEnumerator RefreshCommunityMapsHomeContentNextFrame()
        {
            yield return null;
            _communityMapsHomeContentRefreshPending = false;
            ShowCommunityMapsHome();
        }

        public void OnCommunityMapsCollectionChanged()
        {
            CommunityMapsCollectionScreen collection = Reg<CommunityMapsCollectionScreen>();
            if (collection != null && collection.IsActive() && collection.IsSearchInputFocused())
            {
                // The player is typing in the panel's own search box: rewriting the slot under it
                // would take the field out from under the editor.
                return;
            }

            ShowCommunityMapsCollection();
        }

        public void OnCommunityMapsModalChanged()
        {
            CommunityMapsModalScreen screen = Reg<CommunityMapsModalScreen>();
            if (screen == null)
            {
                return;
            }

            CommunityMapsModalAdapter adapter = CommunityMapsModalScreen.FindActive();
            if (adapter == null)
            {
                screen.Forget();
                return;
            }

            screen.Live = adapter;
            // The modal walks from Authentication to Terms of use to the code box in place, and each
            // is a different page with a different name: the news is the title.
            screen.SayNameIfChanged();
        }

        public void OnCommunityMapsSearchFilterChanged()
        {
            ShowCommunityMaps<CommunityMapsSearchFilterScreen, CommunityMapsSearchFilterAdapter>(
                CommunityMapsSearchFilterScreen.FindActive());
        }

        public void OnCommunityMapsSearchResultsChanged()
        {
            CommunityMapsSearchResultsAdapter adapter = CommunityMapsSearchResultsScreen.FindActive();
            if (adapter != null)
            {
                // Searching leaves the filter panel: the results are what the player asked for.
                Reg<CommunityMapsSearchFilterScreen>()?.Forget();
            }

            ShowCommunityMaps<CommunityMapsSearchResultsScreen, CommunityMapsSearchResultsAdapter>(adapter);
        }

        public void OnCommunityMapsClosed()
        {
            Reg<CommunityMapsSearchFilterScreen>()?.Forget();
            Reg<CommunityMapsSearchResultsScreen>()?.Forget();
            Reg<CommunityMapsModalScreen>()?.Forget();
            Reg<CommunityMapsHomeScreen>()?.Forget();
            Reg<CommunityMapsCollectionScreen>()?.Forget();
            Reg<CommunityMapsDetailsScreen>()?.Forget();
        }

        private void ShowCommunityMapsHome()
        {
            ShowCommunityMaps<CommunityMapsHomeScreen, CommunityMapsHomeAdapter>(
                CommunityMapsHomeScreen.FindActive());
        }

        private void ShowCommunityMapsCollection()
        {
            ShowCommunityMaps<CommunityMapsCollectionScreen, CommunityMapsCollectionAdapter>(
                CommunityMapsCollectionScreen.FindActive());
        }

        /// <summary>The browser tells the mod that SOMETHING changed and nothing more, so each of its
        /// panels is re-read: the panel is either there, and the slot points at it, or it is not.
        /// </summary>
        private void ShowCommunityMaps<TScreen, TAdapter>(TAdapter adapter)
            where TScreen : LiveScreen<TAdapter>
            where TAdapter : class
        {
            TScreen screen = Reg<TScreen>();
            if (screen == null)
            {
                return;
            }

            if (adapter != null)
            {
                screen.Live = adapter;
            }
            else
            {
                screen.Forget();
            }
        }

        // ---- the adventure lobby ----

        public void OnAdventureLobbyMapTypeReady(MapTypeMenu menu)
        {
            AdventureLobbyMapTypeAdapter adapter = new AdventureLobbyMapTypeAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyMapTypeScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyMapTypeClosed(MapTypeMenu menu)
        {
            Reg<AdventureLobbyMapTypeScreen>()?.Forget();
        }

        public void OnAdventureLobbyRandomLayoutReady(LobbyRandomMapSelectionMenu menu)
        {
            AdventureLobbyRandomLayoutAdapter adapter = new AdventureLobbyRandomLayoutAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyRandomLayoutScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyRandomLayoutSelectionChanged(LobbyRandomMapSelectionMenu menu)
        {
            AdventureLobbyRandomLayoutScreen screen = Reg<AdventureLobbyRandomLayoutScreen>();
            if (screen != null && screen.Live != null && screen.Matches(menu))
            {
                return;
            }

            OnAdventureLobbyRandomLayoutReady(menu);
        }

        public void OnAdventureLobbyRandomLayoutClosed(LobbyRandomMapSelectionMenu menu)
        {
            AdventureLobbyRandomLayoutScreen screen = Reg<AdventureLobbyRandomLayoutScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        public void OnAdventureLobbyMapSelectReady(MapSelectMenu menu)
        {
            AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyMapSelectScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyMapSelectChanged(MapSelectMenu menu)
        {
            AdventureLobbyMapSelectScreen screen = Reg<AdventureLobbyMapSelectScreen>();
            if (screen != null && screen.Live != null && screen.Matches(menu))
            {
                return;
            }

            OnAdventureLobbyMapSelectReady(menu);
        }

        public void OnAdventureLobbyMapSelectSelectionChanged(MapSelectMenu menu)
        {
            OnAdventureLobbyMapSelectChanged(menu);
        }

        public void OnAdventureLobbyMapSelectClosed(MapSelectMenu menu)
        {
            Reg<AdventureLobbyMapSelectScreen>()?.Forget();
        }

        public void OnAdventureLobbyChallengeMapSelectReady(ChallengeMapsMenu menu)
        {
            AdventureLobbyChallengeMapSelectAdapter adapter = new AdventureLobbyChallengeMapSelectAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyChallengeMapSelectScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyChallengeMapSelectSelectionChanged(ChallengeMapsMenu menu)
        {
            AdventureLobbyChallengeMapSelectScreen screen = Reg<AdventureLobbyChallengeMapSelectScreen>();
            if (screen != null && screen.Live != null && screen.Matches(menu))
            {
                return;
            }

            OnAdventureLobbyChallengeMapSelectReady(menu);
        }

        public void OnAdventureLobbyChallengeMapSelectClosed(ChallengeMapsMenu menu)
        {
            Reg<AdventureLobbyChallengeMapSelectScreen>()?.Forget();
        }

        public void OnAdventureLobbyPlayersReady(LobbyMenu menu)
        {
            AdventureLobbyPlayersAdapter adapter = new AdventureLobbyPlayersAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyPlayersScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyPlayersChanged()
        {
            AdventureLobbyPlayersScreen screen = Reg<AdventureLobbyPlayersScreen>();
            if (screen == null || screen.Live == null)
            {
                return;
            }

            // The rows are read from a snapshot the adapter keeps: what changed is inside the lobby,
            // not the lobby itself, so the snapshot is what has to go.
            screen.Refresh();
            CompleteDeferredAdventureLobbyDropdownClose();
        }

        public void OnAdventureLobbyPlayersClosed(LobbyMenu menu)
        {
            Reg<AdventureLobbyInviteProvidersScreen>()?.Forget();
            Reg<AdventureLobbyGameSettingsScreen>()?.Forget();
            Reg<AdventureLobbyPlayerSettingsScreen>()?.Forget();
            Reg<AdventureLobbyIconDropdownScreen>()?.Forget();
            ClearDeferredAdventureLobbyDropdownClose();
            Reg<AdventureLobbyPlayersScreen>()?.Forget();
        }

        public void OnAdventureLobbyInviteProvidersReady(LobbyMultiplayerPanel panel)
        {
            AdventureLobbyInviteProvidersAdapter adapter = new AdventureLobbyInviteProvidersAdapter(panel);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyInviteProvidersScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyInviteProvidersClosed(LobbyMultiplayerPanel panel)
        {
            AdventureLobbyInviteProvidersScreen screen = Reg<AdventureLobbyInviteProvidersScreen>();
            if (screen != null && (panel == null || screen.Matches(panel)))
            {
                screen.Forget();
            }
        }

        public void OnAdventureLobbyGameSettingsReady(LobbyMapSettingsMenu menu)
        {
            AdventureLobbyGameSettingsAdapter adapter = new AdventureLobbyGameSettingsAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyGameSettingsScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyGameSettingsClosed(LobbyMapSettingsMenu menu)
        {
            AdventureLobbyGameSettingsScreen screen = Reg<AdventureLobbyGameSettingsScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        public void OnAdventureLobbyPlayerSettingsReady(LobbyPlayerSettingsMenu menu)
        {
            AdventureLobbyPlayerSettingsAdapter adapter = new AdventureLobbyPlayerSettingsAdapter(menu);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyPlayerSettingsScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyPlayerSettingsClosed(LobbyPlayerSettingsMenu menu)
        {
            AdventureLobbyPlayerSettingsScreen screen = Reg<AdventureLobbyPlayerSettingsScreen>();
            if (screen != null && (menu == null || screen.Matches(menu)))
            {
                screen.Forget();
            }
        }

        public void OnAdventureLobbyIconDropdownReady(IconDropdown dropdown)
        {
            AdventureLobbyIconDropdownAdapter adapter = new AdventureLobbyIconDropdownAdapter(dropdown);
            if (adapter.IsPresent())
            {
                Reg<AdventureLobbyIconDropdownScreen>()?.Show(adapter);
            }
        }

        public void OnAdventureLobbyIconDropdownClosed(IconDropdown dropdown)
        {
            // Taking an option other than a colour hides the dropdown BEFORE the lobby has redrawn the
            // row it changed. Closing then would put the player back on a row that is about to be
            // replaced, so the close waits for the lobby's own change - or, failing that, a second.
            if (_deferredAdventureLobbyDropdownClose != null
                && (dropdown == null || ReferenceEquals(_deferredAdventureLobbyDropdownClose, dropdown)))
            {
                _deferredAdventureLobbyDropdownHidden = true;
                _deferredAdventureLobbyDropdownDeadline = UnityEngine.Time.realtimeSinceStartup + 1f;
                return;
            }

            AdventureLobbyIconDropdownScreen screen = Reg<AdventureLobbyIconDropdownScreen>();
            if (screen != null && (dropdown == null || screen.Matches(dropdown)))
            {
                screen.Forget();
            }
        }

        public void OnAdventureLobbyIconDropdownOptionActivating(IconDropdown dropdown, string optionType)
        {
            if (dropdown == null || optionType == "Color")
            {
                ClearDeferredAdventureLobbyDropdownClose();
                return;
            }

            _deferredAdventureLobbyDropdownClose = dropdown;
            _deferredAdventureLobbyDropdownHidden = false;
            _deferredAdventureLobbyDropdownDeadline = 0f;
        }

        public void OnAdventureLobbyIconDropdownOptionActivationFailed(IconDropdown dropdown)
        {
            if (_deferredAdventureLobbyDropdownClose == null
                || dropdown == null
                || ReferenceEquals(_deferredAdventureLobbyDropdownClose, dropdown))
            {
                ClearDeferredAdventureLobbyDropdownClose();
            }
        }

        private void CompleteDeferredAdventureLobbyDropdownClose()
        {
            if (_deferredAdventureLobbyDropdownClose == null)
            {
                return;
            }

            AdventureLobbyIconDropdownScreen screen = Reg<AdventureLobbyIconDropdownScreen>();
            if (screen != null && screen.Matches(_deferredAdventureLobbyDropdownClose))
            {
                screen.Forget();
            }

            ClearDeferredAdventureLobbyDropdownClose();
        }

        private void ClearDeferredAdventureLobbyDropdownClose()
        {
            _deferredAdventureLobbyDropdownClose = null;
            _deferredAdventureLobbyDropdownHidden = false;
            _deferredAdventureLobbyDropdownDeadline = 0f;
        }

        public void OnMainMenuSceneLoaded(MainMenuSceneType loadedScene)
        {
            if (loadedScene == MainMenuSceneType.MainMenu)
            {
                SocAccessMod.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.AdventureMapNotifications);
                SocAccessMod.Instance?.AdventureMapScannerState?.Clear();
            }

            if (loadedScene != MainMenuSceneType.Campaign)
            {
                Reg<CampaignMenuScreen>()?.Forget();
            }

            CustomCampaignSelectScreen customCampaignSelect = Reg<CustomCampaignSelectScreen>();
            if (loadedScene != MainMenuSceneType.CustomCampaign
                && customCampaignSelect != null
                && !customCampaignSelect.IsActive())
            {
                customCampaignSelect.Forget();
            }

            if (loadedScene != MainMenuSceneType.OnlineGameList)
            {
                Reg<OnlineHostGameScreen>()?.Forget();
                Reg<OnlineGameListScreen>()?.Forget();
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby)
            {
                Reg<AdventureLobbyMapTypeScreen>()?.Forget();
                Reg<AdventureLobbyRandomLayoutScreen>()?.Forget();
                Reg<AdventureLobbyMapSelectScreen>()?.Forget();
                Reg<AdventureLobbyChallengeMapSelectScreen>()?.Forget();
                Reg<AdventureLobbyIconDropdownScreen>()?.Forget();
                Reg<AdventureLobbyInviteProvidersScreen>()?.Forget();
                Reg<AdventureLobbyGameSettingsScreen>()?.Forget();
                Reg<AdventureLobbyPlayerSettingsScreen>()?.Forget();
                Reg<PlatformUserMenuScreen>()?.Forget();
                Reg<AdventureLobbyPlayersScreen>()?.Forget();
            }
        }

        // ---- the adventure map ----

        public void OnAdventureViewReady(AdventureViewInstaller installer)
        {
            _adventureViewInstaller = installer;
        }

        public void OnAdventureMapReady()
        {
            // The scene loader raises this on every return to its idle state while the current scene
            // is still the adventure, including the frames after the player quit to the main menu with
            // the view installer already gone; a screen reading an absent adapter threw on its first
            // read of the map and did so on every frame the loop lasted (2026-09-07).
            ShowAdventureMap("adventure map ready");
        }

        public void OnAdventureMapClosed()
        {
            _adventureViewInstaller = null;
            Reg<AdventureMapScreen>()?.Forget();
        }

        /// <summary>Point the map's slot at the installed adventure, unless the adapter says the
        /// adventure is not ready to be read yet.</summary>
        private void ShowAdventureMap(string reason)
        {
            AdventureMapScreen screen = Reg<AdventureMapScreen>();
            if (screen == null)
            {
                return;
            }

            AdventureMapRevealedRegistry revealedRegistry = GetAdventureMapRevealedRegistry();
            AdventureMapAdapter adapter = new AdventureMapAdapter(_adventureViewInstaller, revealedRegistry);
            string readinessDiagnostic = adapter.GetReadinessDiagnostic();
            if (readinessDiagnostic != null)
            {
                SocAccessMod.Instance?.LogWarning(
                    "ScreenDetector "
                    + reason
                    + " adventure map adapter is not present: "
                    + readinessDiagnostic);
                return;
            }

            screen.Live = adapter;
        }

        public void OnTeleportMenuReady(TeleportMenu menu)
        {
            TeleportMenuAdapter adapter = new TeleportMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureMapScreen screen = Reg<AdventureMapScreen>();
            if (screen == null || screen.Live == null)
            {
                return;
            }

            // The teleport menu takes the whole screen over: a mini menu still standing on the map
            // would keep the destination cursor from the player.
            if (_screens.Current is MapEntityMiniMenuScreen)
            {
                Reg<MapEntityMiniMenuScreen>()?.Forget();
            }

            screen.EnterTeleportDestinationMode(adapter);
        }

        public void OnTeleportMenuClosed(TeleportMenu menu, bool cancelled)
        {
            AdventureMapScreen screen = Reg<AdventureMapScreen>();
            if (screen != null && screen.MatchesTeleportMenu(menu))
            {
                screen.ExitTeleportDestinationMode(menu, cancelled);
            }
        }

        // ---- the battle ----

        public void OnBattleSceneReady(BattleSceneInstaller installer)
        {
            _battleSceneInstaller = installer;
        }

        public bool OnCombatReady(ClientBattleCommandsFacade commands)
        {
            CombatAdapter adapter = new CombatAdapter(_battleSceneInstaller);
            if (!adapter.Matches(commands))
            {
                SocAccessMod.Instance?.LogWarning("ScreenDetector.OnCombatReady ignored because the battle command facade did not match the stored battle scene");
                return false;
            }

            CombatScreen screen = Reg<CombatScreen>();
            if (screen == null)
            {
                return false;
            }

            CombatEventNarrator.SetActiveAdapter(adapter);
            SocAccessMod.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.CombatEvents);
            screen.Live = adapter;
            return true;
        }

        public void OnCombatEnded()
        {
            _battleSceneInstaller = null;
            Reg<CombatScreen>()?.Forget();
            CombatEventNarrator.FlushPendingEventsForCombatEnd();
            CombatEventNarrator.Reset();
        }

        public void OnPreBattleMenuChanged(PreBattleMenu menu)
        {
            Reg<PreBattleMenuScreen>()?.Show(new PreBattleMenuAdapter(menu));
        }

        public void OnPreBattleMenuClosed(PreBattleMenu menu)
        {
            Reg<PreBattleMenuScreen>()?.Forget();
        }

        public void OnPostBattleResultReady(AdventureBattleMenu battleMenu)
        {
            PostBattleMenu menu = PostBattleResultAdapter.GetPostBattleMenu(battleMenu);
            Reg<PostBattleResultScreen>()?.Show(new PostBattleResultAdapter(battleMenu, menu));
        }

        public void OnPostBattleResultChanged()
        {
            // The page is pushed before the game has written its title, which arrives when the battle
            // animation ends: the same adapter, with news on it. The animation also makes the troop
            // and loot lines, and nothing changes them afterwards, so this is where the adapter stops
            // walking for them on every build.
            PostBattleResultScreen screen = Reg<PostBattleResultScreen>();
            if (screen == null)
            {
                return;
            }

            screen.Live?.MarkResultsAnimated();
            screen.SayNameIfChanged();
        }

        public void OnPostBattleResultClosed()
        {
            Reg<PostBattleResultScreen>()?.Forget();

            if (_screens.Contains<PostAdventureResultScreen>() || _screens.Contains<PostAdventureStatsScreen>())
            {
                return;
            }

            // Returning from manual combat can report SceneLoaderState.None before every adventure
            // dependency is ready, causing the normal map creation hook to reject the adapter. Once
            // post-battle closes, the native battle menu has completed and the map is readable again.
            AdventureMapScreen map = Reg<AdventureMapScreen>();
            if (map != null && map.Live == null)
            {
                ShowAdventureMap("post battle result closed");
            }
        }

        public void OnPostAdventureResultReady(PostAdventureMenu menu)
        {
            PostAdventureResultScreen screen = Reg<PostAdventureResultScreen>();
            if (screen == null)
            {
                return;
            }

            PostAdventureResultAdapter adapter = new PostAdventureResultAdapter(menu);
            // The post-adventure result is a root screen for the ended game: letting go of everything
            // else avoids briefly returning to the adventure map while transitioning away from
            // victory or defeat.
            ForgetAllExcept(screen);
            screen.Live = adapter;
        }

        public void OnPostAdventureResultClosed(PostAdventureMenu menu)
        {
            Reg<PostAdventureResultScreen>()?.Forget();
        }

        public void OnPostAdventureStatsReady(PostAdventureStatsMenu menu)
        {
            Reg<PostAdventureStatsScreen>()?.Show(new PostAdventureStatsAdapter(menu));
        }

        public void OnPostAdventureStatsClosed(PostAdventureStatsMenu menu)
        {
            Reg<PostAdventureStatsScreen>()?.Forget();
        }

        // ---- the hot reload ----

        /// <summary>
        /// Point every screen's slot at whatever the game is already showing - the one moment the mod
        /// scans the scene for menus, run once from <c>SocAccessMod.Start</c>. Everything after this is
        /// the patches telling us and the poll deciding.
        /// </summary>
        public void RecoverRuntimeState()
        {
            MainMenuScreen.Recover();
            CampaignMenuScreen.Recover();
            TaleSelectScreen.Recover();
            CustomCampaignSelectScreen.Recover();
            OnlineGameListScreen.Recover();
            OnlineHostGameScreen.Recover();
            CommunityMapsHomeScreen.Recover();
            CommunityMapsCollectionScreen.Recover();
            CommunityMapsDetailsScreen.Recover();
            CommunityMapsSearchFilterScreen.Recover();
            CommunityMapsSearchResultsScreen.Recover();
            CommunityMapsModalScreen.Recover();
            AdventureLobbyMapTypeScreen.Recover();
            AdventureLobbyRandomLayoutScreen.Recover();
            AdventureLobbyMapSelectScreen.Recover();
            AdventureLobbyChallengeMapSelectScreen.Recover();
            AdventureLobbyPlayersScreen.Recover();
            AdventureLobbyGameSettingsScreen.Recover();
            AdventureLobbyPlayerSettingsScreen.Recover();
            AdventureLobbyIconDropdownScreen.Recover();
            AdventureLobbyInviteProvidersScreen.Recover();
            PlatformUserMenuScreen.Recover();
            CampaignMapSelectScreen.Recover();
            AdventureMapScreen.Recover();
            AdventurePlayerMenuScreen.Recover();
            SendResourcePopupScreen.Recover();
            GiftTownPopupScreen.Recover();
            OwnedEntitiesScreen.Recover();
            TroopOverviewScreen.Recover();
            MarketplaceScreen.Recover();
            ArtifactMarketScreen.Recover();
            MapEntityMiniMenuScreen.Recover();
            CombatScreen.Recover();
            ChatScreen.Recover();
            SpellbookScreen.Recover();
            PostAdventureResultScreen.Recover();
            PostAdventureStatsScreen.Recover();
            PlayerStatsScreen.Recover();
            PostBattleResultScreen.Recover();
            PreBattleMenuScreen.Recover();
            ClaimMenuScreen.Recover();
            UpgradeTroopsScreen.Recover();
            DraftTroopsScreen.Recover();
            RallyPointScreen.Recover();
            SettlementScreen.Recover();
            DefenceMenuScreen.Recover();
            BuildMenuScreen.Recover();
            ResearchScreen.Recover();
            PurchaseWielderScreen.Recover();
            HostileJoinMenuScreen.Recover();
            MoveTroopPopupScreen.Recover();
            WorldChoiceMenuScreen.Recover();
            WorldConfirmMenuScreen.Recover();
            LevelUpScreen.Recover();
            CommanderSheetScreen.Recover();
            TradingScreen.Recover();
            StoryTextScreen.Recover();
            OptionsScreen.Recover();
            PauseMenuScreen.Recover();
            SaveLoadGameScreen.Recover();
            MessageDialogScreen.Recover();
            QuitToDesktopPopupScreen.Recover();
            CodexScreen.Recover();
            TutorialSlideshowScreen.Recover();
            TutorialSimpleScreen.Recover();
            LoadingCompleteScreen.Recover();
        }

        private static AdventureMapRevealedRegistry GetAdventureMapRevealedRegistry()
        {
            AdventureMapScannerState scannerState = SocAccessMod.Instance?.AdventureMapScannerState;
            return scannerState != null ? scannerState.RevealedRegistry : new AdventureMapRevealedRegistry();
        }

        /// <summary>Whether a story trigger is the local player's - a remote or AI commander's story
        /// is not the one this keyboard is waiting on.</summary>
        private static bool IsLocalStoryTrigger(OnTriggerPayload payload, IClientAdventureFacade facade)
        {
            if (payload == null || facade == null || payload.TriggerData == null)
            {
                return false;
            }

            TriggerType type = payload.TriggerData.Type;
            if (type != TriggerType.Message && type != TriggerType.Dialogue)
            {
                return false;
            }

            if (!facade.Teams.GetIsRemoteOrAI(payload.InteractingCommanderTeamId))
            {
                return true;
            }

            int sourceValue = (int)payload.Source;
            return sourceValue >= 2
                && sourceValue <= 8
                && !facade.Teams.GetIsRemoteOrAI(payload.TriggerCommanderTeamId);
        }
    }
}
