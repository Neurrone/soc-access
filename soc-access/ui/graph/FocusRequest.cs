namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>How far a control asked for is from being in the render - the answer
    /// <see cref="KeyGraph.Reach"/> gives about one id.</summary>
    public enum ReachStep
    {
        /// <summary>The control is declared: focus can land on it now.</summary>
        Present,

        /// <summary>A collapsed group on the way to it was just opened; the control appears on a
        /// later build.</summary>
        Opened,

        /// <summary>Something on the way to it is declared and already open, but the control is not
        /// there yet - the game has not drawn what the branch reads from.</summary>
        Waiting,

        /// <summary>Nothing in the render leads to it: there is no branch to open, so no amount of
        /// waiting can produce it.</summary>
        Unreachable,
    }

    /// <summary>What a caller holding a <see cref="FocusRequest"/> should do this frame.</summary>
    public enum FocusOutcome
    {
        /// <summary>Put the cursor on it and forget the request.</summary>
        Land,

        /// <summary>Keep the request for another frame.</summary>
        Wait,

        /// <summary>Give up on it.</summary>
        Drop,
    }

    /// <summary>
    /// A landing that has been asked for and not yet made - a screen sending the cursor somewhere.
    ///
    /// The control is often not in the render when it is asked for: the branch it hangs in is
    /// collapsed, and a collapsed group declares no children at all. So the request survives while
    /// progress towards it is being made - one level of ancestry opened per build
    /// (<see cref="KeyGraph.Reach"/>), and then however many frames the game takes to draw what that
    /// branch reads from - and dies two ways. A request nothing in the render leads to dies at once,
    /// which is what a landing aimed at a control that has simply gone away has always done. One
    /// whose branch is open and never produces it dies when the budget runs out, so an impossible id
    /// cannot keep a landing armed over the player's own navigation for the rest of the session.
    ///
    /// Both deaths only happen on a frame the request is being WORKED ON. A landing belongs to the
    /// screen that asked for it, and frames where that screen is not the one the player is on cannot
    /// carry it any further: the game may be playing a cutscene over it, or flying the camera between
    /// two views, and the render such a frame would be judged against is somebody else's or half built.
    /// So those frames are SUSPENDED - the request is kept, nothing is spent, and neither "nothing
    /// leads there" nor the budget can kill it - and it resumes with the budget it had when the player
    /// comes back (owner-reported: a fleet action's seat died during the system-discovery cutscene and
    /// the cursor never moved).
    ///
    /// Off the engine so the budget is testable: the frame that drives it is the navigator's.
    /// </summary>
    public sealed class FocusRequest
    {
        /// <summary>About five seconds of frames. Long because the levels of a tree are not the slow
        /// part: a branch that reads from something the game DRAWS (a card the map binds once its
        /// camera has flown in) needs the flight, not the build. Short enough that an id the game
        /// never draws stops being waited for.</summary>
        public const int DefaultFrames = 300;

        private readonly ControlId _id;
        private readonly bool _announce;
        private readonly object _owner;
        private int _frames;

        public FocusRequest(ControlId id, bool announce, int frames = DefaultFrames)
            : this(id, announce, null, frames) { }

        public FocusRequest(ControlId id, bool announce, object owner, int frames = DefaultFrames)
        {
            _id = id;
            _announce = announce;
            _owner = owner;
            _frames = frames;
        }

        /// <summary>Who asked for the landing - the screen whose graph the id belongs to, or null where
        /// the caller does not scope its requests. The one question that decides whether a frame counts:
        /// only the screen that asked can carry its own landing forward, and only that screen's own
        /// navigation cancels it. Compared by reference; nothing here reads it.</summary>
        public object Owner
        {
            get { return _owner; }
        }

        /// <summary>The control the cursor was asked to land on.</summary>
        public ControlId Id
        {
            get { return _id; }
        }

        /// <summary>Whether the landing should be read out (false: the caller has said its own piece
        /// about it).</summary>
        public bool Announce
        {
            get { return _announce; }
        }

        /// <summary>Frames of waiting still allowed - for a test, and for a caller that wants to know
        /// how much of the budget a cascade cost.</summary>
        public int FramesLeft
        {
            get { return _frames; }
        }

        /// <summary>Spend this frame on the request, given how close the render says the control is.
        /// </summary>
        public FocusOutcome Step(ReachStep reach)
        {
            return Step(reach, false);
        }

        /// <summary>
        /// The same, on a frame that may be SUSPENDED - one where nothing about this request can be
        /// judged, because the screen that asked for it is not the one being drawn or the view is still
        /// moving.
        ///
        /// A suspended frame costs nothing and proves nothing: the budget is not spent, an
        /// "unreachable" answer is not believed, and a control that IS present does not land either -
        /// because the render being asked is not the render the landing was aimed at, and neither is
        /// what that control would SAY. A landing announces itself once, and a node read while the
        /// camera is still flying reads the far view's version of itself (the galaxy's planet rows:
        /// "Osulo I, Colonized" before the flight, "Osulo I, group, Medium Mediterrane., Colonized,
        /// collapsed" after it). So the whole judgement waits, landing the frame the screen says it is
        /// settled. An earlier revision landed a present control mid-transition and shipped exactly
        /// that defect.
        /// </summary>
        public FocusOutcome Step(ReachStep reach, bool suspended)
        {
            if (suspended)
            {
                return FocusOutcome.Wait;
            }

            if (reach == ReachStep.Present)
            {
                return FocusOutcome.Land;
            }

            if (reach == ReachStep.Unreachable)
            {
                return FocusOutcome.Drop;
            }

            return --_frames > 0 ? FocusOutcome.Wait : FocusOutcome.Drop;
        }
    }
}
