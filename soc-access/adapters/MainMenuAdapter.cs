using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class MainMenuAdapter
    {
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> LeftButtonContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_leftButtonContainer");
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> ContinueContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_continueContainer");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> ContinueButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_continueButton");
        private static readonly AccessTools.FieldRef<MainMenu, UITextMesh> ContinueHeaderTextRef =
            AccessTools.FieldRefAccess<MainMenu, UITextMesh>("_continueHeaderText");
        private static readonly AccessTools.FieldRef<MainMenu, UITextMesh> ContinueDetailsTextRef =
            AccessTools.FieldRefAccess<MainMenu, UITextMesh>("_continueDetailsText");
        private static readonly AccessTools.FieldRef<MainMenu, UITextMesh> CampaignCompletedTextRef =
            AccessTools.FieldRefAccess<MainMenu, UITextMesh>("_campaignCompletedText");
        private static readonly AccessTools.FieldRef<MainMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<MainMenu, MainMenuManagerContainer>("_managerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> CampaignButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_campaignButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> SkirmishButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_skirmishButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> LoadGameButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_loadGameButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> QuitButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_quitButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> MapEditorButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_mapEditorButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> CommunityMapsButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_communityMapsButton");
        private static readonly AccessTools.FieldRef<MainMenu, FoldoutUIButton> ExtrasFoldoutRef =
            AccessTools.FieldRefAccess<MainMenu, FoldoutUIButton>("_extrasFoldoutButton");
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> ExtrasFoldoutContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_extrasFoldoutContainer");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> HotseatButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_hotseatButton");
        private static readonly AccessTools.FieldRef<MainMenu, FoldoutUIButton> MultiplayerFoldoutRef =
            AccessTools.FieldRefAccess<MainMenu, FoldoutUIButton>("_multiplayerFoldoutButton");
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> MultiplayerFoldoutContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_foldoutContainer");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> TutorialAndCodexButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_tutorialAndCodexButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> PlayerStatsButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_playerStatsButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> CreditsButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_creditsButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> BattlegroundsButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_battlegroundsButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> DigitalArtbookButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_digitalArtbookButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> HostOnlineButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_hostOnlineButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> JoinWithCodeButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_joinWithCodeButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> FindOnlineButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_findOnlineButton");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> StartHotseatButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_startHotseatButton");
        private static readonly AccessTools.FieldRef<FoldoutUIButton, HoverEventsImage> FoldoutBackgroundRef =
            AccessTools.FieldRefAccess<FoldoutUIButton, HoverEventsImage>("_foldoutBackground");
        private static readonly AccessTools.FieldRef<FoldoutUIButton, bool> FoldoutIsOverButtonRef =
            AccessTools.FieldRefAccess<FoldoutUIButton, bool>("_isOverButton");

        private readonly MainMenu _mainMenu;
        private readonly List<IMenuButtonAdapter> _topLevelItems;

        public MainMenuAdapter(MainMenu mainMenu)
        {
            _mainMenu = mainMenu;
            ExtrasFoldout = new NativeFoldoutAdapter(
                new StandardMenuButtonAdapter(
                    GetFoldoutButton(ExtrasFoldoutRef(_mainMenu)),
                    () => MenuButtonAdapterBase.IsButtonVisible(GetFoldoutButton(ExtrasFoldoutRef(_mainMenu))),
                    () => ExtrasFoldout != null && ExtrasFoldout.Open()),
                ExtrasFoldoutRef(_mainMenu),
                new[]
                {
                    CreateMainMenuButton(TutorialAndCodexButtonRef(_mainMenu)),
                    CreateMainMenuButton(PlayerStatsButtonRef(_mainMenu)),
                    CreateMainMenuButton(CreditsButtonRef(_mainMenu)),
                    CreateMainMenuButton(BattlegroundsButtonRef(_mainMenu)),
                    CreateMainMenuButton(DigitalArtbookButtonRef(_mainMenu))
                },
                ExtrasFoldoutContainerRef(_mainMenu));
            MultiplayerFoldout = new NativeFoldoutAdapter(
                new StandardMenuButtonAdapter(
                    GetFoldoutButton(MultiplayerFoldoutRef(_mainMenu)),
                    () => MenuButtonAdapterBase.IsButtonVisible(GetFoldoutButton(MultiplayerFoldoutRef(_mainMenu))),
                    () => MultiplayerFoldout != null && MultiplayerFoldout.Open()),
                MultiplayerFoldoutRef(_mainMenu),
                new[]
                {
                    CreateMainMenuButton(HostOnlineButtonRef(_mainMenu)),
                    CreateMainMenuButton(JoinWithCodeButtonRef(_mainMenu)),
                    CreateMainMenuButton(FindOnlineButtonRef(_mainMenu)),
                    CreateMainMenuButton(StartHotseatButtonRef(_mainMenu))
                },
                MultiplayerFoldoutContainerRef(_mainMenu));

            _topLevelItems = new List<IMenuButtonAdapter>
            {
                new MainMenuButtonAdapter(
                    ContinueButtonRef(_mainMenu),
                    GetContinueLabel,
                    IsContinueVisible,
                    () => NativeSelectionUtility.Click(ContinueButtonRef(_mainMenu))),
                CreateMainMenuButton(CampaignButtonRef(_mainMenu)),
                CreateMainMenuButton(SkirmishButtonRef(_mainMenu)),
                CreateMainMenuButton(LoadGameButtonRef(_mainMenu)),
                // Map editor is not accessible yet, so do not expose it in the main menu.
                // CreateMainMenuButton(MapEditorButtonRef(_mainMenu)),
                CreateMainMenuButton(CommunityMapsButtonRef(_mainMenu)),
                ExtrasFoldout.TriggerButton,
                CreateMainMenuButton(HotseatButtonRef(_mainMenu)),
                MultiplayerFoldout.TriggerButton,
                CreateMainMenuButton(QuitButtonRef(_mainMenu))
            };

            MainMenuManager.Settings settings = GetMainMenuSettings();
            OptionsButton = settings != null ? new OptionsMenuButtonAdapter(
                settings.OptionsButton,
                () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                () => NativeSelectionUtility.Click(settings.OptionsButton)) : null;
        }

        public object SourceKey
        {
            get { return _mainMenu; }
        }

        public IReadOnlyList<IMenuButtonAdapter> TopLevelItems
        {
            get { return _topLevelItems; }
        }

        public NativeFoldoutAdapter ExtrasFoldout { get; private set; }

        public NativeFoldoutAdapter MultiplayerFoldout { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public bool IsPresent()
        {
            return _mainMenu != null
                && IsLiveSceneObject(_mainMenu.gameObject)
                && IsGameObjectActive(LeftButtonContainerRef(_mainMenu));
        }

        private bool IsContinueVisible()
        {
            return IsGameObjectActive(ContinueContainerRef(_mainMenu)) && MenuButtonAdapterBase.IsButtonVisible(ContinueButtonRef(_mainMenu));
        }

        private string GetContinueLabel()
        {
            return MenuButtonTextUtility.JoinParts(
                GetVisibleText(CampaignCompletedTextRef(_mainMenu)),
                GetVisibleText(ContinueHeaderTextRef(_mainMenu)),
                GetVisibleText(ContinueDetailsTextRef(_mainMenu)));
        }

        private static IMenuButtonAdapter CreateMainMenuButton(UIButton button)
        {
            return new StandardMenuButtonAdapter(button, null, () => NativeSelectionUtility.Click(button));
        }

        private static UIButton GetFoldoutButton(FoldoutUIButton foldout)
        {
            return foldout != null ? ((Component)foldout).GetComponent<UIButton>() : null;
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _mainMenu != null ? ManagerContainerRef(_mainMenu) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private static string GetVisibleText(UITextMesh textMesh)
        {
            if (textMesh == null || !textMesh.gameObject.activeInHierarchy)
            {
                return string.Empty;
            }

            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private sealed class MainMenuButtonAdapter : MenuButtonAdapterBase
        {
            private readonly Func<string> _getLabel;

            public MainMenuButtonAdapter(
                UIButton button,
                Func<string> getLabel,
                Func<bool> isVisible,
                Func<bool> activate)
                : base(button, isVisible, activate)
            {
                _getLabel = getLabel;
            }

            protected override string BuildLabel()
            {
                return _getLabel != null ? _getLabel() : string.Empty;
            }
        }

        public sealed class NativeFoldoutAdapter
        {
            private readonly FoldoutUIButton _foldout;
            private readonly List<IMenuButtonAdapter> _items;
            private readonly GameObject _itemContainer;

            public NativeFoldoutAdapter(IMenuButtonAdapter triggerButton, FoldoutUIButton foldout, IEnumerable<IMenuButtonAdapter> items, GameObject itemContainer)
            {
                TriggerButton = triggerButton;
                _foldout = foldout;
                _items = new List<IMenuButtonAdapter>(items ?? new IMenuButtonAdapter[0]);
                _itemContainer = itemContainer;
            }

            public IMenuButtonAdapter TriggerButton { get; private set; }

            public UIButton Button
            {
                get { return TriggerButton != null ? TriggerButton.Button : null; }
            }

            public object SourceKey
            {
                get { return _foldout; }
            }

            public IReadOnlyList<IMenuButtonAdapter> Items
            {
                get { return _items; }
            }

            public string GetLabel()
            {
                return TriggerButton != null ? TriggerButton.GetLabel() : string.Empty;
            }

            public bool IsVisible()
            {
                return MenuButtonAdapterBase.IsButtonVisible(Button);
            }

            public bool IsOpen()
            {
                HoverEventsImage background = GetBackground();
                return background != null
                    && background.gameObject.activeSelf
                    && IsGameObjectActive(_itemContainer);
            }

            public bool Open()
            {
                if (!IsVisible())
                {
                    return false;
                }

                HoverEventsImage background = GetBackground();
                if (background == null)
                {
                    return false;
                }

                MainMenuAdapter.FoldoutIsOverButtonRef(_foldout) = true;
                background.gameObject.SetActive(true);
                _foldout.OnOpenedFoldout?.Invoke(_foldout);
                return true;
            }

            public bool Close()
            {
                if (_foldout == null)
                {
                    return false;
                }

                _foldout.ForceClose();
                return true;
            }

            private HoverEventsImage GetBackground()
            {
                return _foldout != null ? MainMenuAdapter.FoldoutBackgroundRef(_foldout) : null;
            }
        }
    }
}
