using System.Collections.Generic;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquestAccess.Battlefields;
using SongsOfConquestAccess.Localization;
using UnityEngine;

// WHAT THE BATTLEFIELD'S TILES AND ITS INSPECTION ARE, moved out of CombatAdapter.cs unchanged:
// one tile as the adapter reads it, the kinds of reach an inspection marks a tile with, and the
// inspection itself - the tiles it covers, the order they are walked in and how they are described.
namespace SongsOfConquestAccess.Adapters
{
    public sealed class CombatTile
    {
        public CombatTile(Vector2Int point)
        {
            Point = point;
        }

        public Vector2Int Point { get; private set; }

        public byte Elevation { get; set; }

        public bool IsReachable { get; set; }

        public bool IsImpassable { get; set; }

        public bool IsBlocked { get; set; }

        /// <summary>What the ground of this tile is, as the shared analysis read it: flat, raised,
        /// a cliff nothing can climb, impassable, or one of a siege layout's structures. The words
        /// for it belong to the screens.</summary>
        public BattlefieldCellKind Kind { get; set; }

        /// <summary>What blocks this tile, where it is blocked and the fight has painted something
        /// nameable on it. Null on open ground and wherever the obstacle has no name.</summary>
        public BattlefieldObstacle Obstacle { get; set; }

        /// <summary>Whether this tile is one of the cells everything has to pass through, as the
        /// shared analysis grouped them. The word for it belongs to the screens.</summary>
        public bool IsChokePoint { get; set; }

        public IBattleTroopState Troop { get; set; }

        public int TroopId { get; set; } = -1;

        public bool IsTroopAttackable { get; set; }

        public IMapEntity Entity { get; set; }

        public int EntityId { get; set; } = -1;

        public bool IsEntityAttackable { get; set; }

        public List<string> MapEffects { get; private set; } = new List<string>();

        public List<int> DangerousMapEffectEntityIds { get; private set; } = new List<int>();

        /// <summary>The game's names for what an ENEMY root spreader's roots do on this tile to the
        /// player's troops (Mother's Scorn). Empty where no such roots reach it.</summary>
        public List<string> HostileRootEffects { get; private set; } = new List<string>();

        /// <summary>The game's names for what the player's OWN root spreader's roots do on this tile
        /// for the player's troops (Mother's Embrace).</summary>
        public List<string> FriendlyRootEffects { get; private set; } = new List<string>();

        /// <summary>The game's names for what roots do to the troop STANDING on this tile, asked
        /// from that troop's side as the game's troop details ask it: the Scorn of the other
        /// team's spreaders, the Embrace of its own. Empty on a tile no troop stands on.</summary>
        public List<string> OccupantRootEffects { get; private set; } = new List<string>();

        public string DecorativeFeature { get; set; }
    }

    public enum CombatRangeIndicator
    {
        Source,
        Movement,
        Attack,
        Deadly,
        Melee,
        ZoneOfControl
    }

    public sealed class CombatInspectContext
    {
        private readonly Dictionary<Vector2Int, HashSet<CombatRangeIndicator>> _indicators =
            new Dictionary<Vector2Int, HashSet<CombatRangeIndicator>>();
        private readonly HashSet<Vector2Int> _reviewSet = new HashSet<Vector2Int>();
        private int _turn;
        private int _troopId = -1;
        private int _healthLost;
        private int _statusCount;
        private Vector2Int _actingTile;

        private CombatInspectContext(CombatInspectMode mode, Vector2Int pinnedTile)
        {
            Mode = mode;
            PinnedTile = pinnedTile;
            AddReviewTile(pinnedTile);
        }

        public CombatInspectMode Mode { get; private set; }

        public Vector2Int PinnedTile { get; private set; }

        public List<Vector2Int> OrderedTiles { get; private set; }

        public IDetails TooltipDetails { get; set; }

        /// <summary>What the board looked like when this inspection was taken: the battle's turn
        /// counter, who stood on the pinned tile with how much health lost and how many statuses, and
        /// where the acting troop stood - the path and the zone of control below were measured from
        /// it, and it walks without the turn moving on. The reach tiles and the dossier above are a
        /// SNAPSHOT of that moment, so these are what the game has to still answer for them to
        /// describe the board - see <see cref="CombatAdapter.IsInspectStateCurrent"/>.</summary>
        public void CaptureState(int turn, int troopId, int healthLost, int statusCount, Vector2Int actingTile)
        {
            _turn = turn;
            _troopId = troopId;
            _healthLost = healthLost;
            _statusCount = statusCount;
            _actingTile = actingTile;
        }

        public bool MatchesState(int turn, int troopId, int healthLost, int statusCount, Vector2Int actingTile)
        {
            return turn == _turn
                && troopId == _troopId
                && healthLost == _healthLost
                && statusCount == _statusCount
                && actingTile == _actingTile;
        }

        public static CombatInspectContext ForStack(Vector2Int pinnedTile)
        {
            return new CombatInspectContext(CombatInspectMode.Stack, pinnedTile);
        }

        public static CombatInspectContext ForPath(Vector2Int pinnedTile, List<Vector2Int> path)
        {
            CombatInspectContext context = new CombatInspectContext(CombatInspectMode.Path, pinnedTile);
            context.SetPath(path);
            return context;
        }

