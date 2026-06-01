using System;
using System.Collections.Generic;
using System.Collections;
using System.Linq;
using System.Reflection;
using HarmonyLib;
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
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CommanderSheetAdapter
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

        private static readonly FieldInfo BacteriaLookupField = AccessTools.Field(typeof(CommanderSheetSpecialization), "_bacteriaLookup");
        private static readonly FieldInfo FactionLookupField = AccessTools.Field(typeof(CommanderSheetSpecialization), "_factionLookup");
        private static readonly FieldInfo StatsInfoField = AccessTools.Field(typeof(CommanderSheetSpecialization), "_statsInfo");
        private static readonly FieldInfo OffenseTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_offenseTooltipImage");
        private static readonly FieldInfo DefenceTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_defenceTooltipImage");
        private static readonly FieldInfo MovementTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_movementTooltipImage");
        private static readonly FieldInfo ViewTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_viewTooltipImage");

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
        private static readonly FieldInfo InventoryCommandProcessorField = AccessTools.Field(typeof(InventoryHUD), "_commandProcessor");
        private static readonly FieldInfo InventoryArtifactMapField = AccessTools.Field(typeof(InventoryHUD), "_artifactStateToGOMap");

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
            _facade = GetField<IClientAdventureFacade>(sheet, FacadeField);
            _localization = GetField<ILocalizationHandler>(sheet, LocalizationField);
            _wielderLookup = GetField<IWielderLookup>(sheet, WielderLookupField);
            _inventory = GetField<InventoryHUD>(sheet, InventoryField);
            _artifactLookup = GetField<IArtifactLookup>(_inventory, InventoryLookupField);
            _skills = GetField<CommanderSheetSkills>(sheet, SkillsField);
            _skillLookup = GetField<ISkillLookup>(_skills, SkillLookupField);
            _specialization = GetField<CommanderSheetSpecialization>(sheet, SpecializationField);
            _bacteriaLookup = GetField<IBacteriaLookup>(_specialization, BacteriaLookupField);
            _factionLookup = GetField<IFactionLookup>(_specialization, FactionLookupField);
            _statsInfo = GetField<CommanderStatsInfo>(_specialization, StatsInfoField);
            _modifierTabs = GetField<CommanderSheetModifierTabNavigation>(sheet, ModifierTabsField);
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

        public string GetCommanderIdentity()
        {
            UITextMesh name = GetField<UITextMesh>(_sheet, NameField);
            UITextMesh commanderClass = GetField<UITextMesh>(_sheet, ClassField);
            return MenuButtonTextUtility.JoinParts(
                UITextMeshTextUtility.GetEffectiveText(name),
                UITextMeshTextUtility.GetEffectiveText(commanderClass));
        }

        public bool IsTutorialButtonVisible()
        {
            UIButton button = GetField<UIButton>(_sheet, TutorialButtonField);
            return button != null && ((Component)button).gameObject.activeInHierarchy;
        }

        public string GetTutorialButtonLabel()
        {
            UIButton button = GetField<UIButton>(_sheet, TutorialButtonField);
            string label = MenuButtonTextUtility.GetAllVisibleText(button);
            return string.IsNullOrWhiteSpace(label)
                ? GameText.Get(_localization, "Tutorial/CodexCategory/Tutorials", "Tutorials")
                : label;
        }

        public bool ActivateTutorial()
        {
            UIButton button = GetField<UIButton>(_sheet, TutorialButtonField);
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

            AddStat(items, commander, StatEntryType.Offense, GameText.Get(_localization, "Commanders/Tooltip/Offense", "Offence"), commander.Stats.Offense.GetValue(), commander.Stats.Offense.OriginalValue);
            AddStat(items, commander, StatEntryType.Defense, GameText.Get(_localization, "Commanders/Tooltip/Defense", "Defence"), commander.Stats.Defense.GetValue(), commander.Stats.Defense.OriginalValue);
            AddStat(items, commander, StatEntryType.Movement, GameText.Get(_localization, "Commanders/Tooltip/Movement", "Movement"), (int)commander.Stats.Movement.GetValue(), (int)commander.Stats.Movement.OriginalValue);
            AddStat(items, commander, StatEntryType.View, GameText.Get(_localization, "Commanders/Tooltip/ViewRadius", "View Radius"), (int)commander.Stats.ViewRadius.GetValue(), (int)commander.Stats.ViewRadius.OriginalValue);
            return items;
        }

        public IReadOnlyList<LabeledItem> GetSpecializations()
        {
            List<LabeledItem> items = new List<LabeledItem>();
            ICommanderState commander = GetCommander();
            if (commander == null || _wielderLookup == null || _bacteriaLookup == null || _localization == null)
            {
                return items;
            }

            ICommanderDefinition definition = _wielderLookup.Get(commander.Reference);
            SerializableBacteriaDef[] specializations = definition != null ? definition.Specializations : null;
            if (specializations == null)
            {
                return items;
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
                items.Add(new LabeledItem("specialization-" + i, SpeechTextSanitizer.Normalize(text)));
            }

            return items;
        }

        public IReadOnlyList<ModifierCategory> GetModifierCategories()
        {
            return new[]
            {
                new ModifierCategory("modifier-category-troop", "Troop modifiers", 0, Tooltip.ForComponent(GetModifierCategoryButton(0) as Component, _localization)),
                new ModifierCategory("modifier-category-temporary", "Temporary modifiers", 1, Tooltip.ForComponent(GetModifierCategoryButton(1) as Component, _localization)),
                new ModifierCategory("modifier-category-gear", "Gear modifiers", 2, Tooltip.ForComponent(GetModifierCategoryButton(2) as Component, _localization))
            };
        }

        public bool FocusModifierCategory(int categoryIndex)
        {
            if (_modifierTabs == null)
            {
                return false;
            }

            UIButton button = GetModifierCategoryButton(categoryIndex);
            NativeSelectionUtility.Select(button as Component);

            if (GetActiveModifierCategoryIndex() == categoryIndex)
            {
                return true;
            }

            if (button != null)
            {
                button.OnSubmit(EventSystem.current != null ? new BaseEventData(EventSystem.current) : null);
                return true;
            }

            if (SetActiveTabMethod == null)
            {
                return false;
            }

            Type tabStateType = AccessTools.Inner(typeof(CommanderSheetModifierTabNavigation), "TabState");
            if (tabStateType == null)
            {
                return false;
            }

            object tabState = Enum.ToObject(tabStateType, categoryIndex);
            SetActiveTabMethod.Invoke(_modifierTabs, new[] { tabState, (object)false });
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
            UITextMesh title = GetField<UITextMesh>(_modifierTabs, ModifierTitleField);
            string label = UITextMeshTextUtility.GetEffectiveText(title);
            return string.IsNullOrWhiteSpace(label) ? "Modifiers" : label;
        }

        public IReadOnlyList<LabeledItem> GetActiveModifiers()
        {
            List<LabeledItem> items = new List<LabeledItem>();
            Transform content = GetActiveModifierContent();
            if (content != null)
            {
                CommanderSheetSummaryEntry[] entries = ((Component)content).GetComponentsInChildren<CommanderSheetSummaryEntry>(false);
                for (int i = 0; i < entries.Length; i++)
                {
                    UITextMesh text = GetField<UITextMesh>(entries[i], SummaryEntryTextField);
                    string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        items.Add(new LabeledItem("modifier-" + i, label));
                    }
                }
            }

            if (items.Count == 0)
            {
                UITextMesh noneText = GetField<UITextMesh>(_modifierTabs, NoModifiersTextField);
                string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(noneText));
                items.Add(new LabeledItem("modifiers-none", string.IsNullOrWhiteSpace(label) ? "None" : label));
            }

            return items;
        }

        public string EquipmentLabel
        {
            get { return GetLocalizedText("Common/CommanderInventory/Equipment", "Equipment"); }
        }

        public string InventoryLabel
        {
            get { return GetInventoryLabel(); }
        }

        public IReadOnlyList<LabeledItem> GetSkills(bool powers)
        {
            List<LabeledItem> items = new List<LabeledItem>();
            ICommanderState commander = GetCommander();
            if (commander == null || commander.Skills == null || _skillLookup == null)
            {
                return items;
            }

            SkillVariant expectedVariant = powers ? SkillVariant.Power : SkillVariant.Normal;
            List<SkillReference> skills = commander.Skills
                .Where(skill => _skillLookup.GetDefinition(skill).Variant == expectedVariant)
                .ToList();

            if (!powers)
            {
                int commandIndex = skills.FindIndex(skill => (int)skill.Skill == 12);
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
                        (powers ? "power-" : "skill-") + i,
                        text,
                        onFocus: () => SelectSkillEntry(capturedPowers, capturedIndex),
                        tooltip: Tooltip.ForComponent(GetSkillEntryComponent(capturedPowers, capturedIndex), _localization)));
                }
            }

            return items;
        }

        public ICommanderState GetCommander()
        {
            return _facade != null && CommanderId >= 0 ? _facade.Commanders.Get(CommanderId) : null;
        }

        private void AddStat(List<LabeledItem> items, ICommanderState commander, StatEntryType type, string label, int value, int originalValue)
        {
            items.Add(new LabeledItem(
                "stat-" + type,
                label + ", " + value,
                tooltip: Tooltip.ForComponent(GetStatTooltipComponent(type), _localization)));
        }

        private Component GetStatTooltipComponent(StatEntryType type)
        {
            UIImage tooltipImage = null;
            switch (type)
            {
                case StatEntryType.Offense:
                    tooltipImage = GetField<UIImage>(_statsInfo, OffenseTooltipImageField);
                    break;
                case StatEntryType.Defense:
                    tooltipImage = GetField<UIImage>(_statsInfo, DefenceTooltipImageField);
                    break;
                case StatEntryType.Movement:
                    tooltipImage = GetField<UIImage>(_statsInfo, MovementTooltipImageField);
                    break;
                case StatEntryType.View:
                    tooltipImage = GetField<UIImage>(_statsInfo, ViewTooltipImageField);
                    break;
            }

            return tooltipImage as Component;
        }

        public IReadOnlyList<InventorySlotInfo> GetEquipmentSlots()
        {
            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            InventorySlot[] slots =
            {
                InventorySlot.Head,
                InventorySlot.Chest,
                InventorySlot.Hands,
                InventorySlot.MainHand,
                InventorySlot.OffHand,
                InventorySlot.Feet,
                InventorySlot.Trinket1,
                InventorySlot.Trinket2,
                InventorySlot.Trinket3
            };

            string ownerName = GetCommanderName();
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlot slot = slots[i];
                InventoryHUDSlot nativeSlot = _inventory != null ? _inventory.GetSlot(slot) : null;
                IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(slot);
                bool displayOnly = IsDisplayOnlyEquipmentArtifact(slot, artifact);
                InventoryArtifactMovable artifactMovable = GetArtifactMovable(artifact);
                InventoryArtifactMovable movable = displayOnly ? null : artifactMovable;
                InventorySlot capturedSlot = slot;
                InventoryHUDSlot capturedNativeSlot = nativeSlot;
                InventoryArtifactMovable capturedMovable = movable;
                Selectable tooltipSelectable = movable != null
                    ? movable.GetSelectable()
                    : displayOnly && artifactMovable != null
                        ? artifactMovable.GetSelectable()
                        : GetEquipmentSlotSelectable(capturedSlot);
                slotsInfo.Add(new InventorySlotInfo(
                    CommanderId,
                    ownerName,
                    slot,
                    0,
                    isBackpackSlot: false,
                    GetInventorySlotName(slot),
                    GetInventoryLabel(),
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildEquipmentTooltip(artifact, tooltipSelectable, artifactMovable),
                    () => SelectInventoryCell(capturedNativeSlot, capturedMovable, 0)));
            }

            return slotsInfo;
        }

        public IReadOnlyList<InventorySlotInfo> GetBackpackSlots()
        {
            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            InventoryHUDSlot nativeSlot = _inventory != null ? _inventory.GetSlot(InventorySlot.None) : null;
            string ownerName = GetCommanderName();
            int cellCount = nativeSlot != null ? nativeSlot.CellsCount : 0;
            for (int i = 0; i < cellCount; i++)
            {
                IArtifactState artifact = GetArtifactsForSlot(InventorySlot.None).FirstOrDefault(x => x.PositionIndex == i);
                InventoryArtifactMovable movable = GetArtifactMovable(artifact);
                int capturedIndex = i;
                InventoryArtifactMovable capturedMovable = movable;
                slotsInfo.Add(new InventorySlotInfo(
                    CommanderId,
                    ownerName,
                    InventorySlot.None,
                    i,
                    isBackpackSlot: true,
                    string.Empty,
                    GetInventoryLabel(),
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildEquipmentTooltip(artifact, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i), movable),
                    () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
            }

            return slotsInfo;
        }

        private void SelectInventoryCell(InventoryHUDSlot nativeSlot, InventoryArtifactMovable movable, int positionIndex)
        {
            if (movable != null)
            {
                NativeSelectionUtility.Select(movable.GetSelectable());
                return;
            }

            Selectable selectable = GetInventorySlotSelectable(nativeSlot, positionIndex);
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        private Selectable GetEquipmentSlotSelectable(InventorySlot slot)
        {
            InventoryHUDSlot nativeSlot = _inventory != null ? _inventory.GetSlot(slot) : null;
            return nativeSlot != null ? nativeSlot.GetFirstSelectable() : null;
        }

        private Selectable GetInventorySlotSelectable(InventoryHUDSlot nativeSlot, int positionIndex)
        {
            InventoryHUDGridEntry entry = nativeSlot != null ? nativeSlot.TryGetEntry(positionIndex) : null;
            return entry != null ? (Selectable)entry : null;
        }

        public DropResult DropInventoryArtifact(InventorySlotInfo source, InventorySlotInfo target)
        {
            return ArtifactDropUtility.DropInventoryArtifact(_facade, source, target, "CommanderSheetAdapter artifact grid drop");
        }

        private Tooltip BuildEquipmentTooltip(IArtifactState artifact, Selectable selectable, InventoryArtifactMovable movable)
        {
            Tooltip tooltip = Tooltip.ForComponent(selectable, _localization);
            if (tooltip == null || artifact == null || movable == null || _localization == null)
            {
                return tooltip;
            }

            List<TooltipAction> actions = new List<TooltipAction>();
            List<string> instructionLines = new List<string>();

            // ArtifactDetails renders these activation hints as ordinary tooltip
            // rows. When this adapter supports the corresponding action, remove
            // the native instruction row by comparing against the same localized
            // string key the game used to draw it. Do not compare English text:
            // unsupported or unrecognized rows must remain in TextLines so the
            // player still hears that something may be available. Do not rely on
            // captured InputType here: some real artifact actions are drawn with
            // InputType.NoInput, so the input hint alone is not a reliable action
            // signal for equipment.
            string equipInstructionKey = artifact.IsEquipped
                ? "Adventure/TooltipInstruction/Unequip"
                : "Adventure/TooltipInstruction/Equip";
            string equipLabel = GetLocalizedText(equipInstructionKey, artifact.IsEquipped ? "Unequip" : "Equip");
            AddLocalizedLine(instructionLines, equipInstructionKey);
            actions.Add(new TooltipAction(equipLabel, () => InvokeArtifactAction(movable, _inventory.EquipArtifact)));

            if (artifact.IsImportant)
            {
                return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata, actions);
            }

            if (_inventory.IsArtifactShopInventory)
            {
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Sell");
                actions.Add(new TooltipAction(
                    ModText.Get(_localization, ModStrings.Screens.Sell),
                    () => InvokeArtifactAction(movable, _inventory.SellArtifact)));
            }
            else
            {
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Destroy");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Destroy.Gamepad");
                actions.Add(new TooltipAction(
                    GetLocalizedText("Adventure/TooltipInstruction/Destroy.Gamepad", "Destroy"),
                    () => InvokeArtifactAction(movable, _inventory.DestroyArtifact)));
            }

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Drop");
            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Drop.Gamepad");
            actions.Add(new TooltipAction(
                GetLocalizedText("Adventure/TooltipInstruction/Drop.Gamepad", "Drop"),
                () => InvokeArtifactAction(movable, _inventory.DropArtifact)));

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/AutoArrange");
            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/AutoArrange.Gamepad");
            actions.Add(new TooltipAction(
                GetLocalizedText("Adventure/TooltipInstruction/AutoArrange.Gamepad", "Auto Arrange"),
                () => InvokeArtifactAction(movable, _inventory.AutoArrangeArtifacts)));

            return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata, actions);
        }

        private InventoryArtifactMovable GetArtifactMovable(IArtifactState artifact)
        {
            if (artifact == null || InventoryArtifactMapField == null || _inventory == null)
            {
                return null;
            }

            IDictionary artifactMap = InventoryArtifactMapField.GetValue(_inventory) as IDictionary;
            if (artifactMap == null || !artifactMap.Contains(artifact))
            {
                return null;
            }

            return artifactMap[artifact] as InventoryArtifactMovable;
        }

        private static bool InvokeArtifactAction(InventoryArtifactMovable movable, Action<InventoryArtifactMovable> action)
        {
            if (movable == null || action == null)
            {
                return false;
            }

            action(movable);
            return true;
        }

        private void AddLocalizedLine(List<string> lines, string key)
        {
            string line = _localization != null ? _localization.GetText(key) : string.Empty;
            if (!string.IsNullOrWhiteSpace(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return GameText.Get(_localization, key, fallback);
        }

        private static IReadOnlyList<string> RemoveExactLines(IReadOnlyList<string> lines, IReadOnlyList<string> linesToRemove)
        {
            if (lines == null || lines.Count == 0 || linesToRemove == null || linesToRemove.Count == 0)
            {
                return lines ?? new string[0];
            }

            List<string> result = new List<string>();
            for (int i = 0; i < lines.Count; i++)
            {
                string line = lines[i];
                if (!ContainsExact(linesToRemove, line))
                {
                    result.Add(line);
                }
            }

            return result;
        }

        private static bool ContainsExact(IReadOnlyList<string> lines, string candidate)
        {
            for (int i = 0; i < lines.Count; i++)
            {
                if (string.Equals(lines[i], candidate, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
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
                ? GetField<List<CommanderSheetSkillEntry>>(_skills, PowerEntriesField)
                : GetField<List<CommanderSheetSkillEntry>>(_skills, SkillEntriesField);
            if (entries == null || index < 0 || index >= entries.Count)
            {
                return null;
            }

            return entries[index] as Component;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
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
                    return GetField<UIButton>(_modifierTabs, TemporaryModifierButtonField);
                case 2:
                    return GetField<UIButton>(_modifierTabs, GearModifierButtonField);
                default:
                    return GetField<UIButton>(_modifierTabs, TroopModifierButtonField);
            }
        }

        private Transform GetContentTransform(FieldInfo field)
        {
            GameObject content = GetField<GameObject>(_modifierTabs, field);
            return content != null ? content.transform : null;
        }

        private IEnumerable<IArtifactState> GetArtifactsForSlot(InventorySlot slot)
        {
            if (_facade == null || CommanderId < 0)
            {
                return new IArtifactState[0];
            }

            return _facade.Artifacts.GetForOwner(CommanderId, slot) ?? new IArtifactState[0];
        }

        private IArtifactState GetDisplayArtifactForEquipmentSlot(InventorySlot slot)
        {
            if (_facade == null || CommanderId < 0)
            {
                return null;
            }

            if (slot == InventorySlot.OffHand)
            {
                return _facade.Artifacts.GetForOwner(CommanderId, ArtifactSlot.OffHand).FirstOrDefault();
            }

            return GetArtifactsForSlot(slot).FirstOrDefault();
        }

        private static bool IsDisplayOnlyEquipmentArtifact(InventorySlot slot, IArtifactState artifact)
        {
            return slot == InventorySlot.OffHand
                && artifact != null
                && artifact.EquippedInSlot == InventorySlot.MainHand;
        }

        private string GetArtifactName(IArtifactState artifact)
        {
            if (artifact == null)
            {
                return string.Empty;
            }

            try
            {
                return ArtifactSpeechFormatter.FormatName(artifact, _artifactLookup, _localization);
            }
            catch (Exception ex)
            {
                SocAccessPlugin.Instance?.LogWarning("CommanderSheetAdapter could not get artifact rarity color: " + ex.Message);
                return _artifactLookup != null ? _artifactLookup.GetLocalizedName(artifact.Type) : artifact.Type.ToString();
            }
        }

        private string GetCommanderName()
        {
            ICommanderState commander = GetCommander();
            string name = commander != null && _facade != null ? _facade.Commanders.GetName(commander.Id) : string.Empty;
            return SpeechTextSanitizer.Normalize(name);
        }

        private string GetInventoryLabel()
        {
            return GetLocalizedText("Common/CommanderInventory/Inventory", "Inventory");
        }

        private string GetSkillName(SkillReference skill)
        {
            if (_localization != null)
            {
                string key = skill.GetLocalizationNameKey();
                string text = _localization.GetText(key, skill.Level);
                if (!string.IsNullOrWhiteSpace(text) && text != key)
                {
                    return SpeechTextSanitizer.Normalize(text);
                }
            }

            return skill.Skill.ToString();
        }

        private string GetInventorySlotName(InventorySlot slot)
        {
            if (_localization != null)
            {
                string text = _localization.GetText("InventorySlots/" + slot);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return SpeechTextSanitizer.Normalize(text);
                }
            }

            return slot.ToString();
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        internal sealed class LabeledItem
        {
            public LabeledItem(string id, string label, string status = null, Action onFocus = null, Func<bool> activate = null, Tooltip tooltip = null)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                OnFocus = onFocus;
                Activate = activate;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public string Status { get; private set; }
            public Action OnFocus { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

        internal sealed class ModifierCategory
        {
            public ModifierCategory(string id, string label, int index, Tooltip tooltip = null)
            {
                Id = id;
                Label = label;
                Index = index;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public int Index { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

    }
}
