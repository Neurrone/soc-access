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

    public sealed class AdventureMapRevealedRegistry
    {
        private readonly Dictionary<string, AdventureMapRevealedEntry> _entriesByKey =
            new Dictionary<string, AdventureMapRevealedEntry>();
        private readonly List<string> _order = new List<string>();
        private long _nextSequence;

        public IReadOnlyList<AdventureMapRevealedEntry> Entries
        {
            get
            {
                List<AdventureMapRevealedEntry> entries = new List<AdventureMapRevealedEntry>();
                for (int i = 0; i < _order.Count; i++)
                {
                    AdventureMapRevealedEntry entry;
                    if (_entriesByKey.TryGetValue(_order[i], out entry))
                    {
                        entries.Add(entry);
                    }
                }

                return entries;
            }
        }

        public void AddOrUpdate(
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

            AdventureMapRevealedEntry existing;
            if (_entriesByKey.TryGetValue(key, out existing))
            {
                _entriesByKey[key] = new AdventureMapRevealedEntry(
                    existing.Sequence,
                    key,
                    label,
                    position,
                    stableReference,
                    kind);
                return;
            }

            _entriesByKey[key] = new AdventureMapRevealedEntry(
                _nextSequence++,
                key,
                label,
                position,
                stableReference,
                kind);
            _order.Add(key);
        }

        public bool Remove(string key)
        {
            if (string.IsNullOrWhiteSpace(key) || !_entriesByKey.Remove(key))
            {
                return false;
            }

            _order.Remove(key);
            return true;
        }

        public bool Contains(string key)
        {
            return !string.IsNullOrWhiteSpace(key) && _entriesByKey.ContainsKey(key);
        }

        public void Clear()
        {
            _entriesByKey.Clear();
            _order.Clear();
            _nextSequence = 0;
        }
    }
}
