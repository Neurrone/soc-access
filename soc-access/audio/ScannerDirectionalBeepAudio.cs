using System;
using System.IO;
using BepInEx;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    internal enum DirectionalBeepGridGeometry
    {
        Square,
        Hex
    }

    internal static class ScannerDirectionalBeepAudio
    {
        private const string ScannerBeepFileName = "scanner_beep.wav";
        private const int SquarePanSaturationTiles = 12;
        private const int HexPanSaturationTiles = 6;
        private const int SquarePitchSemitonesPerRow = 1;
        private const int HexPitchSemitonesPerRow = 2;
        private const int PitchMaxSemitones = 12;
        private const float AudibleDistanceTiles = 30f;

        private static GameObject _gameObject;
        private static AudioSource _source;
        private static AudioClip _clip;
        private static bool _loadAttempted;

        public static void Play(Vector2Int origin, Vector2Int target, DirectionalBeepGridGeometry geometry)
        {
            if (!ModSettings.ScannerPlaysDirectionalBeep || origin == target)
            {
                return;
            }

            AudioSource source = EnsureSource();
            if (source == null)
            {
                return;
            }

            float dcol;
            int drow;
            float distance;
            int panSaturationTiles;
            int pitchSemitonesPerRow;
            if (geometry == DirectionalBeepGridGeometry.Hex)
            {
                dcol = HexColumn(origin, target);
                drow = target.y - origin.y;
                distance = HexDistance(origin, target);
                panSaturationTiles = HexPanSaturationTiles;
                pitchSemitonesPerRow = HexPitchSemitonesPerRow;
            }
            else
            {
                int dx = target.x - origin.x;
                int dy = target.y - origin.y;
                dcol = dx;
                drow = dy;
                distance = Mathf.Sqrt(dx * dx + dy * dy);
                panSaturationTiles = SquarePanSaturationTiles;
                pitchSemitonesPerRow = SquarePitchSemitonesPerRow;
            }

            float volume = Clamp(1f - distance / AudibleDistanceTiles, 0f, 1f);
            if (volume <= 0f)
            {
                return;
            }

            int semitones = Math.Max(-PitchMaxSemitones, Math.Min(PitchMaxSemitones, drow * pitchSemitonesPerRow));
            source.panStereo = Clamp(dcol / panSaturationTiles, -1f, 1f);
            source.pitch = Mathf.Pow(2f, semitones / 12f);
            source.volume = volume;
            if (source.isPlaying)
            {
                source.Stop();
            }

            source.time = 0f;
            source.Play();
        }

        public static void DisposeAll()
        {
            if (_gameObject != null)
            {
                UnityEngine.Object.Destroy(_gameObject);
                _gameObject = null;
            }

            if (_clip != null)
            {
                UnityEngine.Object.Destroy(_clip);
                _clip = null;
            }

            _source = null;
            _loadAttempted = false;
        }

        private static AudioSource EnsureSource()
        {
            if (_source != null)
            {
                return _source;
            }

            AudioClip clip = EnsureClip();
            if (clip == null)
            {
                return null;
            }

            _gameObject = new GameObject("SongsOfConquestAccess_ScannerDirectionalBeep");
            _gameObject.hideFlags = HideFlags.HideInHierarchy;
            _source = _gameObject.AddComponent<AudioSource>();
            _source.clip = clip;
            _source.loop = false;
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;
            _source.volume = 0f;
            _source.pitch = 1f;
            _source.panStereo = 0f;
            return _source;
        }

        private static AudioClip EnsureClip()
        {
            if (_clip != null)
            {
                return _clip;
            }

            if (_loadAttempted)
            {
                return null;
            }

            _loadAttempted = true;
            string path = GetScannerBeepPath();
            try
            {
                _clip = WavAudioClipLoader.Load(path, "SongsOfConquestAccess_ScannerDirectionalBeep");
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("Failed to load scanner directional beep sound from " + path + ": " + exception.Message);
                _clip = null;
            }

            return _clip;
        }

        private static string GetScannerBeepPath()
        {
            return Path.Combine(Paths.ConfigPath, "SongsOfConquestAccess", "sounds", ScannerBeepFileName);
        }

        private static float HexColumn(Vector2Int origin, Vector2Int target)
        {
            return target.x + ((target.y & 1) == 0 ? 0f : 0.5f)
                - origin.x - ((origin.y & 1) == 0 ? 0f : 0.5f);
        }

        private static int HexDistance(Vector2Int left, Vector2Int right)
        {
            OffsetToCube(left, out int leftX, out int leftY, out int leftZ);
            OffsetToCube(right, out int rightX, out int rightY, out int rightZ);
            return Math.Max(Math.Abs(leftX - rightX), Math.Max(Math.Abs(leftY - rightY), Math.Abs(leftZ - rightZ)));
        }

        private static void OffsetToCube(Vector2Int point, out int cubeX, out int cubeY, out int cubeZ)
        {
            cubeX = point.x - (point.y - (point.y & 1)) / 2;
            cubeZ = point.y;
            cubeY = -cubeX - cubeZ;
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
