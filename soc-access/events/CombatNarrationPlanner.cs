using System;
using System.Collections.Generic;
using System.Linq;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquestAccess.Events.Combat;
using UnityEngine;

namespace SongsOfConquestAccess.Events
{
    internal sealed class CombatNarrationSnapshot
    {
        public CombatNarrationSnapshot(int localTeamId, IEnumerable<int> yourAliveTroopIds, IEnumerable<int> enemyAliveTroopIds)
            : this(localTeamId, yourAliveTroopIds, enemyAliveTroopIds, null, null, null, null)
        {
        }

        public CombatNarrationSnapshot(
            int localTeamId,
            IEnumerable<int> yourAliveTroopIds,
            IEnumerable<int> enemyAliveTroopIds,
            IEnumerable<int> yourMeleeTroopIds,
            IEnumerable<int> enemyMeleeTroopIds,
            IEnumerable<int> yourRangedTroopIds,
            IEnumerable<int> enemyRangedTroopIds)
        {
            LocalTeamId = localTeamId;
            YourAliveTroopIds = yourAliveTroopIds != null ? yourAliveTroopIds.ToList() : new List<int>();
            EnemyAliveTroopIds = enemyAliveTroopIds != null ? enemyAliveTroopIds.ToList() : new List<int>();
            YourMeleeTroopIds = yourMeleeTroopIds != null ? yourMeleeTroopIds.ToList() : new List<int>();
            EnemyMeleeTroopIds = enemyMeleeTroopIds != null ? enemyMeleeTroopIds.ToList() : new List<int>();
            YourRangedTroopIds = yourRangedTroopIds != null ? yourRangedTroopIds.ToList() : new List<int>();
            EnemyRangedTroopIds = enemyRangedTroopIds != null ? enemyRangedTroopIds.ToList() : new List<int>();
            SummaryContext = new EffectTargetSummaryContext(
                localTeamId,
                YourAliveTroopIds,
                EnemyAliveTroopIds,
                YourMeleeTroopIds,
                EnemyMeleeTroopIds,
                YourRangedTroopIds,
                EnemyRangedTroopIds);
        }

        public int LocalTeamId { get; private set; }
        public IReadOnlyList<int> YourAliveTroopIds { get; private set; }
        public IReadOnlyList<int> EnemyAliveTroopIds { get; private set; }
        public IReadOnlyList<int> YourMeleeTroopIds { get; private set; }
        public IReadOnlyList<int> EnemyMeleeTroopIds { get; private set; }
        public IReadOnlyList<int> YourRangedTroopIds { get; private set; }
        public IReadOnlyList<int> EnemyRangedTroopIds { get; private set; }
        public EffectTargetSummaryContext SummaryContext { get; private set; }

        public static CombatNarrationSnapshot Empty
        {
            get { return new CombatNarrationSnapshot(-1, null, null, null, null, null, null); }
        }
    }

    internal sealed class CombatNarrationPlanner
    {
        private const int AcidCloudBattleMapEntityBlueprintId = 6;

        private readonly Queue<CombatNarrationItem> _pendingEvents = new Queue<CombatNarrationItem>();

        public bool HasPendingEvents
        {
            get { return _pendingEvents.Count > 0; }
        }

        public void Reset()
        {
            _pendingEvents.Clear();
        }

        public void Enqueue(CombatNarrationItem pending)
        {
            if (pending == null)
            {
                return;
            }

            _pendingEvents.Enqueue(pending);
        }

        public void EnqueueBacteriaSummary(CombatNarrationItem pending, CombatNarrationSnapshot snapshot)
        {
            if (pending == null)
            {
                return;
            }

            CombatNarrationItem mergeTarget = FindLatestBacteriaSummaryMergeTarget(pending);
            if (mergeTarget != null)
            {
                mergeTarget.MergeBacteriaSummary(pending, snapshot);
                return;
            }

            pending.RefreshBacteriaSummaryEvent(snapshot);
            if (!pending.HasNarratableBacteriaTargets)
            {
                return;
            }

            Enqueue(pending);
        }

        public IReadOnlyList<CombatNarrationItem> Flush()
        {
            if (_pendingEvents.Count == 0)
            {
                return new List<CombatNarrationItem>();
            }

            List<CombatNarrationItem> events = SuppressNonNarratableMapEntityCreations(_pendingEvents.ToList());
            events = CoalesceBacteriaLifecycleEvents(events);
            events = ReorderSpellEvents(events);
            _pendingEvents.Clear();
            return events;
        }

