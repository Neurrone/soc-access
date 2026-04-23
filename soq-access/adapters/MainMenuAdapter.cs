using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class MainMenuAdapter
    {
        private static readonly HashSet<int> LoggedMainMenuNodeTrees = new HashSet<int>();

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
        private static readonly AccessTools.FieldRef<UITextMeshLocalization, string> UITextMeshLocalizationKeyRef =
            AccessTools.FieldRefAccess<UITextMeshLocalization, string>("_localizationKey");

        private readonly MainMenu _mainMenu;
        private readonly List<IMenuButtonAdapter> _topLevelItems;

        public MainMenuAdapter(MainMenu mainMenu)
        {
            _mainMenu = mainMenu;
            ExtrasFoldout = new NativeFoldoutAdapter(
                "extras",
                new FoldoutMenuButtonAdapter(
                    "extras",
                    GetFoldoutButton(ExtrasFoldoutRef(_mainMenu)),
                    () => MenuButtonAdapterBase.IsButtonVisible(GetFoldoutButton(ExtrasFoldoutRef(_mainMenu))),
                    () => ExtrasFoldout != null && ExtrasFoldout.Open()),
                ExtrasFoldoutRef(_mainMenu),
                new[]
                {
                    new StandardMenuButtonAdapter("tutorial-and-codex", TutorialAndCodexButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("player-stats", PlayerStatsButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("credits", CreditsButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("battlegrounds", BattlegroundsButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("digital-artbook", DigitalArtbookButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly)
                });
            MultiplayerFoldout = new NativeFoldoutAdapter(
                "multiplayer",
                new FoldoutMenuButtonAdapter(
                    "multiplayer",
                    GetFoldoutButton(MultiplayerFoldoutRef(_mainMenu)),
                    () => MenuButtonAdapterBase.IsButtonVisible(GetFoldoutButton(MultiplayerFoldoutRef(_mainMenu))),
                    () => MultiplayerFoldout != null && MultiplayerFoldout.Open()),
                MultiplayerFoldoutRef(_mainMenu),
                new[]
                {
                    new StandardMenuButtonAdapter("host-online", HostOnlineButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("join-with-code", JoinWithCodeButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("find-online", FindOnlineButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly),
                    new StandardMenuButtonAdapter("start-hotseat", StartHotseatButtonRef(_mainMenu), null, null, MenuButtonFocusMode.SemanticOnly)
                });

            _topLevelItems = new List<IMenuButtonAdapter>
            {
                new ContinueMenuButtonAdapter("continue", ContinueButtonRef(_mainMenu), IsContinueVisible),
                new StandardMenuButtonAdapter("campaign", CampaignButtonRef(_mainMenu)),
                new StandardMenuButtonAdapter("skirmish", SkirmishButtonRef(_mainMenu)),
                new StandardMenuButtonAdapter("load-game", LoadGameButtonRef(_mainMenu)),
                new StandardMenuButtonAdapter("quit", QuitButtonRef(_mainMenu)),
                new StandardMenuButtonAdapter("map-editor", MapEditorButtonRef(_mainMenu)),
                new StandardMenuButtonAdapter("community-maps", CommunityMapsButtonRef(_mainMenu)),
                ExtrasFoldout.TriggerButton,
                new StandardMenuButtonAdapter("hotseat", HotseatButtonRef(_mainMenu)),
                MultiplayerFoldout.TriggerButton
            };

            LogDiscoveredMenuNodesOnce();
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

        private void LogDiscoveredMenuNodesOnce()
        {
            if (_mainMenu == null || _mainMenu.gameObject == null)
            {
                return;
            }

            int menuId = _mainMenu.gameObject.GetInstanceID();
            if (!LoggedMainMenuNodeTrees.Add(menuId))
            {
                return;
            }

            SoqAccessPlugin.Instance?.LogInfo(
                "MainMenuAdapter runtime node dump for "
                + DescribeTransform(_mainMenu.transform)
                + " in scene "
                + _mainMenu.gameObject.scene.name);

            LogButtonNodes("top-level", _topLevelItems);
            LogFoldoutNodes("extras", ExtrasFoldout);
            LogFoldoutNodes("multiplayer", MultiplayerFoldout);
        }

        private static void LogButtonNodes(string group, IReadOnlyList<IMenuButtonAdapter> items)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                IMenuButtonAdapter item = items[i];
                if (item == null)
                {
                    continue;
                }

                LogButtonNodes(group, item);
            }
        }

        private static void LogFoldoutNodes(string group, NativeFoldoutAdapter foldout)
        {
            if (foldout == null)
            {
                return;
            }

            LogButtonNodes(group + "-button", foldout.TriggerButton);
            LogButtonNodes(group + "-items", foldout.Items);
        }

        private static void LogButtonNodes(string group, IMenuButtonAdapter item)
        {
            string itemId = item != null ? item.Id : string.Empty;
            UIButton button = item != null ? item.Button : null;
            if (button == null)
            {
                SoqAccessPlugin.Instance?.LogInfo("MainMenuAdapter node dump [" + group + ":" + itemId + "] button=<null>");
                return;
            }

            Component buttonComponent = (Component)button;
            GameObject buttonObject = buttonComponent.gameObject;
            Selectable selectable = button.GetSelectable();
            SoqAccessPlugin.Instance?.LogInfo(
                "MainMenuAdapter node dump ["
                + group
                + ":"
                + itemId
                + "] path="
                + DescribeTransform(buttonComponent.transform)
                + ", activeInHierarchy="
                + buttonObject.activeInHierarchy
                + ", buttonActive="
                + button.Active
                + ", interactable="
                + button.Interactable
                + ", selectableEnabled="
                + (selectable != null && selectable.isActiveAndEnabled)
                + ", adapterType="
                + item.GetType().Name
                + ", directText=\""
                + MenuButtonTextUtility.NormalizeForSpeech(button.Text)
                + "\"");

            UITextMesh[] textMeshes = buttonObject.GetComponentsInChildren<UITextMesh>(includeInactive: true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null)
                {
                    continue;
                }

                SoqAccessPlugin.Instance?.LogInfo(
                    "  UITextMesh path="
                    + DescribeTransform(textMesh.transform)
                    + ", activeInHierarchy="
                    + textMesh.gameObject.activeInHierarchy
                    + ", enabled="
                    + textMesh.enabled
                    + ", text=\""
                    + MenuButtonTextUtility.NormalizeForSpeech(textMesh.Text)
                    + "\"");

                LogUITextMeshLocalization(textMesh);
            }

            Text[] texts = buttonObject.GetComponentsInChildren<Text>(includeInactive: true);
            for (int i = 0; i < texts.Length; i++)
            {
                Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                SoqAccessPlugin.Instance?.LogInfo(
                    "  Text path="
                    + DescribeTransform(text.transform)
                    + ", activeInHierarchy="
                    + text.gameObject.activeInHierarchy
                    + ", enabled="
                    + text.enabled
                    + ", text=\""
                    + MenuButtonTextUtility.NormalizeForSpeech(text.text)
                    + "\"");
            }
        }

        private static string DescribeTransform(Transform transform)
        {
            if (transform == null)
            {
                return "<null>";
            }

            string path = transform.name;
            Transform current = transform.parent;
            while (current != null)
            {
                path = current.name + "/" + path;
                current = current.parent;
            }

            return path;
        }

        private static void LogUITextMeshLocalization(UITextMesh textMesh)
        {
            if (textMesh == null)
            {
                return;
            }

            UITextMeshLocalization localization = ((Component)textMesh).GetComponent<UITextMeshLocalization>();
            if (localization == null)
            {
                return;
            }

            string key = UITextMeshLocalizationKeyRef(localization) ?? string.Empty;
            string resolvedText = string.Empty;
            if (!string.IsNullOrWhiteSpace(key) && GlobalLocalizationVariables.LocalizationHandler != null)
            {
                resolvedText = MenuButtonTextUtility.NormalizeForSpeech(GlobalLocalizationVariables.LocalizationHandler.GetText(key));
            }

            SoqAccessPlugin.Instance?.LogInfo(
                "    UITextMeshLocalization key=\""
                + key
                + "\", resolvedText=\""
                + resolvedText
                + "\"");
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
