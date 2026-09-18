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
    public sealed partial class AdventureHudAdapter
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
        // What LogFailureOnce has already said, so it says each thing once.
        private readonly HashSet<string> _loggedFailures = new HashSet<string>(StringComparer.Ordinal);
        // The canvas group each HUD container carries, resolved once it has one. See HudGroupVisible.
        private readonly Dictionary<GameObject, CanvasGroup> _canvasGroups = new Dictionary<GameObject, CanvasGroup>();
        // The army of the wielder the HUD has selected, kept on the bar it reads. See Troops.
        private TroopHudAdapter _troops;
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

        private bool IsAdventureHudVisible()
        {
            AdventureHUDStateHandler handler = HudStateHandler;
            return handler == null || handler.IsVisible;
        }

        /// <summary>
        /// <see cref="GameObjects.IsGroupVisible(GameObject)"/> over a HUD container, with the
        /// container's canvas group resolved once instead of once per read.
        ///
        /// Every stop of the map's build asks this, up to twelve times a frame between them, and
        /// always of the same fixed containers the HUD's settings object holds for as long as the HUD
        /// lives - which is as long as this adapter does, so the component a container carries cannot
        /// change under the memo.
        ///
        /// Only a HIT is remembered. The game ADDS the canvas group on the container's first toggle
        /// (<c>AdventureHUDStateHandler.SetHudObjectState</c>) and hides the container by fading that
        /// group rather than by deactivating it, so a container probed before the HUD's first state
        /// change has no group YET, and a remembered miss would answer "visible" for a panel faded to
        /// nothing for the rest of the adventure - through a cutscene, through the AI's turn. A miss
        /// costs one <c>GetComponent</c> on one object per read, which is what a container that has
        /// never been toggled is worth.
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
                if (canvasGroup != null)
                {
                    _canvasGroups.Add(container, canvasGroup);
                }
            }

            return GameObjects.IsGroupVisible(container, canvasGroup);
        }

        /// <summary>A guarded game call that threw, reported once per adapter instance and per
        /// subject; the map's build asks most of these on every frame, so a warning per failure would
        /// bury the log. Per adapter, so it needs no reset: the adapter dies with the adventure.
        /// </summary>
        private void LogFailureOnce(string subject, Exception exception)
        {
            if (!_loggedFailures.Add(subject))
            {
                return;
            }

            SocAccessMod.Instance?.LogWarning("AdventureHudAdapter: " + subject + " threw: " + exception);
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

        private void InvokeNoArgs(object instance, MethodInfo method)
        {
            if (instance == null || method == null)
            {
                return;
            }

            try
            {
                method.Invoke(instance, null);
            }
            catch (Exception exception)
            {
                LogFailureOnce("invoking " + method.Name + " on the objectives panel", exception);
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

    }
}
