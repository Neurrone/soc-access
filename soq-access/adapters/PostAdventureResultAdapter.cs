using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PostAdventureResultAdapter
    {
        private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(PostAdventureMenu), "_description");
        private static readonly FieldInfo VictoryCanvasGroupField = AccessTools.Field(typeof(PostAdventureMenu), "_victoryCanvasgroup");
        private static readonly FieldInfo DefeatCanvasGroupField = AccessTools.Field(typeof(PostAdventureMenu), "_defeatCanvasgroup");
        private static readonly FieldInfo ButtonCanvasGroupField = AccessTools.Field(typeof(PostAdventureMenu), "_buttonCanvasGroup");
        private static readonly FieldInfo ObjectiveEntryContainerField = AccessTools.Field(typeof(PostAdventureMenu), "_objectiveEntryContainer");
        private static readonly FieldInfo StatsButtonField = AccessTools.Field(typeof(PostAdventureMenu), "_statsButton");
        private static readonly FieldInfo ContinueCampaignButtonField = AccessTools.Field(typeof(PostAdventureMenu), "_continueCampaignButton");
        private static readonly FieldInfo RestartMapButtonField = AccessTools.Field(typeof(PostAdventureMenu), "_restartMapButton");
        private static readonly FieldInfo LoadButtonField = AccessTools.Field(typeof(PostAdventureMenu), "_loadButton");
        private static readonly FieldInfo QuitToMainButtonField = AccessTools.Field(typeof(PostAdventureMenu), "_quitToMainButton");
        private static readonly FieldInfo PlayerStatsButtonField = AccessTools.Field(typeof(PostAdventureMenu), "_playerStatsButton");

        private static readonly FieldInfo ObjectiveIconTickField = AccessTools.Field(typeof(PostAdventureMenuObjectiveEntry), "_objectiveIconTick");
        private static readonly FieldInfo LoseConditionIconField = AccessTools.Field(typeof(PostAdventureMenuObjectiveEntry), "_loseConditionIcon");
        private static readonly FieldInfo ObjectiveTextField = AccessTools.Field(typeof(PostAdventureMenuObjectiveEntry), "_objectiveText");

        private readonly PostAdventureMenu _menu;

        public PostAdventureResultAdapter(PostAdventureMenu menu)
        {
            _menu = menu;
        }

        public string ResultTitle
        {
            get
            {
                CanvasGroup resultCanvas = ActiveResultCanvas;
                string text = GetFirstVisibleText(resultCanvas);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return text;
                }

                if (IsVictory)
                {
                    return "Victory";
                }

                if (IsDefeat)
                {
                    return "Defeat";
                }

                return "Post adventure result";
            }
        }

        public string Description
        {
            get { return GetText(GetField<UITextMesh>(DescriptionField)); }
        }

        public bool DescriptionVisible
        {
            get
            {
                UITextMesh description = GetField<UITextMesh>(DescriptionField);
                return IsComponentVisible(description) && !string.IsNullOrWhiteSpace(GetText(description));
            }
        }

        public bool IsVictory
        {
            get { return IsCanvasActive(GetField<CanvasGroup>(VictoryCanvasGroupField)); }
        }

        public bool IsDefeat
        {
            get { return IsCanvasActive(GetField<CanvasGroup>(DefeatCanvasGroupField)); }
        }

        public bool IsPresent()
        {
            return IsLiveMenu()
                && (IsVictory || IsDefeat)
                && HasVisibleButton();
        }

        public bool IsReadyAfterAnimation()
        {
            CanvasGroup buttonCanvasGroup = GetField<CanvasGroup>(ButtonCanvasGroupField);
            return IsPresent()
                && buttonCanvasGroup != null
                && buttonCanvasGroup.alpha >= 0.95f
                && !string.IsNullOrWhiteSpace(ResultTitle);
        }

        public IReadOnlyList<ObjectiveEntry> GetObjectives()
        {
            PostAdventureMenuObjectiveEntry[] entries = GetObjectiveEntries();
            List<ObjectiveEntry> result = new List<ObjectiveEntry>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                PostAdventureMenuObjectiveEntry entry = entries[i];
                if (!IsComponentVisible(entry))
                {
                    continue;
                }

                string label = GetObjectiveText(entry);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                result.Add(new ObjectiveEntry(
                    "post-adventure-objective-" + i,
                    label,
                    GetObjectiveStatus(entry),
                    entry));
            }

            return result.ToArray();
        }

        public UIButton StatsButton
        {
            get { return GetField<UIButton>(StatsButtonField); }
        }

        public UIButton ContinueCampaignButton
        {
            get { return GetField<UIButton>(ContinueCampaignButtonField); }
        }

        public UIButton RestartMapButton
        {
            get { return GetField<UIButton>(RestartMapButtonField); }
        }

        public UIButton LoadButton
        {
            get { return GetField<UIButton>(LoadButtonField); }
        }

        public UIButton QuitToMainButton
        {
            get { return GetField<UIButton>(QuitToMainButtonField); }
        }

        public UIButton PlayerStatsButton
        {
            get { return GetField<UIButton>(PlayerStatsButtonField); }
        }

        public string GetButtonLabel(UIButton button)
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(button);
        }

        public bool IsButtonVisible(UIButton button)
        {
            return button != null && button.Active && button.gameObject != null && button.gameObject.activeInHierarchy;
        }

        public bool IsButtonEnabled(UIButton button)
        {
            return IsButtonVisible(button) && button.Interactable;
        }

        public bool ActivateButton(UIButton button)
        {
            return NativeSelectionUtility.Click(button);
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private CanvasGroup ActiveResultCanvas
        {
            get
            {
                CanvasGroup victory = GetField<CanvasGroup>(VictoryCanvasGroupField);
                if (IsCanvasActive(victory))
                {
                    return victory;
                }

                CanvasGroup defeat = GetField<CanvasGroup>(DefeatCanvasGroupField);
                return IsCanvasActive(defeat) ? defeat : null;
            }
        }

        private bool IsLiveMenu()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.scene.IsValid()
                && _menu.gameObject.scene.isLoaded
                && _menu.gameObject.activeInHierarchy;
        }

        private bool HasVisibleButton()
        {
            return IsButtonVisible(StatsButton)
                || IsButtonVisible(ContinueCampaignButton)
                || IsButtonVisible(RestartMapButton)
                || IsButtonVisible(LoadButton)
                || IsButtonVisible(QuitToMainButton)
                || IsButtonVisible(PlayerStatsButton);
        }

        private PostAdventureMenuObjectiveEntry[] GetObjectiveEntries()
        {
            Transform container = GetField<Transform>(ObjectiveEntryContainerField);
            if (container == null)
            {
                return new PostAdventureMenuObjectiveEntry[0];
            }

            return container.GetComponentsInChildren<PostAdventureMenuObjectiveEntry>(false);
        }

        private static string GetObjectiveText(PostAdventureMenuObjectiveEntry entry)
        {
            UITextMesh text = GetField<UITextMesh>(entry, ObjectiveTextField);
            return GetText(text);
        }

        private static string GetObjectiveStatus(PostAdventureMenuObjectiveEntry entry)
        {
            if (IsImageActive(entry, LoseConditionIconField))
            {
                return "failed";
            }

            if (IsImageActive(entry, ObjectiveIconTickField))
            {
                return "completed";
            }

            return "incomplete";
        }

        private static bool IsImageActive(PostAdventureMenuObjectiveEntry entry, FieldInfo field)
        {
            Component component = GetField<Component>(entry, field);
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static string GetFirstVisibleText(CanvasGroup canvasGroup)
        {
            if (canvasGroup == null)
            {
                return string.Empty;
            }

            UITextMesh[] texts = canvasGroup.GetComponentsInChildren<UITextMesh>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                string candidate = GetText(texts[i]);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    return candidate;
                }
            }

            return string.Empty;
        }

        private static string GetText(UITextMesh text)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static bool IsCanvasActive(CanvasGroup canvasGroup)
        {
            return canvasGroup != null && canvasGroup.gameObject != null && canvasGroup.gameObject.activeInHierarchy;
        }

        private static bool IsComponentVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return GetField<T>(_menu, field);
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class ObjectiveEntry
        {
            private readonly PostAdventureMenuObjectiveEntry _entry;

            public ObjectiveEntry(string id, string label, string status, PostAdventureMenuObjectiveEntry entry)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                _entry = entry;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public string Status { get; private set; }

            public bool IsVisible
            {
                get { return IsComponentVisible(_entry) && !string.IsNullOrWhiteSpace(GetObjectiveText(_entry)); }
            }
        }
    }
}
