using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class CombatEssenceSpeechTests
    {
        [TestMethod]
        public void JoinListWithCommasUsesCommaForTwoItems()
        {
            string text = ModText.JoinListWithCommas(new[] { "order", "arcana" });

            Assert.AreEqual("order, arcana", text);
        }

        [TestMethod]
        public void JoinListWithCommasUsesCommaForThreeItems()
        {
            string text = ModText.JoinListWithCommas(new[] { "order", "arcana", "chaos" });

            Assert.AreEqual("order, arcana, chaos", text);
        }

        [TestMethod]
        public void EssenceGeneratedEventUsesSingleEssenceNoun()
        {
            EssenceGeneratedEvent essence = new EssenceGeneratedEvent(null, 1, 0, 0, 1, 0);

            Assert.AreEqual("+1 order, +1 arcana essence", essence.GetSpeechText());
        }

        [TestMethod]
        public void WielderEssenceGeneratedEventPrefixesWielderName()
        {
            WielderEssenceGeneratedEvent essence = new WielderEssenceGeneratedEvent(
                new CommanderRef(1, 1, 1, "Cecilia"),
                1,
                0,
                0,
                1,
                0);

            Assert.AreEqual("Cecilia: +1 order, +1 arcana essence", essence.GetSpeechText());
        }
    }
}
