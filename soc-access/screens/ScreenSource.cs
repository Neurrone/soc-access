using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The set of loaded scenes, as one integer that changes whenever a scene is loaded or
    /// unloaded. Read once per frame and shared by every source, so a screen's per-frame cost is
    /// a comparison. Keyed on scene handles rather than names: the main menu loads its sub-scenes
    /// additively, and a scene reloaded under the same name is a different set of objects.
    /// </summary>
    public static class LoadedScenes
    {
        /// <summary>The adventure scene, which holds the map and every menu bound in its container.
        /// </summary>
        public const string AdventureScene = "AdventureScene";

        private static int _frame = -1;
        private static int _key;

        public static int Key
        {
            get
            {
                int frame = Time.frameCount;
                if (frame != _frame)
                {
                    _frame = frame;
                    _key = Compute();
                }

                return _key;
            }
        }

        /// <summary>Whether a scene with this name is loaded now; a cheap gate for a source whose
        /// object only ever lives in one scene.</summary>
        public static bool IsLoaded(string sceneName)
        {
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded && scene.name == sceneName)
                {
                    return true;
                }
            }

            return false;
        }

        private static int Compute()
        {
            int key = 17;
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (scene.isLoaded)
                {
                    key = unchecked(key * 31 + scene.handle);
                }
            }

            return key;
        }
    }

    /// <summary>
    /// Where a screen's menu comes from: resolved from the game, memoised hit or miss, and looked
    /// for again only when the set of loaded scenes changes. A hot reload starts a source with no
    /// memo, so the first tick after it resolves as first entry does; nothing has to be recovered.
    /// A found object that Unity has since destroyed reads as a miss until the scenes change.
    ///
    /// The four ways a menu is found (AGENTS.md, "Screen Resolution"): the project container, the
    /// scene container, a field off a resolved owner, and a walk of a menu scene's root objects
    /// gated to the scenes that can hold the object.
    /// </summary>
    public sealed class ScreenSource<T> where T : class
    {
        private readonly Func<T> _resolve;
        private readonly string[] _scenes;
        private int _sceneKey = int.MinValue;
        private bool _probed;
        private T _found;

        private ScreenSource(Func<T> resolve, string[] scenes)
        {
            _resolve = resolve;
            _scenes = scenes;
        }

        /// <summary>The menu, or null. Resolves once per set of loaded scenes.</summary>
        public T Current
        {
            get
            {
                int key = LoadedScenes.Key;
                if (key != _sceneKey)
                {
                    _sceneKey = key;
                    _probed = false;
                    _found = null;
                }

                if (!_probed)
                {
                    _probed = true;
                    _found = Gate() ? Guarded(_resolve) : null;
                }

                return IsAlive(_found) ? _found : null;
            }
        }

        /// <summary>Look again on the next read. For a source whose object the game replaces
        /// within one scene (a menu it re-instantiates); not for a scene change, which the key
        /// already covers.</summary>
        public void Invalidate()
        {
            _probed = false;
            _found = null;
        }

        /// <summary>A binding in the project container: the system menus that exist for the whole
        /// game. Only for <c>NonLazy</c> contracts; resolving a lazy one constructs it.</summary>
        public static ScreenSource<T> FromProject()
        {
            return new ScreenSource<T>(ResolveFromProject, null);
        }

        /// <summary>A binding in the container of a loaded scene, found on that scene's root
        /// objects. Only for <c>NonLazy</c> contracts bound without <c>WhenInjectedInto</c>; those
        /// resolve through their owner with <see cref="FromOwner{TOwner}"/>.</summary>
        public static ScreenSource<T> FromScene(params string[] scenes)
        {
            return new ScreenSource<T>(() => ResolveFromScene(scenes), scenes);
        }

        /// <summary>A menu an owner holds in a field: read through the owner's own source so both
        /// share one memo lifetime.</summary>
        public static ScreenSource<T> FromOwner<TOwner>(ScreenSource<TOwner> owner, Func<TOwner, T> read)
            where TOwner : class
        {
            return new ScreenSource<T>(() =>
            {
                TOwner o = owner.Current;
                return o == null ? null : read(o);
            }, null);
        }

        /// <summary>A component on a ROOT object of one of the named scenes, which is where the
        /// scene's installers sit (they share the SceneContext's own object). A handful of
        /// <c>GetComponent</c> calls rather than the subtree walk below.</summary>
        public static ScreenSource<T> FromSceneRoot(params string[] scenes)
        {
            return new ScreenSource<T>(() => OnRoots(scenes), scenes);
        }

        /// <summary>A binding in the container of a <c>GameObjectContext</c> - a sub-container the
        /// scene container cannot see into, which is where the adventure HUD installers put their
        /// bindings. Shares one walk of the scene's contexts with every other source that asks
        /// (<see cref="SceneSubContainers"/>).</summary>
        public static ScreenSource<T> FromSubContainer(params string[] scenes)
        {
            return new ScreenSource<T>(SceneSubContainers.Resolve<T>, scenes);
        }

        /// <summary>A scene object nothing binds or holds: one walk of the named scenes' root
        /// objects. Gated to those scenes because the walk costs milliseconds on a big scene and
        /// a miss costs the same as a hit.</summary>
        public static ScreenSource<T> FromRootWalk(params string[] scenes)
        {
            return new ScreenSource<T>(() => WalkRoots(scenes), scenes);
        }

        /// <summary>Any resolver of the caller's own; the memo and the scene gate still apply.</summary>
        public static ScreenSource<T> From(Func<T> resolve, params string[] scenes)
        {
            return new ScreenSource<T>(resolve, scenes == null || scenes.Length == 0 ? null : scenes);
        }

        private bool Gate()
        {
            if (_scenes == null)
            {
                return true;
            }

            for (int i = 0; i < _scenes.Length; i++)
            {
                if (LoadedScenes.IsLoaded(_scenes[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static T Guarded(Func<T> resolve)
        {
            try
            {
                return resolve();
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("ScreenSource<" + typeof(T).Name + "> " + exception.Message);
                return null;
            }
        }

        private static bool IsAlive(T value)
        {
            if (value == null)
            {
                return false;
            }

            UnityEngine.Object unityObject = value as UnityEngine.Object;
            return unityObject == null ? true : unityObject != null;
        }

        private static T ResolveFromProject()
        {
            ProjectContext context = ProjectContext.HasInstance ? ProjectContext.Instance : null;
            DiContainer container = context != null ? context.Container : null;
            return container != null ? container.TryResolve<T>() : null;
        }

        private static T ResolveFromScene(string[] scenes)
        {
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !Named(scene, scenes))
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    SceneContext context = roots[r].GetComponent<SceneContext>();
                    if (context == null || context.Container == null)
                    {
                        continue;
                    }

                    T found = context.Container.TryResolve<T>();
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static T OnRoots(string[] scenes)
        {
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !Named(scene, scenes))
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    T found = roots[r].GetComponent(typeof(T)) as T;
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static T WalkRoots(string[] scenes)
        {
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded || !Named(scene, scenes))
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    T found = roots[r].GetComponentInChildren(typeof(T), true) as T;
                    if (found != null)
                    {
                        return found;
                    }
                }
            }

            return null;
        }

        private static bool Named(Scene scene, string[] scenes)
        {
            if (scenes == null || scenes.Length == 0)
            {
                return true;
            }

            for (int i = 0; i < scenes.Length; i++)
            {
                if (scene.name == scenes[i])
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>
    /// The Zenject SUB-CONTAINERS of the loaded scenes: a <c>GameObjectContext</c> installs its
    /// bindings into a container of its own, and neither the scene container nor the project
    /// container can see into one, so a menu bound there (the kingdom HUD, the commander HUD, the
    /// chat window and button) is resolved from the sub-container that binds it.
    ///
    /// One walk of every loaded scene's root objects answers all of them, and it is memoised on the
    /// set of loaded scenes exactly as a <see cref="ScreenSource{T}"/> is, so the walk happens once
    /// per scene load however many sources ask.
    /// </summary>
    public static class SceneSubContainers
    {
        private static readonly DiContainer[] None = new DiContainer[0];

        private static int _sceneKey = int.MinValue;
        private static DiContainer[] _containers = None;

        /// <summary>The first sub-container that answers to this contract, or null.</summary>
        public static T Resolve<T>() where T : class
        {
            DiContainer[] containers = All;
            for (int i = 0; i < containers.Length; i++)
            {
                T found = containers[i].TryResolve<T>();
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>The first sub-container's <c>IInitializable</c> of this type. For a service the
        /// game binds with <c>BindInterfacesTo</c> and no interface of its own: the chat window's
        /// and the chat button's behaviours are reachable no other way.</summary>
        public static T ResolveInitializable<T>() where T : class
        {
            DiContainer[] containers = All;
            for (int i = 0; i < containers.Length; i++)
            {
                List<IInitializable> initializables = containers[i].ResolveAll<IInitializable>();
                for (int j = 0; j < initializables.Count; j++)
                {
                    T match = initializables[j] as T;
                    if (match != null)
                    {
                        return match;
                    }
                }
            }

            return null;
        }

        private static DiContainer[] All
        {
            get
            {
                int key = LoadedScenes.Key;
                if (key != _sceneKey)
                {
                    // The walk first, so a throw leaves neither the key nor the answer half written.
                    _containers = Walk();
                    _sceneKey = key;
                }

                return _containers;
            }
        }

        private static DiContainer[] Walk()
        {
            List<DiContainer> containers = new List<DiContainer>();
            List<GameObjectContext> contexts = new List<GameObjectContext>();
            int count = SceneManager.sceneCount;
            for (int i = 0; i < count; i++)
            {
                Scene scene = SceneManager.GetSceneAt(i);
                if (!scene.isLoaded)
                {
                    continue;
                }

                GameObject[] roots = scene.GetRootGameObjects();
                for (int r = 0; r < roots.Length; r++)
                {
                    contexts.Clear();
                    roots[r].GetComponentsInChildren(true, contexts);
                    for (int c = 0; c < contexts.Count; c++)
                    {
                        DiContainer container = contexts[c] == null ? null : contexts[c].Container;
                        if (container != null)
                        {
                            containers.Add(container);
                        }
                    }
                }
            }

            return containers.Count == 0 ? None : containers.ToArray();
        }
    }

    /// <summary>
    /// A source and the adapter over what it finds, paired: the adapter is built once per object the
    /// source answers with and kept while it keeps answering with the same one, so a screen with
    /// SEVERAL sources can ask each of them "are you the one drawing" without building an adapter a
    /// frame. The pairing is keyed on the object read from the game, so a menu the game replaces
    /// gets a new adapter and nothing has to be reset (AGENTS.md, "Screen Resolution").
    ///
    /// Not for an adapter the slot disposes, unless everything its <c>Dispose</c> lets go of is put
    /// back by the screen's <c>Adapt</c>: the pairing hands the disposed one out again the next time
    /// its source answers with the same object (the message dialog's popup adapters, whose Dispose
    /// only detaches the submit handler Adapt attaches).
    /// </summary>
    public sealed class AdaptedSource<TMenu, TAdapter>
        where TMenu : class
        where TAdapter : class
    {
        private readonly ScreenSource<TMenu> _source;
        private readonly Func<TMenu, TAdapter> _adapt;
        private TMenu _of;
        private TAdapter _adapter;

        public AdaptedSource(ScreenSource<TMenu> source, Func<TMenu, TAdapter> adapt)
        {
            _source = source;
            _adapt = adapt;
        }

        /// <summary>The adapter over what the source finds now, or null.</summary>
        public TAdapter Current
        {
            get
            {
                TMenu menu = _source.Current;
                if (!ReferenceEquals(menu, _of))
                {
                    _of = menu;
                    _adapter = menu == null ? null : _adapt(menu);
                }

                return _adapter;
            }
        }
    }
}
