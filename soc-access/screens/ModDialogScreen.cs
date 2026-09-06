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

            ModDialogScreen screen = new ModDialogScreen(key, title, dialog, draw, cancel);
            dialog.DrawContent = index => draw(screen);
            dialog.OnClose = () => screen.Cancel();
            dialog.Select(0);
            manager.Push(screen, key + " dialog opened");
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

        public override bool IsPresent()
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

        public override bool OwnsGameField
        {
            get { return _rows.Editor.Editing || _rows.Editor.Pending; }
        }

        public override bool CapturesRawInput
        {
            get { return _rows.Editor.Pending; }
        }

        /// <summary>The editor behind the text rows is driven from here: without this tick a request
        /// for the keyboard stayed pending forever and Enter on a name box did nothing (owner,
        /// 2026-09-07).</summary>
        public override void Update()
        {
            base.Update();
            _rows.Editor.Update(IsPresent());
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _rows.Editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _rows.Editor.Abandon();
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
            ScreenManager manager = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            return manager != null && manager.Pop<ModDialogScreen>(_key + " dialog closed");
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
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
