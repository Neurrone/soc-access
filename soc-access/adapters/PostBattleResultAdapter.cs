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
        private static readonly FieldInfo PortraitWielderContainerField = AccessTools.Field(typeof(CommanderHUDPortrait), "_wielderPortraitContainer");

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
        //
        // A COUNT CANNOT TELL ONE BATTLE FROM THE NEXT, and the adventure scene shows every battle
        // on the same PostBattleMenu, so the battle's own result object is held beside it: Show
        // assigns _result per battle (PostBattleMenu ~:298) and Hide leaves it there, so two pages
        // presenting equal counts are still two results. The loot entries are POOLED rather than
        // destroyed (PostBattleLootContainer.Despawn/Spawn ~:65-88), so without this a page that
        // opened with the previous page's counts would speak the previous battle's loot names over
        // this battle's entries.
        private ResultEntry[] _attackerTroopsLost;
        private ResultEntry[] _defenderTroopsLost;
        private IBattleResult _troopResult;
        private int _troopInstanceCount = -1;
        private ResultEntry[] _loot;
        private IBattleResult _lootResult;
        private int _attackerLootCount = -1;
        private int _defenderLootCount = -1;

        // Both lists hold the names the game's own tooltips gave, so they are also let go of when the
        // language the names were read in is no longer the one the game is in.
        private ILanguageDefinition _troopLanguage;
        private ILanguageDefinition _lootLanguage;

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

        // Each side's portrait OBJECT is read off the battle menu's settings once per menu, hit or
        // miss; the battle menu holds the same two portraits for the whole adventure scene. WHOSE
        // portrait each one is changes with every battle on those same objects, so the wrapper over it is keyed on the commander the portrait is drawing, read
        // each time: SetCommander assigns _commander and turns the wielder container on
        // (CommanderHUDPortrait ~:194-197), while SetEmptyCommander - what AdventureBattleMenu uses
        // for a neutral defender (~:288) - leaves _commander as the PREVIOUS battle's wielder and
        // turns that container off (CommanderHUDPortrait ~:245-247). A miss kept for the adapter's
        // life would cost the defender's portrait in every battle after a neutral one, and the
        // identity alone would offer the last wielder's portrait for a neutral stack.
        private CommanderHUDPortrait _attackerPortraitObject;
        private bool _attackerPortraitProbed;
        private CommanderHudPortraitAdapter _attackerPortrait;
        private int _attackerPortraitCommanderId = -1;
        private CommanderHUDPortrait _defenderPortraitObject;
        private bool _defenderPortraitProbed;
        private CommanderHudPortraitAdapter _defenderPortrait;
        private int _defenderPortraitCommanderId = -1;

        public CommanderHudPortraitAdapter AttackerCommanderPortrait
        {
            get
            {
                return SyncCommanderPortrait(
                    ref _attackerPortraitObject,
                    ref _attackerPortraitProbed,
                    ref _attackerPortrait,
                    ref _attackerPortraitCommanderId,
                    attacker: true);
            }
        }

        public CommanderHudPortraitAdapter DefenderCommanderPortrait
        {
            get
            {
                return SyncCommanderPortrait(
                    ref _defenderPortraitObject,
                    ref _defenderPortraitProbed,
                    ref _defenderPortrait,
                    ref _defenderPortraitCommanderId,
                    attacker: false);
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

        /// <summary>Let go of both troop columns when the menu has made another entry, or when it is
        /// showing another battle. The menu keeps ONE list for the two sides - <c>ShowTroop</c> adds
        /// to <c>_troopInstances</c> whichever parent it draws into - so a stack appearing on either
        /// side rebuilds both, which is the animation running; once it has stopped adding and the
        /// result is still the same one, neither is walked again.</summary>
        private void SyncTroopEntries()
        {
            IBattleResult result = GetResult();
            int count = TroopInstanceCount;
            ILanguageDefinition language = _localization != null ? _localization.CurrentLanguage : null;
            if (count == _troopInstanceCount
                && ReferenceEquals(result, _troopResult)
                && ReferenceEquals(language, _troopLanguage))
            {
                return;
            }

            _troopResult = result;
            _troopInstanceCount = count;
            _troopLanguage = language;
            _attackerTroopsLost = null;
            _defenderTroopsLost = null;
        }

        /// <summary>Let go of the loot when either container's own list of active entries has changed
        /// length, or when the menu is showing another battle: the menu shows the loot in one of the
        /// two and hides the other, and each keeps the entries it has spawned.</summary>
        private void SyncLootEntries()
        {
            IBattleResult result = GetResult();
            int attacker = ActiveLootCount(Reflect.Get<PostBattleLootContainer>(_menu, AttackerLootContainerField));
            int defender = ActiveLootCount(Reflect.Get<PostBattleLootContainer>(_menu, DefenderLootContainerField));
            ILanguageDefinition language = _localization != null ? _localization.CurrentLanguage : null;
            if (attacker == _attackerLootCount
                && defender == _defenderLootCount
                && ReferenceEquals(result, _lootResult)
                && ReferenceEquals(language, _lootLanguage))
            {
                return;
            }

            _lootResult = result;
            _lootLanguage = language;
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

        /// <summary>The wrapper over one side's portrait, made afresh whenever the portrait has been
        /// handed a different commander - or none - since it was last asked. The walk that finds the
        /// portrait object stays behind <paramref name="probed"/>; what runs every frame is one field
        /// read and one active flag.</summary>
        private CommanderHudPortraitAdapter SyncCommanderPortrait(
            ref CommanderHUDPortrait portrait,
            ref bool probed,
            ref CommanderHudPortraitAdapter adapter,
            ref int seenCommanderId,
            bool attacker)
        {
            if (!probed)
            {
                probed = true;
                string settingsFieldName = attacker
                    ? "AttackerCommanderHudPortrait"
                    : "DefenderCommanderHudPortrait";
                portrait = GetBattleMenuSettingsField<CommanderHUDPortrait>(settingsFieldName);
                if (portrait == null)
                {
                    // No portrait node rather than a guessed one: nothing else in the scene says
                    // which side a CommanderHUDPortrait belongs to. Once per menu, behind the probe.
                    SocAccessMod.Instance?.LogWarning(
                        "The battle menu's settings hold no " + settingsFieldName + "; the post-battle page has no portrait for that side");
                }
            }

            int commanderId = ShownCommanderId(portrait);
            if (commanderId != seenCommanderId)
            {
                seenCommanderId = commanderId;
                adapter = commanderId < 0 ? null : BuildCommanderPortrait(portrait, attacker);
            }

            return adapter;
        }

        /// <summary>The commander the portrait is DRAWING, or -1 where it draws none. The game keeps
        /// whatever commander it was last given in <c>_commander</c> and says "no commander" by
        /// switching the wielder container off instead, which is all <c>SetEmptyCommander</c> does
        /// about it.</summary>
        private static int ShownCommanderId(CommanderHUDPortrait portrait)
        {
            if (portrait == null || portrait.Commander == null)
            {
                return -1;
            }

            GameObject container = Reflect.Get<GameObject>(portrait, PortraitWielderContainerField);
            return container != null && !container.activeSelf ? -1 : portrait.Commander.Id;
        }

        private CommanderHudPortraitAdapter BuildCommanderPortrait(CommanderHUDPortrait portrait, bool attacker)
        {
            UIButton button = CommanderHudPortraitAdapter.GetButton(portrait);
            if (button == null)
            {
                return null;
            }

            Func<string> getName;
            if (attacker)
            {
                getName = () => AttackerCommanderText;
            }
            else
            {
                getName = () => DefenderCommanderText;
            }

            return new CommanderHudPortraitAdapter(
                getName,
                portrait,
                button,
                _localization,
                () => IsPresent());
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
