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
    public sealed class AdventureBookmarkStore
    {
        private const int CurrentVersion = 1;
        private static int _generation;
        private readonly string _directory;

        public AdventureBookmarkStore()
            : this(Path.Combine(Paths.ConfigPath, "SongsOfConquestAccess", "bookmarks"))
        {
        }

        public AdventureBookmarkStore(string directory)
        {
            _directory = directory ?? string.Empty;
        }

        /// <summary>How many times any store has written a bookmarks file. A set already loaded is
        /// stale once this has moved, which is how the map notices an import made from the Mod
        /// options window - a store of its own over the same folder. Static for that reason, and put
        /// back by <c>SocAccessMod.Stop()</c>.</summary>
        public static int Generation
        {
            get { return _generation; }
        }

        public static void Reset()
        {
            _generation = 0;
        }

        /// <summary>The folder every game's bookmarks file sits in.</summary>
        public string Folder
        {
            get { return _directory; }
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
            _generation++;
        }

        public string GetPath(AdventureBookmarkGameIdentity identity)
        {
            return Path.Combine(_directory, identity.FileName);
        }

        public bool Exists(AdventureBookmarkGameIdentity identity)
        {
            return identity != null && File.Exists(GetPath(identity));
        }

        /// <summary>The file's text exactly as it stands on disk, which is what the player copies.
        /// False when there is nothing to read or the disk refused.</summary>
        public bool TryReadText(AdventureBookmarkGameIdentity identity, out string text)
        {
            text = null;
            if (identity == null)
            {
                return false;
            }

            try
            {
                text = File.ReadAllText(GetPath(identity));
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read the adventure bookmarks file: " + exception.Message);
                return false;
            }
        }

        /// <summary>Whether the folder is worth opening: it exists and holds at least one bookmarks
        /// file.</summary>
        public bool FolderHasFiles()
        {
            try
            {
                return Directory.Exists(_directory) && Directory.GetFiles(_directory, "*.json").Length > 0;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read the adventure bookmarks folder: " + exception.Message);
                return false;
            }
        }

        /// <summary>
        /// Write a bookmarks file the player pasted in. The file says which game it belongs to, so
        /// the game being played - if any - has nothing to do with where it lands; the identity is
        /// rebuilt from the text's own game block rather than trusted from its hash.
        ///
        /// ONLY THE SLOTS THE TEXT CARRIES ARE OVERWRITTEN (owner's ruling): a slot the file on disk
        /// holds and the import does not is left where it is, so pasting one bookmark does not empty
        /// the other nine.
        /// </summary>
        public ImportResult Import(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new ImportResult(ImportOutcome.Empty, 0, null);
            }

            BookmarkStoreFile file;
            try
            {
                file = Deserialize(text);
            }
            catch (Exception)
            {
                file = null;
            }

            if (file == null || file.game == null || file.slots == null)
            {
                return new ImportResult(ImportOutcome.NotBookmarks, 0, null);
            }

            AdventureBookmarkGameIdentity identity = AdventureBookmarkGameIdentity.Create(
                file.game.mode,
                file.game.mapFile,
                file.game.campaignIdentifier,
                file.game.mapRandomSeed,
                file.game.instanceRandomSeed,
                file.game.teamId);

            AdventureBookmarkSet set;
            try
            {
                set = Load(identity);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to read the adventure bookmarks being merged into: " + exception.Message);
                set = new AdventureBookmarkSet();
            }

            int count = 0;
            for (int i = 0; i < file.slots.Length; i++)
            {
                BookmarkSlotEntry entry = file.slots[i];
                if (entry == null || !AdventureBookmarkSet.IsValidSlot(entry.slot))
                {
                    continue;
                }

                set.Set(entry.slot, new Vector2Int(entry.x, entry.y));
                count++;
            }

            try
            {
                Save(identity, set);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to write the imported adventure bookmarks: " + exception.Message);
                return new ImportResult(ImportOutcome.WriteFailed, 0, identity);
            }

            return new ImportResult(ImportOutcome.Imported, count, identity);
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

        /// <summary>What became of a paste: nothing on the clipboard, something that is not a
        /// bookmarks file, a file the disk refused, or a file written.</summary>
        public enum ImportOutcome
        {
            Empty,
            NotBookmarks,
            WriteFailed,
            Imported
        }

        /// <summary>The outcome, how many slots the text carried, and the game it was written for -
        /// which the caller compares with the game being played to say so.</summary>
        public sealed class ImportResult
        {
            public ImportResult(ImportOutcome outcome, int count, AdventureBookmarkGameIdentity identity)
            {
                Outcome = outcome;
                Count = count;
                Identity = identity;
            }

            public ImportOutcome Outcome { get; private set; }

            public int Count { get; private set; }

            public AdventureBookmarkGameIdentity Identity { get; private set; }
        }

        [DataContract]
        public sealed class BookmarkStoreFile
        {
            [DataMember]
            public int version;
            [DataMember]
            public BookmarkGameInfo game;
            [DataMember]
            public BookmarkSlotEntry[] slots;
        }

        [DataContract]
        public sealed class BookmarkGameInfo
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
        public sealed class BookmarkSlotEntry
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
