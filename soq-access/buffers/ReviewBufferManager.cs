using System.Collections.Generic;

namespace SongsOfConquestAccess.Buffers
{
    internal sealed class ReviewBufferManager
    {
        private readonly ReviewBufferKind[] _order =
        {
            ReviewBufferKind.Ui,
            ReviewBufferKind.AdventureMapNotifications,
            ReviewBufferKind.CombatEvents
        };

        private readonly Dictionary<ReviewBufferKind, ReviewBuffer> _buffers =
            new Dictionary<ReviewBufferKind, ReviewBuffer>();

        private ReviewBufferKind _currentKind = ReviewBufferKind.Ui;

        public ReviewBufferManager()
        {
            Add(new ReviewBuffer(ReviewBufferKind.Ui, followLatest: false));
            Add(new ReviewBuffer(ReviewBufferKind.AdventureMapNotifications, followLatest: true));
            Add(new ReviewBuffer(ReviewBufferKind.CombatEvents, followLatest: true));
            SetVisibleBuffers(new[] { ReviewBufferKind.Ui });
        }

        public ReviewBuffer CurrentBuffer
        {
            get
            {
                ReviewBuffer buffer;
                if (!_buffers.TryGetValue(_currentKind, out buffer) || !buffer.IsVisible)
                {
                    _currentKind = FirstVisibleKind();
                    buffer = _buffers[_currentKind];
                }

                return buffer;
            }
        }

        public void SetVisibleBuffers(IEnumerable<ReviewBufferKind> kinds)
        {
            HashSet<ReviewBufferKind> visible = new HashSet<ReviewBufferKind>();
            visible.Add(ReviewBufferKind.Ui);

            if (kinds != null)
            {
                foreach (ReviewBufferKind kind in kinds)
                {
                    visible.Add(kind);
                }
            }

            foreach (ReviewBufferKind kind in _order)
            {
                ReviewBuffer buffer = _buffers[kind];
                buffer.IsVisible = visible.Contains(kind);
            }

            ReviewBuffer current;
            if (!_buffers.TryGetValue(_currentKind, out current) || !current.IsVisible)
            {
                _currentKind = FirstVisibleKind();
            }
        }

        public void ReplaceLines(ReviewBufferKind kind, IEnumerable<string> lines)
        {
            ReviewBuffer buffer;
            if (_buffers.TryGetValue(kind, out buffer))
            {
                buffer.ReplaceLines(lines);
            }
        }

        public void AppendLine(ReviewBufferKind kind, string line)
        {
            ReviewBuffer buffer;
            if (_buffers.TryGetValue(kind, out buffer))
            {
                buffer.AppendLine(line);
            }
        }

        public void Clear(ReviewBufferKind kind)
        {
            ReviewBuffer buffer;
            if (_buffers.TryGetValue(kind, out buffer))
            {
                buffer.Clear();
            }
        }

        public void SetCurrentBuffer(ReviewBufferKind kind)
        {
            ReviewBuffer buffer;
            if (!_buffers.TryGetValue(kind, out buffer) || !buffer.IsVisible)
            {
                return;
            }

            if (buffer.FollowLatest)
            {
                buffer.MoveLastLine();
            }

            _currentKind = kind;
        }

        public ReviewBuffer MovePreviousVisibleBuffer()
        {
            return MoveVisibleBuffer(-1);
        }

        public ReviewBuffer MoveNextVisibleBuffer()
        {
            return MoveVisibleBuffer(1);
        }

        public ReviewBufferLineMove MovePreviousBufferLine()
        {
            ReviewBuffer buffer = CurrentBuffer;
            return new ReviewBufferLineMove(buffer, buffer.MovePreviousLine());
        }

        public ReviewBufferLineMove MoveNextBufferLine()
        {
            ReviewBuffer buffer = CurrentBuffer;
            return new ReviewBufferLineMove(buffer, buffer.MoveNextLine());
        }

        public ReviewBufferLineMove MoveFirstBufferLine()
        {
            ReviewBuffer buffer = CurrentBuffer;
            return new ReviewBufferLineMove(buffer, buffer.MoveFirstLine());
        }

        public ReviewBufferLineMove MoveLastBufferLine()
        {
            ReviewBuffer buffer = CurrentBuffer;
            return new ReviewBufferLineMove(buffer, buffer.MoveLastLine());
        }

        private void Add(ReviewBuffer buffer)
        {
            _buffers[buffer.Kind] = buffer;
        }

        private ReviewBuffer MoveVisibleBuffer(int delta)
        {
            int currentIndex = IndexOf(_currentKind);
            if (currentIndex < 0)
            {
                currentIndex = 0;
            }

            int index = currentIndex;
            do
            {
                index += delta;
                if (index < 0)
                {
                    index = _order.Length - 1;
                }
                else if (index >= _order.Length)
                {
                    index = 0;
                }

                ReviewBuffer buffer = _buffers[_order[index]];
                if (buffer.IsVisible)
                {
                    _currentKind = buffer.Kind;
                    if (buffer.FollowLatest)
                    {
                        buffer.MoveLastLine();
                    }

                    return buffer;
                }
            }
            while (index != currentIndex);

            return CurrentBuffer;
        }

        private ReviewBufferKind FirstVisibleKind()
        {
            for (int i = 0; i < _order.Length; i++)
            {
                ReviewBuffer buffer = _buffers[_order[i]];
                if (buffer.IsVisible)
                {
                    return buffer.Kind;
                }
            }

            return ReviewBufferKind.Ui;
        }

        private int IndexOf(ReviewBufferKind kind)
        {
            for (int i = 0; i < _order.Length; i++)
            {
                if (_order[i] == kind)
                {
                    return i;
                }
            }

            return -1;
        }
    }

    internal sealed class ReviewBufferLineMove
    {
        public ReviewBufferLineMove(ReviewBuffer buffer, ReviewBufferMoveResult result)
        {
            Buffer = buffer;
            Result = result;
        }

        public ReviewBuffer Buffer { get; private set; }

        public ReviewBufferMoveResult Result { get; private set; }
    }
}
