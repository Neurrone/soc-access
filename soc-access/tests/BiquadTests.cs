using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class BiquadTests
    {
        private const int SampleRate = 44100;

        [TestMethod]
        public void PassesTheCenterFrequency()
        {
            double atCenter = FilteredTailRms(1000.0, 1000.0, 15.0);

            Assert.IsTrue(atCenter > 0.3, "center tone RMS was " + atCenter);
        }

        [TestMethod]
        public void RejectsFrequenciesFarFromCenter()
        {
            double atCenter = FilteredTailRms(1000.0, 1000.0, 15.0);
            double farAbove = FilteredTailRms(8000.0, 1000.0, 15.0);
            double farBelow = FilteredTailRms(120.0, 1000.0, 15.0);

            Assert.IsTrue(farAbove < atCenter * 0.1, "far-above leaked: " + farAbove + " vs " + atCenter);
            Assert.IsTrue(farBelow < atCenter * 0.1, "far-below leaked: " + farBelow + " vs " + atCenter);
        }

        [TestMethod]
        public void HigherQIsNarrower()
        {
            double offCenterLowQ = FilteredTailRms(1300.0, 1000.0, 5.0);
            double offCenterHighQ = FilteredTailRms(1300.0, 1000.0, 30.0);

            Assert.IsTrue(offCenterHighQ < offCenterLowQ, "highQ " + offCenterHighQ + " should be below lowQ " + offCenterLowQ);
        }

        [TestMethod]
        public void ResetClearsFilterState()
        {
            Biquad filter = Biquad.Bandpass(1000.0, 15.0, SampleRate);
            for (int i = 0; i < 500; i++)
            {
                filter.Process(1f);
            }

            filter.Reset();

            Biquad fresh = Biquad.Bandpass(1000.0, 15.0, SampleRate);
            Assert.AreEqual(fresh.Process(0.5f), filter.Process(0.5f), 0.0000001f);
        }

        private static double FilteredTailRms(double signalHz, double centerHz, double q)
        {
            float[] signal = Sine(signalHz, 8000);
            Biquad.Bandpass(centerHz, q, SampleRate).ProcessInPlace(signal);
            return TailRms(signal, 2000);
        }

        private static float[] Sine(double frequency, int count)
        {
            float[] buffer = new float[count];
            for (int i = 0; i < count; i++)
            {
                buffer[i] = (float)Math.Sin(2.0 * Math.PI * frequency * i / SampleRate);
            }

            return buffer;
        }

        private static double TailRms(float[] buffer, int skip)
        {
            double sum = 0.0;
            for (int i = skip; i < buffer.Length; i++)
            {
                sum += (double)buffer[i] * buffer[i];
            }

            return Math.Sqrt(sum / (buffer.Length - skip));
        }
    }
}
