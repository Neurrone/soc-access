using System;
using System.Collections;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The map and battle tiles' USAGE HINTS: what the game's own tooltip said a click here would do,
    /// said as the mod's keyboard sentence instead - "Enter visits", "Backslash attacks".
    ///
    /// The game draws those as instruction rows in the tile's tooltip; the adapter strips the rows
    /// (they name a mouse gesture nobody here is making) and reports the kind. This turns the kind
    /// into the hint on the key that performs it - the primary row on left click, the secondary on
    /// right click - so the wording follows the player's bindings like every other hint.
    ///
    /// ONE HINT PER KEY, and WHICH sentence it carries is decided when the buffer is READ, because
    /// the vtable is built once per frame while the cursor moves from tile to tile inside it. The
    /// tooltip is therefore asked once per hint per buffer read and must be the screen's CACHED tile
    /// tooltip, never a fresh adapter read.
    /// </summary>
    public static class TileInstructionHints
    {
        /// <summary>What each kind is called on the key that performs it.</summary>
        private static readonly Dictionary<TileInstruction, ModString> Templates =
            new Dictionary<TileInstruction, ModString>
            {
                { TileInstruction.Select, ModStrings.Screens.TileSelectHint },
                { TileInstruction.Visit, ModStrings.Screens.TileVisitHint },
                { TileInstruction.Trade, ModStrings.Screens.TileTradeHint },
                { TileInstruction.Repair, ModStrings.Screens.TileRepairHint },
                { TileInstruction.Interact, ModStrings.Screens.TileInteractHint },
                { TileInstruction.Pillage, ModStrings.Screens.TilePillageHint },
                { TileInstruction.Attack, ModStrings.Screens.TileAttackHint },
                { TileInstruction.Pickup, ModStrings.Screens.TilePickupHint },
                { TileInstruction.Claim, ModStrings.Screens.TileClaimHint },
                { TileInstruction.Teleport, ModStrings.Screens.TileTeleportHint },
                { TileInstruction.Move, ModStrings.Screens.TileMoveHint },
            };

        public static void Add(NodeVtable vtable, Func<Tooltip> tooltip, Func<bool> when = null)
        {
            if (vtable == null || tooltip == null)
            {
                return;
            }

            vtable.Hints = new Instructions(tooltip, when, vtable.Hints);
        }

        /// <summary>
        /// A tile's hint list: whatever was declared before it, then the left click's sentence and
        /// the right click's, then whatever is declared after.
        ///
        /// A <see cref="NodeHint"/>'s template is fixed when the hint is made, so saying what the
        /// LIVE tooltip names used to mean declaring all eleven kinds on both keys and gating twenty
        /// of the twenty-two off - twenty-two hint objects and twenty-two closures appended to the
        /// tile's vtable on every build of the map and of the battle board. Reading the same
        /// declaration the other way round costs nothing per frame and says the same two lines per
        /// read: the list holds two hints, and which sentence each carries is chosen the moment it
        /// is asked for, off the same tooltip the gate read.
        /// </summary>
        private sealed class Instructions : IList<NodeHint>
        {
            /// <summary>A hint that says nothing, for a key the game named no action on: its gate
            /// refuses, which is how <see cref="NodeHints.Lines"/> already skips a hint.</summary>
            private static readonly NodeHint Silent = new NodeHint(
                ModStrings.Screens.TileSelectHint,
                AccessibilityActions.UiLeftClick.Key,
                0,
                () => false);

            private readonly Func<Tooltip> _tooltip;

            private readonly Func<bool> _when;

            private readonly IList<NodeHint> _before;

            private List<NodeHint> _after;

            public Instructions(Func<Tooltip> tooltip, Func<bool> when, IList<NodeHint> before)
            {
                _tooltip = tooltip;
                _when = when;
                _before = before != null && before.Count > 0 ? before : null;
            }

            public int Count
            {
                get { return Before + 2 + (_after == null ? 0 : _after.Count); }
            }

            public bool IsReadOnly
            {
                get { return false; }
            }

            public NodeHint this[int index]
            {
                get
                {
                    int at = index - Before;
                    if (at < 0)
                    {
                        return _before[index];
                    }

                    return at < 2 ? Hint(at == 0) : _after[at - 2];
                }

                set { throw new NotSupportedException("a tile's instruction hints are read off its tooltip"); }
            }

            public void Add(NodeHint hint)
            {
                if (_after == null)
                {
                    _after = new List<NodeHint>(1);
                }

                _after.Add(hint);
            }

            public IEnumerator<NodeHint> GetEnumerator()
            {
                for (int i = 0; i < Count; i++)
                {
                    yield return this[i];
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                return GetEnumerator();
            }

            public int IndexOf(NodeHint hint)
            {
                for (int i = 0; i < Count; i++)
                {
                    if (ReferenceEquals(this[i], hint))
                    {
                        return i;
                    }
                }

                return -1;
            }

            public bool Contains(NodeHint hint)
            {
                return IndexOf(hint) >= 0;
            }

            public void CopyTo(NodeHint[] array, int at)
            {
                for (int i = 0; i < Count; i++)
                {
                    array[at + i] = this[i];
                }
            }

            public void Insert(int index, NodeHint hint)
            {
                throw new NotSupportedException("a tile's instruction hints keep their place");
            }

            public void RemoveAt(int index)
            {
                throw new NotSupportedException("a tile's instruction hints keep their place");
            }

            public bool Remove(NodeHint hint)
            {
                throw new NotSupportedException("a tile's instruction hints keep their place");
            }

            public void Clear()
            {
                throw new NotSupportedException("a tile's instruction hints keep their place");
            }

            private int Before
            {
                get { return _before == null ? 0 : _before.Count; }
            }

            /// <summary>The sentence one of the two keys says right now, or the silent hint where the
            /// gate refuses, the tooltip is gone, or the game named no action on that key. Guarded
            /// because the read used to sit inside <see cref="NodeHints.Lines"/>'s own guard and a
            /// tooltip that throws must still cost one line and not the whole readout.</summary>
            private NodeHint Hint(bool primary)
            {
                try
                {
                    if (_when != null && !_when())
                    {
                        return Silent;
                    }

                    Tooltip it = _tooltip();
                    TileInstruction kind = it == null
                        ? TileInstruction.None
                        : (primary ? it.PrimaryInstruction : it.SecondaryInstruction);

                    ModString template;
                    if (!Templates.TryGetValue(kind, out template))
                    {
                        return Silent;
                    }

                    return new NodeHint(
                        template,
                        primary ? AccessibilityActions.UiLeftClick.Key : AccessibilityActions.UiRightClick.Key);
                }
                catch (Exception e)
                {
                    SocAccessMod.Instance?.LogWarning("tile hints: reading the tile's instructions threw: " + e);
                    return Silent;
                }
            }
        }
    }
}
