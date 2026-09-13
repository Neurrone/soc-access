using System;
using System.Collections.Generic;
using UnityEngine;

namespace SongsOfConquestAccess.Battlefields
{
    /// <summary>What one cell of a battlefield is, once the ground has been read: the kinds the
    /// cursor and the scanner tell apart. Words for these live with the screens, never here.</summary>
    public enum BattlefieldCellKind
    {
        OffGrid,
        Flat,
        Elevated,
        Cliff,
        Impassable,
        Wall,
        Tower,
        Stairs
    }

    /// <summary>What a group of cells is. One region is one thing a player can be told about.</summary>
    public enum BattlefieldRegionKind
    {
        Elevated,
        Cliff,
        Impassable,
        ChokePoint,
        Wall,
        Tower,
        Stairs
    }

    /// <summary>The shape of a region, where its shape is worth a word: a long thin one is a ridge
    /// (or, in impassable ground, a wall), one cell is a single cell, anything else a patch.</summary>
    public enum BattlefieldRegionShape
    {
        None,
        SingleCell,
        Patch,
        Ridge
    }

    /// <summary>One cell as the game answers for it. The caller reads these facts from whatever it
    /// has - the placement page's <c>MapFormat</c>, the battle level facade, the dump's own
    /// loader - and the analysis needs nothing else.</summary>
    public sealed class BattlefieldCell
    {
        public BattlefieldCell(Vector2Int point, bool onGrid, int elevation, bool impassable, int decoration)
        {
            Point = point;
            OnGrid = onGrid;
            Elevation = elevation;
            Impassable = impassable;
            Decoration = decoration;
        }

        public Vector2Int Point { get; private set; }

        public bool OnGrid { get; private set; }

        public int Elevation { get; private set; }

        public bool Impassable { get; private set; }

        public int Decoration { get; private set; }
    }

    /// <summary>A group of cells that is one thing: what kind it is, what shape, how high it rises
    /// and which cells it covers, nearest-first ordering left to whoever speaks it.</summary>
    public sealed class BattlefieldRegion
    {
        public BattlefieldRegion(BattlefieldRegionKind kind, BattlefieldRegionShape shape, int height, List<Vector2Int> cells)
        {
            Kind = kind;
            Shape = shape;
            Height = height;
            Cells = cells ?? new List<Vector2Int>();
            Key = kind.ToString().ToLowerInvariant()
                + ":" + (Cells.Count > 0 ? Cells[0].x : -1)
                + ":" + (Cells.Count > 0 ? Cells[0].y : -1);
        }

        public BattlefieldRegionKind Kind { get; private set; }

        public BattlefieldRegionShape Shape { get; private set; }

        /// <summary>The highest cell in the region; zero where height says nothing about it.</summary>
        public int Height { get; private set; }

        public List<Vector2Int> Cells { get; private set; }

        public int Count
        {
            get { return Cells.Count; }
        }

        /// <summary>Identity, not words: the kind and the region's lowest cell, which the terrain
        /// of a battle never changes, so a scanner item stays the same item between two scans.
        /// </summary>
        public string Key { get; private set; }
    }

    /// <summary>
    /// THE GROUND OF A BATTLEFIELD, READ ONCE. Pure and offline: the caller hands over the grid
    /// size, one <see cref="BattlefieldCell"/> per cell and whether the layout is a siege, and gets
    /// back what each cell is and every group of cells worth telling a player about.
    ///
    /// The rules, all of them the game's own:
    /// - troops step between the six hex neighbours only when the elevation differs by at most one,
    ///   so raised ground that nothing can step onto is a cliff and not a platform. The flood starts
    ///   from every flat enterable cell, which is where a troop can always stand;
    /// - a siege layout's decoration byte names its structures: 7 a wall, 6 a tower, 8 stairs, the
    ///   same three constants <c>BattleTroopPlacementCalculator</c> reads. They are walked on, so
    ///   they are enterable, and they are not elevated ground;
    /// - a choke point is the one or two cells whose loss would cut the walkable board in two.
    /// </summary>
    public sealed class BattlefieldTerrain
    {
        private const int TowerDecoration = 6;
        private const int WallDecoration = 7;
        private const int StairsDecoration = 8;

