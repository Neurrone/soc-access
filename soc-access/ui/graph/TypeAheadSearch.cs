using System;
using System.Collections.Generic;
using System.Text;

namespace SongsOfConquestAccess.UI.Graph
{
    /// <summary>
    /// Type-ahead search engine — ported, with permission, from OniAccess (VisionNotIncluded,
    /// Handlers/TypeAheadSearch.cs) by way of WrathAccess. Speech and key routing live in the
    /// engine-side navigator, which feeds typed characters and arrows in; this class is pure
    /// matching/result state.
    ///
    /// Builds a filtered results list over a flat item list with TIERED matching: start-of-string
    /// whole word, start-of-string prefix, mid-string whole word, mid-string word prefix, substring
    /// anywhere, then space-delimited word-prefix abbreviation ("ga pi" matches "gas pipe"). Within a
    /// tier, items keep their LIST ORDER (the screen's element order, not shortest-name ranking:
    /// "l" must land on Load Game by menu position, not License by length), and matches in the item's
    /// NAME (before the first comma) rank ahead of matches in its appended metadata. Diacritics are
    /// ignored. Typing the same letter repeatedly cycles ALL of that letter's matches in list order
    /// (starts-with first, then the weaker tiers — so "l" reaches Load Game, License, then DLC).
    /// </summary>
    public class TypeAheadSearch
    {
        private readonly StringBuilder _buffer = new StringBuilder(32);

        private bool _isSearchActive;
        private List<int> _resultIndices = new List<int>();
        private List<string> _resultNames = new List<string>();
        private int _resultCursor;

        // Working lists for search, one set per match tier (avoids allocation).
        private const int TierCount = 6;
        private readonly List<int>[] _tierIndices;
        private readonly List<string>[] _tierNames;
        private readonly List<int>[] _tierPositions;
        private readonly List<int>[] _tierSortLengths;
        private readonly List<int>[] _tierInSegment;
        private List<int> _workIndices = new List<int>();
        private List<string> _workNames = new List<string>();

        // Announce-and-move callback (called with the ORIGINAL item index).
        private Action<int> _announceResult;

        /// <summary>Called when the buffer matches nothing (gets the buffer text). The host speaks.</summary>
        public Action<string> OnNoMatch { get; set; }

        public TypeAheadSearch()
        {
            _tierIndices = new List<int>[TierCount];
            _tierNames = new List<string>[TierCount];
            _tierPositions = new List<int>[TierCount];
            _tierSortLengths = new List<int>[TierCount];
            _tierInSegment = new List<int>[TierCount];
            for (int t = 0; t < TierCount; t++)
            {
                _tierIndices[t] = new List<int>();
                _tierNames[t] = new List<string>();
                _tierPositions[t] = new List<int>();
                _tierSortLengths[t] = new List<int>();
                _tierInSegment[t] = new List<int>();
            }
        }

        public string Buffer
        {
            get { return _buffer.ToString(); }
        }

        public bool HasBuffer
        {
            get { return _buffer.Length > 0; }
        }

        public bool IsSearchActive
        {
            get { return _isSearchActive; }
        }

        public int ResultCount
        {
            get { return _resultIndices.Count; }
        }

        public void AddChar(char c)
        {
            _buffer.Append(c);
        }

        public bool RemoveChar()
        {
            if (_buffer.Length == 0) return false;
            _buffer.Length--;
            return true;
        }

        public void Clear()
        {
            _buffer.Length = 0;
            _isSearchActive = false;
            _resultIndices.Clear();
            _resultNames.Clear();
            _resultCursor = 0;
            _announceResult = null;
        }

