using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>
    /// Thread-safe ring of text lines with stable, monotonically increasing sequence numbers,
    /// behind the loader's cursor-polled feeds (GET /log, and the spoken lines POST /eval reports
    /// back). Callers poll with the highest sequence they have already seen and get only what is
    /// newer, so nothing is missed between polls and nothing is replayed.
    ///
    /// Written from whichever thread produced the line - the Unity main thread for speech, any
    /// thread at all for BepInEx log events - and read from HTTP handler threads.
    /// </summary>
    public class SeqLog
    {
        public struct Entry
        {
            public long Seq;
            public string Text;
        }

        private readonly object _lock = new object();
        private readonly List<string> _texts = new List<string>();
        private readonly int _capacity;

        // Sequence number of _texts[0]. Sequences start at 1 so "since=0" means "everything".
        private long _firstSeq = 1;

        // Set when whatever writes this is going away, so a waiter blocked for the next line is
        // released rather than left holding an HTTP thread against an object nobody will write to
        // again.
        private bool _closed;

        public SeqLog(int capacity)
        {
            _capacity = capacity;
        }

        /// <summary>The newest sequence number held, or 0 when nothing has been written. Taking
        /// this before an action and passing it as <c>since</c> afterwards reports exactly what
        /// that action produced.</summary>
        public long Cursor
        {
            get
            {
                lock (_lock)
                {
                    return _firstSeq + _texts.Count - 1;
                }
            }
        }

        public void Add(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return;
            }

            lock (_lock)
            {
                _texts.Add(text);
                if (_texts.Count > _capacity)
                {
                    _texts.RemoveAt(0);
                    _firstSeq++;
                }

                Monitor.PulseAll(_lock);
            }
        }

        /// <summary>
        /// Entries whose sequence is greater than <paramref name="since"/>, oldest first, empty
        /// when nothing is newer. <paramref name="next"/> is the cursor to pass next time; it
        /// advances even when entries were dropped by the ring buffer, so a slow poller resumes at
        /// the oldest line still held rather than replaying from the start.
        /// </summary>
        public List<Entry> Since(long since, out long next)
        {
            lock (_lock)
            {
                long end = _firstSeq + _texts.Count;
                long seq = since + 1;
                if (seq < _firstSeq)
                {
                    seq = _firstSeq;
                }

                List<Entry> entries = new List<Entry>();
                for (; seq < end; seq++)
                {
                    entries.Add(new Entry { Seq = seq, Text = _texts[(int)(seq - _firstSeq)] });
                }

                next = end - 1;
                if (next < since)
                {
                    next = since;
                }

                return entries;
            }
        }

        /// <summary>Drop entries whose text does not contain <paramref name="needle"/>, ignoring
        /// case. Filtering after <see cref="Since"/> rather than inside it keeps the cursor
        /// counting every line, so a filtered poll still advances past what it hid.</summary>
        public static List<Entry> Matching(List<Entry> entries, string needle)
        {
            if (string.IsNullOrEmpty(needle))
            {
                return entries;
            }

            List<Entry> matched = new List<Entry>();
            foreach (Entry entry in entries)
            {
                if (entry.Text.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    matched.Add(entry);
                }
            }

            return matched;
        }

        /// <summary>
        /// Block up to <paramref name="timeoutMilliseconds"/> for a line newer than
        /// <paramref name="since"/>, returning true as soon as there is one (or at once if there
        /// already was). The write side pulses under the same lock it appends under, so a caller
        /// asking "what does this say next" is released on the frame it is said rather than on its
        /// next poll. HTTP threads only - the main thread must never wait on the pump that feeds
        /// this.
        /// </summary>
        public bool WaitForNewer(long since, int timeoutMilliseconds)
        {
            lock (_lock)
            {
                if (Newer(since) || _closed)
                {
                    return Newer(since);
                }

                // One wait, not a loop: a spurious wake would only cost the caller the rest of its
                // budget, and Monitor.Wait here is only ever pulsed by an actual append or by close.
                Monitor.Wait(_lock, timeoutMilliseconds);
                return Newer(since);
            }
        }

        /// <summary>Release anyone waiting for the next line - whatever writes this is going away,
        /// and a waiter left holding an HTTP thread against an object nobody will write to again is
        /// a hang.</summary>
        public void Close()
        {
            lock (_lock)
            {
                _closed = true;
                Monitor.PulseAll(_lock);
            }
        }

        // Caller holds the lock.
        private bool Newer(long since)
        {
            return _firstSeq + _texts.Count - 1 > since;
        }

        /// <summary>
        /// Everything written since <paramref name="since"/>, once the writing has STOPPED: wait
        /// until nothing new has arrived for <paramref name="settleMilliseconds"/>, giving up after
        /// <paramref name="maxWaitMilliseconds"/> however talkative it stays.
        ///
        /// This is what makes "and here is what that made it say" a complete answer rather than
        /// whatever had been said by the time the request returned - a line the action causes two
        /// frames later is still the action's. It polls rather than waiting on the pulse, because
        /// the question is when the noise STOPPED rather than when the next line arrives.
        ///
        /// HTTP threads only, for the reason <see cref="WaitForNewer"/> gives.
        /// </summary>
        public List<Entry> Settled(
            long since,
            int settleMilliseconds,
            int pollMilliseconds,
            int maxWaitMilliseconds
        )
        {
            Stopwatch total = Stopwatch.StartNew();
            Stopwatch quiet = Stopwatch.StartNew();
            long cursor = since;

            while (
                quiet.ElapsedMilliseconds < settleMilliseconds
                && total.ElapsedMilliseconds < maxWaitMilliseconds
            )
            {
                Thread.Sleep(pollMilliseconds);

                long now = Cursor;
                if (now != cursor)
                {
                    cursor = now;
                    quiet.Reset();
                    quiet.Start();
                }
            }

            long next;
            return Since(since, out next);
        }
    }
}
