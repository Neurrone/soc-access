using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class MapDestinationSetEventTests
    {
        [TestMethod]
        public void GetSpeechTextReadsTheCostThenEveryStepOfTheRoute()
        {
            string text = Describe(
                new[] { ScannerDirection.North, ScannerDirection.North, ScannerDirection.Northeast },
                new[] { Turn(1, 5f) });

            Assert.AreEqual("Cost: 5 this turn. Aurelia will move n, n, ne.", text);
        }

        [TestMethod]
        public void GetSpeechTextSplitsTheCostOverTheTurnsItIsSpentOn()
        {
            string text = Describe(
                new[] { ScannerDirection.East, ScannerDirection.East },
                new[] { Turn(1, 5f), Turn(2, 12f), Turn(3, 3f) });

            Assert.AreEqual("Cost: 5 this turn, 12 next turn, 3 in 2 turns. Aurelia will move e, e.", text);
        }

        [TestMethod]
        public void GetSpeechTextKeepsAFractionalCostIntact()
        {
            string text = Describe(
                new[] { ScannerDirection.Southwest },
                new[] { Turn(1, 2.5f) });

            Assert.AreEqual("Cost: 2.5 this turn. Aurelia will move sw.", text);
        }

        [TestMethod]
        public void GetSpeechTextSpellsTheStepsOutWhenLongDirectionsAreOn()
        {
            string text = Describe(
                new[] { ScannerDirection.North, ScannerDirection.Southeast },
                new[] { Turn(1, 4f) },
                useLongDirections: true);

            Assert.AreEqual("Cost: 4 this turn. Aurelia will move north, southeast.", text);
        }

        [TestMethod]
        public void GetSpeechTextFallsBackToTheDestinationTileWhenThereIsNoRoute()
        {
            string text = new MapDestinationSetEvent(7, "Aurelia", new Vector2Int(34, 12), null)
                .GetSpeechText(useLongDirections: false);

            Assert.AreEqual("Aurelia's destination set to 34, 12", text);
        }

        [TestMethod]
        public void GetSpeechTextFallsBackWhenTheRouteHasNoSteps()
        {
            string text = Describe(new ScannerDirection[0], new[] { Turn(1, 5f) });

            Assert.AreEqual("Aurelia's destination set to 34, 12", text);
        }

        private static string Describe(
            IReadOnlyList<ScannerDirection> steps,
            IReadOnlyList<WielderRouteTurn> turns)
        {
            return Describe(steps, turns, useLongDirections: false);
        }

        private static string Describe(
            IReadOnlyList<ScannerDirection> steps,
            IReadOnlyList<WielderRouteTurn> turns,
            bool useLongDirections)
        {
            return new MapDestinationSetEvent(
                7,
                "Aurelia",
                new Vector2Int(34, 12),
                new WielderRoute(steps, turns)).GetSpeechText(useLongDirections);
        }

        private static WielderRouteTurn Turn(int travelTurns, float cost)
        {
            return new WielderRouteTurn(travelTurns, cost);
        }
    }
}