        /// <summary>Run the tiered search over the items and move/announce the best result.</summary>
        public void Search(int itemCount, Func<int, string> nameByIndex, Action<int> announceResult)
        {
            // Repeat single-letter: typing the same letter again cycles ALL its matches in list
            // order (l -> Load Game, l -> License, l -> DLC), wrapping.
            string bufferStr = _buffer.ToString();
            if (_isSearchActive && _resultIndices.Count > 0 && _buffer.Length > 1 && IsAllSameChar(bufferStr))
            {
                _buffer.Length = 1;
                if (announceResult != null) _announceResult = announceResult;
                NavigateResults(1);
                return;
            }

            if (announceResult != null) _announceResult = announceResult;

            string trimmed = bufferStr.TrimEnd();
            if (!HasBuffer || itemCount == 0 || trimmed.Length == 0)
            {
                _resultIndices.Clear();
                _resultNames.Clear();
                _resultCursor = 0;
                _isSearchActive = true;
                if (OnNoMatch != null) OnNoMatch(bufferStr);
                return;
            }

            for (int t = 0; t < TierCount; t++)
            {
                _tierIndices[t].Clear();
                _tierNames[t].Clear();
                _tierPositions[t].Clear();
                _tierSortLengths[t].Clear();
                _tierInSegment[t].Clear();
            }
            string lowerBuffer = trimmed.ToLowerInvariant();

            for (int i = 0; i < itemCount; i++)
            {
                string name = nameByIndex(i);
                if (string.IsNullOrEmpty(name)) continue;
                int pos;
                int tier = MatchTier(name.ToLowerInvariant(), lowerBuffer, out pos);
                if (tier >= 0)
                {
                    _tierIndices[tier].Add(i);
                    _tierNames[tier].Add(name);
                    _tierPositions[tier].Add(pos);
                    // Matches inside the name (before the first comma) rank ahead of matches inside the
                    // appended metadata; sort length is likewise name-only.
                    int comma = name.IndexOf(',');
                    int nameLen = comma >= 0 ? comma : name.Length;
                    _tierSortLengths[tier].Add(nameLen);
                    _tierInSegment[tier].Add(pos < nameLen ? 0 : 1);
                }
            }

            // Within a tier, entries stay in ITEM order (collected 0..n above) — no length re-ranking.
            // Merge: name (pre-comma) matches across all tiers before metadata (post-comma) matches.
            _workIndices.Clear();
            _workNames.Clear();
            for (int inSeg = 0; inSeg <= 1; inSeg++)
                for (int t = 0; t < TierCount; t++)
                    for (int i = 0; i < _tierIndices[t].Count; i++)
                        if (_tierInSegment[t][i] == inSeg)
                        {
                            _workIndices.Add(_tierIndices[t][i]);
                            _workNames.Add(_tierNames[t][i]);
                        }

            if (_workIndices.Count == 0)
            {
                _resultIndices.Clear();
                _resultNames.Clear();
                _resultCursor = 0;
                _isSearchActive = true;
                if (OnNoMatch != null) OnNoMatch(bufferStr);
            }
            else
            {
                List<int> tempIndices = _resultIndices;
                List<string> tempNames = _resultNames;
                _resultIndices = _workIndices;
                _resultNames = _workNames;
                _workIndices = tempIndices;
                _workNames = tempNames;
                _resultCursor = 0;
                _isSearchActive = true;
                AnnounceCurrentResult();
            }
        }

        /// <summary>Step within the filtered results (wrapping). +1 next, -1 previous.</summary>
        public void NavigateResults(int direction)
        {
            if (_resultIndices.Count == 0) return;
            int count = _resultIndices.Count;
            _resultCursor = Cycle.Wrap(_resultCursor + direction, count);
            AnnounceCurrentResult();
        }

        public void JumpToFirstResult()
        {
            if (_resultIndices.Count == 0) return;
            _resultCursor = 0;
            AnnounceCurrentResult();
        }

        public void JumpToLastResult()
        {
            if (_resultIndices.Count == 0) return;
            _resultCursor = _resultIndices.Count - 1;
            AnnounceCurrentResult();
        }

        /// <summary>The original item index of the result the cursor is on, or -1.</summary>
        public int CurrentResultIndex
        {
            get { return _resultIndices.Count == 0 ? -1 : _resultIndices[_resultCursor]; }
        }

