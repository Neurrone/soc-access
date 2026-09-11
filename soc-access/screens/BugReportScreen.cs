using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu.BugReporting;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The game's bug reporter, made navigable as a graph. It is one window that swaps eight
    /// mutually-exclusive sub-windows under a single <c>BugReporterController</c>: bug-or-feedback,
    /// describe-issue, a loading spinner, the YouTrack search results, the compose-and-send form, a
    /// thank-you, an error, and a Discord feedback page. The build reads which sub-window is drawing
    /// now (<see cref="IBugReportAdapter.ActiveWindow"/>) and emits that one's nodes.
    ///
    /// Because the eight windows share one adapter instance, the manager - which announces a name only
    /// on arrival - would say nothing when the wizard steps from one window to the next. So the screen
    /// watches the active window in its own update and re-announces the controller's header when it
    /// moves (the header is the screen's name).
    ///
    /// The mouse-only screenshot draw tools of the compose window are left out: they only raise the
    /// report's strength, which the fields already do.
    ///
    /// ESCAPE is the game's. <c>BugReporterController.Open</c> registers <c>UI.ExitMenu</c> (and
    /// <c>UI.Cancel</c> off gamepad) on its own <c>Close</c>, so the key already shuts the reporter;
    /// the screen does not claim it.
    /// </summary>
    public sealed class BugReportScreen : LiveScreen<IBugReportAdapter>
    {
        private const string Stop = "bug-report";
        private const string DetailsRegion = "bug-report-details";
        private const string ButtonsRegion = "bug-report-buttons";

        private static readonly ScreenSource<IBugReporterController> Source =
            ScreenSource<IBugReporterController>.FromProject();

        private readonly GameTextEditor _editor = new GameTextEditor();

        // Stable subjects for the nodes the adapter has no game component for (or none under test), so
        // the cursor seats onto the same node across builds. Made once per structural key and kept.
        // The window the screen last announced a name for. Mod-owned: it outlives no menu (the
        // adapter does), but a screen lives for the whole mod load, so it is reset when the page is
        // left. There is no hook that fires on a window swap; the update reads it from the game.
        private BugReportWindow _lastWindow = BugReportWindow.None;

        protected override object ResolveMenu()
        {
            return Source.Current;
        }

        protected override IBugReportAdapter Adapt(object menu)
        {
            return new BugReportAdapter((BugReporterController)menu);
        }

        public override string Key
        {
            get { return "bug-report"; }
        }

        /// <summary>Layer 100: a system dialog over whatever raised it (the HUD button, the options
        /// page, or a crash).</summary>
        public override int Layer
        {
            get { return 100; }
        }

        public override string ScreenName
        {
            get { return HeaderName(Live); }
        }

        public override object InitialFocusStop
        {
            get { return Stop; }
        }

        /// <summary>The reporter is open and a sub-window is shown. The adapter's <c>IsPresent</c> is
        /// only the container being on; a shown window is what makes the tree non-empty.</summary>
        public override bool IsActive()
        {
            return base.IsActive() && Live != null && Live.ActiveWindow != BugReportWindow.None;
        }

        /// <summary>While the keyboard is on its way to a field, what the player types next is meant
        /// for that field and must not start a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool OwnsGameField
        {
            get { return _editor.Pending || _editor.Editing; }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            bool active = IsActive();
            _editor.Update(active);

            if (active)
            {
                // The wizard turned in place: say the window's new header, which the manager's
                // arrival-only announcement will not. SayNameIfChanged is silent when the header did
                // not move (the loading and error windows keep the previous one).
                BugReportWindow window = Live.ActiveWindow;
                if (window != _lastWindow)
                {
                    _lastWindow = window;
                    SayNameIfChanged();
                }
            }
            else
            {
                _lastWindow = BugReportWindow.None;
            }
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _editor.Abandon();
            _lastWindow = BugReportWindow.None;
            Forget();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            BuildInto(builder, Live);
        }

        /// <summary>The build proper, over any adapter: the production path passes <c>Live</c>, a test
        /// passes a stub. Never gated on <c>IsActive</c> so a test can drive one window at a time.</summary>
        public void BuildInto(GraphBuilder builder, IBugReportAdapter live)
        {
            if (live == null)
            {
                return;
            }

            builder.BeginStop(Stop);
            ControlId start = null;

            switch (live.ActiveWindow)
            {
                case BugReportWindow.BugOrFeedback:
                    Take(ref start, Paragraphs(builder, "bug:intro", live.BugOrFeedbackLines));
                    Take(ref start, Button(builder, "bug:report", live.BugButton));
                    Take(ref start, Button(builder, "bug:feedback", live.FeedbackButton));
                    break;

                case BugReportWindow.DescribeIssue:
                    Take(ref start, Paragraphs(builder, "describe:intro", live.DescribeIssueLines));
                    Take(ref start, Field(builder, "describe:field", live.DescribeField));
                    Take(ref start, Button(builder, "describe:confirm", live.DescribeConfirmButton));
                    break;

                case BugReportWindow.ComposeIssue:
                    builder.SetRegion(DetailsRegion);
                    Take(ref start, Slider(builder, "compose:severity", live.SeveritySlider));
                    Take(ref start, Field(builder, "compose:why-critical", live.WhyCriticalField));
                    Take(ref start, Slider(builder, "compose:occurrence", live.OccurrenceSlider));
                    Take(ref start, Toggle(builder, "compose:not-sure", live.NotSureToggle));
                    Take(ref start, Field(builder, "compose:subject", live.SubjectField));
                    Take(ref start, Field(builder, "compose:actual", live.ActualField));
                    Take(ref start, Field(builder, "compose:expected", live.ExpectedField));
                    Take(ref start, Text(builder, "compose:strength", () => live.ReportStrengthText));
                    builder.SetRegion(ButtonsRegion);
                    Take(ref start, Button(builder, "compose:submit", live.SubmitButton));
                    builder.SetRegion(null);
                    break;

                case BugReportWindow.ListSearchResult:
                    Take(ref start, Paragraphs(builder, "list:intro", live.ListSearchLines));
                    IList<BugReportIssue> issues = live.Issues;
                    for (int i = 0; i < issues.Count; i++)
                    {
                        Take(ref start, Issue(builder, "list:issue-" + i, issues[i]));
                    }

                    Take(ref start, Toggle(builder, "list:report-anyway", live.ReportAnywayToggle));
                    Take(ref start, Button(builder, "list:new-issue", live.NewIssueButton));
                    break;

                case BugReportWindow.ThankYou:
                    Take(ref start, Paragraphs(builder, "thanks:intro", live.ThankYouLines));
                    Take(ref start, Button(builder, "thanks:close", live.CloseButton));
                    break;

                case BugReportWindow.Error:
                    Take(ref start, Paragraphs(builder, "error:message", live.ErrorLines));
                    Take(ref start, Button(builder, "error:ok", live.ErrorOkButton));
                    break;

                case BugReportWindow.Loading:
                    Take(ref start, Text(builder, "loading:text", () => ModText.Get(ModStrings.Screens.BugReportSending)));
                    break;

                case BugReportWindow.Feedback:
                    Take(ref start, Paragraphs(builder, "feedback:intro", live.FeedbackLines));
                    Take(ref start, Button(builder, "feedback:discord", live.DiscordButton));
                    break;
            }

            if (start != null)
            {
                builder.SetStart(start);
            }
        }

        /// <summary>The reporter's header, which is the screen's name. Static so a test can prove the
        /// name follows the game's header without seating a live adapter.</summary>
        public static string HeaderName(IBugReportAdapter live)
        {
            string header = live != null ? live.Header : null;
            return string.IsNullOrWhiteSpace(header) ? null : header;
        }

        /// <summary>A search-result row's label from the game's own facts: the issue title, its state
        /// where it has one, and its upvote count. A row with no state reads as title and count.</summary>
        public static string SearchRowLabel(string title, string state, string upvotes)
        {
            title = title ?? string.Empty;
            upvotes = upvotes ?? string.Empty;
            if (string.IsNullOrWhiteSpace(state))
            {
                return ModText.Get(ModStrings.Common.ListSeparator, title, upvotes);
            }

            return ModText.Get(ModStrings.Screens.BugReportSearchResult, title, state, upvotes);
        }

        private ControlId Paragraphs(GraphBuilder builder, string key, IList<string> lines)
        {
            if (lines == null || lines.Count == 0)
            {
                return null;
            }

            IList<string> captured = lines;
            ControlId id = ControlId.For(Marker(key), key);
            builder.AddItem(new SyntheticNode(id, GraphNodes.Paragraphs(() => captured)));
            return id;
        }

        private ControlId Text(GraphBuilder builder, string key, Func<string> text)
        {
            ControlId id = ControlId.For(Marker(key), key);
            builder.AddItem(new SyntheticNode(id, GraphNodes.Text(text)));
            return id;
        }

        private ControlId Button(GraphBuilder builder, string key, BugReportButton button)
        {
            if (button == null || !button.Visible)
            {
                return null;
            }

            BugReportButton captured = button;
            NodeVtable vtable = GraphNodes.Button(
                () => captured.Label,
                () => { if (captured.Click != null) captured.Click(); },
                captured.Enabled,
                captured.Tooltip,
                captured.Details == null ? (Func<IList<string>>)null : () => captured.Details);
            return Place(builder, key, vtable, button.Subject);
        }

        private ControlId Field(GraphBuilder builder, string key, BugReportField field)
        {
            if (field == null || !field.Visible)
            {
                return null;
            }

            BugReportField captured = field;
            NodeVtable vtable = GraphNodes.EditField(
                () => captured.Label,
                // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                () => _editor.Editing || captured.Value == null ? null : captured.Value(),
                () => _editor.Request(captured.Field),
                () => captured.Enabled,
                captured.Tooltip);
            GraphNodes.DoNotDrawTooltip(vtable);
            Component subject = captured.Field != null ? captured.Field.MonoTransform : null;
            return Place(builder, key, vtable, subject);
        }

        private ControlId Slider(GraphBuilder builder, string key, BugReportSlider slider)
        {
            if (slider == null || !slider.Visible)
            {
                return null;
            }

            BugReportSlider captured = slider;
            NodeVtable vtable = GraphNodes.Slider(
                () => null,
                () => captured.Label != null ? captured.Label() : null,
                (sign, large) => { if (captured.Adjust != null) captured.Adjust(sign, large); },
                () => captured.Enabled);
            return Place(builder, key, vtable, slider.Subject);
        }

        private ControlId Toggle(GraphBuilder builder, string key, BugReportToggle toggle)
        {
            if (toggle == null || !toggle.Visible)
            {
                return null;
            }

            BugReportToggle captured = toggle;
            NodeVtable vtable = GraphNodes.Checkbox(
                () => captured.Label,
                () => captured.State != null && captured.State(),
                () => { if (captured.Toggle != null) captured.Toggle(); },
                () => captured.Enabled,
                captured.Tooltip);
            return Place(builder, key, vtable, toggle.Subject);
        }

        private ControlId Issue(GraphBuilder builder, string key, BugReportIssue issue)
        {
            if (issue == null)
            {
                return null;
            }

            BugReportIssue captured = issue;
            NodeVtable vtable = GraphNodes.Button(
                () => SearchRowLabel(captured.Title, captured.State, captured.Upvotes),
                () => { if (captured.Click != null) captured.Click(); },
                () => captured.Enabled);
            return Place(builder, key, vtable, issue.Subject);
        }

        // A drawn node tied to the game component when there is one, so native selection follows the
        // cursor; a synthetic node on a stable marker otherwise (and always under test).
        private ControlId Place(GraphBuilder builder, string key, NodeVtable vtable, Component subject)
        {
            if (subject != null)
            {
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(subject);
                ControlId id = ControlId.For(subject, key);
                builder.AddItem(new DrawnNode(id, vtable, subject));
                return id;
            }

            ControlId synthetic = ControlId.For(Marker(key), key);
            builder.AddItem(new SyntheticNode(synthetic, vtable));
            return synthetic;
        }

        private static void Take(ref ControlId start, ControlId id)
        {
            if (start == null && id != null)
            {
                start = id;
            }
        }
    }
}
