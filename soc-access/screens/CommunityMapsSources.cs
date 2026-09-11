using System;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIO.Util;
using ModIOBrowser.Implementation;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Where the six community-maps screens find the panel they read. Unlike every other screen in
    /// the mod, these do NOT use <see cref="ScreenSource{T}"/>, and the reason is the browser's
    /// lifetime: mod.io's browser is a prefab the game instantiates into the scene it is already in,
    /// so it opens and closes without the set of loaded scenes changing. A
    /// <see cref="ScreenSource{T}"/> memoises its answer hit or miss and looks again only when that
    /// set changes, so a source over the browser would cache "closed" for the rest of the session.
    ///
    /// A memo would buy nothing here anyway. Every panel is a mod.io singleton, so the read IS a
    /// static field access: cheaper than the comparison a memo would cost.
    ///
    /// THE GUARDS ARE NOT OPTIONAL. <c>Browser</c> is a <c>MonoSingleton</c> whose <c>Instance</c>
    /// THROWS when nothing has instanced it, and every panel below is a
    /// <c>SelfInstancingMonoSingleton</c> whose <c>Instance</c> runs a <c>FindObjectOfType</c> and
    /// then CREATES a game object when it finds none - so reading one while the browser is shut
    /// would leave a stray panel behind for the rest of the session. Every accessor here asks
    /// <c>Browser.IsOpen</c> first and then <c>SingletonIsInstantiated()</c>, which answers off the
    /// static field and instantiates nothing.
    ///
    /// Three of mod.io's panels (<c>SearchPanel</c>, <c>ModioContextMenu</c>,
    /// <c>NotificationPopup</c>) are <c>internal</c>, so they are reached by reflection over the
    /// same two inherited statics; the lookups are resolved once into delegates.
    /// </summary>
    public static class CommunityMapsSources
    {
        private static readonly Singleton SearchPanelSingleton =
            Singleton.For("ModIOBrowser.Implementation.SearchPanel");
        private static readonly Singleton ContextMenuSingleton =
            Singleton.For("ModIOBrowser.Implementation.ModioContextMenu");
        private static readonly Singleton NotificationPopupSingleton =
            Singleton.For("ModIOBrowser.Implementation.NotificationPopup");
        private static readonly Singleton InputNavigationSingleton =
            Singleton.For("ModIOBrowser.InputNavigation");
        private static readonly Singleton SelectionOverlaySingleton =
            Singleton.For("ModIOBrowser.Implementation.SelectionOverlayHandler");
        private static readonly FieldInfo SearchResultOverlayField = SelectionOverlayField("SearchResultListItemOverlay");

        /// <summary>Whether the browser is up at all. Nothing below answers anything while this is
        /// false, and nothing below may be read without asking this first.</summary>
        public static bool IsOpen
        {
            get { return Browser.IsOpen; }
        }

        /// <summary>One of mod.io's public panels while the browser is up, or null. The browser is
        /// asked first and the instantiated flag second, because reading <c>Instance</c> on a
        /// <c>SelfInstancingMonoSingleton</c> that has none CREATES one.</summary>
        private static T Panel<T>() where T : SelfInstancingMonoSingleton<T>
        {
            return IsOpen && SelfInstancingMonoSingleton<T>.SingletonIsInstantiated()
                ? SelfInstancingMonoSingleton<T>.Instance
                : null;
        }

        /// <summary>The Browse page, which also owns the tab pair drawn over both pages.</summary>
        public static Home Home
        {
            get { return Panel<Home>(); }
        }

        /// <summary>The Collection page.</summary>
        public static Collection Collection
        {
            get { return Panel<Collection>(); }
        }

        /// <summary>The details page one map or mod opens.</summary>
        public static Details Details
        {
            get { return Panel<Details>(); }
        }

        /// <summary>The search results page.</summary>
        public static SearchResults SearchResults
        {
            get { return Panel<SearchResults>(); }
        }

        /// <summary>The report popup.</summary>
        public static Reporting Reporting
        {
            get { return Panel<Reporting>(); }
        }

        /// <summary>The authentication flow, which walks several panels under one object.</summary>
        public static AuthenticationPanels AuthenticationPanels
        {
            get { return Panel<AuthenticationPanels>(); }
        }

        /// <summary>The download queue popup.</summary>
        public static DownloadQueue DownloadQueue
        {
            get { return Panel<DownloadQueue>(); }
        }

        /// <summary>The five-digit code box the email flow ends on.</summary>
        public static KeyInput5DigitsUi KeyInput
        {
            get { return Panel<KeyInput5DigitsUi>(); }
        }

        /// <summary>The bar across the top of both pages, which draws the "Search &amp; filter"
        /// wording the screens read their own titles from.</summary>
        public static NavBar NavBar
        {
            get { return Panel<NavBar>(); }
        }

        /// <summary>mod.io's own translation table, which is what its wording is read through.
        /// </summary>
        public static TranslationManager Translations
        {
            get { return Panel<TranslationManager>(); }
        }

        /// <summary>The search and filter panel. Internal to mod.io, so it comes back as
        /// <c>object</c> and the adapter reads it by reflection, as it already did.</summary>
        public static object SearchPanel
        {
            get { return SearchPanelSingleton.Current; }
        }

        /// <summary>The "More options" menu a list item opens.</summary>
        public static object ContextMenu
        {
            get { return ContextMenuSingleton.Current; }
        }

        /// <summary>The notification popup mod.io puts up over everything.</summary>
        public static object NotificationPopup
        {
            get { return NotificationPopupSingleton.Current; }
        }

        /// <summary>mod.io's own selection driver, which is how a row is focused natively.</summary>
        public static object InputNavigation
        {
            get { return InputNavigationSingleton.Current; }
        }

        /// <summary>The floating card mod.io moves onto the selected search result
        /// (<c>SelectionOverlayHandler.MoveSelection</c> calls <c>Setup</c> on it), or null while no
        /// row is selected. It is the handler's own field, so which row it replicates is read from
        /// the game rather than looked for in the scene.</summary>
        public static object SearchResultOverlay
        {
            get
            {
                object handler = SelectionOverlaySingleton.Current;
                object overlay = handler != null && SearchResultOverlayField != null
                    ? SearchResultOverlayField.GetValue(handler)
                    : null;
                Component component = overlay as Component;
                return component != null && component.gameObject.activeInHierarchy ? component : null;
            }
        }

        private static FieldInfo SelectionOverlayField(string name)
        {
            Type type = AccessTools.TypeByName("ModIOBrowser.Implementation.SelectionOverlayHandler");
            return type != null ? AccessTools.Field(type, name) : null;
        }

        /// <summary>The game object of a component that came back as <c>object</c>.</summary>
        public static GameObject GameObjectOf(object component)
        {
            Component behaviour = component as Component;
            return behaviour != null ? behaviour.gameObject : null;
        }

        /// <summary>One of mod.io's internal singletons, reached through the two public statics its
        /// generic base declares. Resolved once per mod load; the per-frame cost is a delegate call
        /// and a static field read.</summary>
        private sealed class Singleton
        {
            private readonly Func<bool> _instantiated;
            private readonly Func<object> _instance;

            private Singleton(Func<bool> instantiated, Func<object> instance)
            {
                _instantiated = instantiated;
                _instance = instance;
            }

            public object Current
            {
                get
                {
                    if (!Browser.IsOpen || _instantiated == null || _instance == null || !_instantiated())
                    {
                        return null;
                    }

                    return _instance();
                }
            }

            public static Singleton For(string typeName)
            {
                Type type = AccessTools.TypeByName(typeName);
                if (type == null)
                {
                    return new Singleton(null, null);
                }

                const BindingFlags flags = BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy;
                MethodInfo instantiated = null;
                PropertyInfo instance = null;
                // Both are declared on the generic base, so the walk up is explicit rather than left
                // to FlattenHierarchy.
                for (Type current = type; current != null && (instantiated == null || instance == null); current = current.BaseType)
                {
                    if (instantiated == null)
                    {
                        instantiated = current.GetMethod("SingletonIsInstantiated", flags, null, Type.EmptyTypes, null);
                    }

                    if (instance == null)
                    {
                        instance = current.GetProperty("Instance", flags);
                    }
                }

                MethodInfo getter = instance != null ? instance.GetGetMethod(true) : null;
                if (instantiated == null || getter == null)
                {
                    return new Singleton(null, null);
                }

                return new Singleton(
                    () => (bool)instantiated.Invoke(null, null),
                    () => getter.Invoke(null, null));
            }
        }
    }
}
