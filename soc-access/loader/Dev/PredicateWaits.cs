using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>
    /// One outstanding POST /wait: a boolean expression compiled once, then asked every frame
    /// whether it is true yet. Polling from outside the process can only sample between frames and
    /// misses a condition that holds for one of them; this sees every frame, which is what makes
    /// "wait until the ship is selected" reliable instead of racy.
    ///
    /// The predicate runs on the Unity main thread (it reads game state); the requesting HTTP
    /// thread blocks on <see cref="Done"/> and reads the outcome once it is set.
    /// </summary>
    internal sealed class PredicateWait
    {
        public readonly ManualResetEvent Done = new ManualResetEvent(false);

        private readonly object _lock = new object();
        private readonly CompiledPredicate _predicate;
        private readonly int _timeoutMilliseconds;
        private readonly Stopwatch _clock = Stopwatch.StartNew();
        private bool _finished;

        public PredicateWait(CompiledPredicate predicate, int timeoutMilliseconds)
        {
            _predicate = predicate;
            _timeoutMilliseconds = timeoutMilliseconds;
        }

        /// <summary>True when the predicate came back true before the timeout.</summary>
        public bool Satisfied { get; private set; }

        /// <summary>How many frames the predicate was asked on.</summary>
        public int Frames { get; private set; }

        /// <summary>What the predicate threw, or why the wait could not be run at all; null when
        /// the wait ran to a clean answer, satisfied or timed out.</summary>
        public string Error { get; private set; }

        public int ElapsedMilliseconds
        {
            get { return (int)_clock.ElapsedMilliseconds; }
        }

        /// <summary>Ask the predicate once. Main thread. Returns true when this wait is over and
        /// should be dropped from the pending list.</summary>
        public bool Evaluate()
        {
            lock (_lock)
            {
                if (_finished)
                {
                    return true;
                }
            }

            Frames++;

            try
            {
                if (_predicate.Invoke())
                {
                    Satisfied = true;
                    return Finish(null);
                }
            }
            catch (Exception e)
            {
                return Finish(e.ToString());
            }

            if (_clock.ElapsedMilliseconds >= _timeoutMilliseconds)
            {
                return Finish(null);
            }

            return false;
        }

        /// <summary>End the wait from outside, when the game stopped ticking and nothing will ever
        /// evaluate it.</summary>
        public void Abandon(string error)
        {
            Finish(error);
        }

        private bool Finish(string error)
        {
            lock (_lock)
            {
                if (_finished)
                {
                    return true;
                }

                _finished = true;
                Error = error;
            }

            _clock.Stop();
            Done.Set();
            return true;
        }
    }

    /// <summary>Every wait POST /wait has outstanding. Several may be pending at once, so the list
    /// is ticked as a whole, once per frame, from the loader's Update.</summary>
    internal sealed class PredicateWaits
    {
        private readonly object _lock = new object();
        private readonly List<PredicateWait> _pending = new List<PredicateWait>();

        public void Add(PredicateWait wait)
        {
            lock (_lock)
            {
                _pending.Add(wait);
            }
        }

        public void Remove(PredicateWait wait)
        {
            lock (_lock)
            {
                _pending.Remove(wait);
            }
        }

        /// <summary>End every outstanding wait at once, with one reason. The game is going down and
        /// no frame will ever evaluate them, so each request is answered now rather than waiting out
        /// its own timeout.</summary>
        public void AbandonAll(string error)
        {
            PredicateWait[] pending;
            lock (_lock)
            {
                pending = _pending.ToArray();
                _pending.Clear();
            }

            foreach (PredicateWait wait in pending)
            {
                wait.Abandon(error);
            }
        }

        /// <summary>Ask every pending predicate. Main thread, once per frame.</summary>
        public void Tick()
        {
            PredicateWait[] pending;
            lock (_lock)
            {
                if (_pending.Count == 0)
                {
                    return;
                }

                pending = _pending.ToArray();
            }

            foreach (PredicateWait wait in pending)
            {
                if (wait.Evaluate())
                {
                    Remove(wait);
                }
            }
        }
    }
}
