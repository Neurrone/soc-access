using System;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// A type-ahead search as the player experiences it on a screen: the letters they have typed so
    /// far (<see cref="TypeAheadSearch"/> does the matching), where the last result put focus, and
    /// whether that is still where they are.
    ///
    /// There is no key that starts a search - typing one is what starts it, which is the only design
    /// a screen reader user can discover without being told. What ends one is spelled out here
    /// rather than in the navigator, because every way out has to agree: the back key clears it, and
    /// so does focus moving by any other means (<see cref="Strayed"/>) - results that describe a
    /// place the player has left would step them somewhere they never asked to go.
    ///
    /// Engine-free on purpose, so all of it is testable off the game: the host supplies
    /// <see cref="OnLand"/> (focus this control and read it out) and <see cref="OnNoMatch"/> (say
    /// so), and everything else here is state.
    /// </summary>
    public sealed class TypeAhead
    {
        private readonly TypeAheadSearch _search = new TypeAheadSearch();

        // Where the last result put focus. Null when no result has landed - a search that matched
        // nothing has not moved the player, so nothing about it can go stale.
        private ControlId _landedOn;

        /// <summary>Put focus on a control and read it out, answering with where focus actually
        /// ended up (null when it could not be reached). The host's, because the host owns focus and
        /// is the only thing that speaks.</summary>
        public Func<ControlId, ControlId> OnLand;

        /// <summary>Said when what has been typed matches nothing (gets the typed text).</summary>
        public Action<string> OnNoMatch
        {
            get { return _search.OnNoMatch; }
            set { _search.OnNoMatch = value; }
        }

        /// <summary>Whether a search is collecting the keyboard right now.</summary>
        public bool IsActive
        {
            get { return _search.IsSearchActive; }
        }

        /// <summary>Whether anything has been typed into the current search.</summary>
        public bool HasBuffer
        {
            get { return _search.HasBuffer; }
        }

        public int ResultCount
        {
            get { return _search.ResultCount; }
        }

        /// <summary>What has been typed so far - for the dev server, which has no keyboard to look
        /// at.</summary>
        public string Buffer
        {
            get { return _search.Buffer; }
        }

        /// <summary>Whether focus has left the result the search put it on. The results are a list
        /// of places relative to where the player was; once something else has moved them, stepping
        /// that list is no longer the same offer.</summary>
        public bool Strayed(ControlId focus)
        {
            return _landedOn != null && !_landedOn.Equals(focus);
        }

        public void Clear()
        {
            _search.Clear();
            _landedOn = null;
        }

        /// <summary>Extend the search by one character and land on the best match. False when there
        /// was nothing to search, in which case the character is dropped rather than remembered -
        /// otherwise the buffer would fill up invisibly on a screen with no scope.</summary>
        public bool Type(char c, SearchScope scope)
        {
            if (scope == null || scope.Count == 0 || scope.TextOf == null || scope.Land == null)
            {
                return false;
            }

            _search.AddChar(c);
            SearchScope searching = scope;
            _search.Search(scope.Count, scope.TextOf, index => Land(searching, index));
            return true;
        }

        /// <summary>Step within the results, wrapping. False when there are none.</summary>
        public bool Step(int direction)
        {
            if (_search.ResultCount == 0)
            {
                return false;
            }

            _search.NavigateResults(direction);
            return true;
        }

        /// <summary>The first result. False when there are none.</summary>
        public bool First()
        {
            if (_search.ResultCount == 0)
            {
                return false;
            }

            _search.JumpToFirstResult();
            return true;
        }

        /// <summary>The last result. False when there are none.</summary>
        public bool Last()
        {
            if (_search.ResultCount == 0)
            {
                return false;
            }

            _search.JumpToLastResult();
            return true;
        }

        private void Land(SearchScope scope, int index)
        {
            ControlId target = scope.Land(index);
            Func<ControlId, ControlId> land = OnLand;
            _landedOn = target != null && land != null ? land(target) : null;
        }
    }
}
