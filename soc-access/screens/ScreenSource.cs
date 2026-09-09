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
    /// A source and the adapter over what it finds, paired: the adapter is built once per object the
    /// source answers with and kept while it keeps answering with the same one, so a screen with
    /// SEVERAL sources can ask each of them "are you the one drawing" without building an adapter a
    /// frame. The pairing is keyed on the object read from the game, so a menu the game replaces
    /// gets a new adapter and nothing has to be reset (AGENTS.md, "Screen Resolution").
    ///
    /// Not for an adapter the slot disposes: the pairing would hand out the disposed one again.
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
