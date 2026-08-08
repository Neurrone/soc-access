using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Audio;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class DirectionalCueMathTests
    {
        private const float Tolerance = 1e-4f;

        [TestMethod]
        public void CursorOwnTilePlaysFlatAtFullGain()
        {
            float pan;
            float semitones;
            float gainScale;

            Assert.IsTrue(Compute(new Vector2Int(4, 7), new Vector2Int(4, 7), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(0f, pan, Tolerance);
            Assert.AreEqual(0f, semitones, Tolerance);
            Assert.AreEqual(1f, gainScale, Tolerance);

            Assert.IsTrue(Compute(new Vector2Int(4, 7), new Vector2Int(4, 7), CueGridGeometry.Hex, out pan, out semitones, out gainScale));
            Assert.AreEqual(0f, pan, Tolerance);
            Assert.AreEqual(0f, semitones, Tolerance);
            Assert.AreEqual(1f, gainScale, Tolerance);
        }

        [TestMethod]
        public void SquarePanIsColumnOffsetOverTwelve()
        {
            float pan;
            float semitones;
            float gainScale;
            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(6, 0), CueGridGeometry.Square, out pan, out semitones, out gainScale));

            Assert.AreEqual(0.5f, pan, Tolerance);
            Assert.AreEqual(0f, semitones, Tolerance);
            Assert.AreEqual(1f - 6f / 30f, gainScale, Tolerance);
        }

        [TestMethod]
        public void SquarePanSaturatesAtTwelveColumnsEitherSide()
        {
            float pan;
            float semitones;
            float gainScale;

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(20, 0), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(1f, pan, Tolerance);

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(-20, 0), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(-1f, pan, Tolerance);
        }

        [TestMethod]
        public void SquarePitchIsOneSemitonePerRowAndSignedByDirection()
        {
            float pan;
            float semitones;
            float gainScale;

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(0, 5), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(5f, semitones, Tolerance);
            Assert.AreEqual(0f, pan, Tolerance);

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(0, -5), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(-5f, semitones, Tolerance);
        }

        [TestMethod]
        public void PitchClampsToOneOctaveEitherWay()
        {
            float pan;
            float semitones;
            float gainScale;

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(0, 20), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(12f, semitones, Tolerance);

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(0, -20), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.AreEqual(-12f, semitones, Tolerance);

            // Hex is two semitones per row, so it saturates in half the rows.
            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(0, 7), CueGridGeometry.Hex, out pan, out semitones, out gainScale));
            Assert.AreEqual(12f, semitones, Tolerance);
        }

        [TestMethod]
        public void SquareGainFallsOffWithEuclideanDistance()
        {
            float pan;
            float semitones;
            float gainScale;
            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(3, 4), CueGridGeometry.Square, out pan, out semitones, out gainScale));

            Assert.AreEqual(1f - 5f / 30f, gainScale, Tolerance);
        }

        [TestMethod]
        public void TargetsAtOrBeyondThirtyTilesAreDroppedNotFloored()
        {
            float pan;
            float semitones;
            float gainScale;

            Assert.IsFalse(Compute(new Vector2Int(0, 0), new Vector2Int(30, 0), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.IsFalse(Compute(new Vector2Int(0, 0), new Vector2Int(45, 0), CueGridGeometry.Square, out pan, out semitones, out gainScale));
            Assert.IsFalse(Compute(new Vector2Int(0, 0), new Vector2Int(30, 0), CueGridGeometry.Hex, out pan, out semitones, out gainScale));
        }

        [TestMethod]
        public void HexPanUsesTheOddRowHalfColumnOffset()
        {
            Assert.AreEqual(0.5f, DirectionalCueMath.HexColumnDelta(new Vector2Int(0, 0), new Vector2Int(0, 1)), Tolerance);
            Assert.AreEqual(-0.5f, DirectionalCueMath.HexColumnDelta(new Vector2Int(0, 1), new Vector2Int(0, 0)), Tolerance);
            Assert.AreEqual(2f, DirectionalCueMath.HexColumnDelta(new Vector2Int(0, 1), new Vector2Int(2, 3)), Tolerance);

            float pan;
            float semitones;
            float gainScale;
            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(0, 1), CueGridGeometry.Hex, out pan, out semitones, out gainScale));
            Assert.AreEqual(0.5f / 6f, pan, Tolerance);
            Assert.AreEqual(2f, semitones, Tolerance);
            Assert.AreEqual(1f - 1f / 30f, gainScale, Tolerance);
        }

        [TestMethod]
        public void HexPanSaturatesAtSixColumns()
        {
            float pan;
            float semitones;
            float gainScale;

            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(10, 0), CueGridGeometry.Hex, out pan, out semitones, out gainScale));
            Assert.AreEqual(1f, pan, Tolerance);

            Assert.IsTrue(Compute(new Vector2Int(10, 0), new Vector2Int(0, 0), CueGridGeometry.Hex, out pan, out semitones, out gainScale));
            Assert.AreEqual(-1f, pan, Tolerance);
        }

        [TestMethod]
        public void HexDistanceUsesCubeCoordinates()
        {
            Assert.AreEqual(0, DirectionalCueMath.HexDistance(new Vector2Int(3, 3), new Vector2Int(3, 3)));
            Assert.AreEqual(1, DirectionalCueMath.HexDistance(new Vector2Int(0, 0), new Vector2Int(0, 1)));
            Assert.AreEqual(1, DirectionalCueMath.HexDistance(new Vector2Int(0, 0), new Vector2Int(1, 0)));
            Assert.AreEqual(12, DirectionalCueMath.HexDistance(new Vector2Int(0, 0), new Vector2Int(12, 0)));

            // Diagonally stacked rows travel less far than the naive row + column sum.
            Assert.AreEqual(4, DirectionalCueMath.HexDistance(new Vector2Int(0, 0), new Vector2Int(2, 4)));
        }

        [TestMethod]
        public void HexGainUsesHexDistanceNotEuclidean()
        {
            float pan;
            float semitones;
            float gainScale;
            Assert.IsTrue(Compute(new Vector2Int(0, 0), new Vector2Int(2, 4), CueGridGeometry.Hex, out pan, out semitones, out gainScale));

            Assert.AreEqual(1f - 4f / 30f, gainScale, Tolerance);
        }

        private static bool Compute(
            Vector2Int origin,
            Vector2Int target,
            CueGridGeometry geometry,
            out float pan,
            out float semitones,
            out float gainScale)
        {
            return DirectionalCueMath.TryCompute(origin, target, geometry, out pan, out semitones, out gainScale);
        }
    }
}
