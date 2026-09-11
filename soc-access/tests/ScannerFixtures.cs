using System;
using SongsOfConquestAccess.Scanner;
using UnityEngine;

namespace SongsOfConquestAccess.Tests
{
    /// <summary>Shorthand for the pieces every scanner test builds: a controller over stub
    /// callbacks, the snapshot it reads, a result and the three custom-category slots.</summary>
    internal static class ScannerFixtures
    {
        /// <summary>A controller over a snapshot that never changes.</summary>
        public static ScannerController Controller(ScannerSnapshot snapshot)
        {
            return Controller(_ => snapshot);
        }

        /// <summary>A controller whose cursor sits at the origin and whose results all stay valid.</summary>
        public static ScannerController Controller(Func<Vector2Int, ScannerSnapshot> snapshotBuilder)
        {
            return Controller(snapshotBuilder, () => Vector2Int.zero, (ScannerResult result) => true);
        }

        public static ScannerController Controller(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<Vector2Int> cursorProvider,
            Func<Vector2Int, bool> jumpTo)
        {
            return Controller(snapshotBuilder, cursorProvider, _ => true, jumpTo);
        }

        public static ScannerController Controller(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<Vector2Int> cursorProvider,
            Func<ScannerResult, bool> validator)
        {
            return Controller(snapshotBuilder, cursorProvider, validator, _ => true);
        }

        public static ScannerController Controller(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<Vector2Int> cursorProvider,
            Func<ScannerResult, bool> validator,
            Func<Vector2Int, bool> jumpTo)
        {
            return Controller(
                snapshotBuilder,
                cursorProvider,
                (result, cursorHint) => validator(result)
                    ? ScannerResultRefresh.Valid(result.Position)
                    : ScannerResultRefresh.Invalid,
                jumpTo);
        }

        public static ScannerController Controller(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<Vector2Int> cursorProvider,
            Func<ScannerResult, Vector2Int, ScannerResultRefresh> refreshResult,
            Func<Vector2Int, bool> jumpTo)
        {
            return new ScannerController(
                snapshotBuilder,
                cursorProvider,
                refreshResult,
                jumpTo,
                (result, directions, index, count, includeItemName) => null,
                ScannerDirectionMode.Square);
        }

        /// <summary>A snapshot under categories that group their items, which is the taxonomy's
        /// usual shape.</summary>
        public static ScannerSnapshot Snapshot(params ScannerEntry[] entries)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            for (int i = 0; i < entries.Length; i++)
            {
                ScannerEntry entry = entries[i];
                snapshot.Add(entry.Category, entry.Subcategory, new ScannerResult(entry.Key, entry.Label, entry.Position));
            }

            return snapshot;
        }

        /// <summary>The same entries under a category that hands out flat subcategories,
        /// which is how the taxonomy declares the revealed list.</summary>
        public static ScannerSnapshot FlatSnapshot(params ScannerEntry[] entries)
        {
            ScannerSnapshot snapshot = new ScannerSnapshot();
            for (int i = 0; i < entries.Length; i++)
            {
                ScannerEntry entry = entries[i];
                ScannerCategory category = snapshot.GetOrAddCategory(entry.Category);
                category.FlatItems = true;
                category.GetOrAddSubcategory(entry.Subcategory)
                    .Add(new ScannerResult(entry.Key, entry.Label, entry.Position));
            }

            return snapshot;
        }

        public static ScannerEntry Entry(string category, string subcategory, string label, int x, int y)
        {
            return Entry(category, subcategory, label, x, y, category + ":" + subcategory + ":" + label + ":" + x + ":" + y);
        }

        public static ScannerEntry Entry(string category, string subcategory, string label, int x, int y, string key)
        {
            return new ScannerEntry
            {
                Category = category,
                Subcategory = subcategory,
                Label = label,
                Position = new Vector2Int(x, y),
                Key = key
            };
        }

        public static ScannerResult Result(string key, string label, int x, int y)
        {
            return new ScannerResult(key, label, new Vector2Int(x, y));
        }

        /// <summary>The three slots, filled from the front, which is what the settings hand the
        /// synthesizer; a null entry leaves that slot empty.</summary>
        public static ScannerCustomSlots Slots(params ScannerCustomCategory[] categories)
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            for (int i = 0; i < categories.Length; i++)
            {
                slots.Set(i, categories[i]);
            }

            return slots;
        }

        /// <summary>One result waiting to be filed under a category and subcategory.</summary>
        public sealed class ScannerEntry
        {
            public string Category;
            public string Subcategory;
            public string Label;
            public string Key;
            public Vector2Int Position;
        }
    }
}