        private static List<CombatNarrationItem> SuppressNonNarratableMapEntityCreations(List<CombatNarrationItem> events)
        {
            if (events == null || events.Count == 0)
            {
                return new List<CombatNarrationItem>();
            }

            return events.Where(e => !IsSuppressedMapEntityCreation(e)).ToList();
        }

        private static bool IsSuppressedMapEntityCreation(CombatNarrationItem item)
        {
            if (item == null || item.Kind != CombatNarrationItemKind.MapEntityCreated)
            {
                return false;
            }

            MapEntityCreatedEvent created = item.Event as MapEntityCreatedEvent;
            return created != null
                && created.Entity != null
                && created.Entity.BlueprintId == AcidCloudBattleMapEntityBlueprintId;
        }

        private static List<CombatNarrationItem> CoalesceBacteriaLifecycleEvents(List<CombatNarrationItem> events)
        {
            if (events == null || events.Count < 2)
            {
                return events ?? new List<CombatNarrationItem>();
            }

            for (int i = 0; i < events.Count; i++)
            {
                CombatNarrationItem left = events[i];
                if (left == null || !left.IsBacteriaSummary || !left.HasNarratableBacteriaTargets)
                {
                    continue;
                }

                for (int j = i + 1; j < events.Count; j++)
                {
                    CombatNarrationItem right = events[j];
                    if (right == null || !right.IsBacteriaSummary)
                    {
                        continue;
                    }

                    CoalesceBacteriaTargets(left, right);
                }
            }

            List<CombatNarrationItem> result = new List<CombatNarrationItem>();
            for (int i = 0; i < events.Count; i++)
            {
                CombatNarrationItem item = events[i];
                if (item == null)
                {
                    continue;
                }

                if (item.IsBacteriaSummary)
                {
                    item.RefreshBacteriaSummaryEvent(item.SummarySnapshot ?? CombatNarrationSnapshot.Empty);
                    if (!item.HasNarratableBacteriaTargets)
                    {
                        continue;
                    }
                }

                result.Add(item);
            }

            return result;
        }

        private static void CoalesceBacteriaTargets(CombatNarrationItem left, CombatNarrationItem right)
        {
            if (left == null
                || right == null
                || !left.IsBacteriaSummary
                || !right.IsBacteriaSummary
                || left.Kind == right.Kind
                || !AreSameNarratedBacteria(left.BacteriaRef, right.BacteriaRef))
            {
                return;
            }

            CombatNarrationItem removal = left.Kind == CombatNarrationItemKind.BacteriaRemoved ? left : right;
            List<int> shared = left.GetBacteriaTargetIds()
                .Intersect(right.GetBacteriaTargetIds())
                .ToList();
            for (int i = 0; i < shared.Count; i++)
            {
                removal.RemoveBacteriaTarget(shared[i]);
            }
        }

        private static bool AreSameNarratedBacteria(BacteriaRef left, BacteriaRef right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            if (left.BacteriaType == right.BacteriaType)
            {
                return true;
            }

            return !string.IsNullOrWhiteSpace(left.Name)
                && !string.IsNullOrWhiteSpace(right.Name)
                && string.Equals(left.Name, right.Name, StringComparison.Ordinal);
        }

        private CombatNarrationItem FindLatestBacteriaSummaryMergeTarget(CombatNarrationItem pending)
        {
            if (pending == null || !pending.IsBacteriaSummary || _pendingEvents.Count == 0)
            {
                return null;
            }

            CombatNarrationItem[] events = _pendingEvents.ToArray();
            for (int i = events.Length - 1; i >= 0; i--)
            {
                CombatNarrationItem existing = events[i];
                if (existing.CanMergeBacteriaSummary(pending))
                {
                    return existing;
                }

                if (!existing.IsBacteriaSummary)
                {
                    return null;
                }
            }

            return null;
        }

        internal static List<CombatNarrationItem> ReorderSpellEvents(List<CombatNarrationItem> events)
        {
            if (events == null || events.Count < 2)
            {
                return events ?? new List<CombatNarrationItem>();
            }

            for (int i = 0; i < events.Count; i++)
            {
                if (events[i].Kind != CombatNarrationItemKind.Spell)
                {
                    continue;
                }

                int insertIndex = FindSpellInsertIndex(events, i);

                if (insertIndex == i)
                {
                    continue;
                }

                CombatNarrationItem spell = events[i];
                events.RemoveAt(i);
                events.Insert(insertIndex, spell);
            }

            return events;
        }