        /// <summary>How big each half of a split has to be before the cells between them are worth
        /// calling a choke point: a pocket smaller than this is a corner, not a half of the board.
        /// </summary>
        private const int ChokePointSplitSize = 5;

        private readonly int _width;
        private readonly int _height;
        private readonly BattlefieldCellKind[,] _kinds;
        private readonly int[,] _elevations;
        private readonly List<BattlefieldRegion> _regions;

        private BattlefieldTerrain(int width, int height, BattlefieldCellKind[,] kinds, int[,] elevations, List<BattlefieldRegion> regions)
        {
            _width = width;
            _height = height;
            _kinds = kinds;
            _elevations = elevations;
            _regions = regions;
        }

        public Vector2Int Size
        {
            get { return new Vector2Int(_width, _height); }
        }

        /// <summary>Every group worth naming, elevated ground first and the choke points last.
        /// </summary>
        public List<BattlefieldRegion> Regions
        {
            get { return _regions; }
        }

        public static BattlefieldTerrain Analyse(Vector2Int size, IEnumerable<BattlefieldCell> cells, bool isSiege)
        {
            int width = Math.Max(0, size.x);
            int height = Math.Max(0, size.y);
            BattlefieldCellKind[,] kinds = new BattlefieldCellKind[Math.Max(1, width), Math.Max(1, height)];
            int[,] elevations = new int[Math.Max(1, width), Math.Max(1, height)];
            bool[,] enterable = new bool[Math.Max(1, width), Math.Max(1, height)];
            bool[,] elevated = new bool[Math.Max(1, width), Math.Max(1, height)];

            if (cells != null)
            {
                foreach (BattlefieldCell cell in cells)
                {
                    if (cell == null || !Within(cell.Point, width, height))
                    {
                        continue;
                    }

                    int x = cell.Point.x;
                    int y = cell.Point.y;
                    elevations[x, y] = cell.Elevation;
                    if (!cell.OnGrid)
                    {
                        kinds[x, y] = BattlefieldCellKind.OffGrid;
                        continue;
                    }

                    BattlefieldCellKind structure = StructureKind(isSiege, cell.Decoration);
                    if (structure != BattlefieldCellKind.OffGrid)
                    {
                        kinds[x, y] = structure;
                        enterable[x, y] = true;
                        continue;
                    }

                    if (cell.Impassable)
                    {
                        kinds[x, y] = BattlefieldCellKind.Impassable;
                        continue;
                    }

                    enterable[x, y] = true;
                    // Settled below: raised ground the flood never reaches is a cliff.
                    elevated[x, y] = cell.Elevation > 0;
                    kinds[x, y] = cell.Elevation > 0 ? BattlefieldCellKind.Elevated : BattlefieldCellKind.Flat;
                }
            }

            bool[,] reached = FloodFromFlatGround(width, height, enterable, elevations);
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (elevated[x, y] && !reached[x, y])
                    {
                        kinds[x, y] = BattlefieldCellKind.Cliff;
                    }
                }
            }

