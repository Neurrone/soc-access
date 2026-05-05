using System;
using System.Collections.Generic;
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
            Vector2Int origin = GetCursor();
            _snapshot = _snapshotBuilder != null ? _snapshotBuilder(origin) : null;
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                _snapshot = null;
                Speak("No scanner results");
                return true;
            }

            _snapshot.SortByDistance(origin);
            _categoryIndex = 0;
            _subcategoryIndex = 0;
            _resultIndex = 0;
            SpeakCurrent(includePath: true);
            return true;
        }

        public bool MoveCategory(int delta)
        {
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return true;
            }

            int nextIndex = _categoryIndex + delta;
            if (nextIndex < 0 || nextIndex >= _snapshot.Categories.Count)
            {
                return true;
            }

            _categoryIndex = nextIndex;
            _subcategoryIndex = 0;
            _resultIndex = 0;
            SpeakCurrent(includePath: true);
            return true;
        }

        public bool MoveSubcategory(int delta)
        {
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return true;
            }

            ScannerCategory category = CurrentCategory();
            if (category == null || category.Subcategories.Count <= 1)
            {
                Speak("No subcategories");
                return true;
            }

            int nextIndex = _subcategoryIndex + delta;
            if (nextIndex < 0 || nextIndex >= category.Subcategories.Count)
            {
                return true;
            }

            _subcategoryIndex = nextIndex;
            _resultIndex = 0;
            SpeakCurrent(includePath: true);
            return true;
        }

        public bool MoveResult(int delta)
        {
            if (_snapshot == null || _snapshot.IsEmpty)
            {
                return true;
            }

            ScannerSubcategory subcategory = CurrentSubcategory();
            if (subcategory == null || subcategory.Results.Count == 0)
            {
                Speak("No scanner results");
                return true;
            }

            int nextIndex = _resultIndex + delta;
            if (nextIndex < 0 || nextIndex >= subcategory.Results.Count)
            {
                return true;
            }

            _resultIndex = nextIndex;
            SpeakCurrent(includePath: false);
            return true;
        }

        public bool JumpToCurrent()
        {
            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                Speak("No scanner results");
                return true;
            }

            if (_jumpTo != null && _jumpTo(result.Position))
            {
                SpeakCurrent(includePath: false);
            }

            return true;
        }

        public bool SpeakOrientation()
        {
            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                Speak("No scanner results");
                return true;
            }

            SpeakCurrent(includePath: false);
            return true;
        }

        private void SpeakCurrent(bool includePath)
        {
            ScannerResult result = CurrentValidResult();
            if (result == null)
            {
                Speak("No scanner results");
                return;
            }

            SpeechRequest request = BuildSpeechRequest(result);
            if (includePath)
            {
                ScannerCategory category = CurrentCategory();
                ScannerSubcategory subcategory = CurrentSubcategory();
                if (category != null && subcategory != null)
                {
                    request = new SpeechRequest(category.Label + ", " + subcategory.Label + ". " + request.Text, interrupt: false);
                }
            }

            SpeechPipeline.Output(request);
        }

        private SpeechRequest BuildSpeechRequest(ScannerResult result)
        {
            ScannerSubcategory subcategory = CurrentSubcategory();
            int resultCount = subcategory != null ? subcategory.Results.Count : 1;
            IReadOnlyList<ScannerDirectionStep> directions = BuildDirections(GetCursor(), result.Position);
            return _speechContextProvider(result, directions, _resultIndex + 1, resultCount).ToSpeechRequest();
        }

        private IReadOnlyList<ScannerDirectionStep> BuildDirections(Vector2Int origin, Vector2Int target)
        {
            return _directionMode == ScannerDirectionMode.Hex
                ? BuildHexDirections(origin, target)
                : BuildSquareDirections(origin, target);
        }

        private static IReadOnlyList<ScannerDirectionStep> BuildSquareDirections(Vector2Int origin, Vector2Int target)
        {
            List<ScannerDirectionStep> result = new List<ScannerDirectionStep>();
            int x = target.x - origin.x;
            int y = target.y - origin.y;
            if (y > 0)
            {
                result.Add(new ScannerDirectionStep(y, "north"));
            }
            else if (y < 0)
            {
                result.Add(new ScannerDirectionStep(-y, "south"));
            }

            if (x > 0)
            {
                result.Add(new ScannerDirectionStep(x, "east"));
            }
            else if (x < 0)
            {
                result.Add(new ScannerDirectionStep(-x, "west"));
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
                "east",
                "west",
                "northeast",
                "northwest",
                "southeast",
                "southwest"
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

            _snapshot.PruneEmpty();
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

                subcategory.Results.RemoveAt(_resultIndex);
                _snapshot.PruneEmpty();
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

        private static void Speak(string text)
        {
            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }
    }
}