        public static CombatInspectContext ForEntityPath(Vector2Int pinnedTile, List<Vector2Int> path)
        {
            CombatInspectContext context = new CombatInspectContext(CombatInspectMode.EntityPath, pinnedTile);
            context.SetPath(path);
            return context;
        }

        public static CombatInspectContext ForEntityOnly(Vector2Int pinnedTile)
        {
            CombatInspectContext context = new CombatInspectContext(CombatInspectMode.EntityOnly, pinnedTile);
            context.OrderedTiles = new List<Vector2Int> { pinnedTile };
            return context;
        }

        public void Add(Vector2Int point, CombatRangeIndicator indicator)
        {
            AddReviewTile(point);
            HashSet<CombatRangeIndicator> set;
            if (!_indicators.TryGetValue(point, out set))
            {
                set = new HashSet<CombatRangeIndicator>();
                _indicators[point] = set;
            }

            set.Add(indicator);
        }

        public bool Contains(Vector2Int point)
        {
            return _reviewSet.Contains(point);
        }

        public void FinalizeOrdering()
        {
            if (OrderedTiles != null)
            {
                return;
            }

            OrderedTiles = new List<Vector2Int>(_reviewSet);
            OrderedTiles.Sort((left, right) =>
            {
                int y = right.y.CompareTo(left.y);
                return y != 0 ? y : left.x.CompareTo(right.x);
            });
        }

        public int CountConnectedComponents()
        {
            if (_reviewSet.Count == 0)
            {
                return 0;
            }

            HashSet<Vector2Int> remaining = new HashSet<Vector2Int>(_reviewSet);
            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            int count = 0;
            while (remaining.Count > 0)
            {
                Vector2Int start = default(Vector2Int);
                foreach (Vector2Int point in remaining)
                {
                    start = point;
                    break;
                }

                remaining.Remove(start);
                queue.Enqueue(start);
                count++;
                while (queue.Count > 0)
                {
                    Vector2Int current = queue.Dequeue();
                    Vector2Int[] neighbors = CombatAdapter.GetNeighbors(current);
                    for (int i = 0; i < neighbors.Length; i++)
                    {
                        if (remaining.Remove(neighbors[i]))
                        {
                            queue.Enqueue(neighbors[i]);
                        }
                    }
                }
            }

            return count;
        }

        public void AddIndicators(Vector2Int point, List<string> parts)
        {
            if (Mode == CombatInspectMode.Stack && point == PinnedTile)
            {
                return;
            }

            HashSet<CombatRangeIndicator> set;
            if (!_indicators.TryGetValue(point, out set))
            {
                return;
            }

            string text = FormatRangeIndicators(set);
            if (!string.IsNullOrWhiteSpace(text))
            {
                parts.Add(text);
            }
        }

        private void SetPath(List<Vector2Int> path)
        {
            _reviewSet.Clear();
            OrderedTiles = new List<Vector2Int>();
            if (path == null || path.Count == 0)
            {
                AddReviewTile(PinnedTile);
                OrderedTiles.Add(PinnedTile);
                return;
            }

            for (int i = 0; i < path.Count; i++)
            {
                AddReviewTile(path[i]);
                OrderedTiles.Add(path[i]);
            }
        }

        private void AddReviewTile(Vector2Int point)
        {
            _reviewSet.Add(point);
        }

        public static string FormatRangeIndicators(HashSet<CombatRangeIndicator> indicators)
        {
            if (indicators == null || indicators.Count == 0)
            {
                return string.Empty;
            }

            bool hasZoneOfControl = indicators.Contains(CombatRangeIndicator.ZoneOfControl);

            string attackRangeText = string.Empty;
            if (hasZoneOfControl)
            {
                attackRangeText = ModText.Get(ModStrings.Spatial.ZoneOfControl);
            }
            else if (indicators.Contains(CombatRangeIndicator.Deadly))
            {
                attackRangeText = ModText.Get(ModStrings.Spatial.DeadlyRange);
            }
            else if (indicators.Contains(CombatRangeIndicator.Attack) || indicators.Contains(CombatRangeIndicator.Melee))
            {
                attackRangeText = ModText.Get(ModStrings.Spatial.AttackRange);
            }

            bool hasMovement = indicators.Contains(CombatRangeIndicator.Movement);
            if (!hasMovement)
            {
                return attackRangeText;
            }

            if (!hasZoneOfControl && !string.IsNullOrWhiteSpace(attackRangeText))
            {
                string compactAttackText = indicators.Contains(CombatRangeIndicator.Deadly)
                    ? ModText.Get(ModStrings.Spatial.Deadly)
                    : ModText.Get(ModStrings.Spatial.Attack);
                return ModText.Get(ModStrings.Spatial.RangeAndMovement, compactAttackText);
            }

            return string.IsNullOrWhiteSpace(attackRangeText)
                ? ModText.Get(ModStrings.Spatial.MovementRange)
                : ModText.Get(ModStrings.Spatial.RangeAndMovement, attackRangeText);
        }

    }

    public enum CombatInspectMode
    {
        Stack,
        Path,
        EntityPath,
        EntityOnly
    }
}
