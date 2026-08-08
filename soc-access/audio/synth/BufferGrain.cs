// Slice model and interpolation ported from the Tanglebeep Tangledeep accessibility mod by
// Austin Hicks, used with the author's permission. Adapted to this repo's style and C# 7.3,
// and widened to keep the original whole-array constructor.
using System;

namespace SongsOfConquestAccess.Audio.Synth
{
    /// <summary>
    /// A grain backed by a slice of an existing sample array: a noise pool slice now, decoded WAV
    /// data later. The slice is (array, offset, count) rather than a Span, which is a ref struct and
    /// cannot be a field, or a Memory, which would pull an extra net472 package.
    ///
    /// <para>The grain carries the sample rate its data was generated at and interpolates linearly
    /// on read, so it is correct whether the data matches the render rate (interpolation is a no-op)
    /// or not. Interpolation never reads past the slice, so one slice cannot bleed into the next,
    /// which is what keeps left/right noise slices decorrelated.</para>
    /// </summary>
    internal sealed class BufferGrain : Grain
    {
        private readonly float[] _data;
        private readonly int _offset;
        private readonly int _count;
        private readonly int _sourceSampleRate;
        private readonly float _durationSeconds;

        public BufferGrain(float[] monoSamples, int sourceSampleRate)
            : this(monoSamples, 0, monoSamples == null ? 0 : monoSamples.Length, sourceSampleRate)
        {
        }

        public BufferGrain(float[] data, int offset, int count, int sourceSampleRate)
        {
            if (data == null)
            {
                throw new ArgumentNullException("data");
            }

            if (sourceSampleRate <= 0)
            {
                throw new ArgumentOutOfRangeException("sourceSampleRate");
            }

            if (offset < 0 || count < 0 || offset + count > data.Length)
            {
                throw new ArgumentOutOfRangeException("count");
            }

            _data = data;
            _offset = offset;
            _count = count;
            _sourceSampleRate = sourceSampleRate;
            _durationSeconds = count / (float)sourceSampleRate;
        }

        public float[] Data
        {
            get { return _data; }
        }

        public int Offset
        {
            get { return _offset; }
        }

        public int Count
        {
            get { return _count; }
        }

        public int SourceSampleRate
        {
            get { return _sourceSampleRate; }
        }

        public override float DurationSeconds
        {
            get { return _durationSeconds; }
        }

        public override float Evaluate(float seconds)
        {
            if (seconds < 0f || _count == 0)
            {
                return 0f;
            }

            double position = seconds * (double)_sourceSampleRate;
            int index = (int)position;
            if (index >= _count)
            {
                return 0f;
            }

            float a = _data[_offset + index];
            if (index + 1 >= _count)
            {
                return a;
            }

            float b = _data[_offset + index + 1];
            return (float)(a + (b - a) * (position - index));
        }
    }
}
