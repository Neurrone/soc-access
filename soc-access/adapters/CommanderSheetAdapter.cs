using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Skills;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommanderSheetAdapter : IPresent, IArtifactSlots
    {
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(CommanderSheet), "_facade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(CommanderSheet), "_localizationHandler");
        private static readonly FieldInfo WielderLookupField = AccessTools.Field(typeof(CommanderSheet), "_wielderLookup");
        private static readonly FieldInfo NameField = AccessTools.Field(typeof(CommanderSheet), "_wielderName");
        private static readonly FieldInfo ClassField = AccessTools.Field(typeof(CommanderSheet), "_wielderClass");
        private static readonly FieldInfo TutorialButtonField = AccessTools.Field(typeof(CommanderSheet), "_tutorialButton");
        private static readonly FieldInfo InventoryField = AccessTools.Field(typeof(CommanderSheet), "_inventory");
        private static readonly FieldInfo SkillsField = AccessTools.Field(typeof(CommanderSheet), "_skills");
        private static readonly FieldInfo SpecializationField = AccessTools.Field(typeof(CommanderSheet), "_specialization");
        private static readonly FieldInfo ModifierTabsField = AccessTools.Field(typeof(CommanderSheet), "_modifierTabNavigation");
        private static readonly FieldInfo CommanderIdField = AccessTools.Field(typeof(CommanderSheet), "_commanderId");
        private static readonly MethodInfo CloseMethod = AccessTools.Method(typeof(CommanderSheet), "Close", new[] { typeof(bool), typeof(bool) });
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");

        private static readonly FieldInfo BacteriaLookupField = AccessTools.Field(typeof(CommanderSheetSpecialization), "_bacteriaLookup");
        private static readonly FieldInfo FactionLookupField = AccessTools.Field(typeof(CommanderSheetSpecialization), "_factionLookup");
        private static readonly FieldInfo StatsInfoField = AccessTools.Field(typeof(CommanderSheetSpecialization), "_statsInfo");
        private static readonly FieldInfo OffenseTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_offenseTooltipImage");
        private static readonly FieldInfo DefenceTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_defenceTooltipImage");
        private static readonly FieldInfo MovementTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_movementTooltipImage");
        private static readonly FieldInfo ViewTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_viewTooltipImage");

        private static readonly FieldInfo SkillEntryLevelField = AccessTools.Field(typeof(CommanderSheetSkillEntry), "_levelText");
        private static readonly FieldInfo SkillEntriesField = AccessTools.Field(typeof(CommanderSheetSkills), "_entries");
        private static readonly FieldInfo PowerEntriesField = AccessTools.Field(typeof(CommanderSheetSkills), "_powerEntries");
        private static readonly FieldInfo SkillLookupField = AccessTools.Field(typeof(CommanderSheetSkills), "_skillLookup");

        private static readonly FieldInfo ModifierTitleField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_modifierTitle");
        private static readonly FieldInfo NoModifiersTextField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_noActiveModifiersText");
        private static readonly FieldInfo CurrentTabStateField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_currentTabState");
        private static readonly FieldInfo TroopModifierContentField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_troopModiferContent");
        private static readonly FieldInfo TemporaryModifierContentField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_tempModiferContent");
        private static readonly FieldInfo GearModifierContentField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_gearModiferContent");
        private static readonly FieldInfo TroopModifierButtonField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_tabButtonTroopMods");
        private static readonly FieldInfo TemporaryModifierButtonField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_tabButtonTempMods");
        private static readonly FieldInfo GearModifierButtonField = AccessTools.Field(typeof(CommanderSheetModifierTabNavigation), "_tabButtonGearMods");
        private static readonly MethodInfo SetActiveTabMethod = AccessTools.Method(typeof(CommanderSheetModifierTabNavigation), "SetActiveTab", new[] { AccessTools.Inner(typeof(CommanderSheetModifierTabNavigation), "TabState"), typeof(bool) });
        private static readonly FieldInfo SummaryEntryTextField = AccessTools.Field(typeof(CommanderSheetSummaryEntry), "_textMesh");
        private static readonly FieldInfo InventoryLookupField = AccessTools.Field(typeof(InventoryHUD), "_lookup");

        // The showing modifier tab's rows, walked at most once a frame: the build asks for them
        // and each row's live readout asks again. Keyed on the frame rather than held, because the
        // tab switch replaces the rows under the same content transform.
        private readonly FrameSweep<CommanderSheetSummaryEntry> _modifierEntries =
            new FrameSweep<CommanderSheetSummaryEntry>("commander sheet modifiers", inactiveToo: false);

        // The wielder's artifacts, read the way every menu that draws an InventoryHUD reads them.
        private readonly InventorySlotReader _slots;

        // The three bands whose rows are COMPOSED rather than read: the wielder's skills, their
        // powers and their specializations. Each row costs a localization lookup, a tooltip over the
        // drawn entry and, for a skill, the level read off the drawn text; none of it changes until
        // the wielder does or a skill goes up, and both of those are in the key. The stats band is
        // not here because its key would cost what the band does, and the modifier band is not
        // because its rows ARE the game's own text, read live off the showing tab.
        private readonly SlotSnapshot<LabeledItem> _skillRows = new SlotSnapshot<LabeledItem>();

        private readonly SlotSnapshot<LabeledItem> _powerRows = new SlotSnapshot<LabeledItem>();

        private readonly SlotSnapshot<LabeledItem> _specializationRows = new SlotSnapshot<LabeledItem>();

        private readonly CommanderSheet _sheet;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        private readonly IWielderLookup _wielderLookup;
        private readonly InventoryHUD _inventory;
        private readonly IArtifactLookup _artifactLookup;
        private readonly CommanderSheetSkills _skills;
        private readonly ISkillLookup _skillLookup;
        private readonly CommanderSheetSpecialization _specialization;
        private readonly IBacteriaLookup _bacteriaLookup;
        private readonly IFactionLookup _factionLookup;
        private readonly CommanderSheetModifierTabNavigation _modifierTabs;
        private readonly CommanderStatsInfo _statsInfo;

        public CommanderSheetAdapter(CommanderSheet sheet)
        {
            _sheet = sheet;
            _facade = Reflect.Get<IClientAdventureFacade>(sheet, FacadeField);
            _localization = Reflect.Get<ILocalizationHandler>(sheet, LocalizationField);
            _wielderLookup = Reflect.Get<IWielderLookup>(sheet, WielderLookupField);
            _inventory = Reflect.Get<InventoryHUD>(sheet, InventoryField);
            _artifactLookup = Reflect.Get<IArtifactLookup>(_inventory, InventoryLookupField);
            _slots = new InventorySlotReader(
                () => _inventory,
                () => CommanderId,
                _facade,
                _localization,
                _artifactLookup,
                "CommanderSheetAdapter",
                InventorySlotReader.MouseInstructionKeys);
            _skills = Reflect.Get<CommanderSheetSkills>(sheet, SkillsField);
            _skillLookup = Reflect.Get<ISkillLookup>(_skills, SkillLookupField);
            _specialization = Reflect.Get<CommanderSheetSpecialization>(sheet, SpecializationField);
            _bacteriaLookup = Reflect.Get<IBacteriaLookup>(_specialization, BacteriaLookupField);
            _factionLookup = Reflect.Get<IFactionLookup>(_specialization, FactionLookupField);
            _statsInfo = Reflect.Get<CommanderStatsInfo>(_specialization, StatsInfoField);
            _modifierTabs = Reflect.Get<CommanderSheetModifierTabNavigation>(sheet, ModifierTabsField);
        }

        public object SourceKey
        {
            get { return _sheet; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int CommanderId
        {
            get
            {
                int? id = _sheet != null && CommanderIdField != null
                    ? CommanderIdField.GetValue(_sheet) as int?
                    : null;
                return id.HasValue ? id.Value : -1;
            }
        }

        public bool IsPresent()
        {
            return _sheet != null
                && _sheet.IsOpen
                && ((Component)_sheet).gameObject.activeInHierarchy;
        }

        /// <summary>The wielder's name, as the sheet draws it at the top.</summary>
        public string CommanderName
        {
            get { return UITextMeshTextUtility.GetEffectiveText(Reflect.Get<UITextMesh>(_sheet, NameField)); }
        }

        /// <summary>The race and title drawn under the name ("Human Commander").</summary>
        public string CommanderClass
        {
            get { return UITextMeshTextUtility.GetEffectiveText(Reflect.Get<UITextMesh>(_sheet, ClassField)); }
        }

        /// <summary>The close cross this sheet's <c>AdventureMenuBackground</c> draws at the top right;
        /// it is only turned on where the background may be closed and the player is on mouse and
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

        private UIButton GetCloseButton()
        {
            return Reflect.Get<UIButton>(_sheet, BackgroundCloseButtonField);
        }

        /// <summary>The tutorial button the sheet draws only until the tutorial has been seen.</summary>
        public Component TutorialButton
        {
            get { return Reflect.Get<UIButton>(_sheet, TutorialButtonField) as Component; }
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = Reflect.Get<UIButton>(_sheet, TutorialButtonField);
            return button != null && ((Component)button).gameObject.activeInHierarchy;
        }

        public string GetTutorialButtonLabel()
        {
            UIButton button = Reflect.Get<UIButton>(_sheet, TutorialButtonField);
            string label = MenuButtonTextUtility.GetAllVisibleText(button);
            return string.IsNullOrWhiteSpace(label)
                ? GameText.Get(_localization, "Tutorial/CodexCategory/Tutorials", "Tutorials")
                : label;
        }

        public bool ActivateTutorial()
        {
            UIButton button = Reflect.Get<UIButton>(_sheet, TutorialButtonField);
            if (button == null || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        public bool Close()
        {
            if (_sheet == null)
            {
                return false;
            }

            CloseMethod?.Invoke(_sheet, new object[] { true, true });
            return true;
        }

        public IReadOnlyList<LabeledItem> GetStats()
        {
            List<LabeledItem> items = new List<LabeledItem>();
            ICommanderState commander = GetCommander();
            if (commander == null)
            {
                return items;
            }

            AddStat(items, StatEntryType.Offense, GameText.Get(_localization, "Commanders/Tooltip/Offense", "Offence"), commander.Stats.Offense.GetValue());
            AddStat(items, StatEntryType.Defense, GameText.Get(_localization, "Commanders/Tooltip/Defense", "Defence"), commander.Stats.Defense.GetValue());
            AddStat(items, StatEntryType.Movement, GameText.Get(_localization, "Commanders/Tooltip/Movement", "Movement"), (int)commander.Stats.Movement.GetValue());
            AddStat(items, StatEntryType.View, GameText.Get(_localization, "Commanders/Tooltip/ViewRadius", "View Radius"), (int)commander.Stats.ViewRadius.GetValue());
            return items;
        }

        public IReadOnlyList<LabeledItem> GetSpecializations()
        {
            ICommanderState commander = GetCommander();
            ICommanderDefinition definition = commander != null && _wielderLookup != null
                ? _wielderLookup.Get(commander.Reference)
                : null;
            SerializableBacteriaDef[] specializations = definition != null ? definition.Specializations : null;
            List<int> key = _specializationRows.BeginKey();
            key.Add(CommanderId);
            key.Add(specializations != null ? specializations.Length : -1);
            IReadOnlyList<LabeledItem> unchanged = _specializationRows.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            List<LabeledItem> items = new List<LabeledItem>();
            if (commander == null || _bacteriaLookup == null || _localization == null || specializations == null)
            {
                return _specializationRows.Keep(items);
            }

            for (int i = 0; i < specializations.Length; i++)
            {
                SerializableBacteriaDef specialization = specializations[i];
                IDetails details = _bacteriaLookup.GetDetails(
                    specialization.BacteriaType,
                    specialization.DurationType,
                    specialization.DurationLength,
                    new BacteriaCasterInformation { ScalingLevel = 1 });
                string text = details != null
                    ? details.GetBacteriaDescription(_localization, hasDifferentDurations: true)
                    : string.Empty;
                items.Add(new LabeledItem(SpokenLines.Clean(text)));
            }

            return _specializationRows.Keep(items);
        }

        public IReadOnlyList<ModifierCategory> GetModifierCategories()
        {
            return new[]
            {
                BuildModifierCategory("Commanders/Details/Modifiers/TroopModTitle", "Troop modifiers", 0),
                BuildModifierCategory("Commanders/Details/Modifiers/TemporaryModTitle", "Temporary modifiers", 1),
                BuildModifierCategory("Commanders/Details/Modifiers/GearModTitle", "Gear modifiers", 2)
            };
        }

        private ModifierCategory BuildModifierCategory(string key, string fallback, int index)
        {
            UIButton button = GetModifierCategoryButton(index);
            return new ModifierCategory(
                GetLocalizedText(key, fallback),
                index,
                button as Component,
                Tooltip.ForComponent(button as Component, _localization));
        }

        /// <summary>Move the game's selection onto a modifier tab WITHOUT switching to it: the switch
        /// redraws the modifier list under the bar, so arriving at a tab must not take the list the
        /// player is reading away.</summary>
        public bool SelectModifierCategory(int categoryIndex)
        {
            return NativeSelectionUtility.Select(GetModifierCategoryButton(categoryIndex) as Component);
        }

        /// <summary>Switch to a modifier tab through the game's own click, which is what
        /// <c>CommanderSheetModifierTabNavigation.SetActiveTab</c> hangs on.</summary>
        public bool ActivateModifierCategory(int categoryIndex)
        {
            UIButton button = GetModifierCategoryButton(categoryIndex);
            if (button != null && NativeSelectionUtility.Click(button))
            {
                return true;
            }

            // No button to click - the tab bar was drawn without one. Fall back to the method the
            // click would have reached.
            Type tabStateType = AccessTools.Inner(typeof(CommanderSheetModifierTabNavigation), "TabState");
            if (_modifierTabs == null || SetActiveTabMethod == null || tabStateType == null)
            {
                return false;
            }

            SetActiveTabMethod.Invoke(_modifierTabs, new[] { Enum.ToObject(tabStateType, categoryIndex), (object)false });
            return true;
        }

        public int GetActiveModifierCategoryIndex()
        {
            object state = CurrentTabStateField != null && _modifierTabs != null
                ? CurrentTabStateField.GetValue(_modifierTabs)
                : null;
            return state != null ? (int)state : 0;
        }

        public string GetActiveModifierListLabel()
        {
            UITextMesh title = Reflect.Get<UITextMesh>(_modifierTabs, ModifierTitleField);
            string label = UITextMeshTextUtility.GetEffectiveText(title);
            return SpokenLines.Clean(label);
        }

        public IReadOnlyList<LabeledItem> GetActiveModifiers()
        {
            List<LabeledItem> items = new List<LabeledItem>();
            Transform content = GetActiveModifierContent();
            if (content != null)
            {
                CommanderSheetSummaryEntry[] entries = _modifierEntries.Under(content);
                for (int i = 0; i < entries.Length; i++)
                {
                    UITextMesh text = Reflect.Get<UITextMesh>(entries[i], SummaryEntryTextField);
                    string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        items.Add(new LabeledItem(label));
                    }
                }
            }

            if (items.Count == 0)
            {
                UITextMesh noneText = Reflect.Get<UITextMesh>(_modifierTabs, NoModifiersTextField);
                string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(noneText));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    items.Add(new LabeledItem(label));
                }
            }

            return items;
        }

        public string EquipmentLabel
        {
            get { return _slots.EquipmentLabel; }
        }

        public string InventoryLabel
        {
            get { return _slots.InventoryLabel; }
        }

        public IReadOnlyList<LabeledItem> GetSkills(bool powers)
        {
            ICommanderState commander = GetCommander();
            SlotSnapshot<LabeledItem> memo = powers ? _powerRows : _skillRows;
            List<int> key = memo.BeginKey();
            key.Add(CommanderId);
            IList<SkillReference> held = commander != null ? commander.Skills as IList<SkillReference> : null;
            key.Add(held != null ? held.Count : -1);
            if (held != null)
            {
                for (int i = 0; i < held.Count; i++)
                {
                    key.Add((int)held[i].Skill);
                    key.Add(held[i].Level);
                }
            }

            IReadOnlyList<LabeledItem> unchanged = memo.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            List<LabeledItem> items = new List<LabeledItem>();
            if (commander == null || commander.Skills == null || _skillLookup == null)
            {
                return memo.Keep(items);
            }

            SkillVariant expectedVariant = powers ? SkillVariant.Power : SkillVariant.Normal;
            List<SkillReference> skills = commander.Skills
                .Where(skill => _skillLookup.GetDefinition(skill).Variant == expectedVariant)
                .ToList();

            if (!powers)
            {
                int commandIndex = skills.FindIndex(skill => skill.Skill == SkillTypes.Command);
                if (commandIndex > 0)
                {
                    SkillReference command = skills[commandIndex];
                    skills.Remove(command);
                    skills.Insert(0, command);
                }
            }

            for (int i = 0; i < skills.Count; i++)
            {
                SkillReference skill = skills[i];
                string text = GetSkillName(skill);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    int capturedIndex = i;
                    bool capturedPowers = powers;
                    items.Add(new LabeledItem(
                        text,
                        value: GetSkillLevelText(capturedPowers, capturedIndex, skill),
                        onFocus: () => SelectSkillEntry(capturedPowers, capturedIndex),
                        tooltip: Tooltip.ForComponent(GetSkillEntryComponent(capturedPowers, capturedIndex), _localization)));
                }
            }

            return memo.Keep(items);
        }

        public ICommanderState GetCommander()
        {
            return _facade != null && CommanderId >= 0 ? _facade.Commanders.Get(CommanderId) : null;
        }

        private void AddStat(List<LabeledItem> items, StatEntryType type, string label, int value)
        {
            items.Add(new LabeledItem(
                label,
                value: value.ToString(CultureInfo.CurrentCulture),
                tooltip: Tooltip.ForComponent(GetStatTooltipComponent(type), _localization)));
        }

        private Component GetStatTooltipComponent(StatEntryType type)
        {
            UIImage tooltipImage = null;
            switch (type)
            {
                case StatEntryType.Offense:
                    tooltipImage = Reflect.Get<UIImage>(_statsInfo, OffenseTooltipImageField);
                    break;
                case StatEntryType.Defense:
                    tooltipImage = Reflect.Get<UIImage>(_statsInfo, DefenceTooltipImageField);
                    break;
                case StatEntryType.Movement:
                    tooltipImage = Reflect.Get<UIImage>(_statsInfo, MovementTooltipImageField);
                    break;
                case StatEntryType.View:
                    tooltipImage = Reflect.Get<UIImage>(_statsInfo, ViewTooltipImageField);
                    break;
            }

            return tooltipImage as Component;
        }

        /// <summary>The wielder's equipment and backpack, and every gesture the game gives an
        /// artifact in them, read by the shared inventory reader.</summary>
        public IReadOnlyList<InventorySlotInfo> GetEquipmentSlots()
        {
            return _slots.GetEquipmentSlots();
        }

        public IReadOnlyList<InventorySlotInfo> GetBackpackSlots()
        {
            return _slots.GetBackpackSlots();
        }

        public DropResult DropArtifact(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return _slots.DropArtifact(movable, target);
        }

        public bool IsMainHandTwoHanded()
        {
            return _slots.IsMainHandTwoHanded();
        }

        public bool IsOffHandOnlyArtifact(InventoryArtifactMovable movable)
        {
            return _slots.IsOffHandOnlyArtifact(movable);
        }

        public string BothHandsSlotName
        {
            get { return _slots.BothHandsSlotName; }
        }

        public bool CanRearrangeArtifactTo(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return _slots.CanRearrangeArtifactTo(movable, target);
        }

        public string RearrangeRefusalText
        {
            get { return _slots.RearrangeRefusalText; }
        }

        /// <summary>Nothing on this sheet listens to the inventory's left click.</summary>
        public bool AnswersLeftClick(InventorySlotInfo slot)
        {
            return _slots.AnswersLeftClick(slot);
        }

        public bool LeftClickArtifact(InventorySlotInfo slot)
        {
            return _slots.LeftClickArtifact(slot);
        }

        public bool RightClickArtifact(InventorySlotInfo slot)
        {
            return _slots.RightClickArtifact(slot);
        }

        public ArtifactDetails.EquipInstruction GetArtifactInstruction(InventorySlotInfo slot)
        {
            return _slots.GetArtifactInstruction(slot);
        }

        public string AutoArrangeText
        {
            get { return _slots.AutoArrangeText; }
        }

        public bool AutoArrangeArtifacts()
        {
            return _slots.AutoArrangeArtifacts();
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return GameText.Get(_localization, key, fallback);
        }

        /// <summary>The level the skill's own entry DRAWS beside its name; the reference's level where
        /// the entry cannot be read.</summary>
        private string GetSkillLevelText(bool powers, int index, SkillReference skill)
        {
            UITextMesh level = Reflect.Get<UITextMesh>(GetSkillEntryComponent(powers, index), SkillEntryLevelField);
            string text = UITextMeshTextUtility.GetEffectiveText(level);
            return string.IsNullOrWhiteSpace(text) ? skill.Level.ToString(CultureInfo.CurrentCulture) : text;
        }

        private void SelectSkillEntry(bool powers, int index)
        {
            Component component = GetSkillEntryComponent(powers, index);
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        private Component GetSkillEntryComponent(bool powers, int index)
        {
            List<CommanderSheetSkillEntry> entries = powers
                ? Reflect.Get<List<CommanderSheetSkillEntry>>(_skills, PowerEntriesField)
                : Reflect.Get<List<CommanderSheetSkillEntry>>(_skills, SkillEntriesField);
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return null;
            }

            return entries[index] as Component;
        }

        private Transform GetActiveModifierContent()
        {
            int stateIndex = GetActiveModifierCategoryIndex();
            switch (stateIndex)
            {
                case 1:
                    return GetContentTransform(TemporaryModifierContentField);
                case 2:
                    return GetContentTransform(GearModifierContentField);
                default:
                    return GetContentTransform(TroopModifierContentField);
            }
        }

        private UIButton GetModifierCategoryButton(int categoryIndex)
        {
            switch (categoryIndex)
            {
                case 1:
                    return Reflect.Get<UIButton>(_modifierTabs, TemporaryModifierButtonField);
                case 2:
                    return Reflect.Get<UIButton>(_modifierTabs, GearModifierButtonField);
                default:
                    return Reflect.Get<UIButton>(_modifierTabs, TroopModifierButtonField);
            }
        }

        private Transform GetContentTransform(FieldInfo field)
        {
            GameObject content = Reflect.Get<GameObject>(_modifierTabs, field);
            return content != null ? content.transform : null;
        }

        private string GetSkillName(SkillReference skill)
        {
            if (_localization != null)
            {
                string key = skill.GetLocalizationNameKey();
                string text = _localization.GetText(key, skill.Level);
                if (!string.IsNullOrWhiteSpace(text) && text != key)
                {
                    return SpokenLines.Clean(text);
                }
            }

            return skill.Skill.ToString();
        }

        public sealed class LabeledItem
        {
            public LabeledItem(string label, string value = null, Action onFocus = null, Tooltip tooltip = null)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                OnFocus = onFocus;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }

            /// <summary>The number the game draws beside the name - a stat's value, a skill's level -
            /// kept apart from it so the screen decides how the two read together.</summary>
            public string Value { get; private set; }
            public Action OnFocus { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

        public sealed class ModifierCategory
        {
            public ModifierCategory(string label, int index, Component button = null, Tooltip tooltip = null)
            {
                Label = label;
                Index = index;
                Button = button;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public int Index { get; private set; }

            /// <summary>The tab the game draws for this category.</summary>
            public Component Button { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

    }
}
