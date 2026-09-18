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
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class MapEntityMiniMenuAdapter : IPresent
    {
        private static readonly FieldInfo TopContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_topContainer");
        private static readonly FieldInfo NameTextField = AccessTools.Field(typeof(MapEntityMiniMenu), "_nameText");
        private static readonly FieldInfo CustomNameTextField = AccessTools.Field(typeof(MapEntityMiniMenu), "_customNameText");
        private static readonly FieldInfo CustomNameContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_customNameContainer");
        private static readonly FieldInfo DescriptionTextField = AccessTools.Field(typeof(MapEntityMiniMenu), "_descriptionText");
        private static readonly FieldInfo DescriptionTextContainerField = AccessTools.Field(typeof(MapEntityMiniMenu), "_descriptionTextContainer");
        private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(MapEntityMiniMenu), "_description");
        private static readonly FieldInfo DescriptionEntryContainerField = AccessTools.Field(typeof(MiniMenuDescription), "_entryContainer");
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
            RectTransform topContainer = Reflect.Get<RectTransform>(_menu, TopContainerField);
            return _menu != null
                && topContainer != null
                && ((Component)topContainer).gameObject.activeInHierarchy
                && Entity != null;
        }

        /// <summary>The entity's type name ("Small Settlement"), drawn BELOW the custom name.</summary>
        public string EntityName
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, NameTextField)); }
        }

        /// <summary>The entity's own name ("Crowpoint"), drawn ABOVE the type name.</summary>
        public string CustomName
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, CustomNameTextField)); }
        }

        public bool IsCustomNameVisible
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, CustomNameContainerField)) && !string.IsNullOrWhiteSpace(CustomName); }
        }

        public string BlueprintDescription
        {
            get { return string.Join(" ", BlueprintDescriptionLines); }
        }

        /// <summary>The paragraphs the game wrote the blueprint description in, kept apart rather
        /// than collapsed.</summary>
        public IList<string> BlueprintDescriptionLines
        {
            get { return UITextMeshTextUtility.SpokenLines(Reflect.Get<UITextMesh>(_menu, DescriptionTextField)); }
        }

        /// <summary>The text the blueprint description is drawn as.</summary>
        public Component BlueprintDescriptionComponent
        {
            get { return Reflect.Get<UITextMesh>(_menu, DescriptionTextField); }
        }

        public bool IsBlueprintDescriptionVisible
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, DescriptionTextContainerField)) && !string.IsNullOrWhiteSpace(BlueprintDescription); }
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
                return storedCommander != null ? SpokenLines.Clean(facade.Commanders.GetName(storedCommander.Id)) : string.Empty;
            }
        }

        /// <summary>The one native control the stored wielder is drawn as: clicking it ejects.</summary>
        public Component StoredWielderButton
        {
            get { return Reflect.Get<UIButton>(_menu, StoredWielderButtonField); }
        }

        public bool IsStoredWielderVisible
        {
            get { return GameObjects.IsLive(Reflect.Get<UIButton>(_menu, StoredWielderButtonField)); }
        }

        public Tooltip StoredWielderTooltip
        {
            get { return TooltipWithLines(Reflect.Get<UIImage>(_menu, StoredWielderImageField)); }
        }

        public bool ActivateEjectWielder()
        {
            return NativeSelectionUtility.Click(Reflect.Get<UIButton>(_menu, StoredWielderButtonField));
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

        /// <summary>The game's own caption for the row of upgrade slots.</summary>
        public string UpgradeCaption
        {
            get { return SpokenText.Get(Localization, "Adventure/MapEntityHUD/Upgrades", string.Empty); }
        }

        /// <summary>How many of the entity's upgrade tiers the game has drawn as built.</summary>
        public int UpgradeTiersBuilt
        {
            get
            {
                int used;
                int total;
                GetUpgradeCounts(out used, out total);
                return used;
            }
        }

        /// <summary>How many upgrade tiers the entity has slots for.</summary>
        public int UpgradeTiersTotal
        {
            get
            {
                int used;
                int total;
                GetUpgradeCounts(out used, out total);
                return total;
            }
        }

        /// <summary>The row of upgrade slots the tier summary is read off.</summary>
        public Component UpgradesComponent
        {
            get
            {
                GameObject parent = Reflect.Get<GameObject>(_menu, UpgradesParentField);
                return parent != null ? parent.transform : null;
            }
        }

        public bool IsUpgradeSummaryVisible
        {
            get
            {
                int used;
                int total;
                GetUpgradeCounts(out used, out total);
                return total > 0 && GameObjects.IsLive(Reflect.Get<GameObject>(_menu, UpgradesParentField));
            }
        }

        public string SiegeState
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, SiegeStateDescriptionField)); }
        }

        /// <summary>The text the siege state is drawn as.</summary>
        public Component SiegeStateComponent
        {
            get { return Reflect.Get<UITextMesh>(_menu, SiegeStateDescriptionField); }
        }

        public bool IsSiegeStateVisible
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_menu, SiegeStateDescriptionContainerField)) && !string.IsNullOrWhiteSpace(SiegeState); }
        }

        /// <summary>The controller drawing the round dots the town status is counted off.</summary>
        public Component TownStatusComponent
        {
            get { return Reflect.Get<TownStatusController>(_menu, TownStatusControllerField); }
        }

        /// <summary>How many rounds of the claim the game has drawn as filled.</summary>
        public int TownStatusRoundsComplete
        {
            get { return CountFilledRounds(TownStatusEntries); }
        }

        /// <summary>How many rounds of the claim the game has drawn as still empty.</summary>
        public int TownStatusRoundsRemaining
        {
            get
            {
                // One read of the entries answers both halves; asking TownStatusRoundsComplete here
                // would read them again and walk them a second time.
                List<TownStatusControllerRoundEntry> entries = TownStatusEntries;
                return entries == null ? 0 : entries.Count - CountFilledRounds(entries);
            }
        }

        private static int CountFilledRounds(List<TownStatusControllerRoundEntry> entries)
        {
            int filled = 0;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                Transform filledSlot = Reflect.Get<Transform>(entries[i], TownStatusFilledSlotField);
                if (filledSlot != null && ((Component)filledSlot).gameObject.activeSelf)
                {
                    filled++;
                }
            }

            return filled;
        }

        public bool IsTownStatusVisible
        {
            get
            {
                TownStatusController controller = Reflect.Get<TownStatusController>(_menu, TownStatusControllerField);
                List<TownStatusControllerRoundEntry> entries = TownStatusEntries;
                return GameObjects.IsLive(controller) && entries != null && entries.Count > 0;
            }
        }

        // The rows the block last drew. SetDetails clears the entry container and instantiates new
        // rows into it and Clear destroys them, so the memo's key is what that container holds - read
        // from the game each build, not a generation a hook feeds (AGENTS.md, Screen Resolution) -
        // TOGETHER WITH THE ENTITY the menu is drawn for. Clear destroys through Object.Destroy,
        // which Unity defers to the end of the frame, and Show redraws for the next entity without
        // hiding first, so on that frame the block holds the previous entity's rows as well: a new
        // entity that draws none of its own would keep the count and both ends and the key would
        // hit.
        private readonly ContainerMemo<IReadOnlyList<DescriptionRow>> _descriptionRows =
            new ContainerMemo<IReadOnlyList<DescriptionRow>>();

        // The entity the rows were last read for, and the entries that read kept. On the frame the
        // entity moves, the rows awaiting that deferred destroy are still walked, and they are the
        // ones named here, so they are skipped by identity rather than spoken under the new name.
        private IMapEntity _rowsEntity;
        private Component[] _rowsEntries = new Component[0];

        public IReadOnlyList<DescriptionRow> GetDescriptionRows()
        {
            MiniMenuDescription description = Reflect.Get<MiniMenuDescription>(_menu, DescriptionField);
            if (!GameObjects.IsLive(description))
            {
                return new List<DescriptionRow>();
            }

            IMapEntity entity = Entity;
            return _descriptionRows.Get(
                GetDescriptionEntryContainer(description),
                entity,
                () => ReadDescriptionRows(description, entity));
        }

        private IReadOnlyList<DescriptionRow> ReadDescriptionRows(MiniMenuDescription description, IMapEntity entity)
        {
            List<DescriptionRow> rows = new List<DescriptionRow>();

            // Reached only when the key above says the block has been rewritten, so a still menu
            // costs no walk.
            Component[] pendingDestroy = ReferenceEquals(entity, _rowsEntity) ? null : _rowsEntries;
            MapEntityHUDDescriptionEntry[] entries = description.GetComponentsInChildren<MapEntityHUDDescriptionEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                MapEntityHUDDescriptionEntry entry = entries[i];
                if (!GameObjects.IsLive(entry) || WasRead(pendingDestroy, entry))
                {
                    continue;
                }

                UITextMesh text = Reflect.Get<UITextMesh>(entry, DescriptionEntryTextField);
                UIImage icon = Reflect.Get<UIImage>(entry, DescriptionEntryIconField);
                IList<string> label = UITextMeshTextUtility.SpokenLines(text);
                if (label.Count == 0)
                {
                    continue;
                }

                // Counted off the rows KEPT, so a row has the same index on the frame the previous
                // entity's are still there as it has once they are gone, and the node keeps its id.
                rows.Add(new DescriptionRow(
                    rows.Count,
                    entry,
                    label,
                    () => FirstTooltipWithLines(icon, text)));
            }

            _rowsEntity = entity;
            _rowsEntries = new Component[rows.Count];
            for (int i = 0; i < rows.Count; i++)
            {
                _rowsEntries[i] = rows[i].Component;
            }

            return rows;
        }

        private static bool WasRead(Component[] previous, Component entry)
        {
            for (int i = 0; previous != null && i < previous.Length; i++)
            {
                if (ReferenceEquals(previous[i], entry))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The transform the block instantiates its rows into, which is what a rewrite
        /// empties and refills.</summary>
        private static Transform GetDescriptionEntryContainer(MiniMenuDescription description)
        {
            UITransform container = description != null && DescriptionEntryContainerField != null
                ? DescriptionEntryContainerField.GetValue(description) as UITransform
                : null;
            return container != null ? container.MonoTransform : null;
        }

        public IReadOnlyList<ActionButton> GetActions()
        {
            List<ActionButton> buttons = new List<ActionButton>();
            MiniMenuActions actions = Reflect.Get<MiniMenuActions>(_menu, ActionsField);
            if (actions == null || actions.ActiveEntries == null)
            {
                return buttons;
            }

            for (int i = 0; i < actions.ActiveEntries.Count; i++)
            {
                MiniMenuActionButton entry = actions.ActiveEntries[i];
                if (!GameObjects.IsLive(entry) || entry.GameAction == null)
                {
                    continue;
                }

                UIButton button = Reflect.Get<UIButton>(entry, ActionButtonField);
                UIImage background = Reflect.Get<UIImage>(entry, ActionBackgroundImageField);
                IGameAction gameAction = entry.GameAction;
                buttons.Add(new ActionButton(
                    i,
                    gameAction.ActionType,
                    entry,
                    GetActionLabel(gameAction),
                    () => NativeSelectionUtility.Click(button),
                    () => NativeSelectionUtility.Select(entry.GetSelectable()),
                    () => button != null && button.Interactable,
                    () => TooltipWithLines(background)));
            }

            return buttons;
        }

        /// <summary>The game's own hide path, which is what clicking outside the menu runs.</summary>
        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        private List<TownStatusControllerRoundEntry> TownStatusEntries
        {
            get
            {
                TownStatusController controller = Reflect.Get<TownStatusController>(_menu, TownStatusControllerField);
                return Reflect.Get<List<TownStatusControllerRoundEntry>>(controller, TownStatusEntriesField);
            }
        }

        private IMapEntity Entity
        {
            get { return Reflect.Get<IMapEntity>(_menu, EntityField); }
        }

        private IClientAdventureFacade Facade
        {
            get { return Reflect.Get<IClientAdventureFacade>(_menu, AdventureFacadeField); }
        }

        private ILocalizationHandler Localization
        {
            get { return Reflect.Get<ILocalizationHandler>(_menu, LocalizationField); }
        }

        private void GetUpgradeCounts(out int used, out int total)
        {
            used = 0;
            total = 0;
            List<MapEntityHUDUpgradeSlot> slots = Reflect.Get<List<MapEntityHUDUpgradeSlot>>(_menu, SlotsField);
            if (slots == null)
            {
                return;
            }

            for (int i = 0; i < slots.Count; i++)
            {
                MapEntityHUDUpgradeSlot slot = slots[i];
                if (!GameObjects.IsLive(slot))
                {
                    continue;
                }

                total++;
                UIImage filledSlot = Reflect.Get<UIImage>(slot, FilledSlotField);
                if (GameObjects.IsLive(filledSlot))
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
                string line = SpokenLines.Clean(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return BuildActionLabel(line, details);
                }
            }

            return SpokenLines.Clean(action.ActionType.ToString());
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
            return EssenceText.Name(Localization, essenceType);
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

        public sealed class DescriptionRow
        {
            public DescriptionRow(int index, Component component, IList<string> lines, System.Func<Tooltip> getTooltip)
            {
                Index = index;
                Component = component;
                Lines = lines ?? new List<string>();
                GetTooltip = getTooltip;
            }

            /// <summary>Where the row sits among the entries the block drew.</summary>
            public int Index { get; private set; }

            /// <summary>The entry the game draws the row as.</summary>
            public Component Component { get; private set; }

            /// <summary>What the row says, one line per paragraph the game wrote it in.</summary>
            public IList<string> Lines { get; private set; }

            public System.Func<Tooltip> GetTooltip { get; private set; }
        }

        public sealed class ActionButton
        {
            public ActionButton(
                int index,
                HUDActionType actionType,
                Component component,
                string label,
                System.Func<bool> activate,
                System.Action focus,
                System.Func<bool> isEnabled,
                System.Func<Tooltip> getTooltip)
            {
                Index = index;
                ActionType = actionType;
                Component = component;
                Label = label ?? string.Empty;
                Activate = activate;
                Focus = focus;
                IsEnabled = isEnabled;
                GetTooltip = getTooltip;
            }

            /// <summary>Where the button sits among the actions the menu drew.</summary>
            public int Index { get; private set; }

            /// <summary>What the game calls the action this button performs.</summary>
            public HUDActionType ActionType { get; private set; }

            /// <summary>The button the game draws the action as.</summary>
            public Component Component { get; private set; }

            public string Label { get; private set; }

            public System.Func<bool> Activate { get; private set; }

            public System.Action Focus { get; private set; }

            public System.Func<bool> IsEnabled { get; private set; }

            public System.Func<Tooltip> GetTooltip { get; private set; }
        }
    }
}
