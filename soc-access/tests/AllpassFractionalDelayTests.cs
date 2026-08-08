using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AllpassFractionalDelayTests
    {
        [TestMethod]
        public void PreservesMagnitude()
        {
            AllpassFractionalDelay filter = new AllpassFractionalDelay(0.8);
            const double frequency = 0.1;
            const int count = 4000;
            float[] output = new float[count];
            double inputSum = 0.0;
            for (int i = 0; i < count; i++)
            {
                float x = (float)Math.Sin(2.0 * Math.PI * frequency * i);
                output[i] = filter.Process(x);
                inputSum += x * x;
            }

            double inputRms = Math.Sqrt(inputSum / count);
            const int skip = 100;
            double outputSum = 0.0;
            for (int i = skip; i < count; i++)
            {
                outputSum += output[i] * (double)output[i];
            }

            double outputRms = Math.Sqrt(outputSum / (count - skip));
            Assert.AreEqual(inputRms, outputRms, 0.01);
        }

        [TestMethod]
        public void DelaysByRequestedFractionAtHalfASample()
        {
            AssertDelay(0.5);
        }

        [TestMethod]
        public void DelaysByRequestedFractionBelowOneSample()
        {
            AssertDelay(0.8);
        }

        [TestMethod]
        public void DelaysByRequestedFractionAboveOneSample()
        {
            AssertDelay(1.2);
        }

        private static void AssertDelay(double fractionalDelay)
        {
            AllpassFractionalDelay filter = new AllpassFractionalDelay(fractionalDelay);
            const double frequency = 0.002;
            const int count = 8000;
            float[] input = new float[count];
            float[] output = new float[count];
            for (int i = 0; i < count; i++)
            {
                input[i] = (float)Math.Sin(2.0 * Math.PI * frequency * i);
                output[i] = filter.Process(input[i]);
            }

            Assert.AreEqual(fractionalDelay, SignalLag.Estimate(input, output, 4), 0.05);
        }
    }

    /// <summary>
    /// Fractional lag by which one signal trails another, via cross-correlation with parabolic
    /// interpolation around the integer peak. Ported from the Tanglebeep test helpers.
    /// </summary>
    internal static class SignalLag
    {
        public static double Estimate(float[] reference, float[] delayed, int maxLag)
        {
            int best = 0;
            double bestCorrelation = double.NegativeInfinity;
            for (int lag = 0; lag <= maxLag; lag++)
            {
                double correlation = Correlate(reference, delayed, lag);
                if (correlation > bestCorrelation)
                {
                    bestCorrelation = correlation;
                    best = lag;
                }
            }

            double below = Correlate(reference, delayed, best - 1);
            double centre = Correlate(reference, delayed, best);
            double above = Correlate(reference, delayed, best + 1);
            double denominator = below - 2.0 * centre + above;
            double offset = denominator != 0.0 ? 0.5 * (below - above) / denominator : 0.0;
            return best + offset;
        }

        private static double Correlate(float[] reference, float[] delayed, int lag)
        {
            double sum = 0.0;
            for (int i = lag < 0 ? -lag : 0; i < reference.Length && i + lag < delayed.Length; i++)
            {
                sum += reference[i] * (double)delayed[i + lag];
            }

            return sum;
        }
    }
}
