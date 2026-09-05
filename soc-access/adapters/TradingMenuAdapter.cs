using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine.EventSystems;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TradingMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(TradingMenu), "_settings");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(TradingMenu), "_facade");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(TradingMenu), "_async");
        private static readonly FieldInfo LeftCommanderIdField = AccessTools.Field(typeof(TradingMenu), "_leftCommanderId");
        private static readonly FieldInfo RightCommanderIdField = AccessTools.Field(typeof(TradingMenu), "_rightCommanderId");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(InventoryHUD), "_localizationHandler");
        private static readonly FieldInfo ArtifactLookupField = AccessTools.Field(typeof(InventoryHUD), "_lookup");
        private static readonly FieldInfo OffenseTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_offenseTooltipImage");
        private static readonly FieldInfo DefenceTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_defenceTooltipImage");
        private static readonly FieldInfo MovementTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_movementTooltipImage");
        private static readonly FieldInfo ViewTooltipImageField = AccessTools.Field(typeof(CommanderStatsInfo), "_viewTooltipImage");
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

        private readonly TradingMenu _menu;
        private readonly TradingMenu.Settings _settings;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly IArtifactLookup _artifactLookup;

        public TradingMenuAdapter(TradingMenu menu)
        {
            _menu = menu;
            _settings = GetField<TradingMenu.Settings>(menu, SettingsField);
            _facade = GetField<IClientAdventureFacade>(menu, FacadeField);
            _localization = GetField<ILocalizationHandler>(_settings != null ? _settings.leftInventory : null, LocalizationField)
                ?? GetField<ILocalizationHandler>(_settings != null ? _settings.rightInventory : null, LocalizationField);
            _artifactLookup = GetField<IArtifactLookup>(_settings != null ? _settings.leftInventory : null, ArtifactLookupField)
                ?? GetField<IArtifactLookup>(_settings != null ? _settings.rightInventory : null, ArtifactLookupField);
        }

        public TradingMenu Source
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public ILocalizationHandler Localization
        {
            get { return _localization; }
        }

        public int LeftCommanderId
        {
            get { return GetFieldValue<int>(_menu, LeftCommanderIdField, -1); }
        }

        public int RightCommanderId
        {
            get { return GetFieldValue<int>(_menu, RightCommanderIdField, -1); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && GetField<object>(_menu, AsyncField) != null
                && IsVisible(_settings.TradingMenuTransform)
                && _settings.leftInventory != null
                && _settings.rightInventory != null
                && _settings.leftTroopHud != null
                && _settings.rightTroopHud != null;
        }

        public string Title
        {
            get { return "Trade"; }
        }

        public string LeftCommanderName
        {
            get { return GetCommanderName(LeftCommanderId); }
        }

        public string RightCommanderName
        {
            get { return GetCommanderName(RightCommanderId); }
        }

        public string EquipmentLabel
        {
            get { return GetLocalizedText("Common/CommanderInventory/Equipment", "Equipment"); }
        }

        public string InventoryLabel
        {
            get { return GetInventoryLabel(); }
        }

        public TroopHudAdapter LeftTroops
        {
            get { return new TroopHudAdapter(_settings != null ? _settings.leftTroopHud : null, _facade, _localization); }
        }

        public TroopHudAdapter RightTroops
        {
            get { return new TroopHudAdapter(_settings != null ? _settings.rightTroopHud : null, _facade, _localization); }
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Close();
            return true;
        }

        public string GetPortraitLabel(bool left)
        {
            int commanderId = left ? LeftCommanderId : RightCommanderId;
            ICommanderState commander = GetCommander(commanderId);
            string name = GetCommanderName(commanderId);
            if (commander == null)
            {
                return name;
            }

            return MenuButtonTextUtility.JoinParts(name, ModText.Get(ModStrings.Screens.LevelValue, commander.Level));
        }

        public Component GetPortraitTooltipTarget(bool left)
        {
            CommanderHUDPortrait portrait = left ? _settings.leftPortrait : _settings.rightPortrait;
            return portrait != null ? portrait.GetSelectable() as Component : null;
        }

        public IReadOnlyList<LabeledItem> GetStats(bool left)
        {
            int commanderId = left ? LeftCommanderId : RightCommanderId;
            CommanderStatsInfo statsInfo = left ? _settings.leftStatsInfo : _settings.rightStatsInfo;
            List<LabeledItem> items = new List<LabeledItem>();
            ICommanderState commander = GetCommander(commanderId);
            if (commander == null)
            {
                return items;
            }

            AddStat(items, statsInfo, StatEntryType.Offense, GameText.Get(_localization, "Commanders/Tooltip/Offense", "Offence"), commander.Stats.Offense.GetValue());
            AddStat(items, statsInfo, StatEntryType.Defense, GameText.Get(_localization, "Commanders/Tooltip/Defense", "Defence"), commander.Stats.Defense.GetValue());
            AddStat(items, statsInfo, StatEntryType.Movement, GameText.Get(_localization, "Commanders/Tooltip/Movement", "Movement"), (int)commander.Stats.Movement.GetValue());
            AddStat(items, statsInfo, StatEntryType.View, GameText.Get(_localization, "Commanders/Tooltip/ViewRadius", "View Radius"), (int)commander.Stats.ViewRadius.GetValue());
            return items;
        }

        public IReadOnlyList<ModifierCategory> GetModifierCategories(bool left)
        {
            CommanderSheetModifierTabNavigation tabs = GetModifierTabs(left);
            return new[]
            {
                new ModifierCategory("trade-" + GetSideId(left) + "-modifier-category-troop", GetModifierTitle(0), 0, Tooltip.ForComponent(GetModifierCategoryButton(tabs, 0) as Component, _localization)),
                new ModifierCategory("trade-" + GetSideId(left) + "-modifier-category-temporary", GetModifierTitle(1), 1, Tooltip.ForComponent(GetModifierCategoryButton(tabs, 1) as Component, _localization)),
                new ModifierCategory("trade-" + GetSideId(left) + "-modifier-category-gear", GetModifierTitle(2), 2, Tooltip.ForComponent(GetModifierCategoryButton(tabs, 2) as Component, _localization))
            };
        }

        public bool FocusModifierCategory(bool left, int categoryIndex)
        {
            SelectModifierCategoryButton(left, categoryIndex);

            bool changed = SetModifierCategory(_settings.leftModifierTabs, categoryIndex);
            changed = SetModifierCategory(_settings.rightModifierTabs, categoryIndex) || changed;
            return changed || GetModifierTabs(left) != null;
        }

        public void SelectModifierCategoryButton(bool left, int categoryIndex)
        {
            CommanderSheetModifierTabNavigation sideTabs = GetModifierTabs(left);
            NativeSelectionUtility.Select(GetModifierCategoryButton(sideTabs, categoryIndex) as Component);
        }

        public int GetActiveModifierCategoryIndex(bool left)
        {
            CommanderSheetModifierTabNavigation tabs = GetModifierTabs(left);
            object state = CurrentTabStateField != null && tabs != null
                ? CurrentTabStateField.GetValue(tabs)
                : null;
            return state != null ? (int)state : 0;
        }

        public string GetActiveModifierListLabel(bool left)
        {
            return GetPossessiveCommanderName(left) + " " + GetModifierTitle(GetActiveModifierCategoryIndex(left));
        }

        public IReadOnlyList<LabeledItem> GetActiveModifiers(bool left)
        {
            CommanderSheetModifierTabNavigation tabs = GetModifierTabs(left);
            List<LabeledItem> items = new List<LabeledItem>();
            Transform content = GetActiveModifierContent(tabs);
            if (content != null)
            {
                CommanderSheetSummaryEntry[] entries = ((Component)content).GetComponentsInChildren<CommanderSheetSummaryEntry>(false);
                for (int i = 0; i < entries.Length; i++)
                {
                    UITextMesh text = GetField<UITextMesh>(entries[i], SummaryEntryTextField);
                    string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        items.Add(new LabeledItem("trade-" + GetSideId(left) + "-modifier-" + i, label));
                    }
                }
            }

            if (items.Count == 0)
            {
                UITextMesh noneText = GetField<UITextMesh>(tabs, NoModifiersTextField);
                string label = SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(noneText));
                items.Add(new LabeledItem("trade-" + GetSideId(left) + "-modifiers-none", string.IsNullOrWhiteSpace(label) ? "None" : label));
            }

            return items;
        }

        public DropResult DropInventoryArtifact(InventorySlotInfo source, InventorySlotInfo target)
        {
            return ArtifactDropUtility.DropInventoryArtifact(_facade, source, target, "TradingMenuAdapter artifact grid drop");
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public IReadOnlyList<InventorySlotInfo> GetEquipmentSlots(bool left)
        {
            return BuildEquipmentSlots(
                GetInventory(left),
                left ? LeftCommanderId : RightCommanderId);
        }

        public IReadOnlyList<InventorySlotInfo> GetBackpackSlots(bool left)
        {
            return BuildBackpackSlots(
                GetInventory(left),
                left ? LeftCommanderId : RightCommanderId);
        }

        private InventoryHUD GetInventory(bool left)
        {
            if (_settings == null)
            {
                return null;
            }

            return left ? _settings.leftInventory : _settings.rightInventory;
        }

        private IReadOnlyList<InventorySlotInfo> BuildEquipmentSlots(InventoryHUD inventory, int commanderId)
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

            string ownerName = GetCommanderName(commanderId);
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlot slot = slots[i];
                InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(slot) : null;
                IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(commanderId, slot);
                bool displayOnly = IsDisplayOnlyEquipmentArtifact(slot, artifact);
                InventoryArtifactMovable nativeMovable = nativeSlot != null ? nativeSlot.TryGetArtifact(0) : null;
                InventoryArtifactMovable artifactMovable = nativeMovable ?? GetArtifactMovable(inventory, artifact);
                InventoryArtifactMovable movable = displayOnly ? null : artifactMovable;
                InventorySlot capturedSlot = slot;
                InventoryHUDSlot capturedNativeSlot = nativeSlot;
                InventoryArtifactMovable capturedMovable = movable;
                Selectable tooltipSelectable = movable != null
                    ? movable.GetSelectable()
                    : displayOnly && artifactMovable != null
                        ? artifactMovable.GetSelectable()
                        : GetEquipmentSlotSelectable(capturedNativeSlot, capturedSlot);
                slotsInfo.Add(new InventorySlotInfo(
                    commanderId,
                    ownerName,
                    slot,
                    0,
                    isBackpackSlot: false,
                    GetInventorySlotName(slot),
                    GetInventoryLabel(),
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildArtifactTooltip(inventory, artifact, artifactMovable, tooltipSelectable),
                    () => SelectInventoryCell(capturedNativeSlot, capturedMovable, 0)));
            }

            return slotsInfo;
        }

        private IReadOnlyList<InventorySlotInfo> BuildBackpackSlots(InventoryHUD inventory, int commanderId)
        {
            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(InventorySlot.None) : null;
            string ownerName = GetCommanderName(commanderId);
            int cellCount = nativeSlot != null ? nativeSlot.CellsCount : 0;
            for (int i = 0; i < cellCount; i++)
            {
                InventoryArtifactMovable movable = nativeSlot != null ? nativeSlot.TryGetArtifact(i) : null;
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
                    GetInventoryLabel(),
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildArtifactTooltip(inventory, artifact, movable, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i)),
                    () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
            }

            return slotsInfo;
        }

        private void AddStat(List<LabeledItem> items, CommanderStatsInfo statsInfo, StatEntryType type, string label, int value)
        {
            items.Add(new LabeledItem(
                "trade-stat-" + type,
                label + ", " + value,
                tooltip: Tooltip.ForComponent(GetStatTooltipComponent(statsInfo, type), _localization)));
        }

        private Component GetStatTooltipComponent(CommanderStatsInfo statsInfo, StatEntryType type)
        {
            UIImage tooltipImage = null;
            switch (type)
            {
                case StatEntryType.Offense:
                    tooltipImage = GetField<UIImage>(statsInfo, OffenseTooltipImageField);
                    break;
                case StatEntryType.Defense:
                    tooltipImage = GetField<UIImage>(statsInfo, DefenceTooltipImageField);
                    break;
                case StatEntryType.Movement:
                    tooltipImage = GetField<UIImage>(statsInfo, MovementTooltipImageField);
                    break;
                case StatEntryType.View:
                    tooltipImage = GetField<UIImage>(statsInfo, ViewTooltipImageField);
                    break;
            }

            return tooltipImage as Component;
        }

        private bool SetModifierCategory(CommanderSheetModifierTabNavigation tabs, int categoryIndex)
        {
            if (tabs == null)
            {
                return false;
            }

            if (GetActiveModifierCategoryIndex(tabs) == categoryIndex)
            {
                return true;
            }

            UIButton button = GetModifierCategoryButton(tabs, categoryIndex);
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
            SetActiveTabMethod.Invoke(tabs, new[] { tabState, (object)false });
            return true;
        }

        private int GetActiveModifierCategoryIndex(CommanderSheetModifierTabNavigation tabs)
        {
            object state = CurrentTabStateField != null && tabs != null
                ? CurrentTabStateField.GetValue(tabs)
                : null;
            return state != null ? (int)state : 0;
        }

        private CommanderSheetModifierTabNavigation GetModifierTabs(bool left)
        {
            return left ? _settings.leftModifierTabs : _settings.rightModifierTabs;
        }

        private UIButton GetModifierCategoryButton(CommanderSheetModifierTabNavigation tabs, int categoryIndex)
        {
            switch (categoryIndex)
            {
                case 1:
                    return GetField<UIButton>(tabs, TemporaryModifierButtonField);
                case 2:
                    return GetField<UIButton>(tabs, GearModifierButtonField);
                default:
                    return GetField<UIButton>(tabs, TroopModifierButtonField);
            }
        }

        private Transform GetActiveModifierContent(CommanderSheetModifierTabNavigation tabs)
        {
            int stateIndex = GetActiveModifierCategoryIndex(tabs);
            switch (stateIndex)
            {
                case 1:
                    return GetContentTransform(tabs, TemporaryModifierContentField);
                case 2:
                    return GetContentTransform(tabs, GearModifierContentField);
                default:
                    return GetContentTransform(tabs, TroopModifierContentField);
            }
        }

        private Transform GetContentTransform(CommanderSheetModifierTabNavigation tabs, FieldInfo field)
        {
            GameObject content = GetField<GameObject>(tabs, field);
            return content != null ? content.transform : null;
        }

        private string GetModifierTitle(int categoryIndex)
        {
            switch (categoryIndex)
            {
                case 1:
                    return GetLocalizedText("Commanders/Details/Modifiers/TemporaryModTitle", "Temporary modifiers");
                case 2:
                    return GetLocalizedText("Commanders/Details/Modifiers/GearModTitle", "Gear modifiers");
                default:
                    return GetLocalizedText("Commanders/Details/Modifiers/TroopModTitle", "Troop modifiers");
            }
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

        private static Selectable GetEquipmentSlotSelectable(InventoryHUDSlot nativeSlot, InventorySlot slot)
        {
            return nativeSlot != null ? nativeSlot.GetFirstSelectable() : null;
        }

        private static Selectable GetInventorySlotSelectable(InventoryHUDSlot nativeSlot, int positionIndex)
        {
            InventoryHUDGridEntry entry = nativeSlot != null ? nativeSlot.TryGetEntry(positionIndex) : null;
            return entry != null ? (Selectable)entry : null;
        }

        private IArtifactState GetDisplayArtifactForEquipmentSlot(int commanderId, InventorySlot slot)
        {
            if (_facade == null || commanderId < 0)
            {
                return null;
            }

            if (slot == InventorySlot.OffHand)
            {
                return _facade.Artifacts.GetForOwner(commanderId, ArtifactSlot.OffHand).FirstOrDefault();
            }

            return _facade.Artifacts.GetForOwner(commanderId, slot).FirstOrDefault();
        }

        private static bool IsDisplayOnlyEquipmentArtifact(InventorySlot slot, IArtifactState artifact)
        {
            return slot == InventorySlot.OffHand
                && artifact != null
                && artifact.EquippedInSlot == InventorySlot.MainHand;
        }

        private static InventoryArtifactMovable GetArtifactMovable(InventoryHUD inventory, IArtifactState artifact)
        {
            InventoryHUDSlot nativeSlot = inventory != null && artifact != null
                ? inventory.GetSlot(artifact.EquippedInSlot)
                : null;
            return nativeSlot != null ? nativeSlot.TryGetArtifact(artifact.PositionIndex) : null;
        }

        private Tooltip BuildArtifactTooltip(InventoryHUD inventory, IArtifactState artifact, InventoryArtifactMovable movable, Selectable selectable)
        {
            Tooltip tooltip = Tooltip.ForComponent(selectable as Component, _localization);
            if (tooltip == null || inventory == null || artifact == null || movable == null || _localization == null)
            {
                return tooltip;
            }

            if (artifact.IsImportant)
            {
                return tooltip;
            }

            List<TooltipAction> actions = new List<TooltipAction>();
            List<string> instructionLines = new List<string>();

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Trade");
            actions.Add(new TooltipAction(
                GetLocalizedText("Adventure/TooltipInstruction/Trade", "Trade"),
                () => InvokeArtifactAction(movable, inventory.EquipArtifact)));

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Destroy");
            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Destroy.Gamepad");
            actions.Add(new TooltipAction(
                GetLocalizedText("Adventure/TooltipInstruction/Destroy.Gamepad", "Destroy"),
                () => InvokeArtifactAction(movable, inventory.DestroyArtifact)));

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Drop");
            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Drop.Gamepad");
            actions.Add(new TooltipAction(
                GetLocalizedText("Adventure/TooltipInstruction/Drop.Gamepad", "Drop"),
                () => InvokeArtifactAction(movable, inventory.DropArtifact)));

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/AutoArrange");
            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/AutoArrange.Gamepad");
            actions.Add(new TooltipAction(
                GetLocalizedText("Adventure/TooltipInstruction/AutoArrange.Gamepad", "Auto Arrange"),
                () => InvokeArtifactAction(movable, inventory.AutoArrangeArtifacts)));

            return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata, actions);
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
                SocAccessMod.Instance?.LogWarning("TradingMenuAdapter could not get artifact rarity color: " + ex.Message);
                return _artifactLookup != null ? _artifactLookup.GetLocalizedName(artifact.Type) : artifact.Type.ToString();
            }
        }

        private string GetInventorySlotName(InventorySlot slot)
        {
            string text = _localization != null ? _localization.GetText("InventorySlots/" + slot) : string.Empty;
            return string.IsNullOrWhiteSpace(text) || text == "InventorySlots/" + slot
                ? FormatSlotName(slot)
                : SpeechTextSanitizer.Normalize(text);
        }

        private string GetInventoryLabel()
        {
            return GetLocalizedText("Common/CommanderInventory/Inventory", "Inventory");
        }

        private string GetCommanderName(int commanderId)
        {
            string name = commanderId >= 0 && _facade != null ? _facade.Commanders.GetName(commanderId) : string.Empty;
            return SpeechTextSanitizer.Normalize(name);
        }

        private ICommanderState GetCommander(int commanderId)
        {
            return commanderId >= 0 && _facade != null ? _facade.Commanders.Get(commanderId) : null;
        }

        private string GetPossessiveCommanderName(bool left)
        {
            string name = left ? LeftCommanderName : RightCommanderName;
            return string.IsNullOrWhiteSpace(name) ? "Wielder's" : name + "'s";
        }

        private static string GetSideId(bool left)
        {
            return left ? "left" : "right";
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback));
        }

        private static string FormatSlotName(InventorySlot slot)
        {
            string value = slot.ToString();
            string formatted = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                {
                    formatted += " ";
                }

                formatted += char.ToLowerInvariant(c);
            }

            return formatted;
        }

        private static bool IsVisible(Transform transform)
        {
            return transform != null && transform.gameObject != null && transform.gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        private static T GetFieldValue<T>(object owner, FieldInfo field, T fallback)
        {
            if (owner == null || field == null)
            {
                return fallback;
            }

            object value = field.GetValue(owner);
            return value is T ? (T)value : fallback;
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
