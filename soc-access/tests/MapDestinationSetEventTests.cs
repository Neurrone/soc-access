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
                new[] { Step(2, ScannerDirection.North), Step(1, ScannerDirection.Northeast) },
                new[] { Turn(1, 5f) });

            Assert.AreEqual("Cost: 5 this turn. Aurelia will move 2n, ne.", text);
        }

        [TestMethod]
        public void GetSpeechTextSplitsTheCostOverTheTurnsItIsSpentOn()
        {
            string text = Describe(
                new[] { Step(2, ScannerDirection.East) },
                new[] { Turn(1, 5f), Turn(2, 12f), Turn(3, 3f) });

            Assert.AreEqual("Cost: 5 this turn, 12 next turn, 3 in 2 turns. Aurelia will move 2e.", text);
        }

        [TestMethod]
        public void GetSpeechTextKeepsAFractionalCostIntact()
        {
            string text = Describe(
                new[] { Step(1, ScannerDirection.Southwest) },
                new[] { Turn(1, 2.5f) });

            Assert.AreEqual("Cost: 2.5 this turn. Aurelia will move sw.", text);
        }

        [TestMethod]
        public void GetSpeechTextSpellsTheStepsOutWhenLongDirectionsAreOn()
        {
            string text = Describe(
                new[] { Step(3, ScannerDirection.North), Step(1, ScannerDirection.Southeast) },
                new[] { Turn(1, 4f) },
                useLongDirections: true);

            Assert.AreEqual("Cost: 4 this turn. Aurelia will move 3 north, southeast.", text);
        }

        [TestMethod]
        public void GetSpeechTextFallsBackToTheDestinationTileWhenThereIsNoRoute()
        {
            string text = new MapDestinationSetEvent(7, "Aurelia", new Vector2Int(34, 12), null)
                .GetSpeechText(useLongDirections: false);

            Assert.AreEqual("Aurelia's destination set to 34, 12", text);
        }

        [TestMethod]
        public void GetSpeechTextFallsBackWhenTheRouteHasNoStepsToWalkOrActionToTake()
        {
            string text = Describe(new ScannerDirectionStep[0], new[] { Turn(1, 5f) });

            Assert.AreEqual("Aurelia's destination set to 34, 12", text);
        }

        [TestMethod]
        public void GetSpeechTextNamesTheActionWhenThereIsNothingToWalk()
        {
            string text = Describe(
                new ScannerDirectionStep[0],
                new[] { Turn(1, 0.5f) },
                Interaction("Claim", "Gold Mine", 0.5f));

            Assert.AreEqual("Cost: 0.5 this turn. Aurelia will Claim Gold Mine.", text);
        }

        [TestMethod]
        public void GetSpeechTextSaysWhenTheActionHasToWaitForNextTurn()
        {
            string text = Describe(
                new ScannerDirectionStep[0],
                new[] { Turn(2, 0.5f) },
                Interaction("Claim", "Gold Mine", 0.5f));

            Assert.AreEqual("Cost: 0.5 next turn. Aurelia will Claim Gold Mine.", text);
        }

        [TestMethod]
        public void GetSpeechTextDropsTheCostWhenTheActionIsFree()
        {
            string text = Describe(
                new ScannerDirectionStep[0],
                new WielderRouteTurn[0],
                Interaction("Visit", "Watermill", 0f));

            Assert.AreEqual("Aurelia will Visit Watermill.", text);
        }

        [TestMethod]
        public void GetSpeechTextFallsBackWhenTheGameNamesNoAction()
        {
            string text = Describe(
                new ScannerDirectionStep[0],
                new[] { Turn(1, 0.5f) },
                Interaction(string.Empty, string.Empty, 0.5f));

            Assert.AreEqual("Aurelia's destination set to 34, 12", text);
        }

        [TestMethod]
        public void GetSpeechTextNamesTheActionAfterTheWalkThatLeadsToIt()
        {
            string text = Describe(
                new[] { Step(2, ScannerDirection.North), Step(1, ScannerDirection.Northeast) },
                new[] { Turn(1, 5.5f) },
                Interaction("Claim", "Gold Mine", 0.5f));

            Assert.AreEqual("Cost: 5.5 this turn. Aurelia will move 2n, ne and Claim Gold Mine.", text);
        }

        [TestMethod]
        public void GetSpeechTextReadsTheWalkAloneWhenTheGameNamesNoActionForTheDestination()
        {
            string text = Describe(
                new[] { Step(2, ScannerDirection.North) },
                new[] { Turn(1, 5.5f) },
                Interaction(string.Empty, string.Empty, 0.5f));

            Assert.AreEqual("Cost: 5.5 this turn. Aurelia will move 2n.", text);
        }

        private static string Describe(
            IReadOnlyList<ScannerDirectionStep> steps,
            IReadOnlyList<WielderRouteTurn> turns)
        {
            return Describe(steps, turns, null, useLongDirections: false);
        }

        private static string Describe(
            IReadOnlyList<ScannerDirectionStep> steps,
            IReadOnlyList<WielderRouteTurn> turns,
            bool useLongDirections)
        {
            return Describe(steps, turns, null, useLongDirections);
        }

        private static string Describe(
            IReadOnlyList<ScannerDirectionStep> steps,
            IReadOnlyList<WielderRouteTurn> turns,
            WielderRouteInteraction interaction)
        {
            return Describe(steps, turns, interaction, useLongDirections: false);
        }

        private static string Describe(
            IReadOnlyList<ScannerDirectionStep> steps,
            IReadOnlyList<WielderRouteTurn> turns,
            WielderRouteInteraction interaction,
            bool useLongDirections)
        {
            return new MapDestinationSetEvent(
                7,
                "Aurelia",
                new Vector2Int(34, 12),
                new WielderRoute(steps, turns, interaction)).GetSpeechText(useLongDirections);
        }

        private static ScannerDirectionStep Step(int count, ScannerDirection direction)
        {
            return new ScannerDirectionStep(count, direction);
        }

        private static WielderRouteTurn Turn(int travelTurns, float cost)
        {
            return new WielderRouteTurn(travelTurns, cost);
        }

        private static WielderRouteInteraction Interaction(string action, string target, float cost)
        {
            return new WielderRouteInteraction(action, target, cost);
        }
    }
}
