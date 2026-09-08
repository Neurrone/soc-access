using System.Collections.Generic;
using SongsOfConquestAccess.Buffers;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// One page of the game made navigable. A screen answers two questions: is the game showing me
    /// right now (<see cref="IsActive"/>), and what is on me (<c>GraphScreen.Build</c>).
    ///
    /// Screens are REGISTERED once and polled every frame (<see cref="ScreenManager"/>) rather than
    /// pushed and popped by the detector, so there is no way for the mod to end up believing in a
    /// screen the game has closed. The cost is one cheap predicate per screen per frame, which is
    /// why <see cref="IsActive"/> must never scan the scene.
    ///
    /// A screen can also open a CHILD screen (see <see cref="PushChild"/>) - a list to pick from, a
    /// dialog the mod itself drew. Children are pushed by their parent rather than polled, because
    /// nothing in the game says they are open: they are the mod's own idea.
    /// </summary>
    public abstract class Screen
    {
        /// <summary>Stable identity, for logging and for the dev server's <c>/screens</c> and
        /// <c>/gui/graph?screen=KEY</c>.</summary>
        public abstract string Key { get; }

        /// <summary>Which screens cover which. The highest layer among the active screens is the one
        /// the player is on. Static: never computed from what is showing (screens/README.md).</summary>
        public virtual int Layer
        {
            get { return 0; }
        }

        /// <summary>Polled every frame. True while the game is showing this page and it is ready to
        /// be operated.</summary>
        public abstract bool IsActive();

        /// <summary>Keep the cursor position after the screen closes, for a page the player leaves
        /// and comes straight back to (the map across a dialog, combat across the story gap).</summary>
        public virtual bool KeepStateOnPop
        {
            get { return false; }
        }

        /// <summary>Spoken when the player arrives on the screen, before the focused control reads.
        /// Null for a screen whose content already says where you are.</summary>
        public virtual string ScreenName
        {
            get { return null; }
        }

        public virtual IEnumerable<ReviewBufferKind> VisibleReviewBuffers
        {
            get
            {
                yield return ReviewBufferKind.Ui;
            }
        }

        /// <summary>Let go of the game object this screen was reading - the game state it belonged to
        /// has gone. A no-op for a screen with no slot; <see cref="LiveScreen{TAdapter}"/> clears
        /// it.</summary>
        public virtual void Forget()
        {
        }

        public virtual void OnPush()
        {
        }

        public virtual void OnPop()
        {
        }

        public virtual void OnFocus()
        {
        }

        public virtual void OnUnfocus()
        {
        }

        /// <summary>Per-frame work for the focused screen only.</summary>
        public virtual void OnUpdate()
        {
        }

        /// <summary>Whether the screen takes this action, asked BEFORE the key is pressed: an action
        /// nobody claims stays the game's.</summary>
        public virtual bool HasClaimed(string actionKey)
        {
            return false;
        }

        /// <summary>Run an action the screen claimed. True when it was handled.</summary>
        public virtual bool OnActionJustPressed(InputAction action)
        {
            return false;
        }

        // ---- child screens ----
        //
        // One linear chain: a screen has at most one child, which may have one of its own. The player
        // is on the deepest of them, and the manager works that out rather than being told. A covered
        // parent keeps its own cursor, so closing a child puts the player back on the control that
        // opened it for free.

        /// <summary>The screen this one was opened from, or null for a screen the manager polls.
        /// </summary>
        public Screen ParentScreen { get; private set; }

        /// <summary>The child this screen has open, or null.</summary>
        public Screen ActiveChild { get; private set; }

        /// <summary>Who to tell when a child closes, so its cursor can be dropped. Inherited by
        /// children from the screen they are pushed onto.</summary>
        public ScreenManager Manager { get; set; }

        /// <summary>The screen the player is actually on: this one, or the deepest thing open over it.
        /// </summary>
        public Screen Deepest()
        {
            Screen at = this;
            // Bounded rather than "while": a chain is a handful deep by construction, and a cycle
            // introduced by a bug should not hang the frame.
            for (int depth = 0; depth < 16 && at.ActiveChild != null; depth++)
            {
                at = at.ActiveChild;
            }

            return at;
        }

        /// <summary>Open <paramref name="child"/> over this screen. Any child already open is closed
        /// first - one chain, no branching.</summary>
        public void PushChild(Screen child)
        {
            if (child == null || ReferenceEquals(child, ActiveChild) || ReferenceEquals(child, this))
            {
                return;
            }

            if (ActiveChild != null)
            {
                RemoveChild(ActiveChild);
            }

            child.ParentScreen = this;
            child.Manager = Manager;
            ActiveChild = child;
            child.OnPush();
        }

        /// <summary>Close <paramref name="child"/>, and anything it had open, deepest first. Focus
        /// falls back to this screen on the manager's next tick.</summary>
        public void RemoveChild(Screen child)
        {
            if (child == null || !ReferenceEquals(ActiveChild, child))
            {
                return;
            }

            if (child.ActiveChild != null)
            {
                child.RemoveChild(child.ActiveChild);
            }

            ActiveChild = null;
            child.OnPop();
            ScreenManager manager = child.Manager;
            child.ParentScreen = null;
            child.Manager = null;
            if (manager != null)
            {
                manager.ChildClosed(child);
            }
        }

        /// <summary>Close this screen from the inside - what a child screen's own Escape does.</summary>
        public void CloseSelf()
        {
            Screen parent = ParentScreen;
            if (parent != null)
            {
                parent.RemoveChild(this);
            }
        }
    }
}
