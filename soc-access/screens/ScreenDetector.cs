using System.Collections;
using _8_UILayer.ClientView.Menu.Paus;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Facade;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
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
    ///
    /// SHRINKING: a screen that resolves its own menu (AGENTS.md, "Screen Resolution") has no handler
    /// here at all. What is left are the screens that have not moved yet and the events that are not
    /// readiness - the lobby's dropdown, the community maps refreshes.
    /// </summary>
    public sealed class ScreenDetector
    {
        private readonly ScreenManager _screens;
        private BattleSceneInstaller _battleSceneInstaller;
        private IconDropdown _deferredAdventureLobbyDropdownClose;
        private bool _deferredAdventureLobbyDropdownHidden;
        private float _deferredAdventureLobbyDropdownDeadline;
        private bool _communityMapsHomeContentRefreshPending;

        public ScreenDetector(ScreenManager screens)
        {
            _screens = screens;
        }

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

        // ---- the player stats page ----

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
                Reg<AdventureLobbyPlayersScreen>()?.Forget();
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
            CampaignMapSelectScreen.Recover();
            CombatScreen.Recover();
            PlayerStatsScreen.Recover();
            LoadingCompleteScreen.Recover();
        }

    }
}
