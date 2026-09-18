using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest;
using SongsOfConquest.Client.Menu.BugReporting;
using SongsOfConquest.Client.Menu.BugReporting.Windows;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>Which of the bug reporter's eight mutually-exclusive sub-windows is drawing now, read
    /// from the game each frame. <see cref="BugReportWindow.None"/> is the reporter open with no
    /// window shown yet - the pre-first-window frame the readiness check skips.</summary>
    public enum BugReportWindow
    {
        None,
        BugOrFeedback,
        DescribeIssue,
        ComposeIssue,
        ListSearchResult,
        ThankYou,
        Error,
        Loading,
        Feedback,
    }

    /// <summary>A button of the reporter as native facts: what the game drew on it, whether it is
    /// available, the component to sync native selection with, and the game's own click.</summary>
    public sealed class BugReportButton
    {
        public string Label;
        // Live: Submit's availability changes as other fields are filled, and the readout after an
        // action must reflect the game now, not a build-time snapshot.
        public Func<bool> Enabled;
        public bool Visible;
        public Component Subject;
        public Action Click;
        public Tooltip Tooltip;
        public IList<string> Details;
    }

    /// <summary>One of the reporter's text fields: the native placeholder that says what it is for,
    /// the text in it now, and the game field the editor hands the keyboard to.</summary>
    public sealed class BugReportField
    {
        public string Label;
        // Live: the text in the field changes as the player types.
        public Func<string> Value;
        public bool Enabled;
        public bool Visible;
        public IUITextMeshInputField Field;
        public Tooltip Tooltip;
    }

    /// <summary>A severity or occurrence slider: the game's own composed label (concept and value in
    /// one), and the game's own value change.</summary>
    public sealed class BugReportSlider
    {
        // Live: the game recomposes the label when the value moves, and the readout right after an
        // adjust must be the new value, not the one from the last build.
        public Func<string> Label;
        public bool Enabled;
        public bool Visible;
        public Component Subject;
        public Action<int, bool> Adjust;
    }

    /// <summary>A checkbox of the reporter with its native label and state.</summary>
    public sealed class BugReportToggle
    {
        public string Label;
        // Live: the readout right after a toggle must be the new state.
        public Func<bool> State;
        public bool Enabled;
        public bool Visible;
        public Component Subject;
        public Action Toggle;
        public Tooltip Tooltip;
    }

    /// <summary>A YouTrack search result the reporter listed: the native title, state and upvote count,
    /// and the game's own click that votes for it.</summary>
    public sealed class BugReportIssue
    {
        public string Title;
        public string State;
        public string Upvotes;
        public bool Enabled;
        public Component Subject;
        public Action Click;
    }

    /// <summary>The bug reporter read as native facts. Component-typed members are null when the game
    /// object is absent (or under test), which the screen answers by declaring a synthetic node.</summary>
    public interface IBugReportAdapter : IPresent
    {
        BugReportWindow ActiveWindow { get; }
        string Header { get; }

        IList<string> BugOrFeedbackLines { get; }
        BugReportButton BugButton { get; }
        BugReportButton FeedbackButton { get; }

        IList<string> DescribeIssueLines { get; }
        BugReportField DescribeField { get; }
        BugReportButton DescribeConfirmButton { get; }

        BugReportSlider SeveritySlider { get; }
        BugReportField WhyCriticalField { get; }
        BugReportSlider OccurrenceSlider { get; }
        BugReportToggle NotSureToggle { get; }
        BugReportField SubjectField { get; }
        BugReportField ActualField { get; }
        BugReportField ExpectedField { get; }
        string ReportStrengthText { get; }
        BugReportButton SubmitButton { get; }

        IList<string> ListSearchLines { get; }
        IList<BugReportIssue> Issues { get; }
        BugReportToggle ReportAnywayToggle { get; }
        BugReportButton NewIssueButton { get; }

        IList<string> ThankYouLines { get; }
        BugReportButton CloseButton { get; }

        IList<string> ErrorLines { get; }
        BugReportButton ErrorOkButton { get; }

        IList<string> FeedbackLines { get; }
        BugReportButton DiscordButton { get; }
    }

    /// <summary>
    /// Reads one live <c>BugReporterController</c> and its eight windows. Every game object,
    /// <c>FieldInfo</c> and component reference is resolved once in the constructor (misses cached
    /// too, behind the null the field keeps), so a build only reads live text and state. The adapter
    /// lives exactly as long as the controller instance the slot wraps.
    ///
    /// The windows share one controller, so the screen reads <see cref="ActiveWindow"/> - the one
    /// whose game object is active - each frame and emits that window's nodes. The slider divides its
    /// 0..1 range into five bands, the 0.2 grid the game's own severity and occurrence are read on.
    /// </summary>
    public sealed class BugReportAdapter : IBugReportAdapter
    {
        // Five bands - six stops - over the slider's range, which is the 0.2 grid the two sliders
        // are read on. The game reads the raw 0..1 value against thresholds, not against a band
        // count (Lavapotion.BugReporter.BugReportSeverityExtensions.CalculateSeverity and
        // BugReportOccurenceExtensions.CalculateOccurence): severity is None at 0, then Trivial
        // below 0.25, Minor below 0.5, Major below 0.75 and Critical above it, so the stops read
        // None/Trivial/Minor/Major/Critical/Critical and the top two say the same thing; occurrence
        // is Never at 0, HardlyEver below 0.2, Rarely below 0.4, Sometimes below 0.6, Often below
        // 0.9 and Always above it, so the stops read Never/Rarely/Sometimes/Often/Often/Always -
        // the top stop is the only Always there, which is why the grid keeps its sixth stop.
        private const int SliderBands = 5;

        // Controller fields.
        private static readonly FieldInfo MenuContainerField = AccessTools.Field(typeof(BugReporterController), "_menuContainer");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(BugReporterController), "_headerText");
        private static readonly FieldInfo BugOrFeedbackField = AccessTools.Field(typeof(BugReporterController), "_bugsOrFeedbackWindow");
        private static readonly FieldInfo DescribeIssueField = AccessTools.Field(typeof(BugReporterController), "_describeIssueWindow");
        private static readonly FieldInfo ComposeIssueField = AccessTools.Field(typeof(BugReporterController), "_composeIssueWindow");
        private static readonly FieldInfo ListSearchField = AccessTools.Field(typeof(BugReporterController), "_listSearchResultWindow");
        private static readonly FieldInfo ThankYouField = AccessTools.Field(typeof(BugReporterController), "_thankYouWindow");
        private static readonly FieldInfo ErrorField = AccessTools.Field(typeof(BugReporterController), "_errorWindow");
        private static readonly FieldInfo LoadingField = AccessTools.Field(typeof(BugReporterController), "_loadingWindow");
        private static readonly FieldInfo FeedbackField = AccessTools.Field(typeof(BugReporterController), "_feedbackWindow");
        private static readonly FieldInfo SliderTextMeshField = AccessTools.Field(typeof(UISlider), "_textMesh");
        private static readonly FieldInfo EntryPoolField = AccessTools.Field(typeof(BugReporterListSearchResultWindow), "_entryPool");
        private static readonly FieldInfo EntryIssueField = AccessTools.Field(typeof(BugReporterSearchResultEntry), "_issue");

        private readonly UITransform _menuContainer;
        private readonly UITextMesh _headerText;

        private readonly BugReporterBugOrFeedbackWindow _bugOrFeedback;
        private readonly BugReporterDescribeIssueWindow _describeIssue;
        private readonly BugReporterComposeIssueWindow _compose;
        private readonly BugReporterListSearchResultWindow _listSearch;
        private readonly BugReporterThankYouWindow _thankYou;
        private readonly BugReporterErrorWindow _error;
        private readonly BugReporterLoadingWindow _loading;
        private readonly BugReporterFeedbackWindow _feedback;

        // Bug or feedback window controls.
        private readonly UITextMesh _bofDescription;
        private readonly UITextMesh _bofDisclaimer;
        private readonly UIButton _bofBugButton;
        private readonly UITextMesh _bofBugExample;
        private readonly UIButton _bofFeedbackButton;
        private readonly UITextMesh _bofFeedbackExample;

        // Describe issue window controls.
        private readonly UITextMesh _diDescription;
        private readonly UITextMeshInputField _diInput;
        private readonly UITextMesh _diEmpty;
        private readonly UIButton _diButton;

        // Compose issue window controls.
        private readonly UISlider _cSeverity;
        private readonly UITextMeshInputField _cWhyCritical;
        private readonly UISlider _cOccurrence;
        private readonly UIToggle _cNotSure;
        private readonly UITextMeshInputField _cSubject;
        private readonly UITextMeshInputField _cActual;
        private readonly UITextMeshInputField _cExpected;
        private readonly UIButton _cSubmit;
        private readonly UITextMesh _cReportStrength;

        // List search result window controls.
        private readonly UITextMesh _lsDescription;
        private readonly Transform _lsEntryContainer;
        private readonly UIButton _lsNewIssueButton;
        private readonly UIToggle _lsNewIssueToggle;

        // Thank you window controls.
        private readonly UITextMesh _tyDescription;
        private readonly UIButton _tyButton;

        // Error window controls.
        private readonly UITextMesh _errText;
        private readonly UIButton _errButton;

        // Feedback window controls.
        private readonly UITextMesh _fbDescription;
        private readonly UIButton _fbDiscordButton;
        private readonly UITextMesh _fbDiscordDescription;

        // The search-result rows are spawned once per result set and do not change afterwards, so the
        // snapshot is rebuilt only when what the window has spawned moves - two game-owned reads per
        // frame, never a subtree walk per frame. The container's childCount will not do it: the
        // window's pool only switches a row OFF to retire it (SimpleGameObjectPool.Despawn), so the
        // count is a high-water mark and a second search returning as many results as the first would
        // keep the first set's rows. What the pool holds active answers instead - how many rows are
        // spawned now, and which issue the first of them was given. The pool itself is read through
        // its handle every frame rather than kept: the window builds it in its own Awake, which has
        // not necessarily run when this adapter is made.
        private int _entrySnapshotCount = -1;
        private object _entrySnapshotFirstIssue;
        private List<BugReportIssue> _issues = new List<BugReportIssue>();

        public BugReportAdapter(BugReporterController controller)
        {
            _menuContainer = Read<UITransform>(MenuContainerField, controller);
            _headerText = Read<UITextMesh>(HeaderTextField, controller);

            _bugOrFeedback = Read<BugReporterBugOrFeedbackWindow>(BugOrFeedbackField, controller);
            _describeIssue = Read<BugReporterDescribeIssueWindow>(DescribeIssueField, controller);
            _compose = Read<BugReporterComposeIssueWindow>(ComposeIssueField, controller);
            _listSearch = Read<BugReporterListSearchResultWindow>(ListSearchField, controller);
            _thankYou = Read<BugReporterThankYouWindow>(ThankYouField, controller);
            _error = Read<BugReporterErrorWindow>(ErrorField, controller);
            _loading = Read<BugReporterLoadingWindow>(LoadingField, controller);
            _feedback = Read<BugReporterFeedbackWindow>(FeedbackField, controller);

            _bofDescription = Field<UITextMesh>(_bugOrFeedback, "_descriptionText");
            _bofDisclaimer = Field<UITextMesh>(_bugOrFeedback, "_disclaimerText");
            _bofBugButton = Field<UIButton>(_bugOrFeedback, "_bugButton");
            _bofBugExample = Field<UITextMesh>(_bugOrFeedback, "_bugExample");
            _bofFeedbackButton = Field<UIButton>(_bugOrFeedback, "_feedbackButton");
            _bofFeedbackExample = Field<UITextMesh>(_bugOrFeedback, "_feedbackExample");

            _diDescription = Field<UITextMesh>(_describeIssue, "_descriptionText");
            _diInput = Field<UITextMeshInputField>(_describeIssue, "_inputField");
            _diEmpty = Field<UITextMesh>(_describeIssue, "_emptyInputFieldText");
            _diButton = Field<UIButton>(_describeIssue, "_button");

            _cSeverity = Field<UISlider>(_compose, "SeveritySlider");
            _cWhyCritical = Field<UITextMeshInputField>(_compose, "WhyIsItCriticalField");
            _cOccurrence = Field<UISlider>(_compose, "OccurenceSlider");
            _cNotSure = Field<UIToggle>(_compose, "NotSureToggle");
            _cSubject = Field<UITextMeshInputField>(_compose, "SubjectField");
            _cActual = Field<UITextMeshInputField>(_compose, "ActualResultField");
            _cExpected = Field<UITextMeshInputField>(_compose, "ExpectedResultField");
            _cSubmit = Field<UIButton>(_compose, "SubmitButton");
            _cReportStrength = Field<UITextMesh>(_compose, "ReportStrengthText");

            _lsDescription = Field<UITextMesh>(_listSearch, "_descriptionText");
            _lsEntryContainer = Field<Transform>(_listSearch, "_entryContainer");
            _lsNewIssueButton = Field<UIButton>(_listSearch, "_newIssueButton");
            _lsNewIssueToggle = Field<UIToggle>(_listSearch, "_newIssueToggle");

            _tyDescription = Field<UITextMesh>(_thankYou, "_descriptionText");
            _tyButton = Field<UIButton>(_thankYou, "_button");

            _errText = Field<UITextMesh>(_error, "_text");
            _errButton = Field<UIButton>(_error, "_button");

            _fbDescription = Field<UITextMesh>(_feedback, "_descriptionText");
            _fbDiscordButton = Field<UIButton>(_feedback, "_discordButton");
            _fbDiscordDescription = Field<UITextMesh>(_feedback, "_discordDescription");
        }

        /// <summary>The reporter is open. The screen's readiness gate also requires a shown window, so
        /// the pre-first-window frame declares nothing.</summary>
        public bool IsPresent()
        {
            return _menuContainer != null && _menuContainer.Active;
        }

        public BugReportWindow ActiveWindow
        {
            get
            {
                if (IsShown(_bugOrFeedback))
                {
                    return BugReportWindow.BugOrFeedback;
                }

                if (IsShown(_describeIssue))
                {
                    return BugReportWindow.DescribeIssue;
                }

                if (IsShown(_compose))
                {
                    return BugReportWindow.ComposeIssue;
                }

                if (IsShown(_listSearch))
                {
                    return BugReportWindow.ListSearchResult;
                }

                if (IsShown(_thankYou))
                {
                    return BugReportWindow.ThankYou;
                }

                if (IsShown(_error))
                {
                    return BugReportWindow.Error;
                }

                if (IsShown(_loading))
                {
                    return BugReportWindow.Loading;
                }

                if (IsShown(_feedback))
                {
                    return BugReportWindow.Feedback;
                }

                return BugReportWindow.None;
            }
        }

        public string Header
        {
            get { return Clean(_headerText); }
        }

        // ---- Bug or feedback ----

        public IList<string> BugOrFeedbackLines
        {
            get { return Lines(Text(_bofDescription), VisibleText(_bofDisclaimer)); }
        }

        public BugReportButton BugButton
        {
            get { return ButtonOf(_bofBugButton, Lines(VisibleText(_bofBugExample))); }
        }

        public BugReportButton FeedbackButton
        {
            get { return ButtonOf(_bofFeedbackButton, Lines(VisibleText(_bofFeedbackExample))); }
        }

        // ---- Describe issue ----

        public IList<string> DescribeIssueLines
        {
            get { return Lines(Text(_diDescription)); }
        }

        public BugReportField DescribeField
        {
            // This field's TMP placeholder is a dev string ("Le empty text..."); the window draws its
            // real prompt in a separate mesh, so that is the label.
            get { return FieldOf(_diInput, Clean(_diEmpty)); }
        }

        public BugReportButton DescribeConfirmButton
        {
            get { return ButtonOf(_diButton, null); }
        }

        // ---- Compose issue ----

        public BugReportSlider SeveritySlider
        {
            get { return SliderOf(_cSeverity); }
        }

        public BugReportField WhyCriticalField
        {
            get { return FieldOf(_cWhyCritical); }
        }

        public BugReportSlider OccurrenceSlider
        {
            get { return SliderOf(_cOccurrence); }
        }

        public BugReportToggle NotSureToggle
        {
            get { return ToggleOf(_cNotSure); }
        }

        public BugReportField SubjectField
        {
            get { return FieldOf(_cSubject); }
        }

        public BugReportField ActualField
        {
            get { return FieldOf(_cActual); }
        }

        public BugReportField ExpectedField
        {
            get { return FieldOf(_cExpected); }
        }

        public string ReportStrengthText
        {
            get { return Clean(_cReportStrength); }
        }

        public BugReportButton SubmitButton
        {
            get { return ButtonOf(_cSubmit, null); }
        }

        // ---- List search result ----

        public IList<string> ListSearchLines
        {
            get { return Lines(Text(_lsDescription)); }
        }

        public IList<BugReportIssue> Issues
        {
            get
            {
                SimpleGameObjectPool<BugReporterSearchResultEntry> pool =
                    Read<SimpleGameObjectPool<BugReporterSearchResultEntry>>(EntryPoolField, _listSearch);
                List<BugReporterSearchResultEntry> spawned = pool != null ? pool.ActiveEntries : null;
                int count = spawned != null ? spawned.Count : 0;
                object firstIssue = count > 0 && EntryIssueField != null && spawned[0] != null
                    ? EntryIssueField.GetValue(spawned[0])
                    : null;
                if (count != _entrySnapshotCount || !ReferenceEquals(firstIssue, _entrySnapshotFirstIssue))
                {
                    _entrySnapshotCount = count;
                    _entrySnapshotFirstIssue = firstIssue;
                    _issues = ScanIssues();
                }

                return _issues;
            }
        }

        public BugReportToggle ReportAnywayToggle
        {
            get { return ToggleOf(_lsNewIssueToggle); }
        }

        public BugReportButton NewIssueButton
        {
            get { return ButtonOf(_lsNewIssueButton, null); }
        }

        // ---- Thank you ----

        public IList<string> ThankYouLines
        {
            get { return Lines(Text(_tyDescription)); }
        }

        public BugReportButton CloseButton
        {
            get { return ButtonOf(_tyButton, null); }
        }

        // ---- Error ----

        public IList<string> ErrorLines
        {
            get { return Lines(Text(_errText)); }
        }

        public BugReportButton ErrorOkButton
        {
            get { return ButtonOf(_errButton, null); }
        }

        // ---- Feedback ----

        public IList<string> FeedbackLines
        {
            get { return Lines(Text(_fbDescription), Text(_fbDiscordDescription)); }
        }

        public BugReportButton DiscordButton
        {
            get { return ButtonOf(_fbDiscordButton, null); }
        }

        // ---- Reading helpers ----

        private List<BugReportIssue> ScanIssues()
        {
            List<BugReportIssue> issues = new List<BugReportIssue>();
            if (_lsEntryContainer == null)
            {
                return issues;
            }

            // Walked only when the window's spawned rows moved (a new result set), never every frame.
            BugReporterSearchResultEntry[] entries = _lsEntryContainer.GetComponentsInChildren<BugReporterSearchResultEntry>(false);
            for (int i = 0; i < entries.Length; i++)
            {
                BugReporterSearchResultEntry entry = entries[i];
                if (entry == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                UITextMesh subject = Field<UITextMesh>(entry, "_subjectText");
                UITextMesh statusText = Field<UITextMesh>(entry, "_statusText");
                UIImage statusBackground = Field<UIImage>(entry, "_statusBackground");
                UITextMesh upVotes = Field<UITextMesh>(entry, "_upVotesText");
                UIButton button = Field<UIButton>(entry, "_button");

                bool hasState = statusBackground != null && statusBackground.gameObject.activeSelf;
                UIButton clickButton = button;
                issues.Add(new BugReportIssue
                {
                    Title = Clean(subject),
                    State = hasState ? Clean(statusText) : null,
                    Upvotes = Clean(upVotes),
                    Enabled = clickButton != null && clickButton.Interactable,
                    Subject = clickButton,
                    Click = clickButton == null ? (Action)null : () => NativeSelectionUtility.Click(clickButton),
                });
            }

            return issues;
        }

        // One record per widget for the life of the adapter, its snapshot fields written afresh on
        // every read. The reporter's widgets are resolved once in the constructor and never change,
        // so the tables are bounded by the window's controls; a build sees what it saw before, with
        // one object and one tooltip instead of a new pair per frame. The tooltip matters most: it
        // remembers whether the game's details are a long dossier for the life of the instance, and a
        // fresh one per build asked the game again every frame (AGENTS.md, Performance).
        private readonly Dictionary<Component, Tooltip> _tooltips = new Dictionary<Component, Tooltip>();
        private readonly Dictionary<UIButton, BugReportButton> _buttonRecords = new Dictionary<UIButton, BugReportButton>();
        private readonly Dictionary<UITextMeshInputField, BugReportField> _fieldRecords = new Dictionary<UITextMeshInputField, BugReportField>();
        private readonly Dictionary<UISlider, BugReportSlider> _sliderRecords = new Dictionary<UISlider, BugReportSlider>();
        private readonly Dictionary<UIToggle, BugReportToggle> _toggleRecords = new Dictionary<UIToggle, BugReportToggle>();

        private Tooltip TooltipFor(Component component)
        {
            if (component == null)
            {
                return null;
            }

            Tooltip tooltip;
            if (!_tooltips.TryGetValue(component, out tooltip))
            {
                tooltip = Tooltip.ForComponent(component, GlobalLocalizationVariables.LocalizationHandler);
                _tooltips[component] = tooltip;
            }

            return tooltip;
        }

        private BugReportButton ButtonOf(UIButton button, IList<string> details)
        {
            if (button == null)
            {
                return null;
            }

            BugReportButton record;
            if (!_buttonRecords.TryGetValue(button, out record))
            {
                UIButton captured = button;
                record = new BugReportButton
                {
                    Enabled = () => captured.Interactable,
                    Subject = captured,
                    Click = () => NativeSelectionUtility.Click(captured),
                    Tooltip = TooltipFor(captured),
                };
                _buttonRecords[button] = record;
            }

            record.Label = MenuButtonTextUtility.GetStandardButtonLabel(button);
            record.Visible = MenuButtonAdapterBase.IsButtonVisible(button);
            record.Details = details;
            return record;
        }

        private BugReportField FieldOf(UITextMeshInputField field, string labelOverride = null)
        {
            if (field == null)
            {
                return null;
            }

            BugReportField record;
            if (!_fieldRecords.TryGetValue(field, out record))
            {
                UITextMeshInputField captured = field;
                record = new BugReportField
                {
                    Value = () => captured.InputFieldValue ?? string.Empty,
                    Field = captured,
                    Tooltip = TooltipFor(captured),
                };
                _fieldRecords[field] = record;
            }

            record.Label = string.IsNullOrWhiteSpace(labelOverride) ? Placeholder(field) : labelOverride;
            record.Enabled = field.Interactable;
            record.Visible = field.Active;
            return record;
        }

        private BugReportSlider SliderOf(UISlider slider)
        {
            if (slider == null)
            {
                return null;
            }

            BugReportSlider record;
            if (!_sliderRecords.TryGetValue(slider, out record))
            {
                UISlider captured = slider;
                record = new BugReportSlider
                {
                    // The game composes this label into the slider's own text mesh; read it through
                    // GetEffectiveText, whose _stringBuilder path survives the hot-reload desync that
                    // reverts UITextMesh.Text to the prefab placeholder ("Le Severity...").
                    Label = () => SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(SliderTextMesh(captured))),
                    Subject = captured,
                    Adjust = (sign, large) => Adjust(captured, sign, large),
                };
                _sliderRecords[slider] = record;
            }

            record.Enabled = slider.Interactable;
            record.Visible = ((Component)slider).gameObject.activeInHierarchy;
            return record;
        }

        private BugReportToggle ToggleOf(UIToggle toggle)
        {
            if (toggle == null)
            {
                return null;
            }

            BugReportToggle record;
            if (!_toggleRecords.TryGetValue(toggle, out record))
            {
                UIToggle captured = toggle;
                record = new BugReportToggle
                {
                    State = () => captured.ToggleValue,
                    Subject = captured,
                    Toggle = () => Flip(captured),
                    Tooltip = TooltipFor(captured.GetTextMesh() as Component),
                };
                _toggleRecords[toggle] = record;
            }

            record.Label = SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(toggle.GetTextMesh()));
            record.Enabled = toggle.Interactable;
            record.Visible = toggle.Active;
            return record;
        }

        // Move the slider by one band (a fifth of its range), or two with the coarse step. The value
        // change notifies the game, which recomposes the label and re-gates Submit exactly as a mouse
        // drag would.
        private static void Adjust(UISlider slider, int sign, bool large)
        {
            if (slider == null || !slider.Interactable)
            {
                return;
            }

            float min = slider.SliderMinValue;
            float max = slider.SliderMaxValue;
            float band = (max - min) / SliderBands;
            if (band <= 0f)
            {
                return;
            }

            int current = Mathf.RoundToInt((slider.SliderValue - min) / band);
            int next = Mathf.Clamp(current + sign * (large ? 2 : 1), 0, SliderBands);
            slider.SetSliderValue(min + next * band, sendNotify: true);
        }

        private static UITextMesh SliderTextMesh(UISlider slider)
        {
            return slider != null && SliderTextMeshField != null ? SliderTextMeshField.GetValue(slider) as UITextMesh : null;
        }

        private static void Flip(UIToggle toggle)
        {
            if (toggle != null && toggle.Active && toggle.Interactable)
            {
                // The game's own value setter raises onValueChanged, the same event a click does, so
                // the list window refreshes and re-gates its new-issue button.
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        private static bool IsShown(MonoBehaviour window)
        {
            return window != null && window.gameObject.activeSelf;
        }

        // The field's prompt is drawn as its TMP placeholder. Read through the UITextMesh on the
        // placeholder object where there is one (GetEffectiveText's _stringBuilder path survives the
        // hot-reload desync that reverts TMP.text to the prefab dev string "Le Subject..."); the raw
        // TMP text otherwise.
        private static string Placeholder(UITextMeshInputField field)
        {
            try
            {
                if (field == null)
                {
                    return string.Empty;
                }

                TMPro.TMP_InputField input = field.GetInputField();
                TMPro.TMP_Text placeholder = input != null ? input.placeholder as TMPro.TMP_Text : null;
                if (placeholder == null)
                {
                    return string.Empty;
                }

                UITextMesh mesh = placeholder.GetComponent<UITextMesh>();
                string text = mesh != null ? UITextMeshTextUtility.GetEffectiveText(mesh) : placeholder.text;
                return SpokenLines.Clean(text);
            }
            catch (Exception error)
            {
                // A destroyed input field throws rather than answering. Said once, because this runs
                // on every build of the compose window.
                if (!_placeholderFailureLogged)
                {
                    _placeholderFailureLogged = true;
                    SocAccessMod.Instance?.LogWarning("Bug reporter: reading a field's prompt threw: " + error);
                }

                return string.Empty;
            }
        }

        private static bool _placeholderFailureLogged;

        private static string Text(UITextMesh mesh)
        {
            return mesh == null ? null : UITextMeshTextUtility.GetEffectiveText(mesh);
        }

        // The text of a mesh the game only draws in some states (the custom-map disclaimer, the
        // per-button examples): null when the object is hidden, so it never adds an empty line.
        private static string VisibleText(UITextMesh mesh)
        {
            return mesh != null && mesh.gameObject.activeInHierarchy ? UITextMeshTextUtility.GetEffectiveText(mesh) : null;
        }

        private static string Clean(UITextMesh mesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(mesh));
        }

        private static IList<string> Lines(params string[] raw)
        {
            return SpokenLines.Of(raw);
        }

        private static T Read<T>(FieldInfo field, object owner) where T : class
        {
            return owner == null || field == null ? null : field.GetValue(owner) as T;
        }

        private static T Field<T>(object owner, string name) where T : class
        {
            if (owner == null)
            {
                return null;
            }

            FieldInfo field = AccessTools.Field(owner.GetType(), name);
            return field == null ? null : field.GetValue(owner) as T;
        }
    }
}
