using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.WorldMenuComponents;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class WorldChoiceMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(WorldChoiceMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldChoiceMenu), "_async");
        private static readonly FieldInfo RewardButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_rewardButtons");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(WorldChoiceMenu), "_localization");

        private readonly WorldChoiceMenu _menu;
        private readonly WorldChoiceMenu.Settings _settings;
        private readonly ILocalizationHandler _localization;

        public WorldChoiceMenuAdapter(WorldChoiceMenu menu)
        {
            _menu = menu;
            _settings = GetField<WorldChoiceMenu.Settings>(menu, SettingsField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return GetText(_settings != null ? _settings.HeaderText : null); }
        }

        public string Body
        {
            get { return GetText(_settings != null ? _settings.BodyText : null); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && GetRewardButtons().Count > 0;
        }

        public bool IsConfirmEnabled()
        {
            return _settings != null
                && _settings.OkButton != null
                && _settings.OkButton.Interactable;
        }

        public bool ActivateConfirm()
        {
            if (!IsConfirmEnabled())
            {
                return false;
            }

            return NativeSelectionUtility.Click(_settings.OkButton);
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.ForceClose();
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public IReadOnlyList<ChoiceItem> GetChoices()
        {
            List<IWorldMapChoiceButton> buttons = GetRewardButtons();
            List<ChoiceItem> choices = new List<ChoiceItem>(buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
            {
                IWorldMapChoiceButton button = buttons[i];
                if (button == null)
                {
                    continue;
                }

                int capturedIndex = i;
                Selectable selectable = button.Button != null ? button.Button.GetSelectable() : null;
                choices.Add(new ChoiceItem(
                    "reward-" + i,
                    BuildChoiceLabel(button),
                    button.Interactable ? string.Empty : "disabled",
                    () => FocusReward(capturedIndex),
                    () => true,
                    Tooltip.ForComponent(
                        selectable,
                        selectable != null ? selectable.GetComponent<RectTransform>() : null,
                        _localization)));
            }

            return choices;
        }

        private bool FocusReward(int index)
        {
            List<IWorldMapChoiceButton> buttons = GetRewardButtons();
            if (index < 0 || index >= buttons.Count)
            {
                return false;
            }

            IWorldMapChoiceButton choice = buttons[index];
            if (choice == null || choice.Button == null)
            {
                return false;
            }

            Selectable selectable = choice.Button.GetSelectable();
            NativeSelectionUtility.Select(selectable);

            // Accessibility focus in this menu intentionally mirrors a single native click:
            // the reward becomes the selected choice through the game's pointer-click handler.
            // Do not route this through UIButton.OnSubmit;
            // reward buttons wire OnGamepadDown to immediate confirmation, so submitting here
            // would close the menu while the user is only moving through choices.
            return !choice.Interactable || NativeSelectionUtility.Click(choice.Button);
        }

        private string BuildChoiceLabel(IWorldMapChoiceButton button)
        {
            string visibleText = GetText(button != null ? button.TypeTextMesh : null);
            return visibleText;
        }

        private List<IWorldMapChoiceButton> GetRewardButtons()
        {
            List<IWorldMapChoiceButton> buttons = GetField<List<IWorldMapChoiceButton>>(_menu, RewardButtonsField);
            return buttons ?? new List<IWorldMapChoiceButton>();
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class ChoiceItem
        {
            public ChoiceItem(string id, string label, string status, Action onFocus, Func<bool> isVisible, Tooltip tooltip = null)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                OnFocus = onFocus;
                IsVisible = isVisible;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public string Status { get; private set; }
            public Action OnFocus { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
