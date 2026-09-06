using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Every modal the mod.io browser behind the community maps pages puts up, made navigable as a
    /// graph in the three-part dialog contract its family's representative
    /// (<see cref="MessageDialogScreen"/>) established: one stop, the heading as a line of its own and
    /// as the screen name, focus starting on the text the modal draws under it, then the field where
    /// the modal has one, then the buttons in drawn order.
    ///
    /// Measured 2026-09-06 at 1280x800 on the authentication modal (`Authentication Popup` &gt;
    /// `Main Panel`): the heading "Authentication" at y 315, its paragraph at y 403, and the buttons
    /// Back (x 373), Connect with Steam (x 509) and Connect with email (x 721). The buttons are read
    /// by their drawn left edges every build.
    ///
    /// ESCAPE is claimed. The game's own host of the browser
    /// (`SongsOfConquest/Client/Menu/Mods/LavapotionModIOBrowserUtilityBehaviour.cs`) registers no
    /// input callback at all, and mod.io's cancel (`ModIOBrowser/InputReceiver.cs` line 10, calling
    /// `Navigating.Cancel()`) is reached only from its own Input System actions, which this build does
    /// not bind. So the key would do nothing, and the screen claims it and runs mod.io's own
    /// <c>Navigating.Cancel</c> through the adapter - the native cancel path, which knows what each of
    /// the modal's states closes to (the five-digit panel's own Cancel button, the context menu's
    /// Close) far better than picking a drawn button would.
    ///
    /// TWO VARIANTS ARE DECLARED BUT UNREACHED here, both on the e-mail login the account never
    /// needs:
    ///
    /// A plain text box (`AuthenticationPanels.AuthenticationPanelEmailField`, a bare
    /// <c>TMP_InputField</c>) is an edit field driven by <see cref="GameTextEditor"/>. The panel wires
    /// no <c>onSubmit</c> or <c>onEndEdit</c> on that field (`AuthenticationPanels.OpenPanel_Email`,
    /// decompiled lines 640 to 685, sets only its explicit navigation), so Enter ends the edit and
    /// does nothing else - the plain Endless Space 2 contract, without the dialog exception.
    ///
    /// The five-digit code is ONE node, not one per box. `KeyInput5Digits` (decompiled
    /// `modio.UI/ModIOBrowser/Implementation/KeyInput5Digits.cs`) reads the keyboard itself in its own
    /// <c>Update</c> while the panel is drawn, keeps ONE string and ONE index, and
    /// `KeyInput5DigitsUi.Open` clears the event system's selection outright: the five boxes are five
    /// <c>TMP_Text</c>s it renders that string into, with no focus and nothing to select, so there is
    /// no per-box control for a cursor to sit on and no way to move between them but typing and
    /// Backspace. While that panel is up the screen also turns type-ahead off, because the letters and
    /// digits the player types are the code the game is reading.
    /// </summary>
    public sealed class CommunityMapsModalScreen : GraphScreen
    {
        private const string ModalStop = "community-maps-modal";

        private readonly CommunityMapsModalAdapter _adapter;
        private readonly CommunityMapsModalState _state;
        private readonly GameTextEditor _editor = new GameTextEditor();

        // A subject of its own for each node the modal gives no component for, kept across rebuilds:
        // the reconciler seats the cursor by SUBJECT before it looks at the structural key, so two
        // nodes sharing one collapse onto whichever was declared first.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        private string _lastCode;

        public CommunityMapsModalScreen(CommunityMapsModalAdapter adapter)
        {
            _adapter = adapter;
            _state = adapter != null ? adapter.State : CommunityMapsModalState.None;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommunityMapsModalAdapter adapter = CommunityMapsModalAdapter.TryCreate();
            return adapter != null && adapter.IsPresent() ? new CommunityMapsModalScreen(adapter) : null;
        }

        public override string Key
        {
            get { return "community-maps-modal"; }
        }

        /// <summary>The modal's own heading, spoken once on arrival. Null where it draws none.</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>The state the modal was in when this screen was built. The detector compares it
        /// with a freshly read one to decide whether the modal became a DIFFERENT modal - a different
        /// panel object, which this screen's adapter cannot follow - so it is a snapshot, not a live
        /// reading.</summary>
        public CommunityMapsModalState State
        {
            get { return _state; }
        }

        /// <summary>Escape: the key does nothing here on its own, so the screen takes it and runs
        /// mod.io's own cancel.</summary>
        public override bool ConsumesBack
        {
            get { return IsPresent(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.Cancel();
        }

        /// <summary>While the keyboard is on its way to the modal's text box, what the player types
        /// next is meant for that box.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Off while the code panel is up: the letters and digits typed there are the code,
        /// read by the game's own per-frame key scan, and a search must not eat them.</summary>
        public override bool AllowsTypeahead
        {
            get { return _state != CommunityMapsModalState.InputFiveDigits; }
        }

        /// <summary>
        /// The modal's contents changed without becoming a different modal. Nothing to do: the graph
        /// is declared afresh on every operation, so the next build already reads what is there. The
        /// detector still calls this, so it stays.
        /// </summary>
        public void Refresh()
        {
        }

        public override void Update()
        {
            base.Update();

            // After the navigator, so a word about the edit follows the activation's own readout.
            _editor.Update(IsPresent());
            AnnounceCodeTyped();
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
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(ModalStop);
            ControlId start = null;

            IReadOnlyList<CommunityMapsModalAdapter.TextItem> texts = _adapter.GetTexts();
            for (int i = 0; i < texts.Count; i++)
            {
                CommunityMapsModalAdapter.TextItem text = texts[i];
                if (string.IsNullOrWhiteSpace(text.Text))
                {
                    continue;
                }

                // The first line the modal draws is its heading, and is the screen name too; the rest
                // is what the modal has to say, which is where focus starts.
                string key = "modal:text/" + text.Index;
                ControlId id = ControlId.For(Marker(key), key);
                builder.AddItem(new SyntheticNode(id, GraphNodes.Text(() => text.Text)));
                if (i > 0 && start == null)
                {
                    start = id;
                }
            }

            IReadOnlyList<CommunityMapsModalAdapter.InputItem> inputs = _adapter.GetInputs();
            for (int i = 0; i < inputs.Count; i++)
            {
                CommunityMapsModalAdapter.InputItem input = inputs[i];
                if (input.Field == null)
                {
                    continue;
                }

                string key = "modal:input/" + input.Index;
                ControlId id = ControlId.For(input.Field, key);
                builder.AddItem(new DrawnNode(id, EditField(input), input.Field));
                if (start == null)
                {
                    start = id;
                }
            }

            IReadOnlyList<CommunityMapsModalAdapter.FiveDigitInputItem> codes = _adapter.GetFiveDigitInputs();
            for (int i = 0; i < codes.Count; i++)
            {
                CommunityMapsModalAdapter.FiveDigitInputItem code = codes[i];
                if (code.Owner == null || !code.IsVisible)
                {
                    continue;
                }

                string key = "modal:code/" + code.Index;
                ControlId id = ControlId.For(code.Owner, key);
                builder.AddItem(new DrawnNode(id, CodeField(code), code.Owner));
                if (start == null)
                {
                    start = id;
                }
            }

            foreach (CommunityMapsModalAdapter.ActionItem action in DrawnActions())
            {
                string key = "modal:action/" + action.Index;
                ControlId id = ControlId.For(action.Button, key);
                CommunityMapsModalAdapter.ActionItem it = action;
                NodeVtable vtable = GraphNodes.Button(() => it.Label, () => it.Activate(), () => it.IsEnabled);
                vtable.OnFocusVisual = it.Focus;
                builder.AddItem(new DrawnNode(id, vtable, action.Button));
                if (start == null)
                {
                    start = id;
                }
            }

            if (_state == CommunityMapsModalState.DownloadQueue)
            {
                // The download queue draws no way out of its own, so the mod adds one.
                ControlId id = ControlId.For(Marker("modal:downloads-back"), "modal:downloads-back");
                builder.AddItem(new SyntheticNode(
                    id,
                    GraphNodes.Button(() => ModText.Get(ModStrings.Screens.Back), () => _adapter.Cancel())));
                if (start == null)
                {
                    start = id;
                }
            }

            if (start != null)
            {
                builder.SetStart(start);
            }
        }

        /// <summary>The modal's own text box. Activating it asks for the keyboard; Enter inside it
        /// ends the edit and nothing else, because the panel wires no submit on it.</summary>
        private NodeVtable EditField(CommunityMapsModalAdapter.InputItem input)
        {
            CommunityMapsModalAdapter.InputItem it = input;
            return GraphNodes.EditField(
                () => it.Label,
                () =>
                {
                    // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                    return it.Field == null || _editor.Editing ? null : it.Field.text;
                },
                () => _editor.Request(it.Field),
                () => it.Field != null && it.Field.interactable);
        }

        /// <summary>The five-digit code as one control. Activation is the panel's own Continue; the
        /// value is the whole code so far, and the character just typed is spoken by
        /// <see cref="AnnounceCodeTyped"/> rather than by a watch, so a keystroke does not re-read
        /// everything typed before it.</summary>
        private static NodeVtable CodeField(CommunityMapsModalAdapter.FiveDigitInputItem code)
        {
            CommunityMapsModalAdapter.FiveDigitInputItem it = code;
            NodeVtable vtable = GraphNodes.EditField(
                () => it.Label,
                () => it.Value,
                () => it.Activate(),
                () => it.IsVisible);
            vtable.OnFocusVisual = it.Focus;
            return vtable;
        }

        /// <summary>
        /// The character the player just typed into the code, or the deletion. The game reads those
        /// keys itself and says nothing, so the screen watches the string it keeps and speaks only
        /// what changed - which is what the widget this replaces did.
        /// </summary>
        private void AnnounceCodeTyped()
        {
            if (_state != CommunityMapsModalState.InputFiveDigits || !IsPresent())
            {
                _lastCode = null;
                return;
            }

            IReadOnlyList<CommunityMapsModalAdapter.FiveDigitInputItem> codes = _adapter.GetFiveDigitInputs();
            if (codes.Count == 0)
            {
                _lastCode = null;
                return;
            }

            string value = codes[0].Value ?? string.Empty;
            if (value == _lastCode)
            {
                return;
            }

            if (_lastCode != null)
            {
                int prefix = CommonPrefixLength(_lastCode, value);
                SpeechPipeline.Output(new SpeechRequest(
                    value.Length > prefix ? value[prefix].ToString() : ModText.Get(ModStrings.UI.Blank),
                    interrupt: true));
            }

            _lastCode = value;
        }

        /// <summary>The buttons the modal is drawing, leftmost first: the authentication modal draws
        /// Back, Connect with Steam and Connect with email in that order, and the reading order is the
        /// drawn one.</summary>
        private List<CommunityMapsModalAdapter.ActionItem> DrawnActions()
        {
            List<CommunityMapsModalAdapter.ActionItem> actions = new List<CommunityMapsModalAdapter.ActionItem>();
            IReadOnlyList<CommunityMapsModalAdapter.ActionItem> declared = _adapter.GetActions();
            for (int i = 0; i < declared.Count; i++)
            {
                if (declared[i] != null && declared[i].Button != null)
                {
                    actions.Add(declared[i]);
                }
            }

            SortByDrawnLeft(actions);
            return actions;
        }

        // Insertion sort by drawn left edge, leftmost first; stable, so two buttons at one x keep
        // declaration order.
        private static void SortByDrawnLeft(List<CommunityMapsModalAdapter.ActionItem> items)
        {
            List<float> lefts = new List<float>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Component button = items[i].Button;
                lefts.Add(button != null ? button.transform.position.x : 0f);
            }

            for (int i = 1; i < items.Count; i++)
            {
                CommunityMapsModalAdapter.ActionItem moving = items[i];
                float left = lefts[i];
                int j = i - 1;
                while (j >= 0 && lefts[j] > left)
                {
                    items[j + 1] = items[j];
                    lefts[j + 1] = lefts[j];
                    j--;
                }

                items[j + 1] = moving;
                lefts[j + 1] = left;
            }
        }

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers[key] = marker;
            }

            return marker;
        }

        private static int CommonPrefixLength(string a, string b)
        {
            int length = Math.Min(a.Length, b.Length);
            int index = 0;
            while (index < length && a[index] == b[index])
            {
                index++;
            }

            return index;
        }
    }
}
