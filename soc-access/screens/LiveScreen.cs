using System;
namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// A screen with a SLOT: the game object it is currently reading. Every screen is registered
    /// once and lives for the whole mod load, so the menu it describes cannot be a constructor
    /// argument any more - the detector writes it here instead, and clears it
    /// (<see cref="Forget"/>) when the game takes the menu away.
    ///
    /// The slot is on the SCREEN rather than on the adapter because several screens have more than
    /// one adapter class behind one slot: the message dialog's six sources, the story text's three,
    /// the troop pages' three hosts, the two tutorials. There the slot's type is the interface.
    ///
    /// A null slot means the page is not showing: <c>IsActive</c> answers false and <c>Build</c>
    /// declares nothing, so nothing has to be null-guarded twice.
    /// </summary>
    public abstract class LiveScreen<TAdapter> : GraphScreen where TAdapter : class
    {
        private TAdapter _live;

        /// <summary>What the screen is reading now, or null. Written by the detector's readiness
        /// handlers and by <c>Recover</c> after a hot reload.</summary>
        public TAdapter Live
        {
            get { return _live; }
            set
            {
                TAdapter previous = _live;
                _live = value;
                if (!ReferenceEquals(previous, value))
                {
                    OnLiveChanged(previous);
                    // An adapter lives as long as the menu instance it wraps; what it attached to
                    // the game (a handler, a native subscription) goes with it. Per-menu state
                    // belongs on the adapter for the same reason (AGENTS.md, Screen Resolution).
                    IDisposable disposable = previous as IDisposable;
                    if (disposable != null)
                    {
                        try
                        {
                            disposable.Dispose();
                        }
                        catch (Exception exception)
                        {
                            SocAccessMod.Instance?.LogWarning(Key + ": disposing the previous adapter threw: " + exception.Message);
                        }
                    }
                }

                // A PAGE THAT TURNS IN PLACE says its new name itself. The screen never left, so
                // nothing else would: the manager announces only on arrival.
                SayNameWhileFocused();
            }
        }

        /// <summary>Point the slot at what the game has just made ready. The same as writing
        /// <see cref="Live"/>, spelled as a call so the detector can reach it through <c>?.</c>.
        /// </summary>
        public void Show(TAdapter adapter)
        {
            Live = adapter;
        }

        /// <summary>The slot has been pointed at a different object. For a screen that has to let go
        /// of the previous one - a handler attached to it, a cursor built over it.</summary>
        public virtual void OnLiveChanged(TAdapter previous)
        {
        }

        public override void Forget()
        {
            Live = null;
            _menuOfLive = null;
        }

        // ---- self-resolution (AGENTS.md, "Screen Resolution") ----

        // The menu the current adapter was built over, so a menu the source keeps answering costs
        // one reference comparison a frame and a new instance gets a new adapter.
        private object _menuOfLive;

        /// <summary>The game object this screen reads now, or null: the memoised answer of the
        /// screen's <see cref="ScreenSource{T}"/>. A screen that resolves itself overrides this;
        /// a detector-fed screen keeps the default, and the detector writes <see cref="Live"/>.</summary>
        protected virtual object ResolveMenu()
        {
            return null;
        }

        /// <summary>The adapter over a menu the source found. Called once per menu instance, never
        /// per frame.</summary>
        protected virtual TAdapter Adapt(object menu)
        {
            return null;
        }

        /// <summary>Point the slot at what the source finds now; the first line of a
        /// self-resolving screen's <c>IsActive</c>. A vanished menu clears the slot, a new instance
        /// replaces the adapter, the same instance costs a comparison. A source that answers null
        /// while the slot was never source-written leaves the slot alone, which is how a
        /// detector-fed screen and a self-resolving one share this base.</summary>
        protected void SyncLive()
        {
            object menu = ResolveMenu();
            if (ReferenceEquals(menu, _menuOfLive))
            {
                return;
            }

            _menuOfLive = menu;
            Live = menu == null ? null : Adapt(menu);
        }

        /// <summary>Point the REGISTERED singleton's slot at what a one-time scan found - what every
        /// screen's <c>Recover</c> ends with after a hot reload. A null adapter does nothing: the page
        /// is not showing and the poll decides that anyway.</summary>
        public static void Recovered<TScreen>(TAdapter adapter) where TScreen : LiveScreen<TAdapter>
        {
            if (adapter == null)
            {
                return;
            }

            ScreenManager manager = SocAccessMod.Instance == null ? null : SocAccessMod.Instance.ScreenManager;
            TScreen screen = manager == null ? null : manager.Registered<TScreen>();
            if (screen != null)
            {
                screen.Live = adapter;
            }
        }

        private void SayNameWhileFocused()
        {
            UI.GraphNavigator navigator = Navigator;
            if (navigator != null && ReferenceEquals(navigator.Screen, this))
            {
                SayNameIfChanged();
            }
        }
    }
}
