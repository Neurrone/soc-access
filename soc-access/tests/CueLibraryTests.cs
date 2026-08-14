using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Audio.Synth;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CueLibraryTests
    {
        private static readonly string[] InventoryKeys =
        {
            CueLibrary.TerrainRoad,
            CueLibrary.TerrainGround,
            CueLibrary.TerrainSand,
            CueLibrary.TerrainWater,
            CueLibrary.TerrainTrees,
            CueLibrary.TerrainImpassable,
            CueLibrary.TerrainUnexplored,
            CueLibrary.EntityFriendly,
            CueLibrary.EntityEnemy,
            CueLibrary.SweepWielder,
            CueLibrary.SweepSettlement,
            CueLibrary.SweepResource,
            CueLibrary.SweepPickup,
            CueLibrary.MoveDenied,
            CueLibrary.HexEmpty,
            CueLibrary.HexElevation1,
            CueLibrary.HexElevation2,
            CueLibrary.HexElevation3,
            CueLibrary.HexDanger,
            CueLibrary.HexActive
        };

        [TestMethod]
        public void EveryInventoryKeyHasAPlayableCue()
        {
            Assert.AreEqual(InventoryKeys.Length, CueLibrary.AllCues.Count);
            for (int i = 0; i < InventoryKeys.Length; i++)
            {
                string key = InventoryKeys[i];
                CueDefinition cue = CueLibrary.GetCue(key);
                Assert.IsNotNull(cue, key);
                Assert.IsFalse(string.IsNullOrEmpty(cue.Name.Key), key);
                Assert.IsFalse(string.IsNullOrEmpty(cue.Name.Text), key);
                Assert.IsTrue(cue.DefaultSpec.Segments.Count > 0, key);
                for (int segmentIndex = 0; segmentIndex < cue.DefaultSpec.Segments.Count; segmentIndex++)
                {
                    CueSegment segment = cue.DefaultSpec.Segments[segmentIndex];
                    Assert.IsTrue(segment.DurationMs > 0f, key);
                    Assert.IsTrue(segment.FrequencyHz > 0f, key);
                    Assert.IsTrue(segment.AttackMs >= 4f, key);
                }
            }
        }

        [TestMethod]
        public void CuesAreGroupedByCategoryInGlossaryOrder()
        {
            List<CueCategory> categories = new List<CueCategory>();
            for (int i = 0; i < CueLibrary.AllCues.Count; i++)
            {
                CueCategory category = CueLibrary.AllCues[i].Category;
                if (categories.Count == 0 || categories[categories.Count - 1] != category)
                {
                    Assert.IsFalse(categories.Contains(category), category.ToString());
                    categories.Add(category);
                }
            }

            CollectionAssert.AreEqual(
                new[] { CueCategory.Terrain, CueCategory.Overworld, CueCategory.Combat },
                categories);
        }

        [TestMethod]
        public void ElevationCuesAreTheEmptyHexTickAtBakedVarispeed()
        {
            CueSegment flat = CueLibrary.GetCue(CueLibrary.HexEmpty).DefaultSpec.Segments[0];
            string[] elevationKeys = { CueLibrary.HexElevation1, CueLibrary.HexElevation2, CueLibrary.HexElevation3 };
            float[] expectedSemitones = { 4f, 8f, 12f };

            int emptyIndex = IndexOf(CueLibrary.HexEmpty);
            for (int i = 0; i < elevationKeys.Length; i++)
            {
                CueDefinition cue = CueLibrary.GetCue(elevationKeys[i]);
                Assert.AreEqual(emptyIndex + 1 + i, IndexOf(elevationKeys[i]), elevationKeys[i]);
                Assert.AreEqual(CueCategory.Combat, cue.Category, elevationKeys[i]);
                Assert.AreEqual(1, cue.DefaultSpec.Segments.Count, elevationKeys[i]);

                CueSegment segment = cue.DefaultSpec.Segments[0];
                Assert.AreEqual(expectedSemitones[i], segment.RateSemitones, 0.001f, elevationKeys[i]);
                Assert.AreEqual(flat.Waveform, segment.Waveform, elevationKeys[i]);
                Assert.AreEqual(flat.FrequencyHz, segment.FrequencyHz, 0.001f, elevationKeys[i]);
                Assert.AreEqual(flat.DurationMs, segment.DurationMs, 0.001f, elevationKeys[i]);
                Assert.AreEqual(flat.AttackMs, segment.AttackMs, 0.001f, elevationKeys[i]);
                Assert.AreEqual(flat.ReleaseMs, segment.ReleaseMs, 0.001f, elevationKeys[i]);
            }
        }

        [TestMethod]
        public void SweepVoicesStayShortAndHighEnoughToPrecedeAnAffiliationMarker()
        {
            string[] sweepKeys = { CueLibrary.SweepWielder, CueLibrary.SweepSettlement, CueLibrary.SweepResource, CueLibrary.SweepPickup };
            for (int i = 0; i < sweepKeys.Length; i++)
            {
                CueDefinition cue = CueLibrary.GetCue(sweepKeys[i]);
                Assert.AreEqual(CueCategory.Overworld, cue.Category, sweepKeys[i]);

                float endMs = 0f;
                for (int segmentIndex = 0; segmentIndex < cue.DefaultSpec.Segments.Count; segmentIndex++)
                {
                    CueSegment segment = cue.DefaultSpec.Segments[segmentIndex];
                    Assert.IsTrue(segment.FrequencyHz >= 250f, sweepKeys[i]);
                    Assert.IsTrue(segment.AttackMs >= 4f, sweepKeys[i]);
                    float segmentEnd = segment.StartMs + segment.DurationMs;
                    if (segmentEnd > endMs)
                    {
                        endMs = segmentEnd;
                    }
                }

                Assert.IsTrue(endMs >= 40f && endMs <= 80f, sweepKeys[i] + " lasts " + endMs + " ms");
            }
        }

        [TestMethod]
        public void UnknownKeysHaveNoCueOrSpec()
        {
            Assert.IsNull(CueLibrary.GetCue("terrain_lava"));
            Assert.IsNull(CueLibrary.GetEffectiveSpec("terrain_lava", 100));
            Assert.IsNull(CueLibrary.GetCue(null));
        }

        [TestMethod]
        public void DurationScaleStretchesStartAndDurationOfEverySegment()
        {
            CueSpec defaultSpec = CueLibrary.GetCue(CueLibrary.TerrainUnexplored).DefaultSpec;

            CueSpec spec = CueLibrary.GetEffectiveSpec(CueLibrary.TerrainUnexplored, 50);

            Assert.AreEqual(defaultSpec.Segments.Count, spec.Segments.Count);
            for (int i = 0; i < spec.Segments.Count; i++)
            {
                Assert.AreEqual(defaultSpec.Segments[i].StartMs * 0.5f, spec.Segments[i].StartMs, 0.001f);
                Assert.AreEqual(defaultSpec.Segments[i].DurationMs * 0.5f, spec.Segments[i].DurationMs, 0.001f);
                Assert.AreEqual(defaultSpec.Segments[i].FrequencyHz, spec.Segments[i].FrequencyHz, 0.001f);
            }
        }

        [TestMethod]
        public void EffectiveSpecDoesNotMutateTheDefaultSpec()
        {
            float originalDuration = CueLibrary.GetCue(CueLibrary.TerrainRoad).DefaultSpec.Segments[0].DurationMs;

            CueLibrary.GetEffectiveSpec(CueLibrary.TerrainRoad, 200);

            Assert.AreEqual(
                originalDuration,
                CueLibrary.GetCue(CueLibrary.TerrainRoad).DefaultSpec.Segments[0].DurationMs,
                0.001f);
        }

        [TestMethod]
        public void EveryCueRendersAudibleSamples()
        {
            for (int i = 0; i < CueLibrary.AllCues.Count; i++)
            {
                CueDefinition cue = CueLibrary.AllCues[i];
                float[] buffer = CueRenderer.Render(cue.DefaultSpec);
                float peak = 0f;
                for (int sample = 0; sample < buffer.Length; sample++)
                {
                    float magnitude = buffer[sample] < 0f ? -buffer[sample] : buffer[sample];
                    if (magnitude > peak)
                    {
                        peak = magnitude;
                    }
                }

                Assert.IsTrue(peak > 0.01f, cue.Key);
            }
        }

        private static int IndexOf(string key)
        {
            for (int i = 0; i < CueLibrary.AllCues.Count; i++)
            {
                if (CueLibrary.AllCues[i].Key == key)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
