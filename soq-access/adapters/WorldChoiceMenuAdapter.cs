using System;
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
    internal sealed class WorldChoiceMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(WorldChoiceMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldChoiceMenu), "_async");
        private static readonly FieldInfo RewardButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_rewardButtons");
        private static readonly FieldInfo PenaltyButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_penaltyButtons");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(WorldChoiceMenu), "_localization");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(WorldChoiceMenu), "_adventureFacade");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");
        private static readonly FieldInfo HeaderCloseButtonField = AccessTools.Field(typeof(WielderInteractHeader), "_closeButton");
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");

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

        public string CancelLabel
        {
            get
            {
                string label = GetButtonText(GetBackgroundCloseButton());
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }

                return GetButtonText(GetHeaderCloseButton());
            }
        }

        public string ChoiceMenuLabel
        {
            get
            {
                int rewardCount = GetRewardButtons().Count;
                int penaltyCount = GetPenaltyButtons().Count;
                if (rewardCount > 0 && penaltyCount > 0)
                {
                    return "Choices";
                }

                if (penaltyCount > 0)
                {
                    return "Penalties";
                }

                return "Rewards";
            }
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
                && (GetRewardButtons().Count > 0 || GetPenaltyButtons().Count > 0);
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
            List<ChoiceItem> choices = new List<ChoiceItem>(rewardButtons.Count + penaltyButtons.Count);

            for (int i = 0; i < rewardButtons.Count; i++)
            {
                IWorldMapChoiceButton button = rewardButtons[i];
                if (button == null)
                {
                    continue;
                }

                int capturedIndex = i;
                Selectable selectable = button.Button != null ? button.Button.GetSelectable() : null;
                choices.Add(new ChoiceItem(
                    isPenalty: false,
                    BuildChoiceLabel(button),
                    button.Interactable,
                    () => FocusReward(capturedIndex),
                    () => true,
                    Tooltip.ForComponent(
                        selectable,
                        selectable != null ? selectable.GetComponent<RectTransform>() : null,
                        _localization)));
            }

            for (int i = 0; i < penaltyButtons.Count; i++)
            {
                IWorldMapChoiceButton button = penaltyButtons[i];
                if (button == null)
                {
                    continue;
                }

                int capturedIndex = i;
                Selectable selectable = button.Button != null ? button.Button.GetSelectable() : null;
                choices.Add(new ChoiceItem(
                    isPenalty: true,
                    BuildChoiceLabel(button),
                    button.Interactable,
                    () => FocusPenalty(capturedIndex),
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

        private string BuildChoiceLabel(IWorldMapChoiceButton button)
        {
            return NormalizeChoiceText(GetText(button != null ? button.TypeTextMesh : null));
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

        private TroopHUD GetWielderTroopHud()
        {
            return _settings != null && _settings.WielderInteractHeader != null
                ? GetField<TroopHUD>(_settings.WielderInteractHeader, HeaderTroopHudField)
                : null;
        }

        private UIButton GetHeaderCloseButton()
        {
            return _settings != null && _settings.WielderInteractHeader != null
                ? GetField<UIButton>(_settings.WielderInteractHeader, HeaderCloseButtonField)
                : null;
        }

        private UIButton GetBackgroundCloseButton()
        {
            return _settings != null && _settings.AdventureMenuBackground != null
                ? GetField<UIButton>(_settings.AdventureMenuBackground, BackgroundCloseButtonField)
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

        internal sealed class ChoiceItem
        {
            public ChoiceItem(bool isPenalty, string label, bool isEnabled, Action onFocus, Func<bool> isVisible, Tooltip tooltip = null)
            {
                IsPenalty = isPenalty;
                Label = label ?? string.Empty;
                IsEnabled = isEnabled;
                OnFocus = onFocus;
                IsVisible = isVisible;
                Tooltip = tooltip;
            }

            public bool IsPenalty { get; private set; }
            public string Label { get; private set; }
            public bool IsEnabled { get; private set; }
            public Action OnFocus { get; private set; }
            public Func<bool> IsVisible { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
