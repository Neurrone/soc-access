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
        /// the dialog without confirming can put it back. The enabled flag is not in it: it is ticked
        /// in the glossary's table, not in the dialog.</summary>
        public static CueTuning SnapshotCue(string cueKey)
        {
            return new CueTuning(
                GetCueVolume(cueKey),
                GetCuePitchSemitones(cueKey),
                GetCueDurationScale(cueKey));
        }

        public static void RestoreCue(string cueKey, CueTuning tuning)
        {
            SetCueVolume(cueKey, tuning.Volume);
            SetCuePitchSemitones(cueKey, tuning.PitchSemitones);
            SetCueDurationScale(cueKey, tuning.DurationScale);
        }

        /// <summary>The cue's sound back to its default. Whether the cue plays at all is left alone:
        /// that is the glossary table's checkbox and not one of the defaults this button restores.
        /// </summary>
        public static void ResetCue(string cueKey)
        {
            AudioCueConfig config = GetAudioCueConfig(cueKey);
            if (config == null)
            {
                return;
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

        /// <summary>The bookmark beacon's volume, on the same scale as a cue's and defaulting to
        /// full. It multiplies the distance volume a beacon voice already carries; the tile-cue
        /// master switch never touches beacons.</summary>
        public static int GetBeaconVolume()
        {
            return _beaconVolume != null ? _beaconVolume.Value : BeaconVolumeDefault;
        }

        public static void SetBeaconVolume(int value)
        {
            if (_beaconVolume == null)
            {
                return;
            }

            _beaconVolume.Value = Clamp(value, CueVolumeMinimum, CueVolumeMaximum);
            _config?.Save();
        }

        /// <summary>The volume over every sound the mod plays, cues and beacon alike: what a cue or
        /// the beacon at 100 plays at. On the scale of the game's own sliders, which is linear gain -
        /// the game's Init.bnk drives bus volume from MasterVolume, MusicVolume and SFXVolume with a
        /// two-point linear curve in Wwise's dB scaling, gain = slider, and <c>SoundManager</c> sets
        /// <c>AudioSource.volume</c> to the same product for its own Unity clips - so 50 here is as
        /// much quieter than full as the game's SFX slider at 50%.</summary>
        public static int GetModVolume()
        {
            return _modVolume != null ? _modVolume.Value : ModVolumeDefault;
        }

        /// <summary><see cref="GetModVolume"/> as the gain an <c>AudioSource.volume</c> takes.</summary>
        public static float ModGain
        {
            get { return GetModVolume() / 100f; }
        }

        public static void SetModVolume(int value)
        {
            if (_modVolume == null)
            {
                return;
            }

            _modVolume.Value = Clamp(value, CueVolumeMinimum, CueVolumeMaximum);
            _config?.Save();
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
