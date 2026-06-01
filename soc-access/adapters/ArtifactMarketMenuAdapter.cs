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
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class ArtifactMarketMenuAdapter
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
        private static readonly FieldInfo InventoryArtifactMapField = AccessTools.Field(typeof(InventoryHUD), "_artifactStateToGOMap");
        private static readonly FieldInfo PurchaseButtonButtonField = AccessTools.Field(typeof(PurchaseButton), "_button");
        private static readonly FieldInfo HeaderWielderPortraitField = AccessTools.Field(typeof(WielderInteractHeader), "_wielderPortrait");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");

        private readonly ArtifactMarketMenu _menu;
        private readonly InventoryHUD _inventory;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly IArtifactLookup _artifactLookup;

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

        public string CommanderName
        {
            get { return GetCommanderName(CommanderId); }
        }

        public Component WielderPortraitTarget
        {
            get { return GetField<UIImage>(GetWielderInteractHeader(), HeaderWielderPortraitField) as Component; }
        }

        public TroopHudAdapter Troops
        {
            get { return new TroopHudAdapter(GetField<TroopHUD>(GetWielderInteractHeader(), HeaderTroopHudField), _facade, _localization); }
        }

        public bool IsArmyVisible()
        {
            TroopHUD troopHud = GetField<TroopHUD>(GetWielderInteractHeader(), HeaderTroopHudField);
            return troopHud != null && ((Component)troopHud).gameObject.activeInHierarchy;
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

        public IReadOnlyList<CategoryItem> GetCategories()
        {
            return new[]
            {
                new CategoryItem("artifact-market-category-all", ModText.Get(_localization, ModStrings.Scanner.All), 0),
                new CategoryItem("artifact-market-category-head", GetInventorySlotName(InventorySlot.Head), 1),
                new CategoryItem("artifact-market-category-main-hand", GetInventorySlotName(InventorySlot.MainHand), 2),
                new CategoryItem("artifact-market-category-off-hand", GetInventorySlotName(InventorySlot.OffHand), 3),
                new CategoryItem("artifact-market-category-hands", GetInventorySlotName(InventorySlot.Hands), 4),
                new CategoryItem("artifact-market-category-chest", GetInventorySlotName(InventorySlot.Chest), 5),
                new CategoryItem("artifact-market-category-feet", GetInventorySlotName(InventorySlot.Feet), 6),
                new CategoryItem("artifact-market-category-trinkets", GetInventorySlotName(InventorySlot.Trinket1), 7),
                new CategoryItem("artifact-market-category-buyback", ModText.Get(_localization, ModStrings.Screens.Buyback), 8)
            };
        }

        public bool SelectCategory(int categoryIndex)
        {
            UIToggleGroup group = GetField<UIToggleGroup>(_menu, CategoryTabGroupField);
            if (group == null || categoryIndex < 0 || categoryIndex > 8)
            {
                return false;
            }

            group.SetActiveToggle(categoryIndex);
            return true;
        }

        public IReadOnlyList<MarketArtifactItem> GetMarketArtifacts()
        {
            List<MarketArtifactItem> items = new List<MarketArtifactItem>();
            GameObject gridContainer = GetField<GameObject>(_menu, GridContainerField);
            if (gridContainer == null)
            {
                return items;
            }

            ArtifactMarketEntry[] entries = gridContainer.GetComponentsInChildren<ArtifactMarketEntry>(false);
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
                    "artifact-market-offer-" + artifact.Id,
                    GetArtifactName(artifact),
                    GetArtifactBuyCostLabel(artifact),
                    entry,
                    () => SelectMarketEntryForPurchase(entry),
                    () => SelectMarketEntryForPurchase(entry),
                    () => BuildMarketArtifactTooltip(entry)));
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

        public bool SelectMarketEntryForPurchase(ArtifactMarketEntry entry)
        {
            if (entry == null || entry.ArtifactState == null)
            {
                return false;
            }

            SelectMarketEntry(entry);
            Action<ArtifactMarketEntry> clicked = entry.OnClicked;
            if (clicked == null)
            {
                return false;
            }

            clicked(entry);
            return true;
        }

        public bool BuyMarketArtifact(ArtifactMarketEntry entry)
        {
            if (!SelectMarketEntryForPurchase(entry))
            {
                return false;
            }

            return BuySelectedMarketArtifact();
        }

        public bool BuySelectedMarketArtifact()
        {
            if (!HasSelectedBuyArtifact())
            {
                return false;
            }

            PurchaseButton buyButton = GetField<PurchaseButton>(_menu, BuyButtonField);
            UIButton nativeButton = GetField<UIButton>(buyButton, PurchaseButtonButtonField);
            return NativeSelectionUtility.Click(nativeButton);
        }

        public bool HasSelectedBuyArtifact()
        {
            return GetSelectedBuyArtifact() != null;
        }

        public bool CanBuySelectedArtifact()
        {
            PurchaseButton buyButton = GetField<PurchaseButton>(_menu, BuyButtonField);
            return HasSelectedBuyArtifact() && buyButton != null && buyButton.Interactable;
        }

        public string SelectedBuyButtonLabel
        {
            get
            {
                return GetBuyActionLabel(GetSelectedBuyArtifact());
            }
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

            string ownerName = GetCommanderName(CommanderId);
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
                    BuildInventoryArtifactTooltip(artifact, artifactMovable, tooltipSelectable),
                    () => SelectInventoryCell(capturedNativeSlot, capturedMovable, 0)));
            }

            return slotsInfo;
        }

        public IReadOnlyList<InventorySlotInfo> GetBackpackSlots()
        {
            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            InventoryHUDSlot nativeSlot = _inventory != null ? _inventory.GetSlot(InventorySlot.None) : null;
            string ownerName = GetCommanderName(CommanderId);
            int cellCount = nativeSlot != null ? nativeSlot.CellsCount : 0;
            for (int i = 0; i < cellCount; i++)
            {
                InventoryArtifactMovable movable = nativeSlot != null ? nativeSlot.TryGetArtifact(i) : null;
                IArtifactState artifact = movable != null ? movable.State : null;
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
                    BuildInventoryArtifactTooltip(artifact, movable, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i)),
                    () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
            }

            return slotsInfo;
        }

        public DropResult DropInventoryArtifact(InventorySlotInfo source, InventorySlotInfo target)
        {
            return ArtifactDropUtility.DropInventoryArtifact(_facade, source, target, "ArtifactMarketMenuAdapter artifact grid drop");
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private Tooltip BuildMarketArtifactTooltip(ArtifactMarketEntry entry)
        {
            Tooltip tooltip = Tooltip.ForComponent(entry != null ? entry.GetSelectable() as Component : null, _localization);
            if (tooltip == null || entry == null || entry.ArtifactState == null)
            {
                return tooltip;
            }

            return new Tooltip(
                () => tooltip.TextLines,
                tooltip.VisualMetadata,
                new[]
                {
                    new TooltipAction(
                        GetBuyActionLabel(entry.ArtifactState),
                        () => BuyMarketArtifact(entry))
                });
        }

        private Tooltip BuildInventoryArtifactTooltip(IArtifactState artifact, InventoryArtifactMovable movable, Selectable selectable)
        {
            Tooltip tooltip = Tooltip.ForComponent(selectable as Component, _localization);
            if (tooltip == null || artifact == null || movable == null || _localization == null)
            {
                return tooltip;
            }

            List<TooltipAction> actions = new List<TooltipAction>();
            List<string> instructionLines = new List<string>();

            string equipInstructionKey = artifact.IsEquipped
                ? "Adventure/TooltipInstruction/Unequip"
                : "Adventure/TooltipInstruction/Equip";
            AddLocalizedLine(instructionLines, equipInstructionKey);
            actions.Add(new TooltipAction(
                GetLocalizedText(equipInstructionKey, artifact.IsEquipped ? "Unequip" : "Equip"),
                () => InvokeArtifactAction(movable, _inventory.EquipArtifact)));

            if (artifact.IsImportant)
            {
                return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata, actions);
            }

            AddLocalizedLine(instructionLines, "Adventure/TooltipInstruction/Sell");
            actions.Add(new TooltipAction(
                ModText.Get(_localization, ModStrings.Screens.Sell),
                () => InvokeArtifactAction(movable, _inventory.SellArtifact)));

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

        private string GetBuyActionLabel(IArtifactState artifact)
        {
            return MenuButtonTextUtility.JoinParts(
                ModText.Get(_localization, ModStrings.Screens.BuyArtifact),
                GetArtifactBuyCostLabel(artifact));
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
                SocAccessPlugin.Instance?.LogWarning("ArtifactMarketMenuAdapter could not get artifact name: " + ex.Message);
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

        private string GetLocalizedText(string key, string fallback)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback));
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
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
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

        internal sealed class CategoryItem
        {
            public CategoryItem(string id, string label, int index)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Index = index;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public int Index { get; private set; }
        }

        internal sealed class MarketArtifactItem
        {
            public MarketArtifactItem(
                string id,
                string label,
                string costLabel,
                ArtifactMarketEntry entry,
                Action onFocus,
                Func<bool> activate,
                Func<Tooltip> getTooltip)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                CostLabel = costLabel ?? string.Empty;
                Entry = entry;
                OnFocus = onFocus;
                Activate = activate;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public string CostLabel { get; private set; }
            public ArtifactMarketEntry Entry { get; private set; }
            public Action OnFocus { get; private set; }
            public Func<bool> Activate { get; private set; }
            public Func<Tooltip> GetTooltip { get; private set; }
        }
    }
}
