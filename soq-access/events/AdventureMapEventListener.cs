using System;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;

namespace SongsOfConquestAccess.Events
{
    internal sealed class AdventureMapEventListener
    {
        private readonly IClientAdventureFacade _facade;
        private readonly ISelectionHandler _selectionHandler;
        private readonly IHumanAdventureControllerFacade _humanAdventureControllerFacade;
        private readonly ILocalizationHandler _localizationHandler;
        private bool _attached;

        public AdventureMapEventListener(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            IHumanAdventureControllerFacade humanAdventureControllerFacade,
            ILocalizationHandler localizationHandler)
        {
            _facade = facade;
            _selectionHandler = selectionHandler;
            _humanAdventureControllerFacade = humanAdventureControllerFacade;
            _localizationHandler = localizationHandler;
        }

        public void Attach()
        {
            if (_attached)
            {
                return;
            }

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
                _facade.Commands.OnCommanderMoved =
                    (Action<OnCommanderMovedPayload>)Delegate.Combine(
                        _facade.Commands.OnCommanderMoved,
                        new Action<OnCommanderMovedPayload>(HandleCommanderMoved));
            }

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
                _facade.Commands.OnCommanderMoved =
                    (Action<OnCommanderMovedPayload>)Delegate.Remove(
                        _facade.Commands.OnCommanderMoved,
                        new Action<OnCommanderMovedPayload>(HandleCommanderMoved));
            }

            _attached = false;
        }

        private void HandleCommanderChanged(CommanderChangedPayload payload)
        {
            if (payload == null)
            {
                return;
            }

            if (payload.DeselectedCommander != null)
            {
                AccessibilityEventBus.Publish(new MapWielderUnselectedEvent(
                    payload.DeselectedCommander.Id,
                    GetCommanderName(payload.DeselectedCommander),
                    payload.DeselectedCommander.Position));
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

            if (payload.DeselectedMapEntity != null)
            {
                AccessibilityEventBus.Publish(new MapEntityUnselectedEvent(
                    payload.DeselectedMapEntity.Id,
                    GetMapEntityName(payload.DeselectedMapEntity),
                    payload.DeselectedMapEntity.Position));
            }

            if (payload.SelectedMapEntity != null)
            {
                AccessibilityEventBus.Publish(new MapEntitySelectedEvent(
                    payload.SelectedMapEntity.Id,
                    GetMapEntityName(payload.SelectedMapEntity),
                    payload.SelectedMapEntity.Position));
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

            AccessibilityEventBus.Publish(new MapWielderMovedEvent(
                commander.Id,
                GetCommanderName(commander),
                commander.Position));
        }

        private string GetCommanderName(ICommanderState commander)
        {
            if (commander == null || _facade == null || _facade.Commanders == null)
            {
                return "wielder";
            }

            string name = _facade.Commanders.GetName(commander.Id);
            return string.IsNullOrWhiteSpace(name) ? "wielder" : name;
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
    }
}
