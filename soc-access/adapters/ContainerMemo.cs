using System;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// WHAT A CONTAINER HELD WHEN IT WAS LAST READ, KEPT UNTIL THE GAME REDRAWS IT.
    ///
    /// Reading a drawn block means walking its children, which a Build must not do every frame
    /// (AGENTS.md, Performance). A redraw destroys the container's children and draws new ones, so
    /// the memo is keyed on what the container holds: its child count and the identity of its first
    /// and last child, all read from the game each frame. A destroyed child is a different identity,
    /// and so is a new one, so a redraw to the same count still misses - which is why the LAST child
    /// is part of the key and not only the first: a block the game rewrites from the bottom keeps
    /// both its count and its first child.
    ///
    /// A language change is the one thing that alters what the children SAY without the game drawing
    /// new ones: the options menu calls SetCurrentLanguage and every drawn mesh re-localizes where it
    /// stands, so whatever this memo read off them is stale while the container is untouched. The
    /// language is therefore part of the key too, read off the game each time like the children and
    /// compared by reference. Where there is no handler at all, as in a test, the answer never
    /// changes and the key is the container alone.
    /// </summary>
    public sealed class ContainerMemo<T> where T : class
    {
        private T _value;
        private Transform _container;
        private int _childCount = -1;
        private Transform _first;
        private Transform _last;
        private ILanguageDefinition _language;

        /// <summary>What <paramref name="read"/> answered for this container, re-read only where the
        /// container is a different one, its children have changed, or the game has changed
        /// language under them.</summary>
        public T Get(Transform container, Func<T> read)
        {
            int count = container != null ? container.childCount : 0;
            Transform first = count > 0 ? container.GetChild(0) : null;
            Transform last = count > 0 ? container.GetChild(count - 1) : null;
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            ILanguageDefinition language = localization != null ? localization.CurrentLanguage : null;
            if (_value != null
                && ReferenceEquals(container, _container)
                && count == _childCount
                && ReferenceEquals(first, _first)
                && ReferenceEquals(last, _last)
                && ReferenceEquals(language, _language))
            {
                return _value;
            }

            _container = container;
            _childCount = count;
            _first = first;
            _last = last;
            _language = language;
            _value = read();
            return _value;
        }
    }
}
