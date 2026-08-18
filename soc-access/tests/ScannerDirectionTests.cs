using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerDirectionTests
    {
        [TestMethod]
        public void SquareDirectionsPutTheVerticalLegFirst()
        {
            IReadOnlyList<ScannerDirectionStep> steps =
                ScannerDirectionUtility.BuildSquareDirections(new Vector2Int(2, 2), new Vector2Int(5, 4));

            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(ScannerDirection.North, steps[0].Direction);
            Assert.AreEqual(2, steps[0].Count);
            Assert.AreEqual(ScannerDirection.East, steps[1].Direction);
            Assert.AreEqual(3, steps[1].Count);
        }

        [TestMethod]
        public void SquareDirectionsUseSouthAndWestForNegativeDeltas()
        {
            IReadOnlyList<ScannerDirectionStep> steps =
                ScannerDirectionUtility.BuildSquareDirections(new Vector2Int(5, 4), new Vector2Int(2, 2));

            Assert.AreEqual(2, steps.Count);
            Assert.AreEqual(ScannerDirection.South, steps[0].Direction);
            Assert.AreEqual(2, steps[0].Count);
            Assert.AreEqual(ScannerDirection.West, steps[1].Direction);
            Assert.AreEqual(3, steps[1].Count);
        }

        [TestMethod]
        public void SquareDirectionsAreEmptyOnTheOrigin()
        {
            IReadOnlyList<ScannerDirectionStep> steps =
                ScannerDirectionUtility.BuildSquareDirections(new Vector2Int(3, 3), new Vector2Int(3, 3));

            Assert.AreEqual(0, steps.Count);
        }

        [TestMethod]
        public void ShortFormJoinsCountToDirectionWithoutASpace()
        {
            Assert.AreEqual("3ne", ScannerDirectionUtility.FormatStep(
                new ScannerDirectionStep(3, ScannerDirection.Northeast), useLongForm: false));
        }

        [TestMethod]
        public void LongFormSpellsTheDirectionAndSeparatesIt()
        {
            Assert.AreEqual("3 northeast", ScannerDirectionUtility.FormatStep(
                new ScannerDirectionStep(3, ScannerDirection.Northeast), useLongForm: true));
        }

        [TestMethod]
        public void EveryDirectionHasBothForms()
        {
            string[] expectedShort = { "n", "s", "e", "w", "ne", "nw", "se", "sw" };
            string[] expectedLong =
            {
                "north", "south", "east", "west", "northeast", "northwest", "southeast", "southwest"
            };
            ScannerDirection[] directions =
            {
                ScannerDirection.North,
                ScannerDirection.South,
                ScannerDirection.East,
                ScannerDirection.West,
                ScannerDirection.Northeast,
                ScannerDirection.Northwest,
                ScannerDirection.Southeast,
                ScannerDirection.Southwest
            };

            for (int i = 0; i < directions.Length; i++)
            {
                ScannerDirectionStep step = new ScannerDirectionStep(1, directions[i]);
                Assert.AreEqual("1" + expectedShort[i], ScannerDirectionUtility.FormatStep(step, useLongForm: false));
                Assert.AreEqual("1 " + expectedLong[i], ScannerDirectionUtility.FormatStep(step, useLongForm: true));
            }
        }

        [TestMethod]
        public void FormatDirectionsJoinsStepsWithCommas()
        {
            string text = ScannerSpeechUtility.FormatDirections(
                new List<ScannerDirectionStep>
                {
                    new ScannerDirectionStep(2, ScannerDirection.North),
                    new ScannerDirectionStep(3, ScannerDirection.East)
                },
                useLongForm: false);

            Assert.AreEqual("2n, 3e", text);
        }

        [TestMethod]
        public void FormatDirectionsSaysHereWhenThereAreNoSteps()
        {
            Assert.AreEqual("here", ScannerSpeechUtility.FormatDirections(
                new List<ScannerDirectionStep>(), useLongForm: false));
        }

        [TestMethod]
        public void FormatStepIgnoresEmptyRuns()
        {
            Assert.AreEqual(string.Empty, ScannerDirectionUtility.FormatStep(
                new ScannerDirectionStep(0, ScannerDirection.North), useLongForm: false));
            Assert.AreEqual(string.Empty, ScannerDirectionUtility.FormatStep(null, useLongForm: false));
        }
    }
}
