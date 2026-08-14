using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class SweepPlayerTests
    {
        private const float Tolerance = 1e-4f;

        [TestMethod]
        public void EmptyInputSchedulesNothing()
        {
            Assert.AreEqual(0, Build(null).Count);
            Assert.AreEqual(0, Build(new List<SweepEntry>()).Count);
        }

        [TestMethod]
        public void EntriesWithoutCuesAreDropped()
        {
            List<SweepEntry> entries = new List<SweepEntry>
            {
                new SweepEntry(new Vector2Int(1, 0), null),
                new SweepEntry(new Vector2Int(2, 0), new List<TileCue>())
            };

            Assert.AreEqual(0, Build(entries).Count);
        }

        [TestMethod]
        public void PingsAreOrderedWestToEastThenSouthToNorth()
        {
            List<SweepEntry> entries = new List<SweepEntry>
            {
                Entry(3, 1, "east"),
                Entry(-2, 4, "west"),
                Entry(3, -1, "east-lower"),
                Entry(0, 0, "centre")
            };

            List<SweepStep> steps = Build(entries);

            CollectionAssert.AreEqual(
                new[] { "west", "centre", "east-lower", "east" },
                Keys(steps));
        }

        [TestMethod]
        public void EachEntryCarriesItsOwnDirectionalOffsets()
        {
            List<SweepStep> steps = Build(new List<SweepEntry> { Entry(-6, 0, "west"), Entry(0, 5, "north") });

            Assert.AreEqual(-0.5f, steps[0].Pan, Tolerance);
            Assert.AreEqual(0f, steps[0].Semitones, Tolerance);
            Assert.AreEqual(1f - 6f / 30f, steps[0].GainScale, Tolerance);

            Assert.AreEqual(0f, steps[1].Pan, Tolerance);
            Assert.AreEqual(5f, steps[1].Semitones, Tolerance);
            Assert.AreEqual(1f - 5f / 30f, steps[1].GainScale, Tolerance);
        }

        [TestMethod]
        public void OutOfRangeEntriesAreDroppedBeforeTheGapIsSized()
        {
            List<SweepEntry> entries = new List<SweepEntry>
            {
                Entry(-40, 0, "too far west"),
                Entry(1, 0, "near"),
                Entry(2, 0, "also near"),
                Entry(40, 0, "too far east")
            };

            List<SweepStep> steps = Build(entries);

            CollectionAssert.AreEqual(new[] { "near", "also near" }, Keys(steps));

            // Two survivors, so the gap is the clamped maximum rather than 0.75 / 4.
            Assert.AreEqual(SweepPlayer.MaximumGapSeconds, steps[1].TimeSeconds, Tolerance);
        }

        [TestMethod]
        public void FirstPingFiresImmediatelyAndTheRestAreEvenlySpaced()
        {
            List<SweepEntry> entries = new List<SweepEntry>();
            for (int i = 0; i < 5; i++)
            {
                entries.Add(Entry(i, 0, "cue" + i));
            }

            List<SweepStep> steps = Build(entries);
            float gap = SweepPlayer.TargetWindowSeconds / 5f;

            Assert.AreEqual(5, steps.Count);
            for (int i = 0; i < steps.Count; i++)
            {
                Assert.AreEqual(i * gap, steps[i].TimeSeconds, Tolerance);
            }
        }

        [TestMethod]
        public void FewPingsClampTheGapToTheMaximum()
        {
            List<SweepStep> steps = Build(new List<SweepEntry> { Entry(0, 0, "a"), Entry(1, 0, "b") });

            // 0.75 / 2 would be 0.375 s, which drags the sweep out.
            Assert.AreEqual(SweepPlayer.MaximumGapSeconds, steps[1].TimeSeconds, Tolerance);
        }

        [TestMethod]
        public void ManyPingsClampTheGapToTheMinimum()
        {
            List<SweepEntry> entries = new List<SweepEntry>();
            for (int i = 0; i < 20; i++)
            {
                entries.Add(Entry(i, 0, "cue" + i));
            }

            List<SweepStep> steps = Build(entries);

            // 0.75 / 20 would be 0.0375 s, which fuses the pings into a burst.
            Assert.AreEqual(20, steps.Count);
            Assert.AreEqual(SweepPlayer.MinimumGapSeconds, steps[1].TimeSeconds, Tolerance);
            Assert.AreEqual(19f * SweepPlayer.MinimumGapSeconds, steps[19].TimeSeconds, Tolerance);
        }

        [TestMethod]
        public void DensePingsAreSpacedByTheGestureTheyPlayNotJustTheRhythm()
        {
            List<SweepStep> steps = Build(Gestures(12), GestureDuration);
            float gesture = CueLibrary.ComputeStackDurationSeconds(steps[0].Cues, GestureDuration);

            // The rhythm alone would put these 0.1 s apart and run every marker into the next ping.
            Assert.AreEqual(12, steps.Count);
            Assert.IsTrue(gesture > SweepPlayer.MinimumGapSeconds);
            for (int i = 1; i < steps.Count; i++)
            {
                float previousEnd = steps[i - 1].TimeSeconds + gesture;
                Assert.IsTrue(
                    steps[i].TimeSeconds >= previousEnd + SweepPlayer.MinimumSeparationSeconds - Tolerance,
                    "ping " + i + " starts at " + steps[i].TimeSeconds + " but " + (i - 1) + " ends at " + previousEnd);
            }
        }

        [TestMethod]
        public void SparsePingsKeepTheBaseRhythm()
        {
            List<SweepStep> steps = Build(Gestures(3), GestureDuration);

            // 0.75 / 3 clamps to the 0.2 s maximum, which already outlasts the gesture plus its
            // separation, so no ping is pushed later than the rhythm asks.
            for (int i = 0; i < steps.Count; i++)
            {
                Assert.AreEqual(i * SweepPlayer.MaximumGapSeconds, steps[i].TimeSeconds, Tolerance);
            }
        }

        [TestMethod]
        public void ASilencedAffiliationShortensTheGestureAndTightensTheSchedule()
        {
            Func<string, float> withoutMarker = key => key == CueLibrary.EntityEnemy ? 0f : GestureDuration(key);
            List<SweepStep> marked = Build(Gestures(12), GestureDuration);
            List<SweepStep> unmarked = Build(Gestures(12), withoutMarker);

            float shortened = CueLibrary.ComputeStackDurationSeconds(unmarked[0].Cues, withoutMarker)
                + SweepPlayer.MinimumSeparationSeconds;

            Assert.IsTrue(unmarked[11].TimeSeconds < marked[11].TimeSeconds);
            Assert.AreEqual(11f * shortened, unmarked[11].TimeSeconds, Tolerance);
        }

        [TestMethod]
        public void CueStacksArePassedThroughUntouched()
        {
            List<TileCue> cues = new List<TileCue>
            {
                new TileCue(CueLibrary.SweepWielder, 0f),
                new TileCue(CueLibrary.EntityEnemy, 0f, followsPrevious: true)
            };

            List<SweepStep> steps = Build(new List<SweepEntry> { new SweepEntry(new Vector2Int(2, 2), cues) });

            Assert.AreEqual(1, steps.Count);
            Assert.AreSame(cues, steps[0].Cues);
        }

        /// <summary>Instant cues, so the base rhythm is what the schedule shows.</summary>
        private static List<SweepStep> Build(IReadOnlyList<SweepEntry> entries)
        {
            return SweepPlayer.BuildSchedule(entries, Vector2Int.zero, CueGridGeometry.Square, key => 0f);
        }

        private static List<SweepStep> Build(IReadOnlyList<SweepEntry> entries, Func<string, float> durationSeconds)
        {
            return SweepPlayer.BuildSchedule(entries, Vector2Int.zero, CueGridGeometry.Square, durationSeconds);
        }

        private static SweepEntry Entry(int x, int y, string cueKey)
        {
            return new SweepEntry(new Vector2Int(x, y), new List<TileCue> { new TileCue(cueKey, 0f) });
        }

        /// <summary>Two-part pings: a category voice with an affiliation marker behind it.</summary>
        private static List<SweepEntry> Gestures(int count)
        {
            List<SweepEntry> entries = new List<SweepEntry>(count);
            for (int i = 0; i < count; i++)
            {
                entries.Add(new SweepEntry(
                    new Vector2Int(i, 0),
                    new List<TileCue>
                    {
                        new TileCue(CueLibrary.SweepWielder, 0f),
                        new TileCue(CueLibrary.EntityEnemy, 0f, followsPrevious: true)
                    }));
            }

            return entries;
        }

        /// <summary>0.05 s + the 0.04 s stack gap + 0.05 s = a 0.14 s gesture.</summary>
        private static float GestureDuration(string key)
        {
            return 0.05f;
        }

        private static string[] Keys(List<SweepStep> steps)
        {
            string[] keys = new string[steps.Count];
            for (int i = 0; i < steps.Count; i++)
            {
                keys[i] = steps[i].Cues[0].Key;
            }

            return keys;
        }
    }
}
