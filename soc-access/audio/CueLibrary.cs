using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Audio.Synth;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Audio
{
    internal enum CueCategory
    {
        Terrain,
        Overworld,
        Combat
    }

    internal sealed class CueDefinition
    {
        public CueDefinition(string key, CueCategory category, ModString name, CueSpec defaultSpec)
        {
            Key = key;
            Category = category;
            Name = name;
            DefaultSpec = defaultSpec;
        }

        public string Key { get; }

        public CueCategory Category { get; }

        public ModString Name { get; }

        /// <summary>Untuned spec; callers that mutate it must clone first.</summary>
        public CueSpec DefaultSpec { get; }
    }

    /// <summary>
    /// The registry of every tile cue: key, category, localized name and default sound.
    /// Adding a cue is one entry here plus one ModString.
    /// </summary>
    internal static class CueLibrary
    {
        public const string TerrainRoad = "terrain_road";
        public const string TerrainGround = "terrain_ground";
        public const string TerrainSand = "terrain_sand";
        public const string TerrainWater = "terrain_water";
        public const string TerrainTrees = "terrain_trees";
        public const string TerrainImpassable = "terrain_impassable";
        public const string TerrainUnexplored = "terrain_unexplored";
        public const string EntityFriendly = "entity_friendly";
        public const string EntityEnemy = "entity_enemy";
        public const string MoveDenied = "move_denied";
        public const string SweepWielder = "sweep_wielder";
        public const string SweepSettlement = "sweep_settlement";
        public const string SweepResource = "sweep_resource";
        public const string SweepPickup = "sweep_pickup";
        public const string HexEmpty = "hex_empty";
        public const string HexElevation1 = "hex_elevation_1";
        public const string HexElevation2 = "hex_elevation_2";
        public const string HexElevation3 = "hex_elevation_3";
        public const string HexDanger = "hex_danger";
        public const string HexActive = "hex_active";

        private static readonly List<CueDefinition> _allCues = BuildCues();
        private static readonly Dictionary<string, CueDefinition> _cuesByKey = BuildIndex(_allCues);

        /// <summary>Glossary order: grouped by category, terrain first.</summary>
        public static IReadOnlyList<CueDefinition> AllCues
        {
            get { return _allCues; }
        }

        public static CueDefinition GetCue(string key)
        {
            if (string.IsNullOrEmpty(key))
            {
                return null;
            }

            CueDefinition definition;
            return _cuesByKey.TryGetValue(key, out definition) ? definition : null;
        }

        public static ModString GetCategoryName(CueCategory category)
        {
            switch (category)
            {
                case CueCategory.Overworld:
                    return ModStrings.Audio.CategoryOverworld;
                case CueCategory.Combat:
                    return ModStrings.Audio.CategoryCombat;
                default:
                    return ModStrings.Audio.CategoryTerrain;
            }
        }

        /// <summary>
        /// Plays the cue flat and centred with the user's live settings applied. The clip
        /// cache is keyed on the cue key alone, so every setter that changes a cue must
        /// call <see cref="SynthCuePlayer.InvalidateCache"/> for it.
        /// </summary>
        /// <summary>Silence between a cue and one marked <see cref="TileCue.FollowsPrevious"/>.</summary>
        public const float StackGapSeconds = 0.04f;

        public static void PlayCue(string key)
        {
            PlayCue(key, 0f, 0f);
        }

        /// <summary>Extra semitones stack on the user's pitch setting (e.g. hex elevation).</summary>
        public static void PlayCue(string key, float extraSemitones)
        {
            PlayCue(key, extraSemitones, 0f);
        }

        public static void PlayCue(string key, float extraSemitones, float delaySeconds)
        {
            PlayCue(key, extraSemitones, delaySeconds, 0f, 1f);
        }

        /// <summary>Directional play: pan is absolute, gain multiplies the user's volume setting.</summary>
        public static void PlayCue(string key, float extraSemitones, float delaySeconds, float panOffset, float gainScale)
        {
            if (!ModSettings.TileCuesEnabled || !ModSettings.GetCueEnabled(key))
            {
                return;
            }

            CueSpec spec = GetEffectiveSpec(key);
            if (spec == null)
            {
                return;
            }

            SynthCuePlayer.Play(
                key,
                spec,
                panOffset,
                ModSettings.GetCueVolume(key) / 100f * gainScale,
                ModSettings.GetCuePitchSemitones(key) + extraSemitones,
                delaySeconds);
        }

        /// <summary>
        /// Plays a tile-cue stack; a cue marked FollowsPrevious is serialized after the cue
        /// before it (same-timbre cues fired together fuse into one sound), and later
        /// unmarked cues keep that accumulated delay so they stay aligned with it.
        /// </summary>
        public static void PlayCues(IReadOnlyList<TileCue> cues)
        {
            PlayCues(cues, 0f, 1f, 0f);
        }

        /// <summary>
        /// Plays the stack with a direction encoded on top of it: <paramref name="semitoneOffset"/>
        /// adds to each cue's own pitch offset and <paramref name="gainScale"/> multiplies the
        /// user's per-cue volume. Serialization delays are unaffected.
        /// </summary>
        public static void PlayCues(IReadOnlyList<TileCue> cues, float panOffset, float gainScale, float semitoneOffset)
        {
            if (cues == null)
            {
                return;
            }

            float[] delays = ComputeDelaySeconds(cues, GetAudibleDurationSeconds);
            for (int i = 0; i < cues.Count; i++)
            {
                PlayCue(cues[i].Key, cues[i].Semitones + semitoneOffset, delays[i], panOffset, gainScale);
            }
        }

        /// <summary>Pure delay planning; the duration lookup returns 0 for cues that will not sound.</summary>
        internal static float[] ComputeDelaySeconds(IReadOnlyList<TileCue> cues, Func<string, float> durationSeconds)
        {
            float[] delays = new float[cues.Count];
            float accumulated = 0f;
            for (int i = 0; i < cues.Count; i++)
            {
                if (cues[i].FollowsPrevious && i > 0)
                {
                    float previous = durationSeconds(cues[i - 1].Key);
                    if (previous > 0f)
                    {
                        accumulated += previous + StackGapSeconds;
                    }
                }

                delays[i] = accumulated;
            }

            return delays;
        }

        /// <summary>
        /// How long the whole serialized stack sounds, for callers that must not start another
        /// stack on top of it. Pure; the live lookup is <see cref="GetStackDurationSeconds"/>.
        /// </summary>
        internal static float ComputeStackDurationSeconds(IReadOnlyList<TileCue> cues, Func<string, float> durationSeconds)
        {
            if (cues == null || cues.Count == 0)
            {
                return 0f;
            }

            float[] delays = ComputeDelaySeconds(cues, durationSeconds);
            float end = 0f;
            for (int i = 0; i < cues.Count; i++)
            {
                float finish = delays[i] + durationSeconds(cues[i].Key);
                if (finish > end)
                {
                    end = finish;
                }
            }

            return end;
        }

        public static float GetStackDurationSeconds(IReadOnlyList<TileCue> cues)
        {
            return ComputeStackDurationSeconds(cues, GetAudibleDurationSeconds);
        }

        /// <summary>Zero for cues the user has silenced, so they take no room in a schedule.</summary>
        internal static float GetAudibleDurationSeconds(string key)
        {
            if (!ModSettings.TileCuesEnabled || !ModSettings.GetCueEnabled(key))
            {
                return 0f;
            }

            CueSpec spec = GetEffectiveSpec(key);
            if (spec == null)
            {
                return 0f;
            }

            float endMs = 0f;
            for (int i = 0; i < spec.Segments.Count; i++)
            {
                float segmentEnd = spec.Segments[i].StartMs + spec.Segments[i].DurationMs;
                if (segmentEnd > endMs)
                {
                    endMs = segmentEnd;
                }
            }

            return endMs / 1000f;
        }

        /// <summary>Default spec with the duration setting applied; volume and pitch are render offsets.</summary>
        internal static CueSpec GetEffectiveSpec(string key)
        {
            return GetEffectiveSpec(key, ModSettings.GetCueDurationScale(key));
        }

        internal static CueSpec GetEffectiveSpec(string key, int durationScale)
        {
            CueDefinition definition = GetCue(key);
            if (definition == null || definition.DefaultSpec == null)
            {
                return null;
            }

            CueSpec spec = definition.DefaultSpec.Clone();
            float scale = durationScale / 100f;
            if (scale <= 0f || scale == 1f)
            {
                return spec;
            }

            for (int i = 0; i < spec.Segments.Count; i++)
            {
                CueSegment segment = spec.Segments[i];
                if (segment == null)
                {
                    continue;
                }

                segment.StartMs = segment.StartMs * scale;
                segment.DurationMs = segment.DurationMs * scale;
            }

            return spec;
        }

        private static List<CueDefinition> BuildCues()
        {
            List<CueDefinition> cues = new List<CueDefinition>();

            cues.Add(new CueDefinition(
                TerrainRoad,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainRoad,
                Spec(TerrainRoad, Tone(CueWaveform.Triangle, 660f, 0f, 45f, 4f, 18f))));
            cues.Add(new CueDefinition(
                TerrainGround,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainGround,
                Spec(TerrainGround, Band(400f, 8f, 0f, 55f, 5f, 25f))));
            cues.Add(new CueDefinition(
                TerrainSand,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainSand,
                Spec(TerrainSand, Band(280f, 6f, 0f, 70f, 6f, 35f))));
            cues.Add(new CueDefinition(
                TerrainWater,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainWater,
                Spec(
                    TerrainWater,
                    Tone(CueWaveform.Sine, 520f, 0f, 35f, 4f, 12f),
                    Tone(CueWaveform.Sine, 620f, 40f, 35f, 4f, 12f))));
            cues.Add(new CueDefinition(
                TerrainTrees,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainTrees,
                Spec(TerrainTrees, Band(900f, 20f, 0f, 60f, 5f, 25f))));
            cues.Add(new CueDefinition(
                TerrainImpassable,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainImpassable,
                Spec(TerrainImpassable, Tone(CueWaveform.Sine, 150f, 0f, 90f, 5f, 40f))));
            cues.Add(new CueDefinition(
                TerrainUnexplored,
                CueCategory.Terrain,
                ModStrings.Audio.TerrainUnexplored,
                Spec(
                    TerrainUnexplored,
                    Tone(CueWaveform.Sine, 220f, 0f, 25f, 4f, 10f),
                    Tone(CueWaveform.Sine, 220f, 90f, 25f, 4f, 10f))));

            // Affiliation is one sound language: these also mark troops on the battlefield, so
            // they keep the context-free names "Ally" and "Enemy".
            cues.Add(new CueDefinition(
                EntityFriendly,
                CueCategory.Overworld,
                ModStrings.Audio.EntityFriendly,
                Spec(EntityFriendly, Tone(CueWaveform.Sine, 784f, 0f, 45f, 4f, 18f))));
            cues.Add(new CueDefinition(
                EntityEnemy,
                CueCategory.Overworld,
                ModStrings.Audio.EntityEnemy,
                Spec(
                    EntityEnemy,
                    Tone(CueWaveform.Triangle, 587f, 0f, 30f, 4f, 12f),
                    Tone(CueWaveform.Triangle, 587f, 40f, 30f, 4f, 12f))));
            // Sonar sweep voices. Each names a category and is immediately followed at the same
            // pan by an entity_* affiliation marker, so all four avoid the affiliation timbres:
            // a low rising triangle horn, a static sine fifth, and two very high sine blips.
            cues.Add(new CueDefinition(
                SweepWielder,
                CueCategory.Overworld,
                ModStrings.Audio.SweepWielder,
                Spec(
                    SweepWielder,
                    Tone(CueWaveform.Triangle, 392f, 0f, 30f, 4f, 10f),
                    Tone(CueWaveform.Triangle, 523f, 30f, 35f, 4f, 14f))));
            cues.Add(new CueDefinition(
                SweepSettlement,
                CueCategory.Overworld,
                ModStrings.Audio.SweepSettlement,
                Spec(
                    SweepSettlement,
                    Tone(CueWaveform.Sine, 392f, 0f, 60f, 6f, 24f),
                    Tone(CueWaveform.Sine, 588f, 0f, 60f, 6f, 24f))));
            cues.Add(new CueDefinition(
                SweepResource,
                CueCategory.Overworld,
                ModStrings.Audio.SweepResource,
                Spec(
                    SweepResource,
                    Tone(CueWaveform.Sine, 1319f, 0f, 18f, 4f, 6f),
                    Tone(CueWaveform.Sine, 1319f, 26f, 18f, 4f, 8f))));
            cues.Add(new CueDefinition(
                SweepPickup,
                CueCategory.Overworld,
                ModStrings.Audio.SweepPickup,
                Spec(SweepPickup, Tone(CueWaveform.Sine, 1568f, 0f, 40f, 4f, 16f))));

            cues.Add(new CueDefinition(
                MoveDenied,
                CueCategory.Overworld,
                ModStrings.Audio.MoveDenied,
                Spec(
                    MoveDenied,
                    Tone(CueWaveform.Sine, 180f, 0f, 35f, 4f, 8f),
                    Tone(CueWaveform.Sine, 120f, 35f, 35f, 4f, 20f))));

            cues.Add(new CueDefinition(
                HexEmpty,
                CueCategory.Combat,
                ModStrings.Audio.HexEmpty,
                Spec(HexEmpty, Tone(CueWaveform.Sine, 520f, 0f, 35f, 4f, 14f))));
            cues.Add(new CueDefinition(
                HexElevation1,
                CueCategory.Combat,
                ModStrings.Audio.HexElevation1,
                ElevatedHexSpec(HexElevation1, 4f)));
            cues.Add(new CueDefinition(
                HexElevation2,
                CueCategory.Combat,
                ModStrings.Audio.HexElevation2,
                ElevatedHexSpec(HexElevation2, 8f)));
            cues.Add(new CueDefinition(
                HexElevation3,
                CueCategory.Combat,
                ModStrings.Audio.HexElevation3,
                ElevatedHexSpec(HexElevation3, 12f)));
            // A falling tritone: the one dissonant interval in the set, so danger never reads
            // as a variant of another cue.
            cues.Add(new CueDefinition(
                HexDanger,
                CueCategory.Combat,
                ModStrings.Audio.HexDanger,
                Spec(
                    HexDanger,
                    Tone(CueWaveform.Triangle, 566f, 0f, 30f, 4f, 12f),
                    Tone(CueWaveform.Triangle, 400f, 35f, 30f, 4f, 12f))));
            cues.Add(new CueDefinition(
                HexActive,
                CueCategory.Combat,
                ModStrings.Audio.HexActive,
                Spec(
                    HexActive,
                    Tone(CueWaveform.Sine, 880f, 0f, 30f, 4f, 12f),
                    Tone(CueWaveform.Sine, 880f, 40f, 30f, 4f, 12f))));

            return cues;
        }

        private static Dictionary<string, CueDefinition> BuildIndex(List<CueDefinition> cues)
        {
            Dictionary<string, CueDefinition> index = new Dictionary<string, CueDefinition>();
            for (int i = 0; i < cues.Count; i++)
            {
                index[cues[i].Key] = cues[i];
            }

            return index;
        }

        private static CueSpec Spec(string name, params CueSegment[] segments)
        {
            CueSpec spec = new CueSpec(name, 1f);
            for (int i = 0; i < segments.Length; i++)
            {
                spec.Segments.Add(segments[i]);
            }

            return spec;
        }

        private static CueSegment Tone(
            CueWaveform waveform,
            float frequencyHz,
            float startMs,
            float durationMs,
            float attackMs,
            float releaseMs)
        {
            CueSegment segment = new CueSegment();
            segment.Waveform = waveform;
            segment.FrequencyHz = frequencyHz;
            segment.StartMs = startMs;
            segment.DurationMs = durationMs;
            segment.AttackMs = attackMs;
            segment.ReleaseMs = releaseMs;
            return segment;
        }

        /// <summary>The empty-hex tick played as varispeed, so raised ground keeps the shortened
        /// duration a runtime pitch offset used to give it.</summary>
        private static CueSpec ElevatedHexSpec(string key, float rateSemitones)
        {
            CueSegment segment = Tone(CueWaveform.Sine, 520f, 0f, 35f, 4f, 14f);
            segment.RateSemitones = rateSemitones;
            return Spec(key, segment);
        }

        private static CueSegment Band(
            float frequencyHz,
            float noiseQ,
            float startMs,
            float durationMs,
            float attackMs,
            float releaseMs)
        {
            CueSegment segment = Tone(CueWaveform.Noise, frequencyHz, startMs, durationMs, attackMs, releaseMs);
            segment.NoiseQ = noiseQ;
            return segment;
        }
    }
}
