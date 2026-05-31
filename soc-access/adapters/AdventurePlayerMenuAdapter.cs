using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Ai;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common.Economy;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventurePlayerMenuAdapter
    {
        private static readonly FieldInfo AdventureFacadeField =
            AccessTools.Field(typeof(AdventurePlayerMenu), "_adventureFacade");
        private static readonly FieldInfo UiBlockerField =
            AccessTools.Field(typeof(AdventurePlayerMenu), "_uiBlocker");

        private readonly AdventurePlayerMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private int _selectedTeamId = -1;

        public AdventurePlayerMenuAdapter(AdventurePlayerMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(menu, AdventureFacadeField);
            _localization = GlobalLocalizationVariables.LocalizationHandler;
        }

        public AdventurePlayerMenu Source
        {
            get { return _menu; }
        }

        public int SelectedTeamId
        {
            get { return _selectedTeamId; }
            set { _selectedTeamId = value; }
        }

        public bool IsPresent()
        {
            return _menu != null
                && IsLiveSceneObject(((Component)_menu).gameObject)
                && ((Component)_menu).gameObject.activeInHierarchy;
        }

        public string Title
        {
            get { return GetTitle(); }
        }

        public IReadOnlyList<PlayerItem> GetPlayers()
        {
            List<PlayerItem> players = new List<PlayerItem>();
            if (!IsPresent())
            {
                return players;
            }

            AdventurePlayerMenuEntry[] entries =
                ((Component)_menu).GetComponentsInChildren<AdventurePlayerMenuEntry>(includeInactive: false);
            for (int i = 0; i < entries.Length; i++)
            {
                AdventurePlayerMenuEntry entry = entries[i];
                if (entry == null || !IsLiveSceneObject(((Component)entry).gameObject) || !((Component)entry).gameObject.activeInHierarchy)
                {
                    continue;
                }

                players.Add(new PlayerItem(this, entry, i));
            }

            players.Sort((left, right) => left.TeamId.CompareTo(right.TeamId));
            if (_selectedTeamId < 0 && players.Count > 0)
            {
                _selectedTeamId = players[0].TeamId;
            }

            return players;
        }

        public PlayerItem SelectedPlayer
        {
            get
            {
                IReadOnlyList<PlayerItem> players = GetPlayers();
                for (int i = 0; i < players.Count; i++)
                {
                    if (players[i] != null && players[i].TeamId == _selectedTeamId)
                    {
                        return players[i];
                    }
                }

                return players.Count > 0 ? players[0] : null;
            }
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private string GetTitle()
        {
            string nativePlayers = Localize("Common/Players");
            if (_menu != null)
            {
                UITextMesh[] texts = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: false);
                for (int i = 0; i < texts.Length; i++)
                {
                    UITextMesh text = texts[i];
                    if (text == null || text.GetComponentInParent<AdventurePlayerMenuEntry>() != null)
                    {
                        continue;
                    }

                    string candidate = GetText(text);
                    if (!string.IsNullOrWhiteSpace(candidate)
                        && !string.IsNullOrWhiteSpace(nativePlayers)
                        && string.Equals(candidate.Trim(), nativePlayers.Trim(), StringComparison.OrdinalIgnoreCase))
                    {
                        return candidate;
                    }
                }

                for (int i = 0; i < texts.Length; i++)
                {
                    UITextMesh text = texts[i];
                    if (text == null || text.GetComponentInParent<AdventurePlayerMenuEntry>() != null)
                    {
                        continue;
                    }

                    string candidate = GetText(text);
                    if (!string.IsNullOrWhiteSpace(candidate))
                    {
                        return candidate;
                    }
                }
            }

            return nativePlayers;
        }

        private string Localize(string key)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, string.Empty));
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static T GetField<T>(object target, FieldInfo field) where T : class
        {
            if (target == null || field == null)
            {
                return null;
            }

            try
            {
                return field.GetValue(target) as T;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        internal sealed class PlayerItem
        {
            private static readonly FieldInfo TeamField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_teamState");
            private static readonly FieldInfo NameTextField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_nameText");
            private static readonly FieldInfo NameButtonField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_nameButton");
            private static readonly FieldInfo ResourceButtonField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_resourceButton");
            private static readonly FieldInfo ResourcesContainerField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_resourcesContainer");
            private static readonly FieldInfo GoldAmountField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_goldAmount");
            private static readonly FieldInfo GoldIncomeField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_goldIncome");
            private static readonly FieldInfo StoneAmountField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_stoneAmount");
            private static readonly FieldInfo StoneIncomeField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_stoneIncome");
            private static readonly FieldInfo WoodAmountField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_woodAmount");
            private static readonly FieldInfo WoodIncomeField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_woodIncome");
            private static readonly FieldInfo WeaveAmountField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_weaveAmount");
            private static readonly FieldInfo WeaveIncomeField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_weaveIncome");
            private static readonly FieldInfo AmberAmountField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_amberAmount");
            private static readonly FieldInfo AmberIncomeField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_amberIncome");
            private static readonly FieldInfo OreAmountField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_oreAmount");
            private static readonly FieldInfo OreIncomeField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_oreIncome");
            private static readonly FieldInfo TownsButtonField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_townsButton");
            private static readonly FieldInfo NonAggressionPactButtonField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_nonAggressionPactButton");
            private static readonly FieldInfo SpectateBattleButtonField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_spectateBattleButton");
            private static readonly FieldInfo PartnershipIdField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_partnershipId");
            private static readonly FieldInfo AiContainerField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_aiContainer");
            private static readonly FieldInfo ScoreContainerField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_scoreContainer");
            private static readonly FieldInfo ScoreTextField =
                AccessTools.Field(typeof(AdventurePlayerMenuEntry), "_scoreText");
            private static readonly FieldInfo NonAggressionPactInnerButtonField =
                AccessTools.Field(typeof(NonAggressionPactButton), "_button");

            private readonly AdventurePlayerMenuAdapter _adapter;
            private readonly AdventurePlayerMenuEntry _entry;
            private readonly int _index;

            public PlayerItem(AdventurePlayerMenuAdapter adapter, AdventurePlayerMenuEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                _index = index;
            }

            public int TeamId
            {
                get
                {
                    ITeamState team = Team;
                    return team != null ? team.Id : _index;
                }
            }

            public string Id
            {
                get { return "adventure-player-" + Math.Max(TeamId, 0); }
            }

            public string Name
            {
                get { return GetText(GetField<UITextMesh>(_entry, NameTextField)); }
            }

            public string TeamLabel
            {
                get
                {
                    string teamNumber = GetText(GetField<UITextMesh>(_entry, PartnershipIdField));
                    return !string.IsNullOrWhiteSpace(teamNumber)
                        ? ModText.Get(ModStrings.Screens.TeamValue, teamNumber)
                        : string.Empty;
                }
            }

            public string ColorLabel
            {
                get
                {
                    ITeamState team = Team;
                    if (team == null)
                    {
                        return string.Empty;
                    }

                    TeamColor teamColor = TeamColorExtensions.GetTeamColorFromIndex(team.Color);
                    return TeamColorText.Get(teamColor);
                }
            }

            public string RelationLabel
            {
                get
                {
                    ITeamState team = Team;
                    IClientAdventureFacade facade = _adapter != null ? _adapter._facade : null;
                    if (team == null || facade == null || facade.Teams == null || team.Id == facade.Teams.LocalTeamInControlId)
                    {
                        return string.Empty;
                    }

                    return facade.Teams.IsInPartnership(facade.Teams.LocalTeamInControlId, team.Id)
                        ? ModText.Get(ModStrings.Spatial.Friendly)
                        : ModText.Get(ModStrings.Spatial.Enemy);
                }
            }

            public string AiLabel
            {
                get
                {
                    ITeamState team = Team;
                    GameObject container = GetField<GameObject>(_entry, AiContainerField);
                    if (team == null || team.AiMode == AiMode.Off || container == null || !container.activeInHierarchy)
                    {
                        return string.Empty;
                    }

                    string difficulty = SpeechTextSanitizer.Normalize(GameText.Get(_adapter != null ? _adapter._localization : null, "Common/AiMode/" + team.AiDifficulty, string.Empty));
                    return string.IsNullOrWhiteSpace(difficulty)
                        ? string.Empty
                        : ModText.Get(ModStrings.Screens.AiDifficulty, difficulty);
                }
            }

            public string ScoreText
            {
                get
                {
                    GameObject container = GetField<GameObject>(_entry, ScoreContainerField);
                    if (container == null || !container.activeInHierarchy)
                    {
                        return string.Empty;
                    }

                    return GetText(GetField<UITextMesh>(_entry, ScoreTextField));
                }
            }

            public bool HasResourceSummary
            {
                get
                {
                    ITeamState team = Team;
                    IClientAdventureFacade facade = _adapter != null ? _adapter._facade : null;
                    return team != null
                        && facade != null
                        && facade.Teams != null
                        && team.IsAlive
                        && team.Id != facade.Teams.LocalTeamInControlId
                        && facade.Teams.IsInPartnership(facade.Teams.LocalTeamInControlId, team.Id);
                }
            }

            public string GetResourceLabel(ResourceType resourceType)
            {
                string name = SpeechTextSanitizer.Normalize(GameText.Get(_adapter != null ? _adapter._localization : null, "Common/Resource/" + resourceType, string.Empty));
                string amount = GetText(GetResourceAmountText(resourceType));
                string income = IsGameObjectVisible(GetResourceIncomeText(resourceType))
                    ? GetText(GetResourceIncomeText(resourceType))
                    : string.Empty;

                string label = string.IsNullOrWhiteSpace(amount) ? name : name + " " + amount;
                return string.IsNullOrWhiteSpace(income) ? label : label + ", income " + income;
            }

            public void FocusResource(ResourceType resourceType)
            {
                Component component = GetResourceTooltipComponent(resourceType);
                if (component != null)
                {
                    NativeSelectionUtility.Select(component);
                }
            }

            public Tooltip GetResourceTooltip(ResourceType resourceType)
            {
                return Tooltip.ForComponent(GetResourceTooltipComponent(resourceType), _adapter != null ? _adapter._localization : null);
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(GetPrimaryTooltipComponent(), _adapter != null ? _adapter._localization : null); }
            }

            public ActionItem PlatformActions
            {
                get
                {
                    UIButton button = GetField<UIButton>(_entry, NameButtonField);
                    return BuildAction(
                        "platform-actions",
                        button,
                        () => SpeechTextSanitizer.Normalize(GameText.Get(_adapter != null ? _adapter._localization : null, "Lobby/LobbyPlayerMenu/ShowPlayerActions", string.Empty)));
                }
            }

            public ActionItem Resources
            {
                get { return BuildAction("resources", GetField<UIButton>(_entry, ResourceButtonField), null); }
            }

            public ActionItem Towns
            {
                get { return BuildAction("towns", GetField<UIButton>(_entry, TownsButtonField), null); }
            }

            public ActionItem NonAggressionPact
            {
                get
                {
                    NonAggressionPactButton pactButton = GetField<NonAggressionPactButton>(_entry, NonAggressionPactButtonField);
                    UIButton button = pactButton != null
                        ? GetField<UIButton>(pactButton, NonAggressionPactInnerButtonField)
                        : null;
                    return BuildAction(
                        "non-aggression-pact",
                        button,
                        () => SpeechTextSanitizer.Normalize(GameText.Get(_adapter != null ? _adapter._localization : null, "Adventure/NonAggressionPact/TooltipTitle", string.Empty)));
                }
            }

            public ActionItem SpectateBattle
            {
                get { return BuildAction("spectate-battle", GetField<UIButton>(_entry, SpectateBattleButtonField), null); }
            }

            public void FocusNative()
            {
                if (_entry != null && _entry.Selectable != null)
                {
                    NativeSelectionUtility.Select(_entry.Selectable);
                    return;
                }

                Component component = GetPrimaryTooltipComponent();
                if (component != null)
                {
                    NativeSelectionUtility.Select(component);
                }
            }

            private ITeamState Team
            {
                get { return GetField<ITeamState>(_entry, TeamField); }
            }

            private UITextMesh GetResourceAmountText(ResourceType resourceType)
            {
                return GetField<UITextMesh>(_entry, GetResourceAmountField(resourceType));
            }

            private UITextMesh GetResourceIncomeText(ResourceType resourceType)
            {
                return GetField<UITextMesh>(_entry, GetResourceIncomeField(resourceType));
            }

            private Component GetResourceTooltipComponent(ResourceType resourceType)
            {
                UITextMesh amountText = GetResourceAmountText(resourceType);
                if (IsGameObjectVisible(amountText))
                {
                    return amountText as Component;
                }

                UITextMesh incomeText = GetResourceIncomeText(resourceType);
                return IsGameObjectVisible(incomeText) ? incomeText as Component : null;
            }

            private static FieldInfo GetResourceAmountField(ResourceType resourceType)
            {
                switch (resourceType)
                {
                    case ResourceType.Gold:
                        return GoldAmountField;
                    case ResourceType.Stone:
                        return StoneAmountField;
                    case ResourceType.Wood:
                        return WoodAmountField;
                    case ResourceType.Glimmerweave:
                        return WeaveAmountField;
                    case ResourceType.AncientAmber:
                        return AmberAmountField;
                    case ResourceType.CelestialOre:
                        return OreAmountField;
                    default:
                        return null;
                }
            }

            private static FieldInfo GetResourceIncomeField(ResourceType resourceType)
            {
                switch (resourceType)
                {
                    case ResourceType.Gold:
                        return GoldIncomeField;
                    case ResourceType.Stone:
                        return StoneIncomeField;
                    case ResourceType.Wood:
                        return WoodIncomeField;
                    case ResourceType.Glimmerweave:
                        return WeaveIncomeField;
                    case ResourceType.AncientAmber:
                        return AmberIncomeField;
                    case ResourceType.CelestialOre:
                        return OreIncomeField;
                    default:
                        return null;
                }
            }

            private static bool IsGameObjectVisible(Component component)
            {
                return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
            }

            private ActionItem BuildAction(string idSuffix, UIButton button, Func<string> fallbackLabel)
            {
                return button != null
                    ? new ActionItem(_adapter, Id + "-" + idSuffix, button, fallbackLabel)
                    : null;
            }

            private Component GetPrimaryTooltipComponent()
            {
                Component[] components =
                {
                    GetField<UIButton>(_entry, NameButtonField) as Component,
                    GetField<UIButton>(_entry, ResourceButtonField) as Component,
                    GetField<UIButton>(_entry, TownsButtonField) as Component,
                    GetField<UIButton>(_entry, SpectateBattleButtonField) as Component
                };

                for (int i = 0; i < components.Length; i++)
                {
                    UIButton button = components[i] as UIButton;
                    if (MenuButtonAdapterBase.IsButtonVisible(button))
                    {
                        return components[i];
                    }
                }

                return _entry as Component;
            }
        }

        internal sealed class ActionItem
        {
            private readonly AdventurePlayerMenuAdapter _adapter;
            private readonly UIButton _button;
            private readonly Func<string> _fallbackLabel;

            public ActionItem(AdventurePlayerMenuAdapter adapter, string id, UIButton button, Func<string> fallbackLabel)
            {
                _adapter = adapter;
                Id = id ?? string.Empty;
                _button = button;
                _fallbackLabel = fallbackLabel;
            }

            public string Id { get; private set; }

            public string Label
            {
                get
                {
                    Tooltip tooltip = Tooltip;
                    if (tooltip != null && tooltip.TextLines.Count > 0)
                    {
                        return SpeechTextSanitizer.Normalize(tooltip.TextLines[0]);
                    }

                    string buttonText = MenuButtonTextUtility.GetStandardButtonLabel(_button);
                    if (!string.IsNullOrWhiteSpace(buttonText))
                    {
                        return buttonText;
                    }

                    return _fallbackLabel != null ? _fallbackLabel() ?? string.Empty : string.Empty;
                }
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
                get { return Tooltip.ForComponent(_button as Component, _adapter != null ? _adapter._localization : null); }
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
    }
}
