using System;
using System.Collections.Generic;
using System.Linq;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal static class ReachableAdventureEntitySummaryFormatter
    {
        public static string Format(IReadOnlyList<ReachableAdventureEntity> entities)
        {
            List<Group> groups = BuildGroups(entities);
            if (groups.Count == 0)
            {
                return ModText.Get(ModStrings.Scanner.ReachableSummaryNone);
            }

            List<string> parts = groups
                .OrderBy(group => group.NearestDistance)
                .ThenBy(group => group.Label, StringComparer.OrdinalIgnoreCase)
                .ThenBy(group => group.NearestPosition.x)
                .ThenBy(group => group.NearestPosition.y)
                .ThenBy(group => group.NearestId)
                .Select(FormatGroup)
                .ToList();

            return ModText.Get(ModStrings.Scanner.ReachableSummary, ModText.JoinListWithCommas(parts));
        }

        private static List<Group> BuildGroups(IReadOnlyList<ReachableAdventureEntity> entities)
        {
            Dictionary<string, Group> groups = new Dictionary<string, Group>(StringComparer.OrdinalIgnoreCase);
            if (entities == null)
            {
                return new List<Group>();
            }

            for (int i = 0; i < entities.Count; i++)
            {
                ReachableAdventureEntity entity = entities[i];
                if (entity == null || string.IsNullOrWhiteSpace(entity.Name))
                {
                    continue;
                }

                Group group;
                if (!groups.TryGetValue(entity.Name, out group))
                {
                    group = new Group(entity.Name);
                    groups[entity.Name] = group;
                }

                group.Add(entity);
            }

            return groups.Values.ToList();
        }

        private static string FormatGroup(Group group)
        {
            if (group.Count <= 1)
            {
                return group.Label;
            }

            return ModText.Get(ModStrings.Scanner.ReachableSummaryCount, group.Count, group.Label);
        }

        private sealed class Group
        {
            public Group(string label)
            {
                Label = label;
                NearestDistance = float.PositiveInfinity;
                NearestPosition = new Vector2Int(int.MaxValue, int.MaxValue);
                NearestId = int.MaxValue;
            }

            public string Label { get; private set; }

            public int Count { get; private set; }

            public float NearestDistance { get; private set; }

            public Vector2Int NearestPosition { get; private set; }

            public int NearestId { get; private set; }

            public void Add(ReachableAdventureEntity entity)
            {
                Count++;
                if (IsNearer(entity))
                {
                    NearestDistance = entity.Distance;
                    NearestPosition = entity.Position;
                    NearestId = entity.Id;
                }
            }

            private bool IsNearer(ReachableAdventureEntity entity)
            {
                int distanceCompare = entity.Distance.CompareTo(NearestDistance);
                if (distanceCompare != 0)
                {
                    return distanceCompare < 0;
                }

                int xCompare = entity.Position.x.CompareTo(NearestPosition.x);
                if (xCompare != 0)
                {
                    return xCompare < 0;
                }

                int yCompare = entity.Position.y.CompareTo(NearestPosition.y);
                if (yCompare != 0)
                {
                    return yCompare < 0;
                }

                return entity.Id.CompareTo(NearestId) < 0;
            }
        }
    }
}
