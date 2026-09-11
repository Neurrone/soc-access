using System;
using System.Collections.Generic;
using Lavapotion.Networking;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Adventure;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Events
{
    public sealed class AdventureMapEventListener
    {
        private readonly IClientAdventureFacade _facade;
        private readonly ISelectionHandler _selectionHandler;
        private readonly IHumanAdventureControllerFacade _humanAdventureControllerFacade;
        private readonly ILocalizationHandler _localizationHandler;
        private readonly IFogManager _fogManager;
        private readonly AdventureMapRevealedRegistry _revealedRegistry;
        private readonly Dictionary<int, bool> _lastVisibleNonLocalCommanders = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _announcedVisibleNonLocalCommanders = new Dictionary<int, bool>();
        private readonly PendingAnnouncementLedger _pendingDiscoveries = new PendingAnnouncementLedger();
        private readonly PendingAnnouncementLedger _pendingHiddenWielders = new PendingAnnouncementLedger();
        private readonly HashSet<int> _discoveredMapEntityIds = new HashSet<int>();
        private readonly Dictionary<int, string> _discoveredMapEntityLabelsById = new Dictionary<int, string>();
        private readonly HashSet<int> _knownLocalCommanderIds = new HashSet<int>();
        private readonly HashSet<int> _seenNonLocalCommanderIds = new HashSet<int>();
        private readonly List<int> _staleNonLocalCommanderIds = new List<int>();
        private int _lastExplorationLength = -1;
        private long _lastDiscoverySweepKey;
        private bool _hasDiscoverySweepKey;
        private bool _attached;

        public AdventureMapEventListener(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            IHumanAdventureControllerFacade humanAdventureControllerFacade,
            ILocalizationHandler localizationHandler,
            IFogManager fogManager,
            AdventureMapRevealedRegistry revealedRegistry = null)
        {
            _facade = facade;
            _selectionHandler = selectionHandler;
            _humanAdventureControllerFacade = humanAdventureControllerFacade;
            _localizationHandler = localizationHandler;
            _fogManager = fogManager;
            _revealedRegistry = revealedRegistry;
        }

        /// <summary>Raised for every change the map listens to, before the listener decides whether the
        /// change is worth announcing. <see cref="Screens.AdventureMapScreen"/> hangs its cached cursor
        /// tile off it, so nothing the game changes under a still cursor is read from the cache.</summary>
        public Action OnMapChanged;

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

            CaptureKnownLocalCommanders();

            if (_selectionHandler != null)
            {
                _selectionHandler.OnCommanderChanged =
                    (Action<CommanderChangedPayload>)Delegate.Combine(
                        _selectionHandler.OnCommanderChanged,
                        new Action<CommanderChangedPayload>(HandleCommanderChanged));
                _selectionHandler.OnMapEntityChanged =
                    (Action<MapEntityChangedPayload>)Delegate.Combine(
                        _selectionHandler.OnMapEntityChanged,
                        new Action<MapEntityChangedPayload>(HandleMapEntityChanged));
            }

            if (_humanAdventureControllerFacade != null)
            {
                _humanAdventureControllerFacade.OnDestinationSet =
                    (Action<int>)Delegate.Combine(
                        _humanAdventureControllerFacade.OnDestinationSet,
                        new Action<int>(HandleDestinationSet));
            }

            if (_facade != null && _facade.Commands != null)
            {
                _facade.Commands.OnCommand =
                    (Action<ICommandResponse>)Delegate.Combine(
                        _facade.Commands.OnCommand,
                        new Action<ICommandResponse>(HandleCommand));
                _facade.Commands.OnCommanderMoved =
                    (Action<OnCommanderMovedPayload>)Delegate.Combine(
                        _facade.Commands.OnCommanderMoved,
                        new Action<OnCommanderMovedPayload>(HandleCommanderMoved));
                _facade.Commands.OnCommanderTeleported =
                    (Action<TeleportCommanderCommand.Response>)Delegate.Combine(
                        _facade.Commands.OnCommanderTeleported,
                        new Action<TeleportCommanderCommand.Response>(HandleCommanderTeleported));
                _facade.Commands.OnMapEntityCreated =
                    (Action<int>)Delegate.Combine(
                        _facade.Commands.OnMapEntityCreated,
                        new Action<int>(HandleMapEntityCreated));
            }

            if (_fogManager != null)
            {
                _fogManager.onFogUpdated =
                    (Action)Delegate.Combine(
                        _fogManager.onFogUpdated,
                        new Action(HandleFogUpdated));
            }

            CaptureDiscoveryBaseline();
            _attached = true;
        }

        public void Detach()
        {
            if (!_attached)
            {
                return;
            }

            if (_selectionHandler != null)
            {
                _selectionHandler.OnCommanderChanged =
                    (Action<CommanderChangedPayload>)Delegate.Remove(
                        _selectionHandler.OnCommanderChanged,
                        new Action<CommanderChangedPayload>(HandleCommanderChanged));
                _selectionHandler.OnMapEntityChanged =
                    (Action<MapEntityChangedPayload>)Delegate.Remove(
                        _selectionHandler.OnMapEntityChanged,
                        new Action<MapEntityChangedPayload>(HandleMapEntityChanged));
            }

            if (_humanAdventureControllerFacade != null)
            {
                _humanAdventureControllerFacade.OnDestinationSet =
                    (Action<int>)Delegate.Remove(
                        _humanAdventureControllerFacade.OnDestinationSet,
                        new Action<int>(HandleDestinationSet));
            }

            if (_facade != null && _facade.Commands != null)
            {
                _facade.Commands.OnCommand =
                    (Action<ICommandResponse>)Delegate.Remove(
                        _facade.Commands.OnCommand,
                        new Action<ICommandResponse>(HandleCommand));
                _facade.Commands.OnCommanderMoved =
                    (Action<OnCommanderMovedPayload>)Delegate.Remove(
                        _facade.Commands.OnCommanderMoved,
                        new Action<OnCommanderMovedPayload>(HandleCommanderMoved));
                _facade.Commands.OnCommanderTeleported =
                    (Action<TeleportCommanderCommand.Response>)Delegate.Remove(
                        _facade.Commands.OnCommanderTeleported,
                        new Action<TeleportCommanderCommand.Response>(HandleCommanderTeleported));
                _facade.Commands.OnMapEntityCreated =
                    (Action<int>)Delegate.Remove(
                        _facade.Commands.OnMapEntityCreated,
                        new Action<int>(HandleMapEntityCreated));
            }

            if (_fogManager != null)
            {
                _fogManager.onFogUpdated =
                    (Action)Delegate.Remove(
                        _fogManager.onFogUpdated,
                        new Action(HandleFogUpdated));
            }

            ClearPendingDiscoveries();
            _lastExplorationLength = -1;
            _hasDiscoverySweepKey = false;
            _lastVisibleNonLocalCommanders.Clear();
            _announcedVisibleNonLocalCommanders.Clear();
            _discoveredMapEntityIds.Clear();
            _discoveredMapEntityLabelsById.Clear();
            _knownLocalCommanderIds.Clear();
            OnMapChanged = null;
            _attached = false;
        }

        public void Update()
        {
            FlushPendingDiscoveriesIfReady();
        }

        private void CaptureKnownLocalCommanders()
        {
            _knownLocalCommanderIds.Clear();

            if (_facade == null || _facade.Commanders == null)
            {
                return;
            }

            IEnumerable<ICommanderState> commanders = _facade.Commanders.All;
            if (commanders == null)
            {
                return;
            }

            foreach (ICommanderState commander in commanders)
            {
                if (IsLocalCommander(commander))
                {
                    _knownLocalCommanderIds.Add(commander.Id);
                }
            }
        }

        private void RaiseMapChanged()
        {
            Action changed = OnMapChanged;
            if (changed != null)
            {
                changed();
            }
        }

        private void HandleCommand(ICommandResponse response)
        {
            RaiseMapChanged();
            KillCommanderCommand.Response killed = response as KillCommanderCommand.Response;
            if (killed != null)
            {
                ForgetNonLocalCommanderVisibility(killed.CommanderId);
                return;
            }

            RemoveCommanderCommand.Response removed = response as RemoveCommanderCommand.Response;
            if (removed != null)
            {
                ForgetNonLocalCommanderVisibility(removed.CommanderId);
                return;
            }

            SpawnCommanderCommand.Response spawned = response as SpawnCommanderCommand.Response;
            ICommanderState commander = spawned != null ? spawned.commander : null;
            if (!IsLocalCommander(commander) || !_knownLocalCommanderIds.Add(commander.Id))
            {
                return;
            }

            AccessibilityEventBus.Publish(new WielderRecruitedEvent(commander.Id, GetCommanderName(commander)));
        }

        private void HandleCommanderChanged(CommanderChangedPayload payload)
        {
            RaiseMapChanged();
            if (payload == null)
            {
                return;
            }

            if (payload.SelectedCommander != null)
            {
                AccessibilityEventBus.Publish(new MapWielderSelectedEvent(
                    payload.SelectedCommander.Id,
                    GetCommanderName(payload.SelectedCommander),
                    payload.SelectedCommander.Position));
            }
        }

        private void HandleMapEntityChanged(MapEntityChangedPayload payload)
        {
            RaiseMapChanged();
            if (payload == null)
            {
                return;
            }

            IMapEntity selected = payload.SelectedMapEntity;
            if (selected == null || selected.Category != MapEntityCategory.BuildSite)
            {
                return;
            }

            IIsBuildSiteComponent buildSite;
            if (selected.TryGetComponent<IIsBuildSiteComponent>(out buildSite))
            {
                AccessibilityEventBus.Publish(new BuildSiteSelectedEvent(
                    selected.Id,
                    buildSite.Size,
                    selected.Position));
            }
        }

        private void HandleDestinationSet(int commanderId)
        {
            RaiseMapChanged();
            ICommanderState commander = _facade != null && _facade.Commanders != null
                ? _facade.Commanders.Get(commanderId)
                : null;
            if (commander == null)
            {
                return;
            }

            string name = GetCommanderName(commander);
            if (commander.Destination != null && commander.Destination.HasDestination)
            {
                WielderRoute route;
                WielderRoute.TryBuild(
                    _facade,
                    _selectionHandler,
                    _localizationHandler,
                    _fogManager,
                    commander,
                    out route);
                AccessibilityEventBus.Publish(new MapDestinationSetEvent(
                    commander.Id,
                    name,
                    commander.Destination.Destination,
                    route));
            }
            else
            {
                AccessibilityEventBus.Publish(new MapDestinationClearedEvent(commander.Id, name));
            }
        }

        private void HandleCommanderMoved(OnCommanderMovedPayload payload)
        {
            RaiseMapChanged();
            ICommanderState commander = payload != null ? payload.commander : null;
            if (commander == null)
            {
                return;
            }

            // TeleportCommanderCommand also raises OnCommanderMoved, but marks
            // that payload cameraIgnore so camera-follow systems do not treat it
            // as ordinary path movement. Teleports are published separately.
            if (payload.cameraIgnore)
            {
                return;
            }

            bool revealedNonLocalCommander = !IsLocalCommander(commander)
                && TrackNonLocalCommanderVisibility(commander, announceTransitions: true);
            if (IsLocalCommander(commander))
            {
                AddKnownMapEntityDiscoveries();
            }

            if (!ShouldPublishCommanderPositionEvent(commander, commander.Position))
            {
                return;
            }

            if (revealedNonLocalCommander)
            {
                FlushPendingDiscoveriesIfReady();
                return;
            }

            AccessibilityEventBus.Publish(new MapWielderMovedEvent(
                commander.Id,
                GetCommanderName(commander),
                commander.Position,
                IsLocalCommander(commander)));
        }

        private void HandleCommanderTeleported(TeleportCommanderCommand.Response response)
        {
            RaiseMapChanged();
            ICommanderState commander = response != null && _facade != null && _facade.Commanders != null
                ? _facade.Commanders.Get(response.CommanderId)
                : null;
            if (commander == null)
            {
                return;
            }

            bool revealedNonLocalCommander = !IsLocalCommander(commander)
                && TrackNonLocalCommanderVisibility(commander, announceTransitions: true);
            if (IsLocalCommander(commander))
            {
                AddKnownMapEntityDiscoveries();
            }

            if (!ShouldPublishCommanderPositionEvent(commander, response.ToLocation))
            {
                return;
            }

            if (revealedNonLocalCommander)
            {
                FlushPendingDiscoveriesIfReady();
                return;
            }

            AccessibilityEventBus.Publish(new MapWielderTeleportedEvent(
                commander.Id,
                GetCommanderName(commander),
                response.ToLocation,
                response.Source));
        }

        private void HandleMapEntityCreated(int entityId)
        {
            RaiseMapChanged();
            IMapEntity entity = _facade != null && _facade.MapEntities != null
                ? _facade.MapEntities.Get(entityId)
                : null;
            if (AddKnownMapEntityDiscovery(entity))
            {
                FlushPendingDiscoveriesIfReady();
            }
        }

        private void HandleFogUpdated()
        {
            RaiseMapChanged();
            int explorationLength = GetLocalExplorationLength();
            if (explorationLength <= 0 || _lastExplorationLength != explorationLength)
            {
                _lastExplorationLength = explorationLength;
                RefreshMapEntityDiscoveryBaseline();
                RefreshNonLocalCommanderVisibilityBaseline();
                return;
            }

            bool added = AddKnownMapEntityDiscoveries();
            added = RefreshNonLocalCommanderVisibility(announceTransitions: true) || added;
            if (added)
            {
                FlushPendingDiscoveriesIfReady();
            }
        }

        private void CaptureDiscoveryBaseline()
        {
            _lastExplorationLength = GetLocalExplorationLength();
            RefreshMapEntityDiscoveryBaseline();
            RefreshNonLocalCommanderVisibilityBaseline();
        }

        private void RefreshMapEntityDiscoveryBaseline()
        {
            if (_facade == null || _facade.MapEntities == null)
            {
                return;
            }

            IEnumerable<IMapEntity> entities = _facade.MapEntities.All;
            if (entities == null)
            {
                return;
            }

            foreach (IMapEntity entity in entities)
            {
                if (!ShouldConsiderMapEntityDiscovery(entity))
                {
                    continue;
                }

                if (IsMapEntityKnown(entity))
                {
                    _discoveredMapEntityIds.Add(entity.Id);
                    _discoveredMapEntityLabelsById[entity.Id] = GetMapEntityName(entity);
                }
            }
        }

        /// <summary>
        /// Walks every map entity looking for newly identifiable ones. The fog manager raises
        /// onFogUpdated every frame while a fog actor is moving, so the walk is gated on a key read
        /// from the game each call: how much of the map is explored, where every commander stands,
        /// and which commander is selected. Those are the sweep's inputs - a tile becomes known when
        /// it is explored, and an entity's label comes from the scouting detail level, which the
        /// game computes from the local partnership's commander positions. They change once per tile
        /// step where the fog event fires once per frame.
        /// </summary>
        private bool AddKnownMapEntityDiscoveries()
        {
            if (_facade == null || _facade.MapEntities == null)
            {
                return false;
            }

            long sweepKey = GetDiscoverySweepKey();
            if (_hasDiscoverySweepKey && sweepKey == _lastDiscoverySweepKey)
            {
                return false;
            }

            _lastDiscoverySweepKey = sweepKey;
            _hasDiscoverySweepKey = true;

            IEnumerable<IMapEntity> entities = _facade.MapEntities.All;
            if (entities == null)
            {
                return false;
            }

            bool added = false;
            foreach (IMapEntity entity in entities)
            {
                added = AddKnownMapEntityDiscovery(entity) || added;
            }

            return added;
        }

        private bool AddKnownMapEntityDiscovery(IMapEntity entity)
        {
            Vector2Int revealTile;
            if (!ShouldConsiderMapEntityDiscovery(entity)
                || !AdventureMapVisibility.TryGetKnownMapEntityIdentityTile(
                    _facade,
                    _fogManager,
                    entity,
                    out revealTile))
            {
                return false;
            }

            string label = GetMapEntityName(entity);
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            bool alreadyDiscovered = _discoveredMapEntityIds.Contains(entity.Id);
            string previousLabel;
            if (alreadyDiscovered
                && _discoveredMapEntityLabelsById.TryGetValue(entity.Id, out previousLabel)
                && LabelsMatch(previousLabel, label))
            {
                return false;
            }

            _discoveredMapEntityIds.Add(entity.Id);
            _discoveredMapEntityLabelsById[entity.Id] = label;
            string discoveryKey = EntityDiscoveryKey(entity.Id);
            if (alreadyDiscovered)
            {
                RemovePendingDiscovery(discoveryKey);
            }

            return AddPendingDiscovery(
                discoveryKey,
                label,
                revealTile,
                entity.Id,
                AdventureMapRevealedKind.MapEntity);
        }

        private long GetDiscoverySweepKey()
        {
            long key = 17;
            key = Mix(key, GetLocalTeamId());

            byte[] exploration = GetLocalExploration();
            key = Mix(key, exploration == null ? -1 : exploration.Length);
            int exploredCount = 0;
            if (exploration != null)
            {
                for (int i = 0; i < exploration.Length; i++)
                {
                    if (exploration[i] != 0)
                    {
                        exploredCount++;
                    }
                }
            }

            key = Mix(key, exploredCount);

            ICommanderState selected = _selectionHandler != null ? _selectionHandler.SelectedCommander : null;
            key = Mix(key, selected != null ? selected.Id : -1);

            IEnumerable<ICommanderState> commanders = _facade != null && _facade.Commanders != null
                ? _facade.Commanders.All
                : null;
            if (commanders != null)
            {
                foreach (ICommanderState commander in commanders)
                {
                    if (commander == null)
                    {
                        continue;
                    }

                    key = Mix(key, commander.Id);
                    key = Mix(key, commander.Position.x);
                    key = Mix(key, commander.Position.y);
                    key = Mix(key, commander.IsAlive ? 1 : 0);
                }
            }

            return key;
        }

        private static long Mix(long key, int value)
        {
            unchecked
            {
                return (key * 1099511628211L) ^ value;
            }
        }

        private bool ShouldConsiderMapEntityDiscovery(IMapEntity entity)
        {
            return entity != null
                && entity.IsEnabled
                && entity.IsVisibleInGame
                && entity.Category != MapEntityCategory.Artistic;
        }

        private bool IsMapEntityKnown(IMapEntity entity)
        {
            Vector2Int ignored;
            return AdventureMapVisibility.TryGetKnownMapEntityIdentityTile(_facade, _fogManager, entity, out ignored);
        }

        private void RefreshNonLocalCommanderVisibilityBaseline()
        {
            RefreshNonLocalCommanderVisibility(announceTransitions: false);
        }

        private bool RefreshNonLocalCommanderVisibility(bool announceTransitions)
        {
            if (_facade == null || _facade.Commanders == null)
            {
                return false;
            }

            IEnumerable<ICommanderState> commanders = _facade.Commanders.All;
            if (commanders == null)
            {
                return false;
            }

            HashSet<int> seen = _seenNonLocalCommanderIds;
            seen.Clear();
            bool added = false;
            foreach (ICommanderState commander in commanders)
            {
                if (commander == null || IsLocalCommander(commander))
                {
                    continue;
                }

                seen.Add(commander.Id);
                added = TrackNonLocalCommanderVisibility(commander, announceTransitions) || added;
            }

            RemoveStaleCommanderVisibility(seen);
            return added;
        }

        private bool TrackNonLocalCommanderVisibility(ICommanderState commander, bool announceTransitions)
        {
            if (commander == null || IsLocalCommander(commander))
            {
                return false;
            }

            if (!commander.IsAlive)
            {
                ForgetNonLocalCommanderVisibility(commander.Id);
                return false;
            }

            bool visible = IsPointVisible(commander.Position);
            bool wasVisible;
            _lastVisibleNonLocalCommanders.TryGetValue(commander.Id, out wasVisible);
            _lastVisibleNonLocalCommanders[commander.Id] = visible;
            bool announcedVisible;
            if (!_announcedVisibleNonLocalCommanders.TryGetValue(commander.Id, out announcedVisible))
            {
                announcedVisible = wasVisible;
                _announcedVisibleNonLocalCommanders[commander.Id] = visible;
            }

            string discoveryKey = CommanderDiscoveryKey(commander.Id);
            if (!announceTransitions)
            {
                _announcedVisibleNonLocalCommanders[commander.Id] = visible;
                return false;
            }

            if (visible)
            {
                RemovePendingHiddenWielder(discoveryKey);
                if (announcedVisible)
                {
                    return false;
                }
            }
            else
            {
                RemovePendingDiscovery(discoveryKey);
                _revealedRegistry?.Remove(discoveryKey);
                if (announcedVisible)
                {
                    return AddPendingHiddenWielder(discoveryKey, GetCommanderName(commander));
                }

                return false;
            }

            if (wasVisible)
            {
                return false;
            }

            string label = GetCommanderName(commander);
            return AddPendingDiscovery(
                discoveryKey,
                label,
                commander.Position,
                commander.Id,
                AdventureMapRevealedKind.Wielder);
        }

        private void RemoveStaleCommanderVisibility(HashSet<int> seen)
        {
            List<int> stale = _staleNonLocalCommanderIds;
            stale.Clear();
            foreach (int commanderId in _lastVisibleNonLocalCommanders.Keys)
            {
                if (seen == null || !seen.Contains(commanderId))
                {
                    stale.Add(commanderId);
                }
            }

            for (int i = 0; i < stale.Count; i++)
            {
                ForgetNonLocalCommanderVisibility(stale[i]);
            }
        }

        private void ForgetNonLocalCommanderVisibility(int commanderId)
        {
            string discoveryKey = CommanderDiscoveryKey(commanderId);
            _lastVisibleNonLocalCommanders.Remove(commanderId);
            _announcedVisibleNonLocalCommanders.Remove(commanderId);
            RemovePendingDiscovery(discoveryKey);
            RemovePendingHiddenWielder(discoveryKey);
            _revealedRegistry?.Remove(discoveryKey);
        }

        private bool AddPendingDiscovery(
            string objectKey,
            string label,
            Vector2Int position,
            int stableReference,
            AdventureMapRevealedKind kind)
        {
            if (!_pendingDiscoveries.Add(objectKey, label))
            {
                return false;
            }

            _pendingDiscoveries.SetEntry(
                objectKey,
                new PendingRevealedEntry(objectKey, label, position, stableReference, kind));
            return true;
        }

        private bool RemovePendingDiscovery(string objectKey)
        {
            return _pendingDiscoveries.Remove(objectKey);
        }

        private bool AddPendingHiddenWielder(string objectKey, string label)
        {
            return _pendingHiddenWielders.Add(objectKey, label);
        }

        private bool RemovePendingHiddenWielder(string objectKey)
        {
            return _pendingHiddenWielders.Remove(objectKey);
        }

        private void FlushPendingDiscoveriesIfReady()
        {
            if ((_pendingDiscoveries.IsEmpty && _pendingHiddenWielders.IsEmpty)
                || IsMovementInProgress())
            {
                return;
            }

            RevalidatePendingMapEntityDiscoveries();
            if (_pendingDiscoveries.IsEmpty && _pendingHiddenWielders.IsEmpty)
            {
                return;
            }

            List<string> discoveredItems = _pendingDiscoveries.BuildItems();
            List<string> hiddenWielders = _pendingHiddenWielders.BuildItems();
            AddPendingDiscoveriesToRevealedRegistry();
            ApplyFlushedCommanderVisibilityStates();
            ClearPendingDiscoveries();
            if (discoveredItems.Count != 0)
            {
                AccessibilityEventBus.Publish(new MapDiscoveryRevealedEvent(discoveredItems));
            }

            if (hiddenWielders.Count != 0)
            {
                AccessibilityEventBus.Publish(new MapWieldersNoLongerVisibleEvent(hiddenWielders));
            }
        }

        private void RevalidatePendingMapEntityDiscoveries()
        {
            IReadOnlyList<string> keys = _pendingDiscoveries.KeyOrder;
            for (int i = keys.Count - 1; i >= 0; i--)
            {
                string key = keys[i];
                PendingRevealedEntry entry;
                if (!_pendingDiscoveries.TryGetEntry(key, out entry)
                    || entry.Kind != AdventureMapRevealedKind.MapEntity)
                {
                    continue;
                }

                IMapEntity entity = TryGetMapEntity(entry.StableReference);
                Vector2Int revealTile;
                if (entity == null
                    || !AdventureMapVisibility.TryGetKnownMapEntityIdentityTile(
                        _facade,
                        _fogManager,
                        entity,
                        out revealTile))
                {
                    RemovePendingDiscovery(key);
                    _discoveredMapEntityIds.Remove(entry.StableReference);
                    _discoveredMapEntityLabelsById.Remove(entry.StableReference);
                    continue;
                }

                string label = GetMapEntityName(entity);
                bool labelChanged = !string.IsNullOrWhiteSpace(label) && !LabelsMatch(entry.Label, label);
                if (revealTile != entry.Position || labelChanged)
                {
                    _pendingDiscoveries.SetEntry(key, new PendingRevealedEntry(
                        entry.Key,
                        labelChanged ? label : entry.Label,
                        revealTile,
                        entry.StableReference,
                        entry.Kind));
                }

                if (labelChanged)
                {
                    _pendingDiscoveries.ReplaceLabel(key, label);
                    _discoveredMapEntityLabelsById[entry.StableReference] = label;
                }
            }
        }

        private void ClearPendingDiscoveries()
        {
            _pendingDiscoveries.Clear();
            _pendingHiddenWielders.Clear();
        }

        private void AddPendingDiscoveriesToRevealedRegistry()
        {
            if (_revealedRegistry == null)
            {
                return;
            }

            IReadOnlyList<string> keys = _pendingDiscoveries.KeyOrder;
            for (int i = 0; i < keys.Count; i++)
            {
                PendingRevealedEntry entry;
                if (!_pendingDiscoveries.TryGetEntry(keys[i], out entry))
                {
                    continue;
                }

                _revealedRegistry.AddOrUpdate(
                    entry.Key,
                    entry.Label,
                    entry.Position,
                    entry.StableReference,
                    entry.Kind);
            }
        }

        private void ApplyFlushedCommanderVisibilityStates()
        {
            IReadOnlyList<string> keys = _pendingDiscoveries.KeyOrder;
            for (int i = 0; i < keys.Count; i++)
            {
                PendingRevealedEntry entry;
                if (_pendingDiscoveries.TryGetEntry(keys[i], out entry)
                    && entry.Kind == AdventureMapRevealedKind.Wielder
                    && TryParseCommanderDiscoveryKey(entry.Key, out int commanderId))
                {
                    _announcedVisibleNonLocalCommanders[commanderId] = true;
                }
            }

            foreach (string key in _pendingHiddenWielders.KeyOrder)
            {
                if (TryParseCommanderDiscoveryKey(key, out int commanderId))
                {
                    _announcedVisibleNonLocalCommanders[commanderId] = false;
                }
            }
        }

        private static string EntityDiscoveryKey(int entityId)
        {
            return "entity:" + entityId;
        }

        private static bool LabelsMatch(string left, string right)
        {
            return string.Equals(
                (left ?? string.Empty).Trim(),
                (right ?? string.Empty).Trim(),
                StringComparison.Ordinal);
        }

        // The game's Get is a dictionary TryGetValue (AbstractMapEntityManager.Get), so an id it
        // does not know answers null rather than throwing.
        private IMapEntity TryGetMapEntity(int id)
        {
            return _facade != null && _facade.MapEntities != null ? _facade.MapEntities.Get(id) : null;
        }

        private static string CommanderDiscoveryKey(int commanderId)
        {
            return "commander:" + commanderId;
        }

        private static bool TryParseCommanderDiscoveryKey(string key, out int commanderId)
        {
            commanderId = 0;
            const string Prefix = "commander:";
            return !string.IsNullOrWhiteSpace(key)
                && key.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
                && int.TryParse(key.Substring(Prefix.Length), out commanderId);
        }

        private bool IsMovementInProgress()
        {
            if (_humanAdventureControllerFacade == null || _humanAdventureControllerFacade.StateMachine == null)
            {
                return false;
            }

            // CurrentStateType is a plain field read on the game's StateMachine, and the two
            // objects that lead to it are null-checked above, so there is nothing here to throw.
            HumanAdventureController.State state = _humanAdventureControllerFacade.StateMachine.CurrentStateType;
            return state == HumanAdventureController.State.CommanderMoveToPoint
                || state == HumanAdventureController.State.WaitForCommanderToFinish;
        }

        /// <summary>
        /// The length of the local team's exploration array, or -1 when the game has none. Only the
        /// length was ever compared against the previous update, so the array is read, never copied.
        /// </summary>
        private int GetLocalExplorationLength()
        {
            byte[] exploration = GetLocalExploration();
            return exploration == null ? -1 : exploration.Length;
        }

        private byte[] GetLocalExploration()
        {
            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0 || _facade == null || _facade.Level == null)
            {
                return null;
            }

            return _facade.Level.GetExplorationForTeam(localTeamId);
        }

        private int GetLocalTeamId()
        {
            if (_facade == null || _facade.Teams == null)
            {
                return -1;
            }

            return _facade.Teams.LocalTeamInControlId;
        }

        private bool IsPointVisible(Vector2Int point)
        {
            if (_fogManager == null)
            {
                return false;
            }

            // FogRenderer.GetFog answers 0 for a point outside the fog area and for a renderer
            // that is not valid yet, so an off-map point is false rather than a throw. The same
            // call is already made unguarded in ShouldPublishCommanderPositionEvent.
            return _fogManager.IsVisible(point);
        }

        private bool ShouldPublishCommanderPositionEvent(ICommanderState commander, UnityEngine.Vector2Int tile)
        {
            if (commander == null || _facade == null || _facade.Teams == null)
            {
                return false;
            }

            if (IsLocalCommander(commander))
            {
                return true;
            }

            return _fogManager != null && _fogManager.IsVisible(tile);
        }

        private bool IsLocalCommander(ICommanderState commander)
        {
            return commander != null
                && _facade != null
                && _facade.Teams != null
                && _facade.Teams.GetIsLocal(commander.TeamId);
        }

        private string GetCommanderName(ICommanderState commander)
        {
            if (commander == null || _facade == null || _facade.Commanders == null)
            {
                return ModText.Get(ModStrings.Events.Wielder);
            }

            string name = _facade.Commanders.GetName(commander.Id);
            return string.IsNullOrWhiteSpace(name) ? ModText.Get(ModStrings.Events.Wielder) : name;
        }

        private string GetMapEntityName(IMapEntity entity)
        {
            return AdventureMapEntityLabel.GetMapEntityName(_facade, _selectionHandler, _localizationHandler, entity);
        }

        /// <summary>
        /// One queue of announcements waiting for movement to finish: a line per distinct label
        /// with how many objects carry it, in the order the labels first appeared, and the object
        /// keys that contributed so one can be withdrawn again before the queue is spoken. The
        /// listener keeps two, one for what was revealed and one for the wielders that went out of
        /// sight, and a key never belongs to both at once.
        /// </summary>
        private sealed class PendingAnnouncementLedger
        {
            private readonly Dictionary<string, DiscoveryCount> _countsByLabel =
                new Dictionary<string, DiscoveryCount>(StringComparer.OrdinalIgnoreCase);
            private readonly Dictionary<string, string> _labelsByKey = new Dictionary<string, string>();
            private readonly Dictionary<string, PendingRevealedEntry> _entriesByKey =
                new Dictionary<string, PendingRevealedEntry>();
            private readonly List<string> _labelOrder = new List<string>();
            private readonly List<string> _keyOrder = new List<string>();

            public bool IsEmpty
            {
                get { return _labelOrder.Count == 0; }
            }

            /// <summary>The object keys in the order they were added.</summary>
            public IReadOnlyList<string> KeyOrder
            {
                get { return _keyOrder; }
            }

            public bool Add(string objectKey, string label)
            {
                if (string.IsNullOrWhiteSpace(label))
                {
                    return false;
                }

                if (!string.IsNullOrWhiteSpace(objectKey))
                {
                    if (_labelsByKey.ContainsKey(objectKey))
                    {
                        return false;
                    }

                    _labelsByKey[objectKey] = label;
                    _keyOrder.Add(objectKey);
                }

                Increment(label);
                return true;
            }

            /// <summary>Attaches what the revealed registry needs; ignored for a key that is not queued.</summary>
            public void SetEntry(string objectKey, PendingRevealedEntry entry)
            {
                if (string.IsNullOrWhiteSpace(objectKey) || !_labelsByKey.ContainsKey(objectKey))
                {
                    return;
                }

                _entriesByKey[objectKey] = entry;
            }

            public bool TryGetEntry(string objectKey, out PendingRevealedEntry entry)
            {
                entry = default(PendingRevealedEntry);
                return !string.IsNullOrWhiteSpace(objectKey) && _entriesByKey.TryGetValue(objectKey, out entry);
            }

            public bool Remove(string objectKey)
            {
                if (string.IsNullOrWhiteSpace(objectKey))
                {
                    return false;
                }

                string label;
                if (!_labelsByKey.TryGetValue(objectKey, out label))
                {
                    return false;
                }

                _labelsByKey.Remove(objectKey);
                _entriesByKey.Remove(objectKey);
                _keyOrder.Remove(objectKey);
                Decrement(label);
                return true;
            }

            /// <summary>Moves one queued object to a different label, keeping the counts right.</summary>
            public bool ReplaceLabel(string objectKey, string newLabel)
            {
                if (string.IsNullOrWhiteSpace(objectKey) || string.IsNullOrWhiteSpace(newLabel))
                {
                    return false;
                }

                string oldLabel;
                if (!_labelsByKey.TryGetValue(objectKey, out oldLabel) || LabelsMatch(oldLabel, newLabel))
                {
                    return false;
                }

                Decrement(oldLabel);
                _labelsByKey[objectKey] = newLabel;
                Increment(newLabel);
                return true;
            }

            public List<string> BuildItems()
            {
                List<string> items = new List<string>();
                for (int i = 0; i < _labelOrder.Count; i++)
                {
                    DiscoveryCount count;
                    if (!_countsByLabel.TryGetValue(_labelOrder[i], out count))
                    {
                        continue;
                    }

                    items.Add(count.Count <= 1
                        ? count.Label
                        : ModText.Get(ModStrings.Common.ResourceAmount, count.Count, count.Label));
                }

                return items;
            }

            public void Clear()
            {
                _countsByLabel.Clear();
                _labelsByKey.Clear();
                _entriesByKey.Clear();
                _labelOrder.Clear();
                _keyOrder.Clear();
            }

            private void Increment(string label)
            {
                DiscoveryCount count;
                if (_countsByLabel.TryGetValue(label, out count))
                {
                    _countsByLabel[label] = new DiscoveryCount(label, count.Count + 1);
                    return;
                }

                _countsByLabel[label] = new DiscoveryCount(label, 1);
                _labelOrder.Add(label);
            }

            private void Decrement(string label)
            {
                DiscoveryCount count;
                if (string.IsNullOrWhiteSpace(label) || !_countsByLabel.TryGetValue(label, out count))
                {
                    return;
                }

                if (count.Count <= 1)
                {
                    _countsByLabel.Remove(label);
                    _labelOrder.Remove(label);
                    return;
                }

                _countsByLabel[label] = new DiscoveryCount(label, count.Count - 1);
            }
        }

        private struct DiscoveryCount
        {
            public DiscoveryCount(string label, int count)
            {
                Label = label ?? string.Empty;
                Count = count;
            }

            public string Label;
            public int Count;
        }

        private struct PendingRevealedEntry
        {
            public PendingRevealedEntry(
                string key,
                string label,
                Vector2Int position,
                int stableReference,
                AdventureMapRevealedKind kind)
            {
                Key = key ?? string.Empty;
                Label = label ?? string.Empty;
                Position = position;
                StableReference = stableReference;
                Kind = kind;
            }

            public string Key;
            public string Label;
            public Vector2Int Position;
            public int StableReference;
            public AdventureMapRevealedKind Kind;
        }
    }
}
