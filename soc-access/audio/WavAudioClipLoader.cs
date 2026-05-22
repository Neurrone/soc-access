using System;
using System.IO;
using System.Text;
using UnityEngine;

namespace SongsOfConquestAccess.Audio
{
    internal static class WavAudioClipLoader
    {
        public static AudioClip Load(string path, string clipName)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("Missing WAV path");
            }

            byte[] bytes = File.ReadAllBytes(path);
            using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes)))
            {
                ExpectFourCc(reader, "RIFF");
                reader.ReadInt32();
                ExpectFourCc(reader, "WAVE");

                short audioFormat = 0;
                short channels = 0;
                int sampleRate = 0;
                short bitsPerSample = 0;
                byte[] data = null;

                while (reader.BaseStream.Position + 8 <= reader.BaseStream.Length)
                {
                    string chunkId = ReadFourCc(reader);
                    int chunkSize = reader.ReadInt32();
                    long nextChunk = reader.BaseStream.Position + chunkSize;
                    if (chunkId == "fmt ")
                    {
                        audioFormat = reader.ReadInt16();
                        channels = reader.ReadInt16();
                        sampleRate = reader.ReadInt32();
                        reader.ReadInt32();
                        reader.ReadInt16();
                        bitsPerSample = reader.ReadInt16();
                    }
                    else if (chunkId == "data")
                    {
                        data = reader.ReadBytes(chunkSize);
                    }

                    reader.BaseStream.Position = nextChunk + (chunkSize % 2);
                }

                if (audioFormat != 1)
                {
                    throw new InvalidDataException("Only PCM WAV files are supported");
                }

                if (channels <= 0 || sampleRate <= 0)
                {
                    throw new InvalidDataException("WAV format chunk is incomplete");
                }

                if (bitsPerSample != 16)
                {
                    throw new InvalidDataException("Only 16-bit WAV files are supported");
                }

                if (data == null || data.Length == 0)
                {
                    throw new InvalidDataException("WAV data chunk is missing");
                }

                int sampleCount = data.Length / 2;
                float[] samples = new float[sampleCount];
                for (int i = 0; i < sampleCount; i++)
                {
                    short value = BitConverter.ToInt16(data, i * 2);
                    samples[i] = value / 32768f;
                }

                AudioClip clip = AudioClip.Create(
                    string.IsNullOrWhiteSpace(clipName) ? "SongsOfConquestAccess_Wav" : clipName,
                    sampleCount / channels,
                    channels,
                    sampleRate,
                    false);
                clip.SetData(samples, 0);
                return clip;
            }
        }

        private static void ExpectFourCc(BinaryReader reader, string expected)
        {
            string actual = ReadFourCc(reader);
            if (actual != expected)
            {
                throw new InvalidDataException("Expected " + expected + " chunk, found " + actual);
            }
        }

        private static string ReadFourCc(BinaryReader reader)
        {
            return Encoding.ASCII.GetString(reader.ReadBytes(4));
        }
    }
}
