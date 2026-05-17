using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class HexCoordinateFormatterTests
    {
        [TestMethod]
        public void FormatOriginAsZeroZero()
        {
            Assert.AreEqual("0, 0", HexCoordinateFormatter.Format(new Vector2Int(0, 0)));
        }

        [TestMethod]
        public void FormatEastAndWestAsWholeXSteps()
        {
            Assert.AreEqual("1, 0", HexCoordinateFormatter.Format(new Vector2Int(1, 0)));
            Assert.AreEqual("-1, 0", HexCoordinateFormatter.Format(new Vector2Int(-1, 0)));
        }

        [TestMethod]
        public void FormatNortheastFromEvenRowAsHalfXStep()
        {
            Assert.AreEqual("0.5, 1", HexCoordinateFormatter.Format(new Vector2Int(0, 1)));
        }

        [TestMethod]
        public void FormatRepeatedNortheastIncreasesXByHalfEachStep()
        {
            Assert.AreEqual("0.5, 1", HexCoordinateFormatter.Format(new Vector2Int(0, 1)));
            Assert.AreEqual("1, 2", HexCoordinateFormatter.Format(new Vector2Int(1, 2)));
            Assert.AreEqual("1.5, 3", HexCoordinateFormatter.Format(new Vector2Int(1, 3)));
            Assert.AreEqual("2, 4", HexCoordinateFormatter.Format(new Vector2Int(2, 4)));
        }

        [TestMethod]
        public void FormatNegativeHalfValues()
        {
            Assert.AreEqual("-0.5, 1", HexCoordinateFormatter.Format(new Vector2Int(-1, 1)));
        }

        [TestMethod]
        public void FormatWithOddRowOriginSubtractsOriginHalfOffset()
        {
            Assert.AreEqual("-0.5, -1", HexCoordinateFormatter.Format(new Vector2Int(3, 0), new Vector2Int(3, 1)));
            Assert.AreEqual("0.5, 1", HexCoordinateFormatter.Format(new Vector2Int(4, 2), new Vector2Int(3, 1)));
        }
    }
}
