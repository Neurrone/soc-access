using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class MainMenuAdapter
    {
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> LeftButtonContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_leftButtonContainer");
        private static readonly AccessTools.FieldRef<MainMenu, GameObject> ContinueContainerRef =
            AccessTools.FieldRefAccess<MainMenu, GameObject>("_continueContainer");
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> ContinueButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_continueButton");
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
        private static readonly AccessTools.FieldRef<MainMenu, UIButton> HotseatButtonRef =
            AccessTools.FieldRefAccess<MainMenu, UIButton>("_hotseatButton");
        private static readonly AccessTools.FieldRef<MainMenu, FoldoutUIButton> MultiplayerFoldoutRef =
            AccessTools.FieldRefAccess<MainMenu, FoldoutUIButton>("_multiplayerFoldoutButton");
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
                "extras",
                new StandardMenuButtonAdapter(
                    "extras",
                    GetFoldoutButton(ExtrasFoldoutRef(_mainMenu)),
                    () => MenuButtonAdapterBase.IsButtonVisible(GetFoldoutButton(ExtrasFoldoutRef(_mainMenu))),
                    () => ExtrasFoldout != null && ExtrasFoldout.Open()),
                ExtrasFoldoutRef(_mainMenu),
                new[]
                {
                    CreateMainMenuButton("tutorial-and-codex", TutorialAndCodexButtonRef(_mainMenu)),
                    CreateMainMenuButton("player-stats", PlayerStatsButtonRef(_mainMenu)),
                    CreateMainMenuButton("credits", CreditsButtonRef(_mainMenu)),
                    CreateMainMenuButton("battlegrounds", BattlegroundsButtonRef(_mainMenu)),
                    CreateMainMenuButton("digital-artbook", DigitalArtbookButtonRef(_mainMenu))
                });
            MultiplayerFoldout = new NativeFoldoutAdapter(
                "multiplayer",
                new StandardMenuButtonAdapter(
                    "multiplayer",
                    GetFoldoutButton(MultiplayerFoldoutRef(_mainMenu)),
                    () => MenuButtonAdapterBase.IsButtonVisible(GetFoldoutButton(MultiplayerFoldoutRef(_mainMenu))),
                    () => MultiplayerFoldout != null && MultiplayerFoldout.Open()),
                MultiplayerFoldoutRef(_mainMenu),
                new[]
                {
                    CreateMainMenuButton("host-online", HostOnlineButtonRef(_mainMenu)),
                    CreateMainMenuButton("join-with-code", JoinWithCodeButtonRef(_mainMenu)),
                    CreateMainMenuButton("find-online", FindOnlineButtonRef(_mainMenu)),
                    CreateMainMenuButton("start-hotseat", StartHotseatButtonRef(_mainMenu))
                });

            _topLevelItems = new List<IMenuButtonAdapter>
            {
                new ContinueMenuButtonAdapter("continue", ContinueButtonRef(_mainMenu), IsContinueVisible, () => NativeSelectionUtility.Click(ContinueButtonRef(_mainMenu))),
                CreateMainMenuButton("campaign", CampaignButtonRef(_mainMenu)),
                CreateMainMenuButton("skirmish", SkirmishButtonRef(_mainMenu)),
                CreateMainMenuButton("load-game", LoadGameButtonRef(_mainMenu)),
                CreateMainMenuButton("quit", QuitButtonRef(_mainMenu)),
                CreateMainMenuButton("map-editor", MapEditorButtonRef(_mainMenu)),
                CreateMainMenuButton("community-maps", CommunityMapsButtonRef(_mainMenu)),
                ExtrasFoldout.TriggerButton,
                CreateMainMenuButton("hotseat", HotseatButtonRef(_mainMenu)),
                MultiplayerFoldout.TriggerButton
            };
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

        private static IMenuButtonAdapter CreateMainMenuButton(string id, UIButton button)
        {
            return new StandardMenuButtonAdapter(id, button, null, () => NativeSelectionUtility.Click(button));
        }

        private static UIButton GetFoldoutButton(FoldoutUIButton foldout)
        {
            return foldout != null ? ((Component)foldout).GetComponent<UIButton>() : null;
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        internal sealed class NativeFoldoutAdapter
        {
            private readonly FoldoutUIButton _foldout;
            private readonly List<IMenuButtonAdapter> _items;

            public NativeFoldoutAdapter(string id, IMenuButtonAdapter triggerButton, FoldoutUIButton foldout, IEnumerable<IMenuButtonAdapter> items)
            {
                Id = id ?? string.Empty;
                TriggerButton = triggerButton;
                _foldout = foldout;
                _items = new List<IMenuButtonAdapter>(items ?? new IMenuButtonAdapter[0]);
            }

            public string Id { get; private set; }

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
                return background != null && background.gameObject.activeSelf;
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
