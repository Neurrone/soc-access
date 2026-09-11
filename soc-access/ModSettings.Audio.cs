using BepInEx.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech.Spatial;

namespace SongsOfConquestAccess
{
    public static partial class ModSettings
    {
        public static bool GetCueEnabled(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config == null || config.Enabled == null || config.Enabled.Value;
        }

        public static int GetCueVolume(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config != null && config.Volume != null ? config.Volume.Value : CueVolumeDefault;
        }

        public static int GetCuePitchSemitones(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config != null && config.PitchSemitones != null
                ? config.PitchSemitones.Value
                : CuePitchSemitonesDefault;
        }

        public static int GetCueDurationScale(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            return config != null && config.DurationScale != null
                ? config.DurationScale.Value
                : CueDurationScaleDefault;
        }

        public static void SetCueEnabled(string cueKey, bool value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.Enabled == null)
            {
                return;
            }

            config.Enabled.Value = value;
            SaveAndInvalidateCue(cueKey);
        }

        public static void SetCueVolume(string cueKey, int value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.Volume == null)
            {
                return;
            }

            config.Volume.Value = Clamp(value, CueVolumeMinimum, CueVolumeMaximum);
            SaveAndInvalidateCue(cueKey);
        }

        public static void SetCuePitchSemitones(string cueKey, int value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.PitchSemitones == null)
            {
                return;
            }

            config.PitchSemitones.Value = Clamp(value, CuePitchSemitonesMinimum, CuePitchSemitonesMaximum);
            SaveAndInvalidateCue(cueKey);
        }

        public static void SetCueDurationScale(string cueKey, int value)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null || config.DurationScale == null)
            {
                return;
            }

            config.DurationScale.Value = Clamp(value, CueDurationScaleMinimum, CueDurationScaleMaximum);
            SaveAndInvalidateCue(cueKey);
        }

        /// <summary>Everything the cue dialog can change about one cue, taken as a value so leaving
        /// the dialog without confirming can put it back.</summary>
        public static CueTuning SnapshotCue(string cueKey)
        {
            return new CueTuning(
                GetCueEnabled(cueKey),
                GetCueVolume(cueKey),
                GetCuePitchSemitones(cueKey),
                GetCueDurationScale(cueKey));
        }

        public static void RestoreCue(string cueKey, CueTuning tuning)
        {
            SetCueEnabled(cueKey, tuning.Enabled);
            SetCueVolume(cueKey, tuning.Volume);
            SetCuePitchSemitones(cueKey, tuning.PitchSemitones);
            SetCueDurationScale(cueKey, tuning.DurationScale);
        }

        public static void ResetCue(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null)
            {
                return;
            }

            if (config.Enabled != null)
            {
                config.Enabled.Value = true;
            }

            if (config.Volume != null)
            {
                config.Volume.Value = CueVolumeDefault;
            }

            if (config.PitchSemitones != null)
            {
                config.PitchSemitones.Value = CuePitchSemitonesDefault;
            }

            if (config.DurationScale != null)
            {
                config.DurationScale.Value = CueDurationScaleDefault;
            }

            SaveAndInvalidateCue(cueKey);
        }

        private static void BindAudioCues(ConfigFile config)
        {
            _audioCues.Clear();
            IReadOnlyList<CueDefinition> cues = CueLibrary.AllCues;
            for (int i = 0; i < cues.Count; i++)
            {
                string cueKey = cues[i].Key;
                string prefix = ToConfigKeyPrefix(cueKey);
                _audioCues[cueKey] = new AudioCueConfig
                {
                    Enabled = config.Bind(
                        AudioSection,
                        prefix + "Enabled",
                        true,
                        "Whether this sound cue is played."),
                    Volume = config.Bind(
                        AudioSection,
                        prefix + "Volume",
                        CueVolumeDefault,
                        "Playback volume of this sound cue, from 0 to 100."),
                    PitchSemitones = config.Bind(
                        AudioSection,
                        prefix + "PitchSemitones",
                        CuePitchSemitonesDefault,
                        "Pitch offset of this sound cue in semitones, from -12 to 12."),
                    DurationScale = config.Bind(
                        AudioSection,
                        prefix + "DurationScale",
                        CueDurationScaleDefault,
                        "Length of this sound cue as a percentage of its default, from 50 to 200.")
                };
            }
        }

        private static AudioCueConfig GetAudioCueConfig(string cueKey)
        {
            return Lookup(_audioCues, cueKey);
        }

        private static void SaveAndInvalidateCue(string cueKey)
        {
            _config?.Save();
            SynthCuePlayer.InvalidateCache(cueKey);
        }

        private static int Clamp(int value, int minimum, int maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            return value > maximum ? maximum : value;
        }

        private sealed class AudioCueConfig
        {
            public ConfigEntry<bool> Enabled { get; set; }
            public ConfigEntry<int> Volume { get; set; }
            public ConfigEntry<int> PitchSemitones { get; set; }
            public ConfigEntry<int> DurationScale { get; set; }
        }
    }
}
