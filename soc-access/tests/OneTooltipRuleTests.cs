using System.Collections.Generic;
using SongsOfConquestAccess.UI.Graph;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using static SongsOfConquestAccess.Tests.Graphs;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The count behind the builder's refusal: how many DIFFERENT hover surfaces a node declared.
    ///
    /// A game draws one tooltip at a time, so a node raises only the one it aims at and any other
    /// tooltip's words on it are a buffer promise nothing can keep. The rule has to tell that shape
    /// apart from the legal one it looks exactly like - a single tooltip split into a spoken half and a
    /// buffer-only half - which is why it counts sources rather than sections, and why every case here
    /// is about which sections share a source.
    /// </summary>
    [TestClass]
    public class OneTooltipRuleTests
    {
        private static readonly object Hero = new object();
        private static readonly object Refusal = new object();

        private static NodeSection Tooltip(object source)
        {
            return NodeSection.Derived(Words, TooltipMode.Announce, null, source);
        }

        [TestMethod]
        public void OneTooltipIsTheOrdinaryNode()
        {
            Assert.IsFalse(OneTooltipRule.Breached(Sections(Tooltip(Hero))));
            Assert.AreEqual(1, OneTooltipRule.Sources(Sections(Tooltip(Hero))));
        }

        [TestMethod]
        public void TwoDIFFERENTTooltipsAreTheRefusal()
        {
            Assert.IsTrue(OneTooltipRule.Breached(Sections(Tooltip(Hero), Tooltip(Refusal))));
            Assert.AreEqual(2, OneTooltipRule.Sources(Sections(Tooltip(Hero), Tooltip(Refusal))));
        }

        [TestMethod]
        public void TheHintSplitIsTwoSectionsOfONETooltip()
        {
            // What a hint-blocked button declares: its description speaks, the mouse instruction it ends
            // in is buffer-only, and both come off the one tooltip a hover would raise.
            IList<NodeSection> hint = Sections(
                Tooltip(Hero),
                NodeSection.Derived(Words, TooltipMode.None, null, Hero)
            );
            Assert.IsFalse(OneTooltipRule.Breached(hint));
            Assert.AreEqual(1, OneTooltipRule.Sources(hint));
        }

        [TestMethod]
        public void ComposedAndDrawnSectionsAreNotHoverSurfaces()
        {
            Assert.IsFalse(
                OneTooltipRule.Breached(
                    Sections(
                        NodeSection.Buffer(Words),
                        NodeSection.Composed(Words),
                        Tooltip(Hero),
                        NodeSection.Buffer(Words)
                    )
                )
            );
        }

        [TestMethod]
        public void AReviewedSecondaryIsCountedWhereverItNAMESItsTooltip()
        {
            // The reviewed-secondary shape came off NodeSection.Buffer, which names no tooltip, so the
            // rule cannot see it. Nothing here should pretend otherwise: the buffer half is not counted.
            Assert.IsFalse(
                OneTooltipRule.Breached(Sections(Tooltip(Hero), NodeSection.Buffer(Words)))
            );
        }

        [TestMethod]
        public void AnUnnamedSourceIsNeverCounted()
        {
            IList<NodeSection> unnamed = Sections(
                NodeSection.Derived(Words, TooltipMode.Announce, null),
                NodeSection.Derived(Words, TooltipMode.Announce, null)
            );
            Assert.IsFalse(OneTooltipRule.Breached(unnamed));
            Assert.AreEqual(0, OneTooltipRule.Sources(unnamed));
        }

        [TestMethod]
        public void ThreeSectionsOverTwoTooltipsStillBreaches()
        {
            Assert.IsTrue(
                OneTooltipRule.Breached(
                    Sections(Tooltip(Hero), Tooltip(Hero), Tooltip(Refusal))
                )
            );
        }

        [TestMethod]
        public void NothingDeclaredIsNotABreach()
        {
            Assert.IsFalse(OneTooltipRule.Breached(null));
            Assert.IsFalse(OneTooltipRule.Breached(new List<NodeSection>()));
            Assert.IsFalse(OneTooltipRule.Breached(Sections(null, Tooltip(Hero), null)));
        }
    }
}
