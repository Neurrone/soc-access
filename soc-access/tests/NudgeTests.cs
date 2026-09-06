using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// When the mod finishes a job the game dropped.
    ///
    /// Both waits are the design and both are easy to lose: without the settle the mod pushes a page
    /// that was merely slow, and without the pause it pushes again every frame while its own first
    /// push is still being carried out - which on the planet page would mean racing the game's own
    /// show coroutine rather than replacing it.
    /// </summary>
    [TestClass]
    public class NudgeTests
    {
        private static bool Run(Nudge nudge, int calls, bool stalled = true, bool safe = true)
        {
            bool pushed = false;
            for (int i = 0; i < calls; i++)
            {
                pushed |= nudge.Due(stalled, safe);
            }

            return pushed;
        }

        [TestMethod]
        public void SomethingTheGameIsMerelySlowAboutIsLeftAlone()
        {
            Nudge nudge = new Nudge(3, 5);
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsFalse(nudge.Due(true, true));
        }

        [TestMethod]
        public void SomethingStillUndoneAfterTheSettleIsPushedOnce()
        {
            Nudge nudge = new Nudge(3, 5);
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsTrue(nudge.Due(true, true));
            Assert.IsFalse(Run(nudge, 5));
        }

        [TestMethod]
        public void TheGameIsLeftAloneForThePauseAndPushedAgainAfterIt()
        {
            Nudge nudge = new Nudge(2, 4);
            Assert.IsTrue(Run(nudge, 2));
            Assert.IsFalse(Run(nudge, 4));
            Assert.IsTrue(Run(nudge, 2));
        }

        [TestMethod]
        public void AStallThatClearsAndComesBackStartsTheSettleOver()
        {
            Nudge nudge = new Nudge(3, 5);
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsFalse(nudge.Due(false, true));
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsTrue(nudge.Due(true, true));
        }

        [TestMethod]
        public void AFrameTheGameCouldNotHaveFinishedInDoesNotCount()
        {
            Nudge nudge = new Nudge(2, 5);
            Assert.IsFalse(Run(nudge, 10, stalled: true, safe: false));
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsTrue(nudge.Due(true, true));
        }

        [TestMethod]
        public void ForgettingPutsBothWaitsBack()
        {
            Nudge nudge = new Nudge(2, 10);
            Assert.IsTrue(Run(nudge, 2));
            nudge.Forget();
            Assert.IsFalse(nudge.Due(true, true));
            Assert.IsTrue(nudge.Due(true, true));
        }
    }
}
