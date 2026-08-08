using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// A single synthesised sound source evaluated in its own local time base.
    /// Implementations must return 0 outside [0, DurationSeconds) so the timeline can
    /// mix grains without tracking their bounds.
    /// </summary>
    internal abstract class Grain
    {
        public abstract float DurationSeconds { get; }

        public abstract float Evaluate(float seconds);
    }

    internal sealed class SineGrain : Grain
    {
        private readonly float _frequencyHz;
        private readonly float _durationSeconds;

        public SineGrain(float frequencyHz, float durationSeconds)
        {
            _frequencyHz = frequencyHz;
            _durationSeconds = durationSeconds < 0f ? 0f : durationSeconds;
        }

        public override float DurationSeconds
        {
            get { return _durationSeconds; }
        }

        public float FrequencyHz
        {
            get { return _frequencyHz; }
        }

        public override float Evaluate(float seconds)
        {
            if (seconds < 0f || seconds >= _durationSeconds)
            {
                return 0f;
            }

            return (float)Math.Sin(2.0 * Math.PI * _frequencyHz * seconds);
        }
    }

    /// <summary>
    /// Linear zigzag between -1 and +1, starting at 0 and rising, so it shares the
    /// sine grain's phase and zero-crossing count.
    /// </summary>
    internal sealed class TriangleGrain : Grain
    {
        private readonly float _frequencyHz;
        private readonly float _durationSeconds;

        public TriangleGrain(float frequencyHz, float durationSeconds)
        {
            _frequencyHz = frequencyHz;
            _durationSeconds = durationSeconds < 0f ? 0f : durationSeconds;
        }

        public override float DurationSeconds
        {
            get { return _durationSeconds; }
        }

        public float FrequencyHz
        {
            get { return _frequencyHz; }
        }

        public override float Evaluate(float seconds)
        {
            if (seconds < 0f || seconds >= _durationSeconds)
            {
                return 0f;
            }

            double phase = _frequencyHz * (double)seconds;
            phase = phase - Math.Floor(phase);
            if (phase < 0.25)
            {
                return (float)(4.0 * phase);
            }

            if (phase < 0.75)
            {
                return (float)(2.0 - 4.0 * phase);
            }

            return (float)(4.0 * phase - 4.0);
        }
    }
}
