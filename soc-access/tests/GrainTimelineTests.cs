using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class GrainTimelineTests
    {
        private const int SampleRate = 44100;

        [TestMethod]
        public void CenterPanIsEqualPowerOnBothChannels()
        {
            float left;
            float right;
            GrainTimeline.PanGains(0f, out left, out right);

            Assert.AreEqual(0.7071f, left, 0.001f);
            Assert.AreEqual(0.7071f, right, 0.001f);
        }

        [TestMethod]
        public void FullLeftAndFullRightPanIsolateOneChannel()
        {
            float left;
            float right;
            GrainTimeline.PanGains(-1f, out left, out right);
            Assert.AreEqual(1f, left, 0.001f);
            Assert.AreEqual(0f, right, 0.001f);

            GrainTimeline.PanGains(1f, out left, out right);
            Assert.AreEqual(0f, left, 0.001f);
            Assert.AreEqual(1f, right, 0.001f);
        }

        [TestMethod]
        public void PanLawKeepsConstantPowerAtEveryPosition()
        {
            for (int step = -10; step <= 10; step++)
            {
                float pan = step / 10f;
                float left;
                float right;
                GrainTimeline.PanGains(pan, out left, out right);
                Assert.AreEqual(1f, left * left + right * right, 0.0001f, "Pan " + pan + " was not constant power");
            }
        }

        [TestMethod]
        public void RenderedPanMatchesThePanLaw()
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateOnes(0.05f), 0f, 1f, -1f, 0.5f);
            float[] output = timeline.Render();

            Assert.AreEqual(0.5f, output[100], 0.001f);
            Assert.AreEqual(0f, output[101], 0.001f);
        }

        [TestMethod]
        public void OnsetIsPlacedAtTheRequestedFrame()
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateOnes(0.05f), 0.1f, 1f, 0f, 1f);
            float[] output = timeline.Render();

            int firstNonZero = -1;
            for (int i = 0; i < output.Length; i++)
            {
                if (output[i] != 0f)
                {
                    firstNonZero = i;
                    break;
                }
            }

            Assert.AreEqual(4410, firstNonZero / 2);
            Assert.AreEqual(0, firstNonZero % 2);
        }

        [TestMethod]
        public void VarispeedHalvesDurationAndDoublesFrequency()
        {
            float[] normal = RenderSine(1f);
            float[] fast = RenderSine(2f);

            int normalFrames = normal.Length / 2;
            int fastFrames = fast.Length / 2;
            Assert.AreEqual(normalFrames / 2, fastFrames, 2);

            double normalRate = CrossingRate(normal, normalFrames);
            double fastRate = CrossingRate(fast, fastFrames);
            Assert.AreEqual(2.0, fastRate / normalRate, 0.05);
        }

        [TestMethod]
        public void OutputStaysInsideUnitRangeWhenManyLoudGrainsOverlap()
        {
            GrainTimeline timeline = new GrainTimeline();
            for (int i = 0; i < 12; i++)
            {
                timeline.Add(new SineGrain(440f + i, 0.05f), 0f, 1f, 0f, 1f);
            }

            float[] output = timeline.Render();

            Assert.IsTrue(output.Length > 0);
            float peak = 0f;
            for (int i = 0; i < output.Length; i++)
            {
                Assert.IsTrue(output[i] > -1f && output[i] < 1f, "Sample " + i + " was " + output[i]);
                float magnitude = Math.Abs(output[i]);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }

            Assert.IsTrue(peak > GrainTimeline.LimiterThreshold, "Expected the limiter to be exercised");
        }

        [TestMethod]
        public void LimiterIsTransparentBelowThreshold()
        {
            Assert.AreEqual(0.5f, GrainTimeline.Limit(0.5f), 0.0000001f);
            Assert.AreEqual(-0.5f, GrainTimeline.Limit(-0.5f), 0.0000001f);
            Assert.IsTrue(GrainTimeline.Limit(50f) < 1f);
            Assert.IsTrue(GrainTimeline.Limit(-50f) > -1f);
        }

        [TestMethod]
        public void EmptyTimelineRendersNothing()
        {
            GrainTimeline timeline = new GrainTimeline();

            Assert.AreEqual(0, timeline.Render().Length);
        }

        [TestMethod]
        public void RenderLengthCoversTheLatestEndingPlacement()
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateOnes(0.05f), 0f, 1f, 0f, 0.2f);
            timeline.Add(CreateOnes(0.05f), 0.2f, 1f, 0f, 0.2f);

            Assert.AreEqual((int)((0.2f + 0.05f) * SampleRate), timeline.Render().Length / 2, 2);
        }

        [TestMethod]
        public void InterauralDelayMatchesPanWithSubSamplePrecision()
        {
            // Pan right means the right ear is near (no delay) and the left ear is far. At pan 0.5
            // the delay is 0.5 * 0.0007 * 44100 = 15.4 samples, a fractional value that integer
            // rounding could not reproduce. Gain is kept low so the limiter stays out of the way.
            const float pan = 0.5f;
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateTone(), 0f, 1f, pan, 0.5f);
            float[] output = timeline.Render();

            float[] near = Channel(output, 1);
            float[] far = Channel(output, 0);

            double expected = pan * GrainTimeline.MaxInterauralDelaySeconds * SampleRate;
            Assert.AreEqual(expected, SignalLag.Estimate(near, far, 40), 0.05);
        }

        [TestMethod]
        public void CenteredGrainsGetNoInterauralDelayOrPadding()
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateTone(), 0f, 1f, 0f, 0.5f);
            float[] output = timeline.Render();

            Assert.AreEqual(timeline.FrameCount, output.Length / 2);
            for (int i = 0; i < output.Length; i += 2)
            {
                Assert.AreEqual(output[i], output[i + 1], 0.0000001f);
            }
        }

        [TestMethod]
        public void PannedGrainsGetTailPaddingForTheDelayedFarEar()
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateTone(), 0f, 1f, 1f, 0.5f);
            float[] output = timeline.Render();

            Assert.AreEqual(timeline.FrameCount + GrainTimeline.ItdPaddingFrames(SampleRate), output.Length / 2);
        }

        [TestMethod]
        public void HardLeftLeavesTheRightChannelSilent()
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(CreateTone(), 0f, 1f, -1f, 0.5f);
            float[] output = timeline.Render();

            float leftPeak = 0f;
            float rightPeak = 0f;
            for (int i = 0; i < output.Length; i += 2)
            {
                leftPeak = Math.Max(leftPeak, Math.Abs(output[i]));
                rightPeak = Math.Max(rightPeak, Math.Abs(output[i + 1]));
            }

            Assert.IsTrue(leftPeak > 0.1f, "left channel should carry the signal");
            Assert.AreEqual(0f, rightPeak, 0.00001f);
        }

        private static AdsrGrain CreateTone()
        {
            return new AdsrGrain(new SineGrain(440f, 0.1f), 0.01f, 0.02f, 1f, 0.04f, 0.03f);
        }

        private static float[] Channel(float[] interleaved, int channel)
        {
            float[] mono = new float[interleaved.Length / 2];
            for (int i = 0; i < mono.Length; i++)
            {
                mono[i] = interleaved[i * 2 + channel];
            }

            return mono;
        }

        private static float[] RenderSine(float rate)
        {
            GrainTimeline timeline = new GrainTimeline();
            timeline.Add(new SineGrain(500f, 0.2f), 0f, rate, 0f, 1f);
            return timeline.Render();
        }

        private static double CrossingRate(float[] interleaved, int frames)
        {
            float[] left = new float[frames];
            for (int i = 0; i < frames; i++)
            {
                left[i] = interleaved[i * 2];
            }

            return GrainTests.CountZeroCrossings(left) / (frames / (double)SampleRate);
        }

        private static BufferGrain CreateOnes(float durationSeconds)
        {
            float[] ones = new float[(int)Math.Ceiling(durationSeconds * SampleRate)];
            for (int i = 0; i < ones.Length; i++)
            {
                ones[i] = 1f;
            }

            return new BufferGrain(ones, SampleRate);
        }
    }
}
