using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Bookmarks
{
    /// <summary>
    /// WHICH BOOKMARK SLOTS THE PLAYER HAS TURNED A BEACON ON FOR, remembered past the thing that
    /// holds the beacons.
    ///
    /// A beacon is armed on <see cref="Audio.AdventureBeaconAudio"/>, which belongs to the map
    /// cursor, which belongs to one adventure-scene adapter: a manual battle unloads and reloads that
    /// scene, so every beacon the player had on went silent with it, and a hot-seat hand-over stops
    /// them all deliberately and could not bring the first player's back when the map returned to
    /// them. But a beacon is not a fact about a cursor - it is a fact about a BOOKMARK, so it is kept
    /// here, beside the slot it is about, keyed on the same storage the bookmark file is keyed on
    /// (<see cref="AdventureBookmarkGameIdentity.FileName"/>: the game, and the team within it).
    ///
    /// IN MEMORY ONLY, deliberately: the bookmark file records a position and nothing else, and its
    /// format is not changed for this. So a beacon does not survive quitting the game - and, since a
    /// hot reload rebuilds this class's assembly, it does not survive one of those either. The mod's
    /// Stop empties it, with the rest of what the mod owns.
    /// </summary>
    public static class AdventureBeaconArming
    {
        private static readonly Dictionary<string, HashSet<string>> Armed =
            new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);

        /// <summary>Whether the player has a beacon on for this slot of this game and team.</summary>
        public static bool IsArmed(AdventureBookmarkGameIdentity identity, string slot)
        {
            HashSet<string> slots;
            return slot != null
                && identity != null
                && Armed.TryGetValue(identity.FileName, out slots)
                && slots.Contains(slot);
        }

        /// <summary>Record what the player just did with the slot's beacon. A slot that is turned off
        /// - by the player, or because the bookmark it pointed at is no longer there - is forgotten
        /// rather than remembered as off, so nothing accumulates.</summary>
        public static void Remember(AdventureBookmarkGameIdentity identity, string slot, bool armed)
        {
            if (identity == null || slot == null)
            {
                return;
            }

            HashSet<string> slots;
            if (!Armed.TryGetValue(identity.FileName, out slots))
            {
                if (!armed)
                {
                    return;
                }

                slots = new HashSet<string>(StringComparer.Ordinal);
                Armed[identity.FileName] = slots;
            }

            if (armed)
            {
                slots.Add(slot);
            }
            else
            {
                slots.Remove(slot);
            }
        }

        /// <summary>A Stop step: the mod is going away and so is everything it was sounding.</summary>
        public static void Reset()
        {
            Armed.Clear();
        }
    }
}