        private static int FindSpellInsertIndex(List<CombatNarrationItem> events, int spellIndex)
        {
            int blindFuryExchangeStart = FindBlindFuryExchangeStart(events, spellIndex);
            if (blindFuryExchangeStart < spellIndex)
            {
                return blindFuryExchangeStart;
            }

            int insertIndex = spellIndex;
            while (insertIndex > 0 && IsLikelySpellEffect(events[insertIndex - 1]))
            {
                insertIndex--;
            }

            return insertIndex;
        }

        private static int FindBlindFuryExchangeStart(List<CombatNarrationItem> events, int spellIndex)
        {
            int insertIndex = spellIndex;
            bool foundBlindFuryAttack = false;
            while (insertIndex > 0)
            {
                CombatNarrationItem previous = events[insertIndex - 1];
                if (IsLikelySpellEffect(previous))
                {
                    insertIndex--;
                    continue;
                }

                if (IsBlindFuryExchangeAttack(previous))
                {
                    foundBlindFuryAttack |= IsAttackWithTrigger(previous, AttackTrigger.BlindFury);
                    insertIndex--;
                    continue;
                }

                break;
            }

            return foundBlindFuryAttack ? insertIndex : spellIndex;
        }

        private static bool IsBlindFuryExchangeAttack(CombatNarrationItem pending)
        {
            return IsAttackWithTrigger(pending, AttackTrigger.BlindFury)
                || IsAttackWithTrigger(pending, AttackTrigger.Retaliation);
        }

        private static bool IsAttackWithTrigger(CombatNarrationItem pending, AttackTrigger trigger)
        {
            if (pending == null || pending.Kind != CombatNarrationItemKind.Attack)
            {
                return false;
            }

            AttackEvent attack = pending.Event as AttackEvent;
            return attack != null && attack.AttackTrigger == trigger;
        }

        private static bool IsLikelySpellEffect(CombatNarrationItem pending)
        {
            if (pending == null)
            {
                return false;
            }

            switch (pending.Kind)
            {
                case CombatNarrationItemKind.Damage:
                case CombatNarrationItemKind.BacteriaRemoved:
                case CombatNarrationItemKind.BacteriaModifierApplied:
                case CombatNarrationItemKind.TroopCreated:
                case CombatNarrationItemKind.MapEntityCreated:
                case CombatNarrationItemKind.MapEntityDestroyed:
                case CombatNarrationItemKind.Push:
                case CombatNarrationItemKind.Teleport:
                    return true;
                default:
                    return false;
            }
        }
    }

    internal sealed class CombatNarrationItem
    {
        public CombatNarrationItemKind Kind;
        public IAccessibilityEvent Event;
        public int TroopId = -1;
        public int EntityId = -1;
        public List<Vector2Int> Path;
        public Vector2Int From;
        public Vector2Int To;
        public BacteriaRef BacteriaRef;
        public List<TroopRef> BacteriaTargets;
        public List<CombatNarrationModifierTarget> BacteriaModifierTargets;
        public bool IsBacteriaSummary;
        public CombatNarrationSnapshot SummarySnapshot;
        public bool HasNarratableBacteriaTargets
        {
            get
            {
                if (!IsBacteriaSummary)
                {
                    return true;
                }

                if (Kind == CombatNarrationItemKind.BacteriaRemoved)
                {
                    return BacteriaTargets != null && BacteriaTargets.Count > 0;
                }

                if (Kind == CombatNarrationItemKind.BacteriaModifierApplied)
                {
                    return BacteriaModifierTargets != null && BacteriaModifierTargets.Count > 0;
                }

                return true;
            }
        }

        public static CombatNarrationItem Direct(CombatNarrationItemKind kind, IAccessibilityEvent accessibilityEvent)
        {
            return new CombatNarrationItem
            {
                Kind = kind,
                Event = accessibilityEvent
            };
        }

        public static CombatNarrationItem Create(CombatNarrationItemKind kind, IAccessibilityEvent accessibilityEvent, int troopId = -1, List<Vector2Int> path = null, int entityId = -1)
        {
            return new CombatNarrationItem
            {
                Kind = kind,
                Event = accessibilityEvent,
                TroopId = troopId,
                Path = path,
                EntityId = entityId
            };
        }

