using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Scanner;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class ScannerResultLabelsTests
    {
        [TestMethod]
        public void ZoneOfControlIncludesZoneOfControl()
        {
            string label = ScannerResultLabels.ZoneOfControl(8, "A swarm of Barony of Loth troops");

            Assert.AreEqual("8 tiles within A swarm of Barony of Loth troops' zone of control", label);
        }
    }
}
