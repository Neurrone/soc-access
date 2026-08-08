using System;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CueRendererTests
    {
        private const int SampleRate = 44100;

        [TestMethod]
        public void RendersInterleavedStereoCoveringTheWholeCue()
        {
            CueSpec spec = CreateToneSpec();
            spec.Segments[0].StartMs = 50f;
            spec.Segments[0].DurationMs = 100f;

            float[] output = CueRenderer.Render(spec);

            Assert.AreEqual(0, output.Length % 2);
            Assert.AreEqual((int)(0.15f * SampleRate), output.Length / 2, 2);
        }

        [TestMethod]
        public void PanOffsetShiftsTheRenderedChannels()
        {
            CueSpec spec = CreateToneSpec();

            float[] centered = CueRenderer.Render(spec);
            float[] left = CueRenderer.Render(spec, -1f, 1f, 0f);

            Assert.AreEqual(Peak(centered, 0), Peak(centered, 1), 0.001f);
            Assert.AreEqual(0f, Peak(left, 1), 0.001f);
            Assert.IsTrue(Peak(left, 0) > Peak(centered, 0));
        }

        [TestMethod]
        public void PerSegmentPanCombinesWithThePanOffsetAndClamps()
        {
            CueSpec spec = CreateToneSpec();
            spec.Segments[0].Pan = -0.5f;

            float[] output = CueRenderer.Render(spec, -1f, 1f, 0f);

            Assert.AreEqual(0f, Peak(output, 1), 0.001f);
        }

        [TestMethod]
        public void GainScaleScalesTheOutputLinearly()
        {
            CueSpec spec = CreateToneSpec();

            float full = Peak(CueRenderer.Render(spec), 0);
            float half = Peak(CueRenderer.Render(spec, 0f, 0.5f, 0f), 0);

            Assert.AreEqual(0.5f, half / full, 0.01f);
        }

        [TestMethod]
        public void MasterGainScalesTheOutputLinearly()
        {
            CueSpec spec = CreateToneSpec();
            float full = Peak(CueRenderer.Render(spec), 0);

            spec.MasterGain = 0.25f;
            float quiet = Peak(CueRenderer.Render(spec), 0);

            Assert.AreEqual(0.25f, quiet / full, 0.01f);
        }

        [TestMethod]
        public void RateSemitoneOffsetShortensAndRaisesTheCue()
        {
            CueSpec spec = CreateToneSpec();

            float[] normal = CueRenderer.Render(spec);
            float[] octaveUp = CueRenderer.Render(spec, 0f, 1f, 12f);

            Assert.AreEqual(normal.Length / 4, octaveUp.Length / 2, 2);
        }

        [TestMethod]
        public void SegmentRateSemitonesShortenTheCue()
        {
            CueSpec spec = CreateToneSpec();
            float[] normal = CueRenderer.Render(spec);

            spec.Segments[0].RateSemitones = 12f;
            float[] octaveUp = CueRenderer.Render(spec);

            Assert.AreEqual(normal.Length / 4, octaveUp.Length / 2, 2);
        }

        [TestMethod]
        public void RenderIsDeterministicForNoiseSegments()
        {
            CueSpec spec = new CueSpec("test.noise", 1f);
            CueSegment segment = new CueSegment();
            segment.Waveform = CueWaveform.Noise;
            segment.FrequencyHz = 2200f;
            segment.DurationMs = 120f;
            segment.NoiseQ = 30f;
            segment.Gain = 0.7f;
            spec.Segments.Add(segment);

            float[] first = CueRenderer.Render(spec);
            float[] second = CueRenderer.Render(spec);

            CollectionAssert.AreEqual(first, second);
        }

        [TestMethod]
        public void RenderIsDeterministicAcrossEquivalentSpecs()
        {
            CueSpec spec = CreateToneSpec();
            spec.Segments[0].Waveform = CueWaveform.Noise;

            float[] first = CueRenderer.Render(spec);
            float[] second = CueRenderer.Render(spec.Clone());

            CollectionAssert.AreEqual(first, second);
        }

        [TestMethod]
        public void RenderedCueStartsAndEndsAtSilence()
        {
            CueSpec spec = CreateToneSpec();
            spec.Segments[0].AttackMs = 5f;
            spec.Segments[0].ReleaseMs = 20f;

            float[] output = CueRenderer.Render(spec);

            Assert.AreEqual(0f, output[0], 0.0001f);
            Assert.AreEqual(0f, output[1], 0.0001f);
            Assert.AreEqual(0f, output[output.Length - 2], 0.001f);
            Assert.AreEqual(0f, output[output.Length - 1], 0.001f);
        }

        [TestMethod]
        public void CueSegmentCloneIsIndependent()
        {
            CueSegment segment = new CueSegment();
            segment.Waveform = CueWaveform.Triangle;
            segment.FrequencyHz = 660f;
            segment.StartMs = 10f;
            segment.DurationMs = 90f;
            segment.Gain = 0.4f;
            segment.Pan = -0.25f;
            segment.AttackMs = 6f;
            segment.ReleaseMs = 12f;
            segment.NoiseQ = 12f;
            segment.RateSemitones = 3f;

            CueSegment clone = segment.Clone();
            clone.Waveform = CueWaveform.Noise;
            clone.FrequencyHz = 100f;
            clone.RateSemitones = -5f;

            Assert.AreEqual(CueWaveform.Triangle, segment.Waveform);
            Assert.AreEqual(660f, segment.FrequencyHz);
            Assert.AreEqual(3f, segment.RateSemitones);
            Assert.AreEqual(10f, clone.StartMs);
            Assert.AreEqual(90f, clone.DurationMs);
            Assert.AreEqual(0.4f, clone.Gain);
            Assert.AreEqual(-0.25f, clone.Pan);
            Assert.AreEqual(6f, clone.AttackMs);
            Assert.AreEqual(12f, clone.ReleaseMs);
            Assert.AreEqual(12f, clone.NoiseQ);
        }

        [TestMethod]
        public void CueSpecCloneIsADeepCopy()
        {
            CueSpec spec = CreateToneSpec();
            spec.Segments.Add(new CueSegment());

            CueSpec clone = spec.Clone();
            clone.Name = "other";
            clone.MasterGain = 0.1f;
            clone.Segments[0].FrequencyHz = 123f;
            clone.Segments.RemoveAt(1);

            Assert.AreEqual("test.tone", spec.Name);
            Assert.AreEqual(1f, spec.MasterGain);
            Assert.AreEqual(2, spec.Segments.Count);
            Assert.AreEqual(440f, spec.Segments[0].FrequencyHz);
            Assert.AreEqual(1, clone.Segments.Count);
        }

        [TestMethod]
        public void NullSpecRendersEmptyBuffer()
        {
            Assert.AreEqual(0, CueRenderer.Render(null).Length);
            Assert.AreEqual(0, CueRenderer.Render(new CueSpec("empty", 1f)).Length);
        }

        private static CueSpec CreateToneSpec()
        {
            CueSpec spec = new CueSpec("test.tone", 1f);
            CueSegment segment = new CueSegment();
            segment.Waveform = CueWaveform.Sine;
            segment.FrequencyHz = 440f;
            segment.StartMs = 0f;
            segment.DurationMs = 100f;
            segment.Gain = 0.5f;
            segment.AttackMs = 5f;
            segment.ReleaseMs = 20f;
            spec.Segments.Add(segment);
            return spec;
        }

        private static float Peak(float[] interleaved, int channel)
        {
            float peak = 0f;
            for (int i = channel; i < interleaved.Length; i += 2)
            {
                float magnitude = Math.Abs(interleaved[i]);
                if (magnitude > peak)
                {
                    peak = magnitude;
                }
            }

            return peak;
        }
    }
}
