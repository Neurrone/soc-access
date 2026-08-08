using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// Turns a <see cref="CueSpec"/> into interleaved stereo samples. Rendering is
    /// deterministic: the same spec always produces an identical buffer, because noise
    /// segments seed their RNG from a stable hash of the segment's own parameters.
    /// </summary>
    internal static class CueRenderer
    {
        public static float[] Render(CueSpec spec)
        {
            return Render(spec, 0f, 1f, 0f);
        }

        public static float[] Render(CueSpec spec, float panOffset, float gainScale, float rateSemitoneOffset)
        {
            if (spec == null || spec.Segments == null)
            {
                return new float[0];
            }

            GrainTimeline timeline = new GrainTimeline();
            for (int i = 0; i < spec.Segments.Count; i++)
            {
                CueSegment segment = spec.Segments[i];
                if (segment == null || segment.DurationMs <= 0f)
                {
                    continue;
                }

                float durationSeconds = segment.DurationMs / 1000f;
                float attackSeconds = segment.AttackMs / 1000f;
                float releaseSeconds = segment.ReleaseMs / 1000f;
                if (attackSeconds < 0f)
                {
                    attackSeconds = 0f;
                }

                if (releaseSeconds < 0f)
                {
                    releaseSeconds = 0f;
                }

                // Keep the envelope inside the requested duration; scale both edges down
                // proportionally rather than truncating one of them.
                float edges = attackSeconds + releaseSeconds;
                if (edges > durationSeconds && edges > 0f)
                {
                    float scale = durationSeconds / edges;
                    attackSeconds = attackSeconds * scale;
                    releaseSeconds = releaseSeconds * scale;
                }

                float sustainSeconds = durationSeconds - attackSeconds - releaseSeconds;
                if (sustainSeconds < 0f)
                {
                    sustainSeconds = 0f;
                }

                Grain inner = CreateGrain(segment, durationSeconds);
                AdsrGrain enveloped = new AdsrGrain(inner, attackSeconds, 0f, 1f, sustainSeconds, releaseSeconds);

                float pan = Clamp(segment.Pan + panOffset, -1f, 1f);
                float rate = SemitoneMath.ToRate(segment.RateSemitones + rateSemitoneOffset);
                float gain = segment.Gain * spec.MasterGain * gainScale;
                timeline.Add(enveloped, segment.StartMs / 1000f, rate, pan, gain);
            }

            return timeline.Render();
        }

        private static Grain CreateGrain(CueSegment segment, float durationSeconds)
        {
            switch (segment.Waveform)
            {
                case CueWaveform.Triangle:
                    return new TriangleGrain(segment.FrequencyHz, durationSeconds);
                case CueWaveform.Noise:
                    return new NoiseGrain(
                        segment.FrequencyHz,
                        durationSeconds,
                        segment.NoiseQ,
                        ComputeNoiseSeed(segment));
                default:
                    return new SineGrain(segment.FrequencyHz, durationSeconds);
            }
        }

        /// <summary>
        /// FNV-1a over the parameters that shape the noise. Deliberately avoids
        /// string.GetHashCode, which is not stable across processes.
        /// </summary>
        private static int ComputeNoiseSeed(CueSegment segment)
        {
            unchecked
            {
                uint hash = 2166136261u;
                hash = Mix(hash, FloatBits(segment.FrequencyHz));
                hash = Mix(hash, FloatBits(segment.DurationMs));
                hash = Mix(hash, FloatBits(segment.NoiseQ));
                int seed = (int)(hash & 0x7FFFFFFF);
                return seed == 0 ? NoiseGrain.DefaultSeed : seed;
            }
        }

        private static uint Mix(uint hash, uint value)
        {
            unchecked
            {
                for (int i = 0; i < 4; i++)
                {
                    hash ^= (value >> (i * 8)) & 0xFF;
                    hash *= 16777619u;
                }

                return hash;
            }
        }

        private static uint FloatBits(float value)
        {
            byte[] bytes = BitConverter.GetBytes(value);
            return BitConverter.ToUInt32(bytes, 0);
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
