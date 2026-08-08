using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class NoisePoolTests
    {
        private const int SampleRate = 44100;

        [TestMethod]
        public void TakeReturnsRequestedLengthAtPoolRate()
        {
            BufferGrain grain = CreatePool().Take(0.1);

            Assert.AreEqual((int)Math.Ceiling(0.1 * SampleRate), grain.Count);
            Assert.AreEqual(SampleRate, grain.SourceSampleRate);
        }

        [TestMethod]
        public void SuccessiveTakesAdvanceTheCursor()
        {
            NoisePool pool = CreatePool();

            BufferGrain first = pool.Take(0.05);
            BufferGrain second = pool.Take(0.05);

            Assert.AreSame(first.Data, second.Data);
            Assert.AreEqual(first.Offset + first.Count, second.Offset);
        }

        [TestMethod]
        public void RefillsWhenExhausted()
        {
            NoisePool pool = CreatePool();

            BufferGrain first = pool.Take(0.1);
            pool.Take(0.1);
            BufferGrain afterRefill = pool.Take(0.1);

            Assert.AreEqual(0, afterRefill.Offset);
            Assert.AreNotSame(first.Data, afterRefill.Data);
        }

        [TestMethod]
        public void IsNormalizedTowardTargetRms()
        {
            BufferGrain grain = CreatePool().Take(0.1);

            double sum = 0.0;
            for (int i = 0; i < grain.Count; i++)
            {
                float sample = grain.Data[grain.Offset + i];
                sum += (double)sample * sample;
            }

            double rms = Math.Sqrt(sum / grain.Count);
            Assert.IsTrue(rms > 0.2 && rms < 0.4, "slice RMS was " + rms);
        }

        [TestMethod]
        public void SuccessiveSlicesAreDecorrelated()
        {
            NoisePool pool = CreatePool();

            BufferGrain first = pool.Take(0.05);
            BufferGrain second = pool.Take(0.05);

            double correlation = 0.0;
            double firstEnergy = 0.0;
            double secondEnergy = 0.0;
            for (int i = 0; i < first.Count; i++)
            {
                float a = first.Data[first.Offset + i];
                float b = second.Data[second.Offset + i];
                correlation += a * (double)b;
                firstEnergy += a * (double)a;
                secondEnergy += b * (double)b;
            }

            double normalized = Math.Abs(correlation) / Math.Sqrt(firstEnergy * secondEnergy);
            Assert.IsTrue(normalized < 0.2, "slices correlated at " + normalized);
        }

        private static NoisePool CreatePool()
        {
            return new NoisePool(1000.0, 15.0, SampleRate, 10000, 128, 0.3, 1);
        }
    }
}
