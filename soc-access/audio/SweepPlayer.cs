using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    /// <summary>One entity found by the sweep: where it is and the cue stack that names it.</summary>
    internal struct SweepEntry
    {
        public SweepEntry(Vector2Int position, IReadOnlyList<TileCue> cues)
        {
            Position = position;
            Cues = cues;
        }

        public Vector2Int Position { get; }

        public IReadOnlyList<TileCue> Cues { get; }
    }

    /// <summary>A ping resolved to render offsets plus the time it fires, relative to the sweep start.</summary>
    internal struct SweepStep
    {
        public SweepStep(IReadOnlyList<TileCue> cues, float pan, float semitones, float gainScale, float timeSeconds)
        {
            Cues = cues;
            Pan = pan;
            Semitones = semitones;
            GainScale = gainScale;
            TimeSeconds = timeSeconds;
        }

        public IReadOnlyList<TileCue> Cues { get; }

        public float Pan { get; }

        public float Semitones { get; }

        public float GainScale { get; }

        public float TimeSeconds { get; }
    }

    /// <summary>
    /// Plays a sweep: one directional ping per entity, serialized west to east. Scheduling is
    /// driven from a ticker rather than PlayDelayed because the voice pool steals pending
    /// delayed voices once a sweep gets busy.
    /// </summary>
    internal static class SweepPlayer
    {
        /// <summary>The sweep aims to finish inside this window, within the gap bounds.</summary>
        public const float TargetWindowSeconds = 0.75f;
        public const float MinimumGapSeconds = 0.1f;
        public const float MaximumGapSeconds = 0.2f;

        /// <summary>Silence between one ping finishing and the next starting. A two-part gesture
        /// outlasts the base rhythm, and pings that run into each other cannot be told apart.</summary>
        public const float MinimumSeparationSeconds = 0.05f;

        private static GameObject _gameObject;
        private static List<SweepStep> _steps;
        private static int _nextIndex;
        private static float _elapsedSeconds;

        /// <summary>Cancels any sweep still running; a stale sweep describes a stale cursor.</summary>
        public static void Start(IReadOnlyList<SweepEntry> entries, Vector2Int origin, CueGridGeometry geometry)
        {
            try
            {
                Cancel();
                List<SweepStep> steps = BuildSchedule(entries, origin, geometry);
                if (steps.Count == 0)
                {
                    return;
                }

                _steps = steps;
                _nextIndex = 0;
                _elapsedSeconds = 0f;
                EnsureTicker();
                FireDueSteps();
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to start sonar sweep: " + exception.Message);
                _steps = null;
            }
        }

        /// <summary>Stops scheduling; pings that already started are left to ring out.</summary>
        public static void Cancel()
        {
            _steps = null;
            _nextIndex = 0;
            _elapsedSeconds = 0f;
        }

        public static void DisposeAll()
        {
            Cancel();
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }
        }

        internal static List<SweepStep> BuildSchedule(
            IReadOnlyList<SweepEntry> entries,
            Vector2Int origin,
            CueGridGeometry geometry)
        {
            return BuildSchedule(entries, origin, geometry, CueLibrary.GetAudibleDurationSeconds);
        }

        /// <summary>
        /// Sorts west to east, drops entries out of audible range and spaces the survivors on the
        /// base rhythm, pushed later whenever a gesture would still be sounding. Pure: no Unity
        /// time, no playback; <paramref name="durationSeconds"/> supplies each cue's length.
        /// </summary>
        internal static List<SweepStep> BuildSchedule(
            IReadOnlyList<SweepEntry> entries,
            Vector2Int origin,
            CueGridGeometry geometry,
            Func<string, float> durationSeconds)
        {
            List<SweepStep> steps = new List<SweepStep>();
            if (entries == null || entries.Count == 0)
            {
                return steps;
            }

            List<SweepEntry> ordered = new List<SweepEntry>(entries.Count);
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i].Cues != null && entries[i].Cues.Count > 0)
                {
                    ordered.Add(entries[i]);
                }
            }

            ordered.Sort(CompareWestToEast);

            List<SweepStep> audible = new List<SweepStep>(ordered.Count);
            for (int i = 0; i < ordered.Count; i++)
            {
                float pan;
                float semitones;
                float gainScale;
                if (!DirectionalCueMath.TryCompute(origin, ordered[i].Position, geometry, out pan, out semitones, out gainScale))
                {
                    continue;
                }

                audible.Add(new SweepStep(ordered[i].Cues, pan, semitones, gainScale, 0f));
            }

            if (audible.Count == 0)
            {
                return steps;
            }

            float gap = Clamp(TargetWindowSeconds / audible.Count, MinimumGapSeconds, MaximumGapSeconds);
            float time = 0f;
            for (int i = 0; i < audible.Count; i++)
            {
                SweepStep step = audible[i];
                steps.Add(new SweepStep(step.Cues, step.Pan, step.Semitones, step.GainScale, time));

                float end = time + CueLibrary.ComputeStackDurationSeconds(step.Cues, durationSeconds);
                float earliestNext = end + MinimumSeparationSeconds;
                time += gap;
                if (earliestNext > time)
                {
                    time = earliestNext;
                }
            }

            return steps;
        }

        private static int CompareWestToEast(SweepEntry left, SweepEntry right)
        {
            int compare = left.Position.x.CompareTo(right.Position.x);
            return compare != 0 ? compare : left.Position.y.CompareTo(right.Position.y);
        }

        private static void Advance(float deltaSeconds)
        {
            if (_steps == null)
            {
                return;
            }

            _elapsedSeconds += deltaSeconds;
            FireDueSteps();
        }

        private static void FireDueSteps()
        {
            while (_steps != null && _nextIndex < _steps.Count && _steps[_nextIndex].TimeSeconds <= _elapsedSeconds)
            {
                SweepStep step = _steps[_nextIndex];
                _nextIndex++;
                CueLibrary.PlayCues(step.Cues, step.Pan, step.GainScale, step.Semitones);
            }

            if (_steps != null && _nextIndex >= _steps.Count)
            {
                Cancel();
            }
        }

        private static void EnsureTicker()
        {
            if (_gameObject != null)
            {
                return;
            }

            _gameObject = new GameObject("SongsOfConquestAccess_SweepPlayer");
            _gameObject.hideFlags = HideFlags.HideInHierarchy;
            _gameObject.AddComponent<SweepTicker>();
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class SweepTicker : MonoBehaviour
        {
            private void Update()
            {
                try
                {
                    Advance(Time.unscaledDeltaTime);
                }
                catch (Exception exception)
                {
                    Cancel();
                    SocAccessPlugin.Instance?.LogWarning("Sonar sweep scheduling failed: " + exception.Message);
                }
            }
        }
    }
}
