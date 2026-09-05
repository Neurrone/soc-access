// Envelope segment math and the min(inner, envelope) duration rule ported from the Tanglebeep
// Tangledeep accessibility mod by Austin Hicks, used with the author's permission. Constructor
// shape and the EvaluateEnvelope accessor are this repo's.
using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// An attack/decay/sustain/release amplitude envelope wrapping another grain. The envelope's own
    /// length is attack + decay + sustain + release; the wrapped grain has its own length; the
    /// result lasts the lesser of the two, so this both shapes amplitude and bounds duration.
    ///
    /// <para>Callers should pass an attack of at least ~0.004 s to avoid an audible click at onset.
    /// This is not enforced, so deliberate percussive transients remain possible.</para>
    /// </summary>
    public sealed class AdsrGrain : Grain
    {
        private readonly Grain _inner;
        private readonly float _attackSeconds;
        private readonly float _decaySeconds;
        private readonly float _sustainLevel;
        private readonly float _sustainSeconds;
        private readonly float _releaseSeconds;
        private readonly float _envelopeDuration;

        public AdsrGrain(
            Grain inner,
            float attackSeconds,
            float decaySeconds,
            float sustainLevel,
            float sustainSeconds,
            float releaseSeconds)
        {
            _inner = inner;
            _attackSeconds = attackSeconds < 0f ? 0f : attackSeconds;
            _decaySeconds = decaySeconds < 0f ? 0f : decaySeconds;
            _sustainLevel = sustainLevel < 0f ? 0f : (sustainLevel > 1f ? 1f : sustainLevel);
            _sustainSeconds = sustainSeconds < 0f ? 0f : sustainSeconds;
            _releaseSeconds = releaseSeconds < 0f ? 0f : releaseSeconds;
            _envelopeDuration = _attackSeconds + _decaySeconds + _sustainSeconds + _releaseSeconds;
        }

        /// <summary>Total length of the envelope itself, ignoring the wrapped grain.</summary>
        public float EnvelopeDuration
        {
            get { return _envelopeDuration; }
        }

        public override float DurationSeconds
        {
            get
            {
                if (_inner == null)
                {
                    return 0f;
                }

                return Math.Min(_inner.DurationSeconds, _envelopeDuration);
            }
        }

        public override float Evaluate(float seconds)
        {
            if (_inner == null || seconds < 0f || seconds >= DurationSeconds)
            {
                return 0f;
            }

            return EvaluateEnvelope(seconds) * _inner.Evaluate(seconds);
        }

        /// <summary>
        /// The envelope gain at <paramref name="seconds"/>, scaled by the sustain level across the
        /// held segment and 0 outside the envelope. Zero-length segments are skipped without
        /// dividing by zero, because each boundary test fails when its segment has no width.
        /// </summary>
        public float EvaluateEnvelope(float seconds)
        {
            if (seconds < 0f)
            {
                return 0f;
            }

            float decayEnd = _attackSeconds + _decaySeconds;
            float sustainEnd = decayEnd + _sustainSeconds;
            float releaseEnd = sustainEnd + _releaseSeconds;

            if (seconds < _attackSeconds)
            {
                return seconds / _attackSeconds;
            }

            if (seconds < decayEnd)
            {
                float progress = (seconds - _attackSeconds) / _decaySeconds;
                return 1f + (_sustainLevel - 1f) * progress;
            }

            if (seconds < sustainEnd)
            {
                return _sustainLevel;
            }

            if (seconds < releaseEnd)
            {
                float progress = (seconds - sustainEnd) / _releaseSeconds;
                return _sustainLevel * (1f - progress);
            }

            return 0f;
        }
    }
}
