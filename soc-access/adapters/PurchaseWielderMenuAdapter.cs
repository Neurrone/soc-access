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
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class PurchaseWielderMenuAdapter
    {
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(PurchaseWielderMenu), "_async");
        private static readonly FieldInfo WielderListTitleField = AccessTools.Field(typeof(PurchaseWielderMenu), "_wielderListTitle");
        private static readonly FieldInfo ActiveEntriesField = AccessTools.Field(typeof(PurchaseWielderMenu), "_activeEntries");
        private static readonly FieldInfo SelectedEntryIndexField = AccessTools.Field(typeof(PurchaseWielderMenu), "_selectedEntryIndex");
        private static readonly FieldInfo WielderDetailsField = AccessTools.Field(typeof(PurchaseWielderMenu), "_wielderDetails");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(PurchaseWielderMenu), "_localizationHandler");

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

        public string SelectedEntryId
        {
            get
            {
                IReadOnlyList<EntryItem> entries = GetEntries();
                int index = SelectedEntryIndex;
                return index >= 0 && index < entries.Count ? entries[index].Id : string.Empty;
            }
        }

        public int SelectedEntryIndex
        {
            get
            {
                object value = SelectedEntryIndexField != null ? SelectedEntryIndexField.GetValue(_menu) : null;
                return value is int ? (int)value : -1;
            }
        }

        public string SelectedSummary
        {
            get
            {
                PurchaseWielderDetails details = GetDetails();
                string name = GetText(GetField<UITextMesh>(details, DetailsNameField));
                string description = GetText(GetField<UITextMesh>(details, DetailsDescriptionField));
                string level = IsVisible(GetField<GameObject>(details, DetailsLevelContainerField))
                    ? GetText(GetField<UITextMesh>(details, DetailsLevelTextField))
                    : string.Empty;
                List<string> parts = new List<string>();
                AddIfNotEmpty(parts, name);
                if (!string.IsNullOrWhiteSpace(level))
                {
                    parts.Add(ModText.Get(ModStrings.Screens.LevelValue, level));
                }

                AddIfNotEmpty(parts, description);
                return JoinSentences(parts);
            }
        }

        public string StatsHeader
        {
            get { return GetLocalizedText("Common/CommanderInventory/Stats", "Stats"); }
        }

        public string TroopsHeader
        {
            get { return GetLocalizedText("Adventure/PurchaseWielderMenu/TroopsAtStartHeader", "Troops"); }
        }

        public string SkillsHeader
        {
            get { return GetLocalizedText("Commanders/Tooltip/Skills", "Skills"); }
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

        public string StatsSummary
        {
            get
            {
                return JoinSentences(new List<string>
                {
                    OffenceHeader + " " + Offence,
                    DefenceHeader + " " + Defence,
                    MovementHeader + " " + Movement,
                    ViewRadiusHeader + " " + ViewRadius
                });
            }
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

        public void FocusTroop(int index)
        {
            IReadOnlyList<TroopHUDEntry> entries = GetTroopEntries();
            if (index >= 0 && index < entries.Count && entries[index] != null)
            {
                NativeSelectionUtility.Select(entries[index].GetSelectable());
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

        public void FocusSkill(int index)
        {
            IReadOnlyList<PurchaseWielderSkillEntry> entries = GetSkillEntries();
            if (index >= 0 && index < entries.Count)
            {
                NativeSelectionUtility.Select(GetField<UIImage>(entries[index], SkillFrameField) as Component);
            }
        }

        public bool HasSpecialization()
        {
            UITextMesh text = GetField<UITextMesh>(GetDetails(), DetailsSpecializationField);
            return IsVisible(text as Component) && !string.IsNullOrWhiteSpace(GetText(text));
        }

        public string Specialization
        {
            get
            {
                string body = GetText(GetField<UITextMesh>(GetDetails(), DetailsSpecializationField));
                if (string.IsNullOrWhiteSpace(body))
                {
                    return string.Empty;
                }

                string header = GetLocalizedText("Commanders/Tooltip/Specializations", string.Empty);
                return string.IsNullOrWhiteSpace(header) ? body : header.TrimEnd(':') + ": " + body;
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
                    ? SpeechTextSanitizer.Normalize(string.Join(". ", tooltip.TextLines))
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
                AddCostPart(parts, section, GoldCostEntryField, GoldAmountTextField, "gold");
                AddCostPart(parts, section, StoneCostEntryField, StoneAmountTextField, "stone");
                AddCostPart(parts, section, WoodCostEntryField, WoodAmountTextField, "wood");
                AddCostPart(parts, section, GlimmerWeaveCostEntryField, GlimmerWeaveAmountTextField, "glimmerweave");
                AddCostPart(parts, section, AncientAmberCostEntryField, AncientAmberAmountTextField, "ancient amber");
                AddCostPart(parts, section, CelestialOreCostEntryField, CelestialOreAmountTextField, "celestial ore");
                return parts.Count == 0 ? string.Empty : "Cost: " + JoinWithAnd(parts);
            }
        }

        private PurchaseWielderDetails GetDetails()
        {
            return GetField<PurchaseWielderDetails>(_menu, WielderDetailsField);
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

        private static void AddCostPart(List<string> parts, LargeCostSection section, FieldInfo entryField, FieldInfo textField, string resourceName)
        {
            UITransform entry = GetField<UITransform>(section, entryField);
            if (entry == null || !entry.Active)
            {
                return;
            }

            string amount = GetText(GetField<UITextMesh>(section, textField));
            if (!string.IsNullOrWhiteSpace(amount))
            {
                parts.Add(amount + " " + resourceName);
            }
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private string GetLocalizedText(string key, string fallback)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, fallback ?? string.Empty));
        }

        private static string FirstTooltipLine(Tooltip tooltip)
        {
            if (tooltip == null || tooltip.TextLines == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < tooltip.TextLines.Count; i++)
            {
                string line = SpeechTextSanitizer.Normalize(tooltip.TextLines[i]);
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

        private static void AddIfNotEmpty(List<string> parts, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                parts.Add(value);
            }
        }

        private static string JoinSentences(List<string> parts)
        {
            List<string> filtered = new List<string>();
            for (int i = 0; i < parts.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(parts[i]))
                {
                    filtered.Add(parts[i]);
                }
            }

            return string.Join(". ", filtered.ToArray());
        }

        private static string JoinWithAnd(List<string> parts)
        {
            if (parts.Count == 1)
            {
                return parts[0];
            }

            if (parts.Count == 2)
            {
                return parts[0] + " and " + parts[1];
            }

            return string.Join(", ", parts.GetRange(0, parts.Count - 1).ToArray()) + ", and " + parts[parts.Count - 1];
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

        internal sealed class EntryItem
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

            public string Label
            {
                get
                {
                    string name = GetText(GetField<UITextMesh>(_entry, EntryNameField));
                    string classText = GetText(GetField<UITextMesh>(_entry, EntryClassField));
                    if (string.IsNullOrWhiteSpace(name))
                    {
                        return classText;
                    }

                    return string.IsNullOrWhiteSpace(classText) ? name : name + ", " + classText;
                }
            }

            public string Status
            {
                get
                {
                    if (IsVisible(GetField<GameObject>(_entry, EntryDeadOverlayField)))
                    {
                        return "dead";
                    }

                    if (IsVisible(GetField<GameObject>(_entry, EntryOwnedFrameField)))
                    {
                        return "owned";
                    }

                    return string.Empty;
                }
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
