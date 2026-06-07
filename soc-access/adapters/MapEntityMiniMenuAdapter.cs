using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.GameActions;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class MapEntityMiniMenuAdapter
    {
        private static readonly FieldInfo TopContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_topContainer");
        private static readonly FieldInfo NameTextField = AccessTools.Field(typeof(MapEntityMiniMenu), "_nameText");
        private static readonly FieldInfo CustomNameTextField = AccessTools.Field(typeof(MapEntityMiniMenu), "_customNameText");
        private static readonly FieldInfo CustomNameContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_customNameContainer");
        private static readonly FieldInfo DescriptionTextField = AccessTools.Field(typeof(MapEntityMiniMenu), "_descriptionText");
        private static readonly FieldInfo DescriptionTextContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_descriptionTextContainer");
        private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(MapEntityMiniMenu), "_description");
        private static readonly FieldInfo ActionsField = AccessTools.Field(typeof(MapEntityMiniMenu), "_actions");
        private static readonly FieldInfo UpgradesParentField = AccessTools.Field(typeof(MapEntityMiniMenu), "_upgradesParent");
        private static readonly FieldInfo SlotsField = AccessTools.Field(typeof(MapEntityMiniMenu), "_slots");
        private static readonly FieldInfo FilledSlotField = AccessTools.Field(typeof(MapEntityHUDUpgradeSlot), "_filledSlot");
        private static readonly FieldInfo EntityField = AccessTools.Field(typeof(MapEntityMiniMenu), "_entity");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(MapEntityMiniMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(MapEntityMiniMenu), "_localization");
        private static readonly FieldInfo StoredWielderButtonField = AccessTools.Field(typeof(MapEntityMiniMenu), "_storedWielderButton");
        private static readonly FieldInfo StoredWielderImageField = AccessTools.Field(typeof(MapEntityMiniMenu), "_storedWielderImage");
        private static readonly FieldInfo SiegeStateDescriptionContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_siegeStateDescriptionContainer");
        private static readonly FieldInfo SiegeStateDescriptionField = AccessTools.Field(typeof(MapEntityMiniMenu), "_siegeStateDescription");
        private static readonly FieldInfo TownStatusControllerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_townStatusController");
        private static readonly FieldInfo TownStatusEntriesField = AccessTools.Field(typeof(TownStatusController), "_activeEntries");
        private static readonly FieldInfo TownStatusFilledSlotField = AccessTools.Field(typeof(TownStatusControllerRoundEntry), "_filledSlot");
        private static readonly FieldInfo DescriptionEntryIconField = AccessTools.Field(typeof(MapEntityHUDDescriptionEntry), "_icon");
        private static readonly FieldInfo DescriptionEntryTextField = AccessTools.Field(typeof(MapEntityHUDDescriptionEntry), "_text");
        private static readonly FieldInfo ActionButtonField = AccessTools.Field(typeof(MiniMenuActionButton), "_button");
        private static readonly FieldInfo ActionBackgroundImageField = AccessTools.Field(typeof(MiniMenuActionButton), "_backgroundImage");

        private readonly MapEntityMiniMenu _menu;

        public MapEntityMiniMenuAdapter(MapEntityMiniMenu menu)
        {
            _menu = menu;
        }

        public MapEntityMiniMenu Source
        {
            get { return _menu; }
        }

        public bool IsPresent()
        {
            RectTransform topContainer = GetField<RectTransform>(_menu, TopContainerField);
            return _menu != null
                && topContainer != null
                && ((Component)topContainer).gameObject.activeInHierarchy
                && Entity != null;
        }

        public string EntityName
        {
            get { return GetText(GetField<UITextMesh>(_menu, NameTextField)); }
        }

        public string CustomName
        {
            get { return GetText(GetField<UITextMesh>(_menu, CustomNameTextField)); }
        }

        public bool IsCustomNameVisible
        {
            get { return IsActive(GetField<GameObject>(_menu, CustomNameContainerField)) && !string.IsNullOrWhiteSpace(CustomName); }
        }

        public string BlueprintDescription
        {
            get { return GetText(GetField<UITextMesh>(_menu, DescriptionTextField)); }
        }

        public bool IsBlueprintDescriptionVisible
        {
            get { return IsActive(GetField<GameObject>(_menu, DescriptionTextContainerField)) && !string.IsNullOrWhiteSpace(BlueprintDescription); }
        }

        public string StoredWielderName
        {
            get
            {
                IClientAdventureFacade facade = Facade;
                IMapEntity entity = Entity;
                if (facade == null || entity == null)
                {
                    return string.Empty;
                }

                ICommanderState storedCommander = facade.MapEntities.GetStoredCommander(entity.Id);
                return storedCommander != null ? SpeechTextSanitizer.Normalize(facade.Commanders.GetName(storedCommander.Id)) : string.Empty;
            }
        }

        public bool IsStoredWielderVisible
        {
            get { return IsActive(GetField<UIButton>(_menu, StoredWielderButtonField)); }
        }

        public Tooltip StoredWielderTooltip
        {
            get { return TooltipWithLines(GetField<UIImage>(_menu, StoredWielderImageField)); }
        }

        public bool ActivateEjectWielder()
        {
            return NativeSelectionUtility.Click(GetField<UIButton>(_menu, StoredWielderButtonField));
        }

        public bool IsEjectWielderEnabled()
        {
            IClientAdventureFacade facade = Facade;
            IMapEntity entity = Entity;
            return IsStoredWielderVisible
                && facade != null
                && entity != null
                && facade.Commands.CanEjectCommander(entity.Id).success;
        }

        public string UpgradeSummary
        {
            get
            {
                int used;
                int total;
                GetUpgradeCounts(out used, out total);
                return GetLocalizedText("Adventure/MapEntityHUD/Upgrades", "Tier:") + " " + used + " / " + total;
            }
        }

        public bool IsUpgradeSummaryVisible
        {
            get
            {
                int used;
                int total;
                GetUpgradeCounts(out used, out total);
                return total > 0 && IsActive(GetField<GameObject>(_menu, UpgradesParentField));
            }
        }

        public string SiegeState
        {
            get { return GetText(GetField<UITextMesh>(_menu, SiegeStateDescriptionField)); }
        }

        public bool IsSiegeStateVisible
        {
            get { return IsActive(GetField<GameObject>(_menu, SiegeStateDescriptionContainerField)) && !string.IsNullOrWhiteSpace(SiegeState); }
        }

        public string TownStatus
        {
            get
            {
                TownStatusController controller = GetField<TownStatusController>(_menu, TownStatusControllerField);
                List<TownStatusControllerRoundEntry> entries = GetField<List<TownStatusControllerRoundEntry>>(controller, TownStatusEntriesField);
                if (entries == null || entries.Count == 0)
                {
                    return string.Empty;
                }

                int filled = 0;
                for (int i = 0; i < entries.Count; i++)
                {
                    Transform filledSlot = GetField<Transform>(entries[i], TownStatusFilledSlotField);
                    if (filledSlot != null && ((Component)filledSlot).gameObject.activeSelf)
                    {
                        filled++;
                    }
                }

                int remaining = entries.Count - filled;
                string prefix = !string.IsNullOrWhiteSpace(SiegeState) ? SiegeState : "Town status";
                return prefix + ": " + filled + " rounds complete, " + remaining + " rounds remaining";
            }
        }

        public bool IsTownStatusVisible
        {
            get
            {
                TownStatusController controller = GetField<TownStatusController>(_menu, TownStatusControllerField);
                return IsActive(controller) && !string.IsNullOrWhiteSpace(TownStatus);
            }
        }

        public IReadOnlyList<DescriptionRow> GetDescriptionRows()
        {
            List<DescriptionRow> rows = new List<DescriptionRow>();
            MiniMenuDescription description = GetField<MiniMenuDescription>(_menu, DescriptionField);
            if (!IsActive(description))
            {
                return rows;
            }

            MapEntityHUDDescriptionEntry[] entries = description.GetComponentsInChildren<MapEntityHUDDescriptionEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                MapEntityHUDDescriptionEntry entry = entries[i];
                if (!IsActive(entry))
                {
                    continue;
                }

                UITextMesh text = GetField<UITextMesh>(entry, DescriptionEntryTextField);
                UIImage icon = GetField<UIImage>(entry, DescriptionEntryIconField);
                string label = GetText(text);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                rows.Add(new DescriptionRow(
                    "map-entity-description-row-" + i,
                    label,
                    () => HideNativeTooltip(),
                    () => FirstTooltipWithLines(icon, text)));
            }

            return rows;
        }

        public IReadOnlyList<ActionButton> GetActions()
        {
            List<ActionButton> buttons = new List<ActionButton>();
            MiniMenuActions actions = GetField<MiniMenuActions>(_menu, ActionsField);
            if (actions == null || actions.ActiveEntries == null)
            {
                return buttons;
            }

            for (int i = 0; i < actions.ActiveEntries.Count; i++)
            {
                MiniMenuActionButton entry = actions.ActiveEntries[i];
                if (!IsActive(entry) || entry.GameAction == null)
                {
                    continue;
                }

                UIButton button = GetField<UIButton>(entry, ActionButtonField);
                UIImage background = GetField<UIImage>(entry, ActionBackgroundImageField);
                IGameAction gameAction = entry.GameAction;
                buttons.Add(new ActionButton(
                    "map-entity-action-" + i + "-" + gameAction.ActionType,
                    GetActionLabel(gameAction),
                    () => NativeSelectionUtility.Click(button),
                    () => NativeSelectionUtility.Select(entry.GetSelectable()),
                    () => button != null && button.Interactable,
                    () => TooltipWithLines(background)));
            }

            return buttons;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        private IMapEntity Entity
        {
            get { return GetField<IMapEntity>(_menu, EntityField); }
        }

        private IClientAdventureFacade Facade
        {
            get { return GetField<IClientAdventureFacade>(_menu, AdventureFacadeField); }
        }

        private ILocalizationHandler Localization
        {
            get { return GetField<ILocalizationHandler>(_menu, LocalizationField); }
        }

        private string GetLocalizedText(string key, string fallback)
        {
            ILocalizationHandler localization = Localization;
            return SpeechTextSanitizer.Normalize(GameText.Get(localization, key, fallback ?? string.Empty));
        }

        private void GetUpgradeCounts(out int used, out int total)
        {
            used = 0;
            total = 0;
            List<MapEntityHUDUpgradeSlot> slots = GetField<List<MapEntityHUDUpgradeSlot>>(_menu, SlotsField);
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                MapEntityHUDUpgradeSlot slot = slots[i];
                if (!IsActive(slot))
                {
                    continue;
                }

                total++;
                UIImage filledSlot = GetField<UIImage>(slot, FilledSlotField);
                if (IsActive(filledSlot))
                {
                    used++;
                }
            }
        }

        private string GetActionLabel(IGameAction action)
        {
            if (action == null)
            {
                return string.Empty;
            }

            IDetails details = action.GetDetails();
            IReadOnlyList<string> lines = NativeTooltipUtility.ToSpeechLines(details, Localization);
            for (int i = 0; i < lines.Count; i++)
            {
                string line = SpeechTextSanitizer.Normalize(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return BuildActionLabel(line, details);
                }
            }

            return SpeechTextSanitizer.Normalize(action.ActionType.ToString());
        }

        private string BuildActionLabel(string baseLabel, IDetails details)
        {
            LevelUpBuildingDetails? levelUp = details is LevelUpBuildingDetails
                ? (LevelUpBuildingDetails?)details
                : null;
            if (!levelUp.HasValue || !levelUp.Value.EssenceVariant.HasValue)
            {
                return baseLabel;
            }

            string essenceName = GetEssenceName(levelUp.Value.EssenceVariant.Value);
            if (string.IsNullOrWhiteSpace(essenceName)
                || (!string.IsNullOrWhiteSpace(baseLabel) && baseLabel.IndexOf(essenceName, System.StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return baseLabel;
            }

            return ModText.Get(ModStrings.Common.EssenceVariant, baseLabel, essenceName);
        }

        private string GetEssenceName(EssenceType essenceType)
        {
            switch (essenceType)
            {
                case EssenceType.Order:
                    return GetLocalizedText("Units/Types/Order", "Order");
                case EssenceType.Creation:
                    return GetLocalizedText("Units/Types/Creation", "Creation");
                case EssenceType.Chaos:
                    return GetLocalizedText("Units/Types/Chaos", "Chaos");
                case EssenceType.Arcana:
                    return GetLocalizedText("Units/Types/Arcana", "Arcana");
                case EssenceType.Destruction:
                    return GetLocalizedText("Units/Types/Destruction", "Destruction");
                default:
                    return SpeechTextSanitizer.Normalize(essenceType.ToString());
            }
        }

        private Tooltip FirstTooltipWithLines(params Component[] components)
        {
            if (components == null)
            {
                return null;
            }

            for (int i = 0; i < components.Length; i++)
            {
                Tooltip tooltip = TooltipWithLines(components[i]);
                if (tooltip != null)
                {
                    return tooltip;
                }
            }

            return null;
        }

        private Tooltip TooltipWithLines(Component component)
        {
            Tooltip tooltip = Tooltip.ForComponent(component, Localization);
            return HasTooltipLines(tooltip) ? tooltip : null;
        }

        private static bool HasTooltipLines(Tooltip tooltip)
        {
            return tooltip != null && tooltip.TextLines != null && tooltip.TextLines.Count > 0;
        }

        private static string GetText(UITextMesh text)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static T GetField<T>(object instance, FieldInfo field) where T : class
        {
            return instance != null && field != null ? field.GetValue(instance) as T : null;
        }

        private static bool IsActive(Component component)
        {
            return component != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        public sealed class DescriptionRow
        {
            public DescriptionRow(string id, string label, System.Action focus, System.Func<Tooltip> getTooltip)
            {
                Id = id;
                Label = label ?? string.Empty;
                Focus = focus;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public System.Action Focus { get; private set; }

            public System.Func<Tooltip> GetTooltip { get; private set; }
        }

        public sealed class ActionButton
        {
            public ActionButton(
                string id,
                string label,
                System.Func<bool> activate,
                System.Action focus,
                System.Func<bool> isEnabled,
                System.Func<Tooltip> getTooltip)
            {
                Id = id;
                Label = label ?? string.Empty;
                Activate = activate;
                Focus = focus;
                IsEnabled = isEnabled;
                GetTooltip = getTooltip;
            }

            public string Id { get; private set; }

            public string Label { get; private set; }

            public System.Func<bool> Activate { get; private set; }

            public System.Action Focus { get; private set; }

            public System.Func<bool> IsEnabled { get; private set; }

            public System.Func<Tooltip> GetTooltip { get; private set; }
        }
    }
}
