using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace SongsOfConquestAccess.Loader.Dev
{
    /// <summary>Raised when the game did not reach the queued work in time (game frozen, paused
    /// in a modal loop, or shutting down). Answered as HTTP 503.</summary>
    public sealed class MainThreadTimeoutException : Exception
    {
        public MainThreadTimeoutException(string message)
            : base(message)
        {
        }
    }

    /// <summary>
    /// Hands work from HTTP handler threads to the Unity main thread. Every Unity API touch the
    /// dev server makes goes through here, the mod's route handlers included: handlers enqueue a
    /// job and block on it, and <see cref="Drain"/> (called once per frame from the loader's
    /// Update) runs the queue.
    ///
    /// A Queue&lt;T&gt; behind a lock keeps execution ordered. Each job uses a task completion
    /// source so timed-out requests do not leave native wait handles behind.
    /// </summary>
    public sealed class MainThreadQueue
    {
        public const int DefaultTimeoutMilliseconds = 5000;

        private sealed class Job
        {
            public Func<object> Work;
            public object Result;
            public Exception Failure;
            public readonly TaskCompletionSource<object> Done =
                new TaskCompletionSource<object>();
        }

        private readonly object _lock = new object();
        private readonly Queue<Job> _pending = new Queue<Job>();

        /// <summary>Run <paramref name="work"/> on the main thread and block for its result.
        /// Rethrows whatever the job threw; throws <see cref="MainThreadTimeoutException"/> if the
        /// game never got to it.</summary>
        public object Run(Func<object> work)
        {
            return Run(work, DefaultTimeoutMilliseconds);
        }

        public object Run(Func<object> work, int timeoutMilliseconds)
        {
            Job job = new Job { Work = work };
            Enqueue(job);

            if (!job.Done.Task.Wait(timeoutMilliseconds))
            {
                throw new MainThreadTimeoutException(
                    "the game did not run the request within " + timeoutMilliseconds + " ms"
                );
            }

            if (job.Failure != null)
            {
                throw new Exception(job.Failure.Message, job.Failure);
            }

            return job.Result;
        }

        /// <summary>Queue work for the main thread without waiting for it, for requests that must
        /// answer before their effect lands.</summary>
        public void Post(Action work)
        {
            Enqueue(
                new Job
                {
                    Work = () =>
                    {
                        work();
                        return null;
                    },
                }
            );
        }

        /// <summary>Run everything queued since the last call. Main thread only.</summary>
        public void Drain()
        {
            while (true)
            {
                Job job;
                lock (_lock)
                {
                    if (_pending.Count == 0)
                    {
                        return;
                    }

                    job = _pending.Dequeue();
                }

                try
                {
                    job.Result = job.Work();
                }
                catch (Exception e)
                {
                    job.Failure = e;
                }

                job.Done.TrySetResult(null);
            }
        }

        private void Enqueue(Job job)
        {
            lock (_lock)
            {
                _pending.Enqueue(job);
            }
        }
    }
}
