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
