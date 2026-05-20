using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Buffers
{
    internal enum ReviewBufferKind
    {
        Ui,
        AdventureMapNotifications,
        CombatEvents
    }

    internal enum ReviewBufferMoveResult
    {
        Moved,
        BeginningOfBuffer,
        EndOfBuffer
    }

    internal sealed class ReviewBuffer
    {
        private readonly List<string> _lines = new List<string>();

        public ReviewBuffer(ReviewBufferKind kind, bool followLatest)
        {
            Kind = kind;
            FollowLatest = followLatest;
        }

        public ReviewBufferKind Kind { get; private set; }

        public bool IsVisible { get; set; }

        public bool FollowLatest { get; private set; }

        public int CurrentLineIndex { get; private set; }

        public int Count
        {
            get { return _lines.Count; }
        }

        public string Label
        {
            get { return GetLabel(Kind); }
        }

        public string CurrentLine
        {
            get
            {
                if (_lines.Count == 0)
                {
                    return null;
                }

                if (CurrentLineIndex < 0)
                {
                    CurrentLineIndex = 0;
                }

                if (CurrentLineIndex >= _lines.Count)
                {
                    CurrentLineIndex = _lines.Count - 1;
                }

                return _lines[CurrentLineIndex];
            }
        }

        public void ReplaceLines(IEnumerable<string> lines)
        {
            _lines.Clear();
            CurrentLineIndex = 0;
            if (lines == null)
            {
                return;
            }

            foreach (string line in lines)
            {
                AddLineWithoutMoving(line);
            }
        }

        public void AppendLine(string line)
        {
            if (!AddLineWithoutMoving(line))
            {
                return;
            }

            if (FollowLatest)
            {
                CurrentLineIndex = _lines.Count - 1;
            }
        }

        public void Clear()
        {
            _lines.Clear();
            CurrentLineIndex = 0;
        }

        public ReviewBufferMoveResult MovePreviousLine()
        {
            if (_lines.Count == 0 || CurrentLineIndex <= 0)
            {
                CurrentLineIndex = 0;
                return ReviewBufferMoveResult.BeginningOfBuffer;
            }

            CurrentLineIndex--;
            return ReviewBufferMoveResult.Moved;
        }

        public ReviewBufferMoveResult MoveNextLine()
        {
            if (_lines.Count == 0)
            {
                CurrentLineIndex = 0;
                return ReviewBufferMoveResult.EndOfBuffer;
            }

            if (CurrentLineIndex >= _lines.Count - 1)
            {
                CurrentLineIndex = _lines.Count - 1;
                return ReviewBufferMoveResult.EndOfBuffer;
            }

            CurrentLineIndex++;
            return ReviewBufferMoveResult.Moved;
        }

        public ReviewBufferMoveResult MoveFirstLine()
        {
            if (_lines.Count == 0)
            {
                CurrentLineIndex = 0;
                return ReviewBufferMoveResult.BeginningOfBuffer;
            }

            CurrentLineIndex = 0;
            return ReviewBufferMoveResult.Moved;
        }

        public ReviewBufferMoveResult MoveLastLine()
        {
            if (_lines.Count == 0)
            {
                CurrentLineIndex = 0;
                return ReviewBufferMoveResult.EndOfBuffer;
            }

            CurrentLineIndex = _lines.Count - 1;
            return ReviewBufferMoveResult.Moved;
        }

        private bool AddLineWithoutMoving(string line)
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                return false;
            }

            _lines.Add(line.Trim());
            return true;
        }

        private static string GetLabel(ReviewBufferKind kind)
        {
            switch (kind)
            {
                case ReviewBufferKind.Ui:
                    return ModText.Get(ModStrings.UI.ReviewBufferUi);
                case ReviewBufferKind.AdventureMapNotifications:
                    return ModText.Get(ModStrings.UI.ReviewBufferNotifications);
                case ReviewBufferKind.CombatEvents:
                    return ModText.Get(ModStrings.UI.ReviewBufferEvents);
                default:
                    return kind.ToString();
            }
        }
    }
}
