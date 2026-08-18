using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerJumpAnchorTests
    {
        [TestMethod]
        public void TakeFailsBeforeAnyJump()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();

            Vector2Int tile;
            Assert.IsFalse(anchor.TryTake(out tile));
        }

        [TestMethod]
        public void TakeReturnsTheRememberedTileOnce()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();
            anchor.Remember(new Vector2Int(4, 7));

            Vector2Int first;
            Assert.IsTrue(anchor.TryTake(out first));
            Assert.AreEqual(new Vector2Int(4, 7), first);

            Vector2Int second;
            Assert.IsFalse(anchor.TryTake(out second));
        }

        [TestMethod]
        public void LaterJumpsReplaceTheAnchor()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();
            anchor.Remember(new Vector2Int(1, 1));
            anchor.Remember(new Vector2Int(2, 2));

            Vector2Int tile;
            Assert.IsTrue(anchor.TryTake(out tile));
            Assert.AreEqual(new Vector2Int(2, 2), tile);
        }

        [TestMethod]
        public void AJumpThatMovedIsRememberedAndReportedAsAJump()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();

            Assert.IsTrue(anchor.RememberIfMoved(new Vector2Int(1, 1), new Vector2Int(5, 5)));

            Vector2Int tile;
            Assert.IsTrue(anchor.TryTake(out tile));
            Assert.AreEqual(new Vector2Int(1, 1), tile);
        }

        /// <summary>
        /// The grids answer a refused jump the same way they answer one that
        /// landed, so a cursor that never left is the whole tell. The anchor
        /// the player already had has to survive it, or the return key goes
        /// back to the tile they are standing on and does nothing.
        /// </summary>
        [TestMethod]
        public void ARefusedJumpLeavesTheAnchorAlone()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();
            anchor.Remember(new Vector2Int(2, 2));

            Assert.IsFalse(anchor.RememberIfMoved(new Vector2Int(9, 9), new Vector2Int(9, 9)));

            Vector2Int tile;
            Assert.IsTrue(anchor.TryTake(out tile));
            Assert.AreEqual(new Vector2Int(2, 2), tile);
        }

        [TestMethod]
        public void ARefusedJumpDoesNotInventAnAnchor()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();

            Assert.IsFalse(anchor.RememberIfMoved(new Vector2Int(3, 4), new Vector2Int(3, 4)));

            Vector2Int tile;
            Assert.IsFalse(anchor.TryTake(out tile));
        }

        [TestMethod]
        public void ClearDropsTheAnchor()
        {
            ScannerJumpAnchor anchor = new ScannerJumpAnchor();
            anchor.Remember(new Vector2Int(3, 3));

            anchor.Clear();

            Vector2Int tile;
            Assert.IsFalse(anchor.TryTake(out tile));
        }
    }
}
