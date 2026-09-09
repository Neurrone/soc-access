using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PostAdventureResultAdapter : IPresent
    {
        private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(PostAdventureMenu), "_description");
        private static readonly FieldInfo DescriptionTitleField = AccessTools.Field(typeof(PostAdventureMenu), "_descriptionTitle");
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
                    return ModText.Get(ModStrings.Combat.Victory);
                }

                if (IsDefeat)
                {
                    return ModText.Get(ModStrings.Combat.Defeat);
                }

                return ModText.Get(ModStrings.Screens.PostAdventureResult);
            }
        }

        public string Description
        {
            get { return string.Join(" ", DescriptionLines); }
        }

        /// <summary>The paragraphs the menu wrote the outcome in - on a defeat it draws two, one
        /// under the other - kept apart rather than collapsed.</summary>
        public IList<string> DescriptionLines
        {
            get
            {
                return SpokenLines.Of(new[]
                {
                    UITextMeshTextUtility.GetEffectiveText(GetField<UITextMesh>(DescriptionField)),
                });
            }
        }

        public string ObjectivesTitle
        {
            get
            {
                string title = GetText(GetField<UITextMesh>(DescriptionTitleField));
                return !string.IsNullOrWhiteSpace(title) ? title : ModText.Get(ModStrings.Screens.Objectives);
            }
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
                    label,
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

        // The objective rows are pooled under one container and the build walks them once a frame;
        // the sweep is keyed on the frame rather than held, because a pooled row that has been
        // retired must not answer for the next page.
        private readonly FrameSweep<PostAdventureMenuObjectiveEntry> _objectiveEntries =
            new FrameSweep<PostAdventureMenuObjectiveEntry>("post-adventure objectives", inactiveToo: false);

        private PostAdventureMenuObjectiveEntry[] GetObjectiveEntries()
        {
            UITransform container = GetField<UITransform>(ObjectiveEntryContainerField);
            Transform transform = container != null ? container.MonoTransform : null;
            if (transform == null)
            {
                return new PostAdventureMenuObjectiveEntry[0];
            }

            return _objectiveEntries.Under(transform);
        }

        private static string GetObjectiveText(PostAdventureMenuObjectiveEntry entry)
        {
            UITextMesh text = GetField<UITextMesh>(entry, ObjectiveTextField);
            return GetText(text);
        }

        // Which mesh under a canvas says something is fixed once the page is drawn, so the walk
        // that finds it runs once per canvas and the text is read off it live. A mesh that has
        // stopped saying anything sends the search over the canvas again.
        private readonly Dictionary<CanvasGroup, UITextMesh> _firstTexts = new Dictionary<CanvasGroup, UITextMesh>();

        // The title is asked twice a frame - the screen's name and the page's first line - and
        // while the result canvas is drawn but still blank (the buttons fade in about two seconds
        // later) neither ask can be answered off the remembered mesh. Keyed on the frame, the two
        // asks share one walk and the blank frames cost one walk each instead of two.
        private readonly FrameSweep<UITextMesh> _canvasTexts =
            new FrameSweep<UITextMesh>("post-adventure result canvas", inactiveToo: false);

        private string GetFirstVisibleText(CanvasGroup canvasGroup)
        {
            if (canvasGroup == null)
            {
                return string.Empty;
            }

            UITextMesh kept;
            if (_firstTexts.TryGetValue(canvasGroup, out kept) && kept != null)
            {
                string keptText = GetText(kept);
                if (!string.IsNullOrWhiteSpace(keptText))
                {
                    return keptText;
                }
            }

            UITextMesh[] texts = _canvasTexts.Under(canvasGroup);
            for (int i = 0; i < texts.Length; i++)
            {
                string candidate = GetText(texts[i]);
                if (!string.IsNullOrWhiteSpace(candidate))
                {
                    _firstTexts[canvasGroup] = texts[i];
                    return candidate;
                }
            }

            _firstTexts[canvasGroup] = null;
            return string.Empty;
        }

        private static string GetText(UITextMesh text)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
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

        public sealed class ObjectiveEntry
        {
            private readonly PostAdventureMenuObjectiveEntry _entry;

            public ObjectiveEntry(string label, PostAdventureMenuObjectiveEntry entry)
            {
                Label = label ?? string.Empty;
                _entry = entry;
            }

            public string Label { get; private set; }

            public bool IsVisible
            {
                get { return IsComponentVisible(_entry) && !string.IsNullOrWhiteSpace(GetObjectiveText(_entry)); }
            }
        }
    }
}
