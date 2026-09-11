using System;
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
        private static readonly FieldInfo PurchaseButtonButtonField = AccessTools.Field(typeof(PurchaseButton), "_button");
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");
        private static readonly FieldInfo MarketEntryButtonField = AccessTools.Field(typeof(ArtifactMarketEntry), "_button");

        private static readonly ArtifactMarketEntry[] NoEntries = new ArtifactMarketEntry[0];

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

        // Each filter's name, read off its toggle once (see GetCategoryLabel).
        private readonly Dictionary<UIToggle, string> _categoryLabels = new Dictionary<UIToggle, string>();

        // The offers the grid is drawing, kept while it is drawing the same ones. Each costs a
        // rarity-formatted name and a formatted price, and the 24 pooled cells are copied and
        // sorted to read them in the order the grid draws them; the key is read off the game every
        // frame (the wielder, the cells and what is in each of them), so a purchase, a sale and a
        // switch of category all rebuild with nothing having to say so. The screen keeps its nodes
        // for as long as this is the same list (screens/ArtifactMarketScreen.cs).
        private readonly SlotSnapshot<MarketArtifactItem> _offers = new SlotSnapshot<MarketArtifactItem>();
        private readonly FrameSweep<ArtifactMarketEntry> _marketEntries =
            new FrameSweep<ArtifactMarketEntry>("artifact market grid", inactiveToo: false);
        private readonly FrameSweep<UITextMesh> _bandTexts =
            new FrameSweep<UITextMesh>("artifact market band");

        // The wielder's artifacts, read the way every menu that draws an InventoryHUD reads them.
        private readonly InventorySlotReader _slots;

        private WielderInteract _wielder;

        public ArtifactMarketMenuAdapter(ArtifactMarketMenu menu)
        {
            _menu = menu;
            _inventory = Reflect.Get<InventoryHUD>(menu, InventoryField);
            _facade = Reflect.Get<IClientAdventureFacade>(menu, FacadeField);
            _localization = Reflect.Get<ILocalizationHandler>(menu, LocalizationField);
            _artifactLookup = Reflect.Get<IArtifactLookup>(menu, ArtifactLookupField);
            _slots = new InventorySlotReader(
                () => _inventory,
                () => CommanderId,
                _facade,
                _localization,
                _artifactLookup,
                "ArtifactMarketMenuAdapter",
                InventorySlotReader.MouseInstructionKeys,
                answersLeftClick: true);
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
                && Reflect.Get<object>(_menu, AsyncField) != null
                && _inventory != null
                && _inventory.IsArtifactShopInventory
                && ((Component)_menu).gameObject.activeInHierarchy;
        }

        public string Title
        {
            get
            {
                string title = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, HeaderTextField));
                return string.IsNullOrWhiteSpace(title)
                    ? ModText.Get(_localization, ModStrings.Scanner.ArtifactMarkets)
                    : title;
            }
        }

        public string Description
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, DescriptionTextField)); }
        }

        public string EquipmentLabel
        {
            get { return _slots.EquipmentLabel; }
        }

        public string InventoryLabel
        {
            get { return _slots.InventoryLabel; }
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
            return Reflect.Get<UIButton>(_menu, BackgroundCloseButtonField);
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
                string label = GetCategoryLabel(toggle);
                items.Add(new CategoryItem(
                    string.IsNullOrWhiteSpace(label) ? ModText.Get(_localization, ModStrings.Scanner.All) : label,
                    i,
                    toggle));
            }

            return items;
        }

        /// <summary>The game's own word for one filter. Reading a native tooltip draws the whole
        /// details block, and the toggles are the menu's own children with the same word on them for
        /// as long as it is open, so each is read ONCE rather than once a frame. The empty answer is
        /// remembered too - the first toggle has no tooltip at all - so a toggle with no words costs
        /// one read and not one per frame.</summary>
        private string GetCategoryLabel(UIToggle toggle)
        {
            if (toggle == null)
            {
                return string.Empty;
            }

            string label;
            if (!_categoryLabels.TryGetValue(toggle, out label))
            {
                label = FirstLine(Tooltip.ForComponent(toggle, _localization));
                _categoryLabels[toggle] = label;
            }

            return label;
        }

        /// <summary>Switch to a category through the game's own toggle group, which is what the menu
        /// hangs <c>HandleSwitchedCategory</c> on.</summary>
        public bool SelectCategory(int categoryIndex)
        {
            UIToggleGroup group = Reflect.Get<UIToggleGroup>(_menu, CategoryTabGroupField);
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
            UIToggleGroup group = Reflect.Get<UIToggleGroup>(_menu, CategoryTabGroupField);
            return group == null ? new UIToggle[0] : _categoryToggles.Under((Component)group);
        }

        public IReadOnlyList<MarketArtifactItem> GetMarketArtifacts()
        {
            GameObject gridContainer = Reflect.Get<GameObject>(_menu, GridContainerField);
            ArtifactMarketEntry[] drawn = gridContainer == null
                ? NoEntries
                : _marketEntries.Under(gridContainer.transform);
            List<int> key = _offers.BeginKey();
            key.Add(CommanderId);
            for (int i = 0; i < drawn.Length; i++)
            {
                ArtifactMarketEntry entry = drawn[i];
                IArtifactState artifact = entry != null ? entry.ArtifactState : null;
                key.Add(entry != null ? ((Component)entry).GetInstanceID() : 0);
                key.Add(entry != null ? ((Component)entry).transform.GetSiblingIndex() : -1);
                key.Add(artifact != null ? artifact.Id : 0);
            }

            IReadOnlyList<MarketArtifactItem> unchanged = _offers.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            // A copy, because the sweep's answer is shared for the rest of the frame and the order
            // it was walked in is what the next caller expects.
            ArtifactMarketEntry[] entries = (ArtifactMarketEntry[])drawn.Clone();
            Array.Sort(entries, CompareSiblingIndex);
            List<MarketArtifactItem> items = new List<MarketArtifactItem>();
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

            return _offers.Keep(items);
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

            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(entry, MarketEntryButtonField));
        }

        /// <summary>The wielder's equipment and backpack, and every gesture the game gives an
        /// artifact in them, read by the shared inventory reader. The left click is this menu's own:
        /// the shop answers it by selecting the artifact for sale
        /// (<c>ArtifactMarketMenu.HandleInventoryArtifactClicked</c>), and with Ctrl physically held
        /// the right click SELLS rather than destroys
        /// (<c>InventoryArtifactMovable.HandleRightClick</c> branches on
        /// <c>InventoryHUD.IsArtifactShopInventory</c>).</summary>
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

        public bool CanRearrangeArtifactTo(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return _slots.CanRearrangeArtifactTo(movable, target);
        }

        public string RearrangeRefusalText
        {
            get { return _slots.RearrangeRefusalText; }
        }

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

        public string AutoArrangeText
        {
            get { return _slots.AutoArrangeText; }
        }

        public bool AutoArrangeArtifacts()
        {
            return _slots.AutoArrangeArtifacts();
        }

        // ---- the selection band at the foot of the menu ----

        /// <summary>Whether the band is showing its prompt, which is what it shows while nothing is
        /// selected.</summary>
        public bool IsNoSelectionShown
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, NoSelectionContainerField)); }
        }

        /// <summary>The prompt the band draws while nothing is selected.</summary>
        public string NoSelectionText
        {
            get { return FirstText(Reflect.Get<GameObject>(_menu, NoSelectionContainerField)); }
        }

        /// <summary>The container the prompt is drawn in, one of the band's three.</summary>
        public Component NoSelectionContainer
        {
            get { return ContainerTransform(Reflect.Get<GameObject>(_menu, NoSelectionContainerField)); }
        }

        /// <summary>The container the purchase is drawn in, one of the band's three.</summary>
        public Component BuyContainer
        {
            get { return ContainerTransform(Reflect.Get<GameObject>(_menu, BuyContainerField)); }
        }

        /// <summary>The container the sale is drawn in, one of the band's three.</summary>
        public Component SellContainer
        {
            get { return ContainerTransform(Reflect.Get<GameObject>(_menu, SellContainerField)); }
        }

        /// <summary>Whether the band is showing the artifact the player is buying.</summary>
        public bool IsBuyShown
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, BuyContainerField)); }
        }

        /// <summary>The tooltip on the Buy band's icon: the game hangs the artifact's details on it
        /// (<c>ArtifactMarketMenu.SetArtifact</c>, <c>UIImage.SetDetails</c>), which is what the mouse
        /// sees hovering the band.</summary>
        public Tooltip BuyItemTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIImage>(_menu, BuyItemIconField) as Component, _localization); }
        }

        /// <summary>The name of the artifact the Buy band is about, as the band draws it.</summary>
        public string BuyItemName
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, BuyItemTitleField)); }
        }

        /// <summary>The word on the Buy button, as the band draws it above it.</summary>
        public string BuyButtonLabel
        {
            get
            {
                string title = GetBandButtonTitle(
                    Reflect.Get<GameObject>(_menu, BuyContainerField),
                    Reflect.Get<UITextMesh>(_menu, BuyItemTitleField),
                    Reflect.Get<PurchaseButton>(_menu, BuyButtonField));
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
            get { return Reflect.Get<PurchaseButton>(_menu, BuyButtonField) as Component; }
        }

        /// <summary>Whether the game will take the purchase: it turns the button off when the team
        /// cannot afford the price, and says nothing about why.</summary>
        public bool CanBuySelectedArtifact()
        {
            PurchaseButton buyButton = Reflect.Get<PurchaseButton>(_menu, BuyButtonField);
            return GetSelectedBuyArtifact() != null && buyButton != null && buyButton.Interactable;
        }

        /// <summary>Buy, through the game's own click on its Buy button.</summary>
        public bool BuySelectedMarketArtifact()
        {
            PurchaseButton buyButton = Reflect.Get<PurchaseButton>(_menu, BuyButtonField);
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(buyButton, PurchaseButtonButtonField));
        }

        /// <summary>Whether the band is showing the artifact the player has picked out to sell.
        /// </summary>
        public bool IsSellShown
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, SellContainerField)); }
        }

        /// <summary>The tooltip on the Sell band's icon: the game hangs the artifact's details on it
        /// (<c>ArtifactMarketMenu.SetArtifact</c>, <c>UIImage.SetDetails</c>), which is what the mouse
        /// sees hovering the band.</summary>
        public Tooltip SellItemTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<UIImage>(_menu, SellItemIconField) as Component, _localization); }
        }

        /// <summary>The name of the artifact the Sell band is about, as the band draws it.</summary>
        public string SellItemName
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, SellItemTitleField)); }
        }

        /// <summary>Whether the Sell button is drawn at all: the game hides it for an artifact it
        /// treats as important (<c>ArtifactMarketMenu.SetArtifact</c>).</summary>
        public bool IsSellButtonShown
        {
            get { return GameObjects.IsLive(ButtonObject(Reflect.Get<PurchaseButton>(_menu, SellButtonField))); }
        }

        /// <summary>The word on the Sell button, as the band draws it above it.</summary>
        public string SellButtonLabel
        {
            get
            {
                string title = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, SellButtonTitleField));
                return string.IsNullOrWhiteSpace(title) ? ModText.Get(_localization, ModStrings.Screens.Sell) : title;
            }
        }

        /// <summary>What the Sell button pays - the same value it draws in its own price box.</summary>
        public string SellPriceLabel
        {
            get
            {
                IArtifactState artifact = Reflect.Get<IArtifactState>(_menu, SelectedSellArtifactField);
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
            get { return Reflect.Get<PurchaseButton>(_menu, SellButtonField) as Component; }
        }

        public bool CanSellSelectedArtifact()
        {
            PurchaseButton sellButton = Reflect.Get<PurchaseButton>(_menu, SellButtonField);
            return sellButton != null && sellButton.Interactable;
        }

        /// <summary>Sell, through the game's own click on its Sell button.</summary>
        public bool SellSelectedArtifact()
        {
            PurchaseButton sellButton = Reflect.Get<PurchaseButton>(_menu, SellButtonField);
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(sellButton, PurchaseButtonButtonField));
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

                return UITextMeshTextUtility.Spoken(text);
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

        // LAZY: reached only through GraphNodes.Text's Func for the "no selection" line, so the walk
        // is paid when that line is read rather than on every build.
        private static string FirstText(GameObject container)
        {
            UITextMesh text = container == null ? null : container.GetComponentInChildren<UITextMesh>(true);
            return UITextMeshTextUtility.Spoken(text);
        }

        private static string FirstLine(Tooltip tooltip)
        {
            IReadOnlyList<string> lines = tooltip == null ? null : tooltip.TextLines;
            return lines != null && lines.Count > 0 ? lines[0] : string.Empty;
        }

        private WielderInteractHeader GetWielderInteractHeader()
        {
            return Reflect.Get<WielderInteractHeader>(_menu, WielderInteractHeaderField);
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
            return Reflect.Get<IArtifactState>(_menu, SelectedBuyArtifactField);
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

        private static int CompareSiblingIndex(ArtifactMarketEntry left, ArtifactMarketEntry right)
        {
            int leftIndex = left != null ? ((Component)left).transform.GetSiblingIndex() : 0;
            int rightIndex = right != null ? ((Component)right).transform.GetSiblingIndex() : 0;
            return leftIndex.CompareTo(rightIndex);
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
