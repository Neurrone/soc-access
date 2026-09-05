using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    public static class SemitoneMath
    {
        public static float ToRate(float semitones)
        {
            if (semitones == 0f)
            {
                return 1f;
            }

            return (float)Math.Pow(2.0, semitones / 12.0);
        }
    }
}
