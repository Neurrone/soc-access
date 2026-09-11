using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PostBattleResultAdapter : IPresent
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
        private static readonly FieldInfo IsLocalTeamAttackerField = AccessTools.Field(typeof(PostBattleMenu), "_isLocalTeamAttacker");
        private static readonly FieldInfo AttackerLootContainerField = AccessTools.Field(typeof(PostBattleMenu), "_attackerLootContainer");
        private static readonly FieldInfo DefenderLootContainerField = AccessTools.Field(typeof(PostBattleMenu), "_defenderLootContainer");
        private static readonly FieldInfo ConfirmButtonField = AccessTools.Field(typeof(PostBattleMenu), "_confirmButton");
        private static readonly FieldInfo RedoManualBattleButtonField = AccessTools.Field(typeof(PostBattleMenu), "_replayManualBattleButton");
        private static readonly FieldInfo TroopEntryAmountField = AccessTools.Field(typeof(AdventureBattleMenuTroopEntry), "_amount");
        private static readonly FieldInfo TroopEntryTooltipAreaField = AccessTools.Field(typeof(AdventureBattleMenuTroopEntry), "_tooltipArea");
        private static readonly FieldInfo LootEntryMainTransformField = AccessTools.Field(typeof(PostBattleLootEntry), "_mainTransform");
        private static readonly FieldInfo TroopInstancesField = AccessTools.Field(typeof(PostBattleMenu), "_troopInstances");
        private static readonly FieldInfo LootContainerActiveEntriesField = AccessTools.Field(typeof(PostBattleLootContainer), "_activeEntries");

        private readonly AdventureBattleMenu _battleMenu;
        private readonly PostBattleMenu _menu;
        private readonly ILocalizationHandler _localization;

        // The lines the menu draws are made by its AnimateResults coroutine - one troop entry per
        // stack lost, then the loot - and nothing changes them once it ends. Naming one costs a
        // details capture, and the page is a graph screen that rebuilds every frame, so each column
        // is kept while the game's OWN list for it is the same length and rebuilt when it grows:
        // the menu adds one entry to _troopInstances per ShowTroop and each loot container keeps
        // its _activeEntries, so counting them is a field read a frame and no walk. Read from the
        // game rather than from a hook saying the animation has ended, which a hot reload in the
        // middle of the page would never send (AGENTS.md, "Screen Resolution").
        private ResultEntry[] _attackerTroopsLost;
        private ResultEntry[] _defenderTroopsLost;
        private int _troopInstanceCount = -1;
        private ResultEntry[] _loot;
        private int _attackerLootCount = -1;
        private int _defenderLootCount = -1;

        // The caption over each troop column is the menu's own "Title" text two levels above the
        // column; it is found once per side, MISS INCLUDED, so a menu that draws none costs one walk
        // rather than one per frame.
        private UITextMesh _attackerTroopsCaptionText;
        private bool _attackerTroopsCaptionProbed;
        private UITextMesh _defenderTroopsCaptionText;
        private bool _defenderTroopsCaptionProbed;

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

        // Resolving a portrait walks the menu's parents and the scene root, so each side is looked
        // for ONCE per menu, hit or miss: a defender without a commander has no portrait to find,
        // and the search would otherwise run again every frame.
        private CommanderHudPortraitAdapter _attackerPortrait;
        private bool _attackerPortraitProbed;
        private CommanderHudPortraitAdapter _defenderPortrait;
        private bool _defenderPortraitProbed;

        public CommanderHudPortraitAdapter AttackerCommanderPortrait
        {
            get
            {
                if (!_attackerPortraitProbed)
                {
                    _attackerPortraitProbed = true;
                    _attackerPortrait = BuildCommanderPortrait(
                        "post-battle-attacker-commander",
                        () => AttackerCommanderText,
                        "AttackerCommanderHudPortrait");
                }

                return _attackerPortrait;
            }
        }

        public CommanderHudPortraitAdapter DefenderCommanderPortrait
        {
            get
            {
                if (!_defenderPortraitProbed)
                {
                    _defenderPortraitProbed = true;
                    _defenderPortrait = BuildCommanderPortrait(
                        "post-battle-defender-commander",
                        () => DefenderCommanderText,
                        "DefenderCommanderHudPortrait");
                }

                return _defenderPortrait;
            }
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
            get { return BuildXpText(); }
        }

        public bool XpVisible
        {
            get { return IsTextVisible(AttackerXpTextField); }
        }

        public bool XpBelongsToAttacker
        {
            get { return GetFieldValue<bool>(IsLocalTeamAttackerField); }
        }

        /// <summary>The caption the menu draws over the attacker's troop column ("Troops Lost"): the
        /// "Title" text beside the column's container, two levels above the troop parent
        /// (AttackerContainer/TroopsLost/Title against .../TroopsLost/Container/AttackerTroopContainer).
        /// The menu keeps no field for it and none of its localization keys yields the same words.
        /// </summary>
        public string AttackerTroopsCaption
        {
            get
            {
                return TroopsCaption(
                    AttackerTroopsParentField,
                    ref _attackerTroopsCaptionText,
                    ref _attackerTroopsCaptionProbed);
            }
        }

        /// <summary>The caption over the defender's troop column.</summary>
        public string DefenderTroopsCaption
        {
            get
            {
                return TroopsCaption(
                    DefenderTroopsParentField,
                    ref _defenderTroopsCaptionText,
                    ref _defenderTroopsCaptionProbed);
            }
        }

        private string TroopsCaption(FieldInfo parentField, ref UITextMesh cached, ref bool probed)
        {
            if (!probed)
            {
                probed = true;
                Transform parent = Reflect.Get<Transform>(_menu, parentField);
                Transform band = parent != null && parent.parent != null ? parent.parent.parent : null;
                Transform title = band != null ? band.Find("Title") : null;
                cached = title != null ? title.GetComponent<UITextMesh>() : null;
            }

            UITextMesh text = cached;
            return text != null && text.gameObject.activeInHierarchy
                ? SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text))
                : string.Empty;
        }

        public IReadOnlyList<ResultEntry> AttackerTroopsLost
        {
            get
            {
                SyncTroopEntries();
                return _attackerTroopsLost
                    ?? (_attackerTroopsLost = BuildTroopEntries(AttackerTroopsParentField));
            }
        }

        public IReadOnlyList<ResultEntry> DefenderTroopsLost
        {
            get
            {
                SyncTroopEntries();
                return _defenderTroopsLost
                    ?? (_defenderTroopsLost = BuildTroopEntries(DefenderTroopsParentField));
            }
        }

        public IReadOnlyList<ResultEntry> Loot
        {
            get
            {
                SyncLootEntries();
                return _loot ?? (_loot = BuildLootEntries());
            }
        }

        /// <summary>Let go of both troop columns when the menu has made another entry. The menu keeps
        /// ONE list for the two sides - <c>ShowTroop</c> adds to <c>_troopInstances</c> whichever
        /// parent it draws into - so a stack appearing on either side rebuilds both, which is the
        /// animation running; once it has stopped adding, neither is walked again.</summary>
        private void SyncTroopEntries()
        {
            int count = TroopInstanceCount;
            if (count == _troopInstanceCount)
            {
                return;
            }

            _troopInstanceCount = count;
            _attackerTroopsLost = null;
            _defenderTroopsLost = null;
        }

        /// <summary>Let go of the loot when either container's own list of active entries has changed
        /// length: the menu shows the loot in one of the two and hides the other, and each keeps the
        /// entries it has spawned.</summary>
        private void SyncLootEntries()
        {
            int attacker = ActiveLootCount(Reflect.Get<PostBattleLootContainer>(_menu, AttackerLootContainerField));
            int defender = ActiveLootCount(Reflect.Get<PostBattleLootContainer>(_menu, DefenderLootContainerField));
            if (attacker == _attackerLootCount && defender == _defenderLootCount)
            {
                return;
            }

            _attackerLootCount = attacker;
            _defenderLootCount = defender;
            _loot = null;
        }

        private int TroopInstanceCount
        {
            get
            {
                List<AdventureBattleMenuTroopEntry> entries =
                    Reflect.Get<List<AdventureBattleMenuTroopEntry>>(_menu, TroopInstancesField);
                return entries != null ? entries.Count : -1;
            }
        }

        private static int ActiveLootCount(PostBattleLootContainer container)
        {
            List<PostBattleLootEntry> entries =
                Reflect.Get<List<PostBattleLootEntry>>(container, LootContainerActiveEntriesField);
            return entries != null ? entries.Count : -1;
        }

        public string AcceptButtonLabel
        {
            get { return GetButtonLabel(Reflect.Get<UIButton>(_menu, ConfirmButtonField)); }
        }

        /// <summary>The drawn Accept button, for the screen to key a control on, sort by and select.
        /// </summary>
        public Component AcceptButton
        {
            get { return Reflect.Get<UIButton>(_menu, ConfirmButtonField) as Component; }
        }

        public bool Accept()
        {
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(_menu, ConfirmButtonField));
        }

        public bool IsAcceptButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabled(Reflect.Get<UIButton>(_menu, ConfirmButtonField));
        }

        public bool IsAcceptButtonVisible()
        {
            return MenuButtonAdapterBase.IsButtonDrawn(Reflect.Get<UIButton>(_menu, ConfirmButtonField));
        }

        public Tooltip AcceptButtonTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_menu, ConfirmButtonField), _localization); }
        }

        public string RedoManualBattleButtonLabel
        {
            get { return GetButtonLabel(Reflect.Get<UIButton>(_menu, RedoManualBattleButtonField)); }
        }

        /// <summary>The drawn Manual Battle button, for the screen to key a control on, sort by and
        /// select.</summary>
        public Component RedoManualBattleButton
        {
            get { return Reflect.Get<UIButton>(_menu, RedoManualBattleButtonField) as Component; }
        }

        public bool RedoManualBattle()
        {
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(_menu, RedoManualBattleButtonField));
        }

        public bool IsRedoManualBattleButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabled(Reflect.Get<UIButton>(_menu, RedoManualBattleButtonField));
        }

        public bool IsRedoManualBattleButtonVisible()
        {
            return MenuButtonAdapterBase.IsButtonDrawn(Reflect.Get<UIButton>(_menu, RedoManualBattleButtonField));
        }

        public Tooltip RedoManualBattleButtonTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_menu, RedoManualBattleButtonField), _localization); }
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
            return Reflect.Get<IBattleResult>(_menu, PostBattleMenuResultField);
        }

        private CommanderHudPortraitAdapter BuildCommanderPortrait(string id, Func<string> getName, string settingsFieldName)
        {
            CommanderHUDPortrait portrait = GetBattleMenuSettingsField<CommanderHUDPortrait>(settingsFieldName);
            if (portrait == null)
            {
                portrait = ResolveCommanderPortraitByName(settingsFieldName);
            }

            if (portrait == null || portrait.Commander == null)
            {
                return null;
            }

            UIButton button = CommanderHudPortraitAdapter.GetButton(portrait);
            if (button == null)
            {
                return null;
            }

            return new CommanderHudPortraitAdapter(
                id,
                getName,
                portrait,
                button,
                _localization,
                () => IsPresent());
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

        private ResultEntry[] BuildTroopEntries(FieldInfo parentField)
        {
            Transform parent = Reflect.Get<Transform>(_menu, parentField);
            if (parent == null)
            {
                return new ResultEntry[0];
            }

            // Read once per menu, behind the lost-troop lists the page snapshots when it arrives:
            // the game builds these rows with the result and leaves them alone.
            AdventureBattleMenuTroopEntry[] entries = parent.GetComponentsInChildren<AdventureBattleMenuTroopEntry>(false);
            List<ResultEntry> result = new List<ResultEntry>(entries.Length);
            for (int i = 0; i < entries.Length; i++)
            {
                AdventureBattleMenuTroopEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Component tooltipComponent = Reflect.Get<Component>(entry, TroopEntryTooltipAreaField);
                Tooltip tooltip = Tooltip.ForComponent(tooltipComponent, _localization);
                result.Add(new ResultEntry(
                    UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(entry, TroopEntryAmountField)),
                    TooltipLines.First(tooltip),
                    isLostTroop: true,
                    tooltip,
                    () => entry != null && entry.gameObject.activeInHierarchy,
                    entry));
            }

            return result.ToArray();
        }

        private ResultEntry[] BuildLootEntries()
        {
            List<ResultEntry> result = new List<ResultEntry>();
            AddLootEntries(result, Reflect.Get<PostBattleLootContainer>(_menu, AttackerLootContainerField));
            AddLootEntries(result, Reflect.Get<PostBattleLootContainer>(_menu, DefenderLootContainerField));
            return result.ToArray();
        }

        private void AddLootEntries(List<ResultEntry> result, PostBattleLootContainer container)
        {
            if (result == null || container == null || !container.gameObject.activeInHierarchy)
            {
                return;
            }

            // Read once per menu, behind the loot snapshot, for the same reason.
            PostBattleLootEntry[] entries = container.GetComponentsInChildren<PostBattleLootEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                PostBattleLootEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                Component tooltipComponent = Reflect.Get<Component>(entry, LootEntryMainTransformField);
                Tooltip tooltip = Tooltip.ForComponent(tooltipComponent, _localization);
                result.Add(new ResultEntry(
                    string.Empty,
                    GetLootEntryName(tooltipComponent, tooltip),
                    isLostTroop: false,
                    tooltip,
                    () => entry != null && entry.gameObject.activeInHierarchy,
                    entry));
            }
        }

        private string GetLootEntryName(Component tooltipComponent, Tooltip tooltip)
        {
            IDetails details;
            string artifactName;
            if (NativeTooltipUtility.TryGetUiDetails(tooltipComponent, out details)
                && ArtifactSpeechFormatter.TryFormatName(details, _localization, out artifactName))
            {
                return artifactName;
            }

            return TooltipLines.First(tooltip);
        }

        private string BuildXpText()
        {
            string text = GetText(AttackerXpTextField);
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            int amount;
            return TryParsePositiveInt(text, out amount) && _localization != null
                ? SpokenLines.Clean(_localization.GetText("Common/Stats/xp", amount))
                : text;
        }

        private static bool TryParsePositiveInt(string text, out int value)
        {
            value = 0;
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            StringBuilder digits = new StringBuilder(text.Length);
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c >= '0' && c <= '9')
                {
                    digits.Append(c);
                }
            }

            return digits.Length > 0 && int.TryParse(digits.ToString(), out value);
        }

        private string GetText(FieldInfo field)
        {
            return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, field));
        }

        private bool IsTextVisible(FieldInfo field)
        {
            UITextMesh text = Reflect.Get<UITextMesh>(_menu, field);
            return text != null
                && text.gameObject.activeInHierarchy
                && !string.IsNullOrWhiteSpace(UITextMeshTextUtility.Spoken(text));
        }

        private static string GetButtonLabel(UIButton button)
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(button);
        }

        private T GetFieldValue<T>(FieldInfo field)
        {
            if (_menu == null || field == null)
            {
                return default(T);
            }

            object value = field.GetValue(_menu);
            return value is T ? (T)value : default(T);
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

        public sealed class ResultEntry
        {
            private readonly Func<bool> _isVisible;

            public ResultEntry(
                string amount,
                string name,
                bool isLostTroop,
                Tooltip tooltip,
                Func<bool> isVisible,
                Component subject)
            {
                Amount = amount ?? string.Empty;
                Name = name ?? string.Empty;
                IsLostTroop = isLostTroop;
                Tooltip = tooltip;
                _isVisible = isVisible;
                Subject = subject;
            }

            public string Amount { get; private set; }

            public string Name { get; private set; }

            public bool IsLostTroop { get; private set; }

            public Tooltip Tooltip { get; private set; }

            /// <summary>The entry the menu drew this line as - the troop entry, the loot entry - so a
            /// caller can key a control on it and vouch for it being drawn.</summary>
            public Component Subject { get; private set; }

            public bool IsVisible
            {
                get { return _isVisible == null || _isVisible(); }
            }
        }
    }
}
