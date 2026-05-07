using System.Collections.Generic;
using _8_UILayer.ClientView.Menu.Paus;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Facade;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ScreenDetector
    {
        private readonly ScreenManager _screenManager;
        private readonly List<IRuntimeScreenProbe> _runtimeScreenProbes;
        private AdventureViewInstaller _adventureViewInstaller;
        private BattleSceneInstaller _battleSceneInstaller;

        public ScreenDetector(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _runtimeScreenProbes = new List<IRuntimeScreenProbe>
            {
                new MainMenuRuntimeScreenProbe(),
                new CampaignMenuRuntimeScreenProbe(),
                new TaleSelectRuntimeScreenProbe(),
                new CampaignMapSelectRuntimeScreenProbe(),
                new AdventureMapRuntimeScreenProbe(),
                new CombatRuntimeScreenProbe(),
                new SpellbookRuntimeScreenProbe(),
                new PostBattleResultRuntimeScreenProbe(),
                new PreBattleMenuRuntimeScreenProbe(),
                new DwellingInteractionRuntimeScreenProbe(),
                new HostileJoinMenuRuntimeScreenProbe(),
                new MoveTroopPopupRuntimeScreenProbe(),
                new WorldChoiceMenuRuntimeScreenProbe(),
                new LevelUpRuntimeScreenProbe(),
                new CommanderSheetRuntimeScreenProbe(),
                new LetterboxStoryTextRuntimeScreenProbe(),
                new StoryTextRuntimeScreenProbe(),
                new DialogueMenuRuntimeScreenProbe(),
                new PauseMenuRuntimeScreenProbe(),
                new QuestionDialogRuntimeScreenProbe(),
                new ConfirmPopupRuntimeScreenProbe(),
                new SystemPopupRuntimeScreenProbe(),
                new QuitToDesktopPopupRuntimeScreenProbe(),
                new TutorialRuntimeScreenProbe()
            };
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

        public void OnPauseMenuClosed(PauseMenu pauseMenu)
        {
            _screenManager.Pop<PauseMenuScreen>("pause menu closed");
        }

        public void OnConfirmPopupReady(ConfirmPopup popup)
        {
            ConfirmPopupAdapter adapter = new ConfirmPopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new QuestionDialogScreen(adapter), "confirm popup ready");
        }

        public void OnConfirmPopupClosed(ConfirmPopup popup)
        {
            if (!IsCurrentQuestionDialogSource(popup))
            {
                return;
            }

            _screenManager.Pop<QuestionDialogScreen>("confirm popup closed");
        }

        public void OnSystemPopupReady(SystemPopup popup)
        {
            SystemPopupAdapter adapter = new SystemPopupAdapter(popup);
            if (!adapter.IsPresent())
            {
                return;
            }

            Push(new QuestionDialogScreen(adapter), "system popup ready");
        }

        public void OnSystemPopupClosed(SystemPopup popup)
        {
            if (!IsCurrentQuestionDialogSource(popup))
            {
                return;
            }

            _screenManager.Pop<QuestionDialogScreen>("system popup closed");
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

        public void OnQuestionDialogReady(object sourceKey, PopupMenu.Settings settings)
        {
            if (settings == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector.OnQuestionDialogReady received null settings");
                return;
            }

            object resolvedSourceKey = sourceKey ?? (settings != null ? (object)settings.ContainerTransform : null);
            Push(new QuestionDialogScreen(new QuestionDialogAdapter(resolvedSourceKey, settings)), "question dialog ready");
        }

        public void OnQuestionDialogClosed(object sourceKey)
        {
            if (sourceKey != null && !IsCurrentQuestionDialogSource(sourceKey))
            {
                return;
            }

            _screenManager.Pop<QuestionDialogScreen>("question dialog closed");
        }

        public void OnLetterboxStoryTextReady(LetterboxStoryText storyText)
        {
            Push(new StoryTextScreen(new LetterboxStoryTextAdapter(storyText)), "letterbox story text ready");
        }

        public void OnLetterboxStoryTextChanged(LetterboxStoryText storyText)
        {
            _screenManager.RefreshTop<StoryTextScreen>(
                new StoryTextScreen(new LetterboxStoryTextAdapter(storyText)),
                "letterbox story text changed");
        }

        public void OnLetterboxStoryTextClosed(LetterboxStoryText storyText)
        {
            StoryMapSuppression.Clear(storyText);
            _screenManager.Pop<StoryTextScreen>("letterbox story text closed");
        }

        public void OnStoryTextReady(StoryText storyText)
        {
            Push(new StoryTextScreen(new StoryTextAdapter(storyText)), "story text ready");
        }

        public void OnStoryTextChanged(StoryText storyText)
        {
            _screenManager.RefreshTop<StoryTextScreen>(
                new StoryTextScreen(new StoryTextAdapter(storyText)),
                "story text changed");
        }

        public void OnStoryTextClosed(StoryText storyText)
        {
            StoryMapSuppression.Clear(storyText);
            _screenManager.Pop<StoryTextScreen>("story text closed");
        }

        public void OnDialogueMenuReady(DialogueMenu dialogueMenu)
        {
            DialogueMenuAdvanceGuard.ClearPending(dialogueMenu);
            Push(new StoryTextScreen(new DialogueMenuAdapter(dialogueMenu)), "dialogue menu ready");
        }

        public void OnDialogueMenuChanged(DialogueMenu dialogueMenu)
        {
            DialogueMenuAdvanceGuard.ClearPending(dialogueMenu);
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
            StoryMapSuppression.Clear(dialogueMenu);
            _screenManager.Pop<StoryTextScreen>("dialogue menu closed");
        }

        public void OnMainMenuReady(MainMenu mainMenu)
        {
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

        public void OnMainMenuSceneLoaded(MainMenuSceneType loadedScene)
        {
            if (loadedScene != MainMenuSceneType.Campaign && _screenManager.CurrentScreen is CampaignMenuScreen)
            {
                _screenManager.Pop<CampaignMenuScreen>("main menu scene changed away from campaign");
            }
        }

        public void OnAdventureViewReady(AdventureViewInstaller installer)
        {
            _adventureViewInstaller = installer;
        }

        public void OnAdventureMapReady()
        {
            AdventureMapAdapter adapter = new AdventureMapAdapter(_adventureViewInstaller);
            AdventureMapEventListener eventListener = adapter.IsPresent()
                ? new AdventureMapEventListener(
                    adapter.Facade,
                    adapter.SelectionHandler,
                    adapter.HumanAdventureControllerFacade,
                    adapter.LocalizationHandler)
                : null;
            Push(new AdventureMapScreen(adapter, eventListener), "adventure map ready");
        }

        public void OnAdventureMapClosed()
        {
            _adventureViewInstaller = null;
            _screenManager.Pop<AdventureMapScreen>("adventure map closed");
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
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector.OnCombatReady ignored because the battle command facade did not match the stored battle scene");
                return false;
            }

            CombatEventNarrator.SetActiveAdapter(adapter);
            CombatScreen screen = new CombatScreen(adapter);
            if (IsTutorialTopScreen())
            {
                return PushBelowTop(screen, "combat ready");
            }

            return Push(screen, "combat ready");
        }

        public void OnCombatClosed()
        {
            _battleSceneInstaller = null;
            _screenManager.Pop<CombatScreen>("combat closed");
            CombatEventNarrator.Reset();
        }

        public void OnSpellbookReady(SpellBook spellbook)
        {
            SpellbookScreen screen = new SpellbookScreen(new SpellbookAdapter(spellbook));
            if (_screenManager.CurrentScreen is SpellbookScreen)
            {
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector ignored duplicate spellbook ready while spellbook is already top");
                return;
            }

            Push(screen, "spellbook ready");
        }

        public void OnSpellbookClosed(SpellBook spellbook)
        {
            _screenManager.Pop<SpellbookScreen>("spellbook closed");
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
            _screenManager.Pop<PostBattleResultScreen>("post battle result closed");
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

        public void OnWorldChoiceMenuReady(WorldChoiceMenu menu)
        {
            Push(new WorldChoiceMenuScreen(new WorldChoiceMenuAdapter(menu)), "world choice menu ready");
        }

        public void OnWorldChoiceMenuClosed(WorldChoiceMenu menu)
        {
            _screenManager.Pop<WorldChoiceMenuScreen>("world choice menu closed");
        }

        public void OnDwellingInteractionReady(DwellingInteractionMenu menu)
        {
            DwellingInteractionScreen screen = new DwellingInteractionScreen(new DwellingInteractionMenuAdapter(menu));
            if (_screenManager.CurrentScreen is DwellingInteractionScreen)
            {
                _screenManager.RefreshTop<DwellingInteractionScreen>(screen, "dwelling interaction changed");
                return;
            }

            Push(screen, "dwelling interaction ready");
        }

        public void OnDwellingInteractionClosed(DwellingInteractionMenu menu)
        {
            _screenManager.Pop<DwellingInteractionScreen>("dwelling interaction closed");
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
            }

            Push(new HostileJoinMenuScreen(adapter), "hostile join menu ready");
        }

        public void OnHostileJoinMenuChanged(HostileJoinMenu menu)
        {
            HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                adapter.Dispose();
            }

            HostileJoinMenuScreen screen = new HostileJoinMenuScreen(adapter);
            if (_screenManager.CurrentScreen is HostileJoinMenuScreen)
            {
                _screenManager.RefreshTop<HostileJoinMenuScreen>(screen, "hostile join menu changed");
                return;
            }

            Push(screen, "hostile join menu ready");
        }

        public void OnHostileJoinMenuClosed(HostileJoinMenu menu)
        {
            _screenManager.Pop<HostileJoinMenuScreen>("hostile join menu closed");
        }

        public void OnMoveTroopPopupReady(TroopHUDEntryMovable movable)
        {
            Push(new MoveTroopPopupScreen(new MoveTroopPopupAdapter(movable)), "move troop popup ready");
        }

        public void OnMoveTroopPopupClosed(TroopHUDEntryMovable movable)
        {
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

            screen.Refresh(true);
        }

        public void OnCommanderSheetComponentChanged(UnityEngine.Component component)
        {
            if (component == null || component.GetComponentInParent<CommanderSheet>(true) == null)
            {
                return;
            }

            OnCommanderSheetChanged();
        }

        public void ResyncFromRuntimeState()
        {
            List<Screen> activeScreens = new List<Screen>();
            for (int i = 0; i < _runtimeScreenProbes.Count; i++)
            {
                _runtimeScreenProbes[i].AddActiveScreens(activeScreens);
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
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SoqAccessPlugin.Instance?.LogWarning(
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
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector ignored " + reason + " because no screen could be built");
                return false;
            }

            if (!screen.IsPresent())
            {
                SoqAccessPlugin.Instance?.LogWarning(
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
            SoqAccessPlugin.Instance?.LogWarning(
                "ScreenDetector ignored "
                + reason
                + "; unexpected top screen "
                + (current != null ? current.GetType().Name : "<none>"));
        }

        private bool IsCurrentQuestionDialogSource(object sourceKey)
        {
            QuestionDialogScreen screen = _screenManager.CurrentScreen as QuestionDialogScreen;
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
