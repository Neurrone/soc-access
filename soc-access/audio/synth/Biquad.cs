// Ported from the Tanglebeep Tangledeep accessibility mod by Austin Hicks, used with the
// author's permission. Adapted to this repo's style and C# 7.3.
using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// A direct-form-I biquad (two-pole/two-zero IIR) filter, used offline to colour white noise.
    /// Stateful, so the output depends on the previous two input and output samples: run it
    /// sequentially over a buffer from a zeroed state, never sample it pointwise. Coefficients are
    /// computed in double; the only factory here is the RBJ constant-0 dB-peak band-pass.
    /// </summary>
    internal sealed class Biquad
    {
        private double _b0;
        private double _b1;
        private double _b2;
        private double _a1;
        private double _a2;
        private double _x1;
        private double _x2;
        private double _y1;
        private double _y2;

        /// <summary>
        /// RBJ band-pass with 0 dB peak gain at <paramref name="centerHz"/>. <paramref name="q"/>
        /// is the resonance (centre divided by bandwidth); higher Q is a narrower, more tonal band.
        /// </summary>
        public static Biquad Bandpass(double centerHz, double q, int sampleRate)
        {
            double w0 = 2.0 * Math.PI * centerHz / sampleRate;
            double cosW0 = Math.Cos(w0);
            double alpha = Math.Sin(w0) / (2.0 * q);
            double a0 = 1.0 + alpha;

            Biquad filter = new Biquad();
            filter._b0 = alpha / a0;
            filter._b1 = 0.0;
            filter._b2 = -alpha / a0;
            filter._a1 = -2.0 * cosW0 / a0;
            filter._a2 = (1.0 - alpha) / a0;
            return filter;
        }

        public void Reset()
        {
            _x1 = 0.0;
            _x2 = 0.0;
            _y1 = 0.0;
            _y2 = 0.0;
        }

        public float Process(float x)
        {
            double y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
            _x2 = _x1;
            _x1 = x;
            _y2 = _y1;
            _y1 = y;
            return (float)y;
        }

        public void ProcessInPlace(float[] buffer)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = Process(buffer[i]);
            }
        }
    }
}
