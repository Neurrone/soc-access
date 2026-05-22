using System;
using System.Security.Cryptography;
using System.Text;

namespace SongsOfConquestAccess.Bookmarks
{
    internal sealed class AdventureBookmarkGameIdentity
    {
        private const int FileHashLength = 16;

        private AdventureBookmarkGameIdentity(
            string key,
            string hash,
            string fileHash,
            string mode,
            string mapFile,
            string campaignIdentifier,
            uint mapRandomSeed,
            int instanceRandomSeed,
            int teamId)
        {
            Key = key;
            Hash = hash;
            FileHash = fileHash;
            Mode = mode;
            MapFile = mapFile;
            CampaignIdentifier = campaignIdentifier;
            MapRandomSeed = mapRandomSeed;
            InstanceRandomSeed = instanceRandomSeed;
            TeamId = teamId;
        }

        public string Key { get; private set; }

        public string Hash { get; private set; }

        public string FileHash { get; private set; }

        public string Mode { get; private set; }

        public string MapFile { get; private set; }

        public string CampaignIdentifier { get; private set; }

        public uint MapRandomSeed { get; private set; }

        public int InstanceRandomSeed { get; private set; }

        public int TeamId { get; private set; }

        public string FileName
        {
            get { return FileHash + "-team-" + TeamId + ".json"; }
        }

        public bool SameStorageAs(AdventureBookmarkGameIdentity other)
        {
            return other != null
                && string.Equals(Hash, other.Hash, StringComparison.OrdinalIgnoreCase)
                && TeamId == other.TeamId;
        }

        public static AdventureBookmarkGameIdentity Create(
            string mode,
            string mapFile,
            string campaignIdentifier,
            uint mapRandomSeed,
            int instanceRandomSeed,
            int teamId)
        {
            mode = Normalize(mode);
            mapFile = Normalize(mapFile);
            campaignIdentifier = Normalize(campaignIdentifier);
            string key = "mode=" + mode
                + "|map=" + mapFile
                + "|campaign=" + campaignIdentifier
                + "|mapSeed=" + mapRandomSeed
                + "|instanceSeed=" + instanceRandomSeed;

            string hash = Sha256Hex(key);
            return new AdventureBookmarkGameIdentity(
                key,
                hash,
                ShortenHash(hash),
                mode,
                mapFile,
                campaignIdentifier,
                mapRandomSeed,
                instanceRandomSeed,
                teamId);
        }

        private static string Normalize(string value)
        {
            return string.IsNullOrWhiteSpace(value) ? "unknown" : value.Trim();
        }

        private static string Sha256Hex(string value)
        {
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(Encoding.UTF8.GetBytes(value ?? string.Empty));
                StringBuilder builder = new StringBuilder(hash.Length * 2);
                for (int i = 0; i < hash.Length; i++)
                {
                    builder.Append(hash[i].ToString("x2"));
                }

                return builder.ToString();
            }
        }

        private static string ShortenHash(string hash)
        {
            if (string.IsNullOrEmpty(hash) || hash.Length <= FileHashLength)
            {
                return hash ?? string.Empty;
            }

            return hash.Substring(0, FileHashLength);
        }
    }
}
