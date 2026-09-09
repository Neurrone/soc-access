using System;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// A screen that FINDS ITS OWN MENU, every frame, and reads it through an adapter. The two halves
    /// are the whole contract (AGENTS.md, "Screen Resolution"): <see cref="ResolveMenu"/> answers with
    /// the game object this screen describes now, and <see cref="Adapt"/> wraps one such object once.
    /// Nothing tells a screen when its menu arrives or goes; there is no slot for a hook to write.
    ///
    /// The slot is on the SCREEN rather than on the adapter because several screens have more than
    /// one source behind one page: the message dialog's six, the story text's three, the troop pages'
    /// three hosts. There the source answers with the ADAPTER over whichever of them is drawing, and
    /// <see cref="Adapt"/> is the cast; a screen with one source answers with the menu itself.
    ///
    /// A null slot means the page is not showing: <c>IsActive</c> answers false and <c>Build</c>
    /// declares nothing, so nothing has to be null-guarded twice.
    /// </summary>
    public abstract class LiveScreen<TAdapter> : GraphScreen where TAdapter : class, IPresent
    {
        private TAdapter _live;

        // The menu the current adapter was built over, so a menu the source keeps answering costs
        // one reference comparison a frame and a new instance gets a new adapter.
        private object _menuOfLive;

        /// <summary>What the screen is reading now, or null. Written only by <see cref="SyncLive"/>
        /// and <see cref="Forget"/>.</summary>
        public TAdapter Live
        {
            get { return _live; }
            private set
            {
                TAdapter previous = _live;
                _live = value;
                if (!ReferenceEquals(previous, value))
                {
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

                // A PAGE THAT TURNS IN PLACE says its new name itself: the screen never left, so the
                // manager, which announces only on arrival, would say nothing. One popup replacing
                // another and a story line handed to a different host are both this.
                SayNameWhileFocused();
            }
        }

        /// <summary>The game object this screen reads now, or null: what the screen's
        /// <see cref="ScreenSource{T}"/> - or the first of several - answers with this frame.
        /// Memoised by the source, so this is a comparison and not a search.</summary>
        protected abstract object ResolveMenu();

        /// <summary>The adapter over a menu the source found. Called once per menu instance, never
        /// per frame.</summary>
        protected abstract TAdapter Adapt(object menu);

        /// <summary>The source found the menu and the menu is drawn. A screen with a condition of its
        /// own overrides this and calls back into it.</summary>
        public override bool IsActive()
        {
            SyncLive();
            return Live != null && Live.IsPresent();
        }

        public override void Forget()
        {
            Live = null;
            _menuOfLive = null;
        }

        /// <summary>Point the slot at what the source finds now. A vanished menu clears the slot, a
        /// new instance replaces the adapter, the same instance costs a comparison.</summary>
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
