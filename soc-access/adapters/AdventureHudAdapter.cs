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
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class AdventureHudAdapter
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
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(ObjectivesHUDInstaller), "Container");
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
        private KingdomInformationHUD.Settings _kingdomInformationSettings;
        private EndTurnHUD.Settings _endTurnSettings;
        private TeamQueueHUDBehaviour _teamQueueHud;

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
                && IsGameObjectVisible(settings.CommanderContainer as Component)
                && settings.Portrait != null
                && settings.Portrait.Commander != null;
        }

        public bool IsExperienceVisible()
        {
            return IsSelectionHudVisible() && IsGameObjectVisible(GetExperienceBar());
        }

        public string ExperienceLabel
        {
            get
            {
                CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
                ICommanderState commander = portrait != null ? portrait.Commander : null;
                if (commander == null || commander.Stats == null)
                {
                    return Localize("Commanders/Tooltip/Experience", "Experience");
                }

                int level = commander.GetLevel();
                string levelText = GetText(GetField<UITextMesh>(GetExperienceBar(), ExperienceBarLevelTextField));
                int parsedLevel;
                if (!string.IsNullOrWhiteSpace(levelText) && int.TryParse(levelText, out parsedLevel))
                {
                    level = parsedLevel;
                }

                int current = commander.Stats.Experience;
                int nextLevelExperience = CommanderLevelUtility.GetExperienceForLevel(level + 1);
                return Localize("Commanders/Tooltip/Experience", "Experience")
                    + ", level "
                    + level
                    + ", "
                    + current
                    + " / "
                    + nextLevelExperience;
            }
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
            return IsSelectionHudVisible() && IsButtonVisible(GetLevelUpButton());
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
            return IsButtonInteractable(GetLevelUpButton());
        }

        public bool IsEssenceMenuVisible()
        {
            return IsSelectionHudVisible() && IsGameObjectVisible(GetAdventureEssenceContainer() as Component);
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
            if (!IsSelectionHudVisible() || !IsGameObjectVisible(troopHud))
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
            return entry != null && entry.IsUnlocked && IsGameObjectVisible(entry);
        }

        public void FocusTroopSlot(int index)
        {
            TroopHUDEntry entry = GetTroopSlot(index);
            if (entry != null)
            {
                NativeSelectionUtility.Select(entry.GetSelectable());
            }
        }

        public Tooltip GetTroopSlotTooltip(int index)
        {
            return BuildTroopSlotTooltip(GetTroopSlot(index));
        }

        public bool IsInventoryButtonVisible()
        {
            return IsSelectionHudVisible() && IsButtonVisible(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public string InventoryButtonLabel
        {
            get { return Localize("Adventure/HUD/InventoryButton", "Inventory"); }
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
            return IsButtonInteractable(CommanderSettings != null ? CommanderSettings.InventoryButton : null);
        }

        public Tooltip InventoryButtonTooltip
        {
            get { return Tooltip.ForComponent(CommanderSettings != null ? CommanderSettings.InventoryButton : null, LocalizationHandler); }
        }

        public bool IsMoveToDestinationButtonVisible()
        {
            return IsSelectionHudVisible() && IsButtonVisible(GetMoveToDestinationButton());
        }

        public bool IsMoveToDestinationButtonEnabled()
        {
            return IsButtonInteractable(GetMoveToDestinationButton());
        }

        public string MoveToDestinationButtonLabel
        {
            get { return FirstNonEmpty(GetFirstTooltipLine(MoveToDestinationButtonTooltip), "Move to destination"); }
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
            return IsSelectionHudVisible() && IsButtonVisible(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
        }

        public string SpellbookButtonLabel
        {
            get { return Localize("Common/HUD/SpellbookButton", "Spellbook"); }
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
            return IsButtonInteractable(CommanderSettings != null ? CommanderSettings.SpellbookButton : null);
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

        public string GetResourceLabel(ResourceType resourceType)
        {
            ResourceHUD.ResourceEntry entry = GetResourceEntry(resourceType);
            string name = GetResourceName(resourceType);
            string amount = GetText(entry != null ? entry.AmountText : null);
            string income = entry != null && IsGameObjectVisible(entry.IncomeText)
                ? GetText(entry.IncomeText)
                : string.Empty;

            string label = string.IsNullOrWhiteSpace(amount) ? name : name + " " + amount;
            return string.IsNullOrWhiteSpace(income) ? label : label + ", income " + income;
        }

        public int GetResourceAmount(ResourceType resourceType)
        {
            ITeamState team = Facade != null && Facade.Teams != null ? Facade.Teams.LocalTeamInControl : null;
            Resource resource = team != null && team.Resources != null ? team.Resources.GetResource(resourceType) : null;
            return resource != null ? resource.Amount : 0;
        }

        public string GetResourceName(ResourceType resourceType)
        {
            return Localize("Common/Resource/" + resourceType, FormatEnumName(resourceType.ToString()));
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

        public string GetObjectiveLabel(int index)
        {
            ObjectiveEntrySnapshot snapshot = GetObjectiveSnapshot(index);
            if (snapshot == null)
            {
                return string.Empty;
            }

            string label = GetText(snapshot.Text);
            if (string.IsNullOrWhiteSpace(label))
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            if (snapshot.IsLoseCondition)
            {
                parts.Add("Lose condition");
            }
            else
            {
                parts.Add(snapshot.IsComplete ? "Completed" : "Incomplete");
                if (!snapshot.CanBeCompleted)
                {
                    parts.Add("Cannot be completed");
                }
            }
            parts.Add(label);
            return string.Join(", ", parts.ToArray());
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

            return SpeechTextSanitizer.Normalize(entry.Information.Text);
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
                    null,
                    new[] { new TooltipAction("Dismiss", () => DismissNotification(index)) });
            }

            return new Tooltip(
                () => tooltip.TextLines,
                tooltip.VisualMetadata,
                new[] { new TooltipAction("Dismiss", () => DismissNotification(index)) });
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

        public bool IsWielderListEntryVisible(int index)
        {
            return GetWielderListEntry(index) != null;
        }

        public string GetWielderListEntryLabel(int index)
        {
            WielderListHUDEntry entry = GetWielderListEntry(index);
            return entry != null && entry.Commander != null ? GetCommanderName(entry.Commander) : string.Empty;
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
                VisualTooltipMetadata.ForComponent(selectable));
        }

        public bool IsOptionsButtonVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.OptionsButtonsContainer : null)
                && IsButtonVisible(GetOptionsButton());
        }

        public bool IsOptionsButtonEnabled()
        {
            return IsButtonInteractable(GetOptionsButton());
        }

        public string OptionsButtonLabel
        {
            get { return FirstNonEmpty(GetFirstTooltipLine(OptionsButtonTooltip), "Options"); }
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
            return FirstNonEmpty(GetFirstTooltipLine(GetKingdomOverviewTooltip(index)), GetKingdomOverviewFallbackLabel(index));
        }

        public bool IsKingdomOverviewItemVisible(int index)
        {
            return IsButtonVisible(GetKingdomOverviewButton(index));
        }

        public bool IsKingdomOverviewItemEnabled(int index)
        {
            return IsButtonInteractable(GetKingdomOverviewButton(index));
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
            return IsButtonVisible(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public bool IsBugReportButtonEnabled()
        {
            return IsButtonInteractable(KingdomSettings != null ? KingdomSettings.BugReportButton : null);
        }

        public string BugReportButtonLabel
        {
            get { return FirstNonEmpty(GetFirstTooltipLine(BugReportButtonTooltip), "Bug report"); }
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
            UITextMesh text = GetField<UITextMesh>(entry, TeamQueueEntryNameTextField);
            return SpeechTextSanitizer.Normalize(GetText(text));
        }

        public void FocusTeamQueueEntry(int index)
        {
            UIButton button = GetField<UIButton>(GetTeamQueueEntry(index), TeamQueueEntryTooltipButtonField);
            NativeSelectionUtility.Select(button);
        }

        public Tooltip GetTeamQueueEntryTooltip(int index)
        {
            UIButton button = GetField<UIButton>(GetTeamQueueEntry(index), TeamQueueEntryTooltipButtonField);
            return Tooltip.ForComponent(button, LocalizationHandler);
        }

        public bool IsEndTurnButtonVisible()
        {
            return IsAdventureHudVisible()
                && HudGroupVisible(HudStateSettings != null ? HudStateSettings.EndTurnContainer : null)
                && IsButtonVisible(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public bool IsEndTurnButtonEnabled()
        {
            return IsButtonInteractable(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public string EndTurnButtonLabel
        {
            get { return GetFirstTooltipLine(EndTurnButtonTooltip) ?? string.Empty; }
        }

        public void FocusEndTurnButton()
        {
            NativeSelectionUtility.Select(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public bool ClickEndTurnButton()
        {
            return NativeSelectionUtility.Click(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null);
        }

        public Tooltip EndTurnButtonTooltip
        {
            get { return Tooltip.ForComponent(EndTurnSettings != null ? EndTurnSettings.EndTurnButton : null, LocalizationHandler); }
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
                if (_commanderHudSettings == null)
                {
                    _commanderHudSettings = Resolve<CommanderHUD.Settings>();
                    if (_commanderHudSettings == null)
                    {
                        CommanderHUDInstaller installer = FindSameSceneComponent<CommanderHUDInstaller>();
                        _commanderHudSettings = GetField<CommanderHUD.Settings>(installer, CommanderHudInstallerSettingsField);
                    }
                }

                return _commanderHudSettings;
            }
        }

        private ResourceHUD.Settings ResourceSettings
        {
            get
            {
                if (_resourceHudSettings == null)
                {
                    _resourceHudSettings = Resolve<ResourceHUD.Settings>();
                    if (_resourceHudSettings == null)
                    {
                        ResourceHUD resourceHud = Resolve<ResourceHUD>();
                        _resourceHudSettings = GetField<ResourceHUD.Settings>(resourceHud, ResourceHudSettingsField);
                    }

                    if (_resourceHudSettings == null)
                    {
                        ResourceHUDInstaller installer = FindSameSceneComponent<ResourceHUDInstaller>();
                        _resourceHudSettings = GetField<ResourceHUD.Settings>(installer, ResourceHudInstallerSettingsField);
                    }
                }

                return _resourceHudSettings;
            }
        }

        private AdventureHUDStateHandler.Settings HudStateSettings
        {
            get
            {
                if (_hudStateSettings == null)
                {
                    _hudStateSettings = Resolve<AdventureHUDStateHandler.Settings>();
                    if (_hudStateSettings == null)
                    {
                        AdventureHUDStateHandlerInstaller installer = FindSameSceneComponent<AdventureHUDStateHandlerInstaller>();
                        _hudStateSettings = GetField<AdventureHUDStateHandler.Settings>(installer, AdventureHudStateHandlerInstallerSettingsField);
                    }
                }

                return _hudStateSettings;
            }
        }

        private AdventureHUDStateHandler HudStateHandler
        {
            get
            {
                if (_hudStateHandler == null)
                {
                    _hudStateHandler = Resolve<AdventureHUDStateHandler>();
                }

                return _hudStateHandler;
            }
        }

        private ObjectivesHUD ObjectivesHud
        {
            get
            {
                if (_objectivesHud == null)
                {
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
                if (_objectivesHudInstaller == null)
                {
                    _objectivesHudInstaller = FindSameSceneComponent<ObjectivesHUDInstaller>();
                }

                return _objectivesHudInstaller;
            }
        }

        private ObjectivesHUD.Settings ObjectivesSettings
        {
            get
            {
                if (_objectivesHudSettings == null)
                {
                    _objectivesHudSettings = Resolve<ObjectivesHUD.Settings>();
                    if (_objectivesHudSettings == null)
                    {
                        _objectivesHudSettings = GetField<ObjectivesHUD.Settings>(ObjectivesInstaller, ObjectivesHudInstallerSettingsField);
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
                    _objectivesEntryContainer = GetField<Transform>(ObjectivesInstaller, ObjectivesHudInstallerEntryContainerField);
                }

                return _objectivesEntryContainer;
            }
        }

        private NotificationHUD NotificationHud
        {
            get
            {
                if (_notificationHud == null)
                {
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
                if (_townListUi == null)
                {
                    _townListUi = Resolve<TownListUI>();
                }

                return _townListUi;
            }
        }

        private KingdomInformationHUD.Settings KingdomSettings
        {
            get
            {
                if (_kingdomInformationSettings == null)
                {
                    _kingdomInformationSettings = Resolve<KingdomInformationHUD.Settings>();
                    if (_kingdomInformationSettings == null)
                    {
                        KingdomInformationHUD hud = Resolve<KingdomInformationHUD>();
                        _kingdomInformationSettings = GetField<KingdomInformationHUD.Settings>(hud, KingdomInformationHudSettingsField);
                    }

                    if (_kingdomInformationSettings == null)
                    {
                        KingdomInformationHUDInstaller installer = FindSameSceneComponent<KingdomInformationHUDInstaller>();
                        _kingdomInformationSettings = GetField<KingdomInformationHUD.Settings>(installer, KingdomInformationHudInstallerSettingsField);
                    }
                }

                return _kingdomInformationSettings;
            }
        }

        private EndTurnHUD.Settings EndTurnSettings
        {
            get
            {
                if (_endTurnSettings == null)
                {
                    _endTurnSettings = Resolve<EndTurnHUD.Settings>();
                    if (_endTurnSettings == null)
                    {
                        EndTurnHUDInstaller installer = FindSameSceneComponent<EndTurnHUDInstaller>();
                        _endTurnSettings = GetField<EndTurnHUD.Settings>(installer, EndTurnHudInstallerSettingsField);
                    }
                }

                return _endTurnSettings;
            }
        }

        private TeamQueueHUDBehaviour TeamQueueHud
        {
            get
            {
                if (_teamQueueHud == null)
                {
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
            return GetCommanderName(commander);
        }

        private AdventureEssenceContainer GetAdventureEssenceContainer()
        {
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            return GetField<AdventureEssenceContainer>(portrait, CommanderHudPortraitEssenceContainerField);
        }

        private ExperienceBar GetExperienceBar()
        {
            CommanderHUDPortrait portrait = CommanderSettings != null ? CommanderSettings.Portrait : null;
            return GetField<ExperienceBar>(portrait, CommanderHudPortraitExperienceBarField);
        }

        private Component GetExperienceTooltipComponent()
        {
            UIImage image = GetField<UIImage>(GetExperienceBar(), ExperienceBarTooltipImageField);
            return image as Component;
        }

        private UIButton GetLevelUpButton()
        {
            return GetField<UIButton>(GetExperienceBar(), ExperienceBarLevelUpButtonField);
        }

        private Component GetEssenceTooltipComponent(EssenceType essenceType)
        {
            AdventureEssenceContainer container = GetAdventureEssenceContainer();
            FieldInfo field = GetEssenceTooltipField(essenceType);
            Image image = GetField<Image>(container, field);
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
            return Localize("Units/Types/" + essenceType, FormatEnumName(essenceType.ToString()));
        }

        private TroopHUDEntry GetTroopSlot(int index)
        {
            if (index < 0)
            {
                return null;
            }

            TroopHUD troopHud = CommanderSettings != null ? CommanderSettings.TroopHUD : null;
            List<TroopHUDEntry> entries = GetField<List<TroopHUDEntry>>(troopHud, TroopHudTroopsField);
            return entries != null && index < entries.Count ? entries[index] : null;
        }

        private UIButton GetMoveToDestinationButton()
        {
            MovementActionButton movementActionButton = CommanderSettings != null ? CommanderSettings.MovementActionButton : null;
            return GetField<UIButton>(movementActionButton, MovementActionButtonMoveButtonField);
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
            return IsGameObjectVisible(settings != null ? settings.ObjectiveHeader as Component : null)
                || IsGameObjectVisible(ObjectivesEntryContainer as Component);
        }

        private bool IsAdventureHudVisible()
        {
            AdventureHUDStateHandler handler = HudStateHandler;
            return handler == null || handler.IsVisible;
        }

        private List<ObjectiveEntrySnapshot> GetObjectiveSnapshots()
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
                if (text != null && IsGameObjectVisible(text) && !string.IsNullOrWhiteSpace(GetText(text)))
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
            List<INotificationHUDEntry> entries = GetField<List<INotificationHUDEntry>>(NotificationHud, NotificationHudActiveEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private NotificationHUDEntry GetNotificationEntry(int index)
        {
            List<INotificationHUDEntry> entries = GetField<List<INotificationHUDEntry>>(NotificationHud, NotificationHudActiveEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] as NotificationHUDEntry : null;
        }

        private UIButton GetNotificationButton(int index)
        {
            NotificationHUDEntry.Settings settings = GetField<NotificationHUDEntry.Settings>(GetNotificationEntry(index), NotificationHudEntrySettingsField);
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
            List<ITownListHUDEntry> entries = GetField<List<ITownListHUDEntry>>(TownList, TownListEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private ITownListHUDEntry GetTownListEntry(int index)
        {
            List<ITownListHUDEntry> entries = GetField<List<ITownListHUDEntry>>(TownList, TownListEntriesField);
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

        private List<WielderListHUDEntry> GetWielderListEntries()
        {
            object pool = HudStateSettings != null && HudStateSettings.WielderList != null
                ? WielderListEntryPoolField.GetValue(HudStateSettings.WielderList)
                : null;
            if (pool == null)
            {
                return null;
            }

            PropertyInfo property = AccessTools.Property(pool.GetType(), "ActiveEntries");
            return property != null ? property.GetValue(pool, null) as List<WielderListHUDEntry> : null;
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
                SocAccessPlugin.Instance?.LogWarning("AdventureHudAdapter failed to refresh wielder list tooltip: " + exception.Message);
            }
        }

        private UIButton GetOptionsButton()
        {
            GameObject container = HudStateSettings != null ? HudStateSettings.OptionsButtonsContainer : null;
            OptionsButtonInstaller installer = container != null ? container.GetComponentInChildren<OptionsButtonInstaller>(false) : null;
            if (installer != null)
            {
                return installer.GetComponent<UIButton>();
            }

            return container != null ? container.GetComponentInChildren<UIButton>(false) : null;
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

        private static string GetKingdomOverviewFallbackLabel(int index)
        {
            switch (index)
            {
                case 0:
                    return "Owned entities";
                case 1:
                    return "Troop income";
                case 2:
                    return "Research";
                case 3:
                    return "Marketplace";
                case 4:
                    return "Player";
                default:
                    return string.Empty;
            }
        }

        private int GetTeamQueueEntryCount()
        {
            List<TeamQueueEntryBehaviour> entries = GetField<List<TeamQueueEntryBehaviour>>(TeamQueueHud, TeamQueueEntriesField);
            return entries != null ? entries.Count : 0;
        }

        private TeamQueueEntryBehaviour GetTeamQueueEntry(int index)
        {
            List<TeamQueueEntryBehaviour> entries = GetField<List<TeamQueueEntryBehaviour>>(TeamQueueHud, TeamQueueEntriesField);
            return entries != null && index >= 0 && index < entries.Count ? entries[index] : null;
        }

        private Tooltip BuildTroopSlotTooltip(TroopHUDEntry entry)
        {
            Tooltip tooltip = Tooltip.ForComponent(entry, LocalizationHandler);
            AdventureTroopDetails details = entry != null ? entry.TroopDetails : null;
            if (tooltip == null || details == null || !details.ShowDisbandInstruction || !details.CanDisband)
            {
                return tooltip;
            }

            string disbandLine = Localize("Adventure/TroopHUD/DisbandInstruction", "Disband Troop");
            List<string> instructionLines = new List<string> { disbandLine };
            List<TooltipAction> actions = new List<TooltipAction>
            {
                new TooltipAction(disbandLine, () => InvokeTroopRightClick(entry))
            };

            return new Tooltip(() => RemoveExactLines(tooltip.TextLines, instructionLines), tooltip.VisualMetadata, actions);
        }

        private static bool InvokeTroopRightClick(TroopHUDEntry entry)
        {
            if (entry == null || entry.OnRightClick == null)
            {
                return false;
            }

            entry.OnRightClick(entry);
            return true;
        }

        private string GetRoundTextLabel()
        {
            UITextMesh[] texts = GetField<UITextMesh[]>(TeamQueueHud, TeamQueueRoundTextsField);
            if (texts == null)
            {
                return string.Empty;
            }

            for (int i = 0; i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || !IsGameObjectVisible(text))
                {
                    continue;
                }

                string label = GetText(text);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label;
                }
            }

            return string.Empty;
        }

        private T Resolve<T>() where T : class
        {
            if (Container == null)
            {
                return null;
            }

            try
            {
                return Container.Resolve<T>();
            }
            catch
            {
                return null;
            }
        }

        private static T ResolveFromInstaller<T>(object installer) where T : class
        {
            DiContainer container = GetInstallerContainer(installer);
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch
            {
                return null;
            }
        }

        private static DiContainer GetInstallerContainer(object installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            try
            {
                return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            }
            catch
            {
                return null;
            }
        }

        private T FindSameSceneComponent<T>() where T : Component
        {
            T[] components = Resources.FindObjectsOfTypeAll<T>();
            for (int i = 0; i < components.Length; i++)
            {
                T component = components[i];
                if (IsLiveSceneComponent(component) && IsSameScene(component))
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

        private static bool IsLiveSceneComponent(Component component)
        {
            return component != null
                && component.gameObject != null
                && component.gameObject.scene.IsValid()
                && component.gameObject.scene.isLoaded;
        }

        private static T GetField<T>(object instance, FieldInfo field) where T : class
        {
            return instance != null && field != null ? field.GetValue(instance) as T : null;
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
                if (IsButtonVisible(buttons[i]))
                {
                    return buttons[i];
                }
            }

            return null;
        }

        private static bool IsButtonInteractable(UIButton button)
        {
            return IsButtonVisible(button) && button.Interactable;
        }

        private string Localize(string key, string fallback)
        {
            string localized = Localize(key);
            return string.IsNullOrWhiteSpace(localized) ? fallback : localized;
        }

        private string Localize(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || LocalizationHandler == null)
            {
                return string.Empty;
            }

            try
            {
                string text = LocalizationHandler.GetText(key);
                return string.IsNullOrWhiteSpace(text) || text == key ? string.Empty : text;
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private static string GetText(UITextMesh text)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
        }

        private static bool IsButtonVisible(UIButton button)
        {
            if (button == null || !button.Active)
            {
                return false;
            }

            GameObject gameObject = ((Component)button).gameObject;
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static bool IsGameObjectVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool HudGroupVisible(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return false;
            }

            if (!gameObject.activeInHierarchy)
            {
                return false;
            }

            CanvasGroup canvasGroup = gameObject.GetComponent<CanvasGroup>();
            if (canvasGroup == null)
            {
                return true;
            }

            return canvasGroup.alpha > 0.01f && canvasGroup.interactable && canvasGroup.blocksRaycasts;
        }

        private static string GetFirstTooltipLine(Tooltip tooltip)
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

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback : preferred;
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

        private string GetCommanderName(ICommanderState commander)
        {
            if (commander == null || Facade == null || Facade.Commanders == null)
            {
                return string.Empty;
            }

            try
            {
                return Facade.Commanders.GetName(commander.Id);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        private string GetMapEntityName(IMapEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string customNameKey = string.Empty;
            if (entity.TryGetCustomNameKey(out customNameKey))
            {
                string customName = Localize(customNameKey);
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    return customName;
                }
            }

            string localizedName = Localize(entity.NameKey);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return localizedName;
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return entity.Name;
            }

            return entity.NameKey;
        }

        private static string FormatEnumName(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>(value.Length + 4);
            for (int i = 0; i < value.Length; i++)
            {
                char current = value[i];
                if (i > 0 && char.IsUpper(current) && !char.IsUpper(value[i - 1]) && value[i - 1] != ' ')
                {
                    chars.Add(' ');
                }

                chars.Add(current);
            }

            string formatted = new string(chars.ToArray()).Trim();
            if (formatted.Length == 0)
            {
                return string.Empty;
            }

            return char.ToUpperInvariant(formatted[0]) + (formatted.Length > 1 ? formatted.Substring(1) : string.Empty);
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
