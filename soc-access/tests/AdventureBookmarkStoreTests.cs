using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SongsOfConquestAccess.Bookmarks;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    [TestClass]
    public sealed class AdventureBookmarkStoreTests
    {
        private string _directory;

        [TestInitialize]
        public void Setup()
        {
            _directory = Path.Combine(Path.GetTempPath(), "soc-access-bookmark-tests-" + Guid.NewGuid().ToString("N"));
        }

        [TestCleanup]
        public void Cleanup()
        {
            if (!string.IsNullOrWhiteSpace(_directory) && Directory.Exists(_directory))
            {
                Directory.Delete(_directory, recursive: true);
            }
        }

        [TestMethod]
        public void GameHashDoesNotIncludeTeamId()
        {
            AdventureBookmarkGameIdentity teamZero = Identity(teamId: 0);
            AdventureBookmarkGameIdentity teamOne = Identity(teamId: 1);

            Assert.AreEqual(teamZero.Hash, teamOne.Hash);
            Assert.AreEqual(teamZero.FileHash, teamOne.FileHash);
            Assert.AreNotEqual(teamZero.FileName, teamOne.FileName);
        }

        [TestMethod]
        public void FileNameUsesShortHash()
        {
            AdventureBookmarkGameIdentity identity = Identity(teamId: 3);

            Assert.AreEqual(64, identity.Hash.Length);
            Assert.AreEqual(16, identity.FileHash.Length);
            Assert.AreEqual(identity.Hash.Substring(0, 16), identity.FileHash);
            Assert.AreEqual(identity.FileHash + "-team-3.json", identity.FileName);
        }

        [TestMethod]
        public void SaveWritesJsonWithDiagnosticsAndSlots()
        {
            AdventureBookmarkGameIdentity identity = Identity(teamId: 2);
            AdventureBookmarkSet set = new AdventureBookmarkSet();
            set.Set("1", new Vector2Int(12, 8));
            set.Set("0", new Vector2Int(22, 14));

            AdventureBookmarkStore store = new AdventureBookmarkStore(_directory);
            store.Save(identity, set);

            string json = File.ReadAllText(store.GetPath(identity));
            StringAssert.Contains(json, "\"version\":1");
            StringAssert.Contains(json, "\"teamId\":2");
            StringAssert.Contains(json, "\"hash\":\"" + identity.Hash + "\"");
            StringAssert.Contains(json, "\"fileHash\":\"" + identity.FileHash + "\"");
            StringAssert.Contains(json, "\"slot\":\"1\"");
            StringAssert.Contains(json, "\"x\":12");
            StringAssert.Contains(json, "\"slot\":\"0\"");
        }

        [TestMethod]
        public void LoadRestoresSavedSlots()
        {
            AdventureBookmarkGameIdentity identity = Identity(teamId: 0);
            AdventureBookmarkSet set = new AdventureBookmarkSet();
            set.Set("3", new Vector2Int(4, -2));
            AdventureBookmarkStore store = new AdventureBookmarkStore(_directory);
            store.Save(identity, set);

            AdventureBookmarkSet loaded = store.Load(identity);

            Vector2Int point;
            Assert.IsTrue(loaded.TryGet("3", out point));
            Assert.AreEqual(new Vector2Int(4, -2), point);
        }

        [TestMethod]
        public void DifferentTeamsUseSeparateFiles()
        {
            AdventureBookmarkStore store = new AdventureBookmarkStore(_directory);
            AdventureBookmarkSet teamZeroSet = new AdventureBookmarkSet();
            teamZeroSet.Set("1", new Vector2Int(1, 1));
            store.Save(Identity(teamId: 0), teamZeroSet);

            AdventureBookmarkSet loaded = store.Load(Identity(teamId: 1));

            Vector2Int point;
            Assert.IsFalse(loaded.TryGet("1", out point));
        }

        [TestMethod]
        public void LoadSkipsMalformedSlots()
        {
            AdventureBookmarkGameIdentity identity = Identity(teamId: 0);
            AdventureBookmarkStore store = new AdventureBookmarkStore(_directory);
            Directory.CreateDirectory(_directory);
            File.WriteAllText(
                store.GetPath(identity),
                "{\"version\":1,\"slots\":[{\"slot\":\"x\",\"x\":1,\"y\":2},{\"slot\":\"2\",\"x\":3,\"y\":4}]}");

            AdventureBookmarkSet loaded = store.Load(identity);

            Vector2Int point;
            Assert.IsFalse(loaded.TryGet("x", out point));
            Assert.IsTrue(loaded.TryGet("2", out point));
            Assert.AreEqual(new Vector2Int(3, 4), point);
        }

        private static AdventureBookmarkGameIdentity Identity(int teamId)
        {
            return AdventureBookmarkGameIdentity.Create(
                "Campaign",
                "maps/test-map",
                "campaign-one",
                123,
                456,
                teamId);
        }
    }
}