            List<BattlefieldRegion> regions = new List<BattlefieldRegion>();
            AddRegions(regions, width, height, kinds, elevations, BattlefieldCellKind.Elevated, BattlefieldRegionKind.Elevated);
            AddRegions(regions, width, height, kinds, elevations, BattlefieldCellKind.Cliff, BattlefieldRegionKind.Cliff);
            AddRegions(regions, width, height, kinds, elevations, BattlefieldCellKind.Impassable, BattlefieldRegionKind.Impassable);
            AddRegions(regions, width, height, kinds, elevations, BattlefieldCellKind.Wall, BattlefieldRegionKind.Wall);
            AddRegions(regions, width, height, kinds, elevations, BattlefieldCellKind.Tower, BattlefieldRegionKind.Tower);
            AddRegions(regions, width, height, kinds, elevations, BattlefieldCellKind.Stairs, BattlefieldRegionKind.Stairs);
            regions.AddRange(ChokePoints(width, height, enterable, elevations));
            return new BattlefieldTerrain(width, height, kinds, elevations, regions);
        }

        public BattlefieldCellKind GetKind(Vector2Int point)
        {
            return Within(point, _width, _height) ? _kinds[point.x, point.y] : BattlefieldCellKind.OffGrid;
        }

        /// <summary>The neighbours of a cell that a troop standing on it cannot step to because the
        /// elevation differs by more than one. Empty for a cell nothing can stand on.</summary>
        public List<Vector2Int> CliffNeighbours(Vector2Int point)
        {
            List<Vector2Int> cliffs = new List<Vector2Int>();
            if (!Within(point, _width, _height) || !IsEnterable(_kinds[point.x, point.y]))
            {
                return cliffs;
            }

            foreach (Vector2Int neighbour in Neighbours(point, _width, _height))
            {
                if (IsEnterable(_kinds[neighbour.x, neighbour.y])
                    && Math.Abs(_elevations[neighbour.x, neighbour.y] - _elevations[point.x, point.y]) > 1)
                {
                    cliffs.Add(neighbour);
                }
            }

            return cliffs;
        }

        private static BattlefieldCellKind StructureKind(bool isSiege, int decoration)
        {
            if (!isSiege)
            {
                return BattlefieldCellKind.OffGrid;
            }

            switch (decoration)
            {
                case WallDecoration:
                    return BattlefieldCellKind.Wall;
                case TowerDecoration:
                    return BattlefieldCellKind.Tower;
                case StairsDecoration:
                    return BattlefieldCellKind.Stairs;
                default:
                    return BattlefieldCellKind.OffGrid;
            }
        }

        private static bool IsEnterable(BattlefieldCellKind kind)
        {
            return kind != BattlefieldCellKind.OffGrid && kind != BattlefieldCellKind.Impassable;
        }

        /// <summary>Everywhere a troop starting on flat ground can walk to, one elevation step at a
        /// time. Raised ground outside it is a cliff.</summary>
        private static bool[,] FloodFromFlatGround(int width, int height, bool[,] enterable, int[,] elevations)
        {
            bool[,] reached = new bool[Math.Max(1, width), Math.Max(1, height)];
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (enterable[x, y] && elevations[x, y] == 0)
                    {
                        reached[x, y] = true;
                        queue.Enqueue(new Vector2Int(x, y));
                    }
                }
            }

            while (queue.Count > 0)
            {
                Vector2Int point = queue.Dequeue();
                foreach (Vector2Int neighbour in Neighbours(point, width, height))
                {
                    if (!reached[neighbour.x, neighbour.y]
                        && enterable[neighbour.x, neighbour.y]
                        && Math.Abs(elevations[neighbour.x, neighbour.y] - elevations[point.x, point.y]) <= 1)
                    {
                        reached[neighbour.x, neighbour.y] = true;
                        queue.Enqueue(neighbour);
                    }
                }
            }

            return reached;
        }

        private static void AddRegions(
            List<BattlefieldRegion> regions,
            int width,
            int height,
            BattlefieldCellKind[,] kinds,
            int[,] elevations,
            BattlefieldCellKind cellKind,
            BattlefieldRegionKind regionKind)
        {
            bool[,] seen = new bool[Math.Max(1, width), Math.Max(1, height)];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (seen[x, y] || kinds[x, y] != cellKind)
                    {
                        continue;
                    }

                    List<Vector2Int> members = new List<Vector2Int>();
                    Queue<Vector2Int> grow = new Queue<Vector2Int>();
                    grow.Enqueue(new Vector2Int(x, y));
                    seen[x, y] = true;
                    while (grow.Count > 0)
                    {
                        Vector2Int point = grow.Dequeue();
                        members.Add(point);
                        foreach (Vector2Int neighbour in Neighbours(point, width, height))
                        {
                            if (!seen[neighbour.x, neighbour.y] && kinds[neighbour.x, neighbour.y] == cellKind)
                            {
                                seen[neighbour.x, neighbour.y] = true;
                                grow.Enqueue(neighbour);
                            }
                        }
                    }

                    members.Sort(CompareCells);
                    regions.Add(new BattlefieldRegion(
                        regionKind,
                        ShapeOf(regionKind, members, width, height),
                        MaxElevation(members, elevations),
                        members));
                }
            }
        }

        /// <summary>A region's shape: one cell, a ridge where it is at least three times as long as
        /// it is wide or spans more than half the board, and a patch otherwise. Impassable ground
        /// is only ever a wall (the same ridge test) and only from three cells up; the siege
        /// structures are named by what they are, so their shape says nothing.</summary>
        private static BattlefieldRegionShape ShapeOf(BattlefieldRegionKind kind, List<Vector2Int> members, int width, int height)
        {
            if (kind == BattlefieldRegionKind.Elevated)
            {
                return members.Count == 1
                    ? BattlefieldRegionShape.SingleCell
                    : IsRidge(members, width, height) ? BattlefieldRegionShape.Ridge : BattlefieldRegionShape.Patch;
            }

            if (kind == BattlefieldRegionKind.Impassable)
            {
                return members.Count >= 3 && IsRidge(members, width, height)
                    ? BattlefieldRegionShape.Ridge
                    : BattlefieldRegionShape.None;
            }

            return BattlefieldRegionShape.None;
        }

        private static bool IsRidge(List<Vector2Int> members, int width, int height)
        {
            int minX = int.MaxValue;
            int maxX = int.MinValue;
            int minY = int.MaxValue;
            int maxY = int.MinValue;
            for (int i = 0; i < members.Count; i++)
            {
                minX = Math.Min(minX, members[i].x);
                maxX = Math.Max(maxX, members[i].x);
                minY = Math.Min(minY, members[i].y);
                maxY = Math.Max(maxY, members[i].y);
            }

            int columns = maxX - minX + 1;
            int rows = maxY - minY + 1;
            int longest = Math.Max(columns, rows);
            int shortest = Math.Min(columns, rows);
            return longest >= 3 * shortest || columns > width / 2 || rows > height / 2;
        }

        private static int MaxElevation(List<Vector2Int> members, int[,] elevations)
        {
            int highest = 0;
            for (int i = 0; i < members.Count; i++)
            {
                highest = Math.Max(highest, elevations[members[i].x, members[i].y]);
            }

            return highest;
        }

        /// <summary>The cells everything has to pass through: one cell, or two next to each other,
        /// whose removal leaves the walkable board in two halves worth calling halves. Only the
        /// smallest such sets are reported, so a pair that owes its split to one of its own cells is
        /// left out. Brute force over 117 cells, run once per battle.</summary>
        private static List<BattlefieldRegion> ChokePoints(int width, int height, bool[,] enterable, int[,] elevations)
        {
            List<BattlefieldRegion> chokePoints = new List<BattlefieldRegion>();
            List<Vector2Int> main = LargestWalkableComponent(width, height, enterable, elevations);
            if (main.Count < (2 * ChokePointSplitSize) + 1)
            {
                return chokePoints;
            }

            HashSet<Vector2Int> inMain = new HashSet<Vector2Int>(main);
            HashSet<Vector2Int> singles = new HashSet<Vector2Int>();
            for (int i = 0; i < main.Count; i++)
            {
                HashSet<Vector2Int> removed = new HashSet<Vector2Int> { main[i] };
                if (SplitsInTwo(main, inMain, removed, width, height, elevations))
                {
                    singles.Add(main[i]);
                    chokePoints.Add(new BattlefieldRegion(
                        BattlefieldRegionKind.ChokePoint,
                        BattlefieldRegionShape.None,
                        0,
                        new List<Vector2Int> { main[i] }));
                }
            }

            for (int i = 0; i < main.Count; i++)
            {
                Vector2Int first = main[i];
                if (singles.Contains(first))
                {
                    continue;
                }

                foreach (Vector2Int second in Neighbours(first, width, height))
                {
                    // Each pair once, and never one a single cell already explains.
                    if (!inMain.Contains(second) || singles.Contains(second) || CompareCells(first, second) >= 0)
                    {
                        continue;
                    }

                    HashSet<Vector2Int> removed = new HashSet<Vector2Int> { first, second };
                    if (SplitsInTwo(main, inMain, removed, width, height, elevations))
                    {
                        List<Vector2Int> pair = new List<Vector2Int> { first, second };
                        pair.Sort(CompareCells);
                        chokePoints.Add(new BattlefieldRegion(
                            BattlefieldRegionKind.ChokePoint, BattlefieldRegionShape.None, 0, pair));
                    }
                }
            }

            return chokePoints;
        }

        private static bool SplitsInTwo(
            List<Vector2Int> main,
            HashSet<Vector2Int> inMain,
            HashSet<Vector2Int> removed,
            int width,
            int height,
            int[,] elevations)
        {
            HashSet<Vector2Int> seen = new HashSet<Vector2Int>(removed);
            int halves = 0;
            for (int i = 0; i < main.Count; i++)
            {
                if (seen.Contains(main[i]))
                {
                    continue;
                }

                int size = 0;
                Queue<Vector2Int> queue = new Queue<Vector2Int>();
                queue.Enqueue(main[i]);
                seen.Add(main[i]);
                while (queue.Count > 0)
                {
                    Vector2Int point = queue.Dequeue();
                    size++;
                    foreach (Vector2Int neighbour in Neighbours(point, width, height))
                    {
                        if (!seen.Contains(neighbour)
                            && inMain.Contains(neighbour)
                            && Math.Abs(elevations[neighbour.x, neighbour.y] - elevations[point.x, point.y]) <= 1)
                        {
                            seen.Add(neighbour);
                            queue.Enqueue(neighbour);
                        }
                    }
                }

                if (size >= ChokePointSplitSize)
                {
                    halves++;
                }
            }

            return halves >= 2;
        }

        /// <summary>The board a troop actually fights on: the biggest set of enterable cells joined
        /// by steps of at most one elevation. A pocket a cliff cut off is not part of it.</summary>
        private static List<Vector2Int> LargestWalkableComponent(int width, int height, bool[,] enterable, int[,] elevations)
        {
            List<Vector2Int> largest = new List<Vector2Int>();
            bool[,] seen = new bool[Math.Max(1, width), Math.Max(1, height)];
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (seen[x, y] || !enterable[x, y])
                    {
                        continue;
                    }

                    List<Vector2Int> component = new List<Vector2Int>();
                    Queue<Vector2Int> queue = new Queue<Vector2Int>();
                    queue.Enqueue(new Vector2Int(x, y));
                    seen[x, y] = true;
                    while (queue.Count > 0)
                    {
                        Vector2Int point = queue.Dequeue();
                        component.Add(point);
                        foreach (Vector2Int neighbour in Neighbours(point, width, height))
                        {
                            if (!seen[neighbour.x, neighbour.y]
                                && enterable[neighbour.x, neighbour.y]
                                && Math.Abs(elevations[neighbour.x, neighbour.y] - elevations[point.x, point.y]) <= 1)
                            {
                                seen[neighbour.x, neighbour.y] = true;
                                queue.Enqueue(neighbour);
                            }
                        }
                    }

                    if (component.Count > largest.Count)
                    {
                        largest = component;
                    }
                }
            }

            largest.Sort(CompareCells);
            return largest;
        }

        /// <summary>The six hex neighbours. Odd rows sit half a cell to the right, which is what
        /// makes the diagonal offsets depend on the row.</summary>
        public static IEnumerable<Vector2Int> Neighbours(Vector2Int point, int width, int height)
        {
            int shift = (point.y & 1) == 1 ? 1 : 0;
            int[,] offsets =
            {
                { -1, 0 }, { 1, 0 },
                { shift - 1, -1 }, { shift, -1 },
                { shift - 1, 1 }, { shift, 1 }
            };
            for (int i = 0; i < 6; i++)
            {
                int x = point.x + offsets[i, 0];
                int y = point.y + offsets[i, 1];
                if (x >= 0 && y >= 0 && x < width && y < height)
                {
                    yield return new Vector2Int(x, y);
                }
            }
        }

        private static int CompareCells(Vector2Int left, Vector2Int right)
        {
            int compare = left.y.CompareTo(right.y);
            return compare != 0 ? compare : left.x.CompareTo(right.x);
        }

        private static bool Within(Vector2Int point, int width, int height)
        {
            return point.x >= 0 && point.y >= 0 && point.x < width && point.y < height;
        }
    }
}
