namespace SongsOfConquestAccess.Audio
{
    /// <summary>A cue key plus a pitch offset applied on top of the cue's user pitch setting.</summary>
    public readonly struct TileCue
    {
        public TileCue(string key, float semitones)
            : this(key, semitones, false)
        {
        }

        public TileCue(string key, float semitones, bool followsPrevious)
        {
            Key = key;
            Semitones = semitones;
            FollowsPrevious = followsPrevious;
        }

        public string Key { get; }

        public float Semitones { get; }

        /// <summary>Starts after the previous cue in the stack finishes instead of alongside it.</summary>
        public bool FollowsPrevious { get; }
    }
}
