using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Buffers
{
    public enum ReviewBufferKind
    {
        Ui,
        AdventureMapNotifications,
        CombatEvents
    }

    public enum ReviewBufferMoveResult
    {
        Moved,
        BeginningOfBuffer,
        EndOfBuffer
    }

    public sealed class ReviewBuffer
    {
        /// <summary>
        /// How many lines a buffer keeps. A session can announce without limit, so the oldest line
        /// is dropped once the buffer is full; the cursor moves down with it and keeps pointing at
        /// the line it was on. Generous enough that reviewing a whole battle or a whole turn still
        /// reaches the start of it.
        /// </summary>
        public const int MaxLines = 2000;

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
            if (_lines.Count > MaxLines)
            {
                int dropped = _lines.Count - MaxLines;
                _lines.RemoveRange(0, dropped);
                CurrentLineIndex = CurrentLineIndex > dropped ? CurrentLineIndex - dropped : 0;
            }

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
