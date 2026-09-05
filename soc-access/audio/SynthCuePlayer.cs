using System;
using System.Collections.Generic;
using System.Globalization;
using SongsOfConquestAccess.Audio.Synth;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    /// <summary>
    /// Plays procedurally rendered cues through a small pool of stereo voices.
    /// Pan, gain and pitch offsets are baked into the rendered clip rather than applied
    /// on the AudioSource, so a cue always sounds the same regardless of engine mixer
    /// state; the offsets are quantised before rendering to keep the clip cache bounded.
    /// </summary>
    internal static class SynthCuePlayer
    {
        private const int VoiceCount = 8;
        private const int MaxCachedClips = 64;
        private const float PanQuantizeSteps = 20f;
        private const float GainQuantizeSteps = 20f;
        private const float SemitoneQuantizeSteps = 2f;

        private static readonly Dictionary<string, AudioClip> Clips = new Dictionary<string, AudioClip>();
        private static readonly List<string> ClipOrder = new List<string>();

        private static GameObject _gameObject;
        private static AudioSource[] _voices;
        private static long[] _voiceSequence;
        private static long _sequence;

        public static void Play(string cacheKey, CueSpec spec, float panOffset = 0f, float gainScale = 1f, float rateSemitoneOffset = 0f, float delaySeconds = 0f)
        {
            try
            {
                if (string.IsNullOrEmpty(cacheKey) || spec == null)
                {
                    return;
                }

                float pan = Quantize(Clamp(panOffset, -1f, 1f), PanQuantizeSteps);
                float gain = Quantize(gainScale < 0f ? 0f : gainScale, GainQuantizeSteps);
                float semitones = Quantize(rateSemitoneOffset, SemitoneQuantizeSteps);

                AudioClip clip = GetOrCreateClip(cacheKey, spec, pan, gain, semitones);
                if (clip == null)
                {
                    return;
                }

                AudioSource source = TakeVoice();
                if (source == null)
                {
                    return;
                }

                if (source.isPlaying)
                {
                    source.Stop();
                }

                source.clip = clip;
                source.volume = 1f;
                source.pitch = 1f;
                source.panStereo = 0f;
                source.time = 0f;
                if (delaySeconds > 0f)
                {
                    source.PlayDelayed(delaySeconds);
                }
                else
                {
                    source.Play();
                }
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to play synthesised cue " + cacheKey + ": " + exception.Message);
            }
        }

        public static void InvalidateCache(string cacheKey)
        {
            if (string.IsNullOrEmpty(cacheKey))
            {
                return;
            }

            string prefix = cacheKey + "|";
            for (int i = ClipOrder.Count - 1; i >= 0; i--)
            {
                string key = ClipOrder[i];
                if (!key.StartsWith(prefix, StringComparison.Ordinal))
                {
                    continue;
                }

                RemoveClipAt(i);
            }
        }

        public static void InvalidateAll()
        {
            for (int i = ClipOrder.Count - 1; i >= 0; i--)
            {
                RemoveClipAt(i);
            }

            Clips.Clear();
            ClipOrder.Clear();
        }

        public static void DisposeAll()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }

            _voices = null;
            _voiceSequence = null;
            _sequence = 0;
            InvalidateAll();
        }

        private static AudioClip GetOrCreateClip(string cacheKey, CueSpec spec, float panOffset, float gainScale, float rateSemitoneOffset)
        {
            string key = BuildKey(cacheKey, panOffset, gainScale, rateSemitoneOffset);
            AudioClip cached;
            if (Clips.TryGetValue(key, out cached) && cached != null)
            {
                return cached;
            }

            float[] buffer = CueRenderer.Render(spec, panOffset, gainScale, rateSemitoneOffset);
            int frames = buffer.Length / 2;
            if (frames <= 0)
            {
                return null;
            }

            AudioClip clip = AudioClip.Create("SongsOfConquestAccess_Cue_" + key, frames, 2, GrainTimeline.SampleRate, false);
            clip.SetData(buffer, 0);

            if (Clips.ContainsKey(key))
            {
                Clips[key] = clip;
            }
            else
            {
                Clips.Add(key, clip);
                ClipOrder.Add(key);
            }

            while (ClipOrder.Count > MaxCachedClips)
            {
                if (ClipOrder[0] == key)
                {
                    break;
                }

                RemoveClipAt(0);
            }

            return clip;
        }

        private static void RemoveClipAt(int index)
        {
            if (index < 0 || index >= ClipOrder.Count)
            {
                return;
            }

            string key = ClipOrder[index];
            ClipOrder.RemoveAt(index);
            AudioClip clip;
            if (Clips.TryGetValue(key, out clip))
            {
                Clips.Remove(key);
                if (clip != null)
                {
                    UnityEngine.Object.Destroy(clip);
                }
            }
        }

        private static string BuildKey(string cacheKey, float panOffset, float gainScale, float rateSemitoneOffset)
        {
            return cacheKey
                + "|" + panOffset.ToString("0.###", CultureInfo.InvariantCulture)
                + "|" + gainScale.ToString("0.###", CultureInfo.InvariantCulture)
                + "|" + rateSemitoneOffset.ToString("0.###", CultureInfo.InvariantCulture);
        }

        private static AudioSource TakeVoice()
        {
            AudioSource[] voices = EnsureVoices();
            if (voices == null)
            {
                return null;
            }

            int chosen = -1;
            for (int i = 0; i < voices.Length; i++)
            {
                if (voices[i] != null && !voices[i].isPlaying)
                {
                    chosen = i;
                    break;
                }
            }

            if (chosen < 0)
            {
                long oldest = long.MaxValue;
                for (int i = 0; i < voices.Length; i++)
                {
                    if (voices[i] != null && _voiceSequence[i] < oldest)
                    {
                        oldest = _voiceSequence[i];
                        chosen = i;
                    }
                }
            }

            if (chosen < 0)
            {
                return null;
            }

            _sequence++;
            _voiceSequence[chosen] = _sequence;
            return voices[chosen];
        }

        private static AudioSource[] EnsureVoices()
        {
            if (_voices != null && _gameObject != null)
            {
                return _voices;
            }

            _gameObject = new GameObject("SongsOfConquestAccess_SynthCuePlayer");
            _gameObject.hideFlags = HideFlags.HideInHierarchy;
            _voices = new AudioSource[VoiceCount];
            _voiceSequence = new long[VoiceCount];
            for (int i = 0; i < VoiceCount; i++)
            {
                AudioSource source = _gameObject.AddComponent<AudioSource>();
                source.loop = false;
                source.playOnAwake = false;
                source.spatialBlend = 0f;
                source.volume = 1f;
                source.pitch = 1f;
                source.panStereo = 0f;
                _voices[i] = source;
            }

            return _voices;
        }

        private static float Quantize(float value, float steps)
        {
            return (float)Math.Round(value * steps) / steps;
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }
    }
}
