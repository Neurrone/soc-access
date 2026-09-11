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
        private readonly MarkerTable _markers = new MarkerTable();

        /// <summary>Declare the screen's controls. Called on every navigation operation.</summary>
        public abstract void Build(GraphBuilder builder);

        /// <summary>This screen's own object for that node key, to mint a node id from. The object
        /// belongs to this screen INSTANCE, so two screens using the same key still get two ids.
        /// </summary>
        protected object Marker(string key)
        {
            return _markers.For(key);
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

        /// <summary>
        /// Whether a MODE of this screen takes the action - asked BEFORE the navigator's own set, which
        /// is the opposite of <see cref="ClaimsAction"/>. For a mode whose cursor is not the focus
        /// cursor (the map's tile cursor, combat's hex cursor): while the focused node is the mode's,
        /// the arrows, Home, End and the rest mean the cursor rather than the tree, and the screen
        /// answers them in <see cref="OnAction"/>. Answer only while the mode is driving; a stop that is
        /// not the mode's keeps every key of its own. A live type-ahead search is innermost and is
        /// asked first still; the carry's back key is not displaced either.
        /// </summary>
        public virtual bool ModeClaims(string actionKey)
        {
            return false;
        }

        /// <summary>Run an action <see cref="ClaimsAction"/> answered for. True when it was
        /// handled.</summary>
        public virtual bool OnAction(string actionKey)
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

        /// <summary>THE SCREEN'S OWN TEXT EDITOR, or null where the page has no box the player types
        /// into. A screen that has one answers with it and gets its whole lifecycle from here: the
        /// raw-input and field-ownership answers, the per-frame update, and the abandon on leaving and
        /// on popping. A screen whose editor lives on a row group answers with that one.</summary>
        public virtual GameTextEditor Editor
        {
            get { return null; }
        }

        /// <summary>Whether the screen is handing the keyboard to a game field and so must not have it
        /// taken back. A screen with an <see cref="Editor"/> answers with its pending-or-editing
        /// state.</summary>
        public virtual bool OwnsGameField
        {
            get
            {
                GameTextEditor editor = Editor;
                return editor != null && (editor.Pending || editor.Editing);
            }
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
            get
            {
                GameTextEditor editor = Editor;
                return editor != null && editor.Pending;
            }
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

        /// <summary>The name this screen actually spoke on arrival, null when it had none to say. A
        /// page that turns in place compares against this rather than against the live name: a page
        /// pushed before the game wrote its title (the post-battle page, whose title arrives with its
        /// animation) has said nothing, and the title arriving later is news.</summary>
        public string SpokenName { get; private set; }

        /// <summary>Say the screen's name on arrival - the manager's one announcement site
        /// (<see cref="ScreenManager"/>). Queued, so the focused control's readout follows it rather
        /// than cutting it off.</summary>
        public void SayName()
        {
            string name = ScreenName;
            if (string.IsNullOrEmpty(name))
            {
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(name, interrupt: false));
            SpokenName = name;
        }

        /// <summary>The page turned in place and is now called something else: say the new name.
        /// Nothing is said when the name has not moved, so a rewrite of the slot that carried no news
        /// is silent.</summary>
        public void SayNameIfChanged()
        {
            string name = ScreenName;
            if (string.IsNullOrEmpty(name) || name == SpokenName)
            {
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(name, interrupt: false));
            SpokenName = name;
        }

        public override void OnUpdate()
        {
            // Every frame, not only on arrival: a game field that takes the keyboard on its own at any
            // time (the chat box re-focusing itself after a send) would otherwise keep every key from
            // the mod until the player found Escape. The screen's own editor is left alone.
            if (!OwnsGameField && !GameTextEditor.Owned)
            {
                GameTextFocus.Release();
            }

            // After the release and after the navigator, so the word a handover speaks follows the
            // activation's own readout. Whether the page is still showing is what tells an edit the
            // player ended from a window that went away under it: Enter in the box can send and close
            // it, and an ending nobody is left to hear is not announced.
            GameTextEditor editor = Editor;
            if (editor != null)
            {
                editor.Update(IsActive());
            }
        }

        /// <summary>The cursor has left the screen: an edit still being handed over is given up.
        /// </summary>
        public override void OnUnfocus()
        {
            base.OnUnfocus();
            GameTextEditor editor = Editor;
            if (editor != null)
            {
                editor.Abandon();
            }
        }

        public override void OnPop()
        {
            base.OnPop();
            GameTextEditor editor = Editor;
            if (editor != null)
            {
                editor.Abandon();
            }
        }

        public override bool HasClaimed(string actionKey)
        {
            GraphNavigator navigator = Navigator;
            return navigator != null && ReferenceEquals(navigator.Screen, this) && navigator.Claims(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            GraphNavigator navigator = Navigator;
            return action != null
                && navigator != null
                && ReferenceEquals(navigator.Screen, this)
                && navigator.Dispatch(action.Key);
        }

        /// <summary>The tooltip of whatever the player is standing on, or null.</summary>
        public Tooltip CurrentTooltip
        {
            get
            {
                GraphNavigator navigator = Navigator;
                return navigator != null && ReferenceEquals(navigator.Screen, this) ? navigator.FocusedTooltip : null;
            }
        }
    }
}
