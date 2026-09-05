using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PostAdventureStatsAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(PostAdventureStatsMenu), "_settings");
        private static readonly FieldInfo DropdownField = AccessTools.Field(typeof(UITextMeshDropdown), "_dropdown");
        private static readonly FieldInfo GraphTitleTextField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "TitleText");
        private static readonly FieldInfo TotalRoundsTextField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "TotalRoundsText");
        private static readonly FieldInfo TotalPlayTimeTextField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "TotalPlayTimeText");
        private static readonly FieldInfo TeamEntriesField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "_spawnedTeamEntries");
        private static readonly FieldInfo TeamNameTextField = AccessTools.Field(typeof(PostAdventureStatsMenuTeamEntry), "_teamNameText");
        private static readonly FieldInfo TeamToggleField = AccessTools.Field(typeof(PostAdventureStatsMenuTeamEntry), "_enabledToggle");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "_facade");

        private readonly PostAdventureStatsMenu _menu;

        public PostAdventureStatsAdapter(PostAdventureStatsMenu menu)
        {
            _menu = menu;
        }

        public string Header
        {
            get { return GetText(Settings != null ? Settings.HeaderText : null); }
        }

        public string GraphTitle
        {
            get { return GetText(GetField<UITextMesh>(GraphView, GraphTitleTextField)); }
        }

        public string TotalRounds
        {
            get { return GetText(GetField<UITextMesh>(GraphView, TotalRoundsTextField)); }
        }

        public string TotalPlayTime
        {
            get { return GetText(GetField<UITextMesh>(GraphView, TotalPlayTimeTextField)); }
        }

        public bool IsPresent()
        {
            PostAdventureStatsMenu.Settings settings = Settings;
            return _menu != null
                && settings != null
                && settings.ContainerCanvasGroup != null
                && settings.ContainerCanvasGroup.gameObject != null
                && settings.ContainerCanvasGroup.gameObject.activeInHierarchy
                && _menu.Active;
        }

        public bool IsReadyAfterAnimation()
        {
            PostAdventureStatsMenu.Settings settings = Settings;
            return IsPresent()
                && settings.ContainerCanvasGroup.alpha >= 0.95f
                && GraphDropdown != null
                && GetGraphOptions().Count > 0;
        }

        public IReadOnlyList<GraphOption> GetGraphOptions()
        {
            UITextMeshDropdown dropdown = GraphDropdown;
            if (dropdown == null)
            {
                return new GraphOption[0];
            }

            EnsureDropdownInitialized(dropdown);
            TMP_Dropdown nativeDropdown = GetField<TMP_Dropdown>(dropdown, DropdownField);
            if (nativeDropdown == null || nativeDropdown.options == null)
            {
                return new GraphOption[0];
            }

            List<GraphOption> options = new List<GraphOption>(nativeDropdown.options.Count);
            for (int i = 0; i < nativeDropdown.options.Count; i++)
            {
                string label = SpeechTextSanitizer.Normalize(nativeDropdown.options[i].text);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                options.Add(new GraphOption("post-adventure-stats-graph-" + i, i, label));
            }

            return options.ToArray();
        }

        public int SelectedGraphIndex
        {
            get { return GraphDropdown != null ? GraphDropdown.DropdownValue : -1; }
        }

        public bool SelectGraph(int index)
        {
            UITextMeshDropdown dropdown = GraphDropdown;
            if (dropdown == null || index < 0 || index >= dropdown.DropdownValueCount)
            {
                return false;
            }

            if (dropdown.DropdownValue == index)
            {
                return true;
            }

            string expectedTitle = GetGraphOptionLabel(index);
            dropdown.DropdownValue = index;
            if (!string.Equals(GraphTitle, expectedTitle, StringComparison.Ordinal) && dropdown.OnDropdownValueChanged != null)
            {
                dropdown.OnDropdownValueChanged.Invoke();
            }

            return true;
        }

        public bool FocusGraphDropdown()
        {
            UITextMeshDropdown dropdown = GraphDropdown;
            return dropdown != null && NativeSelectionUtility.Select(dropdown.GetSelectable());
        }

        public IReadOnlyList<TeamOption> GetTeamOptions()
        {
            List<PostAdventureStatsMenuTeamEntry> entries = GetTeamEntries();
            List<TeamOption> options = new List<TeamOption>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                PostAdventureStatsMenuTeamEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string label = GetTeamLabel(entry);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                options.Add(new TeamOption("post-adventure-stats-team-" + i, entry, label));
            }

            return options.ToArray();
        }

        public bool ToggleTeam(PostAdventureStatsMenuTeamEntry entry)
        {
            UIToggle toggle = GetTeamToggle(entry);
            if (toggle == null)
            {
                return false;
            }

            toggle.ToggleValue = !toggle.ToggleValue;
            return true;
        }

        public bool IsTeamSelected(PostAdventureStatsMenuTeamEntry entry)
        {
            return entry != null && entry.IsGraphEnabled;
        }

        public bool FocusTeam(PostAdventureStatsMenuTeamEntry entry)
        {
            UIToggle toggle = GetTeamToggle(entry);
            return toggle != null && NativeSelectionUtility.Select(toggle.GetSelectable());
        }

        public string GetCloseButtonLabel()
        {
            string label = MenuButtonTextUtility.GetStandardButtonLabel(Settings != null ? Settings.CloseButton : null);
            return !string.IsNullOrWhiteSpace(label) ? label : ModText.Get(ModStrings.Screens.Close);
        }

        public bool IsCloseButtonEnabled()
        {
            UIButton button = Settings != null ? Settings.CloseButton : null;
            return button != null && button.Active && button.Interactable;
        }

        public bool Close()
        {
            return NativeSelectionUtility.Click(Settings != null ? Settings.CloseButton : null);
        }

        public IReadOnlyList<GraphTeamColumn> GetEnabledGraphTeams()
        {
            List<GraphTeamColumn> teams = new List<GraphTeamColumn>();
            IReadOnlyList<TeamOption> options = GetTeamOptions();
            for (int i = 0; i < options.Count; i++)
            {
                TeamOption option = options[i];
                if (option == null || option.Entry == null || option.Entry.Team == null || !IsTeamSelected(option.Entry))
                {
                    continue;
                }

                teams.Add(new GraphTeamColumn("team-" + option.Entry.Team.Id, option.Entry.Team.Id, option.Label));
            }

            return teams.ToArray();
        }

        public IReadOnlyList<GraphRoundRow> GetGraphRows()
        {
            Dictionary<int, Dictionary<int, GraphPoint>> values = BuildGraphValues();
            SortedSet<int> rounds = new SortedSet<int>(values.Keys);
            List<GraphRoundRow> rows = new List<GraphRoundRow>();
            foreach (int round in rounds)
            {
                rows.Add(new GraphRoundRow("round-" + round, round, values[round]));
            }

            return rows.ToArray();
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private PostAdventureStatsMenu.Settings Settings
        {
            get { return GetField<PostAdventureStatsMenu.Settings>(_menu, SettingsField); }
        }

        private PostAdventureStatsMenuGraphView GraphView
        {
            get { return Settings != null ? Settings.GraphView : null; }
        }

        private IClientAdventureFacade Facade
        {
            get { return GetField<IClientAdventureFacade>(GraphView, FacadeField); }
        }

        private UITextMeshDropdown GraphDropdown
        {
            get { return GraphView != null ? GraphView.GraphDropdown : null; }
        }

        private static void EnsureDropdownInitialized(UITextMeshDropdown dropdown)
        {
            if (dropdown != null)
            {
                int unused = dropdown.DropdownValueCount;
            }
        }

        private string GetGraphOptionLabel(int index)
        {
            IReadOnlyList<GraphOption> options = GetGraphOptions();
            for (int i = 0; i < options.Count; i++)
            {
                if (options[i].Index == index)
                {
                    return options[i].Label;
                }
            }

            return string.Empty;
        }

        private static List<PostAdventureStatsMenuTeamEntry> GetTeamEntries(PostAdventureStatsMenuGraphView graphView)
        {
            return GetField<List<PostAdventureStatsMenuTeamEntry>>(graphView, TeamEntriesField)
                ?? new List<PostAdventureStatsMenuTeamEntry>();
        }

        private List<PostAdventureStatsMenuTeamEntry> GetTeamEntries()
        {
            return GetTeamEntries(GraphView);
        }

        private static string GetTeamLabel(PostAdventureStatsMenuTeamEntry entry)
        {
            return GetText(GetField<UITextMesh>(entry, TeamNameTextField));
        }

        private static UIToggle GetTeamToggle(PostAdventureStatsMenuTeamEntry entry)
        {
            return GetField<UIToggle>(entry, TeamToggleField);
        }

        private Dictionary<int, Dictionary<int, GraphPoint>> BuildGraphValues()
        {
            Dictionary<int, Dictionary<int, GraphPoint>> values = new Dictionary<int, Dictionary<int, GraphPoint>>();
            IClientAdventureFacade facade = Facade;
            if (facade == null || facade.Teams == null)
            {
                return values;
            }

            PostAdventureStatsGraphType graphType = GetSelectedGraphType();
            ITeamState[] teams = facade.Teams.All;
            for (int i = 0; teams != null && i < teams.Length; i++)
            {
                ITeamState team = teams[i];
                if (team == null || team.GetIsNeutral())
                {
                    continue;
                }

                TeamStatisticsRoundState[] rounds = team.Statistics != null ? team.Statistics.All : null;
                for (int j = 0; rounds != null && j < rounds.Length; j++)
                {
                    TeamStatisticsRoundState round = rounds[j];
                    if (round.Round == 0)
                    {
                        continue;
                    }

                    int? value = GetGraphValue(round, graphType);
                    if (!value.HasValue)
                    {
                        continue;
                    }

                    Dictionary<int, GraphPoint> roundValues;
                    if (!values.TryGetValue(round.Round, out roundValues))
                    {
                        roundValues = new Dictionary<int, GraphPoint>();
                        values[round.Round] = roundValues;
                    }

                    roundValues[team.Id] = new GraphPoint(value.Value, round.LostBattles > 0);
                }
            }

            return values;
        }

        private PostAdventureStatsGraphType GetSelectedGraphType()
        {
            Array values = Enum.GetValues(typeof(PostAdventureStatsGraphType));
            int selected = SelectedGraphIndex;
            if (selected < 0 || selected >= values.Length)
            {
                return PostAdventureStatsGraphType.ArmyValue;
            }

            return (PostAdventureStatsGraphType)values.GetValue(selected);
        }

        private static int? GetGraphValue(TeamStatisticsRoundState round, PostAdventureStatsGraphType graphType)
        {
            switch (graphType)
            {
                case PostAdventureStatsGraphType.ArmyValue:
                    return round.ArmyValue;
                case PostAdventureStatsGraphType.GoldIncome:
                    return GetResourceAmount(round.Income, ResourceType.Gold);
                case PostAdventureStatsGraphType.WoodIncome:
                    return GetResourceAmount(round.Income, ResourceType.Wood);
                case PostAdventureStatsGraphType.StoneIncome:
                    return GetResourceAmount(round.Income, ResourceType.Stone);
                case PostAdventureStatsGraphType.GlimmerweaveIncome:
                    return GetResourceAmount(round.Income, ResourceType.Glimmerweave);
                case PostAdventureStatsGraphType.CelestialOreIncome:
                    return GetResourceAmount(round.Income, ResourceType.CelestialOre);
                case PostAdventureStatsGraphType.AncientAmberIncome:
                    return GetResourceAmount(round.Income, ResourceType.AncientAmber);
                case PostAdventureStatsGraphType.CollectedGold:
                    return GetResourceAmount(round.UnspentResources, ResourceType.Gold);
                case PostAdventureStatsGraphType.CollectedWood:
                    return GetResourceAmount(round.UnspentResources, ResourceType.Wood);
                case PostAdventureStatsGraphType.CollectedStone:
                    return GetResourceAmount(round.UnspentResources, ResourceType.Stone);
                case PostAdventureStatsGraphType.CollectedGlimmerweave:
                    return GetResourceAmount(round.UnspentResources, ResourceType.Glimmerweave);
                case PostAdventureStatsGraphType.CollectedCelestialOre:
                    return GetResourceAmount(round.UnspentResources, ResourceType.CelestialOre);
                case PostAdventureStatsGraphType.CollectedAncientAmber:
                    return GetResourceAmount(round.UnspentResources, ResourceType.AncientAmber);
                default:
                    return null;
            }
        }

        private static int? GetResourceAmount(IEnumerable<Resource> resources, ResourceType type)
        {
            if (resources == null)
            {
                return null;
            }

            foreach (Resource resource in resources)
            {
                if (resource != null && resource.Type == type)
                {
                    return resource.Amount;
                }
            }

            return null;
        }

        private static string GetText(UITextMesh text)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static bool IsComponentVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class GraphOption
        {
            public GraphOption(string id, int index, string label)
            {
                Id = id;
                Index = index;
                Label = label;
            }

            public string Id { get; private set; }

            public int Index { get; private set; }

            public string Label { get; private set; }
        }

        public sealed class TeamOption
        {
            public TeamOption(string id, PostAdventureStatsMenuTeamEntry entry, string label)
            {
                Id = id;
                Entry = entry;
                Label = label;
            }

            public string Id { get; private set; }

            public PostAdventureStatsMenuTeamEntry Entry { get; private set; }

            public string Label { get; private set; }
        }

        public sealed class GraphTeamColumn
        {
            public GraphTeamColumn(string id, int teamId, string label)
            {
                Id = id ?? string.Empty;
                TeamId = teamId;
                Label = label ?? string.Empty;
            }

            public string Id { get; private set; }

            public int TeamId { get; private set; }

            public string Label { get; private set; }
        }

        public sealed class GraphRoundRow
        {
            private readonly Dictionary<int, GraphPoint> _values;

            public GraphRoundRow(string id, int round, Dictionary<int, GraphPoint> values)
            {
                Id = id ?? string.Empty;
                Round = round;
                _values = values ?? new Dictionary<int, GraphPoint>();
            }

            public string Id { get; private set; }

            public int Round { get; private set; }

            public string GetValue(int teamId)
            {
                GraphPoint point;
                if (!_values.TryGetValue(teamId, out point))
                {
                    return string.Empty;
                }

                string value = point.Value.ToString();
                return point.BattleLost
                    ? ModText.Get(ModStrings.UI.LabelValue, value, ModText.Get(ModStrings.UI.StatusBattleLost))
                    : value;
            }
        }

        public sealed class GraphPoint
        {
            public GraphPoint(int value, bool battleLost)
            {
                Value = value;
                BattleLost = battleLost;
            }

            public int Value { get; private set; }

            public bool BattleLost { get; private set; }
        }
    }
}
