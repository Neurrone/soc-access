using System.Collections;
using System.Collections.Generic;
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
    public sealed class ScreenDetector
    {
        private delegate Screen RuntimeScreenFactory();

        private readonly ScreenManager _screenManager;
        private readonly List<RuntimeScreenFactory> _runtimeScreenFactories;
        private AdventureViewInstaller _adventureViewInstaller;
        private BattleSceneInstaller _battleSceneInstaller;
        private IconDropdown _deferredAdventureLobbyDropdownClose;
        private bool _deferredAdventureLobbyDropdownHidden;
        private float _deferredAdventureLobbyDropdownDeadline;
        private bool _storySequenceActive;
        private bool _communityMapsHomeContentRefreshPending;
        private bool _artifactMarketRefreshPending;

        public ScreenDetector(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _runtimeScreenFactories = new List<RuntimeScreenFactory>
            {
                MainMenuScreen.TryBuildActiveScreen,
                FoldoutMenuScreen.TryBuildActiveScreen,
                CampaignMenuScreen.TryBuildActiveScreen,
                TaleSelectScreen.TryBuildActiveScreen,
                CustomCampaignSelectScreen.TryBuildActiveScreen,
                OnlineGameListScreen.TryBuildActiveScreen,
                OnlineHostGameScreen.TryBuildActiveScreen,
                CommunityMapsHomeScreen.TryBuildActiveScreen,
                CommunityMapsCollectionScreen.TryBuildActiveScreen,
                CommunityMapsDetailsScreen.TryBuildActiveScreen,
                CommunityMapsSearchFilterScreen.TryBuildActiveScreen,
                CommunityMapsSearchResultsScreen.TryBuildActiveScreen,
                CommunityMapsModalScreen.TryBuildActiveScreen,
                AdventureLobbyMapTypeScreen.TryBuildActiveScreen,
                AdventureLobbyRandomLayoutScreen.TryBuildActiveScreen,
                AdventureLobbyMapSelectScreen.TryBuildActiveScreen,
                AdventureLobbyChallengeMapSelectScreen.TryBuildActiveScreen,
                AdventureLobbyPlayersScreen.TryBuildActiveScreen,
                AdventureLobbyGameSettingsScreen.TryBuildActiveScreen,
                AdventureLobbyPlayerSettingsScreen.TryBuildActiveScreen,
                AdventureLobbyIconDropdownScreen.TryBuildActiveScreen,
                AdventureLobbyInviteProvidersScreen.TryBuildActiveScreen,
                PlatformUserMenuScreen.TryBuildActiveScreen,
                CampaignMapSelectScreen.TryBuildActiveScreen,
                AdventureMapScreen.TryBuildActiveScreen,
                AdventurePlayerMenuScreen.TryBuildActiveScreen,
                SendResourcePopupScreen.TryBuildActiveScreen,
                GiftTownPopupScreen.TryBuildActiveScreen,
                OwnedEntitiesScreen.TryBuildActiveScreen,
                TroopOverviewScreen.TryBuildActiveScreen,
                MarketplaceScreen.TryBuildActiveScreen,
                ArtifactMarketScreen.TryBuildActiveScreen,
                MapEntityMiniMenuScreen.TryBuildActiveScreen,
                CombatScreen.TryBuildActiveScreen,
                ChatScreen.TryBuildActiveScreen,
                SpellbookScreen.TryBuildActiveScreen,
                PostAdventureResultScreen.TryBuildActiveScreen,
                PostAdventureStatsScreen.TryBuildActiveScreen,
                PlayerStatsScreen.TryBuildActiveScreen,
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
                MessageDialogScreen.TryBuildActiveRandomEventMenuScreen,
                MessageDialogScreen.TryBuildActiveCustomMessageMenuScreen,
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

        public void Update()
        {
            if (_deferredAdventureLobbyDropdownClose == null || !_deferredAdventureLobbyDropdownHidden)
            {
                return;
            }

            if (UnityEngine.Time.realtimeSinceStartup >= _deferredAdventureLobbyDropdownDeadline)
            {
                CompleteDeferredAdventureLobbyDropdownClose("adventure lobby icon dropdown deferred timeout");
            }
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate owned entities ready while owned entities is already top");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate troop overview ready while troop overview is already top");
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

        public void OnAdventurePlayerMenuReady(AdventurePlayerMenu menu)
        {
            AdventurePlayerMenuAdapter adapter = new AdventurePlayerMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventurePlayerMenuScreen screen = new AdventurePlayerMenuScreen(adapter);
            AdventurePlayerMenuScreen current = _screenManager.CurrentScreen as AdventurePlayerMenuScreen;
            if (current != null && current.Matches(menu))
            {
                _screenManager.RefreshTop<AdventurePlayerMenuScreen>(screen, "adventure players menu shown");
                return;
            }

            Push(screen, "adventure players menu ready");
        }

        public void OnAdventurePlayerMenuChanged()
        {
            AdventurePlayerMenuScreen screen = _screenManager.CurrentScreen as AdventurePlayerMenuScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<AdventurePlayerMenuScreen>("adventure players menu no longer present");
                return;
            }

            screen.Refresh();
        }

        public void OnAdventurePlayerMenuClosed(AdventurePlayerMenu menu)
        {
            AdventurePlayerMenuScreen current = _screenManager.CurrentScreen as AdventurePlayerMenuScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<AdventurePlayerMenuScreen>("adventure players menu closed");
                return;
            }

            if (_screenManager.Contains<AdventurePlayerMenuScreen>())
            {
                _screenManager.Remove<AdventurePlayerMenuScreen>("adventure players menu closed");
            }
        }

        public void OnSendResourcePopupReady(SendResourcePopup popup)
        {
            SendResourcePopupAdapter adapter = new SendResourcePopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            SendResourcePopupScreen screen = new SendResourcePopupScreen(adapter);
            if (_screenManager.CurrentScreen is SendResourcePopupScreen)
            {
                _screenManager.RefreshTop<SendResourcePopupScreen>(screen, "send resource popup shown");
                return;
            }

            Push(screen, "send resource popup ready");
        }

        public void OnSendResourcePopupHidden()
        {
            // SendResourcePopup.Hide is not a reliable close signal by itself:
            // the game also calls it during injection-time initialization and can
            // call it redundantly when the popup is already inactive. Only pop
            // when the corresponding accessibility screen is currently on top.
            if (_screenManager.CurrentScreen is SendResourcePopupScreen)
            {
                _screenManager.Pop<SendResourcePopupScreen>("send resource popup hidden");
            }
        }

        public void OnGiftTownPopupReady(GiftTownPopup popup)
        {
            GiftTownPopupAdapter adapter = new GiftTownPopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            GiftTownPopupScreen screen = new GiftTownPopupScreen(adapter);
            if (_screenManager.CurrentScreen is GiftTownPopupScreen)
            {
                _screenManager.RefreshTop<GiftTownPopupScreen>(screen, "gift town popup shown");
                return;
            }

            Push(screen, "gift town popup ready");
        }

        public void OnGiftTownPopupHidden()
        {
            // GiftTownPopup.Hide is not a reliable close signal by itself:
            // the game also calls it during injection-time initialization and can
            // call it redundantly when the popup is already inactive. Only pop
            // when the corresponding accessibility screen is currently on top.
            if (_screenManager.CurrentScreen is GiftTownPopupScreen)
            {
                _screenManager.Pop<GiftTownPopupScreen>("gift town popup hidden");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate marketplace ready while marketplace is already top");
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

        public void OnArtifactMarketReady(ArtifactMarketMenu menu)
        {
            ArtifactMarketMenuAdapter adapter = new ArtifactMarketMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            ArtifactMarketScreen current = _screenManager.CurrentScreen as ArtifactMarketScreen;
            if (current != null)
            {
                current.Refresh();
                return;
            }

            Push(new ArtifactMarketScreen(adapter), "artifact market ready");
        }

        public void OnArtifactMarketClosed(ArtifactMarketMenu menu)
        {
            // ArtifactMarketMenu.Close is a possible-close signal, not proof
            // that the window was open: the game also calls it from Start() and
            // HideAll() while cleaning up inactive menus. Only pop when the
            // artifact market accessibility screen is currently at the top.
            if (_screenManager.CurrentScreen is ArtifactMarketScreen)
            {
                _screenManager.Pop<ArtifactMarketScreen>("artifact market closed");
            }
        }

        public void OnArtifactMarketChanged()
        {
            if (_artifactMarketRefreshPending)
            {
                return;
            }

            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin == null)
            {
                RefreshArtifactMarket("artifact market changed");
                return;
            }

            _artifactMarketRefreshPending = true;
            plugin.StartCoroutine(RefreshArtifactMarketNextFrame());
        }

        private IEnumerator RefreshArtifactMarketNextFrame()
        {
            yield return null;
            _artifactMarketRefreshPending = false;
            RefreshArtifactMarket("artifact market changed");
        }

        private void RefreshArtifactMarket(string reason)
        {
            ArtifactMarketScreen screen = _screenManager.CurrentScreen as ArtifactMarketScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<ArtifactMarketScreen>("artifact market no longer present");
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

        public void OnCommunityMapsChanged()
        {
            CommunityMapsHomeScreen home = _screenManager.Get<CommunityMapsHomeScreen>();
            CommunityMapsCollectionScreen collection = _screenManager.Get<CommunityMapsCollectionScreen>();
            CommunityMapsDetailsScreen details = _screenManager.Get<CommunityMapsDetailsScreen>();
            Screen newHome = CommunityMapsHomeScreen.TryBuildActiveScreen();
            Screen newCollection = CommunityMapsCollectionScreen.TryBuildActiveScreen();
            Screen newDetails = CommunityMapsDetailsScreen.TryBuildActiveScreen();

            if (newHome != null)
            {
                if (home == null)
                {
                    if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
                    {
                        PushBelowTop(newHome, "community maps home ready below modal");
                    }
                    else
                    {
                        Push(newHome, "community maps home ready");
                    }
                }
                else
                {
                    home.Refresh();
                }
            }
            else if (home != null)
            {
                _screenManager.Remove<CommunityMapsHomeScreen>("community maps home closed");
            }

            if (newCollection != null)
            {
                if (collection == null)
                {
                    if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
                    {
                        PushBelowTop(newCollection, "community maps collection ready below modal");
                    }
                    else
                    {
                        Push(newCollection, "community maps collection ready");
                    }
                }
                else
                {
                    collection.Refresh();
                }
            }
            else if (collection != null)
            {
                _screenManager.Remove<CommunityMapsCollectionScreen>("community maps collection closed");
            }

            if (newDetails != null)
            {
                if (details == null)
                {
                    if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
                    {
                        PushBelowTop(newDetails, "community maps details ready below modal");
                    }
                    else
                    {
                        Push(newDetails, "community maps details ready");
                    }
                }
                else
                {
                    details.Refresh();
                }
            }
            else if (details != null)
            {
                _screenManager.Remove<CommunityMapsDetailsScreen>("community maps details closed");
            }
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
                RefreshCommunityMapsHome("community maps home content changed");
                return;
            }

            _communityMapsHomeContentRefreshPending = true;
            plugin.StartCoroutine(RefreshCommunityMapsHomeContentNextFrame());
        }

        private IEnumerator RefreshCommunityMapsHomeContentNextFrame()
        {
            yield return null;
            _communityMapsHomeContentRefreshPending = false;
            RefreshCommunityMapsHome("community maps home content changed");
        }

        private void RefreshCommunityMapsHome(string reason)
        {
            CommunityMapsHomeScreen home = _screenManager.Get<CommunityMapsHomeScreen>();
            Screen newHome = CommunityMapsHomeScreen.TryBuildActiveScreen();

            if (newHome != null)
            {
                if (home == null)
                {
                    if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
                    {
                        PushBelowTop(newHome, reason + " below modal");
                    }
                    else
                    {
                        Push(newHome, reason);
                    }
                }
                else
                {
                    home.Refresh();
                }
            }
            else if (home != null)
            {
                _screenManager.Remove<CommunityMapsHomeScreen>("community maps home closed");
            }
        }

        public void OnCommunityMapsCollectionChanged()
        {
            CommunityMapsCollectionScreen collection = _screenManager.Get<CommunityMapsCollectionScreen>();
            if (collection != null && collection.IsPresent() && collection.IsSearchInputFocused())
            {
                collection.DeferRefreshUntilSearchInputUnfocused();
                return;
            }

            Screen newCollection = CommunityMapsCollectionScreen.TryBuildActiveScreen();

            if (newCollection != null)
            {
                if (collection == null)
                {
                    if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
                    {
                        PushBelowTop(newCollection, "community maps collection ready below modal");
                    }
                    else
                    {
                        Push(newCollection, "community maps collection ready");
                    }
                }
                else
                {
                    collection.Refresh();
                }
            }
            else if (collection != null)
            {
                _screenManager.Remove<CommunityMapsCollectionScreen>("community maps collection closed");
            }
        }

        public void OnCommunityMapsModalChanged()
        {
            CommunityMapsModalScreen modal = _screenManager.CurrentScreen as CommunityMapsModalScreen;
            Screen newModal = CommunityMapsModalScreen.TryBuildActiveScreen();

            if (newModal != null)
            {
                if (modal == null)
                {
                    Push(newModal, "community maps modal ready");
                }
                else
                {
                    CommunityMapsModalScreen newModalScreen = newModal as CommunityMapsModalScreen;
                    if (newModalScreen != null && modal.State != newModalScreen.State)
                    {
                        _screenManager.RefreshTop<CommunityMapsModalScreen>(newModal, "community maps modal changed");
                    }
                    else
                    {
                        modal.Refresh();
                    }
                }
            }
            else if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
            {
                _screenManager.Pop<CommunityMapsModalScreen>("community maps modal closed");
            }
        }

        public void OnCommunityMapsSearchFilterChanged()
        {
            CommunityMapsSearchFilterScreen searchFilter = _screenManager.CurrentScreen as CommunityMapsSearchFilterScreen;
            Screen newSearchFilter = CommunityMapsSearchFilterScreen.TryBuildActiveScreen();

            if (newSearchFilter != null)
            {
                if (searchFilter == null)
                {
                    Push(newSearchFilter, "community maps search filter ready");
                }
                else
                {
                    searchFilter.Refresh();
                }
            }
            else if (_screenManager.CurrentScreen is CommunityMapsSearchFilterScreen)
            {
                _screenManager.Pop<CommunityMapsSearchFilterScreen>("community maps search filter closed");
            }
            else if (_screenManager.Contains<CommunityMapsSearchFilterScreen>())
            {
                _screenManager.Remove<CommunityMapsSearchFilterScreen>("community maps search filter closed below current screen");
            }
        }

        public void OnCommunityMapsSearchFilterContentsChanged()
        {
            CommunityMapsSearchFilterScreen searchFilter = _screenManager.CurrentScreen as CommunityMapsSearchFilterScreen;
            if (searchFilter != null && searchFilter.IsPresent())
            {
                searchFilter.Refresh();
            }
        }

        public void OnCommunityMapsSearchResultsChanged()
        {
            CommunityMapsSearchResultsScreen searchResults = _screenManager.Get<CommunityMapsSearchResultsScreen>();
            CommunityMapsSearchResultsScreen newSearchResults =
                CommunityMapsSearchResultsScreen.TryBuildActiveScreen() as CommunityMapsSearchResultsScreen;

            if (newSearchResults != null)
            {
                if (_screenManager.Contains<CommunityMapsSearchFilterScreen>())
                {
                    _screenManager.Remove<CommunityMapsSearchFilterScreen>("community maps search results opened");
                }

                if (searchResults == null)
                {
                    Push(newSearchResults, "community maps search results ready");
                }
                else
                {
                    searchResults.Refresh(newSearchResults.Adapter);
                }
            }
            else if (searchResults != null)
            {
                _screenManager.Remove<CommunityMapsSearchResultsScreen>("community maps search results closed");
            }
        }

        public void OnCommunityMapsClosed()
        {
            if (_screenManager.CurrentScreen is CommunityMapsSearchFilterScreen)
            {
                _screenManager.Pop<CommunityMapsSearchFilterScreen>("community maps browser closed with search filter open");
            }

            if (_screenManager.CurrentScreen is CommunityMapsSearchResultsScreen)
            {
                _screenManager.Pop<CommunityMapsSearchResultsScreen>("community maps browser closed with search results open");
            }

            if (_screenManager.CurrentScreen is CommunityMapsModalScreen)
            {
                _screenManager.Pop<CommunityMapsModalScreen>("community maps browser closed with modal open");
            }

            if (_screenManager.Contains<CommunityMapsHomeScreen>())
            {
                _screenManager.Remove<CommunityMapsHomeScreen>("community maps browser closed");
            }

            if (_screenManager.Contains<CommunityMapsCollectionScreen>())
            {
                _screenManager.Remove<CommunityMapsCollectionScreen>("community maps browser closed");
            }

            if (_screenManager.Contains<CommunityMapsDetailsScreen>())
            {
                _screenManager.Remove<CommunityMapsDetailsScreen>("community maps browser closed");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector.OnPopupMenuReady received null settings");
                return;
            }

            object resolvedSourceKey = sourceKey ?? (settings != null ? (object)settings.ContainerTransform : null);
            PopStaleMainMenuFoldoutBeforePopup();
            MessageDialogScreen screen = new MessageDialogScreen(new PopupMenuAdapter(resolvedSourceKey, settings));
            if (IsCurrentMessageDialogSource(resolvedSourceKey))
            {
                _screenManager.RefreshTop<MessageDialogScreen>(screen, "popup menu refreshed");
                return;
            }

            Push(screen, "popup menu ready");
        }

        private void PopStaleMainMenuFoldoutBeforePopup()
        {
            FoldoutMenuScreen foldoutScreen = _screenManager.CurrentScreen as FoldoutMenuScreen;
            if (foldoutScreen == null || foldoutScreen.IsPresent())
            {
                return;
            }

            // MainMenu.HandleJoinWithCodeClicked hides the multiplayer foldout item
            // container directly instead of calling FoldoutUIButton.ForceClose(), so
            // our normal foldout close hook does not run. Pop the now-stale foldout
            // before stacking the popup above it.
            _screenManager.Pop<FoldoutMenuScreen>("main menu foldout hidden before popup");
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

        public void OnRandomEventMenuReady(RandomEventMenu menu)
        {
            RandomEventMenuAdapter adapter = new RandomEventMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new MessageDialogScreen(adapter), "random event menu ready");
        }

        public void OnRandomEventMenuClosed(RandomEventMenu menu)
        {
            if (menu != null && !IsCurrentMessageDialogSource(menu))
            {
                return;
            }

            _screenManager.Pop<MessageDialogScreen>("random event menu closed");
        }

        public void OnCustomMessageMenuReady(CustomMessageMenu menu)
        {
            CustomMessageMenuAdapter adapter = new CustomMessageMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new MessageDialogScreen(adapter), "custom message menu ready");
        }

        public void OnCustomMessageMenuClosed(CustomMessageMenu menu)
        {
            if (menu != null && !IsCurrentMessageDialogSource(menu))
            {
                return;
            }

            _screenManager.Pop<MessageDialogScreen>("custom message menu closed");
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

        public void OnOnlineGameListReady(GameListMenu menu)
        {
            OnlineGameListAdapter adapter = new OnlineGameListAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            OnlineGameListScreen screen = new OnlineGameListScreen(adapter);
            if (_screenManager.CurrentScreen is OnlineGameListScreen)
            {
                _screenManager.RefreshTop<OnlineGameListScreen>(screen, "online game list shown");
                return;
            }

            Push(screen, "online game list ready");
        }

        public void OnOnlineGameListChanged()
        {
            OnlineGameListScreen screen = _screenManager.Get<OnlineGameListScreen>();
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Remove<OnlineGameListScreen>("online game list no longer present");
                return;
            }

            screen.Refresh(announceFocus: false);
        }

        public void OnOnlineGameListChanged(GameListMenu menu)
        {
            OnlineGameListScreen current = _screenManager.Get<OnlineGameListScreen>();
            if (current != null && current.Matches(menu))
            {
                if (current.IsPresent())
                {
                    current.Refresh(announceFocus: false);
                    return;
                }

                _screenManager.Remove<OnlineGameListScreen>("online game list no longer present");
                return;
            }

            OnOnlineGameListReady(menu);
        }

        public void OnOnlineGameListClosed(GameListMenu menu)
        {
            if (_screenManager.Contains<OnlineGameListScreen>())
            {
                _screenManager.Remove<OnlineGameListScreen>("online game list closed");
            }
        }

        public void OnOnlineHostGameReady(GameListMenu menu)
        {
            OnlineHostGameAdapter adapter = new OnlineHostGameAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            OnlineHostGameScreen screen = new OnlineHostGameScreen(adapter);
            if (_screenManager.CurrentScreen is OnlineHostGameScreen)
            {
                _screenManager.RefreshTop<OnlineHostGameScreen>(screen, "online host game shown");
                return;
            }

            Push(screen, "online host game ready");
        }

        public void OnOnlineHostGameClosed(GameListMenu menu)
        {
            OnlineHostGameScreen current = _screenManager.CurrentScreen as OnlineHostGameScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<OnlineHostGameScreen>("online host game closed");
                return;
            }

            if (_screenManager.Contains<OnlineHostGameScreen>())
            {
                _screenManager.Remove<OnlineHostGameScreen>("online host game closed");
            }
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

        public void OnCustomCampaignSelectRepopulated(CustomCampaignSelectMenuBehavior behavior)
        {
            CustomCampaignSelectAdapter adapter = new CustomCampaignSelectAdapter(behavior);
            if (!adapter.IsPresent())
            {
                return;
            }

            CustomCampaignSelectScreen screen = new CustomCampaignSelectScreen(adapter);
            if (_screenManager.CurrentScreen is CustomCampaignSelectScreen)
            {
                _screenManager.RefreshTop<CustomCampaignSelectScreen>(screen, "custom campaign select repopulated");
                return;
            }

            Push(screen, "custom campaign select ready");
        }

        public void OnCustomCampaignSelectClosed(CustomCampaignSelectMenuBehavior behavior)
        {
            if (_screenManager.CurrentScreen is CustomCampaignSelectScreen)
            {
                _screenManager.Pop<CustomCampaignSelectScreen>("custom campaign select closed");
                return;
            }

            if (_screenManager.Contains<CustomCampaignSelectScreen>())
            {
                _screenManager.Remove<CustomCampaignSelectScreen>("custom campaign select closed");
            }
        }

        public void OnCustomCampaignEntryStatusChanged(CustomCampaignEntry entry)
        {
            CustomCampaignSelectScreen screen = _screenManager.CurrentScreen as CustomCampaignSelectScreen;
            screen?.AnnounceStatusChanged(entry);
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

        public void OnAdventureLobbyRandomLayoutReady(LobbyRandomMapSelectionMenu menu)
        {
            AdventureLobbyRandomLayoutAdapter adapter = new AdventureLobbyRandomLayoutAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyRandomLayoutScreen screen = new AdventureLobbyRandomLayoutScreen(adapter);
            AdventureLobbyRandomLayoutScreen current = _screenManager.CurrentScreen as AdventureLobbyRandomLayoutScreen;
            if (current != null && current.Matches(menu))
            {
                _screenManager.RefreshTop<AdventureLobbyRandomLayoutScreen>(screen, "adventure lobby random layout shown");
                return;
            }

            Push(screen, "adventure lobby random layout ready");
        }

        public void OnAdventureLobbyRandomLayoutSelectionChanged(LobbyRandomMapSelectionMenu menu)
        {
            AdventureLobbyRandomLayoutScreen current = _screenManager.CurrentScreen as AdventureLobbyRandomLayoutScreen;
            if (current != null && current.Matches(menu))
            {
                if (current.IsPresent())
                {
                    current.Refresh(announceFocus: false);
                    return;
                }

                _screenManager.Pop<AdventureLobbyRandomLayoutScreen>("adventure lobby random layout no longer present");
                return;
            }

            AdventureLobbyRandomLayoutAdapter adapter = new AdventureLobbyRandomLayoutAdapter(menu);
            if (adapter.IsPresent() && _screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                Push(new AdventureLobbyRandomLayoutScreen(adapter), "adventure lobby random layout selection changed ready");
            }
        }

        public void OnAdventureLobbyRandomLayoutEntryChanged(LobbyRandomMapPreviewEntry entry)
        {
            AdventureLobbyRandomLayoutScreen current = _screenManager.CurrentScreen as AdventureLobbyRandomLayoutScreen;
            if (current == null)
            {
                return;
            }

            if (!current.IsPresent())
            {
                _screenManager.Pop<AdventureLobbyRandomLayoutScreen>("adventure lobby random layout no longer present");
                return;
            }

            current.Refresh(announceFocus: false);
        }

        public void OnAdventureLobbyRandomLayoutClosed(LobbyRandomMapSelectionMenu menu)
        {
            AdventureLobbyRandomLayoutScreen current = _screenManager.CurrentScreen as AdventureLobbyRandomLayoutScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<AdventureLobbyRandomLayoutScreen>("adventure lobby random layout closed");
                return;
            }

            if (_screenManager.Contains<AdventureLobbyRandomLayoutScreen>())
            {
                _screenManager.Remove<AdventureLobbyRandomLayoutScreen>("adventure lobby random layout closed");
            }
        }

        public void OnAdventureLobbyMapSelectReady(MapSelectMenu menu)
        {
            AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyMapSelectScreen screen = new AdventureLobbyMapSelectScreen(adapter);
            if (_screenManager.CurrentScreen is AdventureLobbyMapSelectScreen)
            {
                _screenManager.RefreshTop<AdventureLobbyMapSelectScreen>(screen, "adventure lobby map select shown");
                return;
            }

            Push(screen, "adventure lobby map select ready");
        }

        public void OnAdventureLobbyMapSelectChanged(MapSelectMenu menu)
        {
            AdventureLobbyMapSelectScreen current = _screenManager.CurrentScreen as AdventureLobbyMapSelectScreen;
            if (current != null && current.Matches(menu))
            {
                if (current.IsPresent())
                {
                    current.Refresh();
                    return;
                }

                _screenManager.Pop<AdventureLobbyMapSelectScreen>("adventure lobby map select no longer present");
                return;
            }

            AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
            if (adapter.IsPresent() && _screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                Push(new AdventureLobbyMapSelectScreen(adapter), "adventure lobby map select changed ready");
            }
        }

        public void OnAdventureLobbyMapSelectSelectionChanged(MapSelectMenu menu)
        {
            AdventureLobbyMapSelectScreen current = _screenManager.CurrentScreen as AdventureLobbyMapSelectScreen;
            if (current != null && current.Matches(menu))
            {
                if (current.IsPresent())
                {
                    current.Refresh(announceFocus: false);
                    return;
                }

                _screenManager.Pop<AdventureLobbyMapSelectScreen>("adventure lobby map select no longer present");
                return;
            }

            AdventureLobbyMapSelectAdapter adapter = new AdventureLobbyMapSelectAdapter(menu);
            if (adapter.IsPresent() && _screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                Push(new AdventureLobbyMapSelectScreen(adapter), "adventure lobby map select selection changed ready");
            }
        }

        public void OnAdventureLobbyMapSelectClosed(MapSelectMenu menu)
        {
            if (_screenManager.Contains<AdventureLobbyMapSelectScreen>())
            {
                _screenManager.Remove<AdventureLobbyMapSelectScreen>("adventure lobby map select closed");
            }
        }

        public void OnAdventureLobbyChallengeMapSelectReady(ChallengeMapsMenu menu)
        {
            AdventureLobbyChallengeMapSelectAdapter adapter = new AdventureLobbyChallengeMapSelectAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyChallengeMapSelectScreen screen = new AdventureLobbyChallengeMapSelectScreen(adapter);
            if (_screenManager.CurrentScreen is AdventureLobbyChallengeMapSelectScreen)
            {
                _screenManager.RefreshTop<AdventureLobbyChallengeMapSelectScreen>(screen, "adventure lobby challenge map select shown");
                return;
            }

            Push(screen, "adventure lobby challenge map select ready");
        }

        public void OnAdventureLobbyChallengeMapSelectSelectionChanged(ChallengeMapsMenu menu)
        {
            AdventureLobbyChallengeMapSelectScreen current = _screenManager.CurrentScreen as AdventureLobbyChallengeMapSelectScreen;
            if (current != null && current.Matches(menu))
            {
                if (current.IsPresent())
                {
                    current.Refresh(announceFocus: false);
                    return;
                }

                _screenManager.Pop<AdventureLobbyChallengeMapSelectScreen>("adventure lobby challenge map select no longer present");
                return;
            }

            AdventureLobbyChallengeMapSelectAdapter adapter = new AdventureLobbyChallengeMapSelectAdapter(menu);
            if (adapter.IsPresent() && _screenManager.CurrentScreen is AdventureLobbyMapTypeScreen)
            {
                Push(new AdventureLobbyChallengeMapSelectScreen(adapter), "adventure lobby challenge map select selection changed ready");
            }
        }

        public void OnAdventureLobbyChallengeMapSelectClosed(ChallengeMapsMenu menu)
        {
            if (_screenManager.Contains<AdventureLobbyChallengeMapSelectScreen>())
            {
                _screenManager.Remove<AdventureLobbyChallengeMapSelectScreen>("adventure lobby challenge map select closed");
            }
        }

        public void OnAdventureLobbyPlayersReady(LobbyMenu menu)
        {
            AdventureLobbyPlayersAdapter adapter = new AdventureLobbyPlayersAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyPlayersScreen screen = new AdventureLobbyPlayersScreen(adapter);
            if (_screenManager.CurrentScreen is AdventureLobbyPlayersScreen)
            {
                _screenManager.RefreshTop<AdventureLobbyPlayersScreen>(screen, "adventure lobby players shown");
                return;
            }

            Push(screen, "adventure lobby players ready");
        }

        public void OnAdventureLobbyPlayersChanged()
        {
            AdventureLobbyPlayersScreen screen = _screenManager.Get<AdventureLobbyPlayersScreen>();
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Remove<AdventureLobbyPlayersScreen>("adventure lobby players no longer present");
                return;
            }

            screen.Refresh();
            CompleteDeferredAdventureLobbyDropdownClose();
        }

        public void OnAdventureLobbyInviteProvidersReady(LobbyMultiplayerPanel panel)
        {
            AdventureLobbyInviteProvidersAdapter adapter = new AdventureLobbyInviteProvidersAdapter(panel);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyInviteProvidersScreen screen = new AdventureLobbyInviteProvidersScreen(adapter);
            AdventureLobbyInviteProvidersScreen current = _screenManager.CurrentScreen as AdventureLobbyInviteProvidersScreen;
            if (current != null && current.Matches(panel))
            {
                _screenManager.RefreshTop<AdventureLobbyInviteProvidersScreen>(screen, "adventure lobby invite providers shown");
                return;
            }

            Push(screen, "adventure lobby invite providers ready");
        }

        public void OnAdventureLobbyInviteProvidersClosed(LobbyMultiplayerPanel panel)
        {
            AdventureLobbyInviteProvidersScreen current = _screenManager.CurrentScreen as AdventureLobbyInviteProvidersScreen;
            if (current != null && (panel == null || current.Matches(panel)))
            {
                _screenManager.Pop<AdventureLobbyInviteProvidersScreen>("adventure lobby invite providers closed");
                return;
            }

            if (_screenManager.Contains<AdventureLobbyInviteProvidersScreen>())
            {
                _screenManager.Remove<AdventureLobbyInviteProvidersScreen>("adventure lobby invite providers closed");
            }
        }

        public void OnAdventureLobbyPlayersClosed(LobbyMenu menu)
        {
            if (_screenManager.Contains<AdventureLobbyInviteProvidersScreen>())
            {
                _screenManager.Remove<AdventureLobbyInviteProvidersScreen>("adventure lobby players closed");
            }

            if (_screenManager.Contains<AdventureLobbyGameSettingsScreen>())
            {
                _screenManager.Remove<AdventureLobbyGameSettingsScreen>("adventure lobby players closed");
            }

            if (_screenManager.Contains<AdventureLobbyPlayerSettingsScreen>())
            {
                _screenManager.Remove<AdventureLobbyPlayerSettingsScreen>("adventure lobby players closed");
            }

            if (_screenManager.Contains<AdventureLobbyIconDropdownScreen>())
            {
                _screenManager.Remove<AdventureLobbyIconDropdownScreen>("adventure lobby players closed");
            }

            ClearDeferredAdventureLobbyDropdownClose();

            if (_screenManager.Contains<AdventureLobbyPlayersScreen>())
            {
                _screenManager.Remove<AdventureLobbyPlayersScreen>("adventure lobby players closed");
            }
        }

        public void OnAdventureLobbyGameSettingsReady(LobbyMapSettingsMenu menu)
        {
            AdventureLobbyGameSettingsAdapter adapter = new AdventureLobbyGameSettingsAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyGameSettingsScreen screen = new AdventureLobbyGameSettingsScreen(adapter);
            AdventureLobbyGameSettingsScreen current = _screenManager.CurrentScreen as AdventureLobbyGameSettingsScreen;
            if (current != null && current.Matches(menu))
            {
                _screenManager.RefreshTop<AdventureLobbyGameSettingsScreen>(screen, "adventure lobby game settings shown");
                return;
            }

            Push(screen, "adventure lobby game settings ready");
        }

        public void OnAdventureLobbyGameSettingsChanged(LobbyMapSettingsMenu menu)
        {
            AdventureLobbyGameSettingsScreen current = _screenManager.CurrentScreen as AdventureLobbyGameSettingsScreen;
            if (current == null || !current.Matches(menu))
            {
                return;
            }

            if (!current.IsPresent())
            {
                _screenManager.Pop<AdventureLobbyGameSettingsScreen>("adventure lobby game settings no longer present");
                return;
            }

            current.Refresh();
        }

        public void OnAdventureLobbyGameSettingsClosed(LobbyMapSettingsMenu menu)
        {
            AdventureLobbyGameSettingsScreen current = _screenManager.CurrentScreen as AdventureLobbyGameSettingsScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<AdventureLobbyGameSettingsScreen>("adventure lobby game settings closed");
                return;
            }

            if (_screenManager.Contains<AdventureLobbyGameSettingsScreen>())
            {
                _screenManager.Remove<AdventureLobbyGameSettingsScreen>("adventure lobby game settings closed");
            }
        }

        public void OnAdventureLobbyPlayerSettingsReady(LobbyPlayerSettingsMenu menu)
        {
            AdventureLobbyPlayerSettingsAdapter adapter = new AdventureLobbyPlayerSettingsAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyPlayerSettingsScreen screen = new AdventureLobbyPlayerSettingsScreen(adapter);
            AdventureLobbyPlayerSettingsScreen current = _screenManager.CurrentScreen as AdventureLobbyPlayerSettingsScreen;
            if (current != null && current.Matches(menu))
            {
                _screenManager.RefreshTop<AdventureLobbyPlayerSettingsScreen>(screen, "adventure lobby player settings shown");
                return;
            }

            Push(screen, "adventure lobby player settings ready");
        }

        public void OnAdventureLobbyPlayerSettingsClosed(LobbyPlayerSettingsMenu menu)
        {
            AdventureLobbyPlayerSettingsScreen current = _screenManager.CurrentScreen as AdventureLobbyPlayerSettingsScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<AdventureLobbyPlayerSettingsScreen>("adventure lobby player settings closed");
                return;
            }

            if (_screenManager.Contains<AdventureLobbyPlayerSettingsScreen>())
            {
                _screenManager.Remove<AdventureLobbyPlayerSettingsScreen>("adventure lobby player settings closed");
            }
        }

        public void OnAdventureLobbyIconDropdownReady(IconDropdown dropdown)
        {
            AdventureLobbyIconDropdownAdapter adapter = new AdventureLobbyIconDropdownAdapter(dropdown);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureLobbyIconDropdownScreen screen = new AdventureLobbyIconDropdownScreen(adapter);
            if (_screenManager.CurrentScreen is AdventureLobbyIconDropdownScreen)
            {
                _screenManager.RefreshTop<AdventureLobbyIconDropdownScreen>(screen, "adventure lobby icon dropdown shown");
                return;
            }

            Push(screen, "adventure lobby icon dropdown ready");
        }

        public void OnAdventureLobbyIconDropdownClosed(IconDropdown dropdown)
        {
            if (_deferredAdventureLobbyDropdownClose != null
                && (dropdown == null || ReferenceEquals(_deferredAdventureLobbyDropdownClose, dropdown)))
            {
                _deferredAdventureLobbyDropdownHidden = true;
                _deferredAdventureLobbyDropdownDeadline = UnityEngine.Time.realtimeSinceStartup + 1f;
                return;
            }

            AdventureLobbyIconDropdownScreen current = _screenManager.CurrentScreen as AdventureLobbyIconDropdownScreen;
            if (current != null && (dropdown == null || current.Matches(dropdown)))
            {
                _screenManager.Pop<AdventureLobbyIconDropdownScreen>("adventure lobby icon dropdown closed");
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
            CompleteDeferredAdventureLobbyDropdownClose("adventure lobby icon dropdown changed");
        }

        private void CompleteDeferredAdventureLobbyDropdownClose(string reason)
        {
            if (_deferredAdventureLobbyDropdownClose == null)
            {
                return;
            }

            AdventureLobbyIconDropdownScreen current = _screenManager.CurrentScreen as AdventureLobbyIconDropdownScreen;
            if (current != null && current.Matches(_deferredAdventureLobbyDropdownClose))
            {
                _screenManager.Pop<AdventureLobbyIconDropdownScreen>(reason);
            }

            ClearDeferredAdventureLobbyDropdownClose();
        }

        private void ClearDeferredAdventureLobbyDropdownClose()
        {
            _deferredAdventureLobbyDropdownClose = null;
            _deferredAdventureLobbyDropdownHidden = false;
            _deferredAdventureLobbyDropdownDeadline = 0f;
        }

        public void OnPlatformUserMenuReady(PlatformUserMenu menu)
        {
            PlatformUserMenuAdapter adapter = new PlatformUserMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            PlatformUserMenuScreen screen = new PlatformUserMenuScreen(adapter);
            if (_screenManager.CurrentScreen is PlatformUserMenuScreen)
            {
                _screenManager.RefreshTop<PlatformUserMenuScreen>(screen, "platform user menu shown");
                return;
            }

            Push(screen, "platform user menu ready");
        }

        public void OnPlatformUserMenuClosed(PlatformUserMenu menu)
        {
            PlatformUserMenuScreen current = _screenManager.CurrentScreen as PlatformUserMenuScreen;
            if (current != null && (menu == null || current.Matches(menu)))
            {
                _screenManager.Pop<PlatformUserMenuScreen>("platform user menu closed");
                return;
            }

            if (_screenManager.Contains<PlatformUserMenuScreen>())
            {
                _screenManager.Remove<PlatformUserMenuScreen>("platform user menu closed");
            }
        }

        public void OnMainMenuSceneLoaded(MainMenuSceneType loadedScene)
        {
            CustomCampaignSelectScreen customCampaignSelect = _screenManager.Get<CustomCampaignSelectScreen>();
            if (loadedScene == MainMenuSceneType.MainMenu)
            {
                SocAccessMod.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.AdventureMapNotifications);
                SocAccessMod.Instance?.AdventureMapScannerState?.Clear();
            }

            if (loadedScene != MainMenuSceneType.Campaign && _screenManager.CurrentScreen is CampaignMenuScreen)
            {
                _screenManager.Pop<CampaignMenuScreen>("main menu scene changed away from campaign");
            }

            if (loadedScene != MainMenuSceneType.CustomCampaign
                && customCampaignSelect != null
                && !customCampaignSelect.IsPresent())
            {
                _screenManager.Remove<CustomCampaignSelectScreen>("main menu scene changed away from custom campaign");
            }

            if (loadedScene != MainMenuSceneType.OnlineGameList && _screenManager.Contains<OnlineHostGameScreen>())
            {
                _screenManager.Remove<OnlineHostGameScreen>("main menu scene changed away from online game list");
            }

            if (loadedScene != MainMenuSceneType.OnlineGameList && _screenManager.Contains<OnlineGameListScreen>())
            {
                _screenManager.Remove<OnlineGameListScreen>("main menu scene changed away from online game list");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyMapTypeScreen>())
            {
                _screenManager.Remove<AdventureLobbyMapTypeScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyRandomLayoutScreen>())
            {
                _screenManager.Remove<AdventureLobbyRandomLayoutScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyMapSelectScreen>())
            {
                _screenManager.Remove<AdventureLobbyMapSelectScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyChallengeMapSelectScreen>())
            {
                _screenManager.Remove<AdventureLobbyChallengeMapSelectScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyIconDropdownScreen>())
            {
                _screenManager.Remove<AdventureLobbyIconDropdownScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyInviteProvidersScreen>())
            {
                _screenManager.Remove<AdventureLobbyInviteProvidersScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyGameSettingsScreen>())
            {
                _screenManager.Remove<AdventureLobbyGameSettingsScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyPlayerSettingsScreen>())
            {
                _screenManager.Remove<AdventureLobbyPlayerSettingsScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<PlatformUserMenuScreen>())
            {
                _screenManager.Remove<PlatformUserMenuScreen>("main menu scene changed away from adventure lobby");
            }

            if (loadedScene != MainMenuSceneType.AdventureLobby && _screenManager.Contains<AdventureLobbyPlayersScreen>())
            {
                _screenManager.Remove<AdventureLobbyPlayersScreen>("main menu scene changed away from adventure lobby");
            }
        }

        public void OnAdventureViewReady(AdventureViewInstaller installer)
        {
            _adventureViewInstaller = installer;
        }

        public void OnAdventureMapReady()
        {
            Push(BuildAdventureMapScreen("adventure map ready"), "adventure map ready");
        }

        private void EnsureAdventureMapBaseScreen(string reason)
        {
            if (_screenManager.Contains<AdventureMapScreen>())
            {
                return;
            }

            PushBottom(BuildAdventureMapScreen(reason), reason + " adventure map base");
        }

        private AdventureMapScreen BuildAdventureMapScreen(string reason)
        {
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
            }

            AdventureMapEventListener eventListener = readinessDiagnostic == null
                ? new AdventureMapEventListener(
                    adapter.Facade,
                    adapter.SelectionHandler,
                    adapter.HumanAdventureControllerFacade,
                    adapter.LocalizationHandler,
                    adapter.FogManager,
                    revealedRegistry)
                : null;
            return new AdventureMapScreen(adapter, eventListener);
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector.OnCombatReady ignored because the battle command facade did not match the stored battle scene");
                return false;
            }

            CombatEventNarrator.SetActiveAdapter(adapter);
            SocAccessMod.Instance?.ReviewBuffers?.Clear(ReviewBufferKind.CombatEvents);
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate spellbook ready while spellbook is already top");
                return;
            }

            Push(screen, "spellbook ready");
        }

        public void OnSpellbookClosed(SpellBook spellbook)
        {
            _screenManager.Pop<SpellbookScreen>("spellbook closed");
        }

        public void OnCombatEnded()
        {
            _battleSceneInstaller = null;
            if (_screenManager.Contains<CombatScreen>())
            {
                _screenManager.Remove<CombatScreen>("combat ended");
                SocAccessMod.Instance?.LogInfo("ScreenDetector removed CombatScreen when combat ended");
            }

            CombatEventNarrator.FlushPendingEventsForCombatEnd();
            CombatEventNarrator.Reset();
        }

        public void OnLoadingScreenOpening(LoadingScreenMenu menu)
        {
            NativeTooltipUtility.HideTooltip();
            _screenManager.Clear();
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

            if (_screenManager.Contains<PostAdventureResultScreen>() || _screenManager.Contains<PostAdventureStatsScreen>())
            {
                return;
            }

            // Returning from manual combat can report SceneLoaderState.None before
            // every adventure dependency is ready, causing the normal map creation
            // hook to reject the screen. Once post-battle closes, the native battle
            // menu has completed and any follow-up overlays, such as claim or story
            // menus, should sit above the adventure map base screen.
            EnsureAdventureMapBaseScreen("post battle result closed");
        }

        public void OnPostAdventureResultReady(PostAdventureMenu menu)
        {
            PostAdventureResultScreen screen = new PostAdventureResultScreen(new PostAdventureResultAdapter(menu));
            if (_screenManager.CurrentScreen is PostAdventureResultScreen)
            {
                _screenManager.RefreshTop<PostAdventureResultScreen>(screen, "post adventure result ready");
                return;
            }

            // The post-adventure result is a root screen for the ended game.
            // Clearing avoids briefly returning focus to the adventure map while
            // transitioning away from victory/defeat.
            if (!screen.IsPresent())
            {
                return;
            }

            _screenManager.Clear();
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

        public void OnPostAdventureStatsChanged(bool announceFocus = false)
        {
            PostAdventureStatsScreen screen = _screenManager.CurrentScreen as PostAdventureStatsScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<PostAdventureStatsScreen>("post adventure stats no longer present");
                return;
            }

            screen.Refresh(announceFocus);
        }

        public void OnPlayerStatsReady(PlayerStatsMenuNavigation menu)
        {
            PlayerStatsAdapter adapter = new PlayerStatsAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            PlayerStatsScreen screen = new PlayerStatsScreen(adapter);
            if (_screenManager.CurrentScreen is PlayerStatsScreen)
            {
                _screenManager.RefreshTop<PlayerStatsScreen>(screen, "player stats ready");
                return;
            }

            Push(screen, "player stats ready");
        }

        public void OnPlayerStatsChanged()
        {
            PlayerStatsScreen screen = _screenManager.CurrentScreen as PlayerStatsScreen;
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.Pop<PlayerStatsScreen>("player stats no longer present");
                return;
            }

            screen.Refresh();
        }

        public void OnPlayerStatsClosed(PlayerStatsMenuNavigation menu)
        {
            if (_screenManager.CurrentScreen is PlayerStatsScreen)
            {
                _screenManager.Pop<PlayerStatsScreen>("player stats closed");
                return;
            }

            if (_screenManager.Contains<PlayerStatsScreen>())
            {
                _screenManager.Remove<PlayerStatsScreen>("player stats closed");
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

        public void OnTeleportMenuReady(TeleportMenu menu)
        {
            TeleportMenuAdapter adapter = new TeleportMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                return;
            }

            AdventureMapScreen screen = _screenManager.Get<AdventureMapScreen>();
            if (screen == null)
            {
                return;
            }

            if (!ReferenceEquals(_screenManager.CurrentScreen, screen))
            {
                if (_screenManager.CurrentScreen is MapEntityMiniMenuScreen)
                {
                    _screenManager.Pop<MapEntityMiniMenuScreen>("teleport menu opened");
                }
            }

            screen.EnterTeleportDestinationMode(adapter);
        }

        public void OnTeleportMenuClosed(TeleportMenu menu)
        {
            AdventureMapScreen screen = _screenManager.Get<AdventureMapScreen>();
            if (screen != null && screen.MatchesTeleportMenu(menu))
            {
                screen.ExitTeleportDestinationMode(menu);
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
                    SocAccessMod.Instance?.LogWarning("ScreenDetector ignored dwelling back to top while draft is already top");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate settlement ready while settlement is already top");
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
                    SocAccessMod.Instance?.LogWarning("ScreenDetector ignored settlement back to top while settlement is already top");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate defence menu ready while defence menu is already top");
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
                    SocAccessMod.Instance?.LogWarning("ScreenDetector ignored defence back to top while defence menu is already top");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate build menu ready while build menu is already top");
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored duplicate research menu ready while research menu is already top");
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

        /// <summary>The runtime factories that would answer present right now, by screen type
        /// name, in factory order - what a resync would push. For the dev server's probes.</summary>
        public List<string> PresentRuntimeScreens()
        {
            List<string> names = new List<string>();
            for (int i = 0; i < _runtimeScreenFactories.Count; i++)
            {
                Screen screen = _runtimeScreenFactories[i]();
                if (screen != null && screen.IsPresent())
                {
                    names.Add(screen.GetType().Name);
                }
            }

            return names;
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SocAccessMod.Instance?.LogWarning(
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
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SocAccessMod.Instance?.LogWarning(
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

        private bool PushBottom(Screen screen, string reason)
        {
            if (screen == null)
            {
                SocAccessMod.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SocAccessMod.Instance?.LogWarning(
                    "ScreenDetector ignored "
                    + reason
                    + " because "
                    + screen.GetType().Name
                    + " is not present");
                return false;
            }

            _screenManager.PushBottom(screen, reason);
            return true;
        }

        private static AdventureMapRevealedRegistry GetAdventureMapRevealedRegistry()
        {
            AdventureMapScannerState scannerState = SocAccessMod.Instance?.AdventureMapScannerState;
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
            SocAccessMod.Instance?.LogWarning(
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
