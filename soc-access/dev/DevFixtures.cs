using System;
using Lavapotion.Networking;
using Newtonsoft.Json;
using SongsOfConquest.Common.Entities.Adventure;
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
        /// Spawn map entity blueprint <paramref name="blueprintId"/> at a tile through the game's
        /// own debug route (<see cref="CreateAdventureMapEntityCommand"/>); the state appears a
        /// frame later and the mod's "Revealed ..." line confirms it. <c>accepted:false</c> means
        /// the validator refused the tile: read <see cref="DevProbe.TilesAround"/> and pick a
        /// free one.
        /// </summary>
        public static string SpawnAt(int blueprintId, int x, int y)
        {
            try
            {
                return DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    IGame game = ResolveFromScenes<IGame>();
                    if (game == null)
                    {
                        json.WritePropertyName("error");
                        json.WriteValue("no adventure scene is up");
                    }
                    else
                    {
                        bool accepted = game.server.Commands.ProcessServerRequest(
                            new CreateAdventureMapEntityCommand.Request((ushort)blueprintId, new Vector2Int(x, y)));
                        json.WritePropertyName("blueprint");
                        json.WriteValue(blueprintId);
                        json.WritePropertyName("position");
                        json.WriteValue(x + "," + y);
                        json.WritePropertyName("accepted");
                        json.WriteValue(accepted);
                    }

                    json.WriteEndObject();
                });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.Message);
            }
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
