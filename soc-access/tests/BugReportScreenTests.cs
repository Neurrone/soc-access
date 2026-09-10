using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>
    /// The bug reporter screen's composition, over a stub adapter: which nodes each sub-window emits,
    /// that the "why is it critical" field appears only when the game marks it so, that a disabled
    /// Submit surfaces the game's reason, how a search row reads, and that the screen leaves Escape to
    /// the game.
    /// </summary>
    [TestClass]
    public class BugReportScreenTests
    {
        private static GraphRender Render(BugReportStub stub)
        {
            GraphBuilder builder = new GraphBuilder();
            new BugReportScreen().BuildInto(builder, stub);
            return builder.Build();
        }

        private static HashSet<string> Keys(GraphRender render)
        {
            return new HashSet<string>(render.Nodes.Keys
                .Select(id => id.StructuralKey as string)
                .Where(key => key != null));
        }

        private static GraphNode Node(GraphRender render, string key)
        {
            return render.NodeAt(ControlId.Structural(key));
        }

        [TestMethod]
        public void BugOrFeedbackEmitsIntroAndTwoButtons()
        {
            HashSet<string> keys = Keys(Render(new BugReportStub
            {
                ActiveWindow = BugReportWindow.BugOrFeedback,
                BugOrFeedbackLines = new[] { "What went wrong?" },
                BugButton = Button("Bug"),
                FeedbackButton = Button("Feedback"),
            }));

            CollectionAssert.AreEquivalent(
                new[] { "bug:intro", "bug:report", "bug:feedback" },
                keys.Where(k => k.StartsWith("bug:")).ToArray());
        }

        [TestMethod]
        public void DescribeIssueEmitsIntroFieldAndConfirm()
        {
            HashSet<string> keys = Keys(Render(new BugReportStub
            {
                ActiveWindow = BugReportWindow.DescribeIssue,
                DescribeIssueLines = new[] { "Describe it." },
                DescribeField = Field("Describe the issue"),
                DescribeConfirmButton = Button("Confirm"),
            }));

            Assert.IsTrue(keys.Contains("describe:intro"));
            Assert.IsTrue(keys.Contains("describe:field"));
            Assert.IsTrue(keys.Contains("describe:confirm"));
        }

        [TestMethod]
        public void ComposeOmitsWhyCriticalWhenNotCritical()
        {
            HashSet<string> keys = Keys(Render(Compose(whyCriticalVisible: false)));

            Assert.IsFalse(keys.Contains("compose:why-critical"));
            Assert.IsTrue(keys.Contains("compose:severity"));
            Assert.IsTrue(keys.Contains("compose:occurrence"));
            Assert.IsTrue(keys.Contains("compose:not-sure"));
            Assert.IsTrue(keys.Contains("compose:subject"));
            Assert.IsTrue(keys.Contains("compose:actual"));
            Assert.IsTrue(keys.Contains("compose:expected"));
            Assert.IsTrue(keys.Contains("compose:strength"));
            Assert.IsTrue(keys.Contains("compose:submit"));
        }

        [TestMethod]
        public void ComposeAddsWhyCriticalOnlyWhenCritical()
        {
            HashSet<string> keys = Keys(Render(Compose(whyCriticalVisible: true)));
            Assert.IsTrue(keys.Contains("compose:why-critical"));
        }

        [TestMethod]
        public void DisabledSubmitSurfacesTheGamesReason()
        {
            BugReportStub stub = Compose(whyCriticalVisible: false);
            stub.SubmitButton = new BugReportButton
            {
                Label = "Send Report",
                Enabled = () => false,
                Visible = true,
                Tooltip = new Tooltip(
                    () => (IReadOnlyList<string>)new[] { "Fill in the subject and expected result." },
                    null),
            };

            GraphNode submit = Node(Render(stub), "compose:submit");
            Assert.IsNotNull(submit);
            List<string> buffer = NodeBuffer.Lines(submit);
            Assert.IsTrue(
                buffer.Any(line => line.Contains("Fill in the subject and expected result.")),
                "Submit's disabled reason should reach the review buffer. Buffer: " + string.Join(" / ", buffer));
        }

        [TestMethod]
        public void SearchRowReadsTitleStateAndUpvotes()
        {
            Assert.AreEqual("Broken tooltip, open, 12", BugReportScreen.SearchRowLabel("Broken tooltip", "open", "12"));
        }

        [TestMethod]
        public void SearchRowWithoutStateReadsTitleAndUpvotes()
        {
            Assert.AreEqual("Broken tooltip, 12", BugReportScreen.SearchRowLabel("Broken tooltip", null, "12"));
        }

        [TestMethod]
        public void ScreenNameFollowsTheHeader()
        {
            Assert.AreEqual("Welcome to the bug reporter",
                BugReportScreen.HeaderName(new BugReportStub { Header = "Welcome to the bug reporter" }));
            Assert.IsNull(BugReportScreen.HeaderName(new BugReportStub { Header = "   " }));
            Assert.IsNull(BugReportScreen.HeaderName(null));
        }

        [TestMethod]
        public void EscapeIsLeftToTheGame()
        {
            Assert.IsFalse(new BugReportScreen().ConsumesBack);
        }

        private static BugReportStub Compose(bool whyCriticalVisible)
        {
            return new BugReportStub
            {
                ActiveWindow = BugReportWindow.ComposeIssue,
                SeveritySlider = Slider("Inconvenience: Minor"),
                WhyCriticalField = new BugReportField { Label = "Why is it critical", Visible = whyCriticalVisible },
                OccurrenceSlider = Slider("Perceived Occurrence: Often"),
                NotSureToggle = Toggle("I'm not sure"),
                SubjectField = Field("Subject"),
                ActualField = Field("Actual result"),
                ExpectedField = Field("Expected result"),
                ReportStrengthText = "Report Strength: Poor",
                SubmitButton = Button("Send Report"),
            };
        }

        private static BugReportButton Button(string label)
        {
            return new BugReportButton { Label = label, Enabled = () => true, Visible = true };
        }

        private static BugReportField Field(string label)
        {
            return new BugReportField { Label = label, Value = () => string.Empty, Enabled = true, Visible = true };
        }

        private static BugReportSlider Slider(string label)
        {
            return new BugReportSlider { Label = () => label, Enabled = true, Visible = true };
        }

        private static BugReportToggle Toggle(string label)
        {
            return new BugReportToggle { Label = label, State = () => false, Enabled = true, Visible = true };
        }

        /// <summary>A hand-set adapter: every getter returns what the test put in it, defaulting to
        /// nothing, so a window's build reads only the facts the test cares about.</summary>
        private sealed class BugReportStub : IBugReportAdapter
        {
            public bool IsPresent()
            {
                return true;
            }

            public BugReportWindow ActiveWindow { get; set; }
            public string Header { get; set; }

            public IList<string> BugOrFeedbackLines { get; set; }
            public BugReportButton BugButton { get; set; }
            public BugReportButton FeedbackButton { get; set; }

            public IList<string> DescribeIssueLines { get; set; }
            public BugReportField DescribeField { get; set; }
            public BugReportButton DescribeConfirmButton { get; set; }

            public BugReportSlider SeveritySlider { get; set; }
            public BugReportField WhyCriticalField { get; set; }
            public BugReportSlider OccurrenceSlider { get; set; }
            public BugReportToggle NotSureToggle { get; set; }
            public BugReportField SubjectField { get; set; }
            public BugReportField ActualField { get; set; }
            public BugReportField ExpectedField { get; set; }
            public string ReportStrengthText { get; set; }
            public BugReportButton SubmitButton { get; set; }

            public IList<string> ListSearchLines { get; set; }
            public IList<BugReportIssue> Issues { get; set; } = new List<BugReportIssue>();
            public BugReportToggle ReportAnywayToggle { get; set; }
            public BugReportButton NewIssueButton { get; set; }

            public IList<string> ThankYouLines { get; set; }
            public BugReportButton CloseButton { get; set; }

            public IList<string> ErrorLines { get; set; }
            public BugReportButton ErrorOkButton { get; set; }

            public IList<string> FeedbackLines { get; set; }
            public BugReportButton DiscordButton { get; set; }
        }
    }
}
