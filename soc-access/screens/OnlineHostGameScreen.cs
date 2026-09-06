using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The "host an online game" popup, made navigable as a graph. Two stops: what the popup asks
    /// for, and the buttons under it.
    ///
    /// The popup draws a heading ("Host Game"), a sentence asking for a name, the name box, the
    /// Invite Only toggle, and Cancel and Host at the bottom. The heading is the screen's name, said
    /// on arrival; the sentence is a read-only row, because it is drawn and heads nothing.
    ///
    /// Escape is CLAIMED and presses the drawn Cancel button: <c>GameListMenu.ShowHostGame</c>
    /// registers <c>InputActions.UI.Cancel</c> only on its GAMEPAD branch and gives the keyboard
    /// <c>UI.Confirm</c> instead (decompiled, lines 546 to 555), so the key would otherwise do
    /// nothing here.
    ///
    /// NOTE, and it is the game's: on that same keyboard branch Enter is wired to the HOST button, so
    /// an Enter inside the name box creates the game. The mod does not add to that - the edit field
    /// follows the usual contract and the player's Enter is the game's own submit.
    /// </summary>
    public sealed class OnlineHostGameScreen : GraphScreen
    {
        private const string RowsStop = "host-game-rows";
        private const string ButtonsStop = "host-game-buttons";

        private readonly OnlineHostGameAdapter _adapter;
        private readonly GameTextEditor _editor = new GameTextEditor();
        private readonly object _descriptionKey = new object();
        private readonly object _cancelKey = new object();
        private readonly object _confirmKey = new object();

        public OnlineHostGameScreen(OnlineHostGameAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            OnlineHostGameAdapter adapter = OnlineHostGameAdapter.TryCreateActive();
            return adapter != null ? new OnlineHostGameScreen(adapter) : null;
        }

        public bool Matches(GameListMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override string Key
        {
            get { return "host-game"; }
        }

        /// <summary>The popup's own drawn heading ("Host Game").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return RowsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get
            {
                return _adapter != null
                    && _adapter.NegativeButton != null
                    && _adapter.NegativeButton.IsVisible();
            }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.Cancel();
        }

        /// <summary>While the keyboard is on its way to the name box, what the player types next is
        /// meant for that box and must not start a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        /// <summary>Kept for the detector, which calls it whenever the popup's content changes. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Update()
        {
            base.Update();
            _editor.Update(IsPresent());
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

            builder.BeginStop(RowsStop);
            if (_adapter.HasDescription)
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_descriptionKey, "host-game:description"),
                    GraphNodes.Text(() => _adapter.Description)));
            }

            AddNameField(builder);
            AddInviteOnly(builder);

            builder.BeginStop(ButtonsStop);
            AddButton(builder, "host-game:cancel", _cancelKey, _adapter.NegativeButton);
            AddButton(builder, "host-game:confirm", _confirmKey, _adapter.PositiveButton);
        }

        private void AddNameField(GraphBuilder builder)
        {
            IUITextMeshInputField field = _adapter.InputField;
            Component subject = field != null ? field.MonoTransform : null;
            if (subject == null || !_adapter.IsInputVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.EditField(
                () => _adapter.Title,
                () =>
                {
                    IUITextMeshInputField live = _adapter.InputField;
                    // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                    return live == null || _editor.Editing ? null : live.InputFieldValue;
                },
                () => _editor.Request(_adapter.InputField),
                _adapter.IsInputEnabled,
                _adapter.GetInputTooltip());
            GraphNodes.DoNotDrawTooltip(vtable);
            builder.AddItem(new DrawnNode(ControlId.For(subject, "host-game:name"), vtable, subject));
        }

        private void AddInviteOnly(GraphBuilder builder)
        {
            UIToggle toggle = _adapter.InviteOnlyToggle;
            if (toggle == null || !_adapter.IsInviteOnlyVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Checkbox(
                () => _adapter.InviteOnlyLabel,
                _adapter.IsInviteOnlyChecked,
                _adapter.ToggleInviteOnly,
                _adapter.IsInviteOnlyEnabled,
                _adapter.GetInviteOnlyTooltip());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(toggle.GetSelectable());
            builder.AddItem(new DrawnNode(ControlId.For(toggle, "host-game:invite-only"), vtable, toggle));
        }

        private void AddButton(GraphBuilder builder, string key, object marker, IMenuButtonAdapter button)
        {
            if (button == null || !button.IsVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                button.GetLabel,
                () => button.Activate(),
                button.IsEnabled,
                _adapter.GetButtonTooltip(button));
            Component subject = button.Button;
            if (subject != null)
            {
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(subject);
                builder.AddItem(new DrawnNode(ControlId.For(subject, key), vtable, subject));
                return;
            }

            builder.AddItem(new SyntheticNode(ControlId.For(marker, key), vtable));
        }
    }
}
