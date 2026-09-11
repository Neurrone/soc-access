using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// ONE STACKED DIALOG, WHATEVER IT HOLDS.
    ///
    /// The dialogs the Mod options window opens - the announcement order of a group, the audio
    /// glossary, a cue's tuning, a taxonomy's custom categories, one category, one source's
    /// subcategories - are all the same thing: a titled popup drawn out of a copy of the options
    /// panel, holding nothing but rows. So they are all this screen, differing only in what they
    /// draw and what leaving without confirming means; the drawing lives with the settings it is
    /// about (<see cref="ModOptionsDialogs"/>) and the reading is
    /// <see cref="MenuFormNodes"/>, the same declarations the Options window is read with.
    ///
    /// Escape closes the TOP dialog only: each one is a screen of its own on the stack, and the one
    /// beneath it is left drawn but not interactable until it is uncovered again.
    /// </summary>
    public sealed class ModDialogScreen : GraphScreen
    {
        private readonly string _key;
        private readonly string _title;
        private readonly Action<ModDialogScreen> _draw;
        private readonly Func<bool> _cancel;
        private readonly ModDialog _dialog;
        private readonly MenuFormNodes _rows;

        private ModDialogScreen(string key, string title, ModDialog dialog, Action<ModDialogScreen> draw, Func<bool> cancel)
        {
            _key = key;
            _title = title;
            _dialog = dialog;
            _draw = draw;
            _cancel = cancel;
            _rows = new MenuFormNodes(key);
        }

        /// <summary>
        /// Draw a dialog over whatever is already there and put its screen on top of the stack.
        /// <paramref name="cancel"/> is what leaving without confirming does - the panel's own close
        /// button and Escape both run it - and null means leaving changes nothing, which is what the
        /// list dialogs want.
        /// </summary>
        public static ModDialogScreen Open(string key, string title, Action<ModDialogScreen> draw, Func<bool> cancel = null)
        {
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            if (manager == null || draw == null)
            {
                return null;
            }

            ModDialog dialog = ModDialog.Open(title, withTabs: false);
            if (dialog == null)
            {
                return null;
            }

            Screen owner = manager.Current;
            if (owner == null)
            {
                dialog.Close();
                return null;
            }

            ModDialogScreen screen = new ModDialogScreen(key, title, dialog, draw, cancel);
            dialog.DrawContent = index => draw(screen);
            dialog.OnClose = () => screen.Cancel();
            dialog.Select(0);
            // A CHILD of whatever it was opened over - the mod options window, or another dialog of
            // its own: the layers beneath stay where they are and get their cursors back.
            owner.PushChild(screen);
            return screen;
        }

        public override string Key
        {
            get { return _key; }
        }

        public override string ScreenName
        {
            get { return _title; }
        }

        public override bool IsActive()
        {
            return _dialog != null && _dialog.IsOpen;
        }

        /// <summary>A mod-owned surface, so the key that leaves it is the mod's and never reaches the
        /// game.</summary>
        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override bool Back()
        {
            return Cancel();
        }

        /// <summary>The editor behind the text rows. GraphScreen drives it: without that tick a
        /// request for the keyboard stayed pending forever and Enter on a name box did nothing
        /// (owner, 2026-09-07).</summary>
        public override GameTextEditor Editor
        {
            get { return _rows.Editor; }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();

            // The game took the window away underneath, so the screen goes with it. A child is not
            // polled; it asks for itself.
            if (!IsActive())
            {
                CloseSelf();
            }
        }

        /// <summary>The window this screen reads, for whatever is drawing into it.</summary>
        public ModDialog Dialog
        {
            get { return _dialog; }
        }

        /// <summary>Draw the rows again - after a move, an add, a delete or a rename.</summary>
        public void Redraw()
        {
            _dialog.Redraw();
        }

        /// <summary>Put the cursor on one of the rows just drawn, named the way
        /// <see cref="MenuRows"/> names them ("options-button-2"). Used after a redraw has replaced
        /// every control, when the row the player was working has MOVED.</summary>
        public void FocusRow(string rowId)
        {
            GraphNavigator navigator = Navigator;
            if (navigator != null && !string.IsNullOrEmpty(rowId))
            {
                navigator.FocusNode(ControlId.Structural(_key + ":row/" + rowId));
            }
        }

        /// <summary>Leave without confirming.</summary>
        public bool Cancel()
        {
            if (_cancel != null)
            {
                _cancel();
            }

            return Close();
        }

        /// <summary>Leave, keeping whatever was changed.</summary>
        public bool Close()
        {
            _dialog.Close();
            CloseSelf();
            return true;
        }

        /// <summary>Above the mod options window it is stacked over. Read only by the dev server: a
        /// child screen is not polled.</summary>
        public override int Layer
        {
            get { return 55; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(_key + "-rows");
            IReadOnlyList<MenuRow> rows = _dialog.Rows;
            _rows.BuildRows(builder, rows);

            builder.BeginStop(_key + "-buttons");
            _rows.AddWindowButton(
                builder,
                _dialog.CloseButton,
                () => ModText.Get(ModStrings.Screens.Close));
        }
    }
}
