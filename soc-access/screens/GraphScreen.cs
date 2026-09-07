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

        /// <summary>
        /// Whether the screen takes an action the navigator has no meaning of its own for - asked
        /// AFTER its own set, so a screen can never take a navigation key away from it. The claim is
        /// asked before the press, as every claim is: an action nobody claims stays the game's.
        ///
        /// This is how a screen puts one of the GAME's own chords on one of its controls: the troop
        /// rows' Ctrl+digit quick split (<see cref="UI.TroopHudRows"/>), and in phase E the map and
        /// combat modes' keys, which belong to the mode node rather than to the screen at large.
        /// </summary>
        public virtual bool ClaimsAction(string actionKey)
        {
            return false;
        }

        /// <summary>Run an action <see cref="ClaimsAction"/> answered for. True when it was
        /// handled.</summary>
        public virtual bool OnAction(string actionKey)
        {
            return false;
        }

        /// <summary>
        /// Home or End on the focused control, asked BEFORE the cursor moves: <paramref name="first"/>
        /// is Home. True where the CONTROL owns its own ends - a slider jumps to its minimum and its
        /// maximum, which no number of arrow presses would reach in one gesture - and the value it
        /// reports is spoken as an adjustment's is. False, the usual answer, leaves both keys as the
        /// navigation they are everywhere else.
        /// </summary>
        public virtual bool OnEdge(GraphNode node, bool first)
        {
            return false;
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

        /// <summary>Whether this screen's own editor holds or is about to hold a game text field, so
        /// the arrival release leaves it alone. Screens that own a <see cref="GameTextEditor"/>
        /// answer with its pending-or-editing state.</summary>
        public virtual bool OwnsGameField
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

        /// <summary>This instance replaced an identical one that was already on screen, so the player
        /// has not arrived anywhere: the screen does not say its own name again. Set by
        /// <see cref="ScreenManager.RefreshTop{TScreen}"/> before <see cref="OnFocus"/>, and spent
        /// there.</summary>
        public bool ArrivedByRefresh { get; set; }

        /// <summary>The name this screen actually spoke on arrival, null when it had none to say. A
        /// refresh compares against this rather than the live name: a page pushed before the game
        /// wrote its title (the post-battle page, refreshed once its animation ends) has said nothing,
        /// and the title arriving with the refresh is news.</summary>
        public string SpokenName { get; private set; }

        public override void OnFocus()
        {
            GraphNavigator navigator = Navigator;
            if (navigator == null)
            {
                return;
            }

            navigator.Attach(this);
            bool refreshed = ArrivedByRefresh;
            ArrivedByRefresh = false;
            string name = ScreenName;
            if (!refreshed && !string.IsNullOrEmpty(name))
            {
                // Queued, so the first control's readout follows it rather than cutting it off.
                SpeechPipeline.Output(new SpeechRequest(name, interrupt: false));
                SpokenName = name;
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
            // Every frame, not only on arrival: a game field that takes the keyboard on its own at any
            // time (the chat box re-focusing itself after a send) would otherwise keep every key from
            // the mod until the player found Escape. The screen's own editor is left alone.
            if (!OwnsGameField && !GameTextEditor.Owned)
            {
                GameTextFocus.Release();
            }

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
