using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using BepInEx;
using UnityEngine;

namespace SongsOfConquestAccess.Bookmarks
{
    internal sealed class AdventureBookmarkStore
    {
        private const int CurrentVersion = 1;
        private readonly string _directory;

        public AdventureBookmarkStore()
            : this(Path.Combine(Paths.ConfigPath, "SongsOfConquestAccess", "bookmarks"))
        {
        }

        public AdventureBookmarkStore(string directory)
        {
            _directory = directory ?? string.Empty;
        }

        public AdventureBookmarkSet Load(AdventureBookmarkGameIdentity identity)
        {
            AdventureBookmarkSet set = new AdventureBookmarkSet();
            if (identity == null)
            {
                return set;
            }

            string path = GetPath(identity);
            if (!File.Exists(path))
            {
                return set;
            }

            BookmarkStoreFile file = Deserialize(File.ReadAllText(path));
            if (file == null || file.slots == null)
            {
                return set;
            }

            for (int i = 0; i < file.slots.Length; i++)
            {
                BookmarkSlotEntry entry = file.slots[i];
                if (entry == null || !AdventureBookmarkSet.IsValidSlot(entry.slot))
                {
                    continue;
                }

                set.Set(entry.slot, new Vector2Int(entry.x, entry.y));
            }

            return set;
        }

        public void Save(AdventureBookmarkGameIdentity identity, AdventureBookmarkSet set)
        {
            if (identity == null || set == null)
            {
                return;
            }

            Directory.CreateDirectory(_directory);
            BookmarkStoreFile file = new BookmarkStoreFile
            {
                version = CurrentVersion,
                game = BookmarkGameInfo.FromIdentity(identity),
                slots = ToSlotEntries(set)
            };

            File.WriteAllText(GetPath(identity), Serialize(file), Encoding.UTF8);
        }

        internal string GetPath(AdventureBookmarkGameIdentity identity)
        {
            return Path.Combine(_directory, identity.FileName);
        }

        private static BookmarkSlotEntry[] ToSlotEntries(AdventureBookmarkSet set)
        {
            List<BookmarkSlotEntry> entries = new List<BookmarkSlotEntry>();
            for (int i = 0; i < AdventureBookmarkSlots.All.Length; i++)
            {
                string slot = AdventureBookmarkSlots.All[i];
                Vector2Int position;
                if (!set.TryGet(slot, out position))
                {
                    continue;
                }

                entries.Add(new BookmarkSlotEntry
                {
                    slot = slot,
                    x = position.x,
                    y = position.y
                });
            }

            return entries.ToArray();
        }

        private static string Serialize(BookmarkStoreFile file)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(BookmarkStoreFile));
            using (MemoryStream stream = new MemoryStream())
            {
                serializer.WriteObject(stream, file);
                return Encoding.UTF8.GetString(stream.ToArray());
            }
        }

        private static BookmarkStoreFile Deserialize(string json)
        {
            DataContractJsonSerializer serializer = new DataContractJsonSerializer(typeof(BookmarkStoreFile));
            using (MemoryStream stream = new MemoryStream(Encoding.UTF8.GetBytes(json ?? string.Empty)))
            {
                return serializer.ReadObject(stream) as BookmarkStoreFile;
            }
        }

        [DataContract]
        internal sealed class BookmarkStoreFile
        {
            [DataMember]
            public int version;
            [DataMember]
            public BookmarkGameInfo game;
            [DataMember]
            public BookmarkSlotEntry[] slots;
        }

        [DataContract]
        internal sealed class BookmarkGameInfo
        {
            [DataMember]
            public string key;
            [DataMember]
            public string hash;
            [DataMember]
            public string fileHash;
            [DataMember]
            public string mode;
            [DataMember]
            public string mapFile;
            [DataMember]
            public string campaignIdentifier;
            [DataMember]
            public uint mapRandomSeed;
            [DataMember]
            public int instanceRandomSeed;
            [DataMember]
            public int teamId;

            public static BookmarkGameInfo FromIdentity(AdventureBookmarkGameIdentity identity)
            {
                return new BookmarkGameInfo
                {
                    key = identity.Key,
                    hash = identity.Hash,
                    fileHash = identity.FileHash,
                    mode = identity.Mode,
                    mapFile = identity.MapFile,
                    campaignIdentifier = identity.CampaignIdentifier,
                    mapRandomSeed = identity.MapRandomSeed,
                    instanceRandomSeed = identity.InstanceRandomSeed,
                    teamId = identity.TeamId
                };
            }
        }

        [DataContract]
        internal sealed class BookmarkSlotEntry
        {
            [DataMember]
            public string slot;
            [DataMember]
            public int x;
            [DataMember]
            public int y;
        }
    }
}
