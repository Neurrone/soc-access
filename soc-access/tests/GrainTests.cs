using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class GrainTests
    {
        private const int SampleRate = 44100;

        [TestMethod]
        public void SineGrainProducesRequestedFrequency()
        {
            const float frequency = 1000f;
            const float duration = 0.1f;
            SineGrain grain = new SineGrain(frequency, duration);

            int samples = (int)(duration * SampleRate);
            float[] rendered = new float[samples];
            for (int i = 0; i < samples; i++)
            {
                rendered[i] = grain.Evaluate(i / (float)SampleRate);
            }

            int crossings = CountZeroCrossings(rendered);
            int expected = (int)(2f * frequency * duration);
            Assert.IsTrue(Math.Abs(crossings - expected) <= 2, "Expected about " + expected + " zero crossings, saw " + crossings);
        }

        [TestMethod]
        public void SineGrainIsSilentOutsideItsDuration()
        {
            SineGrain grain = new SineGrain(440f, 0.05f);

            Assert.AreEqual(0f, grain.Evaluate(-0.001f));
            Assert.AreEqual(0f, grain.Evaluate(0.05f));
            Assert.AreEqual(0f, grain.Evaluate(1f));
        }

        [TestMethod]
        public void TriangleGrainStaysInRangeAndReachesBothExtremes()
        {
            TriangleGrain grain = new TriangleGrain(100f, 0.05f);

            float minimum = 1f;
            float maximum = -1f;
            for (int i = 0; i < 50000; i++)
            {
                float value = grain.Evaluate(i * 0.000001f);
                Assert.IsTrue(value >= -1f && value <= 1f, "Triangle left [-1, 1] with " + value);
                if (value < minimum)
                {
                    minimum = value;
                }

                if (value > maximum)
                {
                    maximum = value;
                }
            }

            Assert.AreEqual(1f, maximum, 0.001f);
            Assert.AreEqual(-1f, minimum, 0.001f);
        }

        [TestMethod]
        public void TriangleGrainStartsAtZeroAndRises()
        {
            TriangleGrain grain = new TriangleGrain(100f, 0.05f);

            Assert.AreEqual(0f, grain.Evaluate(0f), 0.0001f);
            Assert.IsTrue(grain.Evaluate(0.001f) > 0f);
        }

        [TestMethod]
        public void AdsrEnvelopeDurationIsSumOfStages()
        {
            AdsrGrain grain = CreateEnvelopedOnes(0.01f, 0.02f, 0.5f, 0.03f, 0.04f);

            Assert.AreEqual(0.1f, grain.DurationSeconds, 0.0001f);
        }

        [TestMethod]
        public void AdsrStartsAtSilenceToAvoidClicks()
        {
            AdsrGrain grain = CreateEnvelopedOnes(0.01f, 0.02f, 0.5f, 0.03f, 0.04f);

            Assert.AreEqual(0f, grain.EvaluateEnvelope(0f), 0.0000001f);
            Assert.AreEqual(0f, grain.Evaluate(0f), 0.0000001f);
        }

        [TestMethod]
        public void AdsrReachesFullAttackThenSustainLevel()
        {
            AdsrGrain grain = CreateEnvelopedOnes(0.01f, 0.02f, 0.5f, 0.03f, 0.04f);

            Assert.AreEqual(1f, grain.EvaluateEnvelope(0.00999f), 0.005f);
            Assert.AreEqual(0.5f, grain.EvaluateEnvelope(0.045f), 0.0001f);
            Assert.AreEqual(0.5f, grain.Evaluate(0.045f), 0.0001f);
        }

        [TestMethod]
        public void AdsrReturnsToSilenceAtTheEnd()
        {
            AdsrGrain grain = CreateEnvelopedOnes(0.01f, 0.02f, 0.5f, 0.03f, 0.04f);

            Assert.IsTrue(grain.EvaluateEnvelope(0.0999f) < 0.01f);
            Assert.AreEqual(0f, grain.EvaluateEnvelope(0.1f), 0.0000001f);
            Assert.AreEqual(0f, grain.Evaluate(0.1f), 0.0000001f);
        }

        [TestMethod]
        public void NoiseGrainIsDeterministicForIdenticalParameters()
        {
            NoiseGrain first = new NoiseGrain(1200f, 0.2f, 30f, 4242);
            NoiseGrain second = new NoiseGrain(1200f, 0.2f, 30f, 4242);

            for (int i = 0; i < 8000; i++)
            {
                float seconds = i / (float)SampleRate;
                Assert.AreEqual(first.Evaluate(seconds), second.Evaluate(seconds));
            }
        }

        [TestMethod]
        public void NoiseGrainWithDifferentSeedProducesDifferentSamples()
        {
            NoiseGrain first = new NoiseGrain(1200f, 0.2f, 30f, 1);
            NoiseGrain second = new NoiseGrain(1200f, 0.2f, 30f, 2);

            bool differs = false;
            for (int i = 0; i < 8000 && !differs; i++)
            {
                float seconds = i / (float)SampleRate;
                differs = first.Evaluate(seconds) != second.Evaluate(seconds);
            }

            Assert.IsTrue(differs);
        }

        [TestMethod]
        public void NoiseGrainIsNormalizedToTargetRms()
        {
            const float duration = 0.5f;
            NoiseGrain grain = new NoiseGrain(1000f, duration, 30f, 7);

            int samples = (int)(duration * SampleRate);
            double sum = 0.0;
            for (int i = 0; i < samples; i++)
            {
                float value = grain.Evaluate(i / (float)SampleRate);
                sum += value * (double)value;
            }

            double rms = Math.Sqrt(sum / samples);
            Assert.AreEqual(NoiseGrain.DefaultTargetRms, rms, 0.02);
        }

        [TestMethod]
        public void BufferGrainResamplesAndReportsSourceDuration()
        {
            float[] samples = new float[] { 0f, 1f, 0f, -1f };
            BufferGrain grain = new BufferGrain(samples, 4);

            Assert.AreEqual(1f, grain.DurationSeconds, 0.0001f);
            Assert.AreEqual(0f, grain.Evaluate(0f), 0.0001f);
            Assert.AreEqual(1f, grain.Evaluate(0.25f), 0.0001f);
            Assert.AreEqual(0.5f, grain.Evaluate(0.125f), 0.0001f);
            Assert.AreEqual(0f, grain.Evaluate(1f), 0.0001f);
        }

        [TestMethod]
        public void SemitoneMathMapsOctavesToDoubledRate()
        {
            Assert.AreEqual(1f, SemitoneMath.ToRate(0f), 0.000001f);
            Assert.AreEqual(2f, SemitoneMath.ToRate(12f), 0.000001f);
            Assert.AreEqual(0.5f, SemitoneMath.ToRate(-12f), 0.000001f);
        }

        internal static AdsrGrain CreateEnvelopedOnes(
            float attackSeconds,
            float decaySeconds,
            float sustainLevel,
            float sustainSeconds,
            float releaseSeconds)
        {
            float total = attackSeconds + decaySeconds + sustainSeconds + releaseSeconds;
            float[] ones = new float[(int)Math.Ceiling(total * SampleRate) + 1];
            for (int i = 0; i < ones.Length; i++)
            {
                ones[i] = 1f;
            }

            return new AdsrGrain(new BufferGrain(ones, SampleRate), attackSeconds, decaySeconds, sustainLevel, sustainSeconds, releaseSeconds);
        }

        internal static int CountZeroCrossings(float[] samples)
        {
            int crossings = 0;
            int previousSign = 0;
            for (int i = 0; i < samples.Length; i++)
            {
                int sign = samples[i] > 0f ? 1 : (samples[i] < 0f ? -1 : 0);
                if (sign == 0)
                {
                    continue;
                }

                if (previousSign != 0 && sign != previousSign)
                {
                    crossings++;
                }

                previousSign = sign;
            }

            return crossings;
        }
    }
}
