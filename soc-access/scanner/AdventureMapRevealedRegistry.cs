using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    public enum AdventureMapRevealedKind
    {
        MapEntity,
        Wielder
    }

    public sealed class AdventureMapRevealedEntry
    {
        public AdventureMapRevealedEntry(
            long sequence,
            string key,
            string label,
            Vector2Int position,
            int stableReference,
            AdventureMapRevealedKind kind)
        {
            Sequence = sequence;
            Key = key ?? string.Empty;
            Label = label ?? string.Empty;
            Position = position;
            StableReference = stableReference;
            Kind = kind;
        }

        public long Sequence { get; private set; }

        public string Key { get; private set; }

        public string Label { get; private set; }

        public Vector2Int Position { get; private set; }

        public int StableReference { get; private set; }

        public AdventureMapRevealedKind Kind { get; private set; }
    }

    /// <summary>
    /// What has been FOUND in one adventure game, kept PER TEAM IN CONTROL. A discovery is decided
    /// from the team's own exploration, fog and scouting detail, and in hot seat the map is handed
    /// from one player to the next inside one game, so one list for the whole game read out player
    /// one's finds to player two - inside fog player two never explored. The team id is the one read
    /// from the game at the call that writes or reads, so a hand-over moves to another partition and
    /// handing the map back finds the first player's list where they left it. The whole thing is
    /// emptied when the GAME changes (<see cref="AdventureMapScannerState.Rebind"/>).
    /// </summary>
    public sealed class AdventureMapRevealedRegistry
    {
        private readonly Dictionary<int, TeamEntries> _byTeam = new Dictionary<int, TeamEntries>();

        public IReadOnlyList<AdventureMapRevealedEntry> Entries(int teamId)
        {
            List<AdventureMapRevealedEntry> entries = new List<AdventureMapRevealedEntry>();
            TeamEntries team;
            if (!_byTeam.TryGetValue(teamId, out team))
            {
                return entries;
            }

            for (int i = 0; i < team.Order.Count; i++)
            {
                AdventureMapRevealedEntry entry;
                if (team.EntriesByKey.TryGetValue(team.Order[i], out entry))
                {
                    entries.Add(entry);
                }
            }

            return entries;
        }

        public void AddOrUpdate(
            int teamId,
            string key,
            string label,
            Vector2Int position,
            int stableReference,
            AdventureMapRevealedKind kind)
        {
            if (string.IsNullOrWhiteSpace(key) || string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            TeamEntries team;
            if (!_byTeam.TryGetValue(teamId, out team))
            {
                team = new TeamEntries();
                _byTeam[teamId] = team;
            }

            AdventureMapRevealedEntry existing;
            if (team.EntriesByKey.TryGetValue(key, out existing))
            {
                team.EntriesByKey[key] = new AdventureMapRevealedEntry(
                    existing.Sequence,
                    key,
                    label,
                    position,
                    stableReference,
                    kind);
                return;
            }

            team.EntriesByKey[key] = new AdventureMapRevealedEntry(
                team.NextSequence++,
                key,
                label,
                position,
                stableReference,
                kind);
            team.Order.Add(key);
        }

        public bool Remove(int teamId, string key)
        {
            TeamEntries team;
            if (string.IsNullOrWhiteSpace(key)
                || !_byTeam.TryGetValue(teamId, out team)
                || !team.EntriesByKey.Remove(key))
            {
                return false;
            }

            team.Order.Remove(key);
            return true;
        }

        public bool Contains(int teamId, string key)
        {
            TeamEntries team;
            return !string.IsNullOrWhiteSpace(key)
                && _byTeam.TryGetValue(teamId, out team)
                && team.EntriesByKey.ContainsKey(key);
        }

        public void Clear()
        {
            _byTeam.Clear();
        }

        private sealed class TeamEntries
        {
            public readonly Dictionary<string, AdventureMapRevealedEntry> EntriesByKey =
                new Dictionary<string, AdventureMapRevealedEntry>();

            public readonly List<string> Order = new List<string>();

            public long NextSequence;
        }
    }
}
