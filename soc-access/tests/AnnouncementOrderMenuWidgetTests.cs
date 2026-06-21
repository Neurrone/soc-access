using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AnnouncementOrderMenuWidgetTests
    {
        [TestMethod]
        public void LeftRightWithinRowDoesNotRepeatElementLabel()
        {
            AnnouncementOrderMenuWidget widget = BuildWidget();
            FocusContext focusContext = new FocusContext();

            widget.EnsureFocus();
            string first = focusContext.BuildAnnouncement(widget.GetFocusedWidget());

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.NextColumn));
            string second = focusContext.BuildAnnouncement(widget.GetFocusedWidget());

            StringAssert.Contains(first, "Reachable horizontal row");
            StringAssert.Contains(first, "Configure button 1 of 3.");
            Assert.AreEqual("Move up button 2 of 3.", second);
        }

        [TestMethod]
        public void HorizontalMovementDoesNotWrap()
        {
            AnnouncementOrderMenuWidget widget = BuildWidget();
            widget.EnsureFocus();

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.PreviousColumn));
            Assert.AreEqual(0, widget.FocusedButtonIndex);

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.NextColumn));
            Assert.IsTrue(widget.HandleAction(AccessibilityActions.NextColumn));
            Assert.AreEqual(2, widget.FocusedButtonIndex);

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.NextColumn));
            Assert.AreEqual(2, widget.FocusedButtonIndex);
        }

        [TestMethod]
        public void VerticalMovementAndHomeEndDoNotWrap()
        {
            AnnouncementOrderMenuWidget widget = BuildWidget();
            widget.EnsureFocus();

            Assert.AreEqual("first", widget.FocusedElementKey);
            Assert.IsTrue(widget.HandleAction(AccessibilityActions.PreviousMenuItem));
            Assert.AreEqual("first", widget.FocusedElementKey);

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.LastMenuItem));
            Assert.AreEqual("third", widget.FocusedElementKey);

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.NextMenuItem));
            Assert.AreEqual("third", widget.FocusedElementKey);
        }

        [TestMethod]
        public void HomeAndEndPreserveFocusedButtonIndex()
        {
            AnnouncementOrderMenuWidget widget = BuildWidget();
            widget.EnsureFocus();
            Assert.IsTrue(widget.HandleAction(AccessibilityActions.NextColumn));

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.LastMenuItem));

            Assert.AreEqual("third", widget.FocusedElementKey);
            Assert.AreEqual(1, widget.FocusedButtonIndex);

            Assert.IsTrue(widget.HandleAction(AccessibilityActions.FirstMenuItem));

            Assert.AreEqual("first", widget.FocusedElementKey);
            Assert.AreEqual(1, widget.FocusedButtonIndex);
        }

        [TestMethod]
        public void MoveFeedbackReportsNewPosition()
        {
            List<string> order = new List<string> { "first", "second", "third" };
            AnnouncementOrderMenuWidget widget = BuildWidget(order);
            widget.EnsureFocus();

            Assert.AreEqual("Moved before Coordinates", widget.BuildMoveFeedback("first"));

            order.Remove("second");
            order.Add("second");

            Assert.AreEqual("Moved after Influence", widget.BuildMoveFeedback("second"));
        }

        private static AnnouncementOrderMenuWidget BuildWidget()
        {
            return BuildWidget(new List<string> { "first", "second", "third" });
        }

        private static AnnouncementOrderMenuWidget BuildWidget(List<string> order)
        {
            AnnouncementGroupDefinition group = new AnnouncementGroupDefinition(
                "test_order",
                "Test",
                ModStrings.Screens.TileAnnouncements,
                new AnnouncementElementDefinition("first", ModStrings.Screens.AnnouncementReachable),
                new AnnouncementElementDefinition("second", ModStrings.Screens.AnnouncementCoordinates),
                new AnnouncementElementDefinition("third", ModStrings.Screens.AnnouncementInfluence));

            return new AnnouncementOrderMenuWidget(
                "test-order",
                group,
                null,
                testGroup => order,
                (testGroup, key, delta) =>
                {
                    int index = order.IndexOf(key);
                    int targetIndex = index + delta;
                    if (index < 0 || targetIndex < 0 || targetIndex >= order.Count)
                    {
                        return false;
                    }

                    order.RemoveAt(index);
                    order.Insert(targetIndex, key);
                    return true;
                });
        }
    }
}
