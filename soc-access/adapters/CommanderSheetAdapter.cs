using System;
using System.Collections.Generic;
using System.Collections;
using System.Globalization;
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
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private static readonly FieldInfo MovableButtonField = AccessTools.Field(typeof(InventoryArtifactMovable), "_button");

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
        private static readonly FieldInfo InventoryCommandProcessorField = AccessTools.Field(typeof(InventoryHUD), "_commandProcessor");
        private static readonly FieldInfo InventoryArtifactMapField = AccessTools.Field(typeof(InventoryHUD), "_artifactStateToGOMap");

        // The showing modifier tab's rows, walked at most once a frame: the build asks for them
        // and each row's live readout asks again. Keyed on the frame rather than held, because the
        // tab switch replaces the rows under the same content transform.
        private readonly FrameSweep<CommanderSheetSummaryEntry> _modifierEntries =
            new FrameSweep<CommanderSheetSummaryEntry>("commander sheet modifiers", inactiveToo: false);

        // The wielder's two artifact lists, kept while the game's own inventory is unchanged. The
        // key is read off the game every frame (the wielder the sheet shows, the HUD, the pooled
        // cells and what is in each of them), so an equip, an unequip, a move and an auto-arrange
        // all rebuild with nothing having to say so (AGENTS.md, Screen Resolution).
        private readonly SlotSnapshot _equipment = new SlotSnapshot();

        private readonly SlotSnapshot _backpack = new SlotSnapshot();

        private readonly CommanderSheet _sheet;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        // The mouse rows every artifact tooltip ends with, resolved on the first slot that needs them.
        private List<string> _mouseInstructionLines;
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

        /// <summary>The wielder's name, as the sheet draws it at the top.</summary>
        public string CommanderName
        {
            get { return UITextMeshTextUtility.GetEffectiveText(GetField<UITextMesh>(_sheet, NameField)); }
        }

        /// <summary>The race and title drawn under the name ("Human Commander").</summary>
        public string CommanderClass
        {
            get { return UITextMeshTextUtility.GetEffectiveText(GetField<UITextMesh>(_sheet, ClassField)); }
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
            return GetField<UIButton>(_sheet, BackgroundCloseButtonField);
        }

        /// <summary>The tutorial button the sheet draws only until the tutorial has been seen.</summary>
        public Component TutorialButton
        {
            get { return GetField<UIButton>(_sheet, TutorialButtonField) as Component; }
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
                items.Add(new LabeledItem("specialization-" + i, SpokenLines.Clean(text)));
            }

            return items;
        }

        public IReadOnlyList<ModifierCategory> GetModifierCategories()
        {
            return new[]
            {
                BuildModifierCategory("modifier-category-troop", "Commanders/Details/Modifiers/TroopModTitle", "Troop modifiers", 0),
                BuildModifierCategory("modifier-category-temporary", "Commanders/Details/Modifiers/TemporaryModTitle", "Temporary modifiers", 1),
                BuildModifierCategory("modifier-category-gear", "Commanders/Details/Modifiers/GearModTitle", "Gear modifiers", 2)
            };
        }

        private ModifierCategory BuildModifierCategory(string id, string key, string fallback, int index)
        {
            UIButton button = GetModifierCategoryButton(index);
            return new ModifierCategory(
                id,
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
                CommanderSheetSummaryEntry[] entries = _modifierEntries.Under(content);
                for (int i = 0; i < entries.Length; i++)
                {
                    UITextMesh text = GetField<UITextMesh>(entries[i], SummaryEntryTextField);
                    string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        items.Add(new LabeledItem("modifier-" + i, label));
                    }
                }
            }

            if (items.Count == 0)
            {
                UITextMesh noneText = GetField<UITextMesh>(_modifierTabs, NoModifiersTextField);
                string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(noneText));
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
                        value: GetSkillLevelText(capturedPowers, capturedIndex, skill),
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
            InventorySlot[] slots = InventorySlotInfo.DrawnEquipmentSlots;
            int commanderId = CommanderId;
            List<int> key = _equipment.BeginKey();
            key.Add(commanderId);
            key.Add(InstanceId(_inventory));
            for (int i = 0; i < slots.Length; i++)
            {
                InventoryHUDSlot keySlot = _inventory != null ? _inventory.GetSlot(slots[i]) : null;
                IArtifactState keyArtifact = GetDisplayArtifactForEquipmentSlot(slots[i]);
                key.Add(InstanceId(keySlot));
                key.Add(InstanceId(keySlot != null ? keySlot.TryGetArtifact(0) : null));
                key.Add(keyArtifact != null ? keyArtifact.Id : 0);
                key.Add(keyArtifact != null ? (int)keyArtifact.EquippedInSlot : -1);
                key.Add(keyArtifact != null ? keyArtifact.PositionIndex : -1);
            }

            IReadOnlyList<InventorySlotInfo> unchanged = _equipment.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            string ownerName = GetCommanderName();
            string inventoryLabel = GetInventoryLabel();
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
                    commanderId,
                    ownerName,
                    slot,
                    0,
                    isBackpackSlot: false,
                    GetInventorySlotName(slot),
                    inventoryLabel,
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildEquipmentTooltip(artifact, tooltipSelectable, artifactMovable),
                    () => SelectInventoryCell(capturedNativeSlot, capturedMovable, 0)));
            }

            return _equipment.Keep(slotsInfo);
        }

        public IReadOnlyList<InventorySlotInfo> GetBackpackSlots()
        {
            InventoryHUDSlot nativeSlot = _inventory != null ? _inventory.GetSlot(InventorySlot.None) : null;
            int commanderId = CommanderId;
            int cellCount = nativeSlot != null ? nativeSlot.CellsCount : 0;
            List<int> key = _backpack.BeginKey();
            key.Add(commanderId);
            key.Add(InstanceId(nativeSlot));
            for (int i = 0; i < cellCount; i++)
            {
                InventoryArtifactMovable keyMovable = nativeSlot.TryGetArtifact(i);
                key.Add(InstanceId(keyMovable));
                key.Add(keyMovable != null && keyMovable.State != null ? keyMovable.State.Id : 0);
            }

            IReadOnlyList<InventorySlotInfo> unchanged = _backpack.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            string ownerName = GetCommanderName();
            string inventoryLabel = GetInventoryLabel();

            // What the backpack holds is read off the DRAWN cell, which already knows, rather than
            // out of the owner's whole artifact list: the same source the key above is read from,
            // and the two must agree. They did not while the list came from the facade - a move
            // within the backpack reaches the HUD a frame before it reaches the facade, so a key
            // that had already changed froze a list built from the position the artifact had just
            // left (measured in-game 2026-09-10: HUD cell 5, facade position 9, for one frame).
            for (int i = 0; i < cellCount; i++)
            {
                InventoryArtifactMovable movable = nativeSlot.TryGetArtifact(i);
                IArtifactState artifact = movable != null ? movable.State : null;
                int capturedIndex = i;
                InventoryArtifactMovable capturedMovable = movable;
                slotsInfo.Add(new InventorySlotInfo(
                    commanderId,
                    ownerName,
                    InventorySlot.None,
                    i,
                    isBackpackSlot: true,
                    string.Empty,
                    inventoryLabel,
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildEquipmentTooltip(artifact, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i), movable),
                    () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
            }

            return _backpack.Keep(slotsInfo);
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

        /// <summary>What a game object is, as a number a key can hold: zero for one the game has not
        /// made or has destroyed, which Unity's own null answers for.</summary>
        private static int InstanceId(Component component)
        {
            return component == null ? 0 : component.GetInstanceID();
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

        /// <summary>Put an artifact down on a slot, through the game's own check and its own move.
        /// </summary>
        public DropResult DropArtifact(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return ArtifactDropUtility.DropArtifact(_facade, movable, target, "CommanderSheetAdapter artifact drop");
        }

        /// <summary>Whether the artifact in the main hand takes BOTH hands - the definition's own slot
        /// (<c>IArtifactLookup.GetSlot</c>), which is what makes the game draw a ghost of it in the off
        /// hand.</summary>
        public bool IsMainHandTwoHanded()
        {
            IArtifactState artifact = GetArtifactsForSlot(InventorySlot.MainHand).FirstOrDefault();
            return artifact != null && _artifactLookup != null && _artifactLookup.GetSlot(artifact.Type) == ArtifactSlot.BothHands;
        }

        /// <summary>Whether an artifact fits the off hand ALONE - the one case the game's own
        /// right-click resolution (<c>InventoryHUD.GetSlot(ArtifactSlot)</c>) sends to the off hand
        /// rather than the main one.</summary>
        public bool IsOffHandOnlyArtifact(InventoryArtifactMovable movable)
        {
            return movable != null
                && movable.State != null
                && _artifactLookup != null
                && _artifactLookup.GetSlot(movable.State.Type) == ArtifactSlot.OffHand;
        }

        /// <summary>The game's own name for the two-handed slot ("Both Hands").</summary>
        public string BothHandsSlotName
        {
            get { return GetInventorySlotName(ArtifactSlot.BothHands.ToString()); }
        }

        /// <summary>Whether the game would accept this artifact in this slot at this position - the
        /// same check its own drop makes (<c>CanRearrangeArtifact</c>), asked without doing anything.
        /// </summary>
        public bool CanRearrangeArtifactTo(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            InventoryHUDSlot nativeSlot = target != null ? target.NativeSlot : null;
            if (_facade == null || movable == null || movable.State == null || nativeSlot == null)
            {
                return false;
            }

            try
            {
                return _facade.Commands.CanRearrangeArtifact(movable.State.Id, nativeSlot.Slot, target.PositionIndex).success;
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("CommanderSheetAdapter could not ask whether an artifact fits: " + ex.Message);
                return false;
            }
        }

        /// <summary>The game's own notification for a rearrangement its Command skill blocks - what it
        /// shows itself when the drop is refused with error code 10.</summary>
        public string RearrangeRefusalText
        {
            get { return GetLocalizedText("Common/CommanderInventory/RearrangeArtifact/CannotRearrangeBecauseOfCommand", string.Empty); }
        }

        /// <summary>Nothing on this sheet listens to the inventory's left click.</summary>
        public bool AnswersLeftClick(InventorySlotInfo slot)
        {
            return false;
        }

        /// <summary>The artifact's LEFT click, through the button the game hangs its own handler on -
        /// inert on this sheet, and with Ctrl physically held the game's own drop on the ground.</summary>
        public bool LeftClickArtifact(InventorySlotInfo slot)
        {
            return NativeSelectionUtility.Click(GetMovableButton(slot));
        }

        /// <summary>The artifact's RIGHT click, through the same button: equip, unequip or use, and
        /// with Ctrl physically held the game's own destroy.</summary>
        public bool RightClickArtifact(InventorySlotInfo slot)
        {
            return NativeSelectionUtility.RightClick(GetMovableButton(slot));
        }

        /// <summary>What a right click on this artifact does, as the GAME decides it in
        /// <c>InventoryArtifactMovable.GetDetails</c>: an artifact whose definition carries an action
        /// is used, and every other one is equipped or unequipped by where it currently is.</summary>
        public ArtifactDetails.EquipInstruction GetArtifactInstruction(InventorySlotInfo slot)
        {
            IArtifactState artifact = GetArtifactState(slot);
            if (artifact == null)
            {
                return ArtifactDetails.EquipInstruction.None;
            }

            IArtifactDataDefinition definition = _artifactLookup != null ? _artifactLookup.GetDefinition(artifact.Type) : null;
            if (definition != null && definition.Action != null)
            {
                return ArtifactDetails.EquipInstruction.Use;
            }

            return artifact.IsEquipped
                ? ArtifactDetails.EquipInstruction.Unequip
                : ArtifactDetails.EquipInstruction.Equip;
        }

        /// <summary>The game's own text for the auto-arrange instruction, as it draws it in an
        /// artifact's tooltip.</summary>
        public string AutoArrangeText
        {
            get { return GetLocalizedText("Adventure/TooltipInstruction/AutoArrange", string.Empty); }
        }

        /// <summary>Auto-arrange, the game's own middle click (<c>InventoryHUD.AutoArrangeArtifacts</c>).
        /// Its second half only remembers which cell to re-select afterwards and needs an artifact to
        /// remember, so with nothing in the inventory the command it runs is called on its own.</summary>
        public bool AutoArrangeArtifacts()
        {
            if (_inventory == null)
            {
                return false;
            }

            InventoryArtifactMovable anyArtifact = FirstArtifactMovable();
            if (anyArtifact != null)
            {
                _inventory.AutoArrangeArtifacts(anyArtifact);
                return true;
            }

            if (_facade == null || CommanderId < 0)
            {
                return false;
            }

            _facade.Commands.EquipBestArtifacts(CommanderId);
            return true;
        }

        private InventoryArtifactMovable FirstArtifactMovable()
        {
            IDictionary artifactMap = InventoryArtifactMapField != null && _inventory != null
                ? InventoryArtifactMapField.GetValue(_inventory) as IDictionary
                : null;
            if (artifactMap == null)
            {
                return null;
            }

            foreach (object movable in artifactMap.Values)
            {
                InventoryArtifactMovable artifact = movable as InventoryArtifactMovable;
                if (artifact != null)
                {
                    return artifact;
                }
            }

            return null;
        }

        private IArtifactState GetArtifactState(InventorySlotInfo slot)
        {
            InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
            return movable != null ? movable.State : null;
        }

        private static IUIButton GetMovableButton(InventorySlotInfo slot)
        {
            InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
            return movable != null && MovableButtonField != null
                ? MovableButtonField.GetValue(movable) as IUIButton
                : null;
        }

        /// <summary>
        /// An artifact's own tooltip, without the lines that tell a MOUSE what to press.
        ///
        /// <c>ArtifactDetails</c> ends its tooltip with a row per gesture ("&lt;rmb&gt; Equip",
        /// "&lt;hl&gt;CTRL&lt;/hl&gt; + &lt;rmb&gt; Destroy", the drop and the auto-arrange), and the
        /// keyboard gets those same gestures as usage hints on the slot itself, so the rows would be
        /// said twice. They are removed by the localized text the game DREW them from rather than by
        /// English, so a row this mod does not know about is left where it is and the player still
        /// hears that something may be available.
        /// </summary>
        private Tooltip BuildEquipmentTooltip(IArtifactState artifact, Selectable selectable, InventoryArtifactMovable movable)
        {
            Tooltip tooltip = Tooltip.ForComponent(selectable, _localization);
            if (tooltip == null || artifact == null || movable == null || _localization == null)
            {
                return tooltip;
            }

            List<string> instructionLines = GetMouseInstructionLines();
            return new Tooltip(
                () => RemoveExactLines(tooltip.TextLines, instructionLines),
                tooltip.VisualMetadata,
                isLong: () => tooltip.IsLong);
        }

        // The nine rows the game writes for a mouse. They are the same for every slot and for the
        // window's whole life, so they are looked up once instead of nine times a slot a frame.
        private List<string> GetMouseInstructionLines()
        {
            if (_mouseInstructionLines != null)
            {
                return _mouseInstructionLines;
            }

            List<string> lines = new List<string>();
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Equip");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Unequip");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Sell");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Destroy");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Destroy.Gamepad");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Drop");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/Drop.Gamepad");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/AutoArrange");
            AddLocalizedLine(lines, "Adventure/TooltipInstruction/AutoArrange.Gamepad");
            _mouseInstructionLines = lines;
            return lines;
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

        /// <summary>The level the skill's own entry DRAWS beside its name; the reference's level where
        /// the entry cannot be read.</summary>
        private string GetSkillLevelText(bool powers, int index, SkillReference skill)
        {
            UITextMesh level = GetField<UITextMesh>(GetSkillEntryComponent(powers, index), SkillEntryLevelField);
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
                ? GetField<List<CommanderSheetSkillEntry>>(_skills, PowerEntriesField)
                : GetField<List<CommanderSheetSkillEntry>>(_skills, SkillEntriesField);
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
                SocAccessMod.Instance?.LogWarning("CommanderSheetAdapter could not get artifact rarity color: " + ex.Message);
                return _artifactLookup != null ? _artifactLookup.GetLocalizedName(artifact.Type) : artifact.Type.ToString();
            }
        }

        private string GetCommanderName()
        {
            ICommanderState commander = GetCommander();
            string name = commander != null && _facade != null ? _facade.Commanders.GetName(commander.Id) : string.Empty;
            return SpokenLines.Clean(name);
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
                    return SpokenLines.Clean(text);
                }
            }

            return skill.Skill.ToString();
        }

        private string GetInventorySlotName(InventorySlot slot)
        {
            return GetInventorySlotName(slot.ToString());
        }

        private string GetInventorySlotName(string slotName)
        {
            if (_localization != null)
            {
                string text = _localization.GetText("InventorySlots/" + slotName);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    return SpokenLines.Clean(text);
                }
            }

            return slotName;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class LabeledItem
        {
            public LabeledItem(string id, string label, string value = null, Action onFocus = null, Tooltip tooltip = null)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                OnFocus = onFocus;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }

            /// <summary>The number the game draws beside the name - a stat's value, a skill's level -
            /// kept apart from it so the screen decides how the two read together.</summary>
            public string Value { get; private set; }
            public Action OnFocus { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

        public sealed class ModifierCategory
        {
            public ModifierCategory(string id, string label, int index, Component button = null, Tooltip tooltip = null)
            {
                Id = id;
                Label = label;
                Index = index;
                Button = button;
                Tooltip = tooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public int Index { get; private set; }

            /// <summary>The tab the game draws for this category.</summary>
            public Component Button { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

    }
}
