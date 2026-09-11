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
        /// <summary>
        /// One taxonomy's three slots, decoded once and kept.
        /// </summary>
        public static ScannerCustomSlots GetScannerCustomSlots(string taxonomyKey)
        {
            return GetScannerCustomSlotsCore(taxonomyKey) ?? new ScannerCustomSlots();
        }

        public static ScannerCustomCategory GetScannerCustomCategory(string taxonomyKey, int slot)
        {
            ScannerCustomSlots slots = GetScannerCustomSlotsCore(taxonomyKey);
            return slots != null ? slots.Slot(slot) : null;
        }

        /// <summary>
        /// Fills an empty slot with a category under the name the caller hands
        /// in, because the wording of the starting name is localized
        /// accessibility text. A slot that already holds one is left alone.
        /// </summary>
        public static ScannerCustomCategory AddScannerCustomCategory(string taxonomyKey, int slot, string name)
        {
            ScannerCustomSlots slots = GetScannerCustomSlotsCore(taxonomyKey);
            if (slots == null || slots.Slot(slot) != null)
            {
                return null;
            }

            ScannerCustomCategory category = new ScannerCustomCategory(name);
            if (!slots.Set(slot, category))
            {
                return null;
            }

            SaveScannerCustomSlots(taxonomyKey, slots);
            return category;
        }

        /// <summary>Empties a slot. The slot itself stays, so there is nothing
        /// to renumber and no key that stops answering.</summary>
        public static bool ClearScannerCustomCategory(string taxonomyKey, int slot)
        {
            ScannerCustomSlots slots = GetScannerCustomSlotsCore(taxonomyKey);
            if (slots == null || slots.Slot(slot) == null || !slots.Clear(slot))
            {
                return false;
            }

            SaveScannerCustomSlots(taxonomyKey, slots);
            return true;
        }

        public static bool RenameScannerCustomCategory(string taxonomyKey, int slot, string name)
        {
            return MutateScannerCustomCategory(taxonomyKey, slot, category => category.Rename(name));
        }

        public static bool SetScannerCustomCategorySelector(
            string taxonomyKey,
            int slot,
            string categoryKey,
            string subcategoryKey,
            bool selected)
        {
            return MutateScannerCustomCategory(
                taxonomyKey,
                slot,
                category => category.SetSelector(categoryKey, subcategoryKey, selected));
        }

        public static bool AddScannerCustomCategoryKeyword(string taxonomyKey, int slot, string keyword)
        {
            return MutateScannerCustomCategory(taxonomyKey, slot, category => category.AddKeyword(keyword));
        }

        public static bool RemoveScannerCustomCategoryKeyword(string taxonomyKey, int slot, string keyword)
        {
            return MutateScannerCustomCategory(taxonomyKey, slot, category => category.RemoveKeyword(keyword));
        }

        /// <summary>
        /// One taxonomy's three slots as the one string they are stored as, and putting that string
        /// back.
        ///
        /// The category dialogs edit a slot in place, as the menus they replace did, so Cancel has
        /// to be able to undo a name, a set of subcategories and a list of keywords at once, and to
        /// empty a slot the editor filled. The stored form already says all of that, so the snapshot
        /// is the stored form.
        /// </summary>
        public static string SnapshotScannerCustomCategories(string taxonomyKey)
        {
            return ScannerCustomSlotsCodec.Encode(GetScannerCustomSlotsCore(taxonomyKey));
        }

        public static bool RestoreScannerCustomCategories(string taxonomyKey, string snapshot)
        {
            ScannerCustomCategoryConfig config = GetScannerCustomCategoryConfig(taxonomyKey);
            if (config == null)
            {
                return false;
            }

            config.Slots = ScannerCustomSlotsCodec.Decode(snapshot);
            SaveScannerCustomSlots(taxonomyKey, config.Slots);
            return true;
        }

        /// <summary>
        /// One slots entry per scanner context.
        /// </summary>
        private static void BindScannerCustomCategories(ConfigFile config)
        {
            _scannerCustomCategories.Clear();
            for (int i = 0; i < ScannerTaxonomyKeys.All.Length; i++)
            {
                string taxonomyKey = ScannerTaxonomyKeys.All[i];
                _scannerCustomCategories[taxonomyKey] = new ScannerCustomCategoryConfig
                {
                    Entry = config.Bind(
                        "Scanner",
                        ToConfigKeyPrefix(taxonomyKey) + "CustomCategorySlots",
                        string.Empty,
                        "The three player-defined scanner categories for this context. Edited through the mod settings screen.")
                };
            }
        }

        /// <summary>
        /// Decoded once per context and kept, because these are player settings
        /// rather than game state: nothing outside this class can change them
        /// between reads.
        /// </summary>
        private static ScannerCustomSlots GetScannerCustomSlotsCore(string taxonomyKey)
        {
            ScannerCustomCategoryConfig config = GetScannerCustomCategoryConfig(taxonomyKey);
            if (config == null)
            {
                return null;
            }

            if (config.Slots == null)
            {
                config.Slots = ScannerCustomSlotsCodec.Decode(config.Entry != null ? config.Entry.Value : null);
            }

            return config.Slots;
        }

        private static ScannerCustomCategoryConfig GetScannerCustomCategoryConfig(string taxonomyKey)
        {
            return Lookup(_scannerCustomCategories, taxonomyKey);
        }

        private static bool MutateScannerCustomCategory(
            string taxonomyKey,
            int slot,
            Func<ScannerCustomCategory, bool> mutate)
        {
            ScannerCustomSlots slots = GetScannerCustomSlotsCore(taxonomyKey);
            ScannerCustomCategory category = slots != null ? slots.Slot(slot) : null;
            if (category == null || mutate == null || !mutate(category))
            {
                return false;
            }

            SaveScannerCustomSlots(taxonomyKey, slots);
            return true;
        }

        private static void SaveScannerCustomSlots(string taxonomyKey, ScannerCustomSlots slots)
        {
            ScannerCustomCategoryConfig config = GetScannerCustomCategoryConfig(taxonomyKey);
            if (config == null || config.Entry == null)
            {
                return;
            }

            config.Entry.Value = ScannerCustomSlotsCodec.Encode(slots);
            _config?.Save();
        }

        private sealed class ScannerCustomCategoryConfig
        {
            public ConfigEntry<string> Entry { get; set; }
            public ScannerCustomSlots Slots { get; set; }
        }
    }
}
