using System.Collections.Generic;

namespace SongsOfConquestAccess.Scanner
{
    /// <summary>
    /// Moves the categories saved as a list onto the three numbered slots, once,
    /// the first time a settings file written before the slots existed is read.
    ///
    /// The key a category was given is what it was reached by, so it is what
    /// decides the number it keeps: comma became slot 1, period slot 2, slash
    /// slot 3. Everything else fills whatever is still empty in the order it was
    /// saved in, and a player who had more than three loses the ones that do not
    /// fit - which is said out loud in the log rather than swallowed, because
    /// nothing else will ever mention them again.
    /// </summary>
    public static class ScannerCustomSlotsMigration
    {
        private const string CommaToken = "comma";
        private const string PeriodToken = "period";
        private const string SlashToken = "slash";

        public static ScannerCustomSlots Migrate(
            IReadOnlyList<ScannerSavedCategory> saved,
            out IReadOnlyList<string> dropped)
        {
            ScannerCustomSlots slots = new ScannerCustomSlots();
            List<string> lost = new List<string>();
            dropped = lost;
            if (saved == null)
            {
                return slots;
            }

            List<ScannerCustomCategory> unkeyed = new List<ScannerCustomCategory>();
            for (int i = 0; i < saved.Count; i++)
            {
                ScannerCustomCategory category = saved[i].Category;
                if (category == null)
                {
                    continue;
                }

                int slot = SlotFor(saved[i].QuickKeyToken);
                if (slot < 0 || slots.Slot(slot) != null || !slots.Set(slot, category))
                {
                    unkeyed.Add(category);
                }
            }

            for (int i = 0; i < unkeyed.Count; i++)
            {
                int slot = slots.FirstEmpty();
                if (slot < 0 || !slots.Set(slot, unkeyed[i]))
                {
                    lost.Add(unkeyed[i].Name);
                }
            }

            return slots;
        }

        private static int SlotFor(string quickKeyToken)
        {
            switch (quickKeyToken)
            {
                case CommaToken:
                    return 0;
                case PeriodToken:
                    return 1;
                case SlashToken:
                    return 2;
                default:
                    return -1;
            }
        }
    }
}
