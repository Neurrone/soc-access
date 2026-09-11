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
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class WorldChoiceMenuAdapter : IPresent
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(WorldChoiceMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldChoiceMenu), "_async");
        private static readonly FieldInfo RewardButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_rewardButtons");
        private static readonly FieldInfo PenaltyButtonsField = AccessTools.Field(typeof(WorldChoiceMenu), "_penaltyButtons");
        private static readonly FieldInfo ButtonPoolField = AccessTools.Field(typeof(WorldChoiceMenu), "_buttonPool");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(WorldChoiceMenu), "_localization");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(WorldChoiceMenu), "_adventureFacade");
        private static readonly FieldInfo SelectedRewardField = AccessTools.Field(typeof(WorldChoiceMenu), "selectedReward");
        private static readonly FieldInfo SelectedPenaltyField = AccessTools.Field(typeof(WorldChoiceMenu), "selectedPenalty");

        private readonly WorldChoiceMenu _menu;
        private readonly WorldChoiceMenu.Settings _settings;
        private readonly ILocalizationHandler _localization;
        private readonly IClientAdventureFacade _facade;
        private WielderInteract _wielder;

        public WorldChoiceMenuAdapter(WorldChoiceMenu menu)
        {
            _menu = menu;
            _settings = Reflect.Get<WorldChoiceMenu.Settings>(menu, SettingsField);
            _localization = Reflect.Get<ILocalizationHandler>(menu, LocalizationField);
            _facade = Reflect.Get<IClientAdventureFacade>(menu, AdventureFacadeField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return UITextMeshTextUtility.Spoken(_settings != null ? _settings.HeaderText : null); }
        }

        public string Body
        {
            get { return UITextMeshTextUtility.Spoken(_settings != null ? _settings.BodyText : null); }
        }

        public string ConfirmLabel
        {
            get { return GetButtonText(_settings != null ? _settings.OkButton : null); }
        }

        /// <summary>The band across the top: the wielder who walked in, their army, and the cross the
        /// menu is closed with.</summary>
        public WielderInteract Wielder
        {
            get
            {
                if (_wielder == null)
                {
                    _wielder = new WielderInteract(
                        _settings != null ? _settings.WielderInteractHeader : null,
                        _facade,
                        _localization);
                }

                return _wielder;
            }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && (GetRewardButtons().Count > 0 || GetPenaltyButtons().Count > 0 || GetGenericChoiceButtons().Count > 0);
        }

        /// <summary>The button that commits the chosen card.</summary>
        public Component ConfirmButton
        {
            get { return _settings != null ? _settings.OkButton : null; }
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

        /// <summary>
        /// The cards the menu draws, in the order it spawned them: the rewards then the penalties,
        /// or - where the menu was opened with plain lines of text rather than with reward data - the
        /// generic buttons those lines were drawn on.
        /// </summary>
        public IReadOnlyList<ChoiceItem> GetChoices()
        {
            List<IWorldMapChoiceButton> rewardButtons = GetRewardButtons();
            List<IWorldMapChoiceButton> penaltyButtons = GetPenaltyButtons();
            if (rewardButtons.Count == 0 && penaltyButtons.Count == 0)
            {
                return BuildChoices(GetGenericChoiceButtons(), isPenalty: false, isGeneric: true);
            }

            List<ChoiceItem> choices = new List<ChoiceItem>(rewardButtons.Count + penaltyButtons.Count);
            choices.AddRange(BuildChoices(rewardButtons, isPenalty: false, isGeneric: false));
            choices.AddRange(BuildChoices(penaltyButtons, isPenalty: true, isGeneric: false));
            return choices;
        }

        private List<ChoiceItem> BuildChoices(List<IWorldMapChoiceButton> buttons, bool isPenalty, bool isGeneric)
        {
            List<ChoiceItem> choices = new List<ChoiceItem>(buttons.Count);
            for (int i = 0; i < buttons.Count; i++)
            {
                int index = i;
                bool penalty = isPenalty;
                choices.Add(new ChoiceItem(
                    isPenalty,
                    () => BuildChoiceLabel(GetChoiceButton(penalty, index)),
                    () => IsChoiceEnabled(penalty, index),
                    () => IsChoiceSelected(penalty, index),
                    () => SelectChoice(penalty, index),
                    () => ChooseChoice(penalty, index),
                    () => GetChoiceTooltip(GetChoiceButton(penalty, index)),
                    GetChoiceComponent(penalty, index),
                    isGeneric));
            }

            return choices;
        }

        private List<IWorldMapChoiceButton> GetChoiceButtons(bool isPenalty)
        {
            if (isPenalty)
            {
                return GetPenaltyButtons();
            }

            List<IWorldMapChoiceButton> rewards = GetRewardButtons();
            return rewards.Count > 0 ? rewards : GetGenericChoiceButtons();
        }

        private IWorldMapChoiceButton GetChoiceButton(bool isPenalty, int index)
        {
            List<IWorldMapChoiceButton> buttons = GetChoiceButtons(isPenalty);
            return index >= 0 && index < buttons.Count ? buttons[index] : null;
        }

        private Component GetChoiceComponent(bool isPenalty, int index)
        {
            IWorldMapChoiceButton button = GetChoiceButton(isPenalty, index);
            return button != null ? button.Button : null;
        }

        private bool IsChoiceEnabled(bool isPenalty, int index)
        {
            IWorldMapChoiceButton button = GetChoiceButton(isPenalty, index);
            return button != null && button.Interactable;
        }

        /// <summary>Which card the menu has taken as chosen: the index it remembers for itself, which
        /// is what its Confirm button acts on.</summary>
        private bool IsChoiceSelected(bool isPenalty, int index)
        {
            FieldInfo field = isPenalty ? SelectedPenaltyField : SelectedRewardField;
            object selected = _menu != null && field != null ? field.GetValue(_menu) : null;
            return selected is int && (int)selected == index;
        }

        /// <summary>Draw the card as the pointer resting on it would: the game's own selection, which
        /// is what raises its tooltip. It does NOT choose.</summary>
        private bool SelectChoice(bool isPenalty, int index)
        {
            IWorldMapChoiceButton choice = GetChoiceButton(isPenalty, index);
            return choice != null
                && choice.Button != null
                && NativeSelectionUtility.Select(choice.Button.GetSelectable());
        }

        /// <summary>
        /// Choose the card, as a pointer click does: the game's own click handler takes it as the
        /// selection and turns its Confirm button on.
        ///
        /// NOT through <c>UIButton.OnSubmit</c>: these buttons wire <c>OnGamepadDown</c> to immediate
        /// confirmation, so submitting would close the whole menu on the card the player is only
        /// looking at.
        /// </summary>
        private bool ChooseChoice(bool isPenalty, int index)
        {
            IWorldMapChoiceButton choice = GetChoiceButton(isPenalty, index);
            return choice != null && choice.Button != null && NativeSelectionUtility.Click(choice.Button);
        }

        private string BuildChoiceLabel(IWorldMapChoiceButton button)
        {
            string artifactName;
            if (TryGetArtifactChoiceLabel(button, out artifactName))
            {
                return artifactName;
            }

            return SpokenText.JoinMinusSign(UITextMeshTextUtility.Spoken(button != null ? button.TypeTextMesh : null));
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
            List<IWorldMapChoiceButton> buttons = Reflect.Get<List<IWorldMapChoiceButton>>(_menu, RewardButtonsField);
            return buttons ?? new List<IWorldMapChoiceButton>();
        }

        private List<IWorldMapChoiceButton> GetPenaltyButtons()
        {
            List<IWorldMapChoiceButton> buttons = Reflect.Get<List<IWorldMapChoiceButton>>(_menu, PenaltyButtonsField);
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

        private static string GetButtonText(IUIButton button)
        {
            UIButton concreteButton = button as UIButton;
            if (concreteButton != null)
            {
                return MenuButtonTextUtility.GetStandardButtonLabel(concreteButton);
            }

            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        /// <summary>One card the menu draws, with everything about it the screen asks for.</summary>
        public sealed class ChoiceItem
        {
            private readonly Func<string> _getLabel;
            private readonly Func<bool> _isEnabled;
            private readonly Func<bool> _isSelected;
            private readonly Func<bool> _select;
            private readonly Func<bool> _choose;
            private readonly Func<Tooltip> _getTooltip;

            public ChoiceItem(
                bool isPenalty,
                Func<string> getLabel,
                Func<bool> isEnabled,
                Func<bool> isSelected,
                Func<bool> select,
                Func<bool> choose,
                Func<Tooltip> getTooltip,
                Component button,
                bool isGeneric = false)
            {
                IsPenalty = isPenalty;
                IsGeneric = isGeneric;
                Button = button;
                _getLabel = getLabel;
                _isEnabled = isEnabled;
                _isSelected = isSelected;
                _select = select;
                _choose = choose;
                _getTooltip = getTooltip;
            }

            public bool IsPenalty { get; private set; }

            public bool IsGeneric { get; private set; }

            /// <summary>The card itself, which the game draws and the graph hangs its node on.</summary>
            public Component Button { get; private set; }

            /// <summary>What the card says, the game's own red reason for a card it will not take
            /// included, since the card draws that as part of its text.</summary>
            public string Label
            {
                get { return _getLabel != null ? _getLabel() ?? string.Empty : string.Empty; }
            }

            public bool IsEnabled
            {
                get { return _isEnabled != null && _isEnabled(); }
            }

            public bool IsSelected
            {
                get { return _isSelected != null && _isSelected(); }
            }

            public Tooltip Tooltip
            {
                get { return _getTooltip != null ? _getTooltip() : null; }
            }

            public bool Select()
            {
                return _select != null && _select();
            }

            public bool Choose()
            {
                return _choose != null && _choose();
            }
        }
    }
}
