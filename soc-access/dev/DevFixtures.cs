using System;
using System.Collections.Generic;
using System.Reflection;
using Lavapotion.Networking;
using Newtonsoft.Json;
using SongsOfConquest;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Loader.Dev;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// The changes a test makes to the live game to set itself up, each as one compile-checked
    /// call from POST /eval. Unlike <see cref="DevProbe"/> these write game state; none of it is
    /// reachable from a player's game because the dev server is off there.
    ///
    /// Main-thread only.
    /// </summary>
    public static class DevFixtures
    {
        /// <summary>
        /// Spawn map entity blueprint <paramref name="blueprintId"/> with its origin on a tile
        /// through the game's own debug route (<see cref="CreateAdventureMapEntityCommand"/>).
        /// The game's validator only looks at the origin tile, and a wider entity (a market is
        /// two tiles, origin and the tile east) shoves any wielder standing on the rest of its
        /// footprint aside, so this checks every footprint tile first and answers
        /// <c>blocked</c> with what is in the way instead of spawning. The state appears a
        /// frame later and the mod's "Revealed ..." line confirms it. Read
        /// <see cref="DevProbe.TilesAround"/> to pick the tile.
        /// </summary>
        public static string SpawnAt(int blueprintId, int x, int y)
        {
            try
            {
                return DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    IGame game = ResolveFromScenes<IGame>();
                    IClientAdventureFacade facade = ResolveFromScenes<IClientAdventureFacade>();
                    if (game == null || facade == null || facade.Level == null)
                    {
                        json.WritePropertyName("error");
                        json.WriteValue("no adventure scene is up");
                        json.WriteEndObject();
                        return;
                    }

                    // Every Arleon*Hostile (37 to 39, 56 to 64) carries the game's DeprecatedComponent
                    // and is not what the game places any more; refused up front, 68 Hostile and 69
                    // RandomHostile are the current hostile blueprints.
                    string deprecated = DeprecatedBlueprint(blueprintId);
                    if (deprecated != null)
                    {
                        json.WritePropertyName("error");
                        json.WriteValue(deprecated);
                        json.WriteEndObject();
                        return;
                    }

                    Vector2Int origin = new Vector2Int(x, y);
                    json.WritePropertyName("blueprint");
                    json.WriteValue(blueprintId);
                    json.WritePropertyName("position");
                    json.WriteValue(x + "," + y);

                    List<string> blocked = new List<string>();
                    json.WritePropertyName("footprint");
                    json.WriteStartArray();
                    foreach (Vector2Int point in Footprint(facade, blueprintId, origin))
                    {
                        json.WriteValue(point.x + "," + point.y);
                        string obstacle = Obstacle(facade, point);
                        if (obstacle != null)
                        {
                            blocked.Add(point.x + "," + point.y + " " + obstacle);
                        }
                    }

                    json.WriteEndArray();
                    if (blocked.Count > 0)
                    {
                        json.WritePropertyName("blocked");
                        json.WriteStartArray();
                        for (int i = 0; i < blocked.Count; i++)
                        {
                            json.WriteValue(blocked[i]);
                        }

                        json.WriteEndArray();
                        json.WritePropertyName("accepted");
                        json.WriteValue(false);
                        json.WriteEndObject();
                        return;
                    }

                    bool accepted;
                    try
                    {
                        accepted = game.server.Commands.ProcessServerRequest(
                            new CreateAdventureMapEntityCommand.Request((ushort)blueprintId, origin));
                    }
                    catch (NullReferenceException)
                    {
                        // A hostile blueprint's server component spawns its commander inside its own
                        // Initialize and then destroys its map entity, which nulls the entity's
                        // component array; the game's initialisation loop dereferences it on the next
                        // iteration (AbstractMapEntityManager.InitializeEntity, 2026-09-13). The
                        // commander is on the map by then, so that throw is the expected end of a
                        // hostile spawn, and any other throw is not.
                        // The commander's client-side state appears a frame later, so the blueprint,
                        // not the map, says whether this was a hostile.
                        if (!IsHostileBlueprint(blueprintId))
                        {
                            throw;
                        }

                        accepted = true;
                        json.WritePropertyName("spawnedCommander");
                        json.WriteValue(true);
                    }

                    json.WritePropertyName("accepted");
                    json.WriteValue(accepted);
                    json.WriteEndObject();
                });
            }
            catch (Exception e)
            {
                // The whole trace, not the message: a throw inside the game's own spawn path (its
                // component initialisation, 2026-09-13) is only diagnosable from where it came from.
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>The game's own spawn path one call at a time, for a blueprint SpawnAt reports a
        /// throw for: which server component is null or throws in Initialize or LateInitialize.
        /// Registers no entity, but a component's Initialize has the game's own side effects: a
        /// hostile's creates its commander on the tile, so run it on a scratch save.</summary>
        public static string SpawnDiagnose(int blueprintId, int x, int y)
        {
            try
            {
                IGame game = ResolveFromScenes<IGame>();
                if (game == null)
                {
                    return DevJson.Error("no game");
                }

                DiContainer server = game.server.Context.Container;
                SongsOfConquest.Server.Gamestate.IServerAdventureFacade facade = server.TryResolve<SongsOfConquest.Server.Gamestate.IServerAdventureFacade>();
                IMapEntityManifestRetriever manifests = server.TryResolve<IMapEntityManifestRetriever>();
                if (facade == null || manifests == null)
                {
                    return DevJson.Error("server container lacks the adventure facade or the manifest retriever");
                }

                // The manager's own container and manifest, as its Add uses, not the server root.
                object manager = facade.MapEntities.GetType()
                    .GetField("_entityManager", BindingFlags.NonPublic | BindingFlags.Instance)
                    .GetValue(facade.MapEntities);
                Type managerType = manager != null ? manager.GetType() : null;
                DiContainer managerContainer = manager != null
                    ? managerType.GetProperty("Container", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager, null) as DiContainer
                    : null;
                object categories = manager != null
                    ? managerType.BaseType.GetField("_entityCategories", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(manager)
                    : null;
                IMapEntityBlueprint blueprint = manifests.GetAdventureBlueprint((AdventureMapEntities)blueprintId);
                IMapEntityState state = facade.MapEntities.CreateState(new MapEntityFormat((byte)x, (byte)y, (ushort)blueprintId, null), -1);
                IMapEntity entity = blueprint.CreateServerEntity(state, managerContainer ?? server);
                string categoryKnown = "unknown";
                if (categories != null)
                {
                    System.Collections.IDictionary dictionary = categories as System.Collections.IDictionary;
                    categoryKnown = dictionary != null ? dictionary.Contains(entity.Category).ToString() : categories.GetType().Name;
                }

                IAIMapEntity aiEntity = blueprint.CreateAIEntity(state, managerContainer ?? server);
                List<object> components = new List<object>();
                for (int i = 0; i < entity.AllComponents.Length; i++)
                {
                    IMapEntityComponent component = entity.AllComponents[i];
                    string initialize = null;
                    string late = null;
                    if (component != null)
                    {
                        try { component.Initialize(); } catch (Exception e) { initialize = e.ToString(); }
                        try { component.LateInitialize(); } catch (Exception e) { late = e.ToString(); }
                    }

                    components.Add(new
                    {
                        index = i,
                        type = component != null ? component.GetType().Name : null,
                        initializeThrew = initialize,
                        lateInitializeThrew = late
                    });
                }

                return Newtonsoft.Json.JsonConvert.SerializeObject(new
                {
                    blueprint = blueprintId,
                    name = ((AdventureMapEntities)blueprintId).ToString(),
                    category = entity.Category.ToString(),
                    categoryKnownToManager = categoryKnown,
                    managerContainer = managerContainer != null,
                    aiComponents = aiEntity != null && aiEntity.AllComponents != null ? aiEntity.AllComponents.Length : -1,
                    components
                });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>Whether the blueprint carries a hostile component, whose Initialize turns the
        /// map entity into a commander.</summary>
        private static bool IsHostileBlueprint(int blueprintId)
        {
            IMapEntityBlueprint blueprint = AdventureBlueprint(blueprintId);
            if (blueprint == null || blueprint.AllComponents == null)
            {
                return false;
            }

            for (int i = 0; i < blueprint.AllComponents.Length; i++)
            {
                if (blueprint.AllComponents[i] != null && blueprint.AllComponents[i].GetType().Name.Contains("Hostile"))
                {
                    return true;
                }
            }

            return false;
        }

        private static IMapEntityBlueprint AdventureBlueprint(int blueprintId)
        {
            IMapEntityManifestRetriever manifests = ProjectContext.Instance == null
                ? null
                : ProjectContext.Instance.Container.TryResolve<IMapEntityManifestRetriever>();
            try
            {
                return manifests != null ? manifests.GetAdventureBlueprint((AdventureMapEntities)blueprintId) : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The refusal for a blueprint carrying the game's DeprecatedComponent, or null.</summary>
        private static string DeprecatedBlueprint(int blueprintId)
        {
            IMapEntityBlueprint blueprint = AdventureBlueprint(blueprintId);
            if (blueprint == null || blueprint.AllComponents == null)
            {
                return null;
            }

            for (int i = 0; i < blueprint.AllComponents.Length; i++)
            {
                if (blueprint.AllComponents[i] is DeprecatedComponent)
                {
                    return "blueprint " + blueprintId + " " + (AdventureMapEntities)blueprintId
                        + " is deprecated; use 68 Hostile or 69 RandomHostile";
                }
            }

            return null;
        }

        /// <summary>What stops a map entity from covering a tile, or null when nothing does.</summary>
        public static string Obstacle(IClientAdventureFacade facade, Vector2Int point)
        {
            if (point.x < 0 || point.y < 0 || point.x >= facade.Level.Width || point.y >= facade.Level.Height)
            {
                return "off the map";
            }

            IMapEntity entity = facade.MapEntities.GetAt(point);
            if (entity != null)
            {
                return "entity " + (string.IsNullOrEmpty(entity.NameKey) ? entity.Name : entity.NameKey) + " #" + entity.Id;
            }

            ICommanderState commander = CommanderAt(facade, point);
            if (commander != null)
            {
                return "commander " + facade.Commanders.GetName(commander.Id);
            }

            if (!CreateAdventureMapEntityCommand.RequestValidator.IsWalkableTerrain(-1, facade, point, null))
            {
                return "impassable";
            }

            return null;
        }

        public static ICommanderState CommanderAt(IClientAdventureFacade facade, Vector2Int point)
        {
            IEnumerable<ICommanderState> all = facade.Commanders.All;
            if (all == null)
            {
                return null;
            }

            foreach (ICommanderState commander in all)
            {
                if (commander != null && commander.IsAlive && commander.Position == point)
                {
                    return commander;
                }
            }

            return null;
        }

        /// <summary>
        /// The tiles a blueprint covers when its origin is on <paramref name="origin"/>: the
        /// origin plus the blueprint's own local blocking points, which is what the spawned
        /// state gets when the request overrides none.
        /// </summary>
        private static List<Vector2Int> Footprint(IClientAdventureFacade facade, int blueprintId, Vector2Int origin)
        {
            List<Vector2Int> points = new List<Vector2Int>();
            points.Add(origin);
            IMapEntityBlueprint blueprint = facade.MapEntities.GetBlueprint((ushort)blueprintId);
            IMapEntityComponentOverridableData[] data = blueprint == null ? null : blueprint.CreateData();
            if (data == null)
            {
                return points;
            }

            for (int i = 0; i < data.Length; i++)
            {
                LocationComponentOverridableData location = data[i] as LocationComponentOverridableData;
                if (location == null || location.LocalBlockingPoints == null)
                {
                    continue;
                }

                for (int j = 0; j < location.LocalBlockingPoints.Length; j++)
                {
                    Vector2Int point = origin + location.LocalBlockingPoints[j];
                    if (!points.Contains(point))
                    {
                        points.Add(point);
                    }
                }
            }

            return points;
        }

        /// <summary>
        /// Resolve a binding from whichever loaded scene context has it; the adventure scene's
        /// bindings (the game, the facade, the selection handler) live there, not in the project
        /// context.
        /// </summary>
        public static T ResolveFromScenes<T>() where T : class
        {
            SceneContext[] contexts = Resources.FindObjectsOfTypeAll<SceneContext>();
            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] == null || contexts[i].Container == null)
                {
                    continue;
                }

                T resolved = contexts[i].Container.TryResolve<T>();
                if (resolved != null)
                {
                    return resolved;
                }
            }

            return null;
        }
    }
}
