using System.Collections.Generic;
using _8_UILayer.ClientView.Menu.Paus;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
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
    internal sealed class ScreenDetector
    {
        private delegate Screen RuntimeScreenFactory();

        private readonly ScreenManager _screenManager;
        private readonly List<RuntimeScreenFactory> _runtimeScreenFactories;
        private AdventureViewInstaller _adventureViewInstaller;
        private BattleSceneInstaller _battleSceneInstaller;
        private bool _storySequenceActive;

        public ScreenDetector(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _runtimeScreenFactories = new List<RuntimeScreenFactory>
            {
                MainMenuScreen.TryBuildActiveScreen,
                FoldoutMenuScreen.TryBuildActiveScreen,
                CampaignMenuScreen.TryBuildActiveScreen,
                TaleSelectScreen.TryBuildActiveScreen,
                AdventureLobbyMapTypeScreen.TryBuildActiveScreen,
                CampaignMapSelectScreen.TryBuildActiveScreen,
                AdventureMapScreen.TryBuildActiveScreen,
                OwnedEntitiesScreen.TryBuildActiveScreen,
                TroopOverviewScreen.TryBuildActiveScreen,
                MarketplaceScreen.TryBuildActiveScreen,
                MapEntityMiniMenuScreen.TryBuildActiveScreen,
                CombatScreen.TryBuildActiveScreen,
                SpellbookScreen.TryBuildActiveScreen,
                PostAdventureResultScreen.TryBuildActiveScreen,
                PostAdventureStatsScreen.TryBuildActiveScreen,
                PostBattleResultScreen.TryBuildActiveScreen,
                PreBattleMenuScreen.TryBuildActiveScreen,
                ClaimMenuScreen.TryBuildActiveScreen,
                UpgradeTroopsScreen.TryBuildActiveDwellingScreen,
                DraftTroopsScreen.TryBuildActiveDwellingScreen,
                RallyPointScreen.TryBuildActiveScreen,
                DraftTroopsScreen.TryBuildActiveSettlementScreen,
                UpgradeTroopsScreen.TryBuildActiveSettlementScreen,
                SettlementScreen.TryBuildActiveScreen,
                DraftTroopsScreen.TryBuildActiveDefenceScreen,
                UpgradeTroopsScreen.TryBuildActiveDefenceScreen,
                DefenceMenuScreen.TryBuildActiveScreen,
                BuildMenuScreen.TryBuildActiveScreen,
                ResearchScreen.TryBuildActiveScreen,
                PurchaseWielderScreen.TryBuildActiveScreen,
                HostileJoinMenuScreen.TryBuildActiveScreen,
                MoveTroopPopupScreen.TryBuildActiveScreen,
                WorldChoiceMenuScreen.TryBuildActiveScreen,
                WorldConfirmMenuScreen.TryBuildActiveScreen,
                LevelUpScreen.TryBuildActiveScreen,
                CommanderSheetScreen.TryBuildActiveScreen,
                TradingScreen.TryBuildActiveScreen,
                () => StoryFocusBlockerScreen.TryBuildActiveScreen(() => _storySequenceActive),
                StoryTextScreen.TryBuildActiveLetterboxScreen,
                StoryTextScreen.TryBuildActiveScreen,
                StoryTextScreen.TryBuildActiveDialogueScreen,
                OptionsScreen.TryBuildActiveScreen,
                PauseMenuScreen.TryBuildActiveScreen,
                SaveLoadGameScreen.TryBuildActiveScreen,
                MessageDialogScreen.TryBuildActiveMapMessagePopupScreen,
                MessageDialogScreen.TryBuildActivePopupMenuScreen,
                MessageDialogScreen.TryBuildActiveConfirmPopupScreen,
                MessageDialogScreen.TryBuildActiveSystemPopupScreen,
                QuitToDesktopPopupScreen.TryBuildActiveScreen,
                CodexScreen.TryBuildActiveScreen,
                TutorialSlideshowScreen.TryBuildActiveScreen,
                TutorialSimpleScreen.TryBuildActiveScreen,
                LoadingCompleteScreen.TryBuildActiveScreen
            };
        }

        public void OnLoadingScreenReady(LoadingScreenMenu menu)
        {
            LoadingScreenAdapter adapter = new LoadingScreenAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            if (_screenManager.CurrentScreen is LoadingCompleteScreen)
            {
                return;
            }

            // The loading-complete prompt is a root screen; nothing from the previous game state should remain beneath it.
            _screenManager.Clear();
            Push(new LoadingCompleteScreen(adapter), "loading screen complete");
        }

        public void OnLoadingScreenClosed(LoadingScreenMenu menu)
        {
            if (_screenManager.CurrentScreen is LoadingCompleteScreen)
            {
                _screenManager.Pop<LoadingCompleteScreen>("loading screen closed");
            }
        }

        public void OnStorySequenceTrigger(OnTriggerPayload payload, IClientAdventureFacade facade)
        {
            if (!IsLocalStoryTrigger(payload, facade))
            {
                return;
            }

            _storySequenceActive = true;
            if (!_screenManager.Contains<StoryFocusBlockerScreen>())
            {
                _screenManager.Push(
                    new StoryFocusBlockerScreen(() => _storySequenceActive),
                    "story sequence focus blocker ready");
            }
        }

        public void OnStorySequenceCompleted()
        {
            _storySequenceActive = false;
            if (_screenManager.Contains<StoryTextScreen>())
            {
                _screenManager.Remove<StoryTextScreen>("story sequence completed");
            }

            if (_screenManager.Contains<StoryFocusBlockerScreen>())
            {
                _screenManager.Remove<StoryFocusBlockerScreen>("story sequence completed");
            }
        }

        public void OnPauseMenuReady(PauseMenu pauseMenu)
        {
            PauseMenuScreen screen = new PauseMenuScreen(new PauseMenuAdapter(pauseMenu));
            if (_screenManager.CurrentScreen is PauseMenuScreen)
            {
                _screenManager.RefreshTop<PauseMenuScreen>(screen, "pause menu changed");
                return;
            }

            Push(screen, "pause menu ready");
        }

        public void OnOptionsMenuReady(OptionsMenu optionsMenu)
        {
            OptionsMenuAdapter adapter = new OptionsMenuAdapter(optionsMenu);
            if (!adapter.IsPresent())
            {
                return;
            }

            OptionsScreen current = _screenManager.CurrentScreen as OptionsScreen;
            if (current != null)
            {
                current.Refresh();
                return;
            }

            OptionsScreen screen = new OptionsScreen(adapter);
            Push(screen, "options menu ready");
        }

        public void OnOptionsMenuChanged(OptionsMenu optionsMenu)
        {
            OptionsMenuAdapter adapter = new OptionsMenuAdapter(optionsMenu);
            if (!adapter.IsPresent())
            {
                return;
            }

            OptionsScreen current = _screenManager.CurrentScreen as OptionsScreen;
            if (current != null)
            {
                current.Refresh();
                return;
            }
        }

        public void OnOptionsMenuClosed(OptionsMenu optionsMenu)
        {
            _screenManager.Remove<OptionsScreen>("options menu closed");
        }

        public void OnPauseMenuClosed(PauseMenu pauseMenu)
        {
            _screenManager.Pop<PauseMenuScreen>("pause menu closed");
        }

        public void OnSaveLoadGameMenuReady(SaveLoadGameMenu menu)
        {
            SaveLoadGameMenuAdapter adapter = new SaveLoadGameMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            SaveLoadGameScreen current = _screenManager.CurrentScreen as SaveLoadGameScreen;
            if (current != null && current.Matches(menu))
            {
                return;
            }

            Push(new SaveLoadGameScreen(adapter), "save/load game menu ready");
        }

        public void OnSaveLoadGameMenuChanged(SaveLoadGameMenu menu)
        {
            SaveLoadGameMenuAdapter adapter = new SaveLoadGameMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            SaveLoadGameScreen current = _screenManager.Get<SaveLoadGameScreen>();
            if (current != null && current.Matches(menu))
            {
                current.Refresh();
            }
        }

        public void OnSaveLoadGameMenuClosed(SaveLoadGameMenu menu)
        {
            SaveLoadGameScreen current = _screenManager.CurrentScreen as SaveLoadGameScreen;
            if (current != null && current.Matches(menu))
            {
                _screenManager.Pop<SaveLoadGameScreen>("save/load game menu closed");
            }
        }

        public void OnOwnedEntitiesReady(KingdomEntityOverviewMenu menu)
        {
            KingdomEntityOverviewAdapter adapter = new KingdomEntityOverviewAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            if (_screenManager.CurrentScreen is OwnedEntitiesScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate owned entities ready while owned entities is already top");
                return;
            }

            Push(new OwnedEntitiesScreen(adapter), "owned entities ready");
        }

        public void OnOwnedEntitiesClosed(KingdomEntityOverviewMenu menu)
        {
            if (_screenManager.CurrentScreen is OwnedEntitiesScreen)
            {
                _screenManager.Pop<OwnedEntitiesScreen>("owned entities closed");
            }
        }

        public void OnTroopOverviewReady(KingdomTroopOverviewMenu menu)
        {
            KingdomTroopOverviewAdapter adapter = new KingdomTroopOverviewAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            if (_screenManager.CurrentScreen is TroopOverviewScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate troop overview ready while troop overview is already top");
                return;
            }

            Push(new TroopOverviewScreen(adapter), "troop overview ready");
        }

        public void OnTroopOverviewClosed(KingdomTroopOverviewMenu menu)
        {
            if (_screenManager.CurrentScreen is TroopOverviewScreen)
            {
                _screenManager.Pop<TroopOverviewScreen>("troop overview closed");
            }
        }

        public void OnMarketplaceReady(MarketplaceMenu menu)
        {
            MarketplaceMenuAdapter adapter = new MarketplaceMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            if (_screenManager.CurrentScreen is MarketplaceScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate marketplace ready while marketplace is already top");
                return;
            }

            Push(new MarketplaceScreen(adapter), "marketplace ready");
        }

        public void OnMarketplaceClosed(MarketplaceMenu menu)
        {
            if (_screenManager.CurrentScreen is MarketplaceScreen)
            {
                _screenManager.Pop<MarketplaceScreen>("marketplace closed");
            }
        }

        public void OnMarketplaceChanged()
        {
            MarketplaceScreen screen = _screenManager.CurrentScreen as MarketplaceScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<MarketplaceScreen>("marketplace no longer present");
                return;
            }

            screen.Refresh();
        }

        public void OnCodexReady(CodexMenu codexMenu)
        {
            CodexMenuAdapter adapter = new CodexMenuAdapter(codexMenu);
            if (!adapter.IsPresent())
            {
                return;
            }

            CodexScreen screen = new CodexScreen(adapter);
            Push(screen, "codex ready");
        }

        public void OnCodexClosed(CodexMenu codexMenu)
        {
            if (_screenManager.CurrentScreen is CodexScreen)
            {
                _screenManager.Pop<CodexScreen>("codex closed");
            }
        }

        public void OnCodexTabChanged(CodexMenu codexMenu)
        {
            CodexMenuAdapter adapter = new CodexMenuAdapter(codexMenu);
            if (!adapter.IsPresent())
            {
                return;
            }

            CodexScreen current = _screenManager.CurrentScreen as CodexScreen;
            if (current != null)
            {
                current.Refresh();
                return;
            }
        }

        public void OnCodexArticleChanged(CodexMenu codexMenu)
        {
            CodexScreen screen = _screenManager.CurrentScreen as CodexScreen;
            if (screen != null)
            {
                screen.Refresh();
            }
        }

        public void OnConfirmPopupReady(ConfirmPopup popup)
        {
            ConfirmPopupAdapter adapter = new ConfirmPopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new MessageDialogScreen(adapter), "confirm popup ready");
        }

        public void OnConfirmPopupClosed(ConfirmPopup popup)
        {
            if (!IsCurrentMessageDialogSource(popup))
            {
                return;
            }

            _screenManager.Pop<MessageDialogScreen>("confirm popup closed");
        }

        public void OnSystemPopupReady(SystemPopup popup)
        {
            SystemPopupAdapter adapter = new SystemPopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new MessageDialogScreen(adapter), "system popup ready");
        }

        public void OnSystemPopupClosed(SystemPopup popup)
        {
            if (!IsCurrentMessageDialogSource(popup))
            {
                return;
            }

            _screenManager.Pop<MessageDialogScreen>("system popup closed");
        }

        public void OnQuitToDesktopPopupReady(QuitToDesktopPopup popup)
        {
            QuitToDesktopPopupAdapter adapter = new QuitToDesktopPopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new QuitToDesktopPopupScreen(adapter), "quit to desktop popup ready");
        }

        public void OnQuitToDesktopPopupClosed(QuitToDesktopPopup popup)
        {
            if (_screenManager.CurrentScreen is QuitToDesktopPopupScreen)
            {
                _screenManager.Pop<QuitToDesktopPopupScreen>("quit to desktop popup closed");
            }
        }

        public void OnTutorialReady(TutorialMenu tutorialMenu)
        {
            Push(BuildTutorialScreen(tutorialMenu), "tutorial ready");
        }

        public void OnTutorialChanged(TutorialMenu tutorialMenu)
        {
            Screen screen = BuildTutorialScreen(tutorialMenu);
            if (_screenManager.CurrentScreen is TutorialSlideshowScreen)
            {
                _screenManager.RefreshTop<TutorialSlideshowScreen>(screen, "tutorial changed");
                return;
            }

            if (_screenManager.CurrentScreen is TutorialSimpleScreen)
            {
                _screenManager.RefreshTop<TutorialSimpleScreen>(screen, "tutorial changed");
                return;
            }

            LogUnexpectedTop("tutorial changed");
        }

        public void OnTutorialClosed(TutorialMenu tutorialMenu)
        {
            if (_screenManager.CurrentScreen is TutorialSlideshowScreen)
            {
                _screenManager.Pop<TutorialSlideshowScreen>("tutorial closed");
                return;
            }

            if (_screenManager.CurrentScreen is TutorialSimpleScreen)
            {
                _screenManager.Pop<TutorialSimpleScreen>("tutorial closed");
                return;
            }

            LogUnexpectedTop("tutorial closed");
        }

        public void OnPopupMenuReady(object sourceKey, PopupMenu.Settings settings)
        {
            if (settings == null)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector.OnPopupMenuReady received null settings");
                return;
            }

            object resolvedSourceKey = sourceKey ?? (settings != null ? (object)settings.ContainerTransform : null);
            MessageDialogScreen screen = new MessageDialogScreen(new PopupMenuAdapter(resolvedSourceKey, settings));
            if (IsCurrentMessageDialogSource(resolvedSourceKey))
            {
                _screenManager.RefreshTop<MessageDialogScreen>(screen, "popup menu refreshed");
                return;
            }

            Push(screen, "popup menu ready");
        }

        public void OnPopupMenuClosed(object sourceKey)
        {
            if (sourceKey != null && !IsCurrentMessageDialogSource(sourceKey))
            {
                return;
            }

            _screenManager.Pop<MessageDialogScreen>("popup menu closed");
        }

        public void OnMapMessagePopupReady(MapMessagePopup popup)
        {
            MapMessagePopupAdapter adapter = new MapMessagePopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new MessageDialogScreen(adapter), "map message popup ready");
        }

        public void OnMapMessagePopupClosed(MapMessagePopup popup)
        {
            if (popup != null && !IsCurrentMessageDialogSource(popup))
            {
                return;
            }

            _screenManager.Pop<MessageDialogScreen>("map message popup closed");
        }

        public void OnLetterboxStoryTextReady(LetterboxStoryText storyText)
        {
            Push(new StoryTextScreen(new LetterboxStoryTextAdapter(storyText)), "letterbox story text ready");
        }

        public void OnLetterboxStoryTextClosed(LetterboxStoryText storyText)
        {
            _screenManager.Pop<StoryTextScreen>("letterbox story text closed");
        }

        public void OnStoryTextReady(StoryText storyText)
        {
            Push(new StoryTextScreen(new StoryTextAdapter(storyText)), "story text ready");
        }

        public void OnStoryTextClosed(StoryText storyText)
        {
            _screenManager.Pop<StoryTextScreen>("story text closed");
        }

        public void OnDialogueMenuChanged(DialogueMenu dialogueMenu)
        {
            StoryTextScreen screen = new StoryTextScreen(new DialogueMenuAdapter(dialogueMenu));
            if (_screenManager.CurrentScreen is StoryTextScreen)
            {
                _screenManager.RefreshTop<StoryTextScreen>(screen, "dialogue menu changed");
                return;
            }

            Push(screen, "dialogue menu ready");
        }

        public void OnDialogueMenuClosed(DialogueMenu dialogueMenu)
        {
            _screenManager.Pop<StoryTextScreen>("dialogue menu closed");
        }

        public void OnMainMenuReady(MainMenu mainMenu)
        {
            // The main menu is a root screen; clearing avoids stale screens from a previous game/load state.
            _screenManager.Clear();
            Push(new MainMenuScreen(new MainMenuAdapter(mainMenu)), "main menu ready");
        }

        public void OnMainMenuClosed(MainMenu mainMenu)
        {
            if (_screenManager.CurrentScreen is FoldoutMenuScreen)
            {
                _screenManager.Pop<FoldoutMenuScreen>("main menu closed with foldout open");
            }

            _screenManager.Pop<MainMenuScreen>("main menu closed");
        }

        public void OnMainMenuFoldoutReady(MainMenu mainMenu, FoldoutUIButton foldoutButton)
        {
            if (mainMenu == null || foldoutButton == null)
            {
                return;
            }

            MainMenuAdapter owner = new MainMenuAdapter(mainMenu);
            MainMenuAdapter.NativeFoldoutAdapter foldout = ResolveFoldout(owner, foldoutButton);
            if (foldout == null || !owner.IsPresent() || !foldout.IsVisible() || !foldout.IsOpen())
            {
                return;
            }

            Push(new FoldoutMenuScreen(owner, foldout), "main menu foldout ready");
        }

        public void OnMainMenuFoldoutClosed(FoldoutUIButton foldoutButton)
        {
            _screenManager.Pop<FoldoutMenuScreen>("main menu foldout closed");
        }

        public void OnCampaignMenuReady(CampaignMenu campaignMenu)
        {
            Push(new CampaignMenuScreen(new CampaignMenuAdapter(campaignMenu)), "campaign menu ready");
        }

        public void OnCampaignMenuClosed(CampaignMenu campaignMenu)
        {
            _screenManager.Pop<CampaignMenuScreen>("campaign menu closed");
        }

        public void OnTaleSelectLayoutRebuilt(TaleButtonLayoutCoordinator coordinator)
        {
            TaleSelectScreen screen = new TaleSelectScreen(new TaleSelectAdapter(coordinator));
            if (_screenManager.CurrentScreen is TaleSelectScreen)
            {
                _screenManager.RefreshTop<TaleSelectScreen>(screen, "tale select layout rebuilt");
                return;
            }

            Push(screen, "tale select ready");
        }

        public void OnTaleSelectClosed(TaleButtonLayoutCoordinator coordinator)
        {
            _screenManager.Pop<TaleSelectScreen>("tale select closed");
        }

        public void OnCampaignMapSelectShown(CampaignMapSelectMenu menu, CampaignMapSelectedInformationView informationView)
        {
            CampaignMapSelectScreen screen = new CampaignMapSelectScreen(
                new CampaignMapSelectAdapter(menu, informationView),
                CampaignMapSelectScreen.ConsumeFocusDifficultyAfterNextRebuild());

            if (_screenManager.CurrentScreen is CampaignMapSelectScreen)
            {
                _screenManager.RefreshTop<CampaignMapSelectScreen>(screen, "campaign map select shown");
                return;
            }

            Push(screen, "campaign map select ready");
        }

        public void OnCampaignMapSelectClosed(CampaignMapSelectedInformationView informationView)
        {
            _screenManager.Pop<CampaignMapSelectScreen>("campaign map select closed");
        }

        public void OnAdventureLobbyMapTypeReady(MapTypeMenu menu)
        {
            AdventureLobbyMapTypeAdapter adapter = new AdventureLobbyMapTypeAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyMapTypeScreen screen = new AdventureLobbyMapTypeScreen(adapter);
            if (_screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                _screenManager.RefreshTop<AdventureLobbyMapTypeScreen>(screen, "adventure lobby map type shown");
                return;
            }

            Push(screen, "adventure lobby map type ready");
        }

        public void OnAdventureLobbyMapTypeClosed(MapTypeMenu menu)
        {
            if (_screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                _screenManager.Pop<AdventureLobbyMapTypeScreen>("adventure lobby map type closed");
            }
        }

        public void OnMainMenuSceneLoaded(MainMenuSceneType loadedScene)
        {
            if (loadedScene == MainMenuSceneType.MainMenu)
            {
                SocAccessPlugin.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.AdventureMapNotifications);
                SocAccessPlugin.Instance?.AdventureMapScannerState?.Clear();
            }

            if (loadedScene != MainMenuSceneType.Campaign && _screenManager.CurrentScreen is CampaignMenuScreen)
            {
                _screenManager.Pop<CampaignMenuScreen>("main menu scene changed away from campaign");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                _screenManager.Pop<AdventureLobbyMapTypeScreen>("main menu scene changed away from adventure lobby");
            }
        }

        public void OnAdventureViewReady(AdventureViewInstaller installer)
        {
            _adventureViewInstaller = installer;
        }

        public void OnAdventureMapReady()
        {
            AdventureMapRevealedRegistry revealedRegistry = GetAdventureMapRevealedRegistry();
            AdventureMapAdapter adapter = new AdventureMapAdapter(_adventureViewInstaller, revealedRegistry);
            AdventureMapEventListener eventListener = adapter.IsPresent()
                ? new AdventureMapEventListener(
                    adapter.Facade,
                    adapter.SelectionHandler,
                    adapter.HumanAdventureControllerFacade,
                    adapter.LocalizationHandler,
                    adapter.FogManager,
                    revealedRegistry)
                : null;
            Push(new AdventureMapScreen(adapter, eventListener), "adventure map ready");
        }

        public void OnAdventureMapClosed()
        {
            _adventureViewInstaller = null;
            _screenManager.Pop<AdventureMapScreen>("adventure map closed");
        }

        public void OnMapEntityMiniMenuReady(MapEntityMiniMenu menu)
        {
            MapEntityMiniMenuAdapter adapter = new MapEntityMiniMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new MapEntityMiniMenuScreen(adapter), "map entity mini menu ready");
        }

        public void OnMapEntityMiniMenuClosed(MapEntityMiniMenu menu)
        {
            // Selling a building closes the native mini menu from inside the
            // confirm popup's async callback, before ConfirmPopup.Close's
            // postfix pops MessageDialogScreen. Remove the mini menu screen
            // even when it is temporarily below that dialog.
            _screenManager.Remove<MapEntityMiniMenuScreen>("map entity mini menu closed");
        }

        public void OnBattleSceneReady(BattleSceneInstaller installer)
        {
            _battleSceneInstaller = installer;
        }

        public bool OnCombatReady(ClientBattleCommandsFacade commands)
        {
            CombatAdapter adapter = new CombatAdapter(_battleSceneInstaller);
            if (!adapter.Matches(commands))
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector.OnCombatReady ignored because the battle command facade did not match the stored battle scene");
                return false;
            }

            CombatEventNarrator.SetActiveAdapter(adapter);
            SocAccessPlugin.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.CombatEvents);
            CombatScreen screen = new CombatScreen(adapter);
            if (IsTutorialTopScreen())
            {
                return PushBelowTop(screen, "combat ready");
            }

            return Push(screen, "combat ready");
        }

        public void OnSpellbookReady(SpellBook spellbook)
        {
            SpellbookScreen screen = new SpellbookScreen(new SpellbookAdapter(spellbook));
            if (_screenManager.CurrentScreen is SpellbookScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate spellbook ready while spellbook is already top");
                return;
            }

            Push(screen, "spellbook ready");
        }

        public void OnSpellbookClosed(SpellBook spellbook)
        {
            _screenManager.Pop<SpellbookScreen>("spellbook closed");
        }

        public void OnPostBattleResultOpening()
        {
            _battleSceneInstaller = null;
            if (_screenManager.Contains<CombatScreen>())
            {
                _screenManager.Remove<CombatScreen>("post battle result opening");
                SocAccessPlugin.Instance?.LogInfo("ScreenDetector removed CombatScreen before post battle result");
            }

            CombatEventNarrator.Reset();
        }

        public void OnPostBattleResultReady(AdventureBattleMenu battleMenu)
        {
            PostBattleMenu menu = PostBattleResultAdapter.GetPostBattleMenu(battleMenu);
            Push(new PostBattleResultScreen(new PostBattleResultAdapter(battleMenu, menu)), "post battle result ready");
        }

        public void OnPostBattleResultChanged()
        {
            PostBattleResultScreen screen = _screenManager.CurrentScreen as PostBattleResultScreen;
            if (screen == null)
            {
                LogUnexpectedTop("post battle result changed");
                return;
            }

            _screenManager.RefreshTop<PostBattleResultScreen>(screen.Rebuild(), "post battle result changed");
        }

        public void OnPostBattleResultClosed()
        {
            // PostBattleMenu invokes its completion callback before it hides itself.
            // If a victorious town attack opens the claim menu from that callback,
            // ClaimMenuScreen is already top when PostBattleMenu.Hide runs, so this
            // must remove the victory screen from below the claim menu.
            _screenManager.Remove<PostBattleResultScreen>("post battle result closed");
        }

        public void OnPostAdventureResultReady(PostAdventureMenu menu)
        {
            PostAdventureResultScreen screen = new PostAdventureResultScreen(new PostAdventureResultAdapter(menu));
            if (_screenManager.CurrentScreen is PostAdventureResultScreen)
            {
                _screenManager.RefreshTop<PostAdventureResultScreen>(screen, "post adventure result ready");
                return;
            }

            Push(screen, "post adventure result ready");
        }

        public void OnPostAdventureResultClosed(PostAdventureMenu menu)
        {
            if (_screenManager.CurrentScreen is PostAdventureResultScreen)
            {
                _screenManager.Pop<PostAdventureResultScreen>("post adventure result closed");
            }
        }

        public void OnPostAdventureStatsReady(PostAdventureStatsMenu menu)
        {
            PostAdventureStatsScreen screen = new PostAdventureStatsScreen(new PostAdventureStatsAdapter(menu));
            if (_screenManager.CurrentScreen is PostAdventureStatsScreen)
            {
                _screenManager.RefreshTop<PostAdventureStatsScreen>(screen, "post adventure stats ready");
                return;
            }

            Push(screen, "post adventure stats ready");
        }

        public void OnPostAdventureStatsClosed(PostAdventureStatsMenu menu)
        {
            if (_screenManager.CurrentScreen is PostAdventureStatsScreen)
            {
                _screenManager.Pop<PostAdventureStatsScreen>("post adventure stats closed");
            }
        }

        public void OnPreBattleMenuReady(PreBattleMenu menu)
        {
            Push(new PreBattleMenuScreen(new PreBattleMenuAdapter(menu)), "pre battle menu ready");
        }

        public void OnPreBattleMenuChanged(PreBattleMenu menu)
        {
            PreBattleMenuScreen screen = new PreBattleMenuScreen(new PreBattleMenuAdapter(menu));
            if (_screenManager.CurrentScreen is PreBattleMenuScreen)
            {
                _screenManager.RefreshTop<PreBattleMenuScreen>(screen, "pre battle menu changed");
                return;
            }

            Push(screen, "pre battle menu ready");
        }

        public void OnPreBattleMenuClosed(PreBattleMenu menu)
        {
            _screenManager.Pop<PreBattleMenuScreen>("pre battle menu closed");
        }

        public void OnClaimMenuReady(ClaimMenu menu)
        {
            ClaimMenuAdapter adapter = new ClaimMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            ClaimMenuScreen current = _screenManager.CurrentScreen as ClaimMenuScreen;
            if (current != null && current.Matches(menu))
            {
                return;
            }

            Push(new ClaimMenuScreen(adapter), "claim menu ready");
        }

        public void OnClaimMenuClosed(ClaimMenu menu)
        {
            ClaimMenuScreen current = _screenManager.CurrentScreen as ClaimMenuScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<ClaimMenuScreen>("claim menu closed");
            }
        }

        public void OnWorldChoiceMenuReady(WorldChoiceMenu menu)
        {
            WorldChoiceMenuAdapter adapter = new WorldChoiceMenuAdapter(menu);
            Push(new WorldChoiceMenuScreen(adapter), "world choice menu ready");
        }

        public void OnWorldChoiceMenuClosed(WorldChoiceMenu menu)
        {
            _screenManager.Pop<WorldChoiceMenuScreen>("world choice menu closed");
        }

        public void OnWorldConfirmMenuReady(WorldConfirmMenu menu)
        {
            WorldConfirmMenuAdapter adapter = new WorldConfirmMenuAdapter(menu);
            Push(new WorldConfirmMenuScreen(adapter), "world confirm menu ready");
        }

        public void OnWorldConfirmMenuClosed(WorldConfirmMenu menu)
        {
            if (_screenManager.CurrentScreen is WorldConfirmMenuScreen)
            {
                _screenManager.Pop<WorldConfirmMenuScreen>("world confirm menu closed");
            }
        }

        public void OnDwellingInteractionReady(DwellingInteractionMenu menu)
        {
            DraftTroopsScreen screen = new DraftTroopsScreen(new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu)));
            DraftTroopsScreen currentDraft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (currentDraft != null && currentDraft.HostIdPrefix == "dwelling")
            {
                _screenManager.RefreshTop<DraftTroopsScreen>(screen, "dwelling draft changed");
                return;
            }

            Push(screen, "dwelling draft ready");
        }

        public void OnDwellingUpgradeReady(DwellingInteractionMenu menu)
        {
            DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (draft != null && draft.HostIdPrefix == "dwelling")
            {
                _screenManager.Pop<DraftTroopsScreen>("dwelling upgrade opened");
            }

            Push(new UpgradeTroopsScreen(new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu))), "dwelling upgrade ready");
        }

        public void OnDwellingBackToTop(DwellingInteractionMenu menu)
        {
            UpgradeTroopsScreen upgrade = _screenManager.CurrentScreen as UpgradeTroopsScreen;
            if (upgrade != null && upgrade.HostIdPrefix == "dwelling")
            {
                _screenManager.Pop<UpgradeTroopsScreen>("dwelling upgrade closed");
            }
            else
            {
                DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
                if (draft != null && draft.HostIdPrefix == "dwelling")
                {
                    SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored dwelling back to top while draft is already top");
                    return;
                }
            }

            Push(new DraftTroopsScreen(new DwellingTroopManagementHostAdapter(new DwellingInteractionMenuAdapter(menu))), "dwelling draft ready");
        }

        public void OnDwellingInteractionClosed(DwellingInteractionMenu menu)
        {
            UpgradeTroopsScreen upgrade = _screenManager.CurrentScreen as UpgradeTroopsScreen;
            if (upgrade != null && upgrade.HostIdPrefix == "dwelling")
            {
                _screenManager.Pop<UpgradeTroopsScreen>("dwelling closed with upgrade open");
            }

            DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (draft != null && draft.HostIdPrefix == "dwelling")
            {
                _screenManager.Pop<DraftTroopsScreen>("dwelling interaction closed");
            }
        }

        public void OnRallyPointReady(RallyPointInteractionMenu menu)
        {
            RallyPointScreen screen = new RallyPointScreen(new RallyPointInteractionMenuAdapter(menu));
            Push(screen, "rally point ready");
        }

        public void OnRallyPointChanged(RallyPointInteractionMenu menu)
        {
            RallyPointScreen current = _screenManager.CurrentScreen as RallyPointScreen;
            if (current == null)
            {
                return;
            }

            if (!current.IsPresent())
            {
                _screenManager.Pop<RallyPointScreen>("rally point no longer present");
                return;
            }

            current.Refresh();
        }

        public void OnRallyPointClosed(RallyPointInteractionMenu menu)
        {
            if (_screenManager.CurrentScreen is RallyPointScreen)
            {
                _screenManager.Pop<RallyPointScreen>("rally point closed");
            }
        }

        public void OnSettlementReady(TownInteractionMenu menu)
        {
            SettlementScreen screen = new SettlementScreen(new TownInteractionMenuAdapter(menu));
            if (_screenManager.CurrentScreen is SettlementScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate settlement ready while settlement is already top");
                return;
            }

            Push(screen, "settlement ready");
        }

        public void OnSettlementDraftReady(TownInteractionMenu menu)
        {
            if (_screenManager.CurrentScreen is SettlementScreen)
            {
                _screenManager.Pop<SettlementScreen>("settlement draft opened");
            }

            Push(new DraftTroopsScreen(new SettlementTroopManagementHostAdapter(new TownInteractionMenuAdapter(menu))), "settlement draft ready");
        }

        public void OnSettlementUpgradeReady(TownInteractionMenu menu)
        {
            if (_screenManager.CurrentScreen is SettlementScreen)
            {
                _screenManager.Pop<SettlementScreen>("settlement upgrade opened");
            }

            Push(new UpgradeTroopsScreen(new SettlementTroopManagementHostAdapter(new TownInteractionMenuAdapter(menu))), "settlement upgrade ready");
        }

        public void OnSettlementBackToTop(TownInteractionMenu menu)
        {
            DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (draft != null && draft.HostIdPrefix == "settlement")
            {
                _screenManager.Pop<DraftTroopsScreen>("settlement draft closed");
            }
            else
            {
                UpgradeTroopsScreen upgrade = _screenManager.CurrentScreen as UpgradeTroopsScreen;
                if (upgrade != null && upgrade.HostIdPrefix == "settlement")
                {
                    _screenManager.Pop<UpgradeTroopsScreen>("settlement upgrade closed");
                }
                else if (_screenManager.CurrentScreen is SettlementScreen)
                {
                    SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored settlement back to top while settlement is already top");
                    return;
                }
            }

            Push(new SettlementScreen(new TownInteractionMenuAdapter(menu)), "settlement top level ready");
        }

        public void OnSettlementClosed(TownInteractionMenu menu)
        {
            DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (draft != null && draft.HostIdPrefix == "settlement")
            {
                _screenManager.Pop<DraftTroopsScreen>("settlement closed with draft open");
            }

            UpgradeTroopsScreen upgrade = _screenManager.CurrentScreen as UpgradeTroopsScreen;
            if (upgrade != null && upgrade.HostIdPrefix == "settlement")
            {
                _screenManager.Pop<UpgradeTroopsScreen>("settlement closed with upgrade open");
            }

            if (_screenManager.CurrentScreen is SettlementScreen)
            {
                _screenManager.Pop<SettlementScreen>("settlement closed");
            }
        }

        public void OnDefenceMenuReady(DefenceMenu menu)
        {
            DefenceMenuScreen screen = new DefenceMenuScreen(new DefenceMenuAdapter(menu));
            if (_screenManager.CurrentScreen is DefenceMenuScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate defence menu ready while defence menu is already top");
                return;
            }

            Push(screen, "defence menu ready");
        }

        public void OnDefenceDraftReady(DefenceMenu menu)
        {
            if (_screenManager.CurrentScreen is DefenceMenuScreen)
            {
                _screenManager.Pop<DefenceMenuScreen>("defence draft opened");
            }

            Push(new DraftTroopsScreen(new DefenceTroopManagementHostAdapter(new DefenceMenuAdapter(menu))), "defence draft ready");
        }

        public void OnDefenceUpgradeReady(DefenceMenu menu)
        {
            if (_screenManager.CurrentScreen is DefenceMenuScreen)
            {
                _screenManager.Pop<DefenceMenuScreen>("defence upgrade opened");
            }

            Push(new UpgradeTroopsScreen(new DefenceTroopManagementHostAdapter(new DefenceMenuAdapter(menu))), "defence upgrade ready");
        }

        public void OnDefenceMenuBackToTop(DefenceMenu menu)
        {
            DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (draft != null && draft.HostIdPrefix == "defences")
            {
                _screenManager.Pop<DraftTroopsScreen>("defence draft closed");
            }
            else
            {
                UpgradeTroopsScreen upgrade = _screenManager.CurrentScreen as UpgradeTroopsScreen;
                if (upgrade != null && upgrade.HostIdPrefix == "defences")
                {
                    _screenManager.Pop<UpgradeTroopsScreen>("defence upgrade closed");
                }
                else if (_screenManager.CurrentScreen is DefenceMenuScreen)
                {
                    SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored defence back to top while defence menu is already top");
                    return;
                }
                else
                {
                    return;
                }
            }

            Push(new DefenceMenuScreen(new DefenceMenuAdapter(menu)), "defence top level ready");
        }

        public void OnDefenceMenuClosed(DefenceMenu menu)
        {
            DraftTroopsScreen draft = _screenManager.CurrentScreen as DraftTroopsScreen;
            if (draft != null && draft.HostIdPrefix == "defences")
            {
                _screenManager.Pop<DraftTroopsScreen>("defence closed with draft open");
            }

            UpgradeTroopsScreen upgrade = _screenManager.CurrentScreen as UpgradeTroopsScreen;
            if (upgrade != null && upgrade.HostIdPrefix == "defences")
            {
                _screenManager.Pop<UpgradeTroopsScreen>("defence closed with upgrade open");
            }

            if (_screenManager.CurrentScreen is DefenceMenuScreen)
            {
                _screenManager.Pop<DefenceMenuScreen>("defence closed");
            }
        }

        public void OnBuildMenuReady(BuildMenu menu)
        {
            if (_screenManager.CurrentScreen is BuildMenuScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate build menu ready while build menu is already top");
                return;
            }

            BuildMenuAdapter adapter = new BuildMenuAdapter(menu);
            BuildMenuScreen screen = new BuildMenuScreen(adapter);
            Push(screen, "build menu ready");
        }

        public void OnBuildMenuClosed(BuildMenu menu)
        {
            if (_screenManager.CurrentScreen is BuildMenuScreen)
            {
                _screenManager.Pop<BuildMenuScreen>("build menu closed");
            }
        }

        public void OnBuildMenuSiteChanged(BuildMenu menu)
        {
            RefreshCurrentBuildMenu();
        }

        public void OnBuildMenuCategoryChanged(BuildMenu menu)
        {
            RefreshCurrentBuildMenu();
        }

        public void OnResearchMenuReady(ResearchMenu menu)
        {
            ResearchMenuAdapter adapter = new ResearchMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            if (_screenManager.CurrentScreen is ResearchScreen)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate research menu ready while research menu is already top");
                return;
            }

            Push(new ResearchScreen(adapter), "research menu ready");
        }

        public void OnResearchMenuClosed(ResearchMenu menu)
        {
            if (_screenManager.CurrentScreen is ResearchScreen)
            {
                _screenManager.Pop<ResearchScreen>("research menu closed");
            }
        }

        public void OnResearchMenuChanged(ResearchMenu menu)
        {
            RefreshCurrentResearchMenu();
        }

        public void OnPurchaseWielderReady(PurchaseWielderMenu menu)
        {
            PurchaseWielderMenuAdapter adapter = new PurchaseWielderMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new PurchaseWielderScreen(adapter), "purchase wielder ready");
        }

        public void OnPurchaseWielderClosed(PurchaseWielderMenu menu)
        {
            PurchaseWielderScreen current = _screenManager.CurrentScreen as PurchaseWielderScreen;
            if (current != null && (menu == null || ReferenceEquals(current.Adapter.Source, menu)))
            {
                _screenManager.Pop<PurchaseWielderScreen>("purchase wielder closed");
            }
        }

        private void RefreshCurrentBuildMenu()
        {
            BuildMenuScreen screen = _screenManager.CurrentScreen as BuildMenuScreen;
            if (screen == null || !screen.IsPresent())
            {
                return;
            }

            screen.Refresh();
        }

        private void RefreshCurrentResearchMenu()
        {
            ResearchScreen screen = _screenManager.CurrentScreen as ResearchScreen;
            if (screen == null || !screen.IsPresent())
            {
                return;
            }

            screen.Refresh();
        }

        public void OnLevelUpMenuReady(CommanderLevelUpMenu menu)
        {
            LevelUpScreen screen = new LevelUpScreen(new LevelUpMenuAdapter(menu));
            if (_screenManager.CurrentScreen is LevelUpScreen)
            {
                _screenManager.RefreshTop<LevelUpScreen>(screen, "level up menu changed");
                return;
            }

            Push(screen, "level up menu ready");
        }

        public void OnLevelUpMenuClosed(CommanderLevelUpMenu menu)
        {
            if (_screenManager.CurrentScreen is LevelUpScreen)
            {
                _screenManager.Pop<LevelUpScreen>("level up menu closed");
            }
        }

        public void OnHostileJoinMenuReady(HostileJoinMenu menu)
        {
            HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                adapter.Dispose();
                return;
            }

            Push(new HostileJoinMenuScreen(adapter), "hostile join menu ready");
        }

        public void OnHostileJoinMenuChanged(HostileJoinMenu menu)
        {
            HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                adapter.Dispose();
                return;
            }

            HostileJoinMenuScreen current = _screenManager.CurrentScreen as HostileJoinMenuScreen;
            if (current != null)
            {
                adapter.Dispose();
                current.Refresh();
                return;
            }

            HostileJoinMenuScreen screen = new HostileJoinMenuScreen(adapter);
            Push(screen, "hostile join menu ready");
        }

        public void OnHostileJoinMenuClosed(HostileJoinMenu menu)
        {
            _screenManager.Pop<HostileJoinMenuScreen>("hostile join menu closed");
        }

        public void OnMoveTroopPopupReady(TroopHUDEntryMovable movable)
        {
            if (_screenManager.CurrentScreen is MoveTroopPopupScreen)
            {
                return;
            }

            Push(new MoveTroopPopupScreen(new MoveTroopPopupAdapter(movable)), "move troop popup ready");
        }

        public void OnMoveTroopPopupClosed(TroopHUDEntryMovable movable)
        {
            // The game calls TroopHUDEntryMovable.Reset even when the troop move
            // popup is not open, such as during HUD teardown and refresh.
            if (!_screenManager.Contains<MoveTroopPopupScreen>())
            {
                return;
            }

            _screenManager.Pop<MoveTroopPopupScreen>("move troop popup closed");
        }

        public void OnCommanderSheetReady(CommanderSheet commanderSheet)
        {
            Push(new CommanderSheetScreen(new CommanderSheetAdapter(commanderSheet)), "commander sheet ready");
        }

        public void OnCommanderSheetClosed(CommanderSheet commanderSheet)
        {
            _screenManager.Pop<CommanderSheetScreen>("commander sheet closed");
        }

        public void OnCommanderSheetChanged()
        {
            CommanderSheetScreen screen = _screenManager.CurrentScreen as CommanderSheetScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<CommanderSheetScreen>("commander sheet no longer present");
                return;
            }

            screen.Refresh();
        }

        public void OnCommanderSheetComponentChanged(UnityEngine.Component component)
        {
            if (component == null || component.GetComponentInParent<CommanderSheet>(true) == null)
            {
                return;
            }

            OnCommanderSheetChanged();
        }

        public void OnTradingMenuReady(TradingMenu menu)
        {
            TradingScreen current = _screenManager.CurrentScreen as TradingScreen;
            if (current != null)
            {
                current.Refresh();
                return;
            }

            TradingScreen screen = new TradingScreen(new TradingMenuAdapter(menu));
            Push(screen, "trading menu ready");
        }

        public void OnTradingMenuClosed(TradingMenu menu)
        {
            if (_screenManager.CurrentScreen is TradingScreen)
            {
                _screenManager.Pop<TradingScreen>("trading menu closed");
            }
        }

        public void OnTradingMenuChanged()
        {
            TradingScreen screen = _screenManager.CurrentScreen as TradingScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<TradingScreen>("trading menu no longer present");
                return;
            }

            screen.Refresh();
        }

        public void ResyncFromRuntimeState()
        {
            List<Screen> activeScreens = new List<Screen>();
            for (int i = 0; i < _runtimeScreenFactories.Count; i++)
            {
                Screen screen = _runtimeScreenFactories[i]();
                if (screen != null && screen.IsPresent())
                {
                    activeScreens.Add(screen);
                }
            }

            _screenManager.Clear();
            for (int i = 0; i < activeScreens.Count; i++)
            {
                _screenManager.Push(activeScreens[i], "runtime resync");
            }
        }

        private bool Push(Screen screen, string reason)
        {
            if (screen == null)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SocAccessPlugin.Instance?.LogWarning(
                    "ScreenDetector ignored "
                    + reason
                    + " because "
                    + screen.GetType().Name
                    + " is not present");
                return false;
            }

            _screenManager.Push(screen, reason);
            return true;
        }

        private bool PushBelowTop(Screen screen, string reason)
        {
            if (screen == null)
            {
                SocAccessPlugin.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SocAccessPlugin.Instance?.LogWarning(
                    "ScreenDetector ignored "
                    + reason
                    + " because "
                    + screen.GetType().Name
                    + " is not present");
                return false;
            }

            _screenManager.PushBelowTop(screen, reason);
            return true;
        }

        private static AdventureMapRevealedRegistry GetAdventureMapRevealedRegistry()
        {
            AdventureMapScannerState scannerState = SocAccessPlugin.Instance?.AdventureMapScannerState;
            return scannerState != null ? scannerState.RevealedRegistry : new AdventureMapRevealedRegistry();
        }

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

        private bool IsTutorialTopScreen()
        {
            return _screenManager.CurrentScreen is TutorialSlideshowScreen
                || _screenManager.CurrentScreen is TutorialSimpleScreen;
        }

        private static Screen BuildTutorialScreen(TutorialMenu tutorialMenu)
        {
            TutorialSlideshowAdapter slideshowAdapter = new TutorialSlideshowAdapter(tutorialMenu);
            if (slideshowAdapter.IsPresent())
            {
                return new TutorialSlideshowScreen(slideshowAdapter);
            }

            TutorialSimpleAdapter simpleAdapter = new TutorialSimpleAdapter(tutorialMenu);
            if (simpleAdapter.IsPresent())
            {
                return new TutorialSimpleScreen(simpleAdapter);
            }

            return null;
        }

        private static MainMenuAdapter.NativeFoldoutAdapter ResolveFoldout(MainMenuAdapter adapter, FoldoutUIButton foldoutButton)
        {
            if (adapter == null || foldoutButton == null)
            {
                return null;
            }

            if (adapter.ExtrasFoldout != null && ReferenceEquals(adapter.ExtrasFoldout.SourceKey, foldoutButton))
            {
                return adapter.ExtrasFoldout;
            }

            if (adapter.MultiplayerFoldout != null && ReferenceEquals(adapter.MultiplayerFoldout.SourceKey, foldoutButton))
            {
                return adapter.MultiplayerFoldout;
            }

            return null;
        }

        private void LogUnexpectedTop(string reason)
        {
            Screen current = _screenManager.CurrentScreen;
            SocAccessPlugin.Instance?.LogWarning(
                "ScreenDetector ignored "
                + reason
                + "; unexpected top screen "
                + (current != null ? current.GetType().Name : "<none>"));
        }

        private bool IsCurrentMessageDialogSource(object sourceKey)
        {
            MessageDialogScreen screen = _screenManager.CurrentScreen as MessageDialogScreen;
            if (screen == null)
            {
                return false;
            }

            object currentSourceKey = screen.SourceKey;
            return sourceKey == null
                || currentSourceKey == null
                || ReferenceEquals(sourceKey, currentSourceKey);
        }
    }
}
