using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Buffers
{
    internal sealed class ReviewBufferController
    {
        private const string BufferEmpty = "Buffer empty";

        private readonly ReviewBufferManager _buffers;

        public ReviewBufferController(ReviewBufferManager buffers)
        {
            _buffers = buffers;
        }

        public void PreviousBuffer()
        {
            SpeakBuffer(_buffers.MovePreviousVisibleBuffer());
        }

        public void NextBuffer()
        {
            SpeakBuffer(_buffers.MoveNextVisibleBuffer());
        }

        public void PreviousBufferLine()
        {
            SpeakMove(_buffers.MovePreviousBufferLine());
        }

        public void NextBufferLine()
        {
            SpeakMove(_buffers.MoveNextBufferLine());
        }

        public void FirstBufferLine()
        {
            SpeakMove(_buffers.MoveFirstBufferLine());
        }

        public void LastBufferLine()
        {
            SpeakMove(_buffers.MoveLastBufferLine());
        }

        private static void SpeakBuffer(ReviewBuffer buffer)
        {
            if (buffer == null)
            {
                return;
            }

            string line = buffer.CurrentLine;
            if (string.IsNullOrWhiteSpace(line))
            {
                Speak(buffer.Label + ". " + BufferEmpty);
                return;
            }

            Speak(buffer.Label + ". " + line);
        }

        private static void SpeakMove(ReviewBufferLineMove move)
        {
            if (move == null || move.Buffer == null)
            {
                return;
            }

            string line = move.Buffer.CurrentLine;
            if (string.IsNullOrWhiteSpace(line))
            {
                Speak(BufferEmpty);
                return;
            }

            Speak(line);
        }

        private static void Speak(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
        }
    }
}
