using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Utils;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE ROWS OF ONE FORM, READ ONCE PER DRAW RATHER THAN ONCE PER FRAME.
    ///
    /// <see cref="MenuRows.Read(IMenuFactoryCollection, KeyBindingSource)"/> mints a record and a
    /// dozen closures per control and sorts them by sibling path; asked every frame for a Controls
    /// page of seventy rows it was the whole build cost (about 4 ms, 2026-09-11). The rows only
    /// change when the column is redrawn, and a redraw destroys the column's children and draws new
    /// ones, so the memo is keyed on what the column holds: its child count and the identity of its
    /// first and last child, all three read from the game each frame. A destroyed child is a
    /// different identity, and so is a new one, so a redraw to the same count still misses.
    ///
    /// The records themselves read the live widget every time they are asked, so a chip the game
    /// redraws inside a row, or a control it hides, needs no re-read.
    /// </summary>
    public sealed class MenuRowMemo
    {
        private readonly IMenuFactoryCollection _factory;
        private readonly MenuRowSettings _settings;
        private readonly Transform _column;

        private IReadOnlyList<MenuRow> _rows;
        private int _childCount = -1;
        private Transform _first;
        private Transform _last;

        /// <param name="column">The content column the factory draws into.</param>
        /// <param name="keyBindings">The rebindable-action context, or null for a form that draws
        /// none of its own.</param>
        public MenuRowMemo(IMenuFactoryCollection factory, Transform column, KeyBindingSource keyBindings)
            : this(factory, column, new MenuRowSettings { KeyBindings = keyBindings })
        {
        }

        /// <param name="column">The content column the factory draws into.</param>
        /// <param name="settings">How this form's rows are named and read.</param>
        public MenuRowMemo(IMenuFactoryCollection factory, Transform column, MenuRowSettings settings)
        {
            _factory = factory;
            _column = column;
            _settings = settings;
        }

        public IReadOnlyList<MenuRow> Rows
        {
            get
            {
                if (_column == null)
                {
                    return _rows ?? (_rows = MenuRows.Read(_factory, _settings));
                }

                int count = _column.childCount;
                Transform first = count > 0 ? _column.GetChild(0) : null;
                Transform last = count > 0 ? _column.GetChild(count - 1) : null;
                if (_rows == null || count != _childCount || !ReferenceEquals(first, _first) || !ReferenceEquals(last, _last))
                {
                    _rows = MenuRows.Read(_factory, _settings);
                    _childCount = count;
                    _first = first;
                    _last = last;
                }

                return _rows;
            }
        }
    }
}
