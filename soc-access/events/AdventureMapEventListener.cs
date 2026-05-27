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
    internal sealed class AdventureMapEventListener
    {
        private readonly IClientAdventureFacade _facade;
        private readonly ISelectionHandler _selectionHandler;
        private readonly IHumanAdventureControllerFacade _humanAdventureControllerFacade;
        private readonly ILocalizationHandler _localizationHandler;
        private readonly IFogManager _fogManager;
        private readonly AdventureMapRevealedRegistry _revealedRegistry;
        private readonly Dictionary<int, bool> _lastVisibleNonLocalCommanders = new Dictionary<int, bool>();
        private readonly Dictionary<int, bool> _announcedVisibleNonLocalCommanders = new Dictionary<int, bool>();
        private readonly Dictionary<string, DiscoveryCount> _pendingDiscoveryCounts =
            new Dictionary<string, DiscoveryCount>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pendingDiscoveryLabelsByKey = new Dictionary<string, string>();
        private readonly Dictionary<string, PendingRevealedEntry> _pendingRevealedEntriesByKey =
            new Dictionary<string, PendingRevealedEntry>();
        private readonly List<string> _pendingDiscoveryOrder = new List<string>();
        private readonly List<string> _pendingDiscoveryKeyOrder = new List<string>();
        private readonly Dictionary<string, DiscoveryCount> _pendingHiddenWielderCounts =
            new Dictionary<string, DiscoveryCount>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, string> _pendingHiddenWielderLabelsByKey = new Dictionary<string, string>();
        private readonly List<string> _pendingHiddenWielderOrder = new List<string>();
        private readonly HashSet<int> _discoveredMapEntityIds = new HashSet<int>();
        private readonly Dictionary<int, string> _discoveredMapEntityLabelsById = new Dictionary<int, string>();
        private readonly HashSet<int> _knownLocalCommanderIds = new HashSet<int>();
        private byte[] _lastExploration;
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
            _lastExploration = null;
            _lastVisibleNonLocalCommanders.Clear();
            _announcedVisibleNonLocalCommanders.Clear();
            _discoveredMapEntityIds.Clear();
            _discoveredMapEntityLabelsById.Clear();
            _knownLocalCommanderIds.Clear();
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

        private void HandleCommand(ICommandResponse response)
        {
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
                AccessibilityEventBus.Publish(new MapDestinationSetEvent(
                    commander.Id,
                    name,
                    commander.Destination.Destination));
            }
            else
            {
                AccessibilityEventBus.Publish(new MapDestinationClearedEvent(commander.Id, name));
            }
        }

        private void HandleCommanderMoved(OnCommanderMovedPayload payload)
        {
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
            byte[] currentExploration = GetLocalExplorationSnapshot();
            if (currentExploration == null || currentExploration.Length == 0)
            {
                _lastExploration = currentExploration;
                RefreshMapEntityDiscoveryBaseline();
                RefreshNonLocalCommanderVisibilityBaseline();
                return;
            }

            if (_lastExploration == null || _lastExploration.Length != currentExploration.Length)
            {
                _lastExploration = currentExploration;
                RefreshMapEntityDiscoveryBaseline();
                RefreshNonLocalCommanderVisibilityBaseline();
                return;
            }

            _lastExploration = currentExploration;

            bool added = AddKnownMapEntityDiscoveries();
            added = RefreshNonLocalCommanderVisibility(announceTransitions: true) || added;
            if (added)
            {
                FlushPendingDiscoveriesIfReady();
            }
        }

        private void CaptureDiscoveryBaseline()
        {
            _lastExploration = GetLocalExplorationSnapshot();
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

        private bool AddKnownMapEntityDiscoveries()
        {
            if (_facade == null || _facade.MapEntities == null)
            {
                return false;
            }

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

            HashSet<int> seen = new HashSet<int>();
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
            List<int> stale = new List<int>();
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
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(objectKey))
            {
                if (_pendingDiscoveryLabelsByKey.ContainsKey(objectKey))
                {
                    return false;
                }

                _pendingDiscoveryLabelsByKey[objectKey] = label;
                _pendingRevealedEntriesByKey[objectKey] = new PendingRevealedEntry(
                    objectKey,
                    label,
                    position,
                    stableReference,
                    kind);
                _pendingDiscoveryKeyOrder.Add(objectKey);
            }

            DiscoveryCount count;
            if (_pendingDiscoveryCounts.TryGetValue(label, out count))
            {
                count.Count++;
                _pendingDiscoveryCounts[label] = count;
            }
            else
            {
                _pendingDiscoveryCounts[label] = new DiscoveryCount(label, 1);
                _pendingDiscoveryOrder.Add(label);
            }

            return true;
        }

        private bool RemovePendingDiscovery(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return false;
            }

            string label;
            if (!_pendingDiscoveryLabelsByKey.TryGetValue(objectKey, out label))
            {
                return false;
            }

            _pendingDiscoveryLabelsByKey.Remove(objectKey);
            _pendingRevealedEntriesByKey.Remove(objectKey);
            _pendingDiscoveryKeyOrder.Remove(objectKey);
            DiscoveryCount count;
            if (!_pendingDiscoveryCounts.TryGetValue(label, out count))
            {
                return true;
            }

            if (count.Count <= 1)
            {
                _pendingDiscoveryCounts.Remove(label);
                _pendingDiscoveryOrder.Remove(label);
            }
            else
            {
                count.Count--;
                _pendingDiscoveryCounts[label] = count;
            }

            return true;
        }

        private bool AddPendingHiddenWielder(string objectKey, string label)
        {
            if (string.IsNullOrWhiteSpace(label))
            {
                return false;
            }

            if (!string.IsNullOrWhiteSpace(objectKey))
            {
                if (_pendingHiddenWielderLabelsByKey.ContainsKey(objectKey))
                {
                    return false;
                }

                _pendingHiddenWielderLabelsByKey[objectKey] = label;
            }

            DiscoveryCount count;
            if (_pendingHiddenWielderCounts.TryGetValue(label, out count))
            {
                count.Count++;
                _pendingHiddenWielderCounts[label] = count;
            }
            else
            {
                _pendingHiddenWielderCounts[label] = new DiscoveryCount(label, 1);
                _pendingHiddenWielderOrder.Add(label);
            }

            return true;
        }

        private bool RemovePendingHiddenWielder(string objectKey)
        {
            if (string.IsNullOrWhiteSpace(objectKey))
            {
                return false;
            }

            string label;
            if (!_pendingHiddenWielderLabelsByKey.TryGetValue(objectKey, out label))
            {
                return false;
            }

            _pendingHiddenWielderLabelsByKey.Remove(objectKey);
            DiscoveryCount count;
            if (!_pendingHiddenWielderCounts.TryGetValue(label, out count))
            {
                return true;
            }

            if (count.Count <= 1)
            {
                _pendingHiddenWielderCounts.Remove(label);
                _pendingHiddenWielderOrder.Remove(label);
            }
            else
            {
                count.Count--;
                _pendingHiddenWielderCounts[label] = count;
            }

            return true;
        }

        private void FlushPendingDiscoveriesIfReady()
        {
            if ((_pendingDiscoveryOrder.Count == 0 && _pendingHiddenWielderOrder.Count == 0)
                || IsMovementInProgress())
            {
                return;
            }

            RevalidatePendingMapEntityDiscoveries();
            if (_pendingDiscoveryOrder.Count == 0 && _pendingHiddenWielderOrder.Count == 0)
            {
                return;
            }

            List<string> discoveredItems = BuildPendingItems(_pendingDiscoveryOrder, _pendingDiscoveryCounts);
            List<string> hiddenWielders = BuildPendingItems(_pendingHiddenWielderOrder, _pendingHiddenWielderCounts);
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
            for (int i = _pendingDiscoveryKeyOrder.Count - 1; i >= 0; i--)
            {
                string key = _pendingDiscoveryKeyOrder[i];
                PendingRevealedEntry entry;
                if (!_pendingRevealedEntriesByKey.TryGetValue(key, out entry)
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
                    _pendingRevealedEntriesByKey[key] = new PendingRevealedEntry(
                        entry.Key,
                        labelChanged ? label : entry.Label,
                        revealTile,
                        entry.StableReference,
                        entry.Kind);
                }

                if (labelChanged)
                {
                    ReplacePendingDiscoveryLabel(key, label);
                    _discoveredMapEntityLabelsById[entry.StableReference] = label;
                }
            }
        }

        private static List<string> BuildPendingItems(List<string> order, Dictionary<string, DiscoveryCount> counts)
        {
            List<string> items = new List<string>();
            if (order == null || counts == null)
            {
                return items;
            }

            for (int i = 0; i < order.Count; i++)
            {
                DiscoveryCount count;
                if (!counts.TryGetValue(order[i], out count))
                {
                    continue;
                }

                items.Add(count.Count <= 1
                    ? count.Label
                    : ModText.Get(ModStrings.Common.ResourceAmount, count.Count, count.Label));
            }

            return items;
        }

        private void ClearPendingDiscoveries()
        {
            _pendingDiscoveryCounts.Clear();
            _pendingDiscoveryLabelsByKey.Clear();
            _pendingRevealedEntriesByKey.Clear();
            _pendingDiscoveryOrder.Clear();
            _pendingDiscoveryKeyOrder.Clear();
            _pendingHiddenWielderCounts.Clear();
            _pendingHiddenWielderLabelsByKey.Clear();
            _pendingHiddenWielderOrder.Clear();
        }

        private bool ReplacePendingDiscoveryLabel(string objectKey, string newLabel)
        {
            if (string.IsNullOrWhiteSpace(objectKey) || string.IsNullOrWhiteSpace(newLabel))
            {
                return false;
            }

            string oldLabel;
            if (!_pendingDiscoveryLabelsByKey.TryGetValue(objectKey, out oldLabel)
                || LabelsMatch(oldLabel, newLabel))
            {
                return false;
            }

            DecrementPendingDiscoveryCount(oldLabel);
            _pendingDiscoveryLabelsByKey[objectKey] = newLabel;

            DiscoveryCount count;
            if (_pendingDiscoveryCounts.TryGetValue(newLabel, out count))
            {
                _pendingDiscoveryCounts[newLabel] = new DiscoveryCount(newLabel, count.Count + 1);
            }
            else
            {
                _pendingDiscoveryCounts[newLabel] = new DiscoveryCount(newLabel, 1);
                _pendingDiscoveryOrder.Add(newLabel);
            }

            return true;
        }

        private void DecrementPendingDiscoveryCount(string label)
        {
            DiscoveryCount count;
            if (string.IsNullOrWhiteSpace(label) || !_pendingDiscoveryCounts.TryGetValue(label, out count))
            {
                return;
            }

            if (count.Count <= 1)
            {
                _pendingDiscoveryCounts.Remove(label);
                _pendingDiscoveryOrder.Remove(label);
                return;
            }

            _pendingDiscoveryCounts[label] = new DiscoveryCount(label, count.Count - 1);
        }

        private void AddPendingDiscoveriesToRevealedRegistry()
        {
            if (_revealedRegistry == null)
            {
                return;
            }

            for (int i = 0; i < _pendingDiscoveryKeyOrder.Count; i++)
            {
                PendingRevealedEntry entry;
                if (!_pendingRevealedEntriesByKey.TryGetValue(_pendingDiscoveryKeyOrder[i], out entry))
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
            for (int i = 0; i < _pendingDiscoveryKeyOrder.Count; i++)
            {
                PendingRevealedEntry entry;
                if (_pendingRevealedEntriesByKey.TryGetValue(_pendingDiscoveryKeyOrder[i], out entry)
                    && entry.Kind == AdventureMapRevealedKind.Wielder
                    && TryParseCommanderDiscoveryKey(entry.Key, out int commanderId))
                {
                    _announcedVisibleNonLocalCommanders[commanderId] = true;
                }
            }

            foreach (string key in _pendingHiddenWielderLabelsByKey.Keys)
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

        private IMapEntity TryGetMapEntity(int id)
        {
            try
            {
                return _facade != null && _facade.MapEntities != null ? _facade.MapEntities.Get(id) : null;
            }
            catch
            {
                return null;
            }
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

            try
            {
                HumanAdventureController.State state = _humanAdventureControllerFacade.StateMachine.CurrentStateType;
                return state == HumanAdventureController.State.CommanderMoveToPoint
                    || state == HumanAdventureController.State.WaitForCommanderToFinish;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private byte[] GetLocalExplorationSnapshot()
        {
            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0 || _facade == null || _facade.Level == null)
            {
                return null;
            }

            byte[] exploration = _facade.Level.GetExplorationForTeam(localTeamId);
            if (exploration == null)
            {
                return null;
            }

            byte[] copy = new byte[exploration.Length];
            Array.Copy(exploration, copy, exploration.Length);
            return copy;
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

            try
            {
                return _fogManager.IsVisible(point);
            }
            catch (Exception)
            {
                return false;
            }
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

        private string Localize(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || _localizationHandler == null)
            {
                return string.Empty;
            }

            try
            {
                string text = _localizationHandler.GetText(key);
                return string.IsNullOrWhiteSpace(text) || text == key ? string.Empty : text;
            }
            catch (Exception)
            {
                return string.Empty;
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