        private void AnnounceCurrentResult()
        {
            if (_resultIndices.Count == 0) return;
            if (_announceResult != null) _announceResult(_resultIndices[_resultCursor]);
        }

        private static bool IsAllSameChar(string s)
        {
            char first = s[0];
            for (int i = 1; i < s.Length; i++)
                if (s[i] != first) return false;
            return true;
        }

        /// <summary>Match tier for a prefix against a name (both lowercase), or -1. 0 = start whole
        /// word, 1 = start prefix, 2 = mid whole word, 3 = mid word prefix, 4 = substring anywhere,
        /// 5 = space-delimited word-prefix abbreviation ("ga pi" in "gas pipe").</summary>
        public static int MatchTier(string lowerName, string lowerPrefix, out int position)
        {
            position = -1;
            lowerName = TextUtil.RemoveDiacritics(lowerName);
            lowerPrefix = TextUtil.RemoveDiacritics(lowerPrefix);
            int prefixLen = lowerPrefix.Length;
            if (prefixLen > lowerName.Length) return -1;

            if (string.Compare(lowerName, 0, lowerPrefix, 0, prefixLen, StringComparison.Ordinal) == 0)
            {
                position = 0;
                bool wholeWord = lowerName.Length == prefixLen || lowerName[prefixLen] == ' ' || lowerName[prefixLen] == ',';
                return wholeWord ? 0 : 1;
            }

            for (int i = 1; i < lowerName.Length; i++)
            {
                char prev = lowerName[i - 1];
                if (prev != ' ' && prev != ',') continue;
                if (lowerName[i] == ' ') continue;
                if (lowerName.Length - i < prefixLen) break;
                if (string.Compare(lowerName, i, lowerPrefix, 0, prefixLen, StringComparison.Ordinal) == 0)
                {
                    int afterMatch = i + prefixLen;
                    bool wholeWord = afterMatch >= lowerName.Length || lowerName[afterMatch] == ' ' || lowerName[afterMatch] == ',';
                    position = i;
                    return wholeWord ? 2 : 3;
                }
            }

            int idx = lowerName.IndexOf(lowerPrefix, StringComparison.Ordinal);
            if (idx >= 0)
            {
                position = idx;
                return 4;
            }

            if (lowerPrefix.IndexOf(' ') >= 0)
            {
                int abbrevPos = MatchWordPrefixTokens(lowerName, lowerPrefix);
                if (abbrevPos >= 0)
                {
                    position = abbrevPos;
                    return 5;
                }
            }

            return -1;
        }

        // Position of the first matched word if every space-delimited token in the prefix is a prefix
        // of a distinct word in the name, consumed in order, all within one comma-delimited segment.
        private static int MatchWordPrefixTokens(string lowerName, string lowerPrefix)
        {
            string[] rawTokens = lowerPrefix.Split(' ');
            int tokenCount = 0;
            for (int t = 0; t < rawTokens.Length; t++)
                if (rawTokens[t].Length > 0) rawTokens[tokenCount++] = rawTokens[t];
            if (tokenCount == 0) return -1;

            int tokenIdx = 0;
            int firstPos = -1;
            int i = 0;
            while (i < lowerName.Length)
            {
                char c = lowerName[i];
                if (c == ',')
                {
                    tokenIdx = 0;
                    firstPos = -1;
                    i++;
                    continue;
                }
                if (c == ' ') { i++; continue; }

                if (tokenIdx < tokenCount)
                {
                    string token = rawTokens[tokenIdx];
                    bool fits = i + token.Length <= lowerName.Length
                        && string.Compare(lowerName, i, token, 0, token.Length, StringComparison.Ordinal) == 0;
                    if (fits)
                    {
                        if (tokenIdx == 0) firstPos = i;
                        tokenIdx++;
                        if (tokenIdx == tokenCount) return firstPos;
                        i += token.Length;
                    }
                }
                while (i < lowerName.Length && lowerName[i] != ' ' && lowerName[i] != ',') i++;
            }

            return -1;
        }
    }
}
