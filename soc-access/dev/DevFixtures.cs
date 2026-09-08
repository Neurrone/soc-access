using System;
using System.Collections.Generic;
using Lavapotion.Networking;
using Newtonsoft.Json;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
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

                    bool accepted = game.server.Commands.ProcessServerRequest(
                        new CreateAdventureMapEntityCommand.Request((ushort)blueprintId, origin));
                    json.WritePropertyName("accepted");
                    json.WriteValue(accepted);
                    json.WriteEndObject();
                });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
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
