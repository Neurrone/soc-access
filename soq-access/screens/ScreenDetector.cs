using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Central detector that translates game lifecycle hooks into accessibility screens.
    /// Harmony patches should call this class directly instead of making stack decisions.
    ///
    /// It also owns the registry of runtime probes used for startup / hot-reload recovery,
    /// so probe selection stays here rather than leaking into the plugin bootstrap.
    /// </summary>
    internal sealed class ScreenDetector
    {
        private readonly ScreenManager _screenManager;
        private readonly List<IRuntimeScreenProbe> _runtimeScreenProbes;

        public ScreenDetector(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _runtimeScreenProbes = new List<IRuntimeScreenProbe>
            {
                new MainMenuRuntimeScreenProbe(),
                new CampaignMenuRuntimeScreenProbe(),
                new CampaignMapSelectRuntimeScreenProbe(),
                new AdventureMapRuntimeScreenProbe(),
                new HostileJoinMenuRuntimeScreenProbe(),
                new MoveTroopPopupRuntimeScreenProbe(),
                new WorldChoiceMenuRuntimeScreenProbe(),
                new CommanderSheetRuntimeScreenProbe(),
                new LetterboxStoryTextRuntimeScreenProbe(),
                new StoryTextRuntimeScreenProbe(),
                new DialogueMenuRuntimeScreenProbe(),
                new QuestionDialogRuntimeScreenProbe(),
                new TutorialRuntimeScreenProbe()
            };
        }

        public void OnTutorialOpened(TutorialMenu tutorialMenu)
        {
            PushOrReplaceTutorialScreen(tutorialMenu);
        }

        public void OnTutorialPageChanged(TutorialMenu tutorialMenu)
        {
            PushOrReplaceTutorialScreen(tutorialMenu);
        }

        public void OnTutorialClosed(TutorialMenu tutorialMenu)
        {
            if (tutorialMenu == null || !_screenManager.RemoveScreenForSource(tutorialMenu))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnQuestionDialogOpened(object sourceKey, PopupMenu.Settings settings)
        {
            if (settings == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector.OnQuestionDialogOpened received null settings; falling back to runtime resync");
                ResyncFromRuntimeState();
                return;
            }

            object resolvedSourceKey = sourceKey ?? (settings != null ? (object)settings.ContainerTransform : null);
            QuestionDialogAdapter adapter = new QuestionDialogAdapter(
                resolvedSourceKey,
                settings);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            QuestionDialogScreen screen = new QuestionDialogScreen(adapter.SourceKey, adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is QuestionDialogScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnQuestionDialogClosed(object sourceKey)
        {
            if (sourceKey == null || !_screenManager.RemoveScreenForSource(sourceKey))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnLetterboxStoryTextShown(LetterboxStoryText storyText)
        {
            LetterboxStoryTextAdapter adapter = new LetterboxStoryTextAdapter(storyText);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            StoryTextScreen screen = new StoryTextScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is StoryTextScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnLetterboxStoryTextHidden(LetterboxStoryText storyText)
        {
            if (storyText == null || !_screenManager.RemoveScreenForSource(storyText))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnStoryTextShown(StoryText storyText)
        {
            StoryTextAdapter adapter = new StoryTextAdapter(storyText);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            StoryTextScreen screen = new StoryTextScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is StoryTextScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnStoryTextHidden(StoryText storyText)
        {
            if (storyText == null || !_screenManager.RemoveScreenForSource(storyText))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnDialogueMenuAvailable(DialogueMenu dialogueMenu)
        {
            DialogueMenuAdvanceGuard.ClearPending(dialogueMenu);
            DialogueMenuAdapter adapter = new DialogueMenuAdapter(dialogueMenu);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            StoryTextScreen screen = new StoryTextScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is StoryTextScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnDialogueMenuHidden(DialogueMenu dialogueMenu)
        {
            if (dialogueMenu == null || !_screenManager.RemoveScreenForSource(dialogueMenu))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnMainMenuAvailable(MainMenu mainMenu)
        {
            MainMenuAdapter adapter = new MainMenuAdapter(mainMenu);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            MainMenuScreen screen = new MainMenuScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is MainMenuScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.PushScreen(screen);
        }

        public void OnMainMenuHidden(MainMenu mainMenu)
        {
            if (mainMenu == null)
            {
                ResyncFromRuntimeState();
                return;
            }

            MainMenuAdapter adapter = new MainMenuAdapter(mainMenu);
            object extrasSourceKey = adapter.ExtrasFoldout != null ? adapter.ExtrasFoldout.SourceKey : null;
            object multiplayerSourceKey = adapter.MultiplayerFoldout != null ? adapter.MultiplayerFoldout.SourceKey : null;
            bool removed = _screenManager.RemoveScreens(screen =>
                (screen is MainMenuScreen && ReferenceEquals(screen.SourceKey, mainMenu))
                || (extrasSourceKey != null && ReferenceEquals(screen.SourceKey, extrasSourceKey))
                || (multiplayerSourceKey != null && ReferenceEquals(screen.SourceKey, multiplayerSourceKey)));
            if (!removed)
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnMainMenuFoldoutOpened(MainMenu mainMenu, FoldoutUIButton foldoutButton)
        {
            if (mainMenu == null || foldoutButton == null)
            {
                return;
            }

            MainMenuAdapter owner = new MainMenuAdapter(mainMenu);
            MainMenuAdapter.NativeFoldoutAdapter foldout = ResolveFoldout(owner, foldoutButton);
            if (owner == null || foldout == null || !owner.IsPresent() || !foldout.IsVisible() || !foldout.IsOpen())
            {
                return;
            }

            FoldoutMenuScreen screen = new FoldoutMenuScreen(owner, foldout);
            Screen current = _screenManager.CurrentScreen;
            if (current is FoldoutMenuScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            RemoveKnownFoldouts(owner);
            _screenManager.PushScreen(screen);
        }

        public void OnMainMenuFoldoutClosed(FoldoutUIButton foldoutButton)
        {
            if (foldoutButton == null || !_screenManager.RemoveScreenForSource(foldoutButton))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnCampaignMenuAvailable(CampaignMenu campaignMenu)
        {
            CampaignMenuAdapter adapter = new CampaignMenuAdapter(campaignMenu);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            CampaignMenuScreen screen = new CampaignMenuScreen(adapter);
            _screenManager.PushScreen(screen);
        }

        public void OnCampaignMapSelectAvailable(CampaignMapSelectedInformationView informationView)
        {
            CampaignMapSelectAdapter adapter = CampaignMapSelectRuntimeScreenProbe.FindActiveCampaignMapSelect(informationView);
            if (adapter == null || !adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            CampaignMapSelectScreen screen = new CampaignMapSelectScreen(
                adapter,
                CampaignMapSelectScreen.ConsumeFocusDifficultyAfterNextRebuild());
            Screen current = _screenManager.CurrentScreen;
            if (current is CampaignMapSelectScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                // Difficulty changes cause the game to redraw the selected mission details by
                // calling Show(...) again, so rebuild this accessibility screen from the fresh
                // native state instead of mutating stale labels and button visibility.
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.PushScreen(screen);
        }

        public void OnCampaignMapSelectHidden(CampaignMapSelectedInformationView informationView)
        {
            if (informationView != null)
            {
                _screenManager.RemoveScreenForSource(informationView);
                return;
            }

            _screenManager.RemoveScreens(screen => screen is CampaignMapSelectScreen);
        }

        public void OnMainMenuSceneLoaded(MainMenuSceneType loadedScene)
        {
            if (loadedScene != MainMenuSceneType.Campaign)
            {
                _screenManager.RemoveScreens(screen => screen is CampaignMenuScreen);
            }
        }

        public void OnAdventureSceneLoaded()
        {
            AdventureMapScreen screen = AdventureMapRuntimeScreenProbe.FindActiveAdventureMapScreen();
            if (screen == null || !screen.IsPresent())
            {
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector.OnAdventureSceneLoaded could not find a ready adventure map");
                return;
            }

            Screen current = _screenManager.CurrentScreen;
            if (current is AdventureMapScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.PushScreen(screen);
        }

        public void OnAdventureSceneUnloading()
        {
            _screenManager.RemoveScreens(screen => screen is AdventureMapScreen);
        }

        public void OnWorldChoiceMenuOpened(WorldChoiceMenu menu)
        {
            WorldChoiceMenuAdapter adapter = new WorldChoiceMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            WorldChoiceMenuScreen screen = new WorldChoiceMenuScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is WorldChoiceMenuScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnWorldChoiceMenuClosed(WorldChoiceMenu menu)
        {
            if (menu == null || !_screenManager.RemoveScreenForSource(menu))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnHostileJoinMenuChanged(HostileJoinMenu menu)
        {
            if (menu == null)
            {
                ResyncFromRuntimeState();
                return;
            }

            HostileJoinMenuAdapter adapter = new HostileJoinMenuAdapter(menu);
            if (!adapter.IsPresent())
            {
                adapter.Dispose();
                ResyncFromRuntimeState();
                return;
            }

            HostileJoinMenuScreen screen = new HostileJoinMenuScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is HostileJoinMenuScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnHostileJoinMenuClosed(HostileJoinMenu menu)
        {
            if (menu == null || !_screenManager.RemoveScreenForSource(menu))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnCommanderSheetOpened(CommanderSheet commanderSheet)
        {
            CommanderSheetAdapter adapter = new CommanderSheetAdapter(commanderSheet);
            if (!adapter.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            CommanderSheetScreen screen = new CommanderSheetScreen(adapter);
            Screen current = _screenManager.CurrentScreen;
            if (current is CommanderSheetScreen && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
        }

        public void OnCommanderSheetClosed(CommanderSheet commanderSheet)
        {
            if (commanderSheet == null || !_screenManager.RemoveScreenForSource(commanderSheet))
            {
                ResyncFromRuntimeState();
            }
        }

        public void OnCommanderSheetChanged(object sourceKey)
        {
            CommanderSheetScreen screen = _screenManager.FindScreenForSource<CommanderSheetScreen>(sourceKey);
            if (screen == null)
            {
                return;
            }

            if (!screen.IsPresent())
            {
                _screenManager.RemoveScreenForSource(sourceKey);
                return;
            }

            screen.Refresh(ReferenceEquals(_screenManager.CurrentScreen, screen));
        }

        public void OnCommanderSheetComponentChanged(Component component)
        {
            if (component == null)
            {
                return;
            }

            CommanderSheet sheet = component.GetComponentInParent<CommanderSheet>(true);
            if (sheet != null)
            {
                OnCommanderSheetChanged(sheet);
            }
        }

        public void ResyncFromRuntimeState()
        {
            List<Screen> activeScreens = new List<Screen>();
            for (int i = 0; i < _runtimeScreenProbes.Count; i++)
            {
                _runtimeScreenProbes[i].AddActiveScreens(activeScreens);
            }

            _screenManager.SynchronizeStack(activeScreens);
        }

        private void PushOrReplaceTutorialScreen(TutorialMenu tutorialMenu)
        {
            if (tutorialMenu == null)
            {
                ResyncFromRuntimeState();
                return;
            }

            Screen screen = BuildTutorialScreen(tutorialMenu);
            if (screen == null || !screen.IsPresent())
            {
                ResyncFromRuntimeState();
                return;
            }

            Screen current = _screenManager.CurrentScreen;
            if ((current is TutorialSlideshowScreen || current is TutorialSimpleScreen)
                && ReferenceEquals(current.SourceKey, screen.SourceKey))
            {
                _screenManager.ReplaceTopScreen(screen);
                return;
            }

            _screenManager.RemoveScreenForSource(screen.SourceKey);
            _screenManager.PushScreen(screen);
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

        private void RemoveKnownFoldouts(MainMenuAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }

            object extrasSourceKey = adapter.ExtrasFoldout != null ? adapter.ExtrasFoldout.SourceKey : null;
            object multiplayerSourceKey = adapter.MultiplayerFoldout != null ? adapter.MultiplayerFoldout.SourceKey : null;
            _screenManager.RemoveScreens(screen =>
                (extrasSourceKey != null && ReferenceEquals(screen.SourceKey, extrasSourceKey))
                || (multiplayerSourceKey != null && ReferenceEquals(screen.SourceKey, multiplayerSourceKey)));
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
    }
}
