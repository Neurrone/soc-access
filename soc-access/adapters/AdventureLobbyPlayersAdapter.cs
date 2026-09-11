using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Networking;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Lobby;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Ai;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Lobby;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureLobbyPlayersAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<LobbyMenu, CanvasGroup> CanvasGroupRef =
            AccessTools.FieldRefAccess<LobbyMenu, CanvasGroup>("_canvasGroup");
        private static readonly AccessTools.FieldRef<LobbyMenu, LobbyMapPreview> MapPreviewRef =
            AccessTools.FieldRefAccess<LobbyMenu, LobbyMapPreview>("_mapPreview");
        private static readonly AccessTools.FieldRef<LobbyMenu, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<LobbyMenu, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<LobbyMenu, IClientLobbyFacade> LobbyFacadeRef =
            AccessTools.FieldRefAccess<LobbyMenu, IClientLobbyFacade>("_lobbyFacade");
        private static readonly AccessTools.FieldRef<LobbyMenu, ILocalizationHandler> LocalizationRef =
            AccessTools.FieldRefAccess<LobbyMenu, ILocalizationHandler>("_localizationHandler");

        private static readonly AccessTools.FieldRef<LobbyNavigation, UIBackButton> CommonBackButtonRef =
            AccessTools.FieldRefAccess<LobbyNavigation, UIBackButton>("_commonBackButton");
        private static readonly AccessTools.FieldRef<LobbyNavigation, MainMenuManagerContainer> NavigationManagerContainerRef =
            AccessTools.FieldRefAccess<LobbyNavigation, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");

        private static readonly FieldInfo ActiveEntriesField =
            AccessTools.Field(typeof(LobbyPlayerMenu), "_activeEntries");
        private static readonly FieldInfo MapSettingsChangeButtonField =
            AccessTools.Field(typeof(LobbyMapSettings), "_changeMapSettingsButton");
        private static readonly FieldInfo MapSettingsMixedFactionsToggleField =
            AccessTools.Field(typeof(LobbyMapSettings), "_mixedFactionsToggle");
        private static readonly FieldInfo MapSettingsMixedFactionsToggleContainerField =
            AccessTools.Field(typeof(LobbyMapSettings), "_mixedFactionsToggleContainer");
        private static readonly FieldInfo MapSettingsMixedFactionsClientOnButtonField =
            AccessTools.Field(typeof(LobbyMapSettings), "_mixedFactionsClientONButton");
        private static readonly FieldInfo MapSettingsMixedFactionsClientOffButtonField =
            AccessTools.Field(typeof(LobbyMapSettings), "_mixedFactionsClientOFFButton");
        private static readonly FieldInfo MultiplayerInviteFriendButtonField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_inviteFriendButton");
        private static readonly FieldInfo MultiplayerGameCodeInputField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_gameCodeInputField");
        private static readonly FieldInfo MultiplayerGameNameLabelField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_gameNameLabel");
        private static readonly FieldInfo MultiplayerPublicGameToggleField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_publicGameToggle");
        private static readonly FieldInfo MultiplayerCrossplayToggleField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_crossplayToggle");
        private static readonly FieldInfo MultiplayerXboxCrossplayInformationField =
            AccessTools.Field(typeof(LobbyMultiplayerPanel), "_xboxCrossplayInformation");
        private static readonly FieldInfo ToggleTextMeshField =
            AccessTools.Field(typeof(UIToggle), "_textMesh");

        private readonly LobbyMenu _menu;
        private readonly LobbyNavigation _navigation;
        private readonly LobbyPlayerMenu _playerMenu;
        private readonly LobbyMapSettings _mapSettings;
        private readonly LobbyMenuButtons.Settings _lobbyButtonsSettings;
        private readonly MultiplayerPanelItem _multiplayerPanel;
        private readonly IClientLobbyFacade _facade;
        private readonly ILocalizationHandler _localization;

        // The rows the lobby is drawing, kept while the game's own row list is unchanged.
        private readonly List<LobbyPlayerEntry> _entryScratch = new List<LobbyPlayerEntry>();
        private List<PlayerSlotItem> _playerSlots;
        private int _playerSlotsSignature;

        private string _factionLabel;
        private string _colorLabel;
        private string _startingWielderLabel;
        private string _partnershipLabel;
        private string _aiDifficultyLabel;

        public AdventureLobbyPlayersAdapter(
            LobbyMenu menu,
            LobbyNavigation navigation,
            LobbyPlayerMenu playerMenu,
            LobbyMapSettings mapSettings,
            LobbyMenuButtons.Settings lobbyButtons,
            LobbyMultiplayerPanel multiplayerPanel)
        {
            _menu = menu;
            _facade = menu != null ? LobbyFacadeRef(menu) : null;
            _localization = menu != null ? LocalizationRef(menu) : GlobalLocalizationVariables.LocalizationHandler;
            _navigation = navigation;
            _playerMenu = playerMenu;
            _mapSettings = mapSettings;
            _lobbyButtonsSettings = lobbyButtons;
            _multiplayerPanel = multiplayerPanel != null
                ? new MultiplayerPanelItem(multiplayerPanel, _localization)
                : null;
            BackButton = CreateBackButton();
            OptionsButton = CreateOptionsButton();
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public bool IsPresent()
        {
            CanvasGroup canvasGroup = _menu != null ? CanvasGroupRef(_menu) : null;
            GameObject gameObject = _menu != null ? ((Component)_menu).gameObject : null;
            return _menu != null
                && IsLoadedMainMenuScene(MainMenuSceneType.AdventureLobby)
                && IsLiveSceneObject(gameObject)
                && gameObject.activeInHierarchy
                && canvasGroup != null
                && (canvasGroup.blocksRaycasts || canvasGroup.alpha > 0.5f);
        }

        public string Title
        {
            get { return GetMainMenuTitle(); }
        }

        public string PlayersLabel
        {
            get { return GetLocalizedText("Common/Players", string.Empty); }
        }

        /// <summary>The name the map preview panel draws over its picture.</summary>
        public string MapTitle
        {
            get
            {
                LobbyMapPreview preview = _menu != null ? MapPreviewRef(_menu) : null;
                return LobbyMapPreviewText.GetTitle(preview);
            }
        }

        /// <summary>What the preview panel draws under that name, one drawn line at a time.</summary>
        public string MapDescription
        {
            get
            {
                LobbyMapPreview preview = _menu != null ? MapPreviewRef(_menu) : null;
                return LobbyMapPreviewText.GetInfo(preview);
            }
        }

        /// <summary>Whether the page is still taking input. It fades as the lobby is left, and the
        /// game switches its canvas group off wholesale while one of its own windows is up (measured
        /// 2026-09-06 with the game settings window open: alpha 1, interactable false) - which turns
        /// every control on the page unavailable at once.</summary>
        public bool IsInteractive()
        {
            CanvasGroup canvasGroup = _menu != null ? CanvasGroupRef(_menu) : null;
            return canvasGroup != null && canvasGroup.alpha >= 1f && canvasGroup.interactable;
        }

        /// <summary>The game's own names for the settings each player row draws a button for, read
        /// from the same localization keys <c>LobbyPlayerEntry</c> writes their tooltips with.</summary>
        public string FactionLabel
        {
            get { return _factionLabel ?? (_factionLabel = GetLocalizedText("Adventure/TeamQueueHUD/Faction", string.Empty)); }
        }

        public string ColorLabel
        {
            get { return _colorLabel ?? (_colorLabel = GetLocalizedText("Lobby/LobbyPlayerMenu/SetColor", string.Empty)); }
        }

        public string StartingWielderLabel
        {
            get { return _startingWielderLabel ?? (_startingWielderLabel = GetLocalizedText("Lobby/LobbyPlayerMenu/SetStartingWielder", string.Empty)); }
        }

        public string PartnershipLabel
        {
            get { return _partnershipLabel ?? (_partnershipLabel = GetLocalizedText("Lobby/LobbyPlayerMenu/Coop", string.Empty)); }
        }

        public string AiDifficultyLabel
        {
            get { return _aiDifficultyLabel ?? (_aiDifficultyLabel = GetLocalizedText("Lobby/LobbyPlayerMenu/SetAiDifficulty", string.Empty)); }
        }

        /// <summary>The rows the lobby is drawing, in team order. They are read off the player menu's
        /// own list of live rows (<c>_activeEntries</c>, which it adds to in <c>Spawn</c> and removes
        /// from in <c>Despawn</c>), and the list is rebuilt when that changes: the key is the row
        /// count with each row's instance id and team id, which is what a spawn, a despawn or a move
        /// to another team alters. This is what the lobby's own refresh used to be reported for.
        /// </summary>
        public IReadOnlyList<PlayerSlotItem> GetPlayerSlots()
        {
            List<LobbyPlayerEntry> entries = _playerMenu != null && ActiveEntriesField != null
                ? ActiveEntriesField.GetValue(_playerMenu) as List<LobbyPlayerEntry>
                : null;

            _entryScratch.Clear();
            int signature = 17;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                LobbyPlayerEntry entry = entries[i];
                if (entry == null)
                {
                    continue;
                }

                GameObject gameObject = ((Component)entry).gameObject;
                if (gameObject == null || !gameObject.activeInHierarchy)
                {
                    continue;
                }

                _entryScratch.Add(entry);
                unchecked
                {
                    signature = (signature * 31) + entry.GetInstanceID();
                    signature = (signature * 31) + entry.TeamId;
                }
            }

            if (_playerSlots != null && _playerSlots.Count == _entryScratch.Count && _playerSlotsSignature == signature)
            {
                _entryScratch.Clear();
                return _playerSlots;
            }

            List<PlayerSlotItem> slots = new List<PlayerSlotItem>(_entryScratch.Count);
            for (int i = 0; i < _entryScratch.Count; i++)
            {
                slots.Add(new PlayerSlotItem(this, _entryScratch[i]));
            }

            _entryScratch.Clear();
            slots.Sort((left, right) => left.TeamId.CompareTo(right.TeamId));
            _playerSlots = slots;
            _playerSlotsSignature = signature;
            return _playerSlots;
        }

        public LobbyPlayerSettingsItem GetSettingsItem()
        {
            UIButton button = _mapSettings != null && MapSettingsChangeButtonField != null
                ? MapSettingsChangeButtonField.GetValue(_mapSettings) as UIButton
                : null;
            return button != null ? new LobbyPlayerSettingsItem(button, _localization) : null;
        }

        public MixedFactionsItem GetMixedFactionsItem()
        {
            LobbyMapSettings settings = _mapSettings;
            if (settings == null)
            {
                return null;
            }

            UIToggle toggle = MapSettingsMixedFactionsToggleField != null
                ? MapSettingsMixedFactionsToggleField.GetValue(settings) as UIToggle
                : null;
            GameObject container = MapSettingsMixedFactionsToggleContainerField != null
                ? MapSettingsMixedFactionsToggleContainerField.GetValue(settings) as GameObject
                : null;
            UIButton onButton = MapSettingsMixedFactionsClientOnButtonField != null
                ? MapSettingsMixedFactionsClientOnButtonField.GetValue(settings) as UIButton
                : null;
            UIButton offButton = MapSettingsMixedFactionsClientOffButtonField != null
                ? MapSettingsMixedFactionsClientOffButtonField.GetValue(settings) as UIButton
                : null;
            return toggle != null ? new MixedFactionsItem(toggle, container, onButton, offButton, _localization) : null;
        }

        public LobbyButtonItem GetSetReadyButton()
        {
            return _lobbyButtonsSettings != null
                ? LobbyButtonItem.ForButton(_lobbyButtonsSettings.SetReadyButton, _localization)
                : null;
        }

        public LobbyButtonItem GetSetNotReadyButton()
        {
            return _lobbyButtonsSettings != null
                ? LobbyButtonItem.ForButton(_lobbyButtonsSettings.SetNotReadyButton, _localization)
                : null;
        }

        public LobbyButtonItem GetStartGameButton()
        {
            return _lobbyButtonsSettings != null
                ? LobbyButtonItem.ForButton(_lobbyButtonsSettings.StartGameButton, _localization)
                : null;
        }

        /// <summary>The online band, or null in a lobby that has none. The panel object is in the
        /// scene either way; whether it is DRAWN is read from it every frame.</summary>
        public MultiplayerPanelItem GetMultiplayerPanel()
        {
            return _multiplayerPanel != null && _multiplayerPanel.IsPresent ? _multiplayerPanel : null;
        }

        public Tooltip GetButtonTooltip(IMenuButtonAdapter button)
        {
            return button != null ? Tooltip.ForComponent(button.Button as Component, _localization) : null;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private IMenuButtonAdapter CreateBackButton()
        {
            UIBackButton backButton = _navigation != null ? CommonBackButtonRef(_navigation) : null;
            return backButton != null
                ? new StandardMenuButtonAdapter(backButton, () => MenuButtonAdapterBase.IsButtonVisible(backButton), () => NativeSelectionUtility.Click(backButton))
                : null;
        }

        private IMenuButtonAdapter CreateOptionsButton()
        {
            MainMenuManager.Settings settings = GetMainMenuSettings();
            UIButton button = settings != null ? settings.OptionsButton : null;
            return button != null
                ? new OptionsMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
        }

        private MainMenuManager.Settings GetMainMenuSettings()
        {
            MainMenuManagerContainer container = _navigation != null ? NavigationManagerContainerRef(_navigation) : null;
            if (container == null && _menu != null)
            {
                container = ManagerContainerRef(_menu);
            }

            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private string GetMainMenuTitle()
        {
            MainMenuManager.Settings settings = GetMainMenuSettings();
            string title = GetText(settings != null ? settings.TitleText : null);
            if (string.IsNullOrWhiteSpace(title))
            {
                title = GetText(settings != null ? settings.DualTitleText : null);
            }

            return title;
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpokenLines.Clean(GameText.Get(_localization, key, fallback ?? string.Empty));
        }


        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static bool IsLoadedMainMenuScene(MainMenuSceneType sceneType)
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == sceneType;
        }

        public sealed class PlayerSlotItem
        {
            private static readonly FieldInfo NameTextField = AccessTools.Field(typeof(LobbyPlayerEntry), "_nameText");
            private static readonly FieldInfo JoinButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_joinButton");
            private static readonly FieldInfo LeaveButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_leaveButton");
            private static readonly FieldInfo KickButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_kickButton");
            private static readonly FieldInfo ToggleAiButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_toggleAIButton");
            private static readonly FieldInfo UserActionsButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_userActionsButton");
            private static readonly FieldInfo SetColorButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_setColorButton");
            private static readonly FieldInfo SetFactionButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_setFactionButton");
            private static readonly FieldInfo SetStartingWielderButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_setStartingWielderButton");
            private static readonly FieldInfo SetPartnershipButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_setPartnershipButton");
            private static readonly FieldInfo SetAiButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_setAIButton");
            private static readonly FieldInfo PlayerSettingsButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_playerSettingsButton");
            private static readonly FieldInfo FactionIconImageField = AccessTools.Field(typeof(LobbyPlayerEntry), "_factionIconImage");
            private static readonly FieldInfo PartnershipTransformField = AccessTools.Field(typeof(LobbyPlayerEntry), "_partnershipTransform");
            private static readonly FieldInfo DlcNeededContainerField = AccessTools.Field(typeof(LobbyPlayerEntry), "_dlcNeededToJoinDisclaimer");
            private static readonly FieldInfo DlcNeededButtonField = AccessTools.Field(typeof(LobbyPlayerEntry), "_dlcNeededToJoinDisclaimerButton");
            private static readonly FieldInfo WielderLockedIconField = AccessTools.Field(typeof(LobbyPlayerEntry), "_wielderLockedIcon");
            private static readonly FieldInfo ReadyImageField = AccessTools.Field(typeof(LobbyPlayerEntry), "_isReadyImage");
            private static readonly FieldInfo NotReadyImageField = AccessTools.Field(typeof(LobbyPlayerEntry), "_isNotReadyImage");
            private static readonly FieldInfo FactionLookupField = AccessTools.Field(typeof(LobbyPlayerEntry), "_factionLookup");
            private static readonly FieldInfo WielderLookupField = AccessTools.Field(typeof(LobbyPlayerEntry), "_wielderLookup");

            /// <summary>The row's buttons in the order the tooltip walk asks for them, held once
            /// rather than gathered into a fresh array on every read.</summary>
            private static readonly FieldInfo[] PrimarySelectableFields =
            {
                SetFactionButtonField,
                SetColorButtonField,
                SetStartingWielderButtonField,
                SetPartnershipButtonField,
                SetAiButtonField,
                JoinButtonField,
                LeaveButtonField,
                ToggleAiButtonField,
                KickButtonField,
                PlayerSettingsButtonField,
                UserActionsButtonField,
                DlcNeededButtonField
            };

            private readonly AdventureLobbyPlayersAdapter _adapter;
            private readonly LobbyPlayerEntry _entry;

            public PlayerSlotItem(AdventureLobbyPlayersAdapter adapter, LobbyPlayerEntry entry)
            {
                _adapter = adapter;
                _entry = entry;
            }

            public int TeamId
            {
                get { return _entry != null ? _entry.TeamId : -1; }
            }

            /// <summary>The row the game draws this slot as.</summary>
            public Component Entry
            {
                get { return _entry; }
            }

            /// <summary>The name the row draws - a player's, an AI's, or the game's own word for an
            /// empty slot.</summary>
            public string Name
            {
                get { return GetName(); }
            }

            /// <summary>Whether the row draws a ready marker at all, which it does only online.
            /// </summary>
            public bool IsReadyStateDrawn
            {
                get
                {
                    return IsDrawn(Reflect.Get<GameObject>(_entry, ReadyImageField))
                        || IsDrawn(Reflect.Get<GameObject>(_entry, NotReadyImageField));
                }
            }

            public bool IsReady
            {
                get
                {
                    ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                    return team != null && team.IsReadyToStart;
                }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(GetPrimarySelectableComponent(), _adapter != null ? _adapter._localization : null); }
            }

            public LobbyButtonItem JoinButton
            {
                get { return BuildButton(JoinButtonField); }
            }

            public LobbyButtonItem LeaveButton
            {
                get { return BuildButton(LeaveButtonField); }
            }

            public LobbyButtonItem KickButton
            {
                get { return BuildButton(KickButtonField); }
            }

            public LobbyButtonItem ToggleAiButton
            {
                get { return BuildButton(ToggleAiButtonField); }
            }

            public LobbyButtonItem FactionButton
            {
                get { return BuildValueButton(SetFactionButtonField, GetFactionLabel, Reflect.Get<Component>(_entry, FactionIconImageField)); }
            }

            public LobbyButtonItem ColorButton
            {
                get { return BuildValueButton(SetColorButtonField, GetColorLabel); }
            }

            public LobbyButtonItem StartingWielderButton
            {
                get { return BuildValueButton(SetStartingWielderButtonField, GetStartingWielderLabel); }
            }

            public LobbyButtonItem PartnershipButton
            {
                get { return BuildValueButton(SetPartnershipButtonField, GetPartnershipNumber, Reflect.Get<Component>(_entry, PartnershipTransformField)); }
            }

            public LobbyButtonItem AiDifficultyButton
            {
                get { return BuildValueButton(SetAiButtonField, GetAiDifficultyLabel); }
            }

            public LobbyButtonItem PlayerSettingsButton
            {
                get { return BuildTooltipLabelButton(PlayerSettingsButtonField); }
            }

            public LobbyButtonItem PlayerActionsButton
            {
                get { return BuildTooltipLabelButton(UserActionsButtonField); }
            }

            public string DlcRequirementText
            {
                get
                {
                    GameObject container = Reflect.Get<GameObject>(_entry, DlcNeededContainerField);
                    if (container == null || !container.activeInHierarchy)
                    {
                        return string.Empty;
                    }

                    UIButton button = Reflect.Get<UIButton>(_entry, DlcNeededButtonField);
                    Tooltip tooltip = Tooltip.ForComponent(button, _adapter != null ? _adapter._localization : null);
                    return tooltip != null ? string.Join(". ", tooltip.TextLines) : string.Empty;
                }
            }

            public void FocusNative()
            {
                Component selectable = GetPrimarySelectableComponent();
                if (selectable != null)
                {
                    NativeSelectionUtility.Select(selectable);
                }
            }

            private string GetName()
            {
                UITextMesh text = Reflect.Get<UITextMesh>(_entry, NameTextField);
                return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
            }

            private string GetFactionLabel()
            {
                ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                if (team == null)
                {
                    return string.Empty;
                }

                if (team.FactionIndex == 99)
                {
                    return Localize("Factions/Random/Name");
                }

                IFactionLookup factionLookup = Reflect.Get<IFactionLookup>(_entry, FactionLookupField);
                IFactionDefinition faction = factionLookup != null ? factionLookup.GetFaction(team.FactionIndex) : null;
                return faction != null ? Localize(faction.NameKey) : string.Empty;
            }

            private string GetColorLabel()
            {
                int color = GetTeamColorIndex();
                if (color < 0)
                {
                    return string.Empty;
                }

                TeamColor teamColor = TeamColorExtensions.GetTeamColorFromIndex(color);
                return TeamColorText.Get(teamColor);
            }

            private int GetTeamColorIndex()
            {
                ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                ITeam networkTeam = _adapter != null && _adapter._facade != null && team != null
                    ? _adapter._facade.GetNetworkTeam(team.Id)
                    : null;
                if (networkTeam != null)
                {
                    return networkTeam.Color;
                }

                return _entry != null && _entry.ColorController != null ? _entry.ColorController.Color : -1;
            }

            private string GetStartingWielderLabel()
            {
                ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                if (team == null)
                {
                    return string.Empty;
                }

                if (team.StartingCommander == CommanderReference.Random)
                {
                    return Localize("Factions/Random/Name");
                }

                bool locked = IsVisible(Reflect.Get<Image>(_entry, WielderLockedIconField));
                if (locked)
                {
                    return Localize("Lobby/PlayerSetting/SettingUnknown");
                }

                IWielderLookup wielderLookup = Reflect.Get<IWielderLookup>(_entry, WielderLookupField);
                ICommanderDefinition commander = wielderLookup != null ? wielderLookup.Get(team.StartingCommander) : null;
                return commander != null ? Localize(commander.NameKey) : string.Empty;
            }

            private string GetPartnershipNumber()
            {
                ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                return team != null ? (team.PartnershipIndex + 1).ToString() : string.Empty;
            }

            private string GetAiDifficultyLabel()
            {
                ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                if (team == null || team.AiMode == AiMode.Off)
                {
                    return string.Empty;
                }

                return Localize("Common/AiMode/" + team.AiDifficulty);
            }

            private LobbyButtonItem BuildButton(FieldInfo field)
            {
                UIButton button = Reflect.Get<UIButton>(_entry, field);
                return LobbyButtonItem.ForButton(button, _adapter != null ? _adapter._localization : null);
            }

            private LobbyButtonItem BuildValueButton(FieldInfo field, Func<string> label)
            {
                return BuildValueButton(field, label, null);
            }

            /// <summary>The label is handed over unread: what a setting button draws is a lookup
            /// through the lobby's own team state, and the build only needs the button.</summary>
            private LobbyButtonItem BuildValueButton(FieldInfo field, Func<string> label, Component tooltipComponent)
            {
                UIButton button = Reflect.Get<UIButton>(_entry, field);
                return button != null
                    ? new LobbyButtonItem(button, label, _adapter != null ? _adapter._localization : null, tooltipComponent)
                    : null;
            }

            private LobbyButtonItem BuildTooltipLabelButton(FieldInfo field)
            {
                UIButton button = Reflect.Get<UIButton>(_entry, field);
                return button != null
                    ? new LobbyButtonItem(button, () => GetButtonTooltipLabel(button), _adapter != null ? _adapter._localization : null)
                    : null;
            }

            private string GetButtonTooltipLabel(UIButton button)
            {
                Tooltip tooltip = Tooltip.ForComponent(button, _adapter != null ? _adapter._localization : null);
                if (tooltip != null && tooltip.TextLines != null && tooltip.TextLines.Count > 0)
                {
                    return tooltip.TextLines[0];
                }

                return MenuButtonTextUtility.GetStandardButtonLabel(button);
            }

            private Component GetPrimarySelectableComponent()
            {
                FieldInfo[] fields = PrimarySelectableFields;
                for (int i = 0; i < fields.Length; i++)
                {
                    UIButton button = Reflect.Get<UIButton>(_entry, fields[i]);
                    if (MenuButtonAdapterBase.IsButtonVisible(button))
                    {
                        return button as Component;
                    }
                }

                return _entry as Component;
            }

            private string Localize(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return string.Empty;
                }

                return SpokenLines.Clean(GameText.Get(_adapter != null ? _adapter._localization : null, key, string.Empty));
            }

            private static bool IsVisible(Component component)
            {
                return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
            }

            private static bool IsDrawn(GameObject gameObject)
            {
                return gameObject != null && gameObject.activeInHierarchy;
            }
        }

        public sealed class MultiplayerPanelItem
        {
            private readonly LobbyMultiplayerPanel _panel;
            private readonly ILocalizationHandler _localization;

            public MultiplayerPanelItem(LobbyMultiplayerPanel panel, ILocalizationHandler localization)
            {
                _panel = panel;
                _localization = localization;
            }

            public object SourceKey
            {
                get { return _panel; }
            }

            public bool IsPresent
            {
                get
                {
                    GameObject gameObject = _panel != null ? ((Component)_panel).gameObject : null;
                    return IsLiveSceneObject(gameObject) && gameObject.activeInHierarchy;
                }
            }

            /// <summary>The label the band draws the game's name in, so a caller can key a control
            /// on it and read where it is drawn.</summary>
            public Component GameNameLabel
            {
                get { return Reflect.Get<Component>(_panel, MultiplayerGameNameLabelField); }
            }

            /// <summary>The box the band draws the game code in, likewise.</summary>
            public Component GameCodeField
            {
                get { return Reflect.Get<Component>(_panel, MultiplayerGameCodeInputField); }
            }

            public string GameName
            {
                get { return GetText(Reflect.Get<UITextMesh>(_panel, MultiplayerGameNameLabelField)); }
            }

            public bool IsGameNameVisible
            {
                get { return IsVisibleComponent(Reflect.Get<Component>(_panel, MultiplayerGameNameLabelField)) && !string.IsNullOrWhiteSpace(GameName); }
            }

            public string GameCode
            {
                get
                {
                    UITextMeshInputField field = Reflect.Get<UITextMeshInputField>(_panel, MultiplayerGameCodeInputField);
                    return field != null ? field.InputFieldValue : string.Empty;
                }
            }

            public bool IsGameCodeVisible
            {
                get { return IsVisibleComponent(Reflect.Get<Component>(_panel, MultiplayerGameCodeInputField)) && !string.IsNullOrWhiteSpace(GameCode); }
            }

            public string CopyGameCodeLabel
            {
                get { return ModText.Get(ModStrings.Screens.CopyGameCodeToClipboard, GameCode); }
            }

            public bool CopyGameCodeToClipboard()
            {
                string code = GameCode;
                if (string.IsNullOrWhiteSpace(code))
                {
                    return false;
                }

                GUIUtility.systemCopyBuffer = code;
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Screens.CopiedGameCodeToClipboard), interrupt: false));
                return true;
            }

            public void FocusGameCode()
            {
                UITextMeshInputField field = Reflect.Get<UITextMeshInputField>(_panel, MultiplayerGameCodeInputField);
                NativeSelectionUtility.Select(field != null ? field.GetSelectable() : null);
            }

            public Tooltip GameCodeTooltip
            {
                get { return Tooltip.ForComponent(Reflect.Get<Component>(_panel, MultiplayerGameCodeInputField), _localization); }
            }

            public ToggleItem InvitesOnly
            {
                get
                {
                    UIToggle toggle = Reflect.Get<UIToggle>(_panel, MultiplayerPublicGameToggleField);
                    return toggle != null ? new ToggleItem(toggle, _localization) : null;
                }
            }

            public LobbyButtonItem InviteFriendButton
            {
                get { return LobbyButtonItem.ForButton(Reflect.Get<UIButton>(_panel, MultiplayerInviteFriendButtonField), _localization); }
            }

            public ToggleItem Crossplay
            {
                get
                {
                    UIToggle toggle = Reflect.Get<UIToggle>(_panel, MultiplayerCrossplayToggleField);
                    return toggle != null ? new ToggleItem(toggle, _localization) : null;
                }
            }

            public string XboxCrossplayInformation
            {
                get { return GetText(Reflect.Get<UITextMesh>(_panel, MultiplayerXboxCrossplayInformationField)); }
            }

            public bool IsXboxCrossplayInformationVisible
            {
                get
                {
                    return IsVisibleComponent(Reflect.Get<Component>(_panel, MultiplayerXboxCrossplayInformationField))
                        && !string.IsNullOrWhiteSpace(XboxCrossplayInformation);
                }
            }

            private static string GetText(IUITextMesh textMesh)
            {
                return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
            }
        }

        public sealed class ToggleItem
        {
            private readonly UIToggle _toggle;
            private readonly ILocalizationHandler _localization;

            public ToggleItem(UIToggle toggle, ILocalizationHandler localization)
            {
                _toggle = toggle;
                _localization = localization;
            }

            /// <summary>The drawn toggle, so a caller can key a control on it.</summary>
            public Component Subject
            {
                get { return _toggle as Component; }
            }

            public bool IsVisible
            {
                get { return IsVisibleComponent(_toggle as Component); }
            }

            public bool IsEnabled
            {
                get { return _toggle != null && _toggle.Interactable; }
            }

            public bool IsChecked
            {
                get { return _toggle != null && _toggle.ToggleValue; }
            }

            public string Label
            {
                get { return GetToggleText(_toggle); }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(_toggle as Component, _localization); }
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(_toggle != null ? _toggle.GetSelectable() : null);
            }

            public void Toggle()
            {
                if (_toggle != null && _toggle.Interactable)
                {
                    _toggle.ToggleValue = !_toggle.ToggleValue;
                }
            }
        }

        public sealed class LobbyButtonItem
        {
            private readonly UIButton _button;
            private readonly Func<string> _getLabel;
            private readonly ILocalizationHandler _localization;
            private readonly Component _tooltipComponent;

            public LobbyButtonItem(UIButton button, Func<string> getLabel, ILocalizationHandler localization)
                : this(button, getLabel, localization, null)
            {
            }

            public LobbyButtonItem(UIButton button, Func<string> getLabel, ILocalizationHandler localization, Component tooltipComponent)
            {
                _button = button;
                _getLabel = getLabel;
                _localization = localization;
                _tooltipComponent = tooltipComponent;
            }

            public UIButton Button
            {
                get { return _button; }
            }

            public string Label
            {
                get { return _getLabel != null ? _getLabel() ?? string.Empty : string.Empty; }
            }

            public bool IsVisible
            {
                get { return MenuButtonAdapterBase.IsButtonVisible(_button); }
            }

            public bool IsEnabled
            {
                get { return _button != null && _button.Interactable; }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(_tooltipComponent != null ? _tooltipComponent : _button as Component, _localization); }
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(_button);
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(_button);
            }

            public static LobbyButtonItem ForButton(UIButton button, ILocalizationHandler localization)
            {
                return button != null
                    ? new LobbyButtonItem(button, () => MenuButtonTextUtility.GetStandardButtonLabel(button), localization)
                    : null;
            }
        }

        public sealed class LobbyPlayerSettingsItem
        {
            private readonly UIButton _button;
            private readonly ILocalizationHandler _localization;

            public LobbyPlayerSettingsItem(UIButton button, ILocalizationHandler localization)
            {
                _button = button;
                _localization = localization;
            }

            public bool IsVisible
            {
                get { return MenuButtonAdapterBase.IsButtonVisible(_button); }
            }

            public bool IsEnabled
            {
                get { return _button != null && _button.Interactable; }
            }

            public string Label
            {
                get { return MenuButtonTextUtility.GetStandardButtonLabel(_button); }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(_button as Component, _localization); }
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(_button);
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(_button);
            }
        }

        public sealed class MixedFactionsItem
        {
            private readonly UIToggle _toggle;
            private readonly GameObject _hostContainer;
            private readonly UIButton _clientOnButton;
            private readonly UIButton _clientOffButton;
            private readonly ILocalizationHandler _localization;

            public MixedFactionsItem(
                UIToggle toggle,
                GameObject hostContainer,
                UIButton clientOnButton,
                UIButton clientOffButton,
                ILocalizationHandler localization)
            {
                _toggle = toggle;
                _hostContainer = hostContainer;
                _clientOnButton = clientOnButton;
                _clientOffButton = clientOffButton;
                _localization = localization;
            }

            public bool IsVisible
            {
                get
                {
                    return (_hostContainer != null && _hostContainer.activeInHierarchy)
                        || MenuButtonAdapterBase.IsButtonVisible(_clientOnButton)
                        || MenuButtonAdapterBase.IsButtonVisible(_clientOffButton);
                }
            }

            public bool IsEnabled
            {
                get { return _toggle != null && _toggle.Interactable; }
            }

            public bool IsChecked
            {
                get { return _toggle != null && _toggle.ToggleValue; }
            }

            public string Label
            {
                get { return GetToggleText(_toggle); }
            }

            public Tooltip Tooltip
            {
                get
                {
                    Component component = _toggle as Component;
                    if (component == null || !component.gameObject.activeInHierarchy)
                    {
                        component = _clientOnButton as Component;
                    }

                    if (component == null || !component.gameObject.activeInHierarchy)
                    {
                        component = _clientOffButton as Component;
                    }

                    return Tooltip.ForComponent(component, _localization);
                }
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(_toggle != null ? _toggle.GetSelectable() : null);
            }

            public void Toggle()
            {
                if (_toggle != null && _toggle.Interactable)
                {
                    _toggle.ToggleValue = !_toggle.ToggleValue;
                }
            }
        }

        /// <summary>
        /// A toggle's label as the game DRAWS it. <c>UIToggle.Text</c> answers the string the panel
        /// assigned, which here is the localization token itself ("IsInviteOnly_", "Mixed Factions_",
        /// measured 2026-09-06); the renderer resolves it as it lays the line out, and
        /// <c>GetParsedText</c> is that finished line ("Invites Only Game", "Mixed Factions"). The same
        /// gap the loading screen's tip found for the game's action tokens.
        /// </summary>
        private static string GetToggleText(UIToggle toggle)
        {
            UITextMesh textMesh = toggle != null && ToggleTextMeshField != null
                ? ToggleTextMeshField.GetValue(toggle) as UITextMesh
                : null;
            if (textMesh != null)
            {
                string drawn = textMesh.GetParsedText();
                if (!string.IsNullOrWhiteSpace(drawn))
                {
                    return drawn.Trim();
                }
            }

            return toggle != null ? toggle.Text ?? string.Empty : string.Empty;
        }

        private static bool IsVisibleComponent(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy;
        }
    }
}
