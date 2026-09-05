using System;
using System.Collections.Generic;
using System.Linq;
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
    public sealed class AdventureLobbyPlayersAdapter
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

        private static readonly FieldInfo LobbyButtonsSettingsField =
            AccessTools.Field(typeof(LobbyMenuButtonsInstaller), "_settings");
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

        private readonly LobbyMenu _menu;
        private readonly LobbyNavigation _navigation;
        private readonly IClientLobbyFacade _facade;
        private readonly ILocalizationHandler _localization;
        private List<PlayerSlotItem> _playerSlots;
        private LobbyMapSettings _mapSettings;
        private LobbyMenuButtons.Settings _lobbyButtonsSettings;
        private MultiplayerPanelItem _multiplayerPanel;

        public AdventureLobbyPlayersAdapter(LobbyMenu menu)
        {
            _menu = menu;
            _facade = menu != null ? LobbyFacadeRef(menu) : null;
            _localization = menu != null ? LocalizationRef(menu) : GlobalLocalizationVariables.LocalizationHandler;
            _navigation = FindNavigationFor(menu);
            BackButton = CreateBackButton();
            OptionsButton = CreateOptionsButton();
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

        public int SelectedTeamId { get; set; } = -1;

        public void InvalidateSnapshot()
        {
            _playerSlots = null;
            _mapSettings = null;
            _lobbyButtonsSettings = null;
            _multiplayerPanel = null;
        }

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

        public string MapSummary
        {
            get
            {
                LobbyMapPreview preview = _menu != null ? MapPreviewRef(_menu) : null;
                return LobbyMapPreviewText.GetSummary(preview);
            }
        }

        public IReadOnlyList<PlayerSlotItem> GetPlayerSlots()
        {
            if (_playerSlots != null)
            {
                return _playerSlots;
            }

            List<PlayerSlotItem> slots = new List<PlayerSlotItem>();
            LobbyPlayerEntry[] entries = Resources.FindObjectsOfTypeAll<LobbyPlayerEntry>();
            for (int i = 0; i < entries.Length; i++)
            {
                LobbyPlayerEntry entry = entries[i];
                if (entry == null || !IsLiveSceneObject(((Component)entry).gameObject) || !((Component)entry).gameObject.activeInHierarchy)
                {
                    continue;
                }

                slots.Add(new PlayerSlotItem(this, entry));
            }

            slots.Sort((left, right) => left.TeamId.CompareTo(right.TeamId));
            if (SelectedTeamId < 0 && slots.Count > 0)
            {
                SelectedTeamId = slots[0].TeamId;
            }

            _playerSlots = slots;
            return _playerSlots;
        }

        public PlayerSlotItem SelectedSlot
        {
            get
            {
                IReadOnlyList<PlayerSlotItem> slots = GetPlayerSlots();
                for (int i = 0; i < slots.Count; i++)
                {
                    if (slots[i] != null && slots[i].TeamId == SelectedTeamId)
                    {
                        return slots[i];
                    }
                }

                return slots.Count > 0 ? slots[0] : null;
            }
        }

        public LobbyPlayerSettingsItem GetSettingsItem()
        {
            LobbyMapSettings settings = FindMapSettings();
            UIButton button = settings != null && MapSettingsChangeButtonField != null
                ? MapSettingsChangeButtonField.GetValue(settings) as UIButton
                : null;
            return button != null ? new LobbyPlayerSettingsItem(button, _localization) : null;
        }

        public MixedFactionsItem GetMixedFactionsItem()
        {
            LobbyMapSettings settings = FindMapSettings();
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
            return toggle != null ? new MixedFactionsItem(settings, toggle, container, onButton, offButton, _localization) : null;
        }

        public LobbyButtonItem GetSetReadyButton()
        {
            LobbyMenuButtons.Settings settings = GetLobbyButtonsSettings();
            return settings != null ? LobbyButtonItem.ForButton(settings.SetReadyButton, _localization) : null;
        }

        public LobbyButtonItem GetSetNotReadyButton()
        {
            LobbyMenuButtons.Settings settings = GetLobbyButtonsSettings();
            return settings != null ? LobbyButtonItem.ForButton(settings.SetNotReadyButton, _localization) : null;
        }

        public LobbyButtonItem GetStartGameButton()
        {
            LobbyMenuButtons.Settings settings = GetLobbyButtonsSettings();
            return settings != null ? LobbyButtonItem.ForButton(settings.StartGameButton, _localization) : null;
        }

        public MultiplayerPanelItem GetMultiplayerPanel()
        {
            if (_multiplayerPanel != null && _multiplayerPanel.IsPresent)
            {
                return _multiplayerPanel;
            }

            LobbyMultiplayerPanel panel = FindMultiplayerPanel();
            _multiplayerPanel = panel != null ? new MultiplayerPanelItem(panel, _localization) : null;
            return _multiplayerPanel;
        }

        public Tooltip GetButtonTooltip(IMenuButtonAdapter button)
        {
            return button != null ? Tooltip.ForComponent(button.Button as Component, _localization) : null;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private LobbyMenuButtons.Settings GetLobbyButtonsSettings()
        {
            if (_lobbyButtonsSettings != null)
            {
                return _lobbyButtonsSettings;
            }

            LobbyMenuButtonsInstaller[] installers = Resources.FindObjectsOfTypeAll<LobbyMenuButtonsInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                LobbyMenuButtonsInstaller installer = installers[i];
                if (installer == null || !IsLiveSceneObject(((Component)installer).gameObject))
                {
                    continue;
                }

                _lobbyButtonsSettings = LobbyButtonsSettingsField != null
                    ? LobbyButtonsSettingsField.GetValue(installer) as LobbyMenuButtons.Settings
                    : null;
                return _lobbyButtonsSettings;
            }

            return null;
        }

        private LobbyMapSettings FindMapSettings()
        {
            if (_mapSettings != null && IsLiveSceneObject(((Component)_mapSettings).gameObject))
            {
                return _mapSettings;
            }

            LobbyMapSettings[] settings = Resources.FindObjectsOfTypeAll<LobbyMapSettings>();
            for (int i = 0; i < settings.Length; i++)
            {
                LobbyMapSettings item = settings[i];
                if (item != null && IsLiveSceneObject(((Component)item).gameObject))
                {
                    _mapSettings = item;
                    return _mapSettings;
                }
            }

            return null;
        }

        private LobbyMultiplayerPanel FindMultiplayerPanel()
        {
            GameObject menuObject = _menu != null ? ((Component)_menu).gameObject : null;
            LobbyMultiplayerPanel[] panels = Resources.FindObjectsOfTypeAll<LobbyMultiplayerPanel>();
            for (int i = 0; i < panels.Length; i++)
            {
                LobbyMultiplayerPanel panel = panels[i];
                if (panel == null)
                {
                    continue;
                }

                GameObject panelObject = ((Component)panel).gameObject;
                if (!IsLiveSceneObject(panelObject) || !panelObject.activeInHierarchy)
                {
                    continue;
                }

                if (menuObject != null && panelObject.scene != menuObject.scene)
                {
                    continue;
                }

                return panel;
            }

            return null;
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
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback ?? string.Empty));
        }

        private static LobbyNavigation FindNavigationFor(LobbyMenu menu)
        {
            if (menu == null)
            {
                return null;
            }

            GameObject menuObject = ((Component)menu).gameObject;
            LobbyNavigation[] navigations = Resources.FindObjectsOfTypeAll<LobbyNavigation>();
            for (int i = 0; i < navigations.Length; i++)
            {
                LobbyNavigation navigation = navigations[i];
                if (navigation == null)
                {
                    continue;
                }

                GameObject navigationObject = ((Component)navigation).gameObject;
                if (IsLiveSceneObject(navigationObject) && navigationObject.scene == menuObject.scene)
                {
                    return navigation;
                }
            }

            return null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
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

            public string Id
            {
                get { return "lobby-player-slot-" + Math.Max(TeamId + 1, 0); }
            }

            public string Label
            {
                get { return BuildLabel(); }
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
                get { return BuildValueButton(SetFactionButtonField, GetFactionLabel(), GetField<Component>(_entry, FactionIconImageField)); }
            }

            public LobbyButtonItem ColorButton
            {
                get { return BuildValueButton(SetColorButtonField, GetColorLabel()); }
            }

            public LobbyButtonItem StartingWielderButton
            {
                get { return BuildValueButton(SetStartingWielderButtonField, GetStartingWielderLabel()); }
            }

            public LobbyButtonItem PartnershipButton
            {
                get { return BuildValueButton(SetPartnershipButtonField, ModText.Get(ModStrings.Screens.TeamValue, GetPartnershipNumber()), GetField<Component>(_entry, PartnershipTransformField)); }
            }

            public LobbyButtonItem AiDifficultyButton
            {
                get { return BuildValueButton(SetAiButtonField, GetAiDifficultyLabel()); }
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
                    GameObject container = GetField<GameObject>(_entry, DlcNeededContainerField);
                    if (container == null || !container.activeInHierarchy)
                    {
                        return string.Empty;
                    }

                    UIButton button = GetField<UIButton>(_entry, DlcNeededButtonField);
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

            private string BuildLabel()
            {
                List<string> parts = new List<string>();
                AddIfNotEmpty(parts, ModText.Get(ModStrings.UI.Slot, TeamId + 1));
                AddIfNotEmpty(parts, GetName());
                if (!IsOccupied)
                {
                    return ModText.JoinListWithCommas(parts);
                }

                AddIfNotEmpty(parts, GetFactionLabel());
                AddIfNotEmpty(parts, GetColorLabel());
                AddIfNotEmpty(parts, GetStartingWielderLabel());
                AddIfNotEmpty(parts, ModText.Get(ModStrings.Screens.TeamValue, GetPartnershipNumber()));
                AddIfNotEmpty(parts, GetAiDifficultyLabel());
                AddIfNotEmpty(parts, GetReadyStatusLabel());
                return ModText.JoinListWithCommas(parts);
            }

            private bool IsOccupied
            {
                get
                {
                    ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                    return team != null
                        && ((team.AiMode != AiMode.Off)
                            || (_adapter != null && _adapter._facade != null && _adapter._facade.HasClient(team.Id)));
                }
            }

            private string GetReadyStatusLabel()
            {
                ILobbyTeamState team = _entry != null ? _entry.LobbyTeamState : null;
                if (team == null || _adapter == null || _adapter.GetMultiplayerPanel() == null)
                {
                    return string.Empty;
                }

                return team.IsReadyToStart
                    ? ModText.Get(ModStrings.Screens.Ready)
                    : ModText.Get(ModStrings.Screens.NotReady);
            }

            private string GetName()
            {
                UITextMesh text = GetField<UITextMesh>(_entry, NameTextField);
                return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
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

                IFactionLookup factionLookup = GetInjectedField<IFactionLookup>(_entry, "_factionLookup");
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

                bool locked = IsVisible(GetField<Image>(_entry, WielderLockedIconField));
                if (locked)
                {
                    return Localize("Lobby/PlayerSetting/SettingUnknown");
                }

                IWielderLookup wielderLookup = GetInjectedField<IWielderLookup>(_entry, "_wielderLookup");
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
                UIButton button = GetField<UIButton>(_entry, field);
                return LobbyButtonItem.ForButton(button, _adapter != null ? _adapter._localization : null);
            }

            private LobbyButtonItem BuildValueButton(FieldInfo field, string label)
            {
                return BuildValueButton(field, label, null);
            }

            private LobbyButtonItem BuildValueButton(FieldInfo field, string label, Component tooltipComponent)
            {
                UIButton button = GetField<UIButton>(_entry, field);
                return button != null
                    ? new LobbyButtonItem(button, () => label, _adapter != null ? _adapter._localization : null, tooltipComponent)
                    : null;
            }

            private LobbyButtonItem BuildTooltipLabelButton(FieldInfo field)
            {
                UIButton button = GetField<UIButton>(_entry, field);
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
                FieldInfo[] fields =
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

                for (int i = 0; i < fields.Length; i++)
                {
                    UIButton button = GetField<UIButton>(_entry, fields[i]);
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

                return SpeechTextSanitizer.Normalize(GameText.Get(_adapter != null ? _adapter._localization : null, key, string.Empty));
            }

            private static bool IsVisible(Component component)
            {
                return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
            }

            private static T GetInjectedField<T>(object owner, string fieldName) where T : class
            {
                FieldInfo field = AccessTools.Field(owner != null ? owner.GetType() : null, fieldName);
                return owner != null && field != null ? field.GetValue(owner) as T : null;
            }

            private static T GetField<T>(object owner, FieldInfo field) where T : class
            {
                return owner != null && field != null ? field.GetValue(owner) as T : null;
            }

            private static void AddIfNotEmpty(List<string> parts, string value)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    parts.Add(value);
                }
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

            public string GameName
            {
                get { return GetText(GetField<UITextMesh>(MultiplayerGameNameLabelField)); }
            }

            public bool IsGameNameVisible
            {
                get { return IsVisible(GetField<Component>(MultiplayerGameNameLabelField)) && !string.IsNullOrWhiteSpace(GameName); }
            }

            public string GameCode
            {
                get
                {
                    UITextMeshInputField field = GetField<UITextMeshInputField>(MultiplayerGameCodeInputField);
                    return field != null ? field.InputFieldValue : string.Empty;
                }
            }

            public bool IsGameCodeVisible
            {
                get { return IsVisible(GetField<Component>(MultiplayerGameCodeInputField)) && !string.IsNullOrWhiteSpace(GameCode); }
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
                UITextMeshInputField field = GetField<UITextMeshInputField>(MultiplayerGameCodeInputField);
                NativeSelectionUtility.Select(field != null ? field.GetSelectable() : null);
            }

            public Tooltip GameCodeTooltip
            {
                get { return Tooltip.ForComponent(GetField<Component>(MultiplayerGameCodeInputField), _localization); }
            }

            public ToggleItem InvitesOnly
            {
                get
                {
                    UIToggle toggle = GetField<UIToggle>(MultiplayerPublicGameToggleField);
                    return toggle != null ? new ToggleItem(toggle, _localization) : null;
                }
            }

            public LobbyButtonItem InviteFriendButton
            {
                get { return LobbyButtonItem.ForButton(GetField<UIButton>(MultiplayerInviteFriendButtonField), _localization); }
            }

            public ToggleItem Crossplay
            {
                get
                {
                    UIToggle toggle = GetField<UIToggle>(MultiplayerCrossplayToggleField);
                    return toggle != null ? new ToggleItem(toggle, _localization) : null;
                }
            }

            public string XboxCrossplayInformation
            {
                get { return GetText(GetField<UITextMesh>(MultiplayerXboxCrossplayInformationField)); }
            }

            public bool IsXboxCrossplayInformationVisible
            {
                get
                {
                    return IsVisible(GetField<Component>(MultiplayerXboxCrossplayInformationField))
                        && !string.IsNullOrWhiteSpace(XboxCrossplayInformation);
                }
            }

            private T GetField<T>(FieldInfo field) where T : class
            {
                return _panel != null && field != null ? field.GetValue(_panel) as T : null;
            }

            private static string GetText(IUITextMesh textMesh)
            {
                return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
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
                get { return SpeechTextSanitizer.Normalize(_toggle != null ? _toggle.Text : string.Empty); }
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
                LobbyMapSettings settings,
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
                get { return SpeechTextSanitizer.Normalize(_toggle != null ? _toggle.Text : string.Empty); }
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

        private static bool IsVisible(Component component)
        {
            return IsVisibleComponent(component);
        }

        private static bool IsVisibleComponent(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.activeInHierarchy;
        }
    }
}
