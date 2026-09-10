using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The grouping the Keybinds tab reads from (<see cref="ModGestureCatalog"/>): the mod's own
    /// gestures arranged into named regions, and the graph spine and quick splits held out.
    /// </summary>
    [TestClass]
    public class ModGestureCatalogTests
    {
        [TestMethod]
        public void ListsGesturesFromEveryFamilyAsRebindable()
        {
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.SonarSweep));
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.SummarizeResources));
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.CombatInspect));
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.HexGridWest));
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.SaveBookmarks[0]));
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.MapSecondaryAction));
        }

        [TestMethod]
        public void HoldsBackTheGraphSpineAndQuickSplits()
        {
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.UiUp));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.UiLeftClick));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.UiRightClick));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.UiBack));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.UiCarry));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.TroopSplits[0]));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(null));
        }

        [TestMethod]
        public void EveryGroupHasACaptionAndAtLeastOneAction()
        {
            IReadOnlyList<ModGestureCatalog.Group> groups = ModGestureCatalog.Groups;
            Assert.IsTrue(groups.Count >= 6);
            for (int g = 0; g < groups.Count; g++)
            {
                Assert.IsNotNull(groups[g].Caption);
                Assert.IsTrue(groups[g].Actions.Count > 0);
                for (int i = 0; i < groups[g].Actions.Count; i++)
                {
                    Assert.IsNotNull(groups[g].Actions[i]);
                }
            }
        }

        [TestMethod]
        public void NoGestureAppearsInTwoGroups()
        {
            HashSet<string> seen = new HashSet<string>();
            foreach (InputAction action in ModGestureCatalog.RebindableActions())
            {
                Assert.IsTrue(seen.Add(action.Key), "Duplicate gesture in the catalog: " + action.Key);
            }
        }
    }
}
