using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class LevelUpMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(CommanderLevelUpMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(CommanderLevelUpMenu), "_async");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(CommanderLevelUpMenu), "_localizationHandler");

        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(CommanderLevelUpSkillComponent), "_headerText");
        private static readonly FieldInfo DescriptionTextField = AccessTools.Field(typeof(CommanderLevelUpSkillComponent), "_descriptionText");
        private static readonly FieldInfo SkillLevelTextField = AccessTools.Field(typeof(CommanderLevelUpSkillComponent), "_skillLevelText");
        private static readonly FieldInfo ButtonField = AccessTools.Field(typeof(CommanderLevelUpSkillComponent), "_button");

        private static readonly FieldInfo OffenseTextField = AccessTools.Field(typeof(CommanderStatsInfo), "_offenseText");
        private static readonly FieldInfo DefenceTextField = AccessTools.Field(typeof(CommanderStatsInfo), "_defenceText");
        private static readonly FieldInfo MovementTextField = AccessTools.Field(typeof(CommanderStatsInfo), "_movementText");
        private static readonly FieldInfo ViewTextField = AccessTools.Field(typeof(CommanderStatsInfo), "_viewText");
        private static readonly FieldInfo SpellDamagePowerTextField = AccessTools.Field(typeof(CommanderStatsInfo), "_spellDamagePowerText");
        private static readonly FieldInfo OffenseTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_offenseTooltipImage");
        private static readonly FieldInfo DefenceTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_defenceTooltipImage");
        private static readonly FieldInfo MovementTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_movementTooltipImage");
        private static readonly FieldInfo ViewTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_viewTooltipImage");
        private static readonly FieldInfo SpellDamagePowerTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_spellDamagePowerTooltipImage");

        private static readonly FieldInfo BackgroundIsOpenField = AccessTools.Field(typeof(AdventureMenuBackground), "_isOpen");
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");

        private readonly CommanderLevelUpMenu _menu;
        private readonly CommanderLevelUpMenu.Settings _settings;
        private readonly ILocalizationHandler _localization;

        public LevelUpMenuAdapter(CommanderLevelUpMenu menu)
        {
            _menu = menu;
            _settings = GetField<CommanderLevelUpMenu.Settings>(menu, SettingsField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && IsBackgroundOpen()
                && GetSkillChoices().Count > 0;
        }

        public string GetTitle()
        {
            string header = GetText(_settings != null ? _settings.HeaderText : null);
            string level = GetText(_settings != null ? _settings.LevelText : null);
            return MenuButtonTextUtility.JoinParts(
                header,
                string.IsNullOrWhiteSpace(level) ? string.Empty : ModText.Get(ModStrings.Screens.LevelValue, level));
        }

        public string GetCommanderIdentity()
        {
            return MenuButtonTextUtility.JoinParts(
                GetText(_settings != null ? _settings.WielderNameText : null),
                GetText(_settings != null ? _settings.WielderTitleText : null));
        }

        /// <summary>The wielder's portrait at the top centre, the image the game hangs the wielder's
        /// details tooltip on (<c>CommanderLevelUpMenu.Open</c> calls <c>Portrait.SetDetails</c>);
        /// null when the menu draws none.</summary>
        public Component Portrait
        {
            get
            {
                Component portrait = _settings != null ? _settings.Portrait as Component : null;
                return portrait != null && portrait.gameObject.activeInHierarchy ? portrait : null;
            }
        }

        /// <summary>The portrait's native tooltip, the wielder's details.</summary>
        public Tooltip PortraitTooltip
        {
            get
            {
                Component portrait = Portrait;
                return portrait != null ? Tooltip.ForComponent(portrait, _localization) : null;
            }
        }

        /// <summary>The line the menu draws over the cards ("Choose a Skill"), set by
        /// <c>CommanderLevelUpMenu.Open</c> from <c>Adventure/CommanderLevelUp/Description</c>.</summary>
        public string GetChooseSkillText()
        {
            return GetText(_settings != null ? _settings.DescriptionText : null);
        }

        /// <summary>The close cross the menu's <c>AdventureMenuBackground</c> draws at the top right.
        /// It is only turned on where the background may be closed and the player is on mouse and
        /// keyboard (<c>AnimateEntry</c>).</summary>
        public Component CloseButton
        {
            get { return GetCloseButton() as Component; }
        }

        public bool IsCloseVisible()
        {
            UIButton button = GetCloseButton();
            return button != null && button.Active && ((Component)button).gameObject.activeInHierarchy;
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(GetCloseButton());
        }

        public bool IsMaxLevelMessageVisible()
        {
            return _settings != null
                && _settings.ReachedMaxXPContainer != null
                && _settings.ReachedMaxXPContainer.activeInHierarchy
                && !string.IsNullOrWhiteSpace(GetMaxLevelMessage());
        }

        public string GetMaxLevelMessage()
        {
            return GetText(_settings != null ? _settings.ReachedMaxXPText : null);
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

        public IReadOnlyList<StatItem> GetStats()
        {
            List<StatItem> items = new List<StatItem>();
            CommanderStatsInfo statsInfo = _settings != null ? _settings.StatsInfo : null;
            AddStat(items, "offense", GameText.Get(_localization, "Commanders/Tooltip/Offense", "Offence"), statsInfo, OffenseTextField, OffenseTooltipImageField);
            AddStat(items, "defense", GameText.Get(_localization, "Commanders/Tooltip/Defense", "Defence"), statsInfo, DefenceTextField, DefenceTooltipImageField);
            AddStat(items, "movement", GameText.Get(_localization, "Commanders/Tooltip/Movement", "Movement"), statsInfo, MovementTextField, MovementTooltipImageField);
            AddStat(items, "view", GameText.Get(_localization, "Commanders/Tooltip/ViewRadius", "View Radius"), statsInfo, ViewTextField, ViewTooltipImageField);
            AddStat(items, "spell-damage-power", GameText.Get(_localization, "Commanders/Tooltip/SpellDamagePower", "Spell Damage Power"), statsInfo, SpellDamagePowerTextField, SpellDamagePowerTooltipImageField);
            return items;
        }

        public IReadOnlyList<SkillChoice> GetSkillChoices()
        {
            List<SkillChoice> choices = new List<SkillChoice>();
            AddSkillChoice(choices, "left", 0, _settings != null ? _settings.LeftSkill : null);
            AddSkillChoice(choices, "middle", 1, _settings != null ? _settings.MiddleSkill : null);
            AddSkillChoice(choices, "right", 2, _settings != null ? _settings.RightSkill : null);
            return choices;
        }

        private void AddStat(
            List<StatItem> items,
            string id,
            string fallbackLabel,
            CommanderStatsInfo statsInfo,
            FieldInfo textField,
            FieldInfo tooltipField)
        {
            UITextMesh textMesh = GetField<UITextMesh>(statsInfo, textField);
            string value = GetText(textMesh);
            UIImage tooltipImage = GetField<UIImage>(statsInfo, tooltipField);
            Component tooltipComponent = tooltipImage as Component;
            if (string.IsNullOrWhiteSpace(value)
                || tooltipComponent == null
                || !tooltipComponent.gameObject.activeInHierarchy)
            {
                return;
            }

            items.Add(new StatItem(
                "level-up-stat-" + id,
                fallbackLabel,
                value,
                tooltipComponent,
                Tooltip.ForComponent(tooltipComponent, _localization)));
        }

        private void AddSkillChoice(List<SkillChoice> choices, string id, int headerIndex, CommanderLevelUpSkillComponent component)
        {
            if (component == null || !component.gameObject.activeInHierarchy)
            {
                return;
            }

            UIButton button = GetField<UIButton>(component, ButtonField);
            string choiceHeader = GetSkillChoiceHeader(headerIndex);
            string skillName = GetText(GetField<UITextMesh>(component, HeaderTextField));
            string skillLevel = GetText(GetField<UITextMesh>(component, SkillLevelTextField));
            string description = GetText(GetField<UITextMesh>(component, DescriptionTextField));
            Component buttonComponent = button as Component;

            choices.Add(new SkillChoice(
                "level-up-skill-" + id,
                choiceHeader,
                BuildSkillNameAndLevel(skillName, skillLevel),
                description,
                buttonComponent,
                () => button != null && button.Active && button.Interactable,
                () => NativeSelectionUtility.Click(button),
                () => component != null && component.gameObject.activeInHierarchy));
        }

        private string GetSkillChoiceHeader(int index)
        {
            UITextMesh[] headers = _settings != null ? _settings.SkillChoiceHeaders : null;
            return headers != null && index >= 0 && index < headers.Length
                ? GetText(headers[index])
                : string.Empty;
        }

        private bool IsBackgroundOpen()
        {
            AdventureMenuBackground background = _settings != null ? _settings.AdventureMenuBackground : null;
            if (background == null || !background.gameObject.activeInHierarchy)
            {
                return false;
            }

            if (BackgroundIsOpenField == null)
            {
                return true;
            }

            object value = BackgroundIsOpenField.GetValue(background);
            return value is bool ? (bool)value : true;
        }

        /// <summary>The card's skill line as the menu draws it: the skill's own name and the level
        /// the card would take it to.</summary>
        private static string BuildSkillNameAndLevel(string skillName, string skillLevel)
        {
            return string.IsNullOrWhiteSpace(skillLevel)
                ? skillName
                : MenuButtonTextUtility.JoinParts(skillName, ModText.Get(ModStrings.Screens.LevelValue, skillLevel));
        }

        private UIButton GetCloseButton()
        {
            AdventureMenuBackground background = _settings != null ? _settings.AdventureMenuBackground : null;
            return GetField<UIButton>(background, BackgroundCloseButtonField);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class StatItem
        {
            public StatItem(string id, string label, string value, Component target, Tooltip tooltip)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Target = target;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }

            /// <summary>The stat's name, as the game's own localization has it.</summary>
            public string Label { get; private set; }

            /// <summary>The number the stats row draws for it.</summary>
            public string Value { get; private set; }

            /// <summary>The icon the game hangs the stat's tooltip on - what draws the row.</summary>
            public Component Target { get; private set; }

            public Tooltip Tooltip { get; private set; }
        }

        public sealed class SkillChoice
        {
            public SkillChoice(
                string id,
                string header,
                string nameAndLevel,
                string description,
                Component button,
                Func<bool> isEnabled,
                Func<bool> activate,
                Func<bool> isVisible)
            {
                Id = id ?? string.Empty;
                Header = header ?? string.Empty;
                NameAndLevel = nameAndLevel ?? string.Empty;
                Description = description ?? string.Empty;
                Button = button;
                IsEnabled = isEnabled;
                Activate = activate;
                IsVisible = isVisible;
            }

            public string Id { get; private set; }

            /// <summary>The header the menu draws over the card ("New Skill", "Upgrade Command"),
            /// read off the settings' own header text mesh.</summary>
            public string Header { get; private set; }

            /// <summary>The skill the card offers and the level it would reach.</summary>
            public string NameAndLevel { get; private set; }

            /// <summary>What the card draws under the name; always drawn, never a tooltip.</summary>
            public string Description { get; private set; }

            /// <summary>The card's own button - what the game draws it with, and what a hover rests
            /// on.</summary>
            public Component Button { get; private set; }

            /// <summary>Whether the card can be chosen: a card the menu filled in with "no more
            /// skills" is turned non-interactable by <c>SetupInactive</c>.</summary>
            public Func<bool> IsEnabled { get; private set; }

            public Func<bool> Activate { get; private set; }
            public Func<bool> IsVisible { get; private set; }
        }

    }
}
