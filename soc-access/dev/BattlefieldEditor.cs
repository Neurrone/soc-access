using System;
using System.Reflection;
using Newtonsoft.Json;
using SongsOfConquest;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Common.Map;
using SongsOfConquest.Server.Adventure.Map.Provider;
using SongsOfConquestAccess.Loader.Dev;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace SongsOfConquestAccess.Dev
{
    /// <summary>
    /// Development only: the map editor as a renderer of battlefield layouts. The battle scene draws
    /// a layout's props only inside a battle; the editor draws them for any layout, with no troops
    /// and nothing to leave, once its context is Battle. The editor's types live in an assembly the
    /// mod does not reference, so they are reached by name. dump-battlefields.ps1 drives these calls
    /// from the main menu and takes the frames through the loader's /screenshot route.
    /// </summary>
    public static class BattlefieldEditor
    {
        private const string EditorAssembly = "Lavapotion.SongsOfConquest.LevelEditor.Runtime";

        /// <summary>From the main menu: put the editor into its Battle context and load it. The
        /// start scene hands over to the editor scene by itself a few seconds later.</summary>
        public static string Open()
        {
            try
            {
                ISceneLoader loader = ProjectContext.Instance.Container.TryResolve<ISceneLoader>();
                if (loader == null)
                {
                    return DevJson.Error("no ISceneLoader");
                }

                MapEditorContextController.SetContext(ContextIdentifier.Battle);
                loader.Load(SceneType.LevelEditorContextScene, new SceneLoaderOptions { UseLoadingScreen = false });
                return DevJson.Ok();
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>Back to the main menu, the editor's own File menu route, and the editor context
        /// back to Adventure so the owner's next visit to the editor is the one they expect.</summary>
        public static string Close()
        {
            try
            {
                ISceneLoader loader = ProjectContext.Instance.Container.TryResolve<ISceneLoader>();
                if (loader == null)
                {
                    return DevJson.Error("no ISceneLoader");
                }

                MapEditorContextController.ResetContext();
                loader.Load(MainMenuSceneType.MainMenu, SceneLoaderOptions.NoLoadingScreen);
                return DevJson.Ok();
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>Which scene is up, and whether the editor's facade answers yet.</summary>
        public static string State()
        {
            try
            {
                ISceneLoader loader = ProjectContext.Instance.Container.TryResolve<ISceneLoader>();
                return JsonConvert.SerializeObject(new
                {
                    scene = loader != null && loader.Current != null ? loader.Current.SceneName : null,
                    state = loader != null ? loader.State.ToString() : null,
                    context = MapEditorContextController.GetContext().ToString(),
                    ready = Facade() != null
                });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>Load one layout, keyed "LevelType/PathName", into the open editor.</summary>
        public static string Load(string key)
        {
            try
            {
                int slash = key.IndexOf('/');
                if (slash <= 0)
                {
                    return DevJson.Error("key is LevelType/PathName");
                }

                LevelType type = (LevelType)Enum.Parse(typeof(LevelType), key.Substring(0, slash));
                string name = key.Substring(slash + 1);
                IInternalLevelSerializer levels = ProjectContext.Instance.Container.TryResolve<IInternalLevelSerializer>();
                MapFormat map = levels != null ? levels.Load(type, name) : null;
                if (map == null)
                {
                    return DevJson.Error("no such layout: " + key);
                }

                object facade = Facade();
                if (facade == null)
                {
                    return DevJson.Error("the editor is not open");
                }

                object serialization = facade.GetType().GetProperty("Serialization").GetValue(facade, null);
                MethodInfo load = serialization.GetType().GetMethod("LoadLevel", new[] { typeof(MapFormat), typeof(LevelType), typeof(bool) });
                if (load == null)
                {
                    return DevJson.Error("no LoadLevel(MapFormat, LevelType, bool) on " + serialization.GetType().FullName);
                }

                load.Invoke(serialization, new object[] { map, type, true });
                return JsonConvert.SerializeObject(new { key, size = map.Metadata.Size.x + "x" + map.Metadata.Size.y });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>Hide or show every canvas of the editor scene, for a frame of the board alone.</summary>
        public static string Ui(bool visible)
        {
            try
            {
                Scene scene = SceneManager.GetSceneByName(SceneType.MapEditor.SceneName);
                if (!scene.isLoaded)
                {
                    return DevJson.Error("the editor scene is not loaded");
                }

                int count = 0;
                GameObject[] roots = scene.GetRootGameObjects();
                for (int i = 0; i < roots.Length; i++)
                {
                    Canvas[] canvases = roots[i].GetComponentsInChildren<Canvas>(true);
                    for (int j = 0; j < canvases.Length; j++)
                    {
                        canvases[j].enabled = visible;
                        count++;
                    }
                }

                return JsonConvert.SerializeObject(new { visible, canvases = count });
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        /// <summary>The editor camera's zoom, 0 closest to 1 furthest, through the call the game's
        /// own debug console makes.</summary>
        public static string Zoom(float time)
        {
            try
            {
                AbstractCameraController camera = Resolve("SongsOfConquest.LevelEditor.ILevelEditorCameraController") as AbstractCameraController;
                if (camera == null)
                {
                    return DevJson.Error("no editor camera controller");
                }

                camera.SetZoom(time);
                return DevJson.Ok();
            }
            catch (Exception e)
            {
                return DevJson.Error(e.ToString());
            }
        }

        private static object Facade()
        {
            return Resolve("SongsOfConquest.LevelEditor.ILevelEditorFacade");
        }

        private static object Resolve(string typeName)
        {
            Type type = Type.GetType(typeName + ", " + EditorAssembly);
            if (type == null)
            {
                return null;
            }

            SceneContext[] contexts = Resources.FindObjectsOfTypeAll<SceneContext>();
            for (int i = 0; i < contexts.Length; i++)
            {
                if (contexts[i] == null || contexts[i].Container == null)
                {
                    continue;
                }

                try
                {
                    object resolved = contexts[i].Container.TryResolve(type);
                    if (resolved != null)
                    {
                        return resolved;
                    }
                }
                catch (Exception)
                {
                }
            }

            return null;
        }
    }
}
