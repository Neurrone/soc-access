using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Adventure.UI.Trading;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The trade between two wielders standing next to each other on the map. The menu is two of the
    /// SAME BAND the wielder sheet draws, side by side: a portrait, a <c>CommanderStatsInfo</c>, a
    /// <c>CommanderSheetModifierTabNavigation</c>, an <c>InventoryHUD</c> and a <c>TroopHUD</c> each,
    /// with a Move all button under each army and one close cross over the pair.
    ///
    /// So the adapter is two <see cref="Side"/>s, each of them an <see cref="IArtifactSlots"/> in its
    /// own right, plus the handful of facts that belong to the menu rather than to a side. Which side
    /// is which is the GAME's: <c>TradingMenu.Open</c> puts the more easterly - or, at equal x, the
    /// more southerly - wielder on the left.
    ///
    /// The clicks on an artifact mean something else here than they do on the sheet, and the
    /// difference is the game's: <c>InventoryHUD.EquipArtifact</c> branches on the other inventory
    /// being set and answers the right click with <c>MoveItemToOtherBackpack</c>. Saying so is the
    /// screen's business.
    /// </summary>
    public sealed class TradingMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(TradingMenu), "_settings");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(TradingMenu), "_facade");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(TradingMenu), "_async");
        private static readonly FieldInfo LeftCommanderIdField = AccessTools.Field(typeof(TradingMenu), "_leftCommanderId");
        private static readonly FieldInfo RightCommanderIdField = AccessTools.Field(typeof(TradingMenu), "_rightCommanderId");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(InventoryHUD), "_localizationHandler");
        private static readonly FieldInfo ArtifactLookupField = AccessTools.Field(typeof(InventoryHUD), "_lookup");
        private static readonly FieldInfo InventoryArtifactMapField = AccessTools.Field(typeof(InventoryHUD), "_artifactStateToGOMap");
        private static readonly FieldInfo MovableButtonField = AccessTools.Field(typeof(InventoryArtifactMovable), "_button");
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");

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
        private static readonly FieldInfo SummaryEntryTextField = AccessTools.Field(typeof(CommanderSheetSummaryEntry), "_textMesh");

        private readonly TradingMenu _menu;
        private readonly TradingMenu.Settings _settings;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly IArtifactLookup _artifactLookup;
        private Side _left;
        private Side _right;

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

        /// <summary>The wielder the menu draws on the LEFT, which the game chose by map position.
        /// </summary>
        public Side Left
        {
            get { return _left ?? (_left = new Side(this, left: true)); }
        }

        public Side Right
        {
            get { return _right ?? (_right = new Side(this, left: false)); }
        }

        /// <summary>The cross the window draws at its top right
        /// (<c>AdventureMenuBackground._closeButton</c>).</summary>
        public Component CloseButton
        {
            get { return GetCloseButton() as Component; }
        }

        public bool IsCloseVisible()
        {
            Component close = CloseButton;
            return close != null && close.gameObject != null && close.gameObject.activeInHierarchy;
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(GetCloseButton());
        }

        /// <summary>Switch the modifier tab, through the game's own click on BOTH sides' buttons: the
        /// menu's own tab control (<c>TradingMenu.HandleSwitchTab</c>) cycles the left and the right
        /// navigation together, so one side's tab is never a page of its own.</summary>
        public bool ActivateModifierCategory(int categoryIndex)
        {
            bool clicked = NativeSelectionUtility.Click(GetModifierCategoryButton(_settings != null ? _settings.leftModifierTabs : null, categoryIndex));
            clicked = NativeSelectionUtility.Click(GetModifierCategoryButton(_settings != null ? _settings.rightModifierTabs : null, categoryIndex)) || clicked;
            return clicked;
        }

        private IUIButton GetCloseButton()
        {
            AdventureMenuBackground background = _settings != null ? _settings.background : null;
            return background != null && BackgroundCloseButtonField != null
                ? BackgroundCloseButtonField.GetValue(background) as IUIButton
                : null;
        }

        private static UIButton GetModifierCategoryButton(CommanderSheetModifierTabNavigation tabs, int categoryIndex)
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

        private string GetLocalizedText(string key, string fallback)
        {
            return SpokenLines.Clean(GameText.Get(_localization, key, fallback));
        }

        private string GetCommanderName(int commanderId)
        {
            string name = commanderId >= 0 && _facade != null ? _facade.Commanders.GetName(commanderId) : string.Empty;
            return SpokenLines.Clean(name);
        }

        private ICommanderState GetCommander(int commanderId)
        {
            return commanderId >= 0 && _facade != null ? _facade.Commanders.Get(commanderId) : null;
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

        /// <summary>
        /// One half of the trade: everything the menu draws for one of the two wielders. It is an
        /// <see cref="IArtifactSlots"/>, so the shared artifact rows read it exactly as they read the
        /// sheet's and the market's inventories.
        /// </summary>
        public sealed class Side : IArtifactSlots
        {
            private readonly TradingMenuAdapter _owner;
            private readonly bool _left;
            private TroopHudAdapter _troops;

            public Side(TradingMenuAdapter owner, bool left)
            {
                _owner = owner;
                _left = left;
            }

            /// <summary>Whether this is the side the menu drew on the left.</summary>
            public bool IsLeft
            {
                get { return _left; }
            }

            public int CommanderId
            {
                get
                {
                    return GetFieldValue(
                        _owner._menu,
                        _left ? LeftCommanderIdField : RightCommanderIdField,
                        -1);
                }
            }

            public string CommanderName
            {
                get { return _owner.GetCommanderName(CommanderId); }
            }

            /// <summary>The level the menu draws on the portrait, or zero before the game has one.
            /// </summary>
            public int Level
            {
                get
                {
                    ICommanderState commander = _owner.GetCommander(CommanderId);
                    return commander != null ? commander.Level : 0;
                }
            }

            /// <summary>The wielder's own portrait, whose details are the stats the game draws on
            /// hover.</summary>
            public Component Portrait
            {
                get
                {
                    CommanderHUDPortrait portrait = _left ? Settings.leftPortrait : Settings.rightPortrait;
                    return portrait != null ? portrait.GetSelectable() as Component : null;
                }
            }

            public Tooltip PortraitTooltip
            {
                get { return Tooltip.ForComponent(Portrait, _owner._localization); }
            }

            public bool FocusPortrait()
            {
                return NativeSelectionUtility.Select(Portrait);
            }

            /// <summary>The army the side draws. Kept: the adapter wakes the game's drag ghost when it
            /// is made, and the rows are rebuilt on every navigation operation.</summary>
            public TroopHudAdapter Troops
            {
                get
                {
                    TroopHUD hud = _left ? Settings.leftTroopHud : Settings.rightTroopHud;
                    if (hud == null)
                    {
                        return null;
                    }

                    if (_troops == null || !ReferenceEquals(_troops.Hud, hud))
                    {
                        _troops = new TroopHudAdapter(hud, _owner._facade, _owner._localization);
                    }

                    return _troops;
                }
            }

            // ---- the stats band ----

            public IReadOnlyList<LabeledItem> GetStats()
            {
                List<LabeledItem> items = new List<LabeledItem>();
                ICommanderState commander = _owner.GetCommander(CommanderId);
                if (commander == null)
                {
                    return items;
                }

                AddStat(items, StatEntryType.Offense, GameText.Get(_owner._localization, "Commanders/Tooltip/Offense", "Offence"), commander.Stats.Offense.GetValue());
                AddStat(items, StatEntryType.Defense, GameText.Get(_owner._localization, "Commanders/Tooltip/Defense", "Defence"), commander.Stats.Defense.GetValue());
                AddStat(items, StatEntryType.Movement, GameText.Get(_owner._localization, "Commanders/Tooltip/Movement", "Movement"), (int)commander.Stats.Movement.GetValue());
                AddStat(items, StatEntryType.View, GameText.Get(_owner._localization, "Commanders/Tooltip/ViewRadius", "View Radius"), (int)commander.Stats.ViewRadius.GetValue());
                return items;
            }

            private void AddStat(List<LabeledItem> items, StatEntryType type, string label, int value)
            {
                items.Add(new LabeledItem(
                    label,
                    value.ToString(CultureInfo.CurrentCulture),
                    Tooltip.ForComponent(GetStatTooltipComponent(type), _owner._localization)));
            }

            private Component GetStatTooltipComponent(StatEntryType type)
            {
                CommanderStatsInfo statsInfo = _left ? Settings.leftStatsInfo : Settings.rightStatsInfo;
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

            // ---- the modifier tabs and the showing tab's lines ----

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
                UIButton button = GetModifierCategoryButton(ModifierTabs, index);
                return new ModifierCategory(
                    _owner.GetLocalizedText(key, fallback),
                    index,
                    button as Component,
                    Tooltip.ForComponent(button as Component, _owner._localization));
            }

            public int GetActiveModifierCategoryIndex()
            {
                object state = CurrentTabStateField != null && ModifierTabs != null
                    ? CurrentTabStateField.GetValue(ModifierTabs)
                    : null;
                return state != null ? (int)state : 0;
            }

            /// <summary>The title the game itself writes over the showing tab's list.</summary>
            public string GetActiveModifierListLabel()
            {
                UITextMesh title = GetField<UITextMesh>(ModifierTabs, ModifierTitleField);
                return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(title));
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
                        string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text));
                        if (!string.IsNullOrWhiteSpace(label))
                        {
                            items.Add(new LabeledItem(label));
                        }
                    }
                }

                if (items.Count == 0)
                {
                    UITextMesh noneText = GetField<UITextMesh>(ModifierTabs, NoModifiersTextField);
                    string label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(noneText));
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        items.Add(new LabeledItem(label));
                    }
                }

                return items;
            }

            /// <summary>Move the game's selection onto a modifier tab WITHOUT switching to it: the
            /// switch redraws the list under the bar, so arriving at a tab must not take the list the
            /// player is reading away.</summary>
            public bool SelectModifierCategory(int categoryIndex)
            {
                return NativeSelectionUtility.Select(GetModifierCategoryButton(ModifierTabs, categoryIndex) as Component);
            }

            private CommanderSheetModifierTabNavigation ModifierTabs
            {
                get { return _left ? Settings.leftModifierTabs : Settings.rightModifierTabs; }
            }

            private Transform GetActiveModifierContent()
            {
                switch (GetActiveModifierCategoryIndex())
                {
                    case 1:
                        return GetContentTransform(TemporaryModifierContentField);
                    case 2:
                        return GetContentTransform(GearModifierContentField);
                    default:
                        return GetContentTransform(TroopModifierContentField);
                }
            }

            private Transform GetContentTransform(FieldInfo field)
            {
                GameObject content = GetField<GameObject>(ModifierTabs, field);
                return content != null ? content.transform : null;
            }

            // ---- the Move all button under the army ----

            /// <summary>The button that hands the whole army to the other side
            /// (<c>TradingMenu.HandleLeftMoveAllButtonClicked</c>).</summary>
            public Component MoveAllButton
            {
                get { return MoveAll as Component; }
            }

            /// <summary>The game's own name for it: the button draws no text, only the tooltip it
            /// carries.</summary>
            public string MoveAllLabel
            {
                get
                {
                    UIButton button = MoveAll;
                    string label = MenuButtonTextUtility.GetStandardButtonLabel(button);
                    if (!string.IsNullOrWhiteSpace(label))
                    {
                        return label;
                    }

                    Tooltip tooltip = Tooltip.ForComponent(button, _owner._localization);
                    IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
                    return lines != null && lines.Count > 0 ? SpokenLines.Clean(lines[0]) : string.Empty;
                }
            }

            /// <summary>Whether the game will take the click: it sets <c>Interactable</c> from
            /// <c>CanMassMoveTroops</c> whenever the armies change.</summary>
            public bool IsMoveAllEnabled()
            {
                UIButton button = MoveAll;
                return button != null && button.Active && button.Interactable;
            }

            public bool ActivateMoveAll()
            {
                return NativeSelectionUtility.Click(MoveAll);
            }

            private UIButton MoveAll
            {
                get { return _left ? Settings.leftMoveAllButton : Settings.rightMoveAllButton; }
            }

            // ---- the artifacts ----

            public string EquipmentLabel
            {
                get { return _owner.GetLocalizedText("Common/CommanderInventory/Equipment", "Equipment"); }
            }

            public string InventoryLabel
            {
                get { return GetInventoryLabel(); }
            }

            public IReadOnlyList<InventorySlotInfo> GetEquipmentSlots()
            {
                List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
                InventoryHUD inventory = Inventory;
                InventorySlot[] slots = InventorySlotInfo.DrawnEquipmentSlots;
                int commanderId = CommanderId;
                string ownerName = _owner.GetCommanderName(commanderId);
                for (int i = 0; i < slots.Length; i++)
                {
                    InventorySlot slot = slots[i];
                    InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(slot) : null;
                    IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(commanderId, slot);
                    bool displayOnly = IsDisplayOnlyEquipmentArtifact(slot, artifact);
                    InventoryArtifactMovable nativeMovable = nativeSlot != null ? nativeSlot.TryGetArtifact(0) : null;
                    InventoryArtifactMovable artifactMovable = nativeMovable ?? GetArtifactMovable(artifact);
                    InventoryArtifactMovable movable = displayOnly ? null : artifactMovable;
                    InventoryHUDSlot capturedNativeSlot = nativeSlot;
                    InventoryArtifactMovable capturedMovable = movable;
                    Selectable tooltipSelectable = movable != null
                        ? movable.GetSelectable()
                        : displayOnly && artifactMovable != null
                            ? artifactMovable.GetSelectable()
                            : GetEquipmentSlotSelectable(capturedNativeSlot);
                    slotsInfo.Add(new InventorySlotInfo(
                        commanderId,
                        ownerName,
                        slot,
                        0,
                        isBackpackSlot: false,
                        GetInventorySlotName(slot.ToString()),
                        GetInventoryLabel(),
                        artifact != null ? GetArtifactName(artifact) : string.Empty,
                        movable,
                        nativeSlot,
                        BuildArtifactTooltip(artifact, artifactMovable, tooltipSelectable),
                        () => SelectInventoryCell(capturedNativeSlot, capturedMovable, 0)));
                }

                return slotsInfo;
            }

            public IReadOnlyList<InventorySlotInfo> GetBackpackSlots()
            {
                List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
                InventoryHUD inventory = Inventory;
                InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(InventorySlot.None) : null;
                int commanderId = CommanderId;
                string ownerName = _owner.GetCommanderName(commanderId);
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
                        BuildArtifactTooltip(artifact, movable, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i)),
                        () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
                }

                return slotsInfo;
            }

            /// <summary>Put an artifact down on a slot, through the game's own check and its own move.
            /// The utility branches on the owner, so a drop on the OTHER side goes down the game's
            /// give path rather than its rearrange path.</summary>
            public DropResult DropArtifact(InventoryArtifactMovable movable, InventorySlotInfo target)
            {
                return ArtifactDropUtility.DropArtifact(_owner._facade, movable, target, "TradingMenuAdapter artifact drop");
            }

            /// <summary>Whether the game would accept this artifact here, asked without doing anything
            /// - the same two checks its own drop makes, chosen the same way.</summary>
            public bool CanRearrangeArtifactTo(InventoryArtifactMovable movable, InventorySlotInfo target)
            {
                return ArtifactDropUtility.CanDrop(_owner._facade, movable, target, "TradingMenuAdapter artifact drop check");
            }

            /// <summary>The game's own notification for a rearrangement its Command skill blocks.
            /// </summary>
            public string RearrangeRefusalText
            {
                get { return _owner.GetLocalizedText("Common/CommanderInventory/RearrangeArtifact/CannotRearrangeBecauseOfCommand", string.Empty); }
            }

            /// <summary>Nothing in a trade listens to the inventory's left click.</summary>
            public bool AnswersLeftClick(InventorySlotInfo slot)
            {
                return false;
            }

            /// <summary>The artifact's LEFT click, through the button the game hangs its own handler
            /// on - inert in a trade, and with Ctrl physically held the game's own drop on the ground.
            /// </summary>
            public bool LeftClickArtifact(InventorySlotInfo slot)
            {
                return NativeSelectionUtility.Click(GetMovableButton(slot));
            }

            /// <summary>The artifact's RIGHT click, through the same button, which in a trade MOVES IT
            /// TO THE OTHER BACKPACK (<c>InventoryHUD.EquipArtifact</c> answers a set other inventory
            /// with <c>MoveItemToOtherBackpack</c>), and with Ctrl physically held destroys it.
            /// </summary>
            public bool RightClickArtifact(InventorySlotInfo slot)
            {
                return NativeSelectionUtility.RightClick(GetMovableButton(slot));
            }

            /// <summary>What a right click on this artifact does, as the GAME decides it in
            /// <c>InventoryArtifactMovable.GetDetails</c>.</summary>
            public ArtifactDetails.EquipInstruction GetArtifactInstruction(InventorySlotInfo slot)
            {
                InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
                IArtifactState artifact = movable != null ? movable.State : null;
                if (artifact == null)
                {
                    return ArtifactDetails.EquipInstruction.None;
                }

                IArtifactDataDefinition definition = _owner._artifactLookup != null
                    ? _owner._artifactLookup.GetDefinition(artifact.Type)
                    : null;
                if (definition != null && definition.Action != null)
                {
                    return ArtifactDetails.EquipInstruction.Use;
                }

                return artifact.IsEquipped
                    ? ArtifactDetails.EquipInstruction.Unequip
                    : ArtifactDetails.EquipInstruction.Equip;
            }

            /// <summary>Whether the artifact in the main hand takes BOTH hands, which is what makes the
            /// game draw a ghost of it in the off hand.</summary>
            public bool IsMainHandTwoHanded()
            {
                IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(CommanderId, InventorySlot.MainHand);
                return artifact != null
                    && _owner._artifactLookup != null
                    && _owner._artifactLookup.GetSlot(artifact.Type) == ArtifactSlot.BothHands;
            }

            /// <summary>Whether an artifact fits the off hand ALONE - the one case the game's own
            /// right-click resolution sends to the off hand rather than the main one.</summary>
            public bool IsOffHandOnlyArtifact(InventoryArtifactMovable movable)
            {
                return movable != null
                    && movable.State != null
                    && _owner._artifactLookup != null
                    && _owner._artifactLookup.GetSlot(movable.State.Type) == ArtifactSlot.OffHand;
            }

            /// <summary>The game's own name for the two-handed slot ("Both Hands").</summary>
            public string BothHandsSlotName
            {
                get { return GetInventorySlotName(ArtifactSlot.BothHands.ToString()); }
            }

            /// <summary>The game's own text for the auto-arrange instruction.</summary>
            public string AutoArrangeText
            {
                get { return _owner.GetLocalizedText("Adventure/TooltipInstruction/AutoArrange", string.Empty); }
            }

            /// <summary>Auto-arrange, the game's own middle click. Its second half only remembers which
            /// cell to re-select afterwards and needs an artifact to remember, so with nothing in the
            /// inventory the command it runs is called on its own.</summary>
            public bool AutoArrangeArtifacts()
            {
                InventoryHUD inventory = Inventory;
                if (inventory == null)
                {
                    return false;
                }

                InventoryArtifactMovable anyArtifact = FirstArtifactMovable();
                if (anyArtifact != null)
                {
                    inventory.AutoArrangeArtifacts(anyArtifact);
                    return true;
                }

                if (_owner._facade == null || CommanderId < 0)
                {
                    return false;
                }

                _owner._facade.Commands.EquipBestArtifacts(CommanderId);
                return true;
            }

            private InventoryHUD Inventory
            {
                get { return _left ? Settings.leftInventory : Settings.rightInventory; }
            }

            private TradingMenu.Settings Settings
            {
                get { return _owner._settings; }
            }

            private IUIButton GetMovableButton(InventorySlotInfo slot)
            {
                InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
                return movable != null && MovableButtonField != null
                    ? MovableButtonField.GetValue(movable) as IUIButton
                    : null;
            }

            private InventoryArtifactMovable FirstArtifactMovable()
            {
                IDictionary artifactMap = InventoryArtifactMapField != null && Inventory != null
                    ? InventoryArtifactMapField.GetValue(Inventory) as IDictionary
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

            private static Selectable GetEquipmentSlotSelectable(InventoryHUDSlot nativeSlot)
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
                if (_owner._facade == null || commanderId < 0)
                {
                    return null;
                }

                if (slot == InventorySlot.OffHand)
                {
                    return _owner._facade.Artifacts.GetForOwner(commanderId, ArtifactSlot.OffHand).FirstOrDefault();
                }

                return _owner._facade.Artifacts.GetForOwner(commanderId, slot).FirstOrDefault();
            }

            private static bool IsDisplayOnlyEquipmentArtifact(InventorySlot slot, IArtifactState artifact)
            {
                return slot == InventorySlot.OffHand
                    && artifact != null
                    && artifact.EquippedInSlot == InventorySlot.MainHand;
            }

            private InventoryArtifactMovable GetArtifactMovable(IArtifactState artifact)
            {
                InventoryHUD inventory = Inventory;
                InventoryHUDSlot nativeSlot = inventory != null && artifact != null
                    ? inventory.GetSlot(artifact.EquippedInSlot)
                    : null;
                return nativeSlot != null ? nativeSlot.TryGetArtifact(artifact.PositionIndex) : null;
            }

            /// <summary>The artifact's own tooltip with the game's mouse instructions taken out of it:
            /// the instruction lines are drawn for a mouse and the graph says the keyboard's gestures
            /// itself.</summary>
            private Tooltip BuildArtifactTooltip(IArtifactState artifact, InventoryArtifactMovable movable, Selectable selectable)
            {
                Tooltip tooltip = Tooltip.ForComponent(selectable as Component, _owner._localization);
                if (tooltip == null || artifact == null || movable == null || _owner._localization == null)
                {
                    return tooltip;
                }

                List<string> instructionLines = new List<string>();
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Trade");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Equip");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Unequip");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Destroy");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Destroy.Gamepad");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Drop");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Drop.Gamepad");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/AutoArrange");
                AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/AutoArrange.Gamepad");
                return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata);
            }

            private void AddLocalizedLine(List<string> lines, string key)
            {
                string line = _owner._localization != null ? _owner._localization.GetText(key) : string.Empty;
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
                    if (!linesToRemove.Contains(lines[i], StringComparer.Ordinal))
                    {
                        result.Add(lines[i]);
                    }
                }

                return result;
            }

            private string GetArtifactName(IArtifactState artifact)
            {
                if (artifact == null)
                {
                    return string.Empty;
                }

                try
                {
                    return ArtifactSpeechFormatter.FormatName(artifact, _owner._artifactLookup, _owner._localization);
                }
                catch (Exception ex)
                {
                    SocAccessMod.Instance?.LogWarning("TradingMenuAdapter could not get artifact rarity color: " + ex.Message);
                    return _owner._artifactLookup != null
                        ? _owner._artifactLookup.GetLocalizedName(artifact.Type)
                        : artifact.Type.ToString();
                }
            }

            private string GetInventorySlotName(string slot)
            {
                string text = _owner._localization != null ? _owner._localization.GetText("InventorySlots/" + slot) : string.Empty;
                return string.IsNullOrWhiteSpace(text) || text == "InventorySlots/" + slot
                    ? FormatSlotName(slot)
                    : SpokenLines.Clean(text);
            }

            private string GetInventoryLabel()
            {
                return _owner.GetLocalizedText("Common/CommanderInventory/Inventory", "Inventory");
            }

            private static string FormatSlotName(string value)
            {
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
        }

        /// <summary>One read-only line of a band: the game's name for it, what it says, and the
        /// breakdown the game draws on hover.</summary>
        public sealed class LabeledItem
        {
            public LabeledItem(string label, string value = null, Tooltip tooltip = null)
            {
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public string Value { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }

        /// <summary>One tab of a side's modifier bar.</summary>
        public sealed class ModifierCategory
        {
            public ModifierCategory(string label, int index, Component button, Tooltip tooltip = null)
            {
                Label = label ?? string.Empty;
                Index = index;
                Button = button;
                Tooltip = tooltip;
            }

            public string Label { get; private set; }
            public int Index { get; private set; }
            public Component Button { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
