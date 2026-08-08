// Band-pass, warm-up-discard and RMS-normalisation approach ported from the Tanglebeep
// Tangledeep accessibility mod by Austin Hicks, used with the author's permission. The
// deterministic per-segment seeding and the fixed-duration grain shape are this repo's.
using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// Band-passed white noise rendered once at construction and read back with linear
    /// interpolation. Uses the RBJ constant-0 dB-peak band-pass from <see cref="Biquad"/>; a narrow
    /// band removes most of the noise energy, so the buffer is RMS-normalised to
    /// <see cref="DefaultTargetRms"/> afterwards to keep cue gains comparable with the tonal grains.
    ///
    /// <para>Unlike <see cref="NoisePool"/>, which hands out decorrelated slices, this grain is
    /// fully determined by its constructor arguments: the same parameters always produce the same
    /// samples, which is what lets <see cref="CueRenderer"/> guarantee reproducible cues.</para>
    /// </summary>
    internal sealed class NoiseGrain : Grain
    {
        public const float DefaultQ = 30f;
        public const float DefaultTargetRms = 0.25f;
        public const int DefaultSeed = 0x5EED;

        private const int WarmupSamples = 4096;

        private readonly BufferGrain _buffer;
        private readonly float _durationSeconds;

        public NoiseGrain(float centerFrequencyHz, float durationSeconds)
            : this(centerFrequencyHz, durationSeconds, DefaultQ, DefaultSeed, DefaultTargetRms, GrainTimeline.SampleRate)
        {
        }

        public NoiseGrain(float centerFrequencyHz, float durationSeconds, float q, int seed)
            : this(centerFrequencyHz, durationSeconds, q, seed, DefaultTargetRms, GrainTimeline.SampleRate)
        {
        }

        public NoiseGrain(float centerFrequencyHz, float durationSeconds, float q, int seed, float targetRms, int sampleRate)
        {
            if (sampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException("sampleRate");
            }

            _durationSeconds = durationSeconds < 0f ? 0f : durationSeconds;
            int count = (int)Math.Ceiling(_durationSeconds * (double)sampleRate);
            if (count < 1)
            {
                count = 1;
            }

            float[] data = Generate(centerFrequencyHz, q, seed, targetRms, sampleRate, count);
            _buffer = new BufferGrain(data, 0, count, sampleRate);
        }

        public override float DurationSeconds
        {
            get { return _durationSeconds; }
        }

        public override float Evaluate(float seconds)
        {
            if (seconds < 0f || seconds >= _durationSeconds)
            {
                return 0f;
            }

            return _buffer.Evaluate(seconds);
        }

        private static float[] Generate(
            float centerFrequencyHz,
            float q,
            int seed,
            float targetRms,
            int sampleRate,
            int count)
        {
            double nyquist = sampleRate * 0.5;
            double center = centerFrequencyHz;
            if (center < 1.0)
            {
                center = 1.0;
            }

            if (center > nyquist - 1.0)
            {
                center = nyquist - 1.0;
            }

            double resonance = q < 0.1f ? 0.1 : q;

            // Generate warmup + count, filter from a zeroed state, then drop the leading warmup so
            // the filter's ring-up transient never reaches the buffer or skews the RMS.
            float[] raw = new float[count + WarmupSamples];
            WhiteNoise.Fill(raw, new Random(seed));
            Biquad.Bandpass(center, resonance, sampleRate).ProcessInPlace(raw);

            float[] data = new float[count];
            Array.Copy(raw, WarmupSamples, data, 0, count);
            if (targetRms > 0f)
            {
                NoisePool.NormalizeRms(data, targetRms);
            }

            return data;
        }
    }
}
