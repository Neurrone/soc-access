using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Levels;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Objectives;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureHudAdapter
    {
        private static readonly FieldInfo CommanderHudPortraitEssenceContainerField =
            AccessTools.Field(typeof(CommanderHUDPortrait), "_essenceContainer");
        private static readonly FieldInfo CommanderHudPortraitExperienceBarField =
            AccessTools.Field(typeof(CommanderHUDPortrait), "_experienceBar");
        private static readonly FieldInfo ExperienceBarTooltipImageField =
            AccessTools.Field(typeof(ExperienceBar), "_tooltipImage");
        private static readonly FieldInfo ExperienceBarLevelTextField =
            AccessTools.Field(typeof(ExperienceBar), "_levelText");
        private static readonly FieldInfo ExperienceBarLevelUpButtonField =
            AccessTools.Field(typeof(ExperienceBar), "_levelUpButton");
        private static readonly FieldInfo EssenceOrderImageField =
            AccessTools.Field(typeof(AdventureEssenceContainer), "_orderImageNonActive");
        private static readonly FieldInfo EssenceCreationImageField =
            AccessTools.Field(typeof(AdventureEssenceContainer), "_creationImageNonActive");
        private static readonly FieldInfo EssenceChaosImageField =
            AccessTools.Field(typeof(AdventureEssenceContainer), "_chaosImageNonActive");
        private static readonly FieldInfo EssenceArcanaImageField =
            AccessTools.Field(typeof(AdventureEssenceContainer), "_arcanaImageNonActive");
        private static readonly FieldInfo EssenceDestructionImageField =
            AccessTools.Field(typeof(AdventureEssenceContainer), "_destructionImageNonActive");
        private static readonly FieldInfo TroopHudTroopsField =
            AccessTools.Field(typeof(TroopHUD), "_troops");
        private static readonly FieldInfo MovementActionButtonMoveButtonField =
            AccessTools.Field(typeof(MovementActionButton), "_moveButton");
        private static readonly FieldInfo ResourceHudSettingsField =
            AccessTools.Field(typeof(ResourceHUD), "_settings");
        private static readonly FieldInfo AdventureHudStateHandlerInstallerSettingsField =
            AccessTools.Field(typeof(AdventureHUDStateHandlerInstaller), "_settings");
        private static readonly FieldInfo CommanderHudInstallerSettingsField =
            AccessTools.Field(typeof(CommanderHUDInstaller), "_settings");
        private static readonly FieldInfo ResourceHudInstallerSettingsField =
            AccessTools.Field(typeof(ResourceHUDInstaller), "_settings");
        private static readonly FieldInfo ObjectivesHudInstallerSettingsField =
            AccessTools.Field(typeof(ObjectivesHUDInstaller), "_settings");
        private static readonly FieldInfo ObjectivesHudInstallerEntryContainerField =
            AccessTools.Field(typeof(ObjectivesHUDInstaller), "_entryContainer");
        private static readonly MethodInfo ObjectivesHudExpandMethod =
            AccessTools.Method(typeof(ObjectivesHUD), "Expand");
        private static readonly MethodInfo ObjectivesHudShrinkMethod =
            AccessTools.Method(typeof(ObjectivesHUD), "Shrink");
        private static readonly FieldInfo ObjectivesHudObjectiveEntriesField =
            AccessTools.Field(typeof(ObjectivesHUD), "_objectiveEntries");
        private static readonly FieldInfo ObjectivesHudEntrySettingsField =
            AccessTools.Field(typeof(ObjectivesHUDEntry), "_settings");
        private static readonly FieldInfo ObjectivesHudEntryObjectiveTextField =
            AccessTools.Field(typeof(ObjectivesHUDEntry.Settings), "ObjectiveText");
        private static readonly FieldInfo NotificationHudActiveEntriesField =
            AccessTools.Field(typeof(NotificationHUD), "_activeEntries");
        private static readonly FieldInfo NotificationHudEntrySettingsField =
            AccessTools.Field(typeof(NotificationHUDEntry), "_settings");
        private static readonly FieldInfo TownListEntriesField =
            AccessTools.Field(typeof(TownListUI), "_entries");
        private static readonly FieldInfo WielderListEntryPoolField =
            AccessTools.Field(typeof(WielderListHUD), "_entryPool");
        private static readonly FieldInfo WielderListEntryButtonField =
            AccessTools.Field(typeof(WielderListHUDEntry), "_button");
        private static readonly MethodInfo WielderListEntryRefreshTooltipMethod =
            AccessTools.Method(typeof(WielderListHUDEntry), "RefreshTooltip");
        private static readonly FieldInfo WielderAmountTextField =
            AccessTools.Field(typeof(WielderListHUD), "_wielderAmountText");
        private static readonly FieldInfo WielderAmountContainerField =
            AccessTools.Field(typeof(WielderListHUD), "_wielderAmountContainer");
        private static readonly FieldInfo WielderLimitTooltipAreaField =
            AccessTools.Field(typeof(WielderListHUD), "_wielderLimitTooltipArea");
        private static readonly FieldInfo TeamQueueEntriesField =
            AccessTools.Field(typeof(TeamQueueHUDBehaviour), "_teamQueueEntries");
        private static readonly FieldInfo TeamQueueRoundTextsField =
            AccessTools.Field(typeof(TeamQueueHUDBehaviour), "_roundTexts");
        private static readonly FieldInfo TeamQueueEntryNameTextField =
            AccessTools.Field(typeof(TeamQueueEntryBehaviour), "_nameText");
        private static readonly FieldInfo TeamQueueEntryTooltipButtonField =
            AccessTools.Field(typeof(TeamQueueEntryBehaviour), "_tooltipTransform");
        private static readonly FieldInfo EndTurnHudInstallerSettingsField =
            AccessTools.Field(typeof(EndTurnHUDInstaller), "_settings");
        private static readonly FieldInfo KingdomInformationHudSettingsField =
            AccessTools.Field(typeof(KingdomInformationHUD), "_settings");
        private static readonly FieldInfo KingdomInformationHudInstallerSettingsField =
            AccessTools.Field(typeof(KingdomInformationHUDInstaller), "_hudSettings");
        private static readonly TooltipAnchor[] ResourceTooltipAnchors =
        {
            TooltipAnchor.BottomLeft
        };

        private readonly AdventureMapAdapter _map;
        private readonly DiContainer _container;
        private CommanderHUD.Settings _commanderHudSettings;
        private ResourceHUD.Settings _resourceHudSettings;
        private AdventureHUDStateHandler _hudStateHandler;
        private AdventureHUDStateHandler.Settings _hudStateSettings;
        private ObjectivesHUD _objectivesHud;
        private ObjectivesHUDInstaller _objectivesHudInstaller;
        private ObjectivesHUD.Settings _objectivesHudSettings;
        private Transform _objectivesEntryContainer;
        private NotificationHUD _notificationHud;
        private TownListUI _townListUi;
        private bool _townListUiProbed;
        private KingdomInformationHUD.Settings _kingdomInformationSettings;
        private EndTurnHUD.Settings _endTurnSettings;
        private EndTurnHUD _endTurnHud;
        private static readonly MethodInfo EndTurnUpdateTooltipMethod = AccessTools.Method(typeof(EndTurnHUD), "UpdateTooltip");
        private TeamQueueHUDBehaviour _teamQueueHud;
        // Each of these resolvers ends in a Zenject resolve whose miss is a thrown-and-caught
        // exception, a whole-scene scan, or both, so a session whose panel is absent would pay it
        // on every read; the flag makes the miss cost one lookup. Safe as a per-adapter answer
        // because nothing reads the HUD until AdventureMapAdapter.IsPresent is true, which already
        // waits for the scene loader to be idle on the adventure - the installers have run by then.
        private bool _commanderHudSettingsProbed;
        private bool _resourceHudSettingsProbed;
        private bool _hudStateSettingsProbed;
        private bool _hudStateHandlerProbed;
        private bool _objectivesHudProbed;
        private bool _objectivesHudSettingsProbed;
        private bool _objectivesHudInstallerProbed;
        private bool _notificationHudProbed;
        private bool _kingdomInformationSettingsProbed;
        private bool _endTurnHudProbed;
        private bool _endTurnSettingsProbed;
        private bool _teamQueueHudProbed;
        private UIButton _optionsButton;
        private bool _optionsButtonProbed;
        // The canvas group each HUD container carries, resolved once. See HudGroupVisible.
        private readonly Dictionary<GameObject, CanvasGroup> _canvasGroups = new Dictionary<GameObject, CanvasGroup>();
        private List<ObjectiveEntrySnapshot> _objectiveSnapshots;
        private int _objectiveSnapshotsFrame = -1;
        private List<WielderListHUDEntry> _wielderListEntries;
        private int _wielderListEntriesFrame = -1;
        private PropertyInfo _wielderListActiveEntriesProperty;
        private bool _wielderListActiveEntriesProbed;

        public AdventureHudAdapter(AdventureMapAdapter map, DiContainer container)
        {
            _map = map;
            _container = container;
        }

        public CommanderHudPortraitAdapter SelectedWielderPortrait
        {
            get
            {
                CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
                return new CommanderHudPortraitAdapter(
                    "adventure-selected-wielder",
                    GetSelectedCommanderName,
                    portrait,
                    LocalizationHandler,
                    IsSelectionHudVisible,
                    () => true);
            }
        }

        public bool IsSelectionHudVisible()
        {
            CommanderHUD.Settings settings = CommanderSettings;
            return settings != null
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.SelectionHUDContainer : null)
                && GameObjects.IsLive(settings.CommanderContainer as Component)
                && settings.Portrait != null
                && settings.Portrait.Commander != null;
        }

        public bool IsExperienceVisible()
        {
            return IsSelectionHudVisible() && GameObjects.IsLive(GetExperienceBar());
        }

        /// <summary>The game's own caption for experience.</summary>
        public string ExperienceCaption
        {
            get { return Localize("Commanders/Tooltip/Experience", "Experience"); }
        }

        /// <summary>The game's own caption for a wielder's level.</summary>
        public string LevelCaption
        {
            get { return Localize("Commanders/Tooltip/Level", "Level"); }
        }

        /// <summary>
        /// The level the selected wielder is on, the experience it has and the experience the next
        /// level asks for. False where no wielder is selected, which is the whole of "there is
        /// nothing to count".
        /// </summary>
        public bool TryGetExperience(out int level, out int current, out int nextLevelExperience)
        {
            level = 0;
            current = 0;
            nextLevelExperience = 0;
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            ICommanderState commander = portrait != null ? portrait.Commander : null;
            if (commander == null || commander.Stats == null)
            {
                return false;
            }

            level = commander.GetLevel();
            // The bar's own level text wins where it says anything: it is what the player can see.
            string levelText = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(GetExperienceBar(), ExperienceBarLevelTextField));
            int parsedLevel;
            if (!string.IsNullOrWhiteSpace(levelText) && int.TryParse(levelText, out parsedLevel))
            {
                level = parsedLevel;
            }

            current = commander.Stats.Experience;
            nextLevelExperience = CommanderLevelUtility.GetExperienceForLevel(level + 1);
            return true;
        }

        public void FocusExperience()
        {
            Component component = GetExperienceTooltipComponent();
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        public Tooltip ExperienceTooltip
        {
            get { return Tooltip.ForComponent(GetExperienceTooltipComponent(), LocalizationHandler); }
        }

        public bool IsLevelUpButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(GetLevelUpButton());
        }

        public string LevelUpButtonLabel
        {
            get { return Localize("Adventure/HUD/LevelUpButtonTooltip", "Level up"); }
        }

        public void FocusLevelUpButton()
        {
            NativeSelectionUtility.Select(GetLevelUpButton());
        }

        public bool ClickLevelUpButton()
        {
            return NativeSelectionUtility.Click(GetLevelUpButton());
        }

        public bool IsLevelUpButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetLevelUpButton());
        }

        public bool IsEssenceMenuVisible()
        {
            return IsSelectionHudVisible() && GameObjects.IsLive(GetAdventureEssenceContainer() as Component);
        }

        public string GetEssenceLabel(EssenceType essenceType)
        {
            return GetEssenceName(essenceType) + " " + GetSelectedCommanderEssenceAmount(essenceType);
        }

        public void FocusEssence(EssenceType essenceType)
        {
            Component component = GetEssenceTooltipComponent(essenceType);
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        public Tooltip GetEssenceTooltip(EssenceType essenceType)
        {
            return Tooltip.ForComponent(GetEssenceTooltipComponent(essenceType), LocalizationHandler);
        }

        public bool IsTroopMenuVisible()
        {
            TroopHUD troopHud = CommanderSettings != null ? CommanderSettings.TroopHUD : null;
            if (!IsSelectionHudVisible() || !GameObjects.IsLive(troopHud))
            {
                return false;
            }

            for (int i = 0; i < 9; i++)
            {
                if (IsTroopSlotVisible(i))
                {
                    return true;
                }
            }

            return false;
        }

        public TroopHudAdapter Troops
        {
            get
            {
                return new TroopHudAdapter(
                    CommanderSettings != null ? CommanderSettings.TroopHUD : null,
                    Facade,
                    LocalizationHandler);
            }
        }

        public bool IsTroopSlotVisible(int index)
        {
            TroopHUDEntry entry = GetTroopSlot(index);
            return entry != null && entry.IsUnlocked && GameObjects.IsLive(entry);
        }

        public void FocusTroopSlot(int index)
        {
            TroopHUDEntry entry = GetTroopSlot(index);
            if (entry != null)
            {
                NativeSelectionUtility.Select(entry.GetSelectable());
            }
        }

        public bool IsInventoryButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public string InventoryButtonLabel
        {
            // The button's own tooltip is the same words with the hotkey ("Wielder Sheet (C)"); use
            // it as the label so the readout is not "Wielder Sheet, button, Wielder Sheet (C)" - the
            // tooltip's first line, now identical to the label, drops from the readout. Fall back to
            // the plain game label when the tooltip has not been drawn.
            get
            {
                string label = TooltipLines.First(InventoryButtonTooltip);
                return string.IsNullOrWhiteSpace(label)
                    ? Localize("Adventure/HUD/InventoryButton", "Inventory")
                    : label;
            }
        }

        public void FocusInventoryButton()
        {
            NativeSelectionUtility.Select(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public bool ClickInventoryButton()
        {
            return NativeSelectionUtility.Click(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public bool IsInventoryButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public Tooltip InventoryButtonTooltip
        {
            get { return Tooltip.ForComponent(CommanderSettings != null ? CommanderSettings.InventoryButton : null, LocalizationHandler); }
        }

        public bool IsMoveToDestinationButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(GetMoveToDestinationButton());
        }

        public bool IsMoveToDestinationButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetMoveToDestinationButton());
        }

        public string MoveToDestinationButtonLabel
        {
            get { return TooltipLines.First(MoveToDestinationButtonTooltip); }
        }

        public void FocusMoveToDestinationButton()
        {
            NativeSelectionUtility.Select(GetMoveToDestinationButton());
        }

        public bool ClickMoveToDestinationButton()
        {
            return NativeSelectionUtility.Click(GetMoveToDestinationButton());
        }

        public Tooltip MoveToDestinationButtonTooltip
        {
            get { return Tooltip.ForComponent(GetMoveToDestinationButton(), LocalizationHandler); }
        }

        public bool IsSpellbookButtonVisible()
        {
            return IsSelectionHudVisible() && MenuButtonAdapterBase.IsButtonDrawn(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public string SpellbookButtonLabel
        {
            // As with the wielder-sheet button: take the label from the tooltip so its hotkey-bearing
            // first line ("Spells (V)") is not read twice.
            get
            {
                string label = TooltipLines.First(SpellbookButtonTooltip);
                return string.IsNullOrWhiteSpace(label)
                    ? Localize("Common/HUD/SpellbookButton", "Spellbook")
                    : label;
            }
        }

        public void FocusSpellbookButton()
        {
            NativeSelectionUtility.Select(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public bool ClickSpellbookButton()
        {
            return NativeSelectionUtility.Click(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public bool IsSpellbookButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public Tooltip SpellbookButtonTooltip
        {
            get { return Tooltip.ForComponent(CommanderSettings != null ? CommanderSettings.SpellbookButton : null, LocalizationHandler); }
        }

        public bool IsResourcesMenuVisible()
        {
            ResourceHUD.Settings settings = ResourceSettings;
            return settings != null
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.ResourceContainer : null)
                && settings.Container != null
                && settings.Container.activeInHierarchy;
        }

        /// <summary>The amount the strip draws beside a resource, empty where it draws none.</summary>
        public string GetResourceAmountText(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            return UITextMeshTextUtility.Spoken(entry != null ? entry.AmountText : null);
        }

        /// <summary>The income the strip draws under a resource, empty where it draws none.</summary>
        public string GetResourceIncomeText(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            return entry != null && GameObjects.IsLive(entry.IncomeText)
                ? UITextMeshTextUtility.Spoken(entry.IncomeText)
                : string.Empty;
        }

        public int GetResourceAmount(ResourceType resourceType)
        {
            ITeamState team = Facade != null && Facade.Teams != null ? Facade.Teams.LocalTeamInControl : null;
            Resource resource = team != null && team.Resources != null ? team.Resources.GetResource(resourceType) : null;
            return resource != null ? resource.Amount : 0;
        }

        public string GetResourceName(ResourceType resourceType)
        {
            return ResourceCosts.Name(LocalizationHandler, resourceType);
        }

        public void FocusResource(ResourceType resourceType)
        {
            Component component = GetResourceTooltipComponent(resourceType);
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
            }
        }

        public Tooltip GetResourceTooltip(ResourceType resourceType)
        {
            Component component = GetResourceTooltipComponent(resourceType);
            RectTransform anchor = GetResourceTooltipAnchor(resourceType);
            return anchor != null
                ? Tooltip.ForComponent(component, anchor, ResourceTooltipAnchors, LocalizationHandler)
                : Tooltip.ForComponent(component, LocalizationHandler);
        }

        public bool IsObjectivesMenuVisible()
        {
            bool hudVisible = IsObjectivesHudVisible();
            int entryCount = hudVisible ? GetObjectiveEntryCount() : 0;
            return hudVisible && entryCount > 0;
        }

        public bool IsObjectiveVisible(int index)
        {
            return GetObjectiveSnapshot(index) != null;
        }

        /// <summary>What the objectives panel draws this entry as, empty where it draws nothing.
        /// </summary>
        public string GetObjectiveText(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null ? UITextMeshTextUtility.Spoken(snapshot.Text) : string.Empty;
        }

        /// <summary>Whether this entry is a lose condition rather than something to achieve.</summary>
        public bool IsObjectiveLoseCondition(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null && snapshot.IsLoseCondition;
        }

        /// <summary>Whether every objective of this entry has reached full progress.</summary>
        public bool IsObjectiveComplete(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null && snapshot.IsComplete;
        }

        /// <summary>Whether the game still counts this entry as achievable.</summary>
        public bool CanObjectiveBeCompleted(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return snapshot != null && snapshot.CanBeCompleted;
        }

        /// <summary>
        /// Where this entry's marker stands relative to the selected wielder, in tiles, together with
        /// the size of the map it stands on. Answers false unless the entry has exactly ONE unfinished
        /// objective with a location and there is a living wielder selected to measure from.
        /// </summary>
        public bool TryGetObjectiveMarkerOffset(int index, out Vector2Int offset, out Vector2Int mapSize)
        {
            offset = Vector2Int.zero;
            mapSize = Vector2Int.zero;
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            Objective objective;
            if (snapshot == null || !TryGetSingleObjectiveMarker(snapshot, out objective))
            {
                return false;
            }

            ICommanderState selectedCommander = SelectionHandler != null ? SelectionHandler.SelectedCommander : null;
            if (selectedCommander == null || !selectedCommander.IsAlive)
            {
                return false;
            }

            int mapWidth = _map != null && _map.Facade != null && _map.Facade.Level != null ? _map.Facade.Level.Width : 0;
            int mapHeight = _map != null && _map.Facade != null && _map.Facade.Level != null ? _map.Facade.Level.Height : 0;
            if (mapWidth <= 0 || mapHeight <= 0)
            {
                return false;
            }

            offset = new Vector2Int(
                objective.location.x - selectedCommander.Position.x,
                objective.location.y - selectedCommander.Position.y);
            mapSize = new Vector2Int(mapWidth, mapHeight);
            return true;
        }

        private static bool TryGetSingleObjectiveMarker(ObjectiveEntrySnapshot snapshot, out Objective objective)
        {
            objective = null;
            IReadOnlyList<Objective> objectives = snapshot != null && snapshot.Entry != null ? snapshot.Entry.Objectives : null;
            if (objectives == null)
            {
                return false;
            }

            for (int i = 0; i < objectives.Count; i++)
            {
                Objective candidate = objectives[i];
                if (candidate == null || !candidate.hasLocation || candidate.progress >= 1f)
                {
                    continue;
                }

                if (objective != null)
                {
                    objective = null;
                    return false;
                }

                objective = candidate;
            }

            return objective != null;
        }

        public void FocusObjective(int index)
        {
            InvokeNoArgs(ObjectivesHud, ObjectivesHudExpandMethod);
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            Component component = snapshot != null ? snapshot.Text : null;
            if (component != null)
            {
                NativeSelectionUtility.Select(component);
                NativeSelectionUtility.PointerEnter(component);
            }
        }

        public void UnfocusObjective()
        {
            InvokeNoArgs(ObjectivesHud, ObjectivesHudShrinkMethod);
        }

        public Tooltip GetObjectiveTooltip(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            return Tooltip.ForComponent(snapshot != null ? snapshot.Text : null, LocalizationHandler);
        }

        public bool IsNotificationsMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.NotificationHUDContainer : null)
                && GetNotificationEntryCount() > 0;
        }

        public bool IsNotificationVisible(int index)
        {
            return GetNotificationEntry(index) != null;
        }

        public string GetNotificationLabel(int index)
        {
            NotificationHUDEntry entry = GetNotificationEntry(index);
            if (entry == null || entry.Information == null)
            {
                return string.Empty;
            }

            return SpokenLines.Clean(entry.Information.Text);
        }

        public void FocusNotification(int index)
        {
            UIButton button = GetNotificationButton(index);
            NativeSelectionUtility.Select(button);
            if (button != null)
            {
                NativeSelectionUtility.PointerEnter(button);
            }
        }

        public bool ClickNotification(int index)
        {
            return NativeSelectionUtility.Click(GetNotificationButton(index));
        }

        public Tooltip GetNotificationTooltip(int index)
        {
            UIButton button = GetNotificationButton(index);
            Tooltip tooltip = Tooltip.ForComponent(button, LocalizationHandler);
            if (tooltip == null)
            {
                return new Tooltip(
                    () => new[] { GetNotificationLabel(index) },
                    null);
            }

            return new Tooltip(
                () => tooltip.TextLines,
                tooltip.VisualMetadata,
                isLong: () => tooltip.IsLong);
        }

        public bool DismissNotification(int index)
        {
            UIButton button = GetNotificationButton(index);
            return button != null && NativeSelectionUtility.PointerRightClick(button);
        }

        public bool IsTownListMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.TownListContainer : null)
                && GetTownListEntryCount() > 0;
        }

        public bool IsTownListEntryVisible(int index)
        {
            return GetTownListEntry(index) != null;
        }

        public string GetTownListEntryLabel(int index)
        {
            ITownListHUDEntry entry = GetTownListEntry(index);
            return entry != null && entry.Town != null ? GetMapEntityName(entry.Town) : string.Empty;
        }

        public void FocusTownListEntry(int index)
        {
            ITownListHUDEntry entry = GetTownListEntry(index);
            if (entry != null)
            {
                NativeSelectionUtility.Select(entry.GetSelectable());
            }
        }

        public bool ClickTownListEntry(int index)
        {
            Selectable selectable = GetTownListEntry(index)?.GetSelectable();
            return selectable != null && NativeSelectionUtility.PointerClick(selectable);
        }

        public Tooltip GetTownListEntryTooltip(int index)
        {
            Selectable selectable = GetTownListEntry(index)?.GetSelectable();
            return Tooltip.ForComponent(selectable, LocalizationHandler);
        }

        public bool IsWielderListMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.WielderlistContainer : null)
                && GetWielderListEntryCount() > 0;
        }

        /// <summary>
        /// Whether the list draws its "Wielders n of m" count. The game only turns the container on
        /// where the team has a wielder cap and more than one wielder it could buy
        /// (<c>WielderListHUD.UpdateOwnedWieldersText</c>), so this is the container's own answer.
        /// </summary>
        public bool IsWielderAmountVisible()
        {
            return IsWielderListMenuVisible()
                && HudGroupVisible(Reflect.Get<GameObject>(WielderList, WielderAmountContainerField));
        }

        /// <summary>The count the list writes over itself, in the game's own words
        /// ("Adventure/CommanderListHUD/WielderAmount").</summary>
        public string WielderAmountLabel
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(WielderList, WielderAmountTextField)); }
        }

        /// <summary>The wielder-limit explanation the game hangs on the count's hover area.</summary>
        public Tooltip WielderAmountTooltip
        {
            get
            {
                return Tooltip.ForComponent(
                    Reflect.Get<UITransform>(WielderList, WielderLimitTooltipAreaField),
                    LocalizationHandler);
            }
        }

        public bool IsWielderListEntryVisible(int index)
        {
            return GetWielderListEntry(index) != null;
        }

        public string GetWielderListEntryLabel(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            return entry != null && entry.Commander != null ? AdventureMapEntityLabel.GetCommanderName(Facade, entry.Commander) : string.Empty;
        }

        public void FocusWielderListEntry(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            if (entry != null)
            {
                RefreshWielderListEntryTooltip(entry);
                NativeSelectionUtility.Select(GetWielderListSelectable(entry));
            }
        }

        public bool ClickWielderListEntry(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            UIButton button = GetWielderListButton(entry);
            return button != null && NativeSelectionUtility.Click(button);
        }

        public Tooltip GetWielderListEntryTooltip(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            Selectable selectable = GetWielderListSelectable(entry);
            if (selectable == null)
            {
                return null;
            }

            return new Tooltip(
                () =>
                {
                    RefreshWielderListEntryTooltip(entry);
                    return NativeTooltipUtility.GetTooltipLinesForComponent(selectable, LocalizationHandler);
                },
                VisualTooltipMetadata.ForComponent(selectable),
                isLong: () => NativeTooltipUtility.IsLongForComponent(selectable, () => RefreshWielderListEntryTooltip(entry)));
        }

        public bool IsOptionsButtonVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.OptionsButtonsContainer : null)
                && MenuButtonAdapterBase.IsButtonDrawn(GetOptionsButton());
        }

        public bool IsOptionsButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetOptionsButton());
        }

        public string OptionsButtonLabel
        {
            get { return TooltipLines.First(OptionsButtonTooltip); }
        }

        public void FocusOptionsButton()
        {
            NativeSelectionUtility.Select(GetOptionsButton());
        }

        public bool ClickOptionsButton()
        {
            return NativeSelectionUtility.Click(GetOptionsButton());
        }

        public Tooltip OptionsButtonTooltip
        {
            get { return Tooltip.ForComponent(GetOptionsButton(), LocalizationHandler); }
        }

        public bool IsKingdomOverviewMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.KingdomOverviewContainer : null)
                && KingdomSettings != null;
        }

        public string GetKingdomOverviewLabel(int index)
        {
            return TooltipLines.First(GetKingdomOverviewTooltip(index));
        }

        public bool IsKingdomOverviewItemVisible(int index)
        {
            return MenuButtonAdapterBase.IsButtonDrawn(GetKingdomOverviewButton(index));
        }

        public bool IsKingdomOverviewItemEnabled(int index)
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetKingdomOverviewButton(index));
        }

        public void FocusKingdomOverviewItem(int index)
        {
            NativeSelectionUtility.Select(GetKingdomOverviewButton(index));
        }

        public bool ClickKingdomOverviewItem(int index)
        {
            return NativeSelectionUtility.Click(GetKingdomOverviewButton(index));
        }

        public Tooltip GetKingdomOverviewTooltip(int index)
        {
            return Tooltip.ForComponent(GetKingdomOverviewButton(index), LocalizationHandler);
        }

        public bool IsBugReportButtonVisible()
        {
            return MenuButtonAdapterBase.IsButtonDrawn(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public bool IsBugReportButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public string BugReportButtonLabel
        {
            get { return TooltipLines.First(BugReportButtonTooltip); }
        }

        public void FocusBugReportButton()
        {
            NativeSelectionUtility.Select(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public bool ClickBugReportButton()
        {
            return NativeSelectionUtility.Click(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public Tooltip BugReportButtonTooltip
        {
            get { return Tooltip.ForComponent(KingdomSettings != null ? KingdomSettings.BugReportButton : null, LocalizationHandler); }
        }

        public bool IsTeamQueueMenuVisible()
        {
            return HudGroupVisible(HudStateSettings != null ? HudStateSettings.TeamQueueContainer : null)
                && GetTeamQueueEntryCount() > 0;
        }

        public bool IsTeamQueueEntryVisible(int index)
        {
            return GetTeamQueueEntry(index) != null;
        }

        public string GetTeamQueueEntryLabel(int index)
        {
            TeamQueueEntryBehaviour entry = GetTeamQueueEntry(index);
            UITextMesh text = Reflect.Get<UITextMesh>(entry, TeamQueueEntryNameTextField);
            return SpokenLines.Clean(UITextMeshTextUtility.Spoken(text));
        }

        public void FocusTeamQueueEntry(int index)
        {
            UIButton button = Reflect.Get<UIButton>(GetTeamQueueEntry(index), TeamQueueEntryTooltipButtonField);
            NativeSelectionUtility.Select(button);
        }

        public Tooltip GetTeamQueueEntryTooltip(int index)
        {
            UIButton button = Reflect.Get<UIButton>(GetTeamQueueEntry(index), TeamQueueEntryTooltipButtonField);
            return Tooltip.ForComponent(button, LocalizationHandler);
        }

        public bool IsEndTurnButtonVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.EndTurnContainer : null)
                && MenuButtonAdapterBase.IsButtonDrawn(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public bool IsEndTurnButtonEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public string EndTurnButtonLabel
        {
            get { return TooltipLines.First(EndTurnButtonTooltip) ?? string.Empty; }
        }

        /// <summary>
        /// Recompose the end-turn button's tooltip the way the game does when the mouse arrives on it.
        /// <c>EndTurnHUD.Tick</c> refreshes the button's interactable state every frame, but its title
        /// ("End turn" against "Hold on...") is only recomposed by <c>UpdateTooltip</c> on mouse-over,
        /// on a click and on round events, so a mouse user never sees it stale while a reader that
        /// names the button by that title and selects it without hovering does (seen 2026-09-08: the
        /// check mark drawn, the tooltip still saying "Hold on..."). This runs the same private
        /// refresh the hover handler runs.
        /// </summary>
        private void RefreshEndTurnTooltip()
        {
            EndTurnHUD hud = EndTurnHud;
            if (hud == null || EndTurnUpdateTooltipMethod == null)
            {
                return;
            }

            try
            {
                EndTurnUpdateTooltipMethod.Invoke(hud, null);
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning("AdventureHudAdapter could not refresh the end-turn tooltip: " + ex.Message);
            }
        }

        public void FocusEndTurnButton()
        {
            NativeSelectionUtility.Select(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public bool ClickEndTurnButton()
        {
            return NativeSelectionUtility.Click(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        /// <summary>The refresh runs when the LINES are read, never when a build asks whether the
        /// button has a tooltip: the map's build passes this tooltip by value every frame, and the
        /// game's private UpdateTooltip is too expensive to run for a tooltip nobody is reading.</summary>
        public Tooltip EndTurnButtonTooltip
        {
            get
            {
                Component button = EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null;
                if (button == null)
                {
                    return null;
                }

                return new Tooltip(
                    () =>
                    {
                        RefreshEndTurnTooltip();
                        return NativeTooltipUtility.GetTooltipLinesForComponent(button, LocalizationHandler);
                    },
                    VisualTooltipMetadata.ForComponent(button),
                    // No composition provoked here, unlike the wielder rows: what this button
                    // composes is PLAIN TEXT, which never lands in _overriddenDetails and so would
                    // be recomposed on every build for an answer that is short whatever it says.
                    isLong: () => NativeTooltipUtility.IsLongForComponent(button));
            }
        }

        public bool IsRoundTextVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.TeamQueueContainer : null)
                && !string.IsNullOrWhiteSpace(RoundTextLabel);
        }

        public string RoundTextLabel
        {
            get { return GetRoundTextLabel(); }
        }

        private DiContainer Container
        {
            get { return _container; }
        }

        private IClientAdventureFacade Facade
        {
            get { return _map != null ? _map.Facade : null; }
        }

        private ISelectionHandler SelectionHandler
        {
            get { return _map != null ? _map.SelectionHandler : null; }
        }

        private ILocalizationHandler LocalizationHandler
        {
            get { return _map != null ? _map.LocalizationHandler : null; }
        }

        private CommanderHUD.Settings CommanderSettings
        {
            get
            {
                if (_commanderHudSettings == null && !_commanderHudSettingsProbed)
                {
                    _commanderHudSettingsProbed = true;
                    _commanderHudSettings = Resolve<CommanderHUD.Settings>();
                    if (_commanderHudSettings == null)
                    {
                        CommanderHUDInstaller installer = FindSameSceneComponent<CommanderHUDInstaller>();
                        _commanderHudSettings = Reflect.Get<CommanderHUD.Settings>(installer, CommanderHudInstallerSettingsField);
                    }
                }

                return _commanderHudSettings;
            }
        }

        private ResourceHUD.Settings ResourceSettings
        {
            get
            {
                if (_resourceHudSettings == null && !_resourceHudSettingsProbed)
                {
                    _resourceHudSettingsProbed = true;
                    _resourceHudSettings = Resolve<ResourceHUD.Settings>();
                    if (_resourceHudSettings == null)
                    {
                        ResourceHUD resourceHud = Resolve<ResourceHUD>();
                        _resourceHudSettings = Reflect.Get<ResourceHUD.Settings>(resourceHud, ResourceHudSettingsField);
                    }

                    if (_resourceHudSettings == null)
                    {
                        ResourceHUDInstaller installer = FindSameSceneComponent<ResourceHUDInstaller>();
                        _resourceHudSettings = Reflect.Get<ResourceHUD.Settings>(installer, ResourceHudInstallerSettingsField);
                    }
                }

                return _resourceHudSettings;
            }
        }

        private AdventureHUDStateHandler.Settings HudStateSettings
        {
            get
            {
                if (_hudStateSettings == null && !_hudStateSettingsProbed)
                {
                    _hudStateSettingsProbed = true;
                    _hudStateSettings = Resolve<AdventureHUDStateHandler.Settings>();
                    if (_hudStateSettings == null)
                    {
                        AdventureHUDStateHandlerInstaller installer = FindSameSceneComponent<AdventureHUDStateHandlerInstaller>();
                        _hudStateSettings = Reflect.Get<AdventureHUDStateHandler.Settings>(installer, AdventureHudStateHandlerInstallerSettingsField);
                    }
                }

                return _hudStateSettings;
            }
        }

        private AdventureHUDStateHandler HudStateHandler
        {
            get
            {
                if (_hudStateHandler == null && !_hudStateHandlerProbed)
                {
                    _hudStateHandlerProbed = true;
                    _hudStateHandler = Resolve<AdventureHUDStateHandler>();
                }

                return _hudStateHandler;
            }
        }

        private ObjectivesHUD ObjectivesHud
        {
            get
            {
                if (_objectivesHud == null && !_objectivesHudProbed)
                {
                    _objectivesHudProbed = true;
                    _objectivesHud = Resolve<ObjectivesHUD>();
                    if (_objectivesHud == null)
                    {
                        _objectivesHud = ResolveFromInstaller<ObjectivesHUD>(ObjectivesInstaller);
                    }
                }

                return _objectivesHud;
            }
        }

        private ObjectivesHUDInstaller ObjectivesInstaller
        {
            get
            {
                if (_objectivesHudInstaller == null && !_objectivesHudInstallerProbed)
                {
                    _objectivesHudInstallerProbed = true;
                    _objectivesHudInstaller = FindSameSceneComponent<ObjectivesHUDInstaller>();
                }

                return _objectivesHudInstaller;
            }
        }

        private ObjectivesHUD.Settings ObjectivesSettings
        {
            get
            {
                if (_objectivesHudSettings == null && !_objectivesHudSettingsProbed)
                {
                    _objectivesHudSettingsProbed = true;
                    _objectivesHudSettings = Resolve<ObjectivesHUD.Settings>();
                    if (_objectivesHudSettings == null)
                    {
                        _objectivesHudSettings = Reflect.Get<ObjectivesHUD.Settings>(ObjectivesInstaller, ObjectivesHudInstallerSettingsField);
                    }
                }

                return _objectivesHudSettings;
            }
        }

        private Transform ObjectivesEntryContainer
        {
            get
            {
                if (_objectivesEntryContainer == null)
                {
                    _objectivesEntryContainer = Reflect.Get<Transform>(ObjectivesInstaller, ObjectivesHudInstallerEntryContainerField);
                }

                return _objectivesEntryContainer;
            }
        }

        private NotificationHUD NotificationHud
        {
            get
            {
                if (_notificationHud == null && !_notificationHudProbed)
                {
                    _notificationHudProbed = true;
                    _notificationHud = Resolve<NotificationHUD>();
                    if (_notificationHud == null)
                    {
                        _notificationHud = ResolveFromInstaller<NotificationHUD>(FindSameSceneComponent<NotificationHUDInstaller>());
                    }
                }

                return _notificationHud;
            }
        }

        private TownListUI TownList
        {
            get
            {
                // TownListUI is bound in TownListHUDInstaller's own container, not the scene's, so the
                // plain resolve throws (caught) and the installer is where it is found. Probed once.
                if (_townListUi == null && !_townListUiProbed)
                {
                    _townListUiProbed = true;
                    _townListUi = Resolve<TownListUI>();
                    if (_townListUi == null)
                    {
                        _townListUi = ResolveFromInstaller<TownListUI>(FindSameSceneComponent<TownListHUDInstaller>());
                    }
                }

                return _townListUi;
            }
        }

        private WielderListHUD WielderList
        {
            get { return HudStateSettings != null ? HudStateSettings.WielderList : null; }
        }

        private KingdomInformationHUD.Settings KingdomSettings
        {
            get
            {
                if (_kingdomInformationSettings == null && !_kingdomInformationSettingsProbed)
                {
                    _kingdomInformationSettingsProbed = true;
                    _kingdomInformationSettings = Resolve<KingdomInformationHUD.Settings>();
                    if (_kingdomInformationSettings == null)
                    {
                        KingdomInformationHUD hud = Resolve<KingdomInformationHUD>();
                        _kingdomInformationSettings = Reflect.Get<KingdomInformationHUD.Settings>(hud, KingdomInformationHudSettingsField);
                    }

                    if (_kingdomInformationSettings == null)
                    {
                        KingdomInformationHUDInstaller installer = FindSameSceneComponent<KingdomInformationHUDInstaller>();
                        _kingdomInformationSettings = Reflect.Get<KingdomInformationHUD.Settings>(installer, KingdomInformationHudInstallerSettingsField);
                    }
                }

                return _kingdomInformationSettings;
            }
        }

        /// <summary>The end-turn HUD object itself, bound in its own installer's container rather than
        /// the scene's (the scene container answers null for it; measured 2026-09-08).</summary>
        private EndTurnHUD EndTurnHud
        {
            get
            {
                if (_endTurnHud == null && !_endTurnHudProbed)
                {
                    _endTurnHudProbed = true;
                    DiContainer container = Reflect.InstallerContainer(FindSameSceneComponent<EndTurnHUDInstaller>());
                    _endTurnHud = container != null ? container.TryResolve<EndTurnHUD>() : null;
                }

                return _endTurnHud;
            }
        }

        private EndTurnHUD.Settings EndTurnSettings
        {
            get
            {
                if (_endTurnSettings == null && !_endTurnSettingsProbed)
                {
                    _endTurnSettingsProbed = true;
                    _endTurnSettings = Resolve<EndTurnHUD.Settings>();
                    if (_endTurnSettings == null)
                    {
                        EndTurnHUDInstaller installer = FindSameSceneComponent<EndTurnHUDInstaller>();
                        _endTurnSettings = Reflect.Get<EndTurnHUD.Settings>(installer, EndTurnHudInstallerSettingsField);
                    }
                }

                return _endTurnSettings;
            }
        }

        private TeamQueueHUDBehaviour TeamQueueHud
        {
            get
            {
                if (_teamQueueHud == null && !_teamQueueHudProbed)
                {
                    _teamQueueHudProbed = true;
                    _teamQueueHud = FindSameSceneComponent<TeamQueueHUDBehaviour>();
                }

                return _teamQueueHud;
            }
        }

        private string GetSelectedCommanderName()
        {
            ICommanderState commander = CommanderSettings != null && CommanderSettings.Portrait != null
                ? CommanderSettings.Portrait.Commander
                : SelectionHandler != null ? SelectionHandler.SelectedCommander : null;
            return AdventureMapEntityLabel.GetCommanderName(Facade, commander);
        }

        private AdventureEssenceContainer GetAdventureEssenceContainer()
        {
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            return Reflect.Get<AdventureEssenceContainer>(portrait, CommanderHudPortraitEssenceContainerField);
        }

        private ExperienceBar GetExperienceBar()
        {
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            return Reflect.Get<ExperienceBar>(portrait, CommanderHudPortraitExperienceBarField);
        }

        private Component GetExperienceTooltipComponent()
        {
            UIImage image = Reflect.Get<UIImage>(GetExperienceBar(), ExperienceBarTooltipImageField);
            return image as Component;
        }

        private UIButton GetLevelUpButton()
        {
            return Reflect.Get<UIButton>(GetExperienceBar(), ExperienceBarLevelUpButtonField);
        }

        private Component GetEssenceTooltipComponent(EssenceType essenceType)
        {
            AdventureEssenceContainer container = GetAdventureEssenceContainer();
            FieldInfo field = GetEssenceTooltipField(essenceType);
            Image image = Reflect.Get<Image>(container, field);
            if (image == null)
            {
                return null;
            }

            UIImage uiImage = ((Component)image).GetComponent<UIImage>();
            return uiImage != null ? (Component)uiImage : image;
        }

        private static FieldInfo GetEssenceTooltipField(EssenceType essenceType)
        {
            switch (essenceType)
            {
                case EssenceType.Order:
                    return EssenceOrderImageField;
                case EssenceType.Creation:
                    return EssenceCreationImageField;
                case EssenceType.Chaos:
                    return EssenceChaosImageField;
                case EssenceType.Arcana:
                    return EssenceArcanaImageField;
                case EssenceType.Destruction:
                    return EssenceDestructionImageField;
                default:
                    return null;
            }
        }

        private int GetSelectedCommanderEssenceAmount(EssenceType essenceType)
        {
            ICommanderState commander = CommanderSettings != null && CommanderSettings.Portrait != null
                ? CommanderSettings.Portrait.Commander
                : SelectionHandler != null ? SelectionHandler.SelectedCommander : null;
            if (commander == null || Facade == null || Facade.Commanders == null)
            {
                return 0;
            }

            try
            {
                return Facade.Commanders.GetTotalEssenceIncome(commander.Id, essenceType);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private string GetEssenceName(EssenceType essenceType)
        {
            return EssenceText.Name(LocalizationHandler, essenceType);
        }

        private TroopHUDEntry GetTroopSlot(int index)
        {
            if (index < 0)
            {
                return null;
            }

            TroopHUD troopHud = CommanderSettings != null ? CommanderSettings.TroopHUD : null;
            List<TroopHUDEntry> entries = Reflect.Get<List<TroopHUDEntry>>(troopHud, TroopHudTroopsField);
            return entries != null && index < entries.Count ? entries[index] : null;
        }

        private UIButton GetMoveToDestinationButton()
        {
            MovementActionButton movementActionButton = CommanderSettings != null ? CommanderSettings.MovementActionButton : null;
            return Reflect.Get<UIButton>(movementActionButton, MovementActionButtonMoveButtonField);
        }

        private ResourceHUD.ResourceEntry GetResourceEntry(ResourceType resourceType)
        {
            ResourceHUD.Settings settings = ResourceSettings;
            ResourceHUD.ResourceEntry[] entries = settings != null ? settings.Resources : null;
            if (entries == null)
            {
                return null;
            }

            for (int i = 0; i < entries.Length; i++)
            {
                ResourceHUD.ResourceEntry entry = entries[i];
                if (entry != null && entry.Type == resourceType)
                {
                    return entry;
                }
            }

            return null;
        }

        private Component GetResourceTooltipComponent(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            return entry != null ? entry.TooltipImage as Component : null;
        }

        private RectTransform GetResourceTooltipAnchor(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            RectTransform incomeGlow = entry != null && entry.IncomeGlow != null ? entry.IncomeGlow.rectTransform : null;
            if (incomeGlow != null)
            {
                return incomeGlow;
            }

            Component amountText = entry != null ? entry.AmountText as Component : null;
            if (amountText != null)
            {
                return amountText.GetComponent<RectTransform>();
            }

            Component tooltipImage = entry != null ? entry.TooltipImage as Component : null;
            return tooltipImage != null ? tooltipImage.GetComponent<RectTransform>() : null;
        }

        private int GetObjectiveEntryCount()
        {
            return GetObjectiveSnapshots().Count;
        }

        private bool IsObjectivesHudVisible()
        {
            if (!IsAdventureHudVisible())
            {
                return false;
            }

            GameObject container = HudStateSettings != null ? HudStateSettings.ObjectivesContainer : null;
            if (HudGroupVisible(container))
            {
                return true;
            }

            ObjectivesHUD.Settings settings = ObjectivesSettings;
            return GameObjects.IsLive(settings != null ? settings.ObjectiveHeader as Component : null)
                || GameObjects.IsLive(ObjectivesEntryContainer as Component);
        }

        private bool IsAdventureHudVisible()
        {
            AdventureHUDStateHandler handler = HudStateHandler;
            return handler == null || handler.IsVisible;
        }

        /// <summary>The objectives as the HUD is drawing them, walked at most once a frame: the map's
        /// build asks whether the menu is visible, then whether each of sixteen slots is, then for
        /// each one's tooltip, which is about seventeen walks of the same subtree for one frame's
        /// worth of unchanged rows.</summary>
        private List<ObjectiveEntrySnapshot> GetObjectiveSnapshots()
        {
            int frame = Time.frameCount;
            if (_objectiveSnapshots != null && _objectiveSnapshotsFrame == frame)
            {
                return _objectiveSnapshots;
            }

            _objectiveSnapshotsFrame = frame;
            _objectiveSnapshots = BuildObjectiveSnapshots();
            return _objectiveSnapshots;
        }

        private List<ObjectiveEntrySnapshot> BuildObjectiveSnapshots()
        {
            Transform container = ObjectivesEntryContainer;
            List<ObjectiveEntrySnapshot> result = new List<ObjectiveEntrySnapshot>();
            if (container == null || !container.gameObject.activeInHierarchy)
            {
                return result;
            }

            Dictionary<UITextMesh, IObjectivesHUDEntry> entriesByText = GetObjectiveEntriesByText();
            UITextMesh[] texts = container.GetComponentsInChildren<UITextMesh>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text != null && GameObjects.IsLive(text) && !string.IsNullOrWhiteSpace(UITextMeshTextUtility.Spoken(text)))
                {
                    IObjectivesHUDEntry entry;
                    entriesByText.TryGetValue(text, out entry);
                    if (entry != null)
                    {
                        result.Add(new ObjectiveEntrySnapshot(text, entry));
                    }
                }
            }

            return result;
        }

        private ObjectiveEntrySnapshot GetObjectiveSnapshot(int index)
        {
            List<ObjectiveEntrySnapshot> snapshots = GetObjectiveSnapshots();
            return index >= 0 && index < snapshots.Count ? snapshots[index] : null;
        }

        private Dictionary<UITextMesh, IObjectivesHUDEntry> GetObjectiveEntriesByText()
        {
            Dictionary<UITextMesh, IObjectivesHUDEntry> result = new Dictionary<UITextMesh, IObjectivesHUDEntry>();
            ObjectivesHUD hud = ObjectivesHud;
            if (hud == null || ObjectivesHudObjectiveEntriesField == null)
            {
                return result;
            }

            object rawEntries = ObjectivesHudObjectiveEntriesField.GetValue(hud);
            IEnumerable<IObjectivesHUDEntry> entries = rawEntries as IEnumerable<IObjectivesHUDEntry>;
            if (entries == null)
            {
                return result;
            }

            foreach (IObjectivesHUDEntry entry in entries)
            {
                UITextMesh text = GetObjectiveEntryText(entry);
                if (text != null && !result.ContainsKey(text))
                {
                    result.Add(text, entry);
                }
            }

            return result;
        }

        private static UITextMesh GetObjectiveEntryText(IObjectivesHUDEntry entry)
        {
            if (entry == null || ObjectivesHudEntrySettingsField == null || ObjectivesHudEntryObjectiveTextField == null)
            {
                return null;
            }

            try
            {
                object settings = ObjectivesHudEntrySettingsField.GetValue(entry);
                return settings != null ? ObjectivesHudEntryObjectiveTextField.GetValue(settings) as UITextMesh : null;
            }
            catch
            {
                return null;
            }
        }

        private int GetNotificationEntryCount()
        {
            List<INotificationHUDEntry> entries = Reflect.Get<List<INotificationHUDEntry>>(NotificationHud, NotificationHudActiveEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private NotificationHUDEntry GetNotificationEntry(int index)
        {
            List<INotificationHUDEntry> entries = Reflect.Get<List<INotificationHUDEntry>>(NotificationHud, NotificationHudActiveEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] as NotificationHUDEntry : null;
        }

        private UIButton GetNotificationButton(int index)
        {
            NotificationHUDEntry.Settings settings = Reflect.Get<NotificationHUDEntry.Settings>(GetNotificationEntry(index), NotificationHudEntrySettingsField);
            return settings == null
                ? null
                : FirstActiveButton(
                    settings.InformationButton,
                    settings.PositiveButton,
                    settings.NegativeButton,
                    settings.ImportantButton,
                    settings.BeaconButton,
                    settings.BeaconOpponentButton);
        }

        private int GetTownListEntryCount()
        {
            List<ITownListHUDEntry> entries = Reflect.Get<List<ITownListHUDEntry>>(TownList, TownListEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private ITownListHUDEntry GetTownListEntry(int index)
        {
            List<ITownListHUDEntry> entries = Reflect.Get<List<ITownListHUDEntry>>(TownList, TownListEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private int GetWielderListEntryCount()
        {
            List<WielderListHUDEntry> entries = GetWielderListEntries();
            return entries != null ? entries.Count : 0;
        }

        private WielderListHUDEntry GetWielderListEntry(int index)
        {
            List<WielderListHUDEntry> entries = GetWielderListEntries();
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        /// <summary>The wielder list's drawn entries, read at most once a frame: the map's build asks
        /// each of thirty-two slots whether it is drawn and then for its tooltip, and the pool's
        /// ActiveEntries property was looked up by name on every one of those calls.</summary>
        private List<WielderListHUDEntry> GetWielderListEntries()
        {
            int frame = Time.frameCount;
            if (_wielderListEntriesFrame == frame)
            {
                return _wielderListEntries;
            }

            _wielderListEntriesFrame = frame;
            _wielderListEntries = ReadWielderListEntries();
            return _wielderListEntries;
        }

        private List<WielderListHUDEntry> ReadWielderListEntries()
        {
            object pool = HudStateSettings != null && HudStateSettings.WielderList != null
                ? WielderListEntryPoolField.GetValue(HudStateSettings.WielderList)
                : null;
            if (pool == null)
            {
                return null;
            }

            if (!_wielderListActiveEntriesProbed)
            {
                _wielderListActiveEntriesProbed = true;
                _wielderListActiveEntriesProperty = AccessTools.Property(pool.GetType(), "ActiveEntries");
            }

            return _wielderListActiveEntriesProperty != null
                ? _wielderListActiveEntriesProperty.GetValue(pool, null) as List<WielderListHUDEntry>
                : null;
        }

        private static Selectable GetWielderListSelectable(WielderListHUDEntry entry)
        {
            if (entry == null)
            {
                return null;
            }

            try
            {
                return entry.Commander != null && entry.Commander.IsAlive
                    ? entry.GetAliveSelectable()
                    : entry.GetDeadSelectable();
            }
            catch
            {
                return null;
            }
        }

        private static UIButton GetWielderListButton(WielderListHUDEntry entry)
        {
            return entry != null && WielderListEntryButtonField != null
                ? WielderListEntryButtonField.GetValue(entry) as UIButton
                : null;
        }

        private static void RefreshWielderListEntryTooltip(WielderListHUDEntry entry)
        {
            if (entry == null || entry.Commander == null || WielderListEntryRefreshTooltipMethod == null)
            {
                return;
            }

            try
            {
                WielderListEntryRefreshTooltipMethod.Invoke(entry, null);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("AdventureHudAdapter failed to refresh wielder list tooltip: " + exception.Message);
            }
        }

        // The map and combat builds both ask whether this button is drawn and for its tooltip, so
        // the walk was paid up to four times a frame. The button is instantiated with the HUD and
        // outlives every page, so it is found once per adapter and the miss is remembered too -
        // the same shape BattleHudAdapter.GetOptionsButton already uses.
        private UIButton GetOptionsButton()
        {
            if (_optionsButton != null || _optionsButtonProbed)
            {
                return _optionsButton;
            }

            _optionsButtonProbed = true;
            GameObject container = HudStateSettings != null ? HudStateSettings.OptionsButtonsContainer : null;
            OptionsButtonInstaller installer = container != null ? container.GetComponentInChildren<OptionsButtonInstaller>(false) : null;
            _optionsButton = installer != null
                ? installer.GetComponent<UIButton>()
                : (container != null ? container.GetComponentInChildren<UIButton>(false) : null);
            return _optionsButton;
        }

        private UIButton GetKingdomOverviewButton(int index)
        {
            KingdomInformationHUD.Settings settings = KingdomSettings;
            if (settings == null)
            {
                return null;
            }

            switch (index)
            {
                case 0:
                    return settings.OwnedEntitiesButton;
                case 1:
                    return settings.TroopIncomeButton;
                case 2:
                    return settings.ResearchButton;
                case 3:
                    return settings.MarketplaceButton;
                case 4:
                    return settings.PlayerButton;
                default:
                    return null;
            }
        }

        private int GetTeamQueueEntryCount()
        {
            List<TeamQueueEntryBehaviour> entries = Reflect.Get<List<TeamQueueEntryBehaviour>>(TeamQueueHud, TeamQueueEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private TeamQueueEntryBehaviour GetTeamQueueEntry(int index)
        {
            List<TeamQueueEntryBehaviour> entries = Reflect.Get<List<TeamQueueEntryBehaviour>>(TeamQueueHud, TeamQueueEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private string GetRoundTextLabel()
        {
            UITextMesh[] texts = Reflect.Get<UITextMesh[]>(TeamQueueHud, TeamQueueRoundTextsField);
            if (texts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || !GameObjects.IsLive(text))
                {
                    continue;
                }

                string label = UITextMeshTextUtility.Spoken(text);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return string.Empty;
        }

        /// <summary>
        /// <see cref="GameObjects.IsGroupVisible(GameObject)"/> over a HUD container, with the
        /// container's canvas group resolved once instead of once per read.
        ///
        /// Every stop of the map's build asks this, up to twelve times a frame between them, and
        /// always of the same fixed containers the HUD's settings object holds for as long as the HUD
        /// lives - which is as long as this adapter does, so the component a container carries cannot
        /// change under the memo. The MISS is remembered too: a container with no canvas group is the
        /// common case and would otherwise cost a GetComponent on every read.
        /// </summary>
        private bool HudGroupVisible(GameObject container)
        {
            if (container == null)
            {
                return false;
            }

            CanvasGroup canvasGroup;
            if (!_canvasGroups.TryGetValue(container, out canvasGroup))
            {
                canvasGroup = container.GetComponent<CanvasGroup>();
                _canvasGroups.Add(container, canvasGroup);
            }

            return GameObjects.IsGroupVisible(container, canvasGroup);
        }

        private T Resolve<T>() where T : class
        {
            return Reflect.Resolve<T>(Container);
        }

        private static T ResolveFromInstaller<T>(MonoInstallerBase installer) where T : class
        {
            return Reflect.Resolve<T>(Reflect.InstallerContainer(installer));
        }

        private T FindSameSceneComponent<T>() where T : Component
        {
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (GameObjects.IsLiveSceneObject(component) && IsSameScene(component))
                {
                    return component;
                }
            }

            return null;
        }

        private bool IsSameScene(Component component)
        {
            Component source = _map != null ? _map.SourceKey as Component : null;
            return source != null
                && component != null
                && source.gameObject.scene.IsValid()
                && component.gameObject.scene.IsValid()
                && source.gameObject.scene == component.gameObject.scene;
        }

        private static void InvokeNoArgs(object instance, MethodInfo method)
        {
            if (instance == null || method == null)
            {
                return;
            }

            try
            {
                method.Invoke(instance, null);
            }
            catch
            {
            }
        }

        private static UIButton FirstActiveButton(params UIButton[] buttons)
        {
            if (buttons == null)
            {
                return null;
            }

            for (int i = 0; i < buttons.Length; i++)
            {
                if (MenuButtonAdapterBase.IsButtonDrawn(buttons[i]))
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private string Localize(string key, string fallback)
        {
            return GameText.Get(LocalizationHandler, key, fallback);
        }

        private string GetMapEntityName(IMapEntity entity)
        {
            return AdventureMapEntityLabel.GetMapEntityName(Facade, SelectionHandler, LocalizationHandler, entity);
        }

        private sealed class ObjectiveEntrySnapshot
        {
            public ObjectiveEntrySnapshot(UITextMesh text, IObjectivesHUDEntry entry)
            {
                Text = text;
                Entry = entry;
            }

            public UITextMesh Text { get; private set; }

            public IObjectivesHUDEntry Entry { get; private set; }

            public bool IsComplete
            {
                get
                {
                    IReadOnlyList<Objective> objectives = Entry != null ? Entry.Objectives : null;
                    if (objectives == null || objectives.Count == 0)
                    {
                        return false;
                    }

                    if (objectives.Count == 1 && objectives[0].currentValue == 0 && objectives[0].maxValue == 0)
                    {
                        return true;
                    }

                    for (int i = 0; i < objectives.Count; i++)
                    {
                        if (objectives[i] == null || objectives[i].progress < 1f)
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }

            public bool CanBeCompleted
            {
                get { return Entry != null && Entry.CanBeCompleted; }
            }

            public bool IsLoseCondition
            {
                get { return Entry != null && Entry.IsLoseCondition; }
            }

        }
    }
}
