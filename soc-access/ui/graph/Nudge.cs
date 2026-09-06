namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// A push for something the game began and then dropped.
    ///
    /// Some of what a page does to finish arriving is deferred - handed to a coroutine, an animation
    /// or a callback - and a deferred step can be lost outright while everything around it reports
    /// success. Doing it on the game's behalf is easy; doing it WITHOUT fighting the game is the part
    /// that needs a rule, because from the outside "the game dropped this" and "the game is still
    /// getting to it" look exactly alike and only differ in how long they last.
    ///
    /// Hence two waits. The SETTLE is how long the stalled state has to hold before it counts as
    /// dropped rather than slow, so the ordinary case - where the game finishes a frame or two later -
    /// never gets pushed at all. The PAUSE is how long to leave the game alone afterwards: the push
    /// itself is deferred too, so the stalled signature reads exactly the same for a while after it,
    /// and a caller that asked again every frame would stack pushes on top of each other.
    ///
    /// Counted in calls rather than in seconds because the caller is a per-frame poll and frames are
    /// what it has; nothing here touches a clock, so it is engine-free and testable off-game.
    /// </summary>
    public sealed class Nudge
    {
        private readonly int _settle;
        private readonly int _pause;
        private int _stalled;
        private int _waiting;

        /// <param name="settle">How many consecutive calls the stall must hold before it is pushed.
        /// </param>
        /// <param name="pause">How many calls to stand back for after a push.</param>
        public Nudge(int settle, int pause)
        {
            _settle = settle < 1 ? 1 : settle;
            _pause = pause < 0 ? 0 : pause;
        }

        /// <summary>
        /// Whether the caller should do the game's job for it this frame. Ask once a frame.
        /// </summary>
        /// <param name="stalled">Whether the thing the game should have done is still not done.
        /// </param>
        /// <param name="safe">Whether doing it now would work - a push into a state the game cannot
        /// complete either is worse than none, because it looks like a fix and is not.</param>
        public bool Due(bool stalled, bool safe)
        {
            if (_waiting > 0)
            {
                _waiting--;
                _stalled = 0;
                return false;
            }

            if (!stalled || !safe)
            {
                _stalled = 0;
                return false;
            }

            _stalled++;
            if (_stalled < _settle)
            {
                return false;
            }

            _stalled = 0;
            _waiting = _pause;
            return true;
        }

        /// <summary>Start over - the page this was watching has gone, so nothing it saw still
        /// applies.</summary>
        public void Forget()
        {
            _stalled = 0;
            _waiting = 0;
        }
    }
}
