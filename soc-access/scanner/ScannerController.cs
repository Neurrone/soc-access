using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal sealed class ScannerController
    {
        private readonly Func<Vector2Int, ScannerSnapshot> _snapshotBuilder;
        private readonly Func<Vector2Int> _cursorProvider;
        private readonly Func<ScannerResult, Vector2Int, ScannerResultRefresh> _refreshResult;
        private readonly Func<Vector2Int, bool> _jumpTo;
        private readonly Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, bool, IScannerSpeechContext> _speechContextProvider;
        private readonly ScannerDirectionMode _directionMode;
        private ScannerSnapshot _snapshot;
        private int _categoryIndex;
        private int _subcategoryIndex;
        private int _itemIndex;
        private int _instanceIndex;
        private int _preTemporaryCategoryIndex;
        private bool _hasBuiltSnapshot;
        private string _announcedCategoryKey;
        private string _announcedSubcategoryKey;
        private string _announcedItemKey;

        public ScannerController(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<Vector2Int> cursorProvider,
            Func<ScannerResult, Vector2Int, ScannerResultRefresh> refreshResult,
            Func<Vector2Int, bool> jumpTo,
            Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, bool, IScannerSpeechContext> speechContextProvider,
            ScannerDirectionMode directionMode)
        {
            _snapshotBuilder = snapshotBuilder;
            _cursorProvider = cursorProvider;
            _refreshResult = refreshResult;
            _jumpTo = jumpTo;
            _speechContextProvider = speechContextProvider;
            _directionMode = directionMode;
        }

        /// <summary>
        /// Builds the first snapshot and announces the first scope that has
        /// anything in it. Every navigation command falls back to this when
        /// nothing has been scanned yet, because otherwise the first key press
        /// steps out of an empty starting category and reports no results while
        /// the map is full of them.
        /// </summary>
        internal ScannerCommandResult ExecuteInitialLanding()
        {
            return ExecuteInitialLandingCore();
        }

        internal ScannerCommandResult ExecuteSearch(string query)
        {
            return ExecuteSearchCore(query);
        }

        internal ScannerCommandResult ExecuteLookAround(int radius)
        {
            return ExecuteLookAroundCore(radius);
        }

        private ScannerCommandResult ExecuteInitialLandingCore()
        {
            Vector2Int origin = GetCursor();
            _snapshot = BuildSnapshot(origin);
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                _snapshot = null;
                return NoResults();
            }

            _snapshot.SortByDistance(origin);
            LandOnFirstNonEmptyScope();
            return BuildCommandResult(includePath: true);
        }

        private ScannerCommandResult ExecuteSearchCore(string query)
        {
            if (string.IsNullOrWhiteSpace(query))
            {
                ClearTemporarySnapshot();
                return SearchNoResults();
            }

            Vector2Int origin = GetCursor();
            ScannerSnapshot source = BuildSnapshot(origin);
            ScannerSnapshot search = ScannerSearch.Build(source, query, origin);
            if (search == null || search.IsEmpty)
            {
                ClearTemporarySnapshot();
                return SearchNoResults();
            }

            if (_snapshot == null || !_snapshot.IsTemporarySnapshot)
            {
                _preTemporaryCategoryIndex = _categoryIndex;
            }

            _snapshot = search;
            _categoryIndex = 0;
            _subcategoryIndex = 0;
            _itemIndex = 0;
            _instanceIndex = 0;
            return BuildCommandResult(includePath: true);
        }

        private ScannerCommandResult ExecuteLookAroundCore(int radius)
        {
            Vector2Int origin = GetCursor();
            ScannerSnapshot source = BuildSnapshot(origin);
            ScannerSnapshot lookAround = ScannerLookAround.Build(source, origin, radius);
            if (lookAround == null || lookAround.IsEmpty)
            {
                ClearTemporarySnapshot();
                return NoResults();
            }

            if (_snapshot == null || !_snapshot.IsTemporarySnapshot)
            {
                _preTemporaryCategoryIndex = _categoryIndex;
            }

            _snapshot = lookAround;
            _categoryIndex = 0;
            _subcategoryIndex = 0;
            _itemIndex = 0;
            _instanceIndex = 0;
            return BuildCommandResult(includePath: true);
        }

        private void ClearTemporarySnapshot()
        {
            if (_snapshot != null && _snapshot.IsTemporarySnapshot)
            {
                _categoryIndex = _preTemporaryCategoryIndex;
            }

            _snapshot = null;
            _subcategoryIndex = 0;
            _itemIndex = 0;
            _instanceIndex = 0;
        }

        public bool MoveCategory(int delta)
        {
            Output(ExecuteMoveCategory(delta));
            return true;
        }

        internal ScannerCommandResult ExecuteMoveCategory(int delta)
        {
            return ExecuteMoveCategoryCore(delta);
        }

        private ScannerCommandResult ExecuteMoveCategoryCore(int delta)
        {
            if (!_hasBuiltSnapshot)
            {
                return ExecuteInitialLandingCore();
            }

            if (_snapshot != null && _snapshot.IsTemporarySnapshot)
            {
                _categoryIndex = _preTemporaryCategoryIndex;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                _snapshot = null;
            }

            RebuildFromCursorPreservingScope();
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return NoResults();
            }

            bool wrapped;
            int nextIndex = FindNextCategoryIndex(delta, out wrapped);
            if (nextIndex < 0)
            {
                return NoResults();
            }

            _categoryIndex = nextIndex;
            _subcategoryIndex = FirstNonEmptySubcategoryIndex(CurrentCategory());
            _itemIndex = 0;
            _instanceIndex = 0;
            ScannerCommandResult result = BuildCommandResult(includePath: true);
            result.Wrapped = wrapped;
            return result;
        }

        public bool MoveSubcategory(int delta)
        {
            Output(ExecuteMoveSubcategory(delta));
            return true;
        }

        internal ScannerCommandResult ExecuteMoveSubcategory(int delta)
        {
            return ExecuteMoveSubcategoryCore(delta);
        }

        private ScannerCommandResult ExecuteMoveSubcategoryCore(int delta)
        {
            if (!_hasBuiltSnapshot)
            {
                return ExecuteInitialLandingCore();
            }

            if (_snapshot == null || !_snapshot.IsTemporarySnapshot)
            {
                RebuildFromCursorPreservingScope();
            }

            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return NoResults();
            }

            ScannerCategory category = CurrentCategory();
            if (category == null)
            {
                return NoResults();
            }

            bool wrapped;
            int nextIndex = NextIndexMatching(category.Subcategories, _subcategoryIndex, delta, SubcategoryHasResults, out wrapped);
            if (nextIndex < 0)
            {
                return NoResults();
            }

            _subcategoryIndex = nextIndex;
            _itemIndex = 0;
            _instanceIndex = 0;
            ScannerCommandResult result = BuildCommandResult(includePath: true);
            result.Wrapped = wrapped;
            return result;
        }

        public bool MoveItem(int delta)
        {
            Output(ExecuteMoveItem(delta));
            return true;
        }

        internal ScannerCommandResult ExecuteMoveItem(int delta)
        {
            return ExecuteMoveItemCore(delta);
        }

        private ScannerCommandResult ExecuteMoveItemCore(int delta)
        {
            if (!_hasBuiltSnapshot)
            {
                return ExecuteInitialLandingCore();
            }

            bool locatedCurrent = _snapshot != null && _snapshot.IsTemporarySnapshot
                ? CurrentValidResult() != null
                : RebuildForResultNavigation();
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return NoResults();
            }

            ScannerSubcategory subcategory = CurrentSubcategory();
            if (subcategory == null || !subcategory.HasResults)
            {
                return NoResults();
            }

            if (!locatedCurrent)
            {
                _itemIndex = delta < 0 ? subcategory.Items.Count : -1;
            }

            subcategory = CurrentSubcategory();
            if (subcategory == null || !subcategory.HasResults)
            {
                return NoResults();
            }

            bool wrapped;
            _itemIndex = WrapIndex(_itemIndex, subcategory.Items.Count, delta, out wrapped);
            _instanceIndex = 0;
            if (!locatedCurrent)
            {
                // Landing back on the list after losing the old result is not
                // a wrap; the player never walked off the end of anything.
                wrapped = false;
            }

            ScannerCommandResult result = BuildCommandResult(includePath: false);
            result.Wrapped = wrapped;
            return result;
        }

        public bool MoveInstance(int delta)
        {
            Output(ExecuteMoveInstance(delta));
            return true;
        }

        internal ScannerCommandResult ExecuteMoveInstance(int delta)
        {
            return ExecuteMoveInstanceCore(delta);
        }

        /// <summary>
        /// Walks the copies of the thing the item cycle is sitting on, so a
        /// map with a dozen chests costs one stop in the outer cycle and the
        /// dozen are only visited when the player asks for them.
        /// </summary>
        private ScannerCommandResult ExecuteMoveInstanceCore(int delta)
        {
            if (!_hasBuiltSnapshot)
            {
                return ExecuteInitialLandingCore();
            }

            bool locatedCurrent = _snapshot != null && _snapshot.IsTemporarySnapshot
                ? CurrentValidResult() != null
                : RebuildForResultNavigation();
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return NoResults();
            }

            ScannerItem item = CurrentItem();
            if (item == null || item.Instances.Count == 0)
            {
                return NoResults();
            }

            if (!locatedCurrent)
            {
                _instanceIndex = delta < 0 ? item.Instances.Count : -1;
            }

            item = CurrentItem();
            if (item == null || item.Instances.Count == 0)
            {
                return NoResults();
            }

            bool wrapped;
            _instanceIndex = WrapIndex(_instanceIndex, item.Instances.Count, delta, out wrapped);
            if (!locatedCurrent)
            {
                wrapped = false;
            }

            ScannerCommandResult result = BuildCommandResult(includePath: false);
            result.Wrapped = wrapped;
            return result;
        }

        public bool MoveCustomCategoryEntry(string categoryKey, int delta)
        {
            Output(ExecuteMoveCustomCategoryEntry(categoryKey, delta));
            return true;
        }

        internal ScannerCommandResult ExecuteMoveCustomCategoryEntry(string categoryKey, int delta)
        {
            return ExecuteMoveCustomCategoryEntryCore(categoryKey, delta);
        }

        /// <summary>
        /// Walks one custom category from a single key, as one flat list of
        /// instances taken nearest first and with the item grouping ignored.
        /// A single key has no separate instance axis the way the paging cycles
        /// do, so two chests gathered under one item stop would leave the second
        /// one unreachable; flattening is what lets the one key reach
        /// everything.
        ///
        /// The walk answers "nearest first from where I am", so a cursor that
        /// has left the origin the snapshot was sorted from starts a fresh
        /// sweep rather than continuing the old one. Everything downstream, the
        /// jump, the bearing readout, the return key and the beep, acts on
        /// whatever this lands on exactly as it does for the paging cycles.
        /// </summary>
        private ScannerCommandResult ExecuteMoveCustomCategoryEntryCore(string categoryKey, int delta)
        {
            if (string.IsNullOrWhiteSpace(categoryKey))
            {
                return NoResults();
            }

            // The same escape hatch the category cycle uses: a frozen search or
            // look around snapshot is stepped out of before the real categories
            // are walked.
            if (_snapshot != null && _snapshot.IsTemporarySnapshot)
            {
                _categoryIndex = _preTemporaryCategoryIndex;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                _snapshot = null;
            }

            if (IsSeatedInCustomCategory(categoryKey) && IsCursorAtSortOrigin())
            {
                ReseatResultState state = RebuildAndReseatCurrentResult();
                if (IsSeatedInCustomCategory(categoryKey))
                {
                    return StepFlatWalk(delta, state.LocatedCurrent);
                }
            }

            return RestartFlatWalk(categoryKey, delta);
        }

        private ScannerCommandResult StepFlatWalk(int delta, bool locatedCurrent)
        {
            List<FlatWalkEntry> walk = BuildFlatWalk();
            if (walk.Count == 0)
            {
                return NoResults();
            }

            bool wrapped = false;
            int position = locatedCurrent ? IndexInFlatWalk(walk, _itemIndex, _instanceIndex) : -1;
            if (position < 0)
            {
                // The entry the walk was standing on died across the rebuild.
                // Landing on the end the press was heading for is not a wrap;
                // the player never walked off anything.
                position = delta < 0 ? walk.Count - 1 : 0;
            }
            else
            {
                position = WrapIndex(position, walk.Count, delta, out wrapped);
            }

            _itemIndex = walk[position].ItemIndex;
            _instanceIndex = walk[position].InstanceIndex;
            return BuildFlatWalkResult(wrapped);
        }

        /// <summary>
        /// Re-anchors on the live cursor and takes the nearest entry, except
        /// when that is the entry the walk was already on with the cursor parked
        /// on top of it, which is what a jump to that very entry leaves behind.
        /// The press asked to move, so it steps on instead of landing where the
        /// player already is: that is what turns a jump-and-press rhythm into a
        /// nearest-neighbour hop across the map.
        /// </summary>
        private ScannerCommandResult RestartFlatWalk(string categoryKey, int delta)
        {
            string previousKey = null;
            ScannerCategory seated = CurrentCategory();
            if (seated != null && seated.Key == categoryKey)
            {
                ScannerResult current = CurrentValidResult();
                previousKey = current != null ? current.Key : null;
            }

            Vector2Int origin = GetCursor();
            _snapshot = BuildSnapshot(origin);
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                _snapshot = null;
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return NoResults();
            }

            _snapshot.SortByDistance(origin);
            int categoryIndex = IndexOfCategory(categoryKey);
            int subcategoryIndex = categoryIndex >= 0
                ? IndexOfSubcategory(_snapshot.Categories[categoryIndex], ScannerSubcategoryKeys.All)
                : -1;
            if (subcategoryIndex < 0)
            {
                // The category the key answers for is not in the snapshot that
                // was just swapped in, which a contribution that threw during
                // the build is enough to do. Leaving the walk seated on indices
                // measured against the snapshot before it would point the next
                // press past the end of this one.
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return NoResults();
            }

            _categoryIndex = categoryIndex;
            _subcategoryIndex = subcategoryIndex;
            _itemIndex = 0;
            _instanceIndex = 0;

            List<FlatWalkEntry> walk = BuildFlatWalk();
            if (walk.Count == 0)
            {
                return NoResults();
            }

            int position = 0;
            if (previousKey != null
                && walk.Count > 1
                && walk[0].Result != null
                && walk[0].Result.Key == previousKey
                && walk[0].Result.Position == origin)
            {
                position = WrapIndex(0, walk.Count, delta);
            }

            _itemIndex = walk[position].ItemIndex;
            _instanceIndex = walk[position].InstanceIndex;
            return BuildFlatWalkResult(wrapped: false);
        }

        /// <summary>
        /// Counts the position and the total over the flat walk rather than the
        /// copies of one item, because the walk is what the key steps through.
        /// The category name is left off for the same reason the paging cycles
        /// leave it off mid-walk: the player picked this key for this category
        /// and is being told where in it they now are.
        /// </summary>
        private ScannerCommandResult BuildFlatWalkResult(bool wrapped)
        {
            // Validation can prune a result that has gone and reshape the
            // subcategory around it, so the walk is measured afterwards: the
            // spoken count has to be the one the next press will step through.
            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                return NoResults();
            }

            List<FlatWalkEntry> walk = BuildFlatWalk();
            int position = IndexInFlatWalk(walk, _itemIndex, _instanceIndex);
            ScannerCategory category = CurrentCategory();
            ScannerSubcategory subcategory = CurrentSubcategory();
            ScannerItem item = CurrentItem();
            Vector2Int origin = GetSpeechOrigin();
            return new ScannerCommandResult(ScannerCommandStatus.Result)
            {
                Result = result,
                CategoryLabel = category != null ? category.Label : null,
                SubcategoryLabel = subcategory != null ? subcategory.Label : null,
                ResultIndex = position >= 0 ? position + 1 : 1,
                ResultCount = walk.Count,
                IncludeItemName = TakeItemNameTurn(category, subcategory, item),
                Directions = BuildDirections(origin, result.Position),
                HasOrigin = true,
                Origin = origin,
                IncludePath = false,
                Wrapped = wrapped
            };
        }

        /// <summary>
        /// Every instance under the current subcategory as one list, back in the
        /// order the snapshot was sorted into. A scope the adapter deliberately
        /// left unsorted keeps the order it was given.
        /// </summary>
        private List<FlatWalkEntry> BuildFlatWalk()
        {
            List<FlatWalkEntry> walk = new List<FlatWalkEntry>();
            ScannerSubcategory subcategory = CurrentSubcategory();
            if (subcategory == null)
            {
                return walk;
            }

            for (int itemIndex = 0; itemIndex < subcategory.Items.Count; itemIndex++)
            {
                List<ScannerResult> instances = subcategory.Items[itemIndex].Instances;
                for (int instanceIndex = 0; instanceIndex < instances.Count; instanceIndex++)
                {
                    walk.Add(new FlatWalkEntry(itemIndex, instanceIndex, instances[instanceIndex]));
                }
            }

            if (!subcategory.PreserveResultOrder && _snapshot != null && _snapshot.HasSortOrigin)
            {
                Vector2Int origin = _snapshot.SortOrigin;
                walk.Sort((left, right) => ScannerSnapshot.CompareByDistance(origin, left.Result, right.Result));
            }

            return walk;
        }

        private static int IndexInFlatWalk(IReadOnlyList<FlatWalkEntry> walk, int itemIndex, int instanceIndex)
        {
            for (int i = 0; i < walk.Count; i++)
            {
                if (walk[i].ItemIndex == itemIndex && walk[i].InstanceIndex == instanceIndex)
                {
                    return i;
                }
            }

            return -1;
        }

        private bool IsSeatedInCustomCategory(string categoryKey)
        {
            ScannerCategory category = CurrentCategory();
            ScannerSubcategory subcategory = CurrentSubcategory();
            return category != null
                && category.Key == categoryKey
                && subcategory != null
                && subcategory.Key == ScannerSubcategoryKeys.All;
        }

        private bool IsCursorAtSortOrigin()
        {
            return _snapshot != null && _snapshot.HasSortOrigin && GetCursor() == _snapshot.SortOrigin;
        }

        private int IndexOfCategory(string categoryKey)
        {
            if (_snapshot == null)
            {
                return -1;
            }

            for (int i = 0; i < _snapshot.Categories.Count; i++)
            {
                if (_snapshot.Categories[i].Key == categoryKey)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int IndexOfSubcategory(ScannerCategory category, string subcategoryKey)
        {
            if (category == null)
            {
                return -1;
            }

            for (int i = 0; i < category.Subcategories.Count; i++)
            {
                if (category.Subcategories[i].Key == subcategoryKey)
                {
                    return i;
                }
            }

            return -1;
        }

        private struct FlatWalkEntry
        {
            public FlatWalkEntry(int itemIndex, int instanceIndex, ScannerResult result)
            {
                ItemIndex = itemIndex;
                InstanceIndex = instanceIndex;
                Result = result;
            }

            public int ItemIndex { get; private set; }

            public int InstanceIndex { get; private set; }

            public ScannerResult Result { get; private set; }
        }

        /// <summary>
        /// A jump that lands somewhere leaves the talking to the tile it landed
        /// on. The result was just read out by whatever step chose it, so
        /// speaking it again puts the thing the player picked in front of the
        /// tile they are now standing on. Only a jump with nowhere to go still
        /// says so.
        /// </summary>
        public bool JumpToCurrent()
        {
            ScannerCommandResult result = ExecuteJumpToCurrent();
            if (result != null && result.Status == ScannerCommandStatus.NoResults)
            {
                Output(result);
            }

            return true;
        }

        internal ScannerCommandResult ExecuteJumpToCurrent()
        {
            return ExecuteJumpToCurrentCore();
        }

        private ScannerCommandResult ExecuteJumpToCurrentCore()
        {
            if (!RebuildForCurrentResultAction())
            {
                return NoResults();
            }

            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                return NoResults();
            }

            if (_jumpTo != null && _jumpTo(result.Position))
            {
                return BuildCommandResult(includePath: false);
            }

            return null;
        }

        internal ScannerCommandResult ExecuteSpeakDistanceAndDirection()
        {
            return ExecuteSpeakDistanceAndDirectionCore();
        }

        private ScannerCommandResult ExecuteSpeakDistanceAndDirectionCore()
        {
            if (!_hasBuiltSnapshot)
            {
                return NoResults();
            }

            if (!RebuildForCurrentResultAction())
            {
                return NoResults();
            }

            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                return NoResults();
            }

            ScannerCommandResult command = BuildCommandResult(includePath: false, takesItemNameTurn: false);
            if (command != null)
            {
                command.DistanceAndDirectionOnly = true;
            }

            return command;
        }

        private ScannerCommandResult BuildCommandResult(bool includePath)
        {
            return BuildCommandResult(includePath, takesItemNameTurn: true);
        }

        /// <summary>
        /// The item name gets one turn per item, and a readout that never
        /// speaks the name must not be the announcement that spends it. The
        /// bearing key can land on an item nobody has heard of yet, since it
        /// falls back to the nearest scope when the snapshot went away under
        /// it, and the player would then walk the copies of a thing whose name
        /// was never said.
        /// </summary>
        private ScannerCommandResult BuildCommandResult(bool includePath, bool takesItemNameTurn)
        {
            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                return NoResults();
            }

            ScannerCategory category = CurrentCategory();
            ScannerSubcategory subcategory = CurrentSubcategory();
            ScannerItem item = CurrentItem();
            Vector2Int origin = GetSpeechOrigin();
            IReadOnlyList<ScannerDirectionStep> directions = BuildDirections(origin, result.Position);
            int resultIndex;
            int resultCount;
            GetResultPosition(subcategory, item, out resultIndex, out resultCount);
            return new ScannerCommandResult(ScannerCommandStatus.Result)
            {
                Result = result,
                CategoryLabel = category != null ? category.Label : null,
                SubcategoryLabel = subcategory != null ? subcategory.Label : null,
                ResultIndex = resultIndex,
                ResultCount = resultCount,
                IncludeItemName = takesItemNameTurn && TakeItemNameTurn(category, subcategory, item),
                Directions = directions,
                HasOrigin = true,
                Origin = origin,
                IncludePath = includePath
            };
        }

        /// <summary>
        /// Counts over whatever the item cycle actually steps through. A grouped
        /// scope steps between the copies of the item, so it counts those. A
        /// flat scope gives every result an item of its own, so counting copies
        /// there would read "1 of 1" for the whole walk and say nothing; it
        /// counts the items, which is the sequence the player is in. Look Around
        /// and the revealed list are the flat ones, and both are sequences whose
        /// length and position are the information.
        /// </summary>
        private void GetResultPosition(ScannerSubcategory subcategory, ScannerItem item, out int index, out int count)
        {
            if (subcategory != null && subcategory.FlatItems)
            {
                index = _itemIndex + 1;
                count = subcategory.Items.Count;
                return;
            }

            index = _instanceIndex + 1;
            count = item != null ? item.Instances.Count : 1;
        }

        /// <summary>
        /// The item name leads an announcement that moves to a different thing
        /// and is dropped when only the instance changed, since the player has
        /// just been told what they are walking through.
        /// </summary>
        private bool TakeItemNameTurn(ScannerCategory category, ScannerSubcategory subcategory, ScannerItem item)
        {
            string categoryKey = category != null ? category.Key : null;
            string subcategoryKey = subcategory != null ? subcategory.Key : null;
            string itemKey = item != null ? item.Key : null;
            bool changed = categoryKey != _announcedCategoryKey
                || subcategoryKey != _announcedSubcategoryKey
                || itemKey != _announcedItemKey;
            _announcedCategoryKey = categoryKey;
            _announcedSubcategoryKey = subcategoryKey;
            _announcedItemKey = itemKey;
            return changed;
        }

        internal void Output(ScannerCommandResult result)
        {
            if (result == null)
            {
                return;
            }

            SpeechPipeline.Output(result.ToSpeechRequest(_speechContextProvider));
        }

        private bool RebuildForResultNavigation()
        {
            ReseatResultState state = RebuildAndReseatCurrentResult();
            if (state.LocatedCurrent)
            {
                return true;
            }

            if (state.HadCurrent)
            {
                RestoreScope(state.CategoryHint, state.SubcategoryHint);
            }
            else
            {
                LandOnFirstNonEmptyScope();
            }

            return false;
        }

        private bool RebuildForCurrentResultAction()
        {
            if (_snapshot != null && _snapshot.IsTemporarySnapshot)
            {
                return CurrentValidResult() != null;
            }

            ReseatResultState state = RebuildAndReseatCurrentResult();
            if (state.LocatedCurrent)
            {
                return true;
            }

            if (state.HadCurrent)
            {
                RestoreScope(state.CategoryHint, state.SubcategoryHint);
                return false;
            }

            LandOnFirstNonEmptyScope();
            return CurrentSubcategory() != null && CurrentSubcategory().HasResults;
        }

        private ReseatResultState RebuildAndReseatCurrentResult()
        {
            string key = null;
            int categoryHint = _categoryIndex;
            int subcategoryHint = _subcategoryIndex;
            bool hadCurrent = false;
            if (_snapshot != null && !_snapshot.IsEmpty)
            {
                ScannerResult current = CurrentValidResult();
                if (current != null)
                {
                    key = current.Key;
                    categoryHint = _categoryIndex;
                    subcategoryHint = _subcategoryIndex;
                    hadCurrent = true;
                }
            }

            Vector2Int origin = _snapshot != null && _snapshot.HasSortOrigin ? _snapshot.SortOrigin : GetCursor();
            _snapshot = BuildSnapshot(origin);

            if (_snapshot == null || _snapshot.IsEmpty)
            {
                _snapshot = null;
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return new ReseatResultState(false, hadCurrent, categoryHint, subcategoryHint);
            }

            _snapshot.SortByDistance(origin);

            if (!string.IsNullOrWhiteSpace(key)
                && _snapshot.TryLocateByKey(key, categoryHint, subcategoryHint, allowFallback: false, out ScannerSnapshotLocation location))
            {
                _categoryIndex = location.CategoryIndex;
                _subcategoryIndex = location.SubcategoryIndex;
                _itemIndex = location.ItemIndex;
                _instanceIndex = location.ResultIndex;
                return new ReseatResultState(true, hadCurrent, categoryHint, subcategoryHint);
            }

            return new ReseatResultState(false, hadCurrent, categoryHint, subcategoryHint);
        }

        private void RebuildFromCursorPreservingScope()
        {
            int categoryHint = _categoryIndex;
            int subcategoryHint = _subcategoryIndex;
            Vector2Int origin = GetCursor();
            _snapshot = BuildSnapshot(origin);

            if (_snapshot == null || _snapshot.IsEmpty)
            {
                _snapshot = null;
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return;
            }

            _snapshot.SortByDistance(origin);
            RestoreScope(categoryHint, subcategoryHint);
        }

        private bool RestoreScope(int categoryIndex, int subcategoryIndex)
        {
            if (_snapshot == null || _snapshot.Categories.Count == 0)
            {
                return false;
            }

            if (categoryIndex < 0)
            {
                categoryIndex = 0;
            }
            else if (categoryIndex >= _snapshot.Categories.Count)
            {
                categoryIndex = _snapshot.Categories.Count - 1;
            }

            ScannerCategory category = _snapshot.Categories[categoryIndex];
            if (category.Subcategories.Count == 0)
            {
                _categoryIndex = categoryIndex;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return false;
            }

            if (subcategoryIndex < 0)
            {
                subcategoryIndex = 0;
            }
            else if (subcategoryIndex >= category.Subcategories.Count)
            {
                subcategoryIndex = category.Subcategories.Count - 1;
            }

            _categoryIndex = categoryIndex;
            _subcategoryIndex = subcategoryIndex;
            _itemIndex = 0;
            _instanceIndex = 0;
            return true;
        }

        private int FindNextCategoryIndex(int delta, out bool wrapped)
        {
            wrapped = false;
            if (_snapshot == null || _snapshot.Categories.Count == 0)
            {
                return -1;
            }

            return NextIndexMatching(_snapshot.Categories, _categoryIndex, delta, CategoryHasResults, out wrapped);
        }

        private void LandOnFirstNonEmptyScope()
        {
            if (_snapshot == null)
            {
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return;
            }

            for (int categoryIndex = 0; categoryIndex < _snapshot.Categories.Count; categoryIndex++)
            {
                ScannerCategory category = _snapshot.Categories[categoryIndex];
                for (int subcategoryIndex = 0; subcategoryIndex < category.Subcategories.Count; subcategoryIndex++)
                {
                    if (SubcategoryHasResults(category.Subcategories[subcategoryIndex]))
                    {
                        _categoryIndex = categoryIndex;
                        _subcategoryIndex = subcategoryIndex;
                        _itemIndex = 0;
                        _instanceIndex = 0;
                        return;
                    }
                }
            }

            _categoryIndex = 0;
            _subcategoryIndex = 0;
            _itemIndex = 0;
            _instanceIndex = 0;
        }

        private struct ReseatResultState
        {
            public ReseatResultState(bool locatedCurrent, bool hadCurrent, int categoryHint, int subcategoryHint)
            {
                LocatedCurrent = locatedCurrent;
                HadCurrent = hadCurrent;
                CategoryHint = categoryHint;
                SubcategoryHint = subcategoryHint;
            }

            public bool LocatedCurrent { get; private set; }

            public bool HadCurrent { get; private set; }

            public int CategoryHint { get; private set; }

            public int SubcategoryHint { get; private set; }
        }

        private IReadOnlyList<ScannerDirectionStep> BuildDirections(Vector2Int origin, Vector2Int target)
        {
            return _directionMode == ScannerDirectionMode.Hex
                ? BuildHexDirections(origin, target)
                : BuildSquareDirections(origin, target);
        }

        private static ScannerCommandResult NoResults()
        {
            return new ScannerCommandResult(ScannerCommandStatus.NoResults);
        }

        private static ScannerCommandResult SearchNoResults()
        {
            return new ScannerCommandResult(ScannerCommandStatus.NoResults)
            {
                NoResultsText = ModText.Get(ModStrings.Scanner.SearchNoResults)
            };
        }

        private static int WrapIndex(int index, int count, int delta)
        {
            bool wrapped;
            return WrapIndex(index, count, delta, out wrapped);
        }

        private static int WrapIndex(int index, int count, int delta, out bool wrapped)
        {
            wrapped = false;
            if (count <= 0)
            {
                return 0;
            }

            if (delta == 0)
            {
                if (index < 0)
                {
                    return 0;
                }

                return index >= count ? count - 1 : index;
            }

            int next = index + delta;
            int wrappedIndex = next % count;
            if (wrappedIndex < 0)
            {
                wrappedIndex += count;
            }

            wrapped = next < 0 || next >= count || count == 1;
            return wrappedIndex;
        }

        private static int NextIndexMatching<T>(IReadOnlyList<T> list, int startIndex, int delta, Func<T, bool> predicate, out bool wrapped)
        {
            wrapped = false;
            if (list == null || list.Count == 0 || predicate == null)
            {
                return -1;
            }

            if (delta == 0)
            {
                return startIndex >= 0 && startIndex < list.Count && predicate(list[startIndex])
                    ? startIndex
                    : -1;
            }

            int index = startIndex;
            if (index < 0 || index >= list.Count)
            {
                index = delta > 0 ? list.Count - 1 : 0;
            }

            for (int i = 0; i < list.Count; i++)
            {
                bool stepWrapped;
                index = WrapIndex(index, list.Count, delta, out stepWrapped);
                wrapped = wrapped || stepWrapped;
                if (predicate(list[index]))
                {
                    return index;
                }
            }

            return -1;
        }

        private static bool CategoryHasResults(ScannerCategory category)
        {
            if (category == null)
            {
                return false;
            }

            for (int i = 0; i < category.Subcategories.Count; i++)
            {
                if (SubcategoryHasResults(category.Subcategories[i]))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool SubcategoryHasResults(ScannerSubcategory subcategory)
        {
            return subcategory != null && subcategory.HasResults;
        }

        private static int FirstNonEmptySubcategoryIndex(ScannerCategory category)
        {
            if (category == null)
            {
                return 0;
            }

            for (int i = 0; i < category.Subcategories.Count; i++)
            {
                if (SubcategoryHasResults(category.Subcategories[i]))
                {
                    return i;
                }
            }

            return 0;
        }

        private static IReadOnlyList<ScannerDirectionStep> BuildSquareDirections(Vector2Int origin, Vector2Int target)
        {
            return ScannerDirectionUtility.BuildSquareDirections(origin, target);
        }

        private static IReadOnlyList<ScannerDirectionStep> BuildHexDirections(Vector2Int origin, Vector2Int target)
        {
            List<ScannerDirectionStep> result = new List<ScannerDirectionStep>();
            Vector2Int current = origin;
            while (current != target)
            {
                ScannerDirection direction;
                Vector2Int next = GetNextHexStep(current, target, out direction);
                if (next == current)
                {
                    break;
                }

                AddHexDirectionStep(result, direction);
                current = next;
            }

            return result;
        }

        private static Vector2Int GetNextHexStep(Vector2Int current, Vector2Int target, out ScannerDirection direction)
        {
            Vector2Int[] neighbors =
            {
                new Vector2Int(current.x + 1, current.y),
                new Vector2Int(current.x - 1, current.y),
                OffsetHexNeighbor(current, north: true, east: true),
                OffsetHexNeighbor(current, north: true, east: false),
                OffsetHexNeighbor(current, north: false, east: true),
                OffsetHexNeighbor(current, north: false, east: false)
            };
            ScannerDirection[] directions =
            {
                ScannerDirection.East,
                ScannerDirection.West,
                ScannerDirection.Northeast,
                ScannerDirection.Northwest,
                ScannerDirection.Southeast,
                ScannerDirection.Southwest
            };

            int bestIndex = 0;
            int bestDistance = HexDistance(neighbors[0], target);
            for (int i = 1; i < neighbors.Length; i++)
            {
                int distance = HexDistance(neighbors[i], target);
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestIndex = i;
                }
            }

            direction = directions[bestIndex];
            return neighbors[bestIndex];
        }

        private static Vector2Int OffsetHexNeighbor(Vector2Int point, bool north, bool east)
        {
            int yDelta = north ? 1 : -1;
            int xDelta;
            if ((point.y & 1) == 0)
            {
                xDelta = east ? 0 : -1;
            }
            else
            {
                xDelta = east ? 1 : 0;
            }

            return new Vector2Int(point.x + xDelta, point.y + yDelta);
        }

        private static int HexDistance(Vector2Int left, Vector2Int right)
        {
            OffsetToCube(left, out int leftX, out int leftY, out int leftZ);
            OffsetToCube(right, out int rightX, out int rightY, out int rightZ);
            return Math.Max(Math.Abs(leftX - rightX), Math.Max(Math.Abs(leftY - rightY), Math.Abs(leftZ - rightZ)));
        }

        private static void OffsetToCube(Vector2Int point, out int cubeX, out int cubeY, out int cubeZ)
        {
            cubeX = point.x - (point.y - (point.y & 1)) / 2;
            cubeZ = point.y;
            cubeY = -cubeX - cubeZ;
        }

        private static void AddHexDirectionStep(List<ScannerDirectionStep> result, ScannerDirection direction)
        {
            if (result.Count > 0)
            {
                ScannerDirectionStep previous = result[result.Count - 1];
                if (previous.Direction == direction)
                {
                    result[result.Count - 1] = new ScannerDirectionStep(previous.Count + 1, direction);
                    return;
                }
            }

            result.Add(new ScannerDirectionStep(1, direction));
        }

        private ScannerResult CurrentValidResult()
        {
            if (_snapshot == null)
            {
                return null;
            }

            if (_snapshot.IsEmpty)
            {
                _snapshot = null;
                return null;
            }

            ClampIndices();
            ScannerSubcategory subcategory = CurrentSubcategory();
            while (subcategory != null && subcategory.HasResults)
            {
                ClampIndices();
                ScannerItem item = CurrentItem();
                if (item == null || item.Instances.Count == 0)
                {
                    return null;
                }

                ScannerResult result = item.Instances[_instanceIndex];
                if (TryRefreshResult(result))
                {
                    return result;
                }

                if (!string.IsNullOrWhiteSpace(result.Key))
                {
                    _snapshot.PruneByKey(result.Key);
                }
                else
                {
                    item.Instances.RemoveAt(_instanceIndex);
                    if (item.Instances.Count == 0)
                    {
                        subcategory.RemoveItem(item);
                    }
                }
                if (_snapshot == null || _snapshot.IsEmpty)
                {
                    _snapshot = null;
                    return null;
                }

                ClampIndices();
                subcategory = CurrentSubcategory();
            }

            return null;
        }

        /// <summary>
        /// Re-queries the adapter for a result that is about to be announced,
        /// passing the live cursor so the adapter can re-center a result that
        /// covers several tiles. Returns false when the thing is gone, which
        /// makes the caller prune it.
        /// </summary>
        private bool TryRefreshResult(ScannerResult result)
        {
            if (_refreshResult == null)
            {
                return true;
            }

            ScannerResultRefresh refresh = _refreshResult(result, GetCursor());
            if (!refresh.IsValid)
            {
                return false;
            }

            result.ApplyRefresh(refresh);
            return true;
        }

        /// <summary>
        /// Where the walk is standing, or nothing where the indices have not
        /// caught up with the snapshot under them. A snapshot is rebuilt on
        /// every press and a contribution that throws is swallowed and carried
        /// on from, so a snapshot arriving with fewer categories than the last
        /// one is a state the walk has to survive rather than an impossible
        /// one: these answer null instead of reaching past the end.
        /// </summary>
        private ScannerCategory CurrentCategory()
        {
            return _snapshot != null && _categoryIndex >= 0 && _categoryIndex < _snapshot.Categories.Count
                ? _snapshot.Categories[_categoryIndex]
                : null;
        }

        private ScannerSubcategory CurrentSubcategory()
        {
            ScannerCategory category = CurrentCategory();
            return category != null && _subcategoryIndex >= 0 && _subcategoryIndex < category.Subcategories.Count
                ? category.Subcategories[_subcategoryIndex]
                : null;
        }

        private ScannerItem CurrentItem()
        {
            ScannerSubcategory subcategory = CurrentSubcategory();
            return subcategory != null && _itemIndex >= 0 && _itemIndex < subcategory.Items.Count
                ? subcategory.Items[_itemIndex]
                : null;
        }

        private void ClampIndices()
        {
            if (_snapshot == null || _snapshot.Categories.Count == 0)
            {
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return;
            }

            if (_categoryIndex >= _snapshot.Categories.Count)
            {
                _categoryIndex = _snapshot.Categories.Count - 1;
            }

            if (_categoryIndex < 0)
            {
                _categoryIndex = 0;
            }

            ScannerCategory category = _snapshot.Categories[_categoryIndex];
            if (category.Subcategories.Count == 0)
            {
                _subcategoryIndex = 0;
                _itemIndex = 0;
                _instanceIndex = 0;
                return;
            }

            if (_subcategoryIndex >= category.Subcategories.Count)
            {
                _subcategoryIndex = category.Subcategories.Count - 1;
            }

            if (_subcategoryIndex < 0)
            {
                _subcategoryIndex = 0;
            }

            ScannerSubcategory subcategory = category.Subcategories[_subcategoryIndex];
            if (subcategory.Items.Count == 0)
            {
                _itemIndex = 0;
                _instanceIndex = 0;
                return;
            }

            if (_itemIndex >= subcategory.Items.Count)
            {
                _itemIndex = subcategory.Items.Count - 1;
            }

            if (_itemIndex < 0)
            {
                _itemIndex = 0;
            }

            ScannerItem item = subcategory.Items[_itemIndex];
            if (_instanceIndex >= item.Instances.Count)
            {
                _instanceIndex = item.Instances.Count - 1;
            }

            if (_instanceIndex < 0)
            {
                _instanceIndex = 0;
            }
        }

        private Vector2Int GetCursor()
        {
            return _cursorProvider != null ? _cursorProvider() : Vector2Int.zero;
        }

        private Vector2Int GetSpeechOrigin()
        {
            return _snapshot != null && _snapshot.UseSortOriginForDirections && _snapshot.HasSortOrigin
                ? _snapshot.SortOrigin
                : GetCursor();
        }

        private ScannerSnapshot BuildSnapshot(Vector2Int origin)
        {
            _hasBuiltSnapshot = true;
            return _snapshotBuilder != null ? _snapshotBuilder(origin) : null;
        }

    }
}
