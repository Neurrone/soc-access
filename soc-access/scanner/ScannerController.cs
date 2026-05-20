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
        private readonly Func<ScannerResult, bool> _validator;
        private readonly Func<Vector2Int, bool> _jumpTo;
        private readonly Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, IScannerSpeechContext> _speechContextProvider;
        private readonly ScannerDirectionMode _directionMode;
        private ScannerSnapshot _snapshot;
        private int _categoryIndex;
        private int _subcategoryIndex;
        private int _resultIndex;

        public ScannerController(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<Vector2Int> cursorProvider,
            Func<ScannerResult, bool> validator,
            Func<Vector2Int, bool> jumpTo,
            Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, IScannerSpeechContext> speechContextProvider,
            ScannerDirectionMode directionMode)
        {
            _snapshotBuilder = snapshotBuilder;
            _cursorProvider = cursorProvider;
            _validator = validator;
            _jumpTo = jumpTo;
            _speechContextProvider = speechContextProvider;
            _directionMode = directionMode;
        }

        public bool Refresh()
        {
            Output(ExecuteRefresh());
            return true;
        }

        internal ScannerCommandResult ExecuteRefresh()
        {
            return ExecuteRefreshCore();
        }

        private ScannerCommandResult ExecuteRefreshCore()
        {
            Vector2Int origin = GetCursor();
            _snapshot = BuildSnapshot(origin);
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                _snapshot = null;
                return NoResults();
            }

            _snapshot.SortByDistance(origin);
            _categoryIndex = 0;
            _subcategoryIndex = 0;
            _resultIndex = 0;
            return BuildCommandResult(includePath: true);
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
            _resultIndex = 0;
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
            RebuildFromCursorPreservingScope();
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
            _resultIndex = 0;
            ScannerCommandResult result = BuildCommandResult(includePath: true);
            result.Wrapped = wrapped;
            return result;
        }

        public bool MoveResult(int delta)
        {
            Output(ExecuteMoveResult(delta));
            return true;
        }

        internal ScannerCommandResult ExecuteMoveResult(int delta)
        {
            return ExecuteMoveResultCore(delta);
        }

        private ScannerCommandResult ExecuteMoveResultCore(int delta)
        {
            bool locatedCurrent = RebuildForResultNavigation();
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return NoResults();
            }

            ScannerSubcategory subcategory = CurrentSubcategory();
            if (subcategory == null || subcategory.Results.Count == 0)
            {
                return NoResults();
            }

            if (!locatedCurrent)
            {
                _resultIndex = delta < 0 ? subcategory.Results.Count : -1;
            }

            subcategory = CurrentSubcategory();
            if (subcategory == null || subcategory.Results.Count == 0)
            {
                return NoResults();
            }

            bool wrapped = false;
            int previousIndex = _resultIndex;
            _resultIndex = WrapIndex(_resultIndex, subcategory.Results.Count, delta, out wrapped);
            if (!locatedCurrent)
            {
                wrapped = false;
            }
            else if (delta != 0 && subcategory.Results.Count == 1 && previousIndex == _resultIndex)
            {
                wrapped = true;
            }

            ScannerCommandResult result = BuildCommandResult(includePath: false);
            result.Wrapped = wrapped;
            return result;
        }

        public bool JumpToCurrent()
        {
            Output(ExecuteJumpToCurrent());
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

        public bool SpeakOrientation()
        {
            Output(ExecuteSpeakOrientation());
            return true;
        }

        internal ScannerCommandResult ExecuteSpeakOrientation()
        {
            return ExecuteSpeakOrientationCore();
        }

        private ScannerCommandResult ExecuteSpeakOrientationCore()
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

            return BuildCommandResult(includePath: false);
        }

        private ScannerCommandResult BuildCommandResult(bool includePath)
        {
            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                return NoResults();
            }

            ScannerSubcategory subcategory = CurrentSubcategory();
            Vector2Int cursor = GetCursor();
            IReadOnlyList<ScannerDirectionStep> directions = BuildDirections(cursor, result.Position);
            return new ScannerCommandResult(ScannerCommandStatus.Result)
            {
                Result = result,
                CategoryLabel = CurrentCategory() != null ? CurrentCategory().Label : null,
                SubcategoryLabel = subcategory != null ? subcategory.Label : null,
                ResultIndex = _resultIndex + 1,
                ResultCount = subcategory != null ? subcategory.Results.Count : 1,
                Directions = directions,
                IncludePath = includePath
            };
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
            return CurrentSubcategory() != null && CurrentSubcategory().Results.Count > 0;
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
                _resultIndex = 0;
                return new ReseatResultState(false, hadCurrent, categoryHint, subcategoryHint);
            }

            _snapshot.SortByDistance(origin);

            if (!string.IsNullOrWhiteSpace(key)
                && _snapshot.TryLocateByKey(key, categoryHint, subcategoryHint, allowFallback: false, out ScannerSnapshotLocation location))
            {
                _categoryIndex = location.CategoryIndex;
                _subcategoryIndex = location.SubcategoryIndex;
                _resultIndex = location.ResultIndex;
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
                _resultIndex = 0;
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
                _resultIndex = 0;
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
            _resultIndex = 0;
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
                _resultIndex = 0;
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
                        _resultIndex = 0;
                        return;
                    }
                }
            }

            _categoryIndex = 0;
            _subcategoryIndex = 0;
            _resultIndex = 0;
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
            return subcategory != null && subcategory.Results.Count > 0;
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
            List<ScannerDirectionStep> result = new List<ScannerDirectionStep>();
            int x = target.x - origin.x;
            int y = target.y - origin.y;
            if (y > 0)
            {
                result.Add(new ScannerDirectionStep(y, ModText.Get(ModStrings.Scanner.North)));
            }
            else if (y < 0)
            {
                result.Add(new ScannerDirectionStep(-y, ModText.Get(ModStrings.Scanner.South)));
            }

            if (x > 0)
            {
                result.Add(new ScannerDirectionStep(x, ModText.Get(ModStrings.Scanner.East)));
            }
            else if (x < 0)
            {
                result.Add(new ScannerDirectionStep(-x, ModText.Get(ModStrings.Scanner.West)));
            }

            return result;
        }

        private static IReadOnlyList<ScannerDirectionStep> BuildHexDirections(Vector2Int origin, Vector2Int target)
        {
            List<ScannerDirectionStep> result = new List<ScannerDirectionStep>();
            Vector2Int current = origin;
            while (current != target)
            {
                string direction;
                Vector2Int next = GetNextHexStep(current, target, out direction);
                if (next == current || string.IsNullOrWhiteSpace(direction))
                {
                    break;
                }

                AddHexDirectionStep(result, direction);
                current = next;
            }

            return result;
        }

        private static Vector2Int GetNextHexStep(Vector2Int current, Vector2Int target, out string direction)
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
            string[] directions =
            {
                ModText.Get(ModStrings.Scanner.East),
                ModText.Get(ModStrings.Scanner.West),
                ModText.Get(ModStrings.Scanner.Northeast),
                ModText.Get(ModStrings.Scanner.Northwest),
                ModText.Get(ModStrings.Scanner.Southeast),
                ModText.Get(ModStrings.Scanner.Southwest)
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

        private static void AddHexDirectionStep(List<ScannerDirectionStep> result, string direction)
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
            while (subcategory != null && subcategory.Results.Count > 0)
            {
                ClampIndices();
                ScannerResult result = subcategory.Results[_resultIndex];
                if (_validator == null || _validator(result))
                {
                    return result;
                }

                if (!string.IsNullOrWhiteSpace(result.Key))
                {
                    _snapshot.PruneByKey(result.Key);
                }
                else
                {
                    subcategory.Results.RemoveAt(_resultIndex);
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

        private ScannerCategory CurrentCategory()
        {
            return _snapshot != null && _snapshot.Categories.Count > 0 ? _snapshot.Categories[_categoryIndex] : null;
        }

        private ScannerSubcategory CurrentSubcategory()
        {
            ScannerCategory category = CurrentCategory();
            return category != null && category.Subcategories.Count > 0 ? category.Subcategories[_subcategoryIndex] : null;
        }

        private void ClampIndices()
        {
            if (_snapshot == null || _snapshot.Categories.Count == 0)
            {
                _categoryIndex = 0;
                _subcategoryIndex = 0;
                _resultIndex = 0;
                return;
            }

            if (_categoryIndex >= _snapshot.Categories.Count)
            {
                _categoryIndex = _snapshot.Categories.Count - 1;
            }

            ScannerCategory category = _snapshot.Categories[_categoryIndex];
            if (_subcategoryIndex >= category.Subcategories.Count)
            {
                _subcategoryIndex = category.Subcategories.Count - 1;
            }

            ScannerSubcategory subcategory = category.Subcategories[_subcategoryIndex];
            if (_resultIndex >= subcategory.Results.Count)
            {
                _resultIndex = subcategory.Results.Count - 1;
            }

            if (_categoryIndex < 0) _categoryIndex = 0;
            if (_subcategoryIndex < 0) _subcategoryIndex = 0;
            if (_resultIndex < 0) _resultIndex = 0;
        }

        private Vector2Int GetCursor()
        {
            return _cursorProvider != null ? _cursorProvider() : Vector2Int.zero;
        }

        private ScannerSnapshot BuildSnapshot(Vector2Int origin)
        {
            return _snapshotBuilder != null ? _snapshotBuilder(origin) : null;
        }

    }
}
