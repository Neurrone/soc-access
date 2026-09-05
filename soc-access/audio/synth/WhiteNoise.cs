// Ported from the Tanglebeep Tangledeep accessibility mod by Austin Hicks, used with the
// author's permission. Adapted to this repo's style and C# 7.3.
using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// White noise: uniform samples in [-1, 1], which have a flat expected power spectrum (equal
    /// power per Hz, uncorrelated samples). A band-pass over this picks out a pitch while keeping
    /// the random fine structure that makes the tone sound alive and lets slices decorrelate.
    /// </summary>
    public static class WhiteNoise
    {
        public static void Fill(float[] buffer, Random rng)
        {
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = (float)(rng.NextDouble() * 2.0 - 1.0);
            }
        }
    }
}
