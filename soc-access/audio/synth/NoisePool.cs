// Ported from the Tanglebeep Tangledeep accessibility mod by Austin Hicks, used with the
// author's permission. Adapted to this repo's style and C# 7.3.
using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// A reservoir of band-pass-filtered white noise for one frequency band. Pre-filters a buffer
    /// once, amortising the cost, then hands out successive non-overlapping slices via
    /// <see cref="Take"/>; when the buffer runs short it regenerates from fresh noise. Each slice is
    /// a different draw, so two slices taken for the same band stay decorrelated and remain
    /// perceptually separate when panned apart.
    ///
    /// <para>Generation: fill <c>length + warmup</c> samples of white noise, band-pass it from a
    /// zeroed filter, discard the leading <c>warmup</c> samples (the filter's settling transient),
    /// then RMS-normalise so perceived loudness is independent of Q.</para>
    /// </summary>
    public sealed class NoisePool
    {
        private readonly double _centerHz;
        private readonly double _q;
        private readonly int _sampleRate;
        private readonly int _lengthSamples;
        private readonly int _warmupSamples;
        private readonly double _targetRms;
        private readonly Random _rng;

        private float[] _data;
        private int _cursor;

        public NoisePool(
            double centerHz,
            double q,
            int sampleRate,
            int lengthSamples,
            int warmupSamples,
            double targetRms,
            int seed)
        {
            _centerHz = centerHz;
            _q = q;
            _sampleRate = sampleRate;
            _lengthSamples = lengthSamples;
            _warmupSamples = warmupSamples;
            _targetRms = targetRms;
            _rng = new Random(seed);
            Refill();
        }

        public int SampleRate
        {
            get { return _sampleRate; }
        }

        /// <summary>A grain over the next <paramref name="seconds"/> of pool, advancing the cursor.</summary>
        public BufferGrain Take(double seconds)
        {
            int count = (int)Math.Ceiling(seconds * _sampleRate);
            if (count > _data.Length)
            {
                count = _data.Length;
            }

            if (_cursor + count > _data.Length)
            {
                // Not enough left; regenerate. Old slices keep referencing the old array.
                Refill();
            }

            BufferGrain grain = new BufferGrain(_data, _cursor, count, _sampleRate);
            _cursor += count;
            return grain;
        }

        private void Refill()
        {
            float[] raw = new float[_lengthSamples + _warmupSamples];
            WhiteNoise.Fill(raw, _rng);
            Biquad.Bandpass(_centerHz, _q, _sampleRate).ProcessInPlace(raw);

            float[] data = new float[_lengthSamples];
            Array.Copy(raw, _warmupSamples, data, 0, _lengthSamples);
            NormalizeRms(data, _targetRms);

            _data = data;
            _cursor = 0;
        }

        public static void NormalizeRms(float[] buffer, double target)
        {
            double sumSquares = 0.0;
            for (int i = 0; i < buffer.Length; i++)
            {
                sumSquares += (double)buffer[i] * buffer[i];
            }

            double rms = Math.Sqrt(sumSquares / buffer.Length);
            if (rms < 1e-9)
            {
                return;
            }

            double scale = target / rms;
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)(buffer[i] * scale);
            }
        }
    }
}
