using System;
using System.Collections.Generic;
using System.Globalization;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Loader.Dev;
using SongsOfConquestAccess.Screens;
using Zenject;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// The questions a test asks over and over, each as one compile-checked call.
    ///
    /// POST /eval can reach all of this already - that is the point of the REPL - but reaching it
    /// there means writing the traversal by hand every time: a null check, two casts and a string
    /// concatenation, in a language with no `using` directives and one statement per request. Every
    /// one of those questions has exactly one right answer, and every hand-written traversal is a
    /// chance to get it subtly wrong and believe the result. So they live here instead, in a file the
    /// compiler checks, and /eval bodies become <c>SongsOfConquestAccess.Dev.DevProbe.State()</c>.
    ///
    /// Everything returns JSON, and everything that can fail fails as <c>{"error": ...}</c> rather
    /// than by throwing: a probe called from a wait-loop must always answer.
    ///
    /// Main-thread only - all of it reads live game state.
    /// </summary>
    public static class DevProbe
    {
        /// <summary>The screen on top of the mod's stack, or null when there is none.</summary>
        public static string Screen()
        {
            return Guarded(json =>
            {
                SongsOfConquestAccess.Screens.Screen top = Top();
                json.WritePropertyName("screen");
                json.WriteValue(top == null ? null : top.GetType().Name);
            });
        }

        /// <summary>The whole stack, bottom first.</summary>
        public static string Stack()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("stack");
                json.WriteStartArray();
                IReadOnlyList<SongsOfConquestAccess.Screens.Screen> stack = Stacked();
                if (stack != null)
                {
                    for (int i = 0; i < stack.Count; i++)
                    {
                        json.WriteValue(stack[i].GetType().Name);
                    }
                }

                json.WriteEndArray();
            });
        }

        /// <summary>
        /// One word for where the game is, which is what a launcher script waits on. Decided from the
        /// stack in a fixed order, because several of these are true at once: a popup over the
        /// adventure map is a dialog, and the loading screen wins over everything because nothing else
        /// on the stack can be acted on while it is up.
        /// </summary>
        public static string State()
        {
            return Guarded(json =>
            {
                IReadOnlyList<SongsOfConquestAccess.Screens.Screen> stack = Stacked();
                SongsOfConquestAccess.Screens.Screen top = stack == null || stack.Count == 0 ? null : stack[stack.Count - 1];
                json.WritePropertyName("state");
                json.WriteValue(Describe(stack, top));
                json.WritePropertyName("top");
                json.WriteValue(top == null ? null : top.GetType().Name);
            });
        }

        /// <summary>
        /// Press "any key" on the loading-complete screen the way the game's own key handling does
        /// (<see cref="Adapters.LoadingScreenAdapter.Continue"/>). <c>continued</c> is false when
        /// that screen is not up or the scene loader is not yet waiting for it.
        /// </summary>
        public static string ContinueLoading()
        {
            return Guarded(json =>
            {
                json.WritePropertyName("continued");
                json.WriteValue(PressContinue());
            });
        }

        /// <summary>The shared implementation: the loading-complete screen the mod has on its stack,
        /// continued natively. False when there is none to continue.</summary>
        internal static bool PressContinue()
        {
            ScreenManager screens = SocAccessMod.Instance?.ScreenManager;
            LoadingCompleteScreen loading = screens == null ? null : screens.Get<LoadingCompleteScreen>();
            return loading != null && loading.Adapter != null && loading.Adapter.Continue();
        }

        /// <summary>
        /// The saves the game's load menu would list, newest first - name, when it was written, and
        /// whether the game reads it as corrupt.
        ///
        /// It asks the same service the menu asks (<c>IGameLoader.ListAll</c>, which is what the main
        /// menu's Continue button uses), so no menu has to be opened and nothing on screen moves. The
        /// listing is a UniTask; local file listing completes inside the call, and an answer that
        /// somehow did not is reported rather than waited for, because a probe may not block the frame
        /// the continuation would need.
        /// </summary>
        public static string Saves()
        {
            return Guarded(json =>
            {
                IGameLoader loader = ProjectContext.Instance == null
                    ? null
                    : ProjectContext.Instance.Container.TryResolve<IGameLoader>();
                if (loader == null)
                {
                    json.WritePropertyName("error");
                    json.WriteValue("the game's save loader is not up yet");
                    return;
                }

                List<LoadGameDefinition> saves = new List<LoadGameDefinition>();
                UniTask listing = loader.ListAll(AdventureGameMode.Skirmish, saves);
                if (listing.Status != UniTaskStatus.Succeeded)
                {
                    json.WritePropertyName("error");
                    json.WriteValue("the save listing did not complete within the call (" + listing.Status + ")");
                    return;
                }

                saves.Sort((left, right) => right.LastWriteTime.CompareTo(left.LastWriteTime));
                json.WritePropertyName("saves");
                json.WriteStartArray();
                for (int i = 0; i < saves.Count; i++)
                {
                    json.WriteStartObject();
                    json.WritePropertyName("name");
                    json.WriteValue(saves[i].SaveName);
                    json.WritePropertyName("written");
                    json.WriteValue(
                        saves[i]
                            .LastWriteTime.ToUniversalTime()
                            .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture)
                    );
                    json.WritePropertyName("corrupt");
                    json.WriteValue(saves[i].Corrupt);
                    json.WriteEndObject();
                }

                json.WriteEndArray();
            });
        }

        private static string Describe(IReadOnlyList<SongsOfConquestAccess.Screens.Screen> stack, SongsOfConquestAccess.Screens.Screen top)
        {
            if (top == null)
            {
                return "none";
            }

            if (Present(stack, "LoadingCompleteScreen"))
            {
                return "loading";
            }

            string topName = top.GetType().Name;
            if (topName == "MessageDialogScreen"
                || topName == "TooltipActionsMenuScreen"
                || topName.EndsWith("PopupScreen", StringComparison.Ordinal))
            {
                return "dialog";
            }

            if (Present(stack, "CombatScreen"))
            {
                return "combat";
            }

            if (Present(stack, "AdventureMapScreen"))
            {
                // Only the map itself is "ingame": a menu stacked over it (the load menu, the
                // wielder sheet, options) is something a launcher's wait must not mistake for a
                // playable map, and it is dismissed the way a dialog is.
                return topName == "AdventureMapScreen" ? "ingame" : "dialog";
            }

            for (int i = 0; i < stack.Count; i++)
            {
                string name = stack[i].GetType().Name;
                if (name.StartsWith("AdventureLobby", StringComparison.Ordinal)
                    && name.EndsWith("Screen", StringComparison.Ordinal))
                {
                    return "lobby";
                }
            }

            return "menu";
        }

        private static bool Present(IReadOnlyList<SongsOfConquestAccess.Screens.Screen> stack, string typeName)
        {
            for (int i = 0; i < stack.Count; i++)
            {
                if (stack[i].GetType().Name == typeName)
                {
                    return true;
                }
            }

            return false;
        }

        private static SongsOfConquestAccess.Screens.Screen Top()
        {
            ScreenManager screens = SocAccessMod.Instance?.ScreenManager;
            return screens == null ? null : screens.CurrentScreen;
        }

        private static IReadOnlyList<SongsOfConquestAccess.Screens.Screen> Stacked()
        {
            ScreenManager screens = SocAccessMod.Instance?.ScreenManager;
            return screens == null ? null : screens.Stack;
        }

        private static string Guarded(Action<JsonTextWriter> body)
        {
            try
            {
                return DevJson.Write(json =>
                {
                    json.WriteStartObject();
                    body(json);
                    json.WriteEndObject();
                });
            }
            catch (Exception e)
            {
                return Err(e.Message);
            }
        }

        private static string Err(string message)
        {
            return DevJson.Error(message);
        }
    }
}
