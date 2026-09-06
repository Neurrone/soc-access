namespace SongsOfConquestAccess.Audio
{
    /// <summary>
    /// EVERYTHING ABOUT ONE CUE THAT THE PLAYER CAN CHANGE, as a value.
    ///
    /// The cue dialog changes a cue while the player listens, which is the whole point of it: every
    /// move of a slider replays the sound. That makes Cancel a real question - what the player heard
    /// has already been written - and this is the answer: the tuning is taken when the dialog opens
    /// and written back when the player leaves without confirming.
    /// </summary>
    public struct CueTuning
    {
        public CueTuning(bool enabled, int volume, int pitchSemitones, int durationScale)
        {
            Enabled = enabled;
            Volume = volume;
            PitchSemitones = pitchSemitones;
            DurationScale = durationScale;
        }

        public bool Enabled { get; private set; }

        public int Volume { get; private set; }

        public int PitchSemitones { get; private set; }

        public int DurationScale { get; private set; }
    }
}