        public static CombatNarrationItem CreateBacteriaRemovalSummary(BacteriaRef bacteriaRef, TroopRef target, int troopId)
        {
            CombatNarrationItem pending = new CombatNarrationItem
            {
                Kind = CombatNarrationItemKind.BacteriaRemoved,
                TroopId = troopId,
                BacteriaRef = bacteriaRef,
                BacteriaTargets = new List<TroopRef>(),
                IsBacteriaSummary = true
            };

            pending.AddUniqueBacteriaTarget(target);
            return pending;
        }

        public static CombatNarrationItem CreateBacteriaModifierSummary(BacteriaRef bacteriaRef, TroopRef target, IList<ModifierChange> changes, int troopId)
        {
            CombatNarrationItem pending = new CombatNarrationItem
            {
                Kind = CombatNarrationItemKind.BacteriaModifierApplied,
                TroopId = troopId,
                BacteriaRef = bacteriaRef,
                BacteriaModifierTargets = new List<CombatNarrationModifierTarget>(),
                IsBacteriaSummary = true
            };

            pending.AddOrMergeModifierTarget(target, changes);
            return pending;
        }

        public static CombatNarrationItem Push(IAccessibilityEvent accessibilityEvent, int troopId, Vector2Int from, Vector2Int to)
        {
            return new CombatNarrationItem
            {
                Kind = CombatNarrationItemKind.Push,
                Event = accessibilityEvent,
                TroopId = troopId,
                From = from,
                To = to
            };
        }

        public bool CanMergeBacteriaSummary(CombatNarrationItem other)
        {
            if (other == null || !IsBacteriaSummary || !other.IsBacteriaSummary || Kind != other.Kind)
            {
                return false;
            }

            return BacteriaRef != null
                && other.BacteriaRef != null
                && BacteriaRef.BacteriaType == other.BacteriaRef.BacteriaType;
        }

        public void MergeBacteriaSummary(CombatNarrationItem other, CombatNarrationSnapshot snapshot)
        {
            if (other == null)
            {
                return;
            }

            if (Kind == CombatNarrationItemKind.BacteriaRemoved)
            {
                if (other.BacteriaTargets != null)
                {
                    for (int i = 0; i < other.BacteriaTargets.Count; i++)
                    {
                        AddUniqueBacteriaTarget(other.BacteriaTargets[i]);
                    }
                }
            }
            else if (Kind == CombatNarrationItemKind.BacteriaModifierApplied && other.BacteriaModifierTargets != null)
            {
                for (int i = 0; i < other.BacteriaModifierTargets.Count; i++)
                {
                    CombatNarrationModifierTarget target = other.BacteriaModifierTargets[i];
                    AddOrMergeModifierTarget(target.Target, target.Changes);
                }
            }

            RefreshBacteriaSummaryEvent(snapshot);
        }

        public void RefreshBacteriaSummaryEvent(CombatNarrationSnapshot snapshot)
        {
            SummarySnapshot = snapshot ?? CombatNarrationSnapshot.Empty;
            if (Kind == CombatNarrationItemKind.BacteriaRemoved)
            {
                List<TroopRef> targets = BacteriaTargets ?? new List<TroopRef>();
                Event = new BacteriaRemovedSummaryEvent(
                    BacteriaRef,
                    targets,
                    DetermineTargetSummaryKind(targets, SummarySnapshot));
            }
            else if (Kind == CombatNarrationItemKind.BacteriaModifierApplied)
            {
                List<BacteriaModifierTargetSummary> summaries = new List<BacteriaModifierTargetSummary>();
                List<TroopRef> targets = new List<TroopRef>();
                if (BacteriaModifierTargets != null)
                {
                    for (int i = 0; i < BacteriaModifierTargets.Count; i++)
                    {
                        CombatNarrationModifierTarget target = BacteriaModifierTargets[i];
                        summaries.Add(new BacteriaModifierTargetSummary(target.Target, target.Changes));
                        targets.Add(target.Target);
                    }
                }

                Event = new BacteriaModifierSummaryEvent(
                    BacteriaRef,
                    summaries,
                    DetermineTargetSummaryKind(targets, SummarySnapshot),
                    SummarySnapshot.SummaryContext);
            }
        }

        private static EffectTargetSummaryKind DetermineTargetSummaryKind(IList<TroopRef> targets, CombatNarrationSnapshot snapshot)
        {
            return snapshot != null && snapshot.SummaryContext != null
                ? snapshot.SummaryContext.Determine(targets)
                : EffectTargetSummaryKind.ExplicitTargets;
        }

