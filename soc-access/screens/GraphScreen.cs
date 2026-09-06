using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// A screen declared as an immediate-mode graph rather than as a retained widget tree - the
    /// bridge between the existing push/pop <see cref="ScreenManager"/> and the graph engine under
    /// <c>ui/graph/</c>. A screen ports by changing its base class to this and replacing its widget
    /// construction with <see cref="Build"/>, which is called afresh for every navigation operation
    /// and declares the controls as they are at that instant. Declaring nothing is legal and means
    /// "nothing here yet"; the navigator retries next frame.
    ///
    /// Everything the manager asks of a screen is answered by the one <see cref="GraphNavigator"/>:
    /// claims, dispatch, the per-frame focus pass, and the focused tooltip.
    /// </summary>
    public abstract class GraphScreen : Screen
    {
        protected GraphScreen()
            : base(null)
        {
        }

        /// <summary>Stable identity, for logging and the dev server.</summary>
        public abstract string Key { get; }

        /// <summary>Declare the screen's controls. Called on every navigation operation.</summary>
        public abstract void Build(GraphBuilder builder);

        /// <summary>Spoken when the player arrives on the screen, before the focused control reads.
        /// Null for a screen whose content already says where you are.</summary>
        public virtual string ScreenName
        {
            get { return null; }
        }

        /// <summary>Where focus lands on first arrival, as a Tab-stop key; null starts at the graph's
        /// own start node.</summary>
        public virtual object InitialFocusStop
        {
            get { return null; }
        }

        /// <summary>
        /// Whether the page can still be WORKED, as opposed to standing there while the game switches
        /// it off - fading out after a click. A page being switched off wholesale turns every control
        /// on it unavailable at once, and the control the player just pressed saying "disabled" is a
        /// fact about the page, not about the control. While this is false the live watch stays silent
        /// (it still re-baselines, so a change made across the gap is not announced late).
        /// </summary>
        public virtual bool IsWorkable
        {
            get { return true; }
        }

        /// <summary>The back key was pressed. Return true when the screen handled it; false lets the
        /// game's own handling stand.</summary>
        public virtual bool Back()
        {
            return false;
        }

        /// <summary>Whether <see cref="Back"/> is going to claim the key, asked BEFORE it is pressed.
        /// Screens overwhelmingly answer false: Escape belongs to the game, and only a surface the
        /// mod itself put on the screen has any business taking the key away from it.</summary>
        public virtual bool ConsumesBack
        {
            get { return false; }
        }

        /// <summary>Whether typing searches this screen. False for a screen whose whole point is a
        /// box the player types into.</summary>
        public virtual bool AllowsTypeahead
        {
            get { return true; }
        }

        /// <summary>Whether the screen is in the middle of handing the keyboard to the game - a text
        /// editor asked for and not yet given - so typed letters must not start a search.</summary>
        public virtual bool CapturesRawInput
        {
            get { return false; }
        }

        /// <summary>What a search on this screen looks through - null (the usual answer) for the
        /// declared controls of the focused Tab-stop.</summary>
        public virtual SearchScope TypeAheadScope(GraphNode focused, GraphRender render)
        {
            return null;
        }

        /// <summary>The cursor has landed on one of this screen's controls - the screen's own half
        /// of the focus visual, run before the node's.</summary>
        public virtual void OnFocusVisual(GraphNode node)
        {
        }

        public GraphNavigator Navigator
        {
            get { return SocAccessMod.Instance == null ? null : SocAccessMod.Instance.Navigator; }
        }

        public override void OnFocus()
        {
            GraphNavigator navigator = Navigator;
            if (navigator == null)
            {
                return;
            }

            navigator.Attach(this);
            string name = ScreenName;
            if (!string.IsNullOrEmpty(name))
            {
                // Queued, so the first control's readout follows it rather than cutting it off.
                SpeechPipeline.Output(new SpeechRequest(name, interrupt: false));
            }
        }

        public override void OnUnfocus()
        {
            GraphNavigator navigator = Navigator;
            if (navigator != null && ReferenceEquals(navigator.Screen, this))
            {
                navigator.Attach(null);
            }
        }

        public override void OnPop()
        {
            GraphNavigator navigator = Navigator;
            if (navigator != null)
            {
                navigator.ScreenClosed(this);
            }
        }

        public override void Update()
        {
            GraphNavigator navigator = Navigator;
            if (navigator != null && ReferenceEquals(navigator.Screen, this))
            {
                navigator.Update();
            }
        }

        public override bool HasClaimed(string actionKey)
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && ReferenceEquals(navigator.Screen, this) && navigator.Claims(actionKey);
        }

        public override bool HasFocusedWidgetClaimed(string actionKey)
        {
            return HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            GraphNavigator navigator = Navigator;
            return action != null
                && navigator != null
                && ReferenceEquals(navigator.Screen, this)
                && navigator.Dispatch(action.Key);
        }

        public override Tooltip CurrentTooltip
        {
            get
            {
                GraphNavigator navigator = Navigator;
                return navigator != null && ReferenceEquals(navigator.Screen, this) ? navigator.FocusedTooltip : null;
            }
        }
    }
}
