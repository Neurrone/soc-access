using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The grouping the Keybinds tab reads from (<see cref="ModGestureCatalog"/>): the mod's own
    /// gestures arranged into named regions, the graph spine first, and the quick splits held out.
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
        }

        [TestMethod]
        public void ListsTheGraphSpineInItsOwnFirstGroup()
        {
            ModGestureCatalog.Group cursor = ModGestureCatalog.Groups[0];
            Assert.AreEqual(ModStrings.Screens.Cursor, cursor.Caption);
            CollectionAssert.Contains((System.Collections.ICollection)cursor.Actions, AccessibilityActions.UiNext);
            CollectionAssert.Contains((System.Collections.ICollection)cursor.Actions, AccessibilityActions.UiPrev);
            CollectionAssert.Contains((System.Collections.ICollection)cursor.Actions, AccessibilityActions.UiHome);
            CollectionAssert.Contains((System.Collections.ICollection)cursor.Actions, AccessibilityActions.UiEnd);
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.UiLeftClick));
            Assert.IsTrue(ModGestureCatalog.IsRebindable(AccessibilityActions.UiBack));
        }

        [TestMethod]
        public void HoldsBackTheQuickSplits()
        {
            Assert.IsFalse(ModGestureCatalog.IsRebindable(AccessibilityActions.TroopSplits[0]));
            Assert.IsFalse(ModGestureCatalog.IsRebindable(null));
        }

        [TestMethod]
        public void PlacesTheOwnersMovesAndEndsWithBookmarks()
        {
            IReadOnlyList<ModGestureCatalog.Group> groups = ModGestureCatalog.Groups;
            Assert.AreEqual(ModStrings.Screens.ReviewBuffer, groups[1].Caption);
            Assert.AreEqual(ModStrings.Screens.AdventureMap, groups[2].Caption);
            Assert.AreEqual(ModStrings.Screens.Scanner, groups[3].Caption);
            Assert.AreEqual(ModStrings.Screens.Bookmarks, groups[groups.Count - 1].Caption);
            CollectionAssert.Contains((System.Collections.ICollection)groups[2].Actions, AccessibilityActions.SummarizeResources);
            CollectionAssert.Contains((System.Collections.ICollection)groups[2].Actions, AccessibilityActions.ScannerLookAround);
            CollectionAssert.Contains((System.Collections.ICollection)groups[2].Actions, AccessibilityActions.ScannerIncreaseLookAroundRadius);
            CollectionAssert.DoesNotContain((System.Collections.ICollection)groups[3].Actions, AccessibilityActions.ScannerLookAround);
            Assert.AreEqual(ModStrings.Screens.HexGrid, groups[4].Caption);
            Assert.AreEqual(ModStrings.Screens.Combat, groups[5].Caption);
            CollectionAssert.Contains((System.Collections.ICollection)groups[5].Actions, AccessibilityActions.SummarizeEnemyResources);
        }

        [TestMethod]
        public void EveryGroupHasACaptionAndAtLeastOneAction()
        {
            IReadOnlyList<ModGestureCatalog.Group> groups = ModGestureCatalog.Groups;
            Assert.IsTrue(groups.Count >= 7);
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
