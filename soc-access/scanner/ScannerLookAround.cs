using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    public static class ScannerLookAround
    {
        public static ScannerSnapshot Build(ScannerSnapshot source, Vector2Int origin, int radius)
        {
            if (source == null || radius < 1)
            {
                return null;
            }

            ScannerSnapshot lookAround = new ScannerSnapshot();
            lookAround.MarkAsLookAroundSnapshot();

            ScannerCategory lookAroundCategory = lookAround.GetOrAddCategory(
                ScannerCategoryKeys.LookAround,
                () => ModText.Get(ModStrings.Scanner.LookAround));
            lookAroundCategory.FlatItems = true;
            ScannerSubcategory all = lookAroundCategory.GetOrAddSubcategory(
                ScannerSubcategoryKeys.All,
                () => ModText.Get(ModStrings.Scanner.All));
            ScannerProjection.Project(
                source, lookAroundCategory, all, result => IsWithinLookRadius(result, origin, radius));

            if (!all.HasResults)
            {
                return null;
            }

            lookAround.SortBy(origin, (left, right) => CompareDirectionally(origin, left, right));
            return lookAround;
        }

        private static bool IsWithinLookRadius(ScannerResult result, Vector2Int origin, int radius)
        {
            if (result == null || result.Position == origin || result.Kind != ScannerResultKind.Point)
            {
                return false;
            }

            int x = result.Position.x - origin.x;
            int y = result.Position.y - origin.y;
            return x * x + y * y <= radius * radius;
        }

        private static int CompareDirectionally(Vector2Int origin, ScannerResult left, ScannerResult right)
        {
            int angleCompare = AngleFromNorth(origin, left.Position).CompareTo(AngleFromNorth(origin, right.Position));
            if (angleCompare != 0)
            {
                return angleCompare;
            }

            int distanceCompare = ScannerSnapshot.DistanceSquared(origin, left.Position)
                .CompareTo(ScannerSnapshot.DistanceSquared(origin, right.Position));
            return distanceCompare != 0 ? distanceCompare : ScannerSnapshot.CompareTieBreak(left, right);
        }

        private static double AngleFromNorth(Vector2Int origin, Vector2Int point)
        {
            int x = point.x - origin.x;
            int y = point.y - origin.y;
            double angle = Math.Atan2(x, y);
            return angle < 0 ? angle + Math.PI * 2 : angle;
        }
    }
}
