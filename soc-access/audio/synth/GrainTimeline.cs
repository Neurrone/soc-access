// Interaural time delay (ITD), the integer/fractional delay split and the allpass flush are
// ported from the Tanglebeep Tangledeep accessibility mod by Austin Hicks, used with the
// author's permission. The pan law is the same constant-power formula as Tanglebeep's PanLaw.
// The output-stage limiter and the pan-aware padding are this repo's.
using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// Places grains on a stereo timeline and mixes them down to an interleaved buffer.
    /// </summary>
    public sealed class GrainTimeline
    {
        public const int SampleRate = 44100;

        /// <summary>
        /// Level above which the output-stage soft limiter starts bending. Below it the mix is
        /// bit-for-bit linear, which matters because cue gains are tuned by ear; a plain tanh over
        /// the whole range would quietly shave ~2 dB off normal cues. Clamping per voice instead
        /// would distort each voice while still letting the sum clip, so a single limiter at the
        /// output is the correct place for it.
        /// </summary>
        public const float LimiterThreshold = 0.8f;

        /// <summary>
        /// Hard ceiling the limiter asymptotes to. Kept just below full scale so the result is
        /// strictly inside [-1, 1] even once tanh saturates to exactly 1.
        /// </summary>
        public const float LimiterCeiling = 0.999f;

        /// <summary>
        /// Maximum interaural time delay, in seconds, applied at full pan. A panned sound reaches
        /// the far ear slightly later than the near ear; we model that by delaying the far channel
        /// by |pan| * MaxInterauralDelaySeconds. The classic Woodworth maximum head ITD is ~0.66 ms;
        /// this is a tunable approximation, not a measured HRTF. Set to 0 to disable ITD entirely.
        /// </summary>
        public const double MaxInterauralDelaySeconds = 0.0007;

        /// <summary>
        /// When true, the far-ear delay is realised with sub-sample precision: an integer sample
        /// shift plus a first-order <see cref="AllpassFractionalDelay"/> for the fractional
        /// remainder, so the ITD moves continuously with pan instead of snapping in 1-sample steps.
        /// When false the delay is rounded to the nearest whole sample. Kept as a runtime switch,
        /// not a const, so the two can be compared by ear without the rounding branch being
        /// compiled out as dead code.
        /// </summary>
        public static readonly bool UseFractionalDelay = true;

        /// <summary>
        /// Extra tail frames rendered past each fractionally-delayed grain so the allpass filter's
        /// ringout is captured rather than clipped at the buffer's end. The fractional part is held
        /// in [0.5, 1.5) (pole magnitude &lt;= 1/3), so the tail is inaudible within a handful of
        /// samples; 32 is comfortable margin.
        /// </summary>
        public const int AllpassFlushFrames = 32;

        private readonly List<Placement> _placements = new List<Placement>();

        public void Add(Grain grain, float startSeconds, float rate, float pan, float gain)
        {
            if (grain == null || rate <= 0f || grain.DurationSeconds <= 0f)
            {
                return;
            }

            if (startSeconds < 0f)
            {
                startSeconds = 0f;
            }

            int startFrame = (int)Math.Round(startSeconds * (double)SampleRate);
            int frameCount = (int)Math.Ceiling(grain.DurationSeconds / (double)rate * SampleRate);
            if (frameCount < 1)
            {
                return;
            }

            _placements.Add(new Placement(grain, startFrame, frameCount, rate, Clamp(pan, -1f, 1f), gain));
        }

        /// <summary>
        /// Frames of actual grain content, ignoring the ITD tail padding that
        /// <see cref="Render"/> appends. See <see cref="ItdPaddingFrames"/>.
        /// </summary>
        public int FrameCount
        {
            get
            {
                int frames = 0;
                for (int i = 0; i < _placements.Count; i++)
                {
                    int end = _placements[i].StartFrame + _placements[i].FrameCount;
                    if (end > frames)
                    {
                        frames = end;
                    }
                }

                return frames;
            }
        }

        /// <summary>
        /// Worst-case ITD padding, at full pan. <see cref="Render"/> uses the padding implied by the
        /// panning actually present, so a fully centred timeline renders with no padding at all.
        /// </summary>
        public static int ItdPaddingFrames(int sampleRate)
        {
            return PaddingForDelay(MaxInterauralDelaySeconds * sampleRate);
        }

        /// <summary>
        /// Returns interleaved stereo samples (L, R, L, R, ...). The buffer is content frames plus
        /// enough ITD tail padding that a panned grain's delayed far-ear copy is never clipped off
        /// the end; a timeline with every grain dead centre gets no padding.
        /// </summary>
        public float[] Render()
        {
            int contentFrames = FrameCount;
            if (contentFrames <= 0)
            {
                return new float[0];
            }

            int paddedFrames = contentFrames + ComputeItdPadding();
            float[] output = new float[paddedFrames * 2];

            for (int i = 0; i < _placements.Count; i++)
            {
                RenderPlacement(_placements[i], output, paddedFrames);
            }

            for (int i = 0; i < output.Length; i++)
            {
                output[i] = Limit(output[i]);
            }

            return output;
        }

        private static void RenderPlacement(Placement placement, float[] output, int paddedFrames)
        {
            float leftGain;
            float rightGain;
            PanGains(placement.Pan, out leftGain, out rightGain);
            leftGain = leftGain * placement.Gain;
            rightGain = rightGain * placement.Gain;

            // Split the far-ear delay into an integer sample shift and, optionally, a fractional
            // remainder handled by a first-order allpass.
            double delaySamples = Math.Abs(placement.Pan) * MaxInterauralDelaySeconds * SampleRate;
            int integerDelay;
            AllpassFractionalDelay allpass = null;
            if (!UseFractionalDelay)
            {
                integerDelay = (int)Math.Round(delaySamples);
            }
            else if (delaySamples < 0.5)
            {
                // Below half a sample the ITD is negligible; emit zero delay rather than push the
                // allpass toward its d -> 0 pole.
                integerDelay = 0;
            }
            else
            {
                // Keep the fractional part in [0.5, 1.5) by borrowing a sample from the integer
                // part, so the allpass pole stays well inside the unit circle.
                integerDelay = (int)Math.Floor(delaySamples - 0.5);
                allpass = new AllpassFractionalDelay(delaySamples - integerDelay);
            }

            bool farIsLeft = placement.Pan > 0f;
            bool farIsRight = placement.Pan < 0f;

            for (int frame = 0; frame < placement.FrameCount; frame++)
            {
                float innerSeconds = frame / (float)SampleRate * placement.Rate;
                float value = placement.Grain.Evaluate(innerSeconds);
                int target = placement.StartFrame + frame;

                // The allpass is stateful, so every sample must be fed in order; there is no
                // shortcut for silent samples here.
                if (farIsLeft)
                {
                    output[2 * target + 1] += value * rightGain;
                    float far = allpass != null ? allpass.Process(value) : value;
                    output[2 * (target + integerDelay)] += far * leftGain;
                }
                else if (farIsRight)
                {
                    output[2 * target] += value * leftGain;
                    float far = allpass != null ? allpass.Process(value) : value;
                    output[2 * (target + integerDelay) + 1] += far * rightGain;
                }
                else
                {
                    output[2 * target] += value * leftGain;
                    output[2 * target + 1] += value * rightGain;
                }
            }

            if (allpass == null)
            {
                return;
            }

            int lastFrame = placement.StartFrame + placement.FrameCount;
            for (int k = 0; k < AllpassFlushFrames; k++)
            {
                int frame = lastFrame + k + integerDelay;
                if (frame >= paddedFrames)
                {
                    break;
                }

                float far = allpass.Process(0f);
                if (farIsLeft)
                {
                    output[2 * frame] += far * leftGain;
                }
                else
                {
                    output[2 * frame + 1] += far * rightGain;
                }
            }
        }

        private int ComputeItdPadding()
        {
            float maxAbsPan = 0f;
            for (int i = 0; i < _placements.Count; i++)
            {
                float magnitude = Math.Abs(_placements[i].Pan);
                if (magnitude > maxAbsPan)
                {
                    maxAbsPan = magnitude;
                }
            }

            return PaddingForDelay(maxAbsPan * MaxInterauralDelaySeconds * SampleRate);
        }

        private static int PaddingForDelay(double delaySamples)
        {
            if (!UseFractionalDelay)
            {
                return (int)Math.Round(delaySamples);
            }

            if (delaySamples < 0.5)
            {
                return 0;
            }

            return (int)Math.Ceiling(delaySamples) + AllpassFlushFrames;
        }

        /// <summary>Constant-power pan law: left^2 + right^2 == 1 for any pan.</summary>
        public static void PanGains(float pan, out float left, out float right)
        {
            double angle = (Clamp(pan, -1f, 1f) + 1.0) * Math.PI / 4.0;
            left = (float)Math.Cos(angle);
            right = (float)Math.Sin(angle);
        }

        public static float Limit(float value)
        {
            float magnitude = value < 0f ? -value : value;
            if (magnitude <= LimiterThreshold)
            {
                return value;
            }

            float headroom = LimiterCeiling - LimiterThreshold;
            float limited = LimiterThreshold + headroom * (float)Math.Tanh((magnitude - LimiterThreshold) / headroom);
            return value < 0f ? -limited : limited;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private struct Placement
        {
            public Placement(Grain grain, int startFrame, int frameCount, float rate, float pan, float gain)
            {
                Grain = grain;
                StartFrame = startFrame;
                FrameCount = frameCount;
                Rate = rate;
                Pan = pan;
                Gain = gain;
            }

            public readonly Grain Grain;
            public readonly int StartFrame;
            public readonly int FrameCount;
            public readonly float Rate;
            public readonly float Pan;
            public readonly float Gain;
        }
    }
}
