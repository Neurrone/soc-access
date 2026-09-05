using System.Collections.Generic;

namespace SongsOfConquestAccess.Audio.Synth
{
    public enum CueWaveform
    {
        Sine,
        Triangle,
        Noise
    }

    /// <summary>
    /// One layer of a cue. Every sonic property is a plain mutable field so a tuning UI
    /// can edit it directly and re-render.
    /// </summary>
    public sealed class CueSegment
    {
        public CueWaveform Waveform = CueWaveform.Sine;

        /// <summary>Tone frequency, or the bandpass centre frequency for noise.</summary>
        public float FrequencyHz = 440f;

        /// <summary>Onset relative to the start of the cue.</summary>
        public float StartMs;

        public float DurationMs = 120f;

        /// <summary>Linear gain, normally 0..1.</summary>
        public float Gain = 1f;

        /// <summary>Per-segment pan offset in [-1, 1]; usually 0.</summary>
        public float Pan;

        public float AttackMs = 5f;

        public float ReleaseMs = 20f;

        /// <summary>Bandpass Q; used only when <see cref="Waveform"/> is Noise.</summary>
        public float NoiseQ = NoiseGrain.DefaultQ;

        /// <summary>Pitch offset applied at render time as varispeed.</summary>
        public float RateSemitones;

        public CueSegment Clone()
        {
            CueSegment clone = new CueSegment();
            clone.Waveform = Waveform;
            clone.FrequencyHz = FrequencyHz;
            clone.StartMs = StartMs;
            clone.DurationMs = DurationMs;
            clone.Gain = Gain;
            clone.Pan = Pan;
            clone.AttackMs = AttackMs;
            clone.ReleaseMs = ReleaseMs;
            clone.NoiseQ = NoiseQ;
            clone.RateSemitones = RateSemitones;
            return clone;
        }
    }

    public sealed class CueSpec
    {
        public string Name;

        public float MasterGain = 1f;

        public List<CueSegment> Segments = new List<CueSegment>();

        public CueSpec()
        {
        }

        public CueSpec(string name, float masterGain)
        {
            Name = name;
            MasterGain = masterGain;
        }

        public CueSpec Clone()
        {
            CueSpec clone = new CueSpec(Name, MasterGain);
            if (Segments != null)
            {
                for (int i = 0; i < Segments.Count; i++)
                {
                    CueSegment segment = Segments[i];
                    clone.Segments.Add(segment == null ? null : segment.Clone());
                }
            }

            return clone;
        }
    }
}
