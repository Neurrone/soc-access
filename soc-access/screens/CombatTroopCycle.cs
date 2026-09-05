using System.Collections.Generic;

namespace SongsOfConquestAccess.Screens
{
    public sealed class CombatTroopCycle
    {
        private int _anchorTroopId = -1;
        private bool _hasAnchor;

        public void Reset()
        {
            _anchorTroopId = -1;
            _hasAnchor = false;
        }

        public CombatTroopCycleResult AnchorFirst(IReadOnlyList<int> troopIds)
        {
            if (troopIds == null || troopIds.Count == 0)
            {
                Reset();
                return CombatTroopCycleResult.NoTarget();
            }

            _anchorTroopId = troopIds[0];
            _hasAnchor = true;
            return CombatTroopCycleResult.Target(_anchorTroopId, wrapped: false);
        }

        public CombatTroopCycleResult Move(IReadOnlyList<int> troopIds, int delta)
        {
            if (troopIds == null || troopIds.Count == 0 || delta == 0)
            {
                Reset();
                return CombatTroopCycleResult.NoTarget();
            }

            int currentIndex = _hasAnchor ? IndexOf(troopIds, _anchorTroopId) : -1;
            if (currentIndex < 0)
            {
                return AnchorFirst(troopIds);
            }

            int nextIndex = Mod(currentIndex + delta, troopIds.Count);
            bool wrapped = delta > 0
                ? nextIndex <= currentIndex
                : nextIndex >= currentIndex;

            _anchorTroopId = troopIds[nextIndex];
            _hasAnchor = true;
            return CombatTroopCycleResult.Target(_anchorTroopId, wrapped);
        }

        private static int IndexOf(IReadOnlyList<int> values, int value)
        {
            for (int i = 0; i < values.Count; i++)
            {
                if (values[i] == value)
                {
                    return i;
                }
            }

            return -1;
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }
    }

    public struct CombatTroopCycleResult
    {
        private CombatTroopCycleResult(bool moved, int troopId, bool wrapped)
        {
            Moved = moved;
            TroopId = troopId;
            Wrapped = wrapped;
        }

        public bool Moved { get; private set; }

        public int TroopId { get; private set; }

        public bool Wrapped { get; private set; }

        public static CombatTroopCycleResult NoTarget()
        {
            return new CombatTroopCycleResult(false, -1, false);
        }

        public static CombatTroopCycleResult Target(int troopId, bool wrapped)
        {
            return new CombatTroopCycleResult(true, troopId, wrapped);
        }
    }
}
