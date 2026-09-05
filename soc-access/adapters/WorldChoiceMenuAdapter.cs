using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure.WorldMenuComponents;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class WorldChoiceMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(WorldChoiceMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldChoiceMenu), "_async");
        private static readonly FieldInfo RewardButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_rewardButtons");
        private static readonly FieldInfo PenaltyButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_penaltyButtons");
        private static readonly FieldInfo ButtonPoolField = AccessTools.Field(typeof(WorldChoiceMenu), "_buttonPool");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(WorldChoiceMenu), "_localization");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(WorldChoiceMenu), "_adventureFacade");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");

        private readonly WorldChoiceMenu _menu;
        private readonly WorldChoiceMenu.Settings _settings;
        private readonly ILocalizationHandler _localization;
        private readonly IClientAdventureFacade _facade;

        public WorldChoiceMenuAdapter(WorldChoiceMenu menu)
        {
            _menu = menu;
            _settings = GetField<WorldChoiceMenu.Settings>(menu, SettingsField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
            _facade = GetField<IClientAdventureFacade>(menu, AdventureFacadeField);
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

        public string ConfirmLabel
        {
            get { return GetButtonText(_settings != null ? _settings.OkButton : null); }
        }

        public TroopHudAdapter Troops
        {
            get { return new TroopHudAdapter(GetWielderTroopHud(), _facade, _localization); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && (GetRewardButtons().Count > 0 || GetPenaltyButtons().Count > 0 || GetGenericChoiceButtons().Count > 0);
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
            List<IWorldMapChoiceButton> rewardButtons = GetRewardButtons();
            List<IWorldMapChoiceButton> penaltyButtons = GetPenaltyButtons();
            if (rewardButtons.Count == 0 && penaltyButtons.Count == 0)
            {
                return GetGenericChoices();
            }

            List<ChoiceItem> choices = new List<ChoiceItem>(rewardButtons.Count + penaltyButtons.Count);

            for (int i = 0; i < rewardButtons.Count; i++)
            {
                int capturedIndex = i;
                choices.Add(new ChoiceItem(
                    isPenalty: false,
                    () => BuildChoiceLabel(GetRewardButton(capturedIndex)),
                    () => IsRewardEnabled(capturedIndex),
                    () => FocusReward(capturedIndex),
                    () => true,
                    () => GetChoiceTooltip(GetRewardButton(capturedIndex))));
            }

            for (int i = 0; i < penaltyButtons.Count; i++)
            {
                int capturedIndex = i;
                choices.Add(new ChoiceItem(
                    isPenalty: true,
                    () => BuildChoiceLabel(GetPenaltyButton(capturedIndex)),
                    () => IsPenaltyEnabled(capturedIndex),
                    () => FocusPenalty(capturedIndex),
                    () => true,
                    () => GetChoiceTooltip(GetPenaltyButton(capturedIndex))));
            }

            return choices;
        }

        private IReadOnlyList<ChoiceItem> GetGenericChoices()
        {
            List<IWorldMapChoiceButton> buttons = GetGenericChoiceButtons();
            List<ChoiceItem> choices = new List<ChoiceItem>(buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
            {
                int capturedIndex = i;
                choices.Add(new ChoiceItem(
                    isPenalty: false,
                    () => BuildChoiceLabel(GetGenericChoiceButton(capturedIndex)),
                    () => IsGenericChoiceEnabled(capturedIndex),
                    () => FocusGenericChoice(capturedIndex),
                    () => true,
                    () => GetChoiceTooltip(GetGenericChoiceButton(capturedIndex)),
                    isGeneric: true));
            }

            return choices;
        }

        private IWorldMapChoiceButton GetRewardButton(int index)
        {
            List<IWorldMapChoiceButton> buttons = GetRewardButtons();
            return index >= 0 && index < buttons.Count ? buttons[index] : null;
        }

        private IWorldMapChoiceButton GetPenaltyButton(int index)
        {
            List<IWorldMapChoiceButton> buttons = GetPenaltyButtons();
            return index >= 0 && index < buttons.Count ? buttons[index] : null;
        }

        private IWorldMapChoiceButton GetGenericChoiceButton(int index)
        {
            List<IWorldMapChoiceButton> buttons = GetGenericChoiceButtons();
            return index >= 0 && index < buttons.Count ? buttons[index] : null;
        }

        private bool IsRewardEnabled(int index)
        {
            IWorldMapChoiceButton button = GetRewardButton(index);
            return button != null && button.Interactable;
        }

        private bool IsPenaltyEnabled(int index)
        {
            IWorldMapChoiceButton button = GetPenaltyButton(index);
            return button != null && button.Interactable;
        }

        private bool IsGenericChoiceEnabled(int index)
        {
            IWorldMapChoiceButton button = GetGenericChoiceButton(index);
            return button != null && button.Interactable;
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

        private bool FocusPenalty(int index)
        {
            List<IWorldMapChoiceButton> buttons = GetPenaltyButtons();
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

            // Match the reward path: a pointer click selects the native penalty
            // choice, while the separate Confirm button commits it.
            return !choice.Interactable || NativeSelectionUtility.Click(choice.Button);
        }

        private bool FocusGenericChoice(int index)
        {
            List<IWorldMapChoiceButton> buttons = GetGenericChoiceButtons();
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
            return !choice.Interactable || NativeSelectionUtility.Click(choice.Button);
        }

        private string BuildChoiceLabel(IWorldMapChoiceButton button)
        {
            string artifactName;
            if (TryGetArtifactChoiceLabel(button, out artifactName))
            {
                return artifactName;
            }

            return NormalizeChoiceText(GetText(button != null ? button.TypeTextMesh : null));
        }

        private Tooltip GetChoiceTooltip(IWorldMapChoiceButton button)
        {
            Selectable selectable = button != null && button.Button != null ? button.Button.GetSelectable() : null;
            return Tooltip.ForComponent(
                selectable,
                selectable != null ? selectable.GetComponent<RectTransform>() : null,
                _localization);
        }

        private bool TryGetArtifactChoiceLabel(IWorldMapChoiceButton button, out string artifactName)
        {
            artifactName = string.Empty;
            if (button == null)
            {
                return false;
            }

            Component component = button.Button as Component;
            if (component == null && button.Button != null)
            {
                component = button.Button.GetSelectable();
            }

            IDetails details;
            return NativeTooltipUtility.TryGetUiDetails(component, out details)
                && ArtifactSpeechFormatter.TryFormatName(details, _localization, out artifactName);
        }

        private List<IWorldMapChoiceButton> GetRewardButtons()
        {
            List<IWorldMapChoiceButton> buttons = GetField<List<IWorldMapChoiceButton>>(_menu, RewardButtonsField);
            return buttons ?? new List<IWorldMapChoiceButton>();
        }

        private List<IWorldMapChoiceButton> GetPenaltyButtons()
        {
            List<IWorldMapChoiceButton> buttons = GetField<List<IWorldMapChoiceButton>>(_menu, PenaltyButtonsField);
            return buttons ?? new List<IWorldMapChoiceButton>();
        }

        private List<IWorldMapChoiceButton> GetGenericChoiceButtons()
        {
            object buttonPool = _menu != null && ButtonPoolField != null ? ButtonPoolField.GetValue(_menu) : null;
            PropertyInfo activeItemsProperty = buttonPool != null ? buttonPool.GetType().GetProperty("ActiveItems") : null;
            IEnumerable activeItems = activeItemsProperty != null ? activeItemsProperty.GetValue(buttonPool, null) as IEnumerable : null;
            List<IWorldMapChoiceButton> buttons = new List<IWorldMapChoiceButton>();
            if (activeItems == null)
            {
                return buttons;
            }

            foreach (object item in activeItems)
            {
                IWorldMapChoiceButton button = item as IWorldMapChoiceButton;
                if (button != null && button.Button != null)
                {
                    buttons.Add(button);
                }
            }

            return buttons;
        }

        private TroopHUD GetWielderTroopHud()
        {
            return _settings != null && _settings.WielderInteractHeader != null
                ? GetField<TroopHUD>(_settings.WielderInteractHeader, HeaderTroopHudField)
                : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(IUIButton button)
        {
            UIButton concreteButton = button as UIButton;
            if (concreteButton != null)
            {
                return MenuButtonTextUtility.GetStandardButtonLabel(concreteButton);
            }

            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private static string NormalizeChoiceText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(text, @"-\s+(\d)", "-$1");
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class ChoiceItem
        {
            private readonly Func<string> _getLabel;
            private readonly Func<bool> _isEnabled;
            private readonly Func<Tooltip> _getTooltip;

            public ChoiceItem(
                bool isPenalty,
                Func<string> getLabel,
                Func<bool> isEnabled,
                Action onFocus,
                Func<bool> isVisible,
                Func<Tooltip> getTooltip = null,
                bool isGeneric = false)
            {
                IsPenalty = isPenalty;
                IsGeneric = isGeneric;
                _getLabel = getLabel;
                _isEnabled = isEnabled;
                OnFocus = onFocus;
                IsVisible = isVisible;
                _getTooltip = getTooltip;
            }

            public bool IsPenalty { get; private set; }
            public bool IsGeneric { get; private set; }
            public string Label
            {
                get { return _getLabel != null ? _getLabel() ?? string.Empty : string.Empty; }
            }
            public bool IsEnabled
            {
                get { return _isEnabled != null && _isEnabled(); }
            }
            public Action OnFocus { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Tooltip Tooltip
            {
                get { return _getTooltip != null ? _getTooltip() : null; }
            }
        }
    }
}
