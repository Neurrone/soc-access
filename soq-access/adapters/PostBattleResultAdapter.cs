using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PostBattleResultAdapter
    {
        public const string SourceKey = "POST_BATTLE_RESULT";

        private static readonly FieldInfo AdventureBattleMenuSettingsField = AccessTools.Field(typeof(AdventureBattleMenu), "_settings");
        private static readonly FieldInfo PostBattleMenuResultField = AccessTools.Field(typeof(PostBattleMenu), "_result");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(PostBattleMenu), "_headerText");
        private static readonly FieldInfo AttackerNameField = AccessTools.Field(typeof(PostBattleMenu), "_attackerName");
        private static readonly FieldInfo DefenderNameField = AccessTools.Field(typeof(PostBattleMenu), "_defenderName");
        private static readonly FieldInfo AttackerTroopsParentField = AccessTools.Field(typeof(PostBattleMenu), "_attackerTroopsParent");
        private static readonly FieldInfo DefenderTroopsParentField = AccessTools.Field(typeof(PostBattleMenu), "_defenderTroopsParent");
        private static readonly FieldInfo AttackerInfoLabelField = AccessTools.Field(typeof(PostBattleMenu), "_attackerInfoLabel");
        private static readonly FieldInfo DefenderInfoLabelField = AccessTools.Field(typeof(PostBattleMenu), "_defenderInfoLabel");
        private static readonly FieldInfo AttackerXpTextField = AccessTools.Field(typeof(PostBattleMenu), "_attackerXPText");
        private static readonly FieldInfo AttackerLootContainerField = AccessTools.Field(typeof(PostBattleMenu), "_attackerLootContainer");
        private static readonly FieldInfo DefenderLootContainerField = AccessTools.Field(typeof(PostBattleMenu), "_defenderLootContainer");
        private static readonly FieldInfo ConfirmButtonField = AccessTools.Field(typeof(PostBattleMenu), "_confirmButton");
        private static readonly FieldInfo RedoManualBattleButtonField = AccessTools.Field(typeof(PostBattleMenu), "_replayManualBattleButton");

        private static readonly FieldInfo TroopEntryAmountField = AccessTools.Field(typeof(AdventureBattleMenuTroopEntry), "_amount");
        private static readonly FieldInfo TroopEntryTooltipAreaField = AccessTools.Field(typeof(AdventureBattleMenuTroopEntry), "_tooltipArea");
        private static readonly FieldInfo LootEntryMainTransformField = AccessTools.Field(typeof(PostBattleLootEntry), "_mainTransform");

        private readonly AdventureBattleMenu _battleMenu;
        private readonly PostBattleMenu _menu;
        private readonly ILocalizationHandler _localization;

        public PostBattleResultAdapter(AdventureBattleMenu battleMenu, PostBattleMenu menu)
        {
            _battleMenu = battleMenu;
            _menu = menu;
            _localization = GlobalLocalizationVariables.LocalizationHandler;
        }

        public object Source
        {
            get { return SourceKey; }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && GetResult() != null;
        }

        public string HeaderText
        {
            get { return GetText(HeaderTextField); }
        }

        public string AttackerCommanderText
        {
            get { return GetText(AttackerNameField); }
        }

        public string DefenderCommanderText
        {
            get { return GetText(DefenderNameField); }
        }

        public Tooltip AttackerCommanderTooltip
        {
            get { return BuildCommanderTooltip("AttackerCommanderHudPortrait"); }
        }

        public Tooltip DefenderCommanderTooltip
        {
            get { return BuildCommanderTooltip("DefenderCommanderHudPortrait"); }
        }

        public string AttackerReturnedTroopsText
        {
            get { return GetText(AttackerInfoLabelField); }
        }

        public bool AttackerReturnedTroopsVisible
        {
            get { return IsTextVisible(AttackerInfoLabelField); }
        }

        public string DefenderReturnedTroopsText
        {
            get { return GetText(DefenderInfoLabelField); }
        }

        public bool DefenderReturnedTroopsVisible
        {
            get { return IsTextVisible(DefenderInfoLabelField); }
        }

        public string XpText
        {
            get { return GetText(AttackerXpTextField); }
        }

        public bool XpVisible
        {
            get { return IsTextVisible(AttackerXpTextField); }
        }

        public IReadOnlyList<ResultEntry> AttackerTroopsLost
        {
            get { return BuildTroopEntries(AttackerTroopsParentField, "post-battle-attacker-troop-lost-"); }
        }

        public IReadOnlyList<ResultEntry> DefenderTroopsLost
        {
            get { return BuildTroopEntries(DefenderTroopsParentField, "post-battle-defender-troop-lost-"); }
        }

        public IReadOnlyList<ResultEntry> Loot
        {
            get { return BuildLootEntries(); }
        }

        public ButtonWidget BuildAcceptButton()
        {
            UIButton button = GetField<UIButton>(ConfirmButtonField);
            return new ButtonWidget(
                "post-battle-accept",
                GetButtonLabel(button),
                () => NativeSelectionUtility.Click(button),
                HideNativeTooltip,
                () => IsButtonEnabled(button),
                () => IsButtonVisible(button),
                Tooltip.ForComponent(button, _localization));
        }

        public ButtonWidget BuildRedoManualBattleButton()
        {
            UIButton button = GetField<UIButton>(RedoManualBattleButtonField);
            return new ButtonWidget(
                "post-battle-redo-manual-battle",
                GetButtonLabel(button),
                () => NativeSelectionUtility.Click(button),
                HideNativeTooltip,
                () => IsButtonEnabled(button),
                () => IsButtonVisible(button),
                Tooltip.ForComponent(button, _localization));
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public static PostBattleMenu GetPostBattleMenu(AdventureBattleMenu battleMenu)
        {
            object settings = battleMenu != null && AdventureBattleMenuSettingsField != null
                ? AdventureBattleMenuSettingsField.GetValue(battleMenu)
                : null;
            if (settings == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(settings.GetType(), "PostBattleMenu");
            return field != null ? field.GetValue(settings) as PostBattleMenu : null;
        }

        private IBattleResult GetResult()
        {
            return GetField<IBattleResult>(PostBattleMenuResultField);
        }

        private Tooltip BuildCommanderTooltip(string settingsFieldName)
        {
            CommanderHUDPortrait portrait = GetBattleMenuSettingsField<CommanderHUDPortrait>(settingsFieldName);
            if (portrait == null)
            {
                portrait = ResolveCommanderPortraitByName(settingsFieldName);
            }

            return Tooltip.ForComponent(portrait, _localization);
        }

        private CommanderHUDPortrait ResolveCommanderPortraitByName(string settingsFieldName)
        {
            if (_menu == null)
            {
                return null;
            }

            CommanderHUDPortrait[] portraits = _menu.GetComponentsInParent<CommanderHUDPortrait>(true);
            if (portraits != null && portraits.Length > 0)
            {
                return portraits[0];
            }

            Transform root = _menu.transform != null ? _menu.transform.root : null;
            if (root == null)
            {
                return null;
            }

            CommanderHUDPortrait[] candidates = root.GetComponentsInChildren<CommanderHUDPortrait>(true);
            if (candidates == null || candidates.Length == 0)
            {
                return null;
            }

            if (settingsFieldName.IndexOf("Defender", StringComparison.OrdinalIgnoreCase) >= 0 && candidates.Length > 1)
            {
                return candidates[1];
            }

            return candidates[0];
        }

        private IReadOnlyList<ResultEntry> BuildTroopEntries(FieldInfo parentField, string idPrefix)
        {
            Transform parent = GetField<Transform>(parentField);
            if (parent == null)
            {
                return new ResultEntry[0];
            }

            AdventureBattleMenuTroopEntry[] entries = parent.GetComponentsInChildren<AdventureBattleMenuTroopEntry>(false);
            List<ResultEntry> result = new List<ResultEntry>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                AdventureBattleMenuTroopEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Component tooltipComponent = GetField<Component>(entry, TroopEntryTooltipAreaField);
                Tooltip tooltip = Tooltip.ForComponent(tooltipComponent, _localization);
                string label = BuildLostTroopLabel(
                    GetText(GetField<UITextMesh>(entry, TroopEntryAmountField)),
                    tooltip);
                result.Add(new ResultEntry(idPrefix + i, label, tooltip, () => entry != null && entry.gameObject.activeInHierarchy));
            }

            return result.ToArray();
        }

        private IReadOnlyList<ResultEntry> BuildLootEntries()
        {
            List<ResultEntry> result = new List<ResultEntry>();
            AddLootEntries(result, GetField<PostBattleLootContainer>(AttackerLootContainerField), "post-battle-attacker-loot-");
            AddLootEntries(result, GetField<PostBattleLootContainer>(DefenderLootContainerField), "post-battle-defender-loot-");
            return result.ToArray();
        }

        private void AddLootEntries(List<ResultEntry> result, PostBattleLootContainer container, string idPrefix)
        {
            if (result == null || container == null || !container.gameObject.activeInHierarchy)
            {
                return;
            }

            PostBattleLootEntry[] entries = container.GetComponentsInChildren<PostBattleLootEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                PostBattleLootEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Component tooltipComponent = GetField<Component>(entry, LootEntryMainTransformField);
                Tooltip tooltip = Tooltip.ForComponent(tooltipComponent, _localization);
                string label = GetFirstTooltipLine(tooltip);
                result.Add(new ResultEntry(idPrefix + i, label, tooltip, () => entry != null && entry.gameObject.activeInHierarchy));
            }
        }

        private static string BuildLostTroopLabel(string amount, Tooltip tooltip)
        {
            string name = GetFirstTooltipLine(tooltip);
            string label;
            if (string.IsNullOrWhiteSpace(amount))
            {
                label = name;
            }
            else if (string.IsNullOrWhiteSpace(name))
            {
                label = amount;
            }
            else
            {
                label = amount + " " + name;
            }

            return string.IsNullOrWhiteSpace(label) ? string.Empty : label + " lost";
        }

        private static string GetFirstTooltipLine(Tooltip tooltip)
        {
            if (tooltip == null || tooltip.TextLines == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < tooltip.TextLines.Count; i++)
            {
                string line = SpeechTextSanitizer.Normalize(tooltip.TextLines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        private string GetText(FieldInfo field)
        {
            return GetText(GetField<UITextMesh>(field));
        }

        private static string GetText(UITextMesh text)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private bool IsTextVisible(FieldInfo field)
        {
            UITextMesh text = GetField<UITextMesh>(field);
            return text != null
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(GetText(text));
        }

        private static string GetButtonLabel(UIButton button)
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(button);
        }

        private static bool IsButtonVisible(UIButton button)
        {
            return button != null && button.Active && button.gameObject.activeInHierarchy;
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable;
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return GetField<T>(_menu, field);
        }

        private static T GetField<T>(object instance, FieldInfo field) where T : class
        {
            if (instance == null || field == null)
            {
                return null;
            }

            return field.GetValue(instance) as T;
        }

        private T GetBattleMenuSettingsField<T>(string fieldName) where T : class
        {
            object settings = _battleMenu != null && AdventureBattleMenuSettingsField != null
                ? AdventureBattleMenuSettingsField.GetValue(_battleMenu)
                : null;
            if (settings == null || string.IsNullOrWhiteSpace(fieldName))
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(settings.GetType(), fieldName);
            return field != null ? field.GetValue(settings) as T : null;
        }

        internal sealed class ResultEntry
        {
            private readonly Func<bool> _isVisible;

            public ResultEntry(string id, string label, Tooltip tooltip, Func<bool> isVisible)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Tooltip = tooltip;
                _isVisible = isVisible;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public Tooltip Tooltip { get; private set; }

            public bool IsVisible
            {
                get { return _isVisible == null || _isVisible(); }
            }
        }
    }
}
