using System;
using System.Text;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// Fluent message accumulator, ported from Factorio Access's MessageBuilder
    /// (scripts/speech.lua). The value it carries is the separation discipline:
    /// consecutive <see cref="Fragment"/>s are joined with a space, and
    /// <see cref="ListItem"/> boundaries are joined with a comma. That lets a chain of
    /// collaborating functions each append its piece without coordinating spacing.
    ///
    /// Every connective — the separators and the "N of M" and "x N" idioms — comes from
    /// <see cref="ModStrings"/>, so a translation can change them all at once (a Japanese
    /// table joins fragments with nothing and list items with "、").
    ///
    /// Single-use: <see cref="Build"/> throws if the builder is reused.
    /// </summary>
    public sealed class MessageBuilder
    {
        private enum State
        {
            // Nothing appended yet.
            Initial,

            // A list separator was just pushed; the next fragment opens a new list item.
            ListItem,

            // A fragment was just pushed (not inside a list).
            Fragment,

            // A fragment was pushed inside a list; tracks that further fragments here are
            // space-joined, not comma-joined.
            FragmentInList,

            // Build() was called; the builder is spent.
            Built,
        }

        private readonly StringBuilder _sb = new StringBuilder();
        private State _state = State.Initial;
        private bool _isFirstListItem = true;

        /// <summary>True if nothing has been appended yet.</summary>
        public bool IsEmpty
        {
            get { return _sb.Length == 0; }
        }

        private void CheckNotBuilt()
        {
            if (_state == State.Built)
            {
                throw new InvalidOperationException("Attempt to use a MessageBuilder twice");
            }
        }

        /// <summary>
        /// Append a text fragment. Fragments are separated from preceding content by the fragment
        /// separator (a space in English); the first fragment of a fresh list item is separated by
        /// the list separator instead (", " in English). Null/empty fragments are ignored so
        /// optional pieces can be appended blindly.
        /// </summary>
        public MessageBuilder Fragment(string fragment)
        {
            CheckNotBuilt();

            if (fragment == " ")
            {
                throw new ArgumentException(
                    "Fragment(\" \") is unnecessary - spaces are added between fragments automatically"
                );
            }

            if (string.IsNullOrEmpty(fragment))
            {
                return this;
            }

            // Opening a new list item: the list separator replaces the fragment separator here
            // (never before the first item).
            bool opensListItem = false;
            if (_state == State.ListItem)
            {
                opensListItem = !_isFirstListItem;
                _isFirstListItem = false;
            }

            _state =
                (_state == State.ListItem || _state == State.FragmentInList)
                    ? State.FragmentInList
                    : State.Fragment;

            if (opensListItem)
            {
                _sb.Append(ModText.Get(ModStrings.Graph.ListSeparator));
            }
            else if (_sb.Length > 0)
            {
                // Everything except the very first piece of content is separated.
                _sb.Append(ModText.Get(ModStrings.Graph.FragmentSeparator));
            }

            _sb.Append(fragment);
            return this;
        }

        /// <summary>
        /// Mark a list-item boundary; the next fragment (here or passed in) starts a new
        /// comma-separated item. The optional fragment is appended after the boundary.
        /// </summary>
        public MessageBuilder ListItem(string fragment = null)
        {
            CheckNotBuilt();
            _state = State.ListItem;
            if (!string.IsNullOrEmpty(fragment))
            {
                Fragment(fragment);
            }

            return this;
        }

        /// <summary>
        /// Like <see cref="ListItem"/> but forces a comma even for the first item, e.g. grids
        /// that always read "label, dimensions".
        /// </summary>
        public MessageBuilder ListItemForcedComma(string fragment = null)
        {
            CheckNotBuilt();
            ListItem();
            _isFirstListItem = false;
            if (!string.IsNullOrEmpty(fragment))
            {
                Fragment(fragment);
            }

            return this;
        }

        /// <summary>
        /// Append a fraction as "<paramref name="numerator"/> of <paramref name="denominator"/>"
        /// (e.g. "5 of 20"), with an optional trailing <paramref name="unit"/> ("5 of 20 charges").
        /// The single home for the spoken "N of M" idiom — health bars, "item 1 of 4", fleet
        /// movement points — so the connective ("of") lives in one place, translated with the rest
        /// of the mod's strings, and every fraction reads identically. Behaves like
        /// <see cref="Fragment"/> for spacing (the caller sets list boundaries with
        /// <see cref="ListItem"/>).
        /// </summary>
        public MessageBuilder PushFraction(int numerator, int denominator, string unit = null)
        {
            CheckNotBuilt();
            string text = string.IsNullOrEmpty(unit)
                ? ModText.Get(ModStrings.Common.CountOf, numerator, denominator)
                : ModText.Get(ModStrings.Graph.FractionUnit, numerator, denominator, unit);

            return Fragment(text);
        }

        /// <summary>
        /// Append a stack count as the spoken multiplier "x N" (e.g. "Titanium x 5"). The single
        /// home for how quantities read. A count of 1 or less appends nothing — a lone item needs
        /// no multiplier — so callers pass the raw quantity unconditionally. Behaves like
        /// <see cref="Fragment"/> for spacing (the caller sets list boundaries).
        /// </summary>
        public MessageBuilder PushQuantity(int count)
        {
            // Asked before the count is, so a spent builder is caught whichever way the count falls:
            // a caller reusing one is a bug whether or not the quantity it passed happened to be the
            // one this appends nothing for.
            CheckNotBuilt();
            if (count > 1)
            {
                Fragment(ModText.Get(ModStrings.Graph.Quantity, count));
            }

            return this;
        }

        /// <summary>
        /// The same multiplier, appended WHATEVER the count is - including one (owner ruling
        /// 2026-08-29).
        ///
        /// <see cref="PushQuantity"/>'s silent singular is right for a readout, where "Titanium x 1"
        /// is noise nobody asked for. It is wrong inside a DRAG, where the count is the whole point
        /// of the sentence: a population marker hands over a different number depending on which one
        /// it is, so "Dragging Imperials x 3" and a bare "Dragging Imperials" one row later read as
        /// two different KINDS of answer rather than as three and one. Stating it every time makes
        /// the rows comparable, which is what a player walking the ring is doing.
        ///
        /// Same template as <see cref="PushQuantity"/>, so a translation still says the multiplier
        /// its own way and there is no second wording to keep in step.
        /// </summary>
        public MessageBuilder PushQuantityAlways(int count)
        {
            return Fragment(ModText.Get(ModStrings.Graph.Quantity, count));
        }

        /// <summary>
        /// Finalize and return the message, or null if nothing was appended. The builder is
        /// single-use after this.
        /// </summary>
        public string Build()
        {
            CheckNotBuilt();
            _state = State.Built;
            return _sb.Length == 0 ? null : _sb.ToString();
        }
    }
}
