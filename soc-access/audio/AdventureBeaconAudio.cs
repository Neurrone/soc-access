using System;
using System.Collections.Generic;
using System.IO;
using BepInEx;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    internal sealed class AdventureBeaconAudio : IDisposable
    {
        private const string BeaconFileName = "beacon.wav";
        private const int PanSaturationTiles = 12;
        private const int PitchMaxSemitones = 12;
        private const float AudibleDistanceTiles = 30f;

        private static readonly List<AdventureBeaconAudio> LiveInstances = new List<AdventureBeaconAudio>();

        private readonly Dictionary<string, BeaconVoice> _voices = new Dictionary<string, BeaconVoice>();
        private AudioClip _clip;
        private bool _isAudible = true;
        private bool _disposed;

        public AdventureBeaconAudio()
        {
            LiveInstances.Add(this);
        }

        public void SetAudible(bool isAudible, Vector2Int listener)
        {
            _isAudible = isAudible;
            if (_isAudible)
            {
                UpdateListener(listener);
            }
            else
            {
                StopAllSources();
            }
        }

        public bool IsActive(string slot)
        {
            BeaconVoice voice;
            return slot != null && _voices.TryGetValue(slot, out voice) && voice.IsActive;
        }

        public bool Toggle(string slot, Vector2Int target, Vector2Int listener)
        {
            if (IsActive(slot))
            {
                Stop(slot);
                return false;
            }

            Start(slot, target, listener);
            return true;
        }

        public void Start(string slot, Vector2Int target, Vector2Int listener)
        {
            if (string.IsNullOrWhiteSpace(slot) || _disposed)
            {
                return;
            }

            BeaconVoice voice = GetOrCreateVoice(slot);
            if (voice == null)
            {
                return;
            }

            voice.Target = target;
            voice.IsActive = true;
            ApplyParameters(voice, listener);
            if (_isAudible && voice.Source != null && !voice.Source.isPlaying)
            {
                voice.Source.Play();
            }
        }

        public void Stop(string slot)
        {
            BeaconVoice voice;
            if (slot == null || !_voices.TryGetValue(slot, out voice))
            {
                return;
            }

            voice.IsActive = false;
            if (voice.Source != null)
            {
                voice.Source.Stop();
            }
        }

        public void StopAll()
        {
            foreach (BeaconVoice voice in _voices.Values)
            {
                voice.IsActive = false;
                if (voice.Source != null)
                {
                    voice.Source.Stop();
                }
            }
        }

        public void UpdateListener(Vector2Int listener)
        {
            if (_disposed)
            {
                return;
            }

            foreach (BeaconVoice voice in _voices.Values)
            {
                if (!voice.IsActive)
                {
                    continue;
                }

                ApplyParameters(voice, listener);
                if (_isAudible && voice.Source != null && !voice.Source.isPlaying)
                {
                    voice.Source.Play();
                }
            }
        }

        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            LiveInstances.Remove(this);
            foreach (BeaconVoice voice in _voices.Values)
            {
                if (voice.GameObject != null)
                {
                    UnityEngine.Object.Destroy(voice.GameObject);
                }
            }

            _voices.Clear();
            if (_clip != null)
            {
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }
        }

        public static void DisposeAll()
        {
            AdventureBeaconAudio[] instances = LiveInstances.ToArray();
            for (int i = 0; i < instances.Length; i++)
            {
                instances[i]?.Dispose();
            }

            LiveInstances.Clear();
        }

        private void StopAllSources()
        {
            foreach (BeaconVoice voice in _voices.Values)
            {
                if (voice.Source != null)
                {
                    voice.Source.Stop();
                }
            }
        }

        private BeaconVoice GetOrCreateVoice(string slot)
        {
            BeaconVoice voice;
            if (_voices.TryGetValue(slot, out voice))
            {
                return voice;
            }

            AudioClip clip = EnsureClip();
            if (clip == null)
            {
                return null;
            }

            GameObject gameObject = new GameObject("SongsOfConquestAccess_Beacon_" + slot);
            gameObject.hideFlags = HideFlags.HideInHierarchy;
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.spatialBlend = 0f;
            source.volume = 0f;
            source.pitch = 1f;
            source.panStereo = 0f;
            voice = new BeaconVoice(gameObject, source);
            _voices[slot] = voice;
            return voice;
        }

        private AudioClip EnsureClip()
        {
            if (_clip != null)
            {
                return _clip;
            }

            string path = GetBeaconPath();
            try
            {
                _clip = WavAudioClipLoader.Load(path, "SongsOfConquestAccess_Beacon");
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to load beacon sound from " + path + ": " + exception.Message);
                _clip = null;
            }

            return _clip;
        }

        private static string GetBeaconPath()
        {
            return Path.Combine(Paths.ConfigPath, "SongsOfConquestAccess", "sounds", BeaconFileName);
        }

        private void ApplyParameters(BeaconVoice voice, Vector2Int listener)
        {
            if (voice == null || voice.Source == null)
            {
                return;
            }

            int dx = voice.Target.x - listener.x;
            int dy = voice.Target.y - listener.y;
            float pan = Clamp(dx / (float)PanSaturationTiles, -1f, 1f);
            int semitones = Math.Max(-PitchMaxSemitones, Math.Min(PitchMaxSemitones, dy));
            float pitch = Mathf.Pow(2f, semitones / 12f);
            float distance = Mathf.Sqrt(dx * dx + dy * dy);
            float volume = Clamp(1f - distance / AudibleDistanceTiles, 0f, 1f);

            voice.Source.panStereo = pan;
            voice.Source.pitch = pitch;
            voice.Source.volume = _isAudible ? volume : 0f;
            if (!_isAudible && voice.Source.isPlaying)
            {
                voice.Source.Stop();
            }
        }

        private static float Clamp(float value, float min, float max)
        {
            if (value < min)
            {
                return min;
            }

            return value > max ? max : value;
        }

        private sealed class BeaconVoice
        {
            public BeaconVoice(GameObject gameObject, AudioSource source)
            {
                GameObject = gameObject;
                Source = source;
            }

            public GameObject GameObject { get; private set; }

            public AudioSource Source { get; private set; }

            public Vector2Int Target { get; set; }

            public bool IsActive { get; set; }
        }
    }
}
