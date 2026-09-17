using System;
using System.Collections.Generic;
using System.Text;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// What one build of a <see cref="GraphSheet"/> declared, kept until something the game owns says
    /// it is a different table.
    ///
    /// A table's SHAPE - how many rows, how many cells each has, which column each cell is, what each
    /// is a control of and which widget it stands on - is settled when the game binds its entries, and
    /// the game binds them when the panel is refreshed, not sixty times a second. Between two binds
    /// the screen was reading the same widgets and minting the same keys, closures, vtables and edges
    /// every frame for a table that had not moved (the conquest map list is 57 rows of seven columns).
    /// So the sheet keeps what it declared and hands it back until a key changes.
    ///
    /// Nothing is skipped but the MINTING: every declaration is re-gated against the widget the game
    /// is drawing this frame, and every word a cell says is still read when the cell is read, so a
    /// figure that moves reads as the new one on the same row. What a replay cannot notice is a label
    /// some screen composed EAGERLY into a vtable - a row name baked into a vertical edge, a figure
    /// formatted into a primary - so a screen that composes one puts it, or what it was composed from,
    /// into its keys.
    ///
    /// A screen owns one as a field. It is mod-owned state keyed on game-owned identities, so a new
    /// menu instance answers with new ones and the block is minted again: there is no reset hook and
    /// none is needed.
    /// </summary>
    public sealed class SheetSnapshot
    {
        private GraphSheet.Block _kept;
        private object[] _keptKeys;
        private GraphSheet.Block _recording;
        private object[] _recordingKeys;

        /// <summary>
        /// Hand the kept block back to <paramref name="sheet"/> when <paramref name="keys"/> are the
        /// ones it was recorded under, and say so. The caller then does its own <c>Finish</c> and
        /// landing and is done for the frame; false means the table must be read off the game again.
        ///
        /// Keys are compared by identity for objects, by ordinal text for strings and by value for
        /// boxed numbers and flags - so a list an adapter memoises on a game-owned signature, a title
        /// the game drew, a tab index and a fold of per-frame visibility flags all key correctly.
        /// </summary>
        public bool TryReplay(GraphSheet sheet, params object[] keys)
        {
            if (sheet == null || _kept == null || !Matches(_keptKeys, keys))
            {
                return false;
            }

            sheet.Replay(_kept);
            return true;
        }

        /// <summary>Start recording what the sheet declares, under these keys. Before the first
        /// region.</summary>
        public void Record(GraphSheet sheet, params object[] keys)
        {
            _recording = new GraphSheet.Block();
            _recordingKeys = keys;
            if (sheet != null)
            {
                sheet.Records(_recording);
            }
        }

        /// <summary>Keep what was recorded, after <c>Finish</c> returned. Only a build that got all
        /// the way here is kept: one that threw part way through declared part of a table, and
        /// replaying that would hide the failure behind a shorter table.</summary>
        public void Keep()
        {
            if (_recording == null)
            {
                return;
            }

            _kept = _recording;
            _keptKeys = _recordingKeys;
            _recording = null;
            _recordingKeys = null;
        }

        /// <summary>Several strings as one key - the captions a build baked into its edges, the labels
        /// it composed into its rows. Joined rather than hashed: a hash that collided would hand back
        /// a table whose words are someone else's.</summary>
        public static string Words(IReadOnlyList<string> parts)
        {
            if (parts == null)
            {
                return null;
            }

            StringBuilder text = new StringBuilder();
            for (int i = 0; i < parts.Count; i++)
            {
                if (i > 0)
                {
                    text.Append('');
                }

                text.Append(parts[i]);
            }

            return text.ToString();
        }

        private static bool Matches(object[] kept, object[] keys)
        {
            if (kept == null || keys == null || kept.Length != keys.Length)
            {
                return false;
            }

            for (int i = 0; i < kept.Length; i++)
            {
                if (!Same(kept[i], keys[i]))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool Same(object kept, object key)
        {
            if (ReferenceEquals(kept, key))
            {
                return true;
            }

            if (kept == null || key == null)
            {
                return false;
            }

            string text = kept as string;
            if (text != null)
            {
                string other = key as string;
                return other != null && string.Equals(text, other, StringComparison.Ordinal);
            }

            // A boxed number, flag or enum: two boxes of the same value are two objects.
            return kept is ValueType && kept.Equals(key);
        }
    }
}
