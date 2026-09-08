using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class PurchaseWielderMenuAdapter
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(PurchaseWielderMenu), "_async");
        private static readonly FieldInfo WielderListTitleField = AccessTools.Field(typeof(PurchaseWielderMenu), "_wielderListTitle");
        private static readonly FieldInfo ActiveEntriesField = AccessTools.Field(typeof(PurchaseWielderMenu), "_activeEntries");
        private static readonly FieldInfo SelectedEntryIndexField = AccessTools.Field(typeof(PurchaseWielderMenu), "_selectedEntryIndex");
        private static readonly FieldInfo WielderDetailsField = AccessTools.Field(typeof(PurchaseWielderMenu), "_wielderDetails");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(PurchaseWielderMenu), "_localizationHandler");
        private static readonly FieldInfo BackgroundCloseButtonField = AccessTools.Field(typeof(AdventureMenuBackground), "_closeButton");

        private static readonly FieldInfo EntryNameField = AccessTools.Field(typeof(PurchaseWielderEntry), "_name");
        private static readonly FieldInfo EntryClassField = AccessTools.Field(typeof(PurchaseWielderEntry), "_class");
        private static readonly FieldInfo EntryOwnedFrameField = AccessTools.Field(typeof(PurchaseWielderEntry), "_ownedFrame");
        private static readonly FieldInfo EntryDeadOverlayField = AccessTools.Field(typeof(PurchaseWielderEntry), "_deadOverlay");
        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(PurchaseWielderEntry), "_button");

        private static readonly FieldInfo DetailsNameField = AccessTools.Field(typeof(PurchaseWielderDetails), "_name");
        private static readonly FieldInfo DetailsDescriptionField = AccessTools.Field(typeof(PurchaseWielderDetails), "_description");
        private static readonly FieldInfo DetailsLevelContainerField = AccessTools.Field(typeof(PurchaseWielderDetails), "_levelContainer");
        private static readonly FieldInfo DetailsLevelTextField = AccessTools.Field(typeof(PurchaseWielderDetails), "_levelText");
        private static readonly FieldInfo DetailsOffenceField = AccessTools.Field(typeof(PurchaseWielderDetails), "_offence");
        private static readonly FieldInfo DetailsDefenceField = AccessTools.Field(typeof(PurchaseWielderDetails), "_defence");
        private static readonly FieldInfo DetailsMovementField = AccessTools.Field(typeof(PurchaseWielderDetails), "_movement");
        private static readonly FieldInfo DetailsViewRadiusField = AccessTools.Field(typeof(PurchaseWielderDetails), "_viewRadius");
        private static readonly FieldInfo DetailsTroopsSectionField = AccessTools.Field(typeof(PurchaseWielderDetails), "_troopsSection");
        private static readonly FieldInfo DetailsTroopsField = AccessTools.Field(typeof(PurchaseWielderDetails), "_troops");
        private static readonly FieldInfo DetailsSkillEntriesField = AccessTools.Field(typeof(PurchaseWielderDetails), "_skillEntries");
        private static readonly FieldInfo DetailsSpecializationField = AccessTools.Field(typeof(PurchaseWielderDetails), "_specialization");
        private static readonly FieldInfo DetailsLargeCostSectionField = AccessTools.Field(typeof(PurchaseWielderDetails), "_largeCostSection");
        private static readonly FieldInfo DetailsPurchaseButtonField = AccessTools.Field(typeof(PurchaseWielderDetails), "_purchaseButton");
        private static readonly FieldInfo DetailsAlreadyOwnedTextField = AccessTools.Field(typeof(PurchaseWielderDetails), "_alreadyOwnedText");

        private static readonly FieldInfo SkillLevelTextField = AccessTools.Field(typeof(PurchaseWielderSkillEntry), "_levelText");
        private static readonly FieldInfo SkillFrameField = AccessTools.Field(typeof(PurchaseWielderSkillEntry), "_frame");
        private static readonly FieldInfo TroopHudEntrySizeField = AccessTools.Field(typeof(TroopHUDEntry), "_size");

        private static readonly FieldInfo GoldCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_goldCostEntry");
        private static readonly FieldInfo StoneCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_stoneCostEntry");
        private static readonly FieldInfo WoodCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_woodCostEntry");
        private static readonly FieldInfo GlimmerWeaveCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_glimmerWeaveCostEntry");
        private static readonly FieldInfo AncientAmberCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_ancientAmberCostEntry");
        private static readonly FieldInfo CelestialOreCostEntryField = AccessTools.Field(typeof(LargeCostSection), "_celestialOreCostEntry");
        private static readonly FieldInfo GoldAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_goldAmountText");
        private static readonly FieldInfo StoneAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_stoneAmountText");
        private static readonly FieldInfo WoodAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_woodAmountText");
        private static readonly FieldInfo GlimmerWeaveAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_glimmerWeaveAmountText");
        private static readonly FieldInfo AncientAmberAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_ancientAmberAmountText");
        private static readonly FieldInfo CelestialOreAmountTextField = AccessTools.Field(typeof(LargeCostSection), "_celestialOreAmountText");

        private readonly PurchaseWielderMenu _menu;
        private readonly ILocalizationHandler _localization;

        public PurchaseWielderMenuAdapter(PurchaseWielderMenu menu)
        {
            _menu = menu;
            _localization = GetField<ILocalizationHandler>(menu, LocalizationField);
        }

        public PurchaseWielderMenu Source
        {
            get { return _menu; }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && GetField<Async>(_menu, AsyncField) != null
                && GetField<IList>(_menu, ActiveEntriesField) != null;
        }

        public string Title
        {
            get { return GetText(GetField<UITextMesh>(_menu, WielderListTitleField)); }
        }

        public int SelectedEntryIndex
        {
            get
            {
                object value = SelectedEntryIndexField != null ? SelectedEntryIndexField.GetValue(_menu) : null;
                return value is int ? (int)value : -1;
            }
        }

        /// <summary>The name the pane draws for the candidate it is describing.</summary>
        public string SelectedName
        {
            get { return GetText(GetField<UITextMesh>(GetDetails(), DetailsNameField)); }
        }

        /// <summary>The level the pane draws beside that name, or empty while the pane hides it.
        /// </summary>
        public string SelectedLevel
        {
            get
            {
                PurchaseWielderDetails details = GetDetails();
                return IsVisible(GetField<GameObject>(details, DetailsLevelContainerField))
                    ? GetText(GetField<UITextMesh>(details, DetailsLevelTextField))
                    : string.Empty;
            }
        }

        /// <summary>The description the pane draws under the name, one line per paragraph of it.
        /// </summary>
        public IList<string> SelectedDescriptionLines
        {
            get { return GetLines(GetField<UITextMesh>(GetDetails(), DetailsDescriptionField)); }
        }

        public string OffenceHeader
        {
            get { return GetLocalizedText("Commanders/Tooltip/Offense", "Offense"); }
        }

        public string DefenceHeader
        {
            get { return GetLocalizedText("Commanders/Tooltip/Defense", "Defense"); }
        }

        public string MovementHeader
        {
            get { return GetLocalizedText("Commanders/Tooltip/Movement", "Movement"); }
        }

        public string ViewRadiusHeader
        {
            get { return GetLocalizedText("Commanders/Tooltip/ViewRadius", "View radius"); }
        }

        public string Offence
        {
            get { return GetDetailsText(DetailsOffenceField); }
        }

        public string Defence
        {
            get { return GetDetailsText(DetailsDefenceField); }
        }

        public string Movement
        {
            get { return GetDetailsText(DetailsMovementField); }
        }

        public string ViewRadius
        {
            get { return GetDetailsText(DetailsViewRadiusField); }
        }

        public bool HasTroops()
        {
            return IsVisible(GetField<GameObject>(GetDetails(), DetailsTroopsSectionField));
        }

        public int TroopSlotCount
        {
            get { return GetTroopEntries().Count; }
        }

        public bool IsTroopVisible(int index)
        {
            IReadOnlyList<TroopHUDEntry> entries = GetTroopEntries();
            return HasTroops()
                && index >= 0
                && index < entries.Count
                && IsVisible(entries[index] as Component);
        }

        public string GetTroopName(int index)
        {
            return FirstTooltipLine(GetTroopTooltip(index));
        }

        public int GetTroopAmount(int index)
        {
            IReadOnlyList<TroopHUDEntry> entries = GetTroopEntries();
            TroopHUDEntry entry = index >= 0 && index < entries.Count ? entries[index] : null;
            return GetTroopAmount(entry);
        }

        public Tooltip GetTroopTooltip(int index)
        {
            IReadOnlyList<TroopHUDEntry> entries = GetTroopEntries();
            if (index < 0 || index >= entries.Count)
            {
                return null;
            }

            TroopHUDEntry entry = entries[index];
            return Tooltip.ForComponent(entry != null ? entry.GetSelectable() : null, _localization);
        }

        /// <summary>The slot the game draws this troop in - what the row is drawn by, and what its
        /// tooltip hangs on.</summary>
        public Component GetTroopComponent(int index)
        {
            IReadOnlyList<TroopHUDEntry> entries = GetTroopEntries();
            TroopHUDEntry entry = index >= 0 && index < entries.Count ? entries[index] : null;
            return entry != null ? entry.GetSelectable() as Component : null;
        }

        public void FocusTroop(int index)
        {
            NativeSelectionUtility.Select(GetTroopComponent(index));
        }

        /// <summary>The caption the pane draws over the troops ("Starting Troops"), as the game
        /// writes it; empty where the section is not drawn.</summary>
        public string TroopsHeader
        {
            get { return SectionHeader(GetField<GameObject>(GetDetails(), DetailsTroopsSectionField)); }
        }

        /// <summary>The caption the pane draws over the skills ("Skills"). The pane keeps no field for
        /// the section, so it is reached from the first skill entry: entry, container, section.</summary>
        public string SkillsHeader
        {
            get
            {
                IReadOnlyList<PurchaseWielderSkillEntry> entries = GetSkillEntries();
                Component first = entries.Count > 0 ? entries[0] as Component : null;
                Transform container = first == null ? null : first.transform.parent;
                Transform section = container == null ? null : container.parent;
                return SectionHeader(section == null ? null : section.gameObject);
            }
        }

        public int SkillSlotCount
        {
            get { return GetSkillEntries().Count; }
        }

        public bool IsSkillVisible(int index)
        {
            IReadOnlyList<PurchaseWielderSkillEntry> entries = GetSkillEntries();
            return index >= 0 && index < entries.Count && IsVisible(entries[index] as Component);
        }

        public string GetSkillName(int index)
        {
            IReadOnlyList<PurchaseWielderSkillEntry> entries = GetSkillEntries();
            if (index < 0 || index >= entries.Count)
            {
                return string.Empty;
            }

            return FirstTooltipLine(GetSkillTooltip(index));
        }

        public Tooltip GetSkillTooltip(int index)
        {
            IReadOnlyList<PurchaseWielderSkillEntry> entries = GetSkillEntries();
            if (index < 0 || index >= entries.Count)
            {
                return null;
            }

            return Tooltip.ForComponent(GetField<UIImage>(entries[index], SkillFrameField) as Component, _localization);
        }

        /// <summary>The frame the game draws this skill in - what the row is drawn by, and what its
        /// tooltip hangs on.</summary>
        public Component GetSkillComponent(int index)
        {
            IReadOnlyList<PurchaseWielderSkillEntry> entries = GetSkillEntries();
            return index >= 0 && index < entries.Count
                ? GetField<UIImage>(entries[index], SkillFrameField) as Component
                : null;
        }

        public void FocusSkill(int index)
        {
            NativeSelectionUtility.Select(GetSkillComponent(index));
        }

        public bool HasSpecialization()
        {
            UITextMesh text = GetField<UITextMesh>(GetDetails(), DetailsSpecializationField);
            return IsVisible(text as Component) && !string.IsNullOrWhiteSpace(GetText(text));
        }

        /// <summary>The specialization the pane draws, under the game's own caption, one line per
        /// paragraph of it.</summary>
        public IList<string> SpecializationLines
        {
            get
            {
                IList<string> body = GetLines(GetField<UITextMesh>(GetDetails(), DetailsSpecializationField));
                if (body.Count == 0)
                {
                    return body;
                }

                string header = GetLocalizedText("Commanders/Tooltip/Specializations", string.Empty);
                if (!string.IsNullOrWhiteSpace(header))
                {
                    body[0] = header.TrimEnd(':') + ": " + body[0];
                }

                return body;
            }
        }

        public bool HasPurchaseStatus()
        {
            return !string.IsNullOrWhiteSpace(PurchaseStatus);
        }

        public string PurchaseStatus
        {
            get
            {
                PurchaseWielderDetails details = GetDetails();
                UITextMesh alreadyOwned = GetField<UITextMesh>(details, DetailsAlreadyOwnedTextField);
                if (IsVisible(alreadyOwned as Component))
                {
                    return GetText(alreadyOwned);
                }

                string cost = CostText;
                if (!string.IsNullOrWhiteSpace(cost))
                {
                    return cost;
                }

                Tooltip tooltip = PurchaseTooltip;
                return tooltip != null && tooltip.TextLines.Count > 0
                    ? SpokenLines.Clean(string.Join(". ", tooltip.TextLines))
                    : string.Empty;
            }
        }

        public string PurchaseLabel
        {
            get
            {
                string label = GetButtonLabel(GetPurchaseButton());
                return string.IsNullOrWhiteSpace(label) ? "Purchase" : label;
            }
        }

        public bool IsPurchaseVisible()
        {
            return IsVisible(GetPurchaseButton() as Component);
        }

        public bool IsPurchaseEnabled()
        {
            UIButton button = GetPurchaseButton();
            return button != null && button.Active && button.Interactable && IsVisible(button as Component);
        }

        public Tooltip PurchaseTooltip
        {
            get { return Tooltip.ForComponent(GetPurchaseButton() as Component, _localization); }
        }

        public bool ActivatePurchase()
        {
            return NativeSelectionUtility.Click(GetPurchaseButton());
        }

        public void FocusPurchase()
        {
            NativeSelectionUtility.Select(GetPurchaseButton() as Component);
        }

        /// <summary>The Purchase button the details pane draws.</summary>
        public Component PurchaseButton
        {
            get { return GetPurchaseButton() as Component; }
        }

        /// <summary>The close cross the menu's <c>AdventureMenuBackground</c> draws at the top right.
        /// It is only turned on where the background may be closed and the player is on mouse and
        /// keyboard (<c>AnimateEntry</c>).</summary>
        public Component CloseButton
        {
            get { return GetCloseButton() as Component; }
        }

        public bool IsCloseVisible()
        {
            UIButton button = GetCloseButton();
            return button != null && button.Active && IsVisible(button as Component);
        }

        public bool ActivateClose()
        {
            return NativeSelectionUtility.Click(GetCloseButton());
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

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public IReadOnlyList<EntryItem> GetEntries()
        {
            List<EntryItem> result = new List<EntryItem>();
            IList entries = GetField<IList>(_menu, ActiveEntriesField);
            if (entries == null)
            {
                return result;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                PurchaseWielderEntry entry = entries[i] as PurchaseWielderEntry;
                if (entry != null && IsVisible(entry as Component))
                {
                    result.Add(new EntryItem(this, entry, i));
                }
            }

            return result;
        }

        private string CostText
        {
            get
            {
                LargeCostSection section = GetField<LargeCostSection>(GetDetails(), DetailsLargeCostSectionField);
                if (!IsVisible(section as Component))
                {
                    return string.Empty;
                }

                List<string> parts = new List<string>();
                AddCostPart(parts, section, GoldCostEntryField, GoldAmountTextField, ResourceType.Gold);
                AddCostPart(parts, section, StoneCostEntryField, StoneAmountTextField, ResourceType.Stone);
                AddCostPart(parts, section, WoodCostEntryField, WoodAmountTextField, ResourceType.Wood);
                AddCostPart(parts, section, GlimmerWeaveCostEntryField, GlimmerWeaveAmountTextField, ResourceType.Glimmerweave);
                AddCostPart(parts, section, AncientAmberCostEntryField, AncientAmberAmountTextField, ResourceType.AncientAmber);
                AddCostPart(parts, section, CelestialOreCostEntryField, CelestialOreAmountTextField, ResourceType.CelestialOre);
                return parts.Count == 0
                    ? string.Empty
                    : ModText.Get(
                        _localization,
                        ModStrings.UI.LabelValue,
                        GetLocalizedText("Adventure/BuildMenu/Cost", "Cost").TrimEnd(':'),
                        ModText.JoinList(_localization, parts));
            }
        }

        private PurchaseWielderDetails GetDetails()
        {
            return GetField<PurchaseWielderDetails>(_menu, WielderDetailsField);
        }

        private UIButton GetCloseButton()
        {
            return GetField<UIButton>(_menu, BackgroundCloseButtonField);
        }

        private UIButton GetPurchaseButton()
        {
            return GetField<UIButton>(GetDetails(), DetailsPurchaseButtonField);
        }

        private string GetDetailsText(FieldInfo field)
        {
            return GetText(GetField<UITextMesh>(GetDetails(), field));
        }

        private IReadOnlyList<TroopHUDEntry> GetTroopEntries()
        {
            return GetField<List<TroopHUDEntry>>(GetDetails(), DetailsTroopsField) ?? new List<TroopHUDEntry>();
        }

        private IReadOnlyList<PurchaseWielderSkillEntry> GetSkillEntries()
        {
            return GetField<List<PurchaseWielderSkillEntry>>(GetDetails(), DetailsSkillEntriesField) ?? new List<PurchaseWielderSkillEntry>();
        }

        /// <summary>The text a section's header draws, at the path the prefab keeps it at (measured
        /// 2026-09-08: Header/HeaderText under both TroopsSection and SkillsSection).</summary>
        private static string SectionHeader(GameObject section)
        {
            Transform header = section == null ? null : section.transform.Find("Header/HeaderText");
            UITextMesh text = header == null ? null : header.GetComponent<UITextMesh>();
            return text == null ? string.Empty : GetText(text);
        }

        private void AddCostPart(List<string> parts, LargeCostSection section, FieldInfo entryField, FieldInfo textField, ResourceType resourceType)
        {
            UITransform entry = GetField<UITransform>(section, entryField);
            if (entry == null || !entry.Active)
            {
                return;
            }

            string amount = GetText(GetField<UITextMesh>(section, textField));
            if (string.IsNullOrWhiteSpace(amount))
            {
                return;
            }

            int parsed;
            parts.Add(ModText.Get(
                _localization,
                ModStrings.Common.ResourceAmount,
                amount,
                GetResourceName(resourceType, int.TryParse(amount, out parsed) ? parsed : 0)));
        }

        /// <summary>The resource's own name, in the game's plural form for the amount asked for.</summary>
        private string GetResourceName(ResourceType type, int amount)
        {
            string key = "Common/Resource/" + type;
            if (_localization != null)
            {
                string localized = _localization.GetPluralText(key, amount);
                localized = localized != null ? localized.Trim() : string.Empty;
                if (!string.IsNullOrWhiteSpace(localized) && localized != key)
                {
                    return localized;
                }
            }

            return type.ToString();
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpokenLines.Clean(GameText.Get(_localization, key, fallback ?? string.Empty));
        }

        /// <summary>The first line a tooltip has anything to say on. <c>Tooltip.TextLines</c> captures
        /// the game's details afresh on every read, so the capture is taken once rather than once per
        /// loop test and once per indexer.</summary>
        private static string FirstTooltipLine(Tooltip tooltip)
        {
            IReadOnlyList<string> lines = tooltip != null ? tooltip.TextLines : null;
            if (lines == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = SpokenLines.Clean(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        private static int GetTroopAmount(TroopHUDEntry entry)
        {
            if (entry == null || TroopHudEntrySizeField == null)
            {
                return 0;
            }

            object value = TroopHudEntrySizeField.GetValue(entry);
            return value is int ? (int)value : 0;
        }

        // A text mesh the game may have written more than one paragraph into.
        private static IList<string> GetLines(IUITextMesh textMesh)
        {
            return SpokenLines.Of(new[] { UITextMeshTextUtility.GetEffectiveText(textMesh) });
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class EntryItem
        {
            private readonly PurchaseWielderMenuAdapter _adapter;
            private readonly PurchaseWielderEntry _entry;
            private readonly int _index;

            public EntryItem(PurchaseWielderMenuAdapter adapter, PurchaseWielderEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                _index = index;
            }

            public string Id
            {
                get
                {
                    string uniqueName = _entry != null && _entry.CommanderDefinition != null ? _entry.CommanderDefinition.UniqueName : string.Empty;
                    return string.IsNullOrWhiteSpace(uniqueName)
                        ? "purchase-wielder-entry-" + _index
                        : "purchase-wielder-" + uniqueName.Replace(" ", "-").Replace("/", "-").ToLowerInvariant();
                }
            }

            /// <summary>The wielder's own name.</summary>
            public string Name
            {
                get { return GetText(GetField<UITextMesh>(_entry, EntryNameField)); }
            }

            /// <summary>The class line the entry draws under the name ("Level 12 Human Commander").</summary>
            public string ClassText
            {
                get { return GetText(GetField<UITextMesh>(_entry, EntryClassField)); }
            }

            /// <summary>The entry the menu is showing the details of.</summary>
            public bool IsSelected
            {
                get { return _adapter != null && _adapter.SelectedEntryIndex == _index; }
            }

            /// <summary>The game draws a crossed-out overlay over a wielder that has died.</summary>
            public bool IsDead
            {
                get { return IsVisible(GetField<GameObject>(_entry, EntryDeadOverlayField)); }
            }

            /// <summary>The game draws a frame around a wielder the team already has.</summary>
            public bool IsOwned
            {
                get { return IsVisible(GetField<GameObject>(_entry, EntryOwnedFrameField)); }
            }

            /// <summary>The entry's own button - what the row is drawn by.</summary>
            public Component Button
            {
                get { return GetField<UIButton>(_entry, EntryButtonField) as Component; }
            }

            public bool IsVisible
            {
                get { return PurchaseWielderMenuAdapter.IsVisible(_entry as Component); }
            }

            public bool Select()
            {
                return NativeSelectionUtility.Click(GetField<UIButton>(_entry, EntryButtonField));
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(GetField<UIButton>(_entry, EntryButtonField) as Component);
                Select();
            }
        }
    }
}
