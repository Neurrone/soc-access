using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ArtifactMarketMenuAdapter : IPresent, IArtifactSlots
    {
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(ArtifactMarketMenu), "_headerText");
        private static readonly FieldInfo DescriptionTextField = AccessTools.Field(typeof(ArtifactMarketMenu), "_descriptionText");
        private static readonly FieldInfo InventoryField = AccessTools.Field(typeof(ArtifactMarketMenu), "_inventoryHUD");
        private static readonly FieldInfo WielderInteractHeaderField = AccessTools.Field(typeof(ArtifactMarketMenu), "_wielderInteractHeader");
        private static readonly FieldInfo CategoryTabGroupField = AccessTools.Field(typeof(ArtifactMarketMenu), "_categoryTabGroup");
        private static readonly FieldInfo GridContainerField = AccessTools.Field(typeof(ArtifactMarketMenu), "_gridContainer");
        private static readonly FieldInfo BuyButtonField = AccessTools.Field(typeof(ArtifactMarketMenu), "_buyButton");
        private static readonly FieldInfo SelectedBuyArtifactField = AccessTools.Field(typeof(ArtifactMarketMenu), "_selectedBuyArtifact");
        private static readonly FieldInfo CurrentCategoryIndexField = AccessTools.Field(typeof(ArtifactMarketMenu), "_currentCategoryIndex");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(ArtifactMarketMenu), "_async");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(ArtifactMarketMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(ArtifactMarketMenu), "_localizationHandler");
        private static readonly FieldInfo ArtifactLookupField = AccessTools.Field(typeof(ArtifactMarketMenu), "_artifactLookup");
        private static readonly FieldInfo NoSelectionContainerField = AccessTools.Field(typeof(ArtifactMarketMenu), "_noSelectionContainer");
        private static readonly FieldInfo BuyContainerField = AccessTools.Field(typeof(ArtifactMarketMenu), "_buyContainer");
        private static readonly FieldInfo BuyItemTitleField = AccessTools.Field(typeof(ArtifactMarketMenu), "_buyItemTitle");
        private static readonly FieldInfo BuyItemIconField = AccessTools.Field(typeof(ArtifactMarketMenu), "_buyItemIcon");
        private static readonly FieldInfo SellContainerField = AccessTools.Field(typeof(ArtifactMarketMenu), "_sellContainer");
        private static readonly FieldInfo SellItemTitleField = AccessTools.Field(typeof(ArtifactMarketMenu), "_sellItemTitle");
        private static readonly FieldInfo SellItemIconField = AccessTools.Field(typeof(ArtifactMarketMenu), "_sellItemIcon");
        private static readonly FieldInfo SellButtonField = AccessTools.Field(typeof(ArtifactMarketMenu), "_sellButton");
        private static readonly FieldInfo SellButtonTitleField = AccessTools.Field(typeof(ArtifactMarketMenu), "_sellButtonTitle");
        private static readonly FieldInfo SelectedSellArtifactField = AccessTools.Field(typeof(ArtifactMarketMenu), "_selectedSellArtifact");
        private static readonly FieldInfo InventoryArtifactMapField = AccessTools.Field(typeof(InventoryHUD), "_artifactStateToGOMap");
        private static readonly FieldInfo PurchaseButtonButtonField = AccessTools.Field(typeof(PurchaseButton), "_button");
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");
        private static readonly FieldInfo MovableButtonField = AccessTools.Field(typeof(InventoryArtifactMovable), "_button");
        private static readonly FieldInfo MarketEntryButtonField = AccessTools.Field(typeof(ArtifactMarketEntry), "_button");

        private readonly ArtifactMarketMenu _menu;
        private readonly InventoryHUD _inventory;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly IArtifactLookup _artifactLookup;

        // The three subtrees the build walks every frame: the category tab strip, the offer grid,
        // and the buy and sell bands, whose word over the button is read as a context name and so
        // is composed eagerly. Keyed on the frame rather than held, because the grid is pooled and
        // a retired entry must not answer for the next category.
        private readonly FrameSweep<UIToggle> _categoryToggles =
            new FrameSweep<UIToggle>("artifact market categories");
        private readonly FrameSweep<ArtifactMarketEntry> _marketEntries =
            new FrameSweep<ArtifactMarketEntry>("artifact market grid", inactiveToo: false);
        private readonly FrameSweep<UITextMesh> _bandTexts =
            new FrameSweep<UITextMesh>("artifact market band");

        // The wielder's two artifact lists, kept while the game's own inventory is unchanged. The
        // key is read off the game every frame (the wielder the panel is showing, the pooled cells,
        // and what is in each of them), so a sale, a purchase, a move and an auto-arrange all
        // rebuild with nothing having to say so (AGENTS.md, Screen Resolution).
        private readonly SlotSnapshot _equipment = new SlotSnapshot();

        private readonly SlotSnapshot _backpack = new SlotSnapshot();

        // The nine rows the game writes for a mouse. They are the same for every slot and for the
        // menu's whole life, so they are looked up once instead of nine times a slot a frame.
        private List<string> _mouseInstructionLines;
        private WielderInteract _wielder;

        public ArtifactMarketMenuAdapter(ArtifactMarketMenu menu)
        {
            _menu = menu;
            _inventory = GetField<InventoryHUD>(menu, InventoryField);
            _facade = GetField<IClientAdventureFacade>(menu, FacadeField);
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
            _artifactLookup = GetField<IArtifactLookup>(menu, ArtifactLookupField);
        }

        public ArtifactMarketMenu Source
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

        public int CommanderId
        {
            get { return _inventory != null ? _inventory.OwnerId : -1; }
        }

        public bool IsPresent()
        {
            return _menu != null
                && GetField<object>(_menu, AsyncField) != null
                && _inventory != null
                && _inventory.IsArtifactShopInventory
                && ((Component)_menu).gameObject.activeInHierarchy;
        }

        public string Title
        {
            get
            {
                string title = GetText(GetField<UITextMesh>(_menu, HeaderTextField));
                return string.IsNullOrWhiteSpace(title)
                    ? ModText.Get(_localization, ModStrings.Scanner.ArtifactMarkets)
                    : title;
            }
        }

        public string Description
        {
            get { return GetText(GetField<UITextMesh>(_menu, DescriptionTextField)); }
        }

        public string EquipmentLabel
        {
            get { return GetLocalizedText("Common/CommanderInventory/Equipment", "Equipment"); }
        }

        public string InventoryLabel
        {
            get { return GetInventoryLabel(); }
        }

        /// <summary>The band across the top of the menu: the wielder who walked in, their army, and
        /// the market's own name where it has one. The same band every wielder-interaction menu draws.
        /// </summary>
        public WielderInteract Wielder
        {
            get
            {
                WielderInteractHeader header = GetWielderInteractHeader();
                if (_wielder == null || !ReferenceEquals(_wielder.Header, header))
                {
                    _wielder = new WielderInteract(header, _facade, _localization);
                }

                return _wielder;
            }
        }

        /// <summary>The close cross the window itself draws, which the game turns on only where the
        /// menu may be closed and the player is on mouse and keyboard
        /// (<c>AdventureMenuBackground.AnimateEntry</c>). The wielder band draws a second cross wired
        /// to the same handler.</summary>
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
            return GetField<UIButton>(_menu, BackgroundCloseButtonField);
        }

        public int ActiveCategoryIndex
        {
            get { return GetFieldValue(_menu, CurrentCategoryIndexField, 0); }
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

        /// <summary>
        /// The category filters, in the order the menu draws them. Each is named by the GAME's own
        /// tooltip on the toggle ("Head", "Main Hand", ... , "Buyback"); the first toggle, which shows
        /// everything, is the only one the game gives no words at all, so the mod names that one.
        /// </summary>
        public IReadOnlyList<CategoryItem> GetCategories()
        {
            List<CategoryItem> items = new List<CategoryItem>();
            UIToggle[] toggles = GetCategoryToggles();
            for (int i = 0; i < toggles.Length; i++)
            {
                UIToggle toggle = toggles[i];
                string label = FirstLine(Tooltip.ForComponent(toggle, _localization));
                items.Add(new CategoryItem(
                    string.IsNullOrWhiteSpace(label) ? ModText.Get(_localization, ModStrings.Scanner.All) : label,
                    i,
                    toggle));
            }

            return items;
        }

        /// <summary>Switch to a category through the game's own toggle group, which is what the menu
        /// hangs <c>HandleSwitchedCategory</c> on.</summary>
        public bool SelectCategory(int categoryIndex)
        {
            UIToggleGroup group = GetField<UIToggleGroup>(_menu, CategoryTabGroupField);
            if (group == null || categoryIndex < 0 || categoryIndex >= GetCategoryToggles().Length)
            {
                return false;
            }

            group.SetActiveToggle(categoryIndex);
            return true;
        }

        /// <summary>Move the game's selection onto a category toggle WITHOUT switching to it: the
        /// switch rebuilds the grid and clears the selection, so arriving at a filter must not take
        /// the offers the player is reading away.</summary>
        public bool FocusCategory(int categoryIndex)
        {
            UIToggle[] toggles = GetCategoryToggles();
            return categoryIndex >= 0
                && categoryIndex < toggles.Length
                && NativeSelectionUtility.Select(toggles[categoryIndex].GetSelectable());
        }

        private UIToggle[] GetCategoryToggles()
        {
            UIToggleGroup group = GetField<UIToggleGroup>(_menu, CategoryTabGroupField);
            return group == null ? new UIToggle[0] : _categoryToggles.Under((Component)group);
        }

        public IReadOnlyList<MarketArtifactItem> GetMarketArtifacts()
        {
            List<MarketArtifactItem> items = new List<MarketArtifactItem>();
            GameObject gridContainer = GetField<GameObject>(_menu, GridContainerField);
            if (gridContainer == null)
            {
                return items;
            }

            // A copy, because the sweep's answer is shared for the rest of the frame and the order
            // it was walked in is what the next caller expects.
            ArtifactMarketEntry[] entries = (ArtifactMarketEntry[])_marketEntries.Under(gridContainer.transform).Clone();
            Array.Sort(entries, CompareSiblingIndex);
            for (int i = 0; i < entries.Length; i++)
            {
                ArtifactMarketEntry entry = entries[i];
                IArtifactState artifact = entry != null ? entry.ArtifactState : null;
                if (artifact == null)
                {
                    continue;
                }

                items.Add(new MarketArtifactItem(
                    GetArtifactName(artifact),
                    GetArtifactBuyCostLabel(artifact),
                    entry,
                    Tooltip.ForComponent(entry.GetSelectable(), _localization)));
            }

            return items;
        }

        public bool SelectMarketEntry(ArtifactMarketEntry entry)
        {
            if (entry == null)
            {
                return false;
            }

            NativeSelectionUtility.Select(entry.GetSelectable());
            return true;
        }

        /// <summary>The offer's own pointer click, into the button the game hangs <c>OnClicked</c> on,
        /// which is what fills the Buy band.</summary>
        public bool SelectMarketEntryForPurchase(ArtifactMarketEntry entry)
        {
            if (entry == null || entry.ArtifactState == null)
            {
                return false;
            }

            return NativeSelectionUtility.Click(GetField<UIButton>(entry, MarketEntryButtonField));
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
            string ownerName = GetCommanderName(commanderId);
            string inventoryName = GetInventoryLabel();
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlot slot = slots[i];
                InventoryHUDSlot nativeSlot = _inventory != null ? _inventory.GetSlot(slot) : null;
                IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(slot);
                bool displayOnly = IsDisplayOnlyEquipmentArtifact(slot, artifact);
                InventoryArtifactMovable nativeMovable = nativeSlot != null ? nativeSlot.TryGetArtifact(0) : null;
                InventoryArtifactMovable artifactMovable = nativeMovable ?? GetArtifactMovable(artifact);
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
                    inventoryName,
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildInventoryArtifactTooltip(artifact, artifactMovable, tooltipSelectable),
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
            string ownerName = GetCommanderName(commanderId);
            string inventoryName = GetInventoryLabel();
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
                    inventoryName,
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildInventoryArtifactTooltip(artifact, movable, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i)),
                    () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
            }

            return _backpack.Keep(slotsInfo);
        }

        // ---- the gestures the game gives an artifact in this menu ----

        /// <summary>Put an artifact down on a slot, through the game's own check and its own move.
        /// </summary>
        public DropResult DropArtifact(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return ArtifactDropUtility.DropArtifact(_facade, movable, target, "ArtifactMarketMenuAdapter artifact drop");
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
                SocAccessMod.Instance?.LogWarning("ArtifactMarketMenuAdapter could not ask whether an artifact fits: " + ex.Message);
                return false;
            }
        }

        /// <summary>The game's own notification for a rearrangement its Command skill blocks - what it
        /// shows itself when the drop is refused with error code 10.</summary>
        public string RearrangeRefusalText
        {
            get { return GetLocalizedText("Common/CommanderInventory/RearrangeArtifact/CannotRearrangeBecauseOfCommand", string.Empty); }
        }

        /// <summary>The market answers the left click on an artifact by selecting it for sale; an
        /// empty slot has no artifact to click.</summary>
        public bool AnswersLeftClick(InventorySlotInfo slot)
        {
            return slot != null && slot.Movable != null;
        }

        /// <summary>The artifact's LEFT click, through the button the game hangs its own handler on.
        /// In this menu the game answers it by SELECTING THE ARTIFACT FOR SALE
        /// (<c>ArtifactMarketMenu.HandleInventoryArtifactClicked</c>), and with Ctrl physically held it
        /// drops the artifact on the ground first. The shop's own answer to the click takes the
        /// artifact's tooltip down with it (<c>ArtifactMarketMenu.HandleInventoryArtifactClicked</c>
        /// rebuilds the band and the game stops drawing the hover), so the cell is selected again
        /// afterwards: the cursor has not moved, and the tooltip the player was reading comes back.
        /// </summary>
        public bool LeftClickArtifact(InventorySlotInfo slot)
        {
            bool clicked = NativeSelectionUtility.Click(GetMovableButton(slot));
            if (clicked && slot != null)
            {
                slot.FocusNative();
            }

            return clicked;
        }

        /// <summary>The artifact's RIGHT click, through the same button: equip, unequip or use, and
        /// with Ctrl physically held the game SELLS it here rather than destroying it
        /// (<c>InventoryArtifactMovable.HandleRightClick</c> branches on
        /// <c>InventoryHUD.IsArtifactShopInventory</c>).</summary>
        public bool RightClickArtifact(InventorySlotInfo slot)
        {
            return NativeSelectionUtility.RightClick(GetMovableButton(slot));
        }

        /// <summary>What a right click on this artifact does, as the GAME decides it in
        /// <c>InventoryArtifactMovable.GetDetails</c>: an artifact whose definition carries an action
        /// is used, and every other one is equipped or unequipped by where it currently is.</summary>
        public ArtifactDetails.EquipInstruction GetArtifactInstruction(InventorySlotInfo slot)
        {
            InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
            IArtifactState artifact = movable != null ? movable.State : null;
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

        /// <summary>Whether the artifact in the main hand takes BOTH hands - the definition's own slot
        /// (<c>IArtifactLookup.GetSlot</c>), which is what makes the game draw a ghost of it in the off
        /// hand.</summary>
        public bool IsMainHandTwoHanded()
        {
            IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(InventorySlot.MainHand);
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

        // ---- the selection band at the foot of the menu ----

        /// <summary>Whether the band is showing its prompt, which is what it shows while nothing is
        /// selected.</summary>
        public bool IsNoSelectionShown
        {
            get { return IsActive(GetField<GameObject>(_menu, NoSelectionContainerField)); }
        }

        /// <summary>The prompt the band draws while nothing is selected.</summary>
        public string NoSelectionText
        {
            get { return FirstText(GetField<GameObject>(_menu, NoSelectionContainerField)); }
        }

        /// <summary>The container the prompt is drawn in, one of the band's three.</summary>
        public Component NoSelectionContainer
        {
            get { return ContainerTransform(GetField<GameObject>(_menu, NoSelectionContainerField)); }
        }

        /// <summary>The container the purchase is drawn in, one of the band's three.</summary>
        public Component BuyContainer
        {
            get { return ContainerTransform(GetField<GameObject>(_menu, BuyContainerField)); }
        }

        /// <summary>The container the sale is drawn in, one of the band's three.</summary>
        public Component SellContainer
        {
            get { return ContainerTransform(GetField<GameObject>(_menu, SellContainerField)); }
        }

        /// <summary>Whether the band is showing the artifact the player is buying.</summary>
        public bool IsBuyShown
        {
            get { return IsActive(GetField<GameObject>(_menu, BuyContainerField)); }
        }

        /// <summary>The tooltip on the Buy band's icon: the game hangs the artifact's details on it
        /// (<c>ArtifactMarketMenu.SetArtifact</c>, <c>UIImage.SetDetails</c>), which is what the mouse
        /// sees hovering the band.</summary>
        public Tooltip BuyItemTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIImage>(_menu, BuyItemIconField) as Component, _localization); }
        }

        /// <summary>The name of the artifact the Buy band is about, as the band draws it.</summary>
        public string BuyItemName
        {
            get { return GetText(GetField<UITextMesh>(_menu, BuyItemTitleField)); }
        }

        /// <summary>The word on the Buy button, as the band draws it above it.</summary>
        public string BuyButtonLabel
        {
            get
            {
                string title = GetBandButtonTitle(
                    GetField<GameObject>(_menu, BuyContainerField),
                    GetField<UITextMesh>(_menu, BuyItemTitleField),
                    GetField<PurchaseButton>(_menu, BuyButtonField));
                return string.IsNullOrWhiteSpace(title) ? ModText.Get(_localization, ModStrings.Screens.BuyArtifact) : title;
            }
        }

        /// <summary>What the Buy button charges - the same cost it draws in its own price box.
        /// </summary>
        public string BuyPriceLabel
        {
            get { return GetArtifactBuyCostLabel(GetSelectedBuyArtifact()); }
        }

        /// <summary>The Buy button itself, for the focus visual and the node's identity.</summary>
        public Component BuyButton
        {
            get { return GetField<PurchaseButton>(_menu, BuyButtonField) as Component; }
        }

        /// <summary>Whether the game will take the purchase: it turns the button off when the team
        /// cannot afford the price, and says nothing about why.</summary>
        public bool CanBuySelectedArtifact()
        {
            PurchaseButton buyButton = GetField<PurchaseButton>(_menu, BuyButtonField);
            return GetSelectedBuyArtifact() != null && buyButton != null && buyButton.Interactable;
        }

        /// <summary>Buy, through the game's own click on its Buy button.</summary>
        public bool BuySelectedMarketArtifact()
        {
            PurchaseButton buyButton = GetField<PurchaseButton>(_menu, BuyButtonField);
            return NativeSelectionUtility.Click(GetField<UIButton>(buyButton, PurchaseButtonButtonField));
        }

        /// <summary>Whether the band is showing the artifact the player has picked out to sell.
        /// </summary>
        public bool IsSellShown
        {
            get { return IsActive(GetField<GameObject>(_menu, SellContainerField)); }
        }

        /// <summary>The tooltip on the Sell band's icon: the game hangs the artifact's details on it
        /// (<c>ArtifactMarketMenu.SetArtifact</c>, <c>UIImage.SetDetails</c>), which is what the mouse
        /// sees hovering the band.</summary>
        public Tooltip SellItemTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIImage>(_menu, SellItemIconField) as Component, _localization); }
        }

        /// <summary>The name of the artifact the Sell band is about, as the band draws it.</summary>
        public string SellItemName
        {
            get { return GetText(GetField<UITextMesh>(_menu, SellItemTitleField)); }
        }

        /// <summary>Whether the Sell button is drawn at all: the game hides it for an artifact it
        /// treats as important (<c>ArtifactMarketMenu.SetArtifact</c>).</summary>
        public bool IsSellButtonShown
        {
            get { return IsActive(ButtonObject(GetField<PurchaseButton>(_menu, SellButtonField))); }
        }

        /// <summary>The word on the Sell button, as the band draws it above it.</summary>
        public string SellButtonLabel
        {
            get
            {
                string title = GetText(GetField<UITextMesh>(_menu, SellButtonTitleField));
                return string.IsNullOrWhiteSpace(title) ? ModText.Get(_localization, ModStrings.Screens.Sell) : title;
            }
        }

        /// <summary>What the Sell button pays - the same value it draws in its own price box.</summary>
        public string SellPriceLabel
        {
            get
            {
                IArtifactState artifact = GetField<IArtifactState>(_menu, SelectedSellArtifactField);
                if (artifact == null || _facade == null || _facade.Artifacts == null)
                {
                    return string.Empty;
                }

                return FormatCost(_facade.Artifacts.GetArtifactMarketSellValue(artifact.Type));
            }
        }

        /// <summary>The Sell button itself, for the focus visual and the node's identity.</summary>
        public Component SellButton
        {
            get { return GetField<PurchaseButton>(_menu, SellButtonField) as Component; }
        }

        public bool CanSellSelectedArtifact()
        {
            PurchaseButton sellButton = GetField<PurchaseButton>(_menu, SellButtonField);
            return sellButton != null && sellButton.Interactable;
        }

        /// <summary>Sell, through the game's own click on its Sell button.</summary>
        public bool SellSelectedArtifact()
        {
            PurchaseButton sellButton = GetField<PurchaseButton>(_menu, SellButtonField);
            return NativeSelectionUtility.Click(GetField<UIButton>(sellButton, PurchaseButtonButtonField));
        }

        /// <summary>The one text a selection band draws that is neither the artifact's name nor a
        /// price inside the button: the word over the button ("Buy", "Sell").</summary>
        private string GetBandButtonTitle(GameObject container, UITextMesh itemTitle, PurchaseButton button)
        {
            if (container == null)
            {
                return string.Empty;
            }

            Transform buttonRoot = button != null ? ((Component)button).transform : null;
            UITextMesh[] texts = _bandTexts.Under(container.transform);
            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || ReferenceEquals(text, itemTitle))
                {
                    continue;
                }

                if (buttonRoot != null && ((Component)text).transform.IsChildOf(buttonRoot))
                {
                    continue;
                }

                return GetText(text);
            }

            return string.Empty;
        }

        private static Component ContainerTransform(GameObject container)
        {
            return container == null ? null : container.transform;
        }

        private static GameObject ButtonObject(PurchaseButton button)
        {
            return button == null ? null : ((Component)button).gameObject;
        }

        private static bool IsActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        // LAZY: reached only through GraphNodes.Text's Func for the "no selection" line, so the walk
        // is paid when that line is read rather than on every build.
        private static string FirstText(GameObject container)
        {
            UITextMesh text = container == null ? null : container.GetComponentInChildren<UITextMesh>(true);
            return GetText(text);
        }

        private static string FirstLine(Tooltip tooltip)
        {
            IReadOnlyList<string> lines = tooltip == null ? null : tooltip.TextLines;
            return lines != null && lines.Count > 0 ? lines[0] : string.Empty;
        }

        private static IUIButton GetMovableButton(InventorySlotInfo slot)
        {
            InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
            return movable != null && MovableButtonField != null
                ? MovableButtonField.GetValue(movable) as IUIButton
                : null;
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
                InventoryArtifactMovable artifactMovable = movable as InventoryArtifactMovable;
                if (artifactMovable != null)
                {
                    return artifactMovable;
                }
            }

            return null;
        }

        /// <summary>
        /// An artifact's own tooltip, without the lines that tell a MOUSE what to press.
        ///
        /// <c>ArtifactDetails</c> ends its tooltip with a row per gesture ("&lt;rmb&gt; Equip",
        /// "&lt;hl&gt;CTRL&lt;/hl&gt; + &lt;rmb&gt; Sell", the drop and the auto-arrange), and the
        /// keyboard gets those same gestures as usage hints on the slot itself, so the rows would be
        /// said twice. They are removed by the localized text the game DREW them from rather than by
        /// English, so a row this mod does not know about is left where it is and the player still
        /// hears that something may be available.
        /// </summary>
        private Tooltip BuildInventoryArtifactTooltip(IArtifactState artifact, InventoryArtifactMovable movable, Selectable selectable)
        {
            Tooltip tooltip = Tooltip.ForComponent(selectable as Component, _localization);
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

        private WielderInteractHeader GetWielderInteractHeader()
        {
            return GetField<WielderInteractHeader>(_menu, WielderInteractHeaderField);
        }

        /// <summary>What a game object is, as a number a key can hold: zero for one the game has not
        /// made or has destroyed, which Unity's own null answers for.</summary>
        private static int InstanceId(Component component)
        {
            return component == null ? 0 : component.GetInstanceID();
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

            return _facade.Artifacts.GetForOwner(CommanderId, slot).FirstOrDefault();
        }

        private string GetArtifactBuyCostLabel(IArtifactState artifact)
        {
            if (_facade == null || _facade.Artifacts == null || _facade.Commanders == null || artifact == null || CommanderId < 0)
            {
                return string.Empty;
            }

            ICommanderState commander = _facade.Commanders.Get(CommanderId);
            Cost cost = commander != null ? _facade.Artifacts.GetArtifactMarketBuyCost(artifact, commander) : null;
            return FormatCost(cost);
        }

        private string FormatCost(Cost cost)
        {
            if (cost == null || cost.CostEntries == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            List<Cost.CostEntry> entries = cost.SortedCostEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                Cost.CostEntry entry = entries[i];
                if (entry.Amount <= 0)
                {
                    continue;
                }

                parts.Add(ModText.Get(
                    _localization,
                    ModStrings.Common.ResourceAmount,
                    FormatAmount(entry.Amount),
                    GetResourceName(entry.Type)));
            }

            return ModText.JoinList(_localization, parts);
        }

        private string GetResourceName(ResourceType resourceType)
        {
            string fallback;
            switch (resourceType)
            {
                case ResourceType.AncientAmber:
                    fallback = "Ancient Amber";
                    break;
                case ResourceType.CelestialOre:
                    fallback = "Celestial Ore";
                    break;
                default:
                    fallback = resourceType.ToString();
                    break;
            }

            return GameText.Get(_localization, "Common/Resource/" + resourceType, fallback);
        }

        private static string FormatAmount(int amount)
        {
            return amount.ToString("N0", CultureInfo.InvariantCulture);
        }

        private IArtifactState GetSelectedBuyArtifact()
        {
            return GetField<IArtifactState>(_menu, SelectedBuyArtifactField);
        }

        private static bool IsDisplayOnlyEquipmentArtifact(InventorySlot slot, IArtifactState artifact)
        {
            return slot == InventorySlot.OffHand
                && artifact != null
                && artifact.EquippedInSlot == InventorySlot.MainHand;
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
                SocAccessMod.Instance?.LogWarning("ArtifactMarketMenuAdapter could not get artifact name: " + ex.Message);
                return _artifactLookup != null ? _artifactLookup.GetLocalizedName(artifact.Type) : artifact.Type.ToString();
            }
        }

        private string GetInventorySlotName(InventorySlot slot)
        {
            string text = _localization != null ? _localization.GetText("InventorySlots/" + slot) : string.Empty;
            return string.IsNullOrWhiteSpace(text) || text == "InventorySlots/" + slot
                ? FormatSlotName(slot)
                : SpokenLines.Clean(text);
        }

        private string GetInventorySlotName(string slotName)
        {
            string text = _localization != null ? _localization.GetText("InventorySlots/" + slotName) : string.Empty;
            return string.IsNullOrWhiteSpace(text) ? slotName : SpokenLines.Clean(text);
        }

        private string GetInventoryLabel()
        {
            return GetLocalizedText("Common/CommanderInventory/Inventory", "Inventory");
        }

        private string GetCommanderName(int commanderId)
        {
            string name = commanderId >= 0 && _facade != null ? _facade.Commanders.GetName(commanderId) : string.Empty;
            return SpokenLines.Clean(name);
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpokenLines.Clean(GameText.Get(_localization, key, fallback));
        }

        private void AddLocalizedLine(List<string> lines, string key)
        {
            string line = _localization != null ? _localization.GetText(key) : string.Empty;
            if (!string.IsNullOrWhiteSpace(line) && !lines.Contains(line))
            {
                lines.Add(line);
            }
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

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static int CompareSiblingIndex(ArtifactMarketEntry left, ArtifactMarketEntry right)
        {
            int leftIndex = left != null ? ((Component)left).transform.GetSiblingIndex() : 0;
            int rightIndex = right != null ? ((Component)right).transform.GetSiblingIndex() : 0;
            return leftIndex.CompareTo(rightIndex);
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

        public sealed class CategoryItem
        {
            public CategoryItem(string label, int index, Component toggle)
            {
                Label = label ?? string.Empty;
                Index = index;
                Toggle = toggle;
            }

            public string Label { get; private set; }
            public int Index { get; private set; }

            /// <summary>The toggle the menu draws for this category.</summary>
            public Component Toggle { get; private set; }
        }

        public sealed class MarketArtifactItem
        {
            public MarketArtifactItem(
                string label,
                string costLabel,
                ArtifactMarketEntry entry,
                Tooltip tooltip)
            {
                Label = label ?? string.Empty;
                CostLabel = costLabel ?? string.Empty;
                Entry = entry;
                Tooltip = tooltip;
            }

            /// <summary>The artifact's name, with the word for its rarity colour behind it.</summary>
            public string Label { get; private set; }

            /// <summary>What the market asks for it.</summary>
            public string CostLabel { get; private set; }

            public ArtifactMarketEntry Entry { get; private set; }
            public Tooltip Tooltip { get; private set; }
        }
    }
}