        private void AddUniqueBacteriaTarget(TroopRef target)
        {
            if (target == null || target.Count <= 0)
            {
                return;
            }

            if (BacteriaTargets == null)
            {
                BacteriaTargets = new List<TroopRef>();
            }

            for (int i = 0; i < BacteriaTargets.Count; i++)
            {
                if (BacteriaTargets[i] != null && BacteriaTargets[i].TroopId == target.TroopId)
                {
                    return;
                }
            }

            BacteriaTargets.Add(target);
        }

        private void AddOrMergeModifierTarget(TroopRef target, IList<ModifierChange> changes)
        {
            if (target == null || target.Count <= 0)
            {
                return;
            }

            if (BacteriaModifierTargets == null)
            {
                BacteriaModifierTargets = new List<CombatNarrationModifierTarget>();
            }

            for (int i = 0; i < BacteriaModifierTargets.Count; i++)
            {
                CombatNarrationModifierTarget existing = BacteriaModifierTargets[i];
                if (existing.Target != null && existing.Target.TroopId == target.TroopId)
                {
                    existing.Changes = CombineModifierChanges(existing.Changes, changes);
                    return;
                }
            }

            BacteriaModifierTargets.Add(new CombatNarrationModifierTarget(target, changes));
        }

        public List<int> GetBacteriaTargetIds()
        {
            if (Kind == CombatNarrationItemKind.BacteriaRemoved)
            {
                return BacteriaTargets != null
                    ? BacteriaTargets.Where(t => t != null).Select(t => t.TroopId).Distinct().ToList()
                    : new List<int>();
            }

            if (Kind == CombatNarrationItemKind.BacteriaModifierApplied)
            {
                return BacteriaModifierTargets != null
                    ? BacteriaModifierTargets.Where(t => t != null && t.Target != null).Select(t => t.Target.TroopId).Distinct().ToList()
                    : new List<int>();
            }

            return new List<int>();
        }

        public void RemoveBacteriaTarget(int troopId)
        {
            if (Kind == CombatNarrationItemKind.BacteriaRemoved && BacteriaTargets != null)
            {
                BacteriaTargets.RemoveAll(t => t == null || t.TroopId == troopId);
            }
            else if (Kind == CombatNarrationItemKind.BacteriaModifierApplied && BacteriaModifierTargets != null)
            {
                BacteriaModifierTargets.RemoveAll(t => t == null || t.Target == null || t.Target.TroopId == troopId);
            }
        }

        private static List<ModifierChange> CombineModifierChanges(IList<ModifierChange> left, IList<ModifierChange> right)
        {
            List<ModifierChange> result = left != null
                ? new List<ModifierChange>(left)
                : new List<ModifierChange>();

            if (right == null)
            {
                return result;
            }

            for (int i = 0; i < right.Count; i++)
            {
                ModifierChange change = right[i];
                if (change == null)
                {
                    continue;
                }

                int index = result.FindIndex(existing => existing != null
                    && existing.ModifierType == change.ModifierType
                    && existing.ApplicationType == change.ApplicationType);
                if (index >= 0)
                {
                    ModifierChange existing = result[index];
                    result[index] = new ModifierChange(
                        existing.ModifierType,
                        existing.ApplicationType,
                        existing.Amount + change.Amount);
                }
                else
                {
                    result.Add(change);
                }
            }

            return result
                .Where(change => change != null && change.Amount != 0)
                .OrderBy(change => change.ModifierType.ToString())
                .ThenBy(change => change.ApplicationType.ToString())
                .ToList();
        }
    }

    internal sealed class CombatNarrationModifierTarget
    {
        public CombatNarrationModifierTarget(TroopRef target, IList<ModifierChange> changes)
        {
            Target = target;
            Changes = changes != null ? new List<ModifierChange>(changes) : new List<ModifierChange>();
        }

        public TroopRef Target;
        public List<ModifierChange> Changes;
    }

    internal enum CombatNarrationItemKind
    {
        NewTurn,
        NewRound,
        QueueChanged,
        Move,
        Attack,
        Damage,
        Spell,
        FaeyFire,
        BacteriaRemoved,
        BacteriaModifierApplied,
        EssenceGenerated,
        WielderEssenceGenerated,
        TroopCreated,
        MapEntityCreated,
        MapEntityDestroyed,
        Push,
        Ability,
        Teleport,
        BurrowUp,
        BattleResult
    }
}
