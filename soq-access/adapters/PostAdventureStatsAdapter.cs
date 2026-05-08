using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PostAdventureStatsAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(PostAdventureStatsMenu), "_settings");
        private static readonly FieldInfo DropdownField = AccessTools.Field(typeof(UITextMeshDropdown), "_dropdown");
        private static readonly FieldInfo GraphTitleTextField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "TitleText");
        private static readonly FieldInfo TotalRoundsTextField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "TotalRoundsText");
        private static readonly FieldInfo TotalPlayTimeTextField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "TotalPlayTimeText");
        private static readonly FieldInfo TeamEntriesField = AccessTools.Field(typeof(PostAdventureStatsMenuGraphView), "_spawnedTeamEntries");
        private static readonly FieldInfo TeamNameTextField = AccessTools.Field(typeof(PostAdventureStatsMenuTeamEntry), "_teamNameText");
        private static readonly FieldInfo TeamToggleField = AccessTools.Field(typeof(PostAdventureStatsMenuTeamEntry), "_enabledToggle");

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
            return !string.IsNullOrWhiteSpace(label) ? label : "Close";
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

        internal sealed class GraphOption
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

        internal sealed class TeamOption
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
    }
}
