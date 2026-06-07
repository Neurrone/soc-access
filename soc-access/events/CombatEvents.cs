using System;
using System.Collections.Generic;
using System.Linq;
using SongsOfConquest;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Battle.Bacterias;
using SongsOfConquest.Common.Entities.Battle;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;
using static SongsOfConquestAccess.Events.Combat.CombatText;

namespace SongsOfConquestAccess.Events.Combat
{
    internal enum TargetKind
    {
        Troop,
        MapEntity,
        Commander,
        Tile
    }

    internal enum BeamFacing
    {
        Left,
        Right
    }

    internal enum EffectTargetSummaryKind
    {
        ExplicitTargets,
        AllTroops,
        YourTroops,
        EnemyTroops,
        AllMeleeTroops,
        YourMeleeTroops,
        EnemyMeleeTroops,
        AllRangedTroops,
        YourRangedTroops,
        EnemyRangedTroops
    }

    internal sealed class TroopRef
    {
        public TroopRef(int troopId, int teamId, int localTeamId, string name, int count, Vector2Int position)
        {
            TroopId = troopId;
            TeamId = teamId;
            LocalTeamId = localTeamId;
            Name = string.IsNullOrWhiteSpace(name) ? ModText.Get(ModStrings.Combat.Troop) : SpeechTextSanitizer.Normalize(name);
            Count = count;
            Position = position;
        }

        public int TroopId { get; private set; }
        public int TeamId { get; private set; }
        public int LocalTeamId { get; private set; }
        public string Name { get; private set; }
        public int Count { get; private set; }
        public Vector2Int Position { get; private set; }
        public bool IsEnemy { get { return LocalTeamId >= 0 && TeamId != LocalTeamId; } }

        public string Format(bool includePosition)
        {
            string text = IsEnemy
                ? ModText.Get(ModStrings.Combat.EnemyTroop, Count, Name)
                : ModText.Get(ModStrings.Combat.TroopQuantity, Count, Name);
            return includePosition ? ModText.Get(ModStrings.Combat.TroopAt, text, FormatPoint(Position)) : text;
        }
    }

    internal sealed class ActorRef
    {
        public ActorRef(TroopRef troop, bool isActingOnItsTurn)
        {
            Troop = troop;
            IsActingOnItsTurn = isActingOnItsTurn;
        }

        public TroopRef Troop { get; private set; }
        public bool IsActingOnItsTurn { get; private set; }

        public string Format()
        {
            return IsActingOnItsTurn && Troop != null
                ? Troop.Name
                : Troop != null ? Troop.Format(includePosition: true) : ModText.Get(ModStrings.Combat.UnknownTroop);
        }
    }

    internal sealed class EntityRef
    {
        public EntityRef(int entityId, string name, Vector2Int position)
            : this(entityId, -1, name, position)
        {
        }

        public EntityRef(int entityId, int blueprintId, string name, Vector2Int position)
        {
            EntityId = entityId;
            BlueprintId = blueprintId;
            Name = string.IsNullOrWhiteSpace(name) ? ModText.Get(ModStrings.Combat.AttackableEntity) : SpeechTextSanitizer.Normalize(name);
            Position = position;
        }

        public int EntityId { get; private set; }
        public int BlueprintId { get; private set; }
        public string Name { get; private set; }
        public Vector2Int Position { get; private set; }

        public string Format()
        {
            return ModText.Get(ModStrings.Combat.TroopAt, Name, FormatPoint(Position));
        }
    }

    internal sealed class CommanderRef
    {
        public CommanderRef(int commanderId, int teamId, int localTeamId, string name)
        {
            CommanderId = commanderId;
            TeamId = teamId;
            LocalTeamId = localTeamId;
            Name = string.IsNullOrWhiteSpace(name) ? ModText.Get(ModStrings.Combat.Wielder) : SpeechTextSanitizer.Normalize(name);
        }

        public int CommanderId { get; private set; }
        public int TeamId { get; private set; }
        public int LocalTeamId { get; private set; }
        public string Name { get; private set; }
        public bool IsEnemy { get { return LocalTeamId >= 0 && TeamId != LocalTeamId; } }

        public string Format()
        {
            return IsEnemy ? ModText.Get(ModStrings.Combat.EnemyCommander, Name) : Name;
        }
    }

    internal sealed class TargetRef
    {
        private TargetRef(TargetKind targetKind)
        {
            TargetKind = targetKind;
        }

        public TargetKind TargetKind { get; private set; }
        public TroopRef Troop { get; private set; }
        public EntityRef Entity { get; private set; }
        public CommanderRef Commander { get; private set; }
        public Vector2Int Tile { get; private set; }

        public static TargetRef FromTroop(TroopRef troop)
        {
            return new TargetRef(TargetKind.Troop) { Troop = troop };
        }

        public static TargetRef FromEntity(EntityRef entity)
        {
            return new TargetRef(TargetKind.MapEntity) { Entity = entity };
        }

        public static TargetRef FromCommander(CommanderRef commander)
        {
            return new TargetRef(TargetKind.Commander) { Commander = commander };
        }

        public static TargetRef FromTile(Vector2Int tile)
        {
            return new TargetRef(TargetKind.Tile) { Tile = tile };
        }

        public string Format()
        {
            switch (TargetKind)
            {
                case TargetKind.Troop:
                    return Troop != null ? Troop.Format(includePosition: true) : ModText.Get(ModStrings.Combat.UnknownTroop);
                case TargetKind.MapEntity:
                    return Entity != null ? Entity.Format() : ModText.Get(ModStrings.Combat.UnknownEntity);
                case TargetKind.Commander:
                    return Commander != null ? Commander.Format() : ModText.Get(ModStrings.Combat.Wielder);
                case TargetKind.Tile:
                    return ModText.Get(ModStrings.Combat.Tile, FormatPoint(Tile));
                default:
                    return ModText.Get(ModStrings.Combat.UnknownTarget);
            }
        }
    }

    internal sealed class SpellRef
    {
        public SpellRef(SpellTypes spellType, string name, int tier)
        {
            SpellType = spellType;
            Name = string.IsNullOrWhiteSpace(name) ? SplitPascalCase(spellType.ToString()) : SpeechTextSanitizer.Normalize(name);
            Tier = tier;
        }

        public SpellTypes SpellType { get; private set; }
        public string Name { get; private set; }
        public int Tier { get; private set; }
    }

    internal sealed class AbilityRef
    {
        public AbilityRef(TroopAbilityType abilityType, string name)
        {
            AbilityType = abilityType;
            Name = string.IsNullOrWhiteSpace(name) ? SplitPascalCase(abilityType.ToString()) : SpeechTextSanitizer.Normalize(name);
        }

        public TroopAbilityType AbilityType { get; private set; }
        public string Name { get; private set; }
    }

    internal sealed class BacteriaRef
    {
        public BacteriaRef(int bacteriaId, BacteriaTypes bacteriaType, string name)
        {
            BacteriaId = bacteriaId;
            BacteriaType = bacteriaType;
            Name = string.IsNullOrWhiteSpace(name) ? SplitPascalCase(bacteriaType.ToString()) : SpeechTextSanitizer.Normalize(name);
        }

        public int BacteriaId { get; private set; }
        public BacteriaTypes BacteriaType { get; private set; }
        public string Name { get; private set; }
    }

    internal sealed class ModifierChange
    {
        public ModifierChange(BacteriaModifierType modifierType, BacteriaModifierApplicationType applicationType, int amount)
        {
            ModifierType = modifierType;
            ApplicationType = applicationType;
            Amount = amount;
        }

        public BacteriaModifierType ModifierType { get; private set; }
        public BacteriaModifierApplicationType ApplicationType { get; private set; }
        public int Amount { get; private set; }

        public string Format()
        {
            string name = FormatModifierType(ModifierType);
            string amount = (Amount > 0 ? "+" : string.Empty) + Amount;
            return ApplicationType == BacteriaModifierApplicationType.Percentage
                ? name + " " + amount + "%"
                : name + " " + amount;
        }
    }

    internal sealed class BacteriaModifierTargetSummary
    {
        public BacteriaModifierTargetSummary(TroopRef target, IList<ModifierChange> changes)
        {
            Target = target;
            Changes = changes != null ? new List<ModifierChange>(changes) : new List<ModifierChange>();
        }

        public TroopRef Target { get; private set; }
        public IReadOnlyList<ModifierChange> Changes { get; private set; }
    }

    internal static class EffectTargetSummary
    {
        public static string FormatTargets(IList<TroopRef> targets, EffectTargetSummaryKind kind)
        {
            switch (kind)
            {
                case EffectTargetSummaryKind.AllTroops:
                    return ModText.Get(ModStrings.Combat.AllTroops);
                case EffectTargetSummaryKind.YourTroops:
                    return ModText.Get(ModStrings.Combat.YourTroops);
                case EffectTargetSummaryKind.EnemyTroops:
                    return ModText.Get(ModStrings.Combat.EnemyTroops);
                case EffectTargetSummaryKind.AllMeleeTroops:
                    return ModText.Get(ModStrings.Combat.AllMeleeTroops);
                case EffectTargetSummaryKind.YourMeleeTroops:
                    return ModText.Get(ModStrings.Combat.YourMeleeTroops);
                case EffectTargetSummaryKind.EnemyMeleeTroops:
                    return ModText.Get(ModStrings.Combat.EnemyMeleeTroops);
                case EffectTargetSummaryKind.AllRangedTroops:
                    return ModText.Get(ModStrings.Combat.AllRangedTroops);
                case EffectTargetSummaryKind.YourRangedTroops:
                    return ModText.Get(ModStrings.Combat.YourRangedTroops);
                case EffectTargetSummaryKind.EnemyRangedTroops:
                    return ModText.Get(ModStrings.Combat.EnemyRangedTroops);
                default:
                    return FormatExplicitTargets(targets);
            }
        }

        public static string FormatExplicitTargets(IList<TroopRef> targets)
        {
            return FormatList(targets != null
                ? targets.Select(t => t != null ? t.Format(includePosition: true) : ModText.Get(ModStrings.Combat.UnknownTroop)).ToList()
                : new List<string>());
        }
    }

    internal sealed class EffectTargetSummaryContext
    {
        public EffectTargetSummaryContext(
            int localTeamId,
            IEnumerable<int> yourTroops,
            IEnumerable<int> enemyTroops,
            IEnumerable<int> yourMeleeTroops,
            IEnumerable<int> enemyMeleeTroops,
            IEnumerable<int> yourRangedTroops,
            IEnumerable<int> enemyRangedTroops)
        {
            LocalTeamId = localTeamId;
            YourTroops = CopySet(yourTroops);
            EnemyTroops = CopySet(enemyTroops);
            YourMeleeTroops = CopySet(yourMeleeTroops);
            EnemyMeleeTroops = CopySet(enemyMeleeTroops);
            YourRangedTroops = CopySet(yourRangedTroops);
            EnemyRangedTroops = CopySet(enemyRangedTroops);
        }

        public int LocalTeamId { get; private set; }
        public HashSet<int> YourTroops { get; private set; }
        public HashSet<int> EnemyTroops { get; private set; }
        public HashSet<int> YourMeleeTroops { get; private set; }
        public HashSet<int> EnemyMeleeTroops { get; private set; }
        public HashSet<int> YourRangedTroops { get; private set; }
        public HashSet<int> EnemyRangedTroops { get; private set; }

        public static EffectTargetSummaryContext Empty
        {
            get { return new EffectTargetSummaryContext(-1, null, null, null, null, null, null); }
        }

        public EffectTargetSummaryKind Determine(IList<TroopRef> targets)
        {
            if (targets == null || targets.Count == 0 || LocalTeamId < 0)
            {
                return EffectTargetSummaryKind.ExplicitTargets;
            }

            HashSet<int> affected = new HashSet<int>();
            for (int i = 0; i < targets.Count; i++)
            {
                TroopRef target = targets[i];
                if (target == null || target.TroopId < 0)
                {
                    return EffectTargetSummaryKind.ExplicitTargets;
                }

                affected.Add(target.TroopId);
            }

            if (affected.Count < 2)
            {
                return EffectTargetSummaryKind.ExplicitTargets;
            }

            if (MatchesUnion(affected, YourTroops, EnemyTroops))
            {
                return EffectTargetSummaryKind.AllTroops;
            }

            if (Matches(affected, YourTroops))
            {
                return EffectTargetSummaryKind.YourTroops;
            }

            if (Matches(affected, EnemyTroops))
            {
                return EffectTargetSummaryKind.EnemyTroops;
            }

            if (MatchesUnion(affected, YourMeleeTroops, EnemyMeleeTroops))
            {
                return EffectTargetSummaryKind.AllMeleeTroops;
            }

            if (Matches(affected, YourMeleeTroops))
            {
                return EffectTargetSummaryKind.YourMeleeTroops;
            }

            if (Matches(affected, EnemyMeleeTroops))
            {
                return EffectTargetSummaryKind.EnemyMeleeTroops;
            }

            if (MatchesUnion(affected, YourRangedTroops, EnemyRangedTroops))
            {
                return EffectTargetSummaryKind.AllRangedTroops;
            }

            if (Matches(affected, YourRangedTroops))
            {
                return EffectTargetSummaryKind.YourRangedTroops;
            }

            if (Matches(affected, EnemyRangedTroops))
            {
                return EffectTargetSummaryKind.EnemyRangedTroops;
            }

            return EffectTargetSummaryKind.ExplicitTargets;
        }

        private static bool Matches(HashSet<int> affected, HashSet<int> expected)
        {
            return expected != null && expected.Count > 0 && affected.SetEquals(expected);
        }

        private static bool MatchesUnion(HashSet<int> affected, HashSet<int> left, HashSet<int> right)
        {
            if (left == null || right == null || left.Count == 0 || right.Count == 0)
            {
                return false;
            }

            HashSet<int> combined = new HashSet<int>(left);
            combined.UnionWith(right);
            return affected.SetEquals(combined);
        }

        private static HashSet<int> CopySet(IEnumerable<int> values)
        {
            return values != null ? new HashSet<int>(values) : new HashSet<int>();
        }
    }

    internal sealed class FaeyFireDamageSummary
    {
        public FaeyFireDamageSummary(TargetRef target, int boltCount, int totalDamage, int totalKills)
        {
            Target = target;
            BoltCount = boltCount;
            TotalDamage = totalDamage;
            TotalKills = totalKills;
        }

        public TargetRef Target { get; private set; }
        public int BoltCount { get; private set; }
        public int TotalDamage { get; private set; }
        public int TotalKills { get; private set; }

        public string FormatBoltText()
        {
            return ModText.Plural(ModStrings.Combat.BoltAt, BoltCount, BoltCount, Target.Format());
        }
    }

    internal sealed class NewTurnEvent : IAccessibilityEvent
    {
        public NewTurnEvent(TroopRef troop) { Troop = troop; }
        public string Kind { get { return AccessibilityEvents.Combat.NewTurn; } }
        public TroopRef Troop { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.NewTurn, Troop.Format(includePosition: true)); }
    }

    internal sealed class NewRoundEvent : IAccessibilityEvent
    {
        public NewRoundEvent(int roundNumber) { RoundNumber = roundNumber; }
        public string Kind { get { return AccessibilityEvents.Combat.NewRound; } }
        public int RoundNumber { get; private set; }
        public string GetSpeechText() { return RoundNumber > 0 ? ModText.Get(ModStrings.Screens.Round, RoundNumber) : string.Empty; }
    }

    internal sealed class QueueChangedEvent : IAccessibilityEvent
    {
        public string Kind { get { return AccessibilityEvents.Combat.QueueChanged; } }
        public string GetSpeechText() { return string.Empty; }
    }

    internal sealed class TroopMovedEvent : IAccessibilityEvent
    {
        public TroopMovedEvent(ActorRef actor, Vector2Int from, Vector2Int to, IList<Vector2Int> path)
        {
            Actor = actor;
            From = from;
            To = to;
            Path = Copy(path);
        }

        public string Kind { get { return AccessibilityEvents.Combat.TroopMoved; } }
        public ActorRef Actor { get; private set; }
        public Vector2Int From { get; private set; }
        public Vector2Int To { get; private set; }
        public IReadOnlyList<Vector2Int> Path { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.TroopMoved, Actor.Format(), FormatPoint(To)); }
    }

    internal sealed class AttackEvent : IAccessibilityEvent
    {
        public AttackEvent(ActorRef attacker, TargetRef target, AttackTrigger attackTrigger)
        {
            Attacker = attacker;
            Target = target;
            AttackTrigger = attackTrigger;
        }

        public string Kind { get { return AccessibilityEvents.Combat.Attack; } }
        public ActorRef Attacker { get; private set; }
        public TargetRef Target { get; private set; }
        public AttackTrigger AttackTrigger { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.Attack, Attacker.Format(), FormatAttackVerb(AttackTrigger), Target.Format()); }
    }

    internal sealed class DirectionalAttackEvent : IAccessibilityEvent
    {
        public DirectionalAttackEvent(ActorRef attacker, BeamFacing direction)
        {
            Attacker = attacker;
            Direction = direction;
        }

        public string Kind { get { return AccessibilityEvents.Combat.Attack; } }
        public ActorRef Attacker { get; private set; }
        public BeamFacing Direction { get; private set; }
        public string GetSpeechText()
        {
            return Direction == BeamFacing.Right
                ? ModText.Get(ModStrings.Combat.AttackRight, Attacker.Format())
                : ModText.Get(ModStrings.Combat.AttackLeft, Attacker.Format());
        }
    }

    internal sealed class DamageEvent : IAccessibilityEvent
    {
        public DamageEvent(ActorRef attacker, TargetRef target, int damage, int kills, int sizeBefore, int sizeAfter, DamageType damageType, AttackTrigger attackTrigger, bool isSplashDamage, BacteriaRef bacteria)
        {
            Attacker = attacker;
            Target = target;
            Damage = damage;
            Kills = kills;
            SizeBefore = sizeBefore;
            SizeAfter = sizeAfter;
            DamageType = damageType;
            AttackTrigger = attackTrigger;
            IsSplashDamage = isSplashDamage;
            Bacteria = bacteria;
        }

        public string Kind { get { return AccessibilityEvents.Combat.Damage; } }
        public ActorRef Attacker { get; private set; }
        public TargetRef Target { get; private set; }
        public int Damage { get; private set; }
        public int Kills { get; private set; }
        public int SizeBefore { get; private set; }
        public int SizeAfter { get; private set; }
        public DamageType DamageType { get; private set; }
        public AttackTrigger AttackTrigger { get; private set; }
        public bool IsSplashDamage { get; private set; }
        public BacteriaRef Bacteria { get; private set; }

        public string GetSpeechText()
        {
            string kind = FormatDamageKind(DamageType, AttackTrigger, IsSplashDamage, Bacteria);
            string suffix = string.Empty;
            if (Target.TargetKind == TargetKind.Troop && Kills > 0)
            {
                suffix = ModText.Get(ModStrings.Combat.DamageKillsSuffix, Kills);
            }
            else if (Target.TargetKind == TargetKind.MapEntity && SizeAfter <= 0)
            {
                suffix = ModText.Get(ModStrings.Combat.DamageDestroyingItSuffix);
            }

            if (Attacker == null)
            {
                return ModText.Get(ModStrings.Combat.TakesDamage, Target.Format(), Damage, kind, suffix);
            }

            return ModText.Get(ModStrings.Combat.DealsDamage, Attacker.Format(), Damage, kind, Target.Format(), suffix);
        }
    }

    internal sealed class SpellCastEvent : IAccessibilityEvent
    {
        public SpellCastEvent(CommanderRef caster, SpellRef spell, IList<Vector2Int> selectedTargetPoints, IList<TargetRef> affectedTargets)
        {
            Caster = caster;
            Spell = spell;
            SelectedTargetPoints = CopyDistinct(selectedTargetPoints);
            AffectedTargets = affectedTargets != null ? new List<TargetRef>(affectedTargets) : new List<TargetRef>();
        }

        public string Kind { get { return AccessibilityEvents.Combat.SpellCast; } }
        public CommanderRef Caster { get; private set; }
        public SpellRef Spell { get; private set; }
        public IReadOnlyList<Vector2Int> SelectedTargetPoints { get; private set; }
        public IReadOnlyList<TargetRef> AffectedTargets { get; private set; }

        public string GetSpeechText()
        {
            string text = ModText.Get(ModStrings.Combat.CastsSpell, Caster.Format(), Spell.Name);
            if (SelectedTargetPoints.Count > 0)
            {
                text = ModText.Get(
                    ModStrings.Combat.CastsSpellAt,
                    Caster.Format(),
                    Spell.Name,
                    FormatList(SelectedTargetPoints.Select(FormatPoint).ToList()));
            }

            return text;
        }

        private static List<Vector2Int> CopyDistinct(IList<Vector2Int> points)
        {
            List<Vector2Int> result = new List<Vector2Int>();
            if (points == null)
            {
                return result;
            }

            HashSet<Vector2Int> seen = new HashSet<Vector2Int>();
            for (int i = 0; i < points.Count; i++)
            {
                Vector2Int point = points[i];
                if (seen.Add(point))
                {
                    result.Add(point);
                }
            }

            return result;
        }
    }

    internal sealed class FaeyFireEvent : IAccessibilityEvent
    {
        public FaeyFireEvent(ActorRef attacker, IList<FaeyFireDamageSummary> damageSummaries)
        {
            Attacker = attacker;
            DamageSummaries = damageSummaries != null ? new List<FaeyFireDamageSummary>(damageSummaries) : new List<FaeyFireDamageSummary>();
        }

        public string Kind { get { return AccessibilityEvents.Combat.FaeyFire; } }
        public ActorRef Attacker { get; private set; }
        public IReadOnlyList<FaeyFireDamageSummary> DamageSummaries { get; private set; }

        public string GetSpeechText()
        {
            if (DamageSummaries.Count == 0)
            {
                return ModText.Get(ModStrings.Combat.CastsSpell, Attacker.Format(), ModText.Get(ModStrings.Combat.FaeyFire));
            }

            return ModText.Get(
                ModStrings.Combat.FaeyFireWithBolts,
                Attacker.Format(),
                ModText.Get(ModStrings.Combat.FaeyFire),
                FormatList(DamageSummaries.Select(s => s.FormatBoltText()).ToList()));
        }
    }

    internal sealed class BacteriaRemovedEvent : IAccessibilityEvent
    {
        public BacteriaRemovedEvent(TroopRef target, BacteriaRef bacteria) { Target = target; Bacteria = bacteria; }
        public string Kind { get { return AccessibilityEvents.Combat.BacteriaRemoved; } }
        public TroopRef Target { get; private set; }
        public BacteriaRef Bacteria { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.RemovedFrom, Bacteria.Name, Target.Format(includePosition: true)); }
    }

    internal sealed class BacteriaModifierAppliedEvent : IAccessibilityEvent
    {
        public BacteriaModifierAppliedEvent(TroopRef target, BacteriaRef bacteria, IList<ModifierChange> changes)
        {
            Target = target;
            Bacteria = bacteria;
            Changes = changes != null ? new List<ModifierChange>(changes) : new List<ModifierChange>();
        }

        public string Kind { get { return AccessibilityEvents.Combat.BacteriaModifierApplied; } }
        public TroopRef Target { get; private set; }
        public BacteriaRef Bacteria { get; private set; }
        public IReadOnlyList<ModifierChange> Changes { get; private set; }

        public string GetSpeechText()
        {
            string text = ModText.Get(ModStrings.Combat.Affects, Bacteria.Name, Target.Format(includePosition: true));
            if (Changes.Count > 0)
            {
                text += ", " + FormatList(Changes.Select(c => c.Format()).ToList());
            }

            return text;
        }
    }

    internal sealed class BacteriaRemovedSummaryEvent : IAccessibilityEvent
    {
        public BacteriaRemovedSummaryEvent(BacteriaRef bacteria, IList<TroopRef> targets, EffectTargetSummaryKind targetSummaryKind)
        {
            Bacteria = bacteria;
            Targets = targets != null ? new List<TroopRef>(targets) : new List<TroopRef>();
            TargetSummaryKind = targetSummaryKind;
        }

        public string Kind { get { return AccessibilityEvents.Combat.BacteriaRemovedSummary; } }
        public BacteriaRef Bacteria { get; private set; }
        public IReadOnlyList<TroopRef> Targets { get; private set; }
        public EffectTargetSummaryKind TargetSummaryKind { get; private set; }

        public string GetSpeechText()
        {
            string name = Bacteria != null ? Bacteria.Name : ModText.Get(ModStrings.Combat.Effect);
            return ModText.Get(ModStrings.Combat.RemovedFrom, name, EffectTargetSummary.FormatTargets(Targets.ToList(), TargetSummaryKind));
        }
    }

    internal sealed class BacteriaModifierSummaryEvent : IAccessibilityEvent
    {
        public BacteriaModifierSummaryEvent(BacteriaRef bacteria, IList<BacteriaModifierTargetSummary> targets, EffectTargetSummaryKind targetSummaryKind)
            : this(bacteria, targets, targetSummaryKind, EffectTargetSummaryContext.Empty)
        {
        }

        public BacteriaModifierSummaryEvent(BacteriaRef bacteria, IList<BacteriaModifierTargetSummary> targets, EffectTargetSummaryKind targetSummaryKind, EffectTargetSummaryContext targetSummaryContext)
        {
            Bacteria = bacteria;
            Targets = targets != null ? new List<BacteriaModifierTargetSummary>(targets) : new List<BacteriaModifierTargetSummary>();
            TargetSummaryKind = targetSummaryKind;
            TargetSummaryContext = targetSummaryContext ?? EffectTargetSummaryContext.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Combat.BacteriaModifierSummary; } }
        public BacteriaRef Bacteria { get; private set; }
        public IReadOnlyList<BacteriaModifierTargetSummary> Targets { get; private set; }
        public EffectTargetSummaryKind TargetSummaryKind { get; private set; }
        public EffectTargetSummaryContext TargetSummaryContext { get; private set; }

        public string GetSpeechText()
        {
            if (Targets.Count == 0)
            {
                return string.Empty;
            }

            Dictionary<string, List<BacteriaModifierTargetSummary>> byChanges = new Dictionary<string, List<BacteriaModifierTargetSummary>>();
            List<string> changeKeys = new List<string>();
            for (int i = 0; i < Targets.Count; i++)
            {
                BacteriaModifierTargetSummary target = Targets[i];
                string key = FormatChanges(target.Changes);
                List<BacteriaModifierTargetSummary> group;
                if (!byChanges.TryGetValue(key, out group))
                {
                    group = new List<BacteriaModifierTargetSummary>();
                    byChanges[key] = group;
                    changeKeys.Add(key);
                }

                group.Add(target);
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < changeKeys.Count; i++)
            {
                KeyValuePair<string, List<BacteriaModifierTargetSummary>> group =
                    new KeyValuePair<string, List<BacteriaModifierTargetSummary>>(changeKeys[i], byChanges[changeKeys[i]]);
                List<TroopRef> groupTargets = group.Value.Select(t => t.Target).ToList();
                EffectTargetSummaryKind kind = byChanges.Count == 1 ? TargetSummaryKind : TargetSummaryContext.Determine(groupTargets);
                string targets = EffectTargetSummary.FormatTargets(groupTargets, kind);
                parts.Add(string.IsNullOrWhiteSpace(group.Key) ? targets : targets + ", " + group.Key);
            }

            string name = Bacteria != null ? Bacteria.Name : ModText.Get(ModStrings.Combat.Effect);
            return ModText.Get(ModStrings.Combat.Affects, name, FormatList(parts));
        }

        private static string FormatChanges(IReadOnlyList<ModifierChange> changes)
        {
            return changes != null && changes.Count > 0
                ? FormatList(changes.Select(c => c.Format()).ToList())
                : string.Empty;
        }
    }

    internal sealed class EssenceGeneratedEvent : IAccessibilityEvent
    {
        public EssenceGeneratedEvent(ActorRef actor, int order, int creation, int chaos, int arcana, int destruction)
        {
            Actor = actor;
            Order = order;
            Creation = creation;
            Chaos = chaos;
            Arcana = arcana;
            Destruction = destruction;
        }

        public string Kind { get { return AccessibilityEvents.Combat.EssenceGenerated; } }
        public ActorRef Actor { get; private set; }
        public int Order { get; private set; }
        public int Creation { get; private set; }
        public int Chaos { get; private set; }
        public int Arcana { get; private set; }
        public int Destruction { get; private set; }

        public string GetSpeechText()
        {
            return FormatEssenceAmounts(Order, Creation, Chaos, Arcana, Destruction);
        }
    }

    internal sealed class WielderEssenceGeneratedEvent : IAccessibilityEvent
    {
        public WielderEssenceGeneratedEvent(CommanderRef wielder, int order, int creation, int chaos, int arcana, int destruction)
        {
            Wielder = wielder;
            Order = order;
            Creation = creation;
            Chaos = chaos;
            Arcana = arcana;
            Destruction = destruction;
        }

        public string Kind { get { return AccessibilityEvents.Combat.WielderEssenceGenerated; } }
        public CommanderRef Wielder { get; private set; }
        public int Order { get; private set; }
        public int Creation { get; private set; }
        public int Chaos { get; private set; }
        public int Arcana { get; private set; }
        public int Destruction { get; private set; }

        public string GetSpeechText()
        {
            string amounts = FormatEssenceAmounts(Order, Creation, Chaos, Arcana, Destruction);
            return string.IsNullOrWhiteSpace(amounts)
                ? string.Empty
                : ModText.Get(
                    ModStrings.Combat.WielderEssenceGenerated,
                    Wielder != null ? Wielder.Format() : ModText.Get(ModStrings.Combat.Wielder),
                    amounts);
        }
    }

    internal sealed class TroopCreatedEvent : IAccessibilityEvent
    {
        public TroopCreatedEvent(TroopRef troop, bool isSummon) { Troop = troop; IsSummon = isSummon; }
        public string Kind { get { return AccessibilityEvents.Combat.TroopCreated; } }
        public TroopRef Troop { get; private set; }
        public bool IsSummon { get; private set; }
        public string GetSpeechText()
        {
            return IsSummon
                ? ModText.Get(ModStrings.Combat.Summoned, Troop.Format(includePosition: true))
                : ModText.Get(ModStrings.Combat.Created, Troop.Format(includePosition: true));
        }
    }

    internal sealed class MapEntityCreatedEvent : IAccessibilityEvent
    {
        public MapEntityCreatedEvent(EntityRef entity) { Entity = entity; }
        public string Kind { get { return AccessibilityEvents.Combat.MapEntityCreated; } }
        public EntityRef Entity { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.Appears, Entity.Format()); }
    }

    internal sealed class MapEntityDestroyedEvent : IAccessibilityEvent
    {
        public MapEntityDestroyedEvent(EntityRef entity) { Entity = entity; }
        public string Kind { get { return AccessibilityEvents.Combat.MapEntityDestroyed; } }
        public EntityRef Entity { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.Destroyed, Entity.Format()); }
    }

    internal sealed class TroopPushedEvent : IAccessibilityEvent
    {
        public TroopPushedEvent(TroopRef troop, Vector2Int from, Vector2Int to, IList<Vector2Int> path)
        {
            Troop = troop;
            From = from;
            To = to;
            Path = Copy(path);
        }

        public string Kind { get { return AccessibilityEvents.Combat.TroopPushed; } }
        public TroopRef Troop { get; private set; }
        public Vector2Int From { get; private set; }
        public Vector2Int To { get; private set; }
        public IReadOnlyList<Vector2Int> Path { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.PushedTo, Troop.Format(includePosition: true), FormatPoint(To)); }
    }

    internal sealed class AbilityUsedEvent : IAccessibilityEvent
    {
        public AbilityUsedEvent(ActorRef actor, AbilityRef ability, Vector2Int? targetingPosition, IList<Vector2Int> movementPath)
        {
            Actor = actor;
            Ability = ability;
            TargetingPosition = targetingPosition;
            MovementPath = Copy(movementPath);
        }

        public string Kind { get { return AccessibilityEvents.Combat.AbilityUsed; } }
        public ActorRef Actor { get; private set; }
        public AbilityRef Ability { get; private set; }
        public Vector2Int? TargetingPosition { get; private set; }
        public IReadOnlyList<Vector2Int> MovementPath { get; private set; }
        public string GetSpeechText() { return ModText.Get(ModStrings.Combat.AbilityUsed, Actor.Format(), Ability.Name); }
    }

    internal sealed class TeleportEvent : IAccessibilityEvent
    {
        public TeleportEvent(ActorRef actor, Vector2Int from, Vector2Int to, TeleportBattleTroopCommand.Source source)
        {
            Actor = actor;
            From = from;
            To = to;
            Source = source;
        }

        public string Kind { get { return AccessibilityEvents.Combat.Teleport; } }
        public ActorRef Actor { get; private set; }
        public Vector2Int From { get; private set; }
        public Vector2Int To { get; private set; }
        public TeleportBattleTroopCommand.Source Source { get; private set; }
        public string GetSpeechText()
        {
            return Source == TeleportBattleTroopCommand.Source.Ravenform
                ? ModText.Get(ModStrings.Combat.RavenformsTo, Actor.Format(), FormatPoint(To))
                : ModText.Get(ModStrings.Combat.TeleportsTo, Actor.Format(), FormatPoint(To));
        }
    }

    internal sealed class BurrowUpEvent : IAccessibilityEvent
    {
        public BurrowUpEvent(ActorRef actor, bool succeeded) { Actor = actor; Succeeded = succeeded; }
        public string Kind { get { return AccessibilityEvents.Combat.BurrowUp; } }
        public ActorRef Actor { get; private set; }
        public bool Succeeded { get; private set; }
        public string GetSpeechText()
        {
            return Succeeded
                ? ModText.Get(ModStrings.Combat.BurrowsUp, Actor.Format())
                : ModText.Get(ModStrings.Combat.FailedBurrow, Actor.Format());
        }
    }

    internal sealed class BattleResultEvent : IAccessibilityEvent
    {
        public BattleResultEvent(int localTeamId, BattleOutcome localOutcome, int attackerTeamId, BattleOutcome attackerOutcome, int defenderTeamId, BattleOutcome defenderOutcome)
        {
            LocalTeamId = localTeamId;
            LocalOutcome = localOutcome;
            AttackerTeamId = attackerTeamId;
            AttackerOutcome = attackerOutcome;
            DefenderTeamId = defenderTeamId;
            DefenderOutcome = defenderOutcome;
        }

        public string Kind { get { return AccessibilityEvents.Combat.BattleResult; } }
        public int LocalTeamId { get; private set; }
        public BattleOutcome LocalOutcome { get; private set; }
        public int AttackerTeamId { get; private set; }
        public BattleOutcome AttackerOutcome { get; private set; }
        public int DefenderTeamId { get; private set; }
        public BattleOutcome DefenderOutcome { get; private set; }

        public string GetSpeechText()
        {
            switch (LocalOutcome)
            {
                case BattleOutcome.Victory:
                    return ModText.Get(ModStrings.Combat.Victory);
                case BattleOutcome.Defeat:
                    return ModText.Get(ModStrings.Combat.Defeat);
                case BattleOutcome.Draw:
                    return ModText.Get(ModStrings.Combat.Draw);
                case BattleOutcome.Walkover:
                    return ModText.Get(ModStrings.Combat.Walkover);
                default:
                    return ModText.Get(ModStrings.Combat.BattleOver);
            }
        }
    }

    internal sealed class HudNotificationEvent : IAccessibilityEvent
    {
        // Native combat HUD popups that share BattleHUDNotificationManager or NotificationPanel
        // handling and are already authored as localized text. Examples include ranged blocked
        // by melee, ranged moved penalty, reloading, Mother's Love/Hate, and Mother's Love absorption.
        public HudNotificationEvent(string text)
        {
            Text = text ?? string.Empty;
        }

        public string Kind { get { return AccessibilityEvents.Combat.HudNotification; } }
        public string Text { get; private set; }
        public string GetSpeechText() { return SpeechTextSanitizer.Normalize(Text); }
    }

    internal static class CombatText
    {
        public static string FormatPoint(Vector2Int point)
        {
            return HexCoordinateFormatter.Format(point);
        }

        public static List<Vector2Int> Copy(IList<Vector2Int> path)
        {
            return path != null ? new List<Vector2Int>(path) : new List<Vector2Int>();
        }

        public static void AddAmount(List<string> parts, int amount, string name)
        {
            if (amount > 0)
            {
                parts.Add(amount + " " + name);
            }
        }

        public static string FormatList(IList<string> values)
        {
            if (values == null || values.Count == 0)
            {
                return string.Empty;
            }

            if (values.Count == 1)
            {
                return values[0];
            }

            if (values.Count == 2)
            {
                return ModText.JoinList(values.ToList());
            }

            return ModText.JoinList(values.ToList());
        }

        public static string FormatEssenceAmounts(int order, int creation, int chaos, int arcana, int destruction)
        {
            List<string> parts = new List<string>();
            AddEssenceAmount(parts, order, ModText.Get(ModStrings.Combat.OrderEssence));
            AddEssenceAmount(parts, creation, ModText.Get(ModStrings.Combat.CreationEssence));
            AddEssenceAmount(parts, chaos, ModText.Get(ModStrings.Combat.ChaosEssence));
            AddEssenceAmount(parts, arcana, ModText.Get(ModStrings.Combat.ArcanaEssence));
            AddEssenceAmount(parts, destruction, ModText.Get(ModStrings.Combat.DestructionEssence));

            string amounts = ModText.JoinListWithCommas(parts);
            return string.IsNullOrWhiteSpace(amounts)
                ? string.Empty
                : ModText.Get(ModStrings.Combat.EssenceAmounts, amounts);
        }

        private static void AddEssenceAmount(List<string> parts, int amount, string name)
        {
            if (amount > 0)
            {
                parts.Add(ModText.Get(ModStrings.Combat.EssenceAmountCompact, amount, name));
            }
        }

        public static string FormatAttackVerb(AttackTrigger trigger)
        {
            switch (trigger)
            {
                case AttackTrigger.Retaliation:
                    return ModText.Get(ModStrings.Combat.AttackVerbRetaliation);
                case AttackTrigger.Overwatch:
                    return ModText.Get(ModStrings.Combat.AttackVerbOverwatch);
                case AttackTrigger.Opportunity:
                    return ModText.Get(ModStrings.Combat.AttackVerbOpportunity);
                case AttackTrigger.Spearwall:
                    return ModText.Get(ModStrings.Combat.AttackVerbSpearwall);
                default:
                    return ModText.Get(ModStrings.Combat.AttackVerbDefault);
            }
        }

        public static string FormatBeamFacing(BeamFacing facing)
        {
            return facing == BeamFacing.Right
                ? ModText.Get(ModStrings.Combat.FacingRight)
                : ModText.Get(ModStrings.Combat.FacingLeft);
        }

        public static string FormatDamageKind(DamageType type, AttackTrigger trigger, bool isSplashDamage, BacteriaRef bacteria)
        {
            List<string> parts = new List<string>();
            if (isSplashDamage)
            {
                parts.Add(ModText.Get(ModStrings.Combat.Splash));
            }

            string attackTrigger = FormatAttackTrigger(trigger);
            if (!string.IsNullOrWhiteSpace(attackTrigger))
            {
                parts.Add(attackTrigger);
            }

            parts.Add(bacteria != null ? bacteria.Name.ToLowerInvariant() : FormatDamageType(type));
            return string.Join(" ", parts.ToArray());
        }

        public static string FormatAttackTrigger(AttackTrigger trigger)
        {
            switch (trigger)
            {
                case AttackTrigger.Retaliation:
                    return ModText.Get(ModStrings.Combat.Retaliation);
                case AttackTrigger.Overwatch:
                    return ModText.Get(ModStrings.Combat.Overwatch);
                case AttackTrigger.Opportunity:
                    return ModText.Get(ModStrings.Combat.OpportunityAttack);
                case AttackTrigger.Spearwall:
                    return ModText.Get(ModStrings.Combat.Spearwall);
                case AttackTrigger.BlindFury:
                    return ModText.Get(ModStrings.Combat.BlindFury);
                case AttackTrigger.Lunge:
                    return ModText.Get(ModStrings.Combat.Lunge);
                case AttackTrigger.Challenge:
                    return ModText.Get(ModStrings.Combat.Challenge);
                default:
                    return string.Empty;
            }
        }

        public static string FormatDamageType(DamageType type)
        {
            switch (type)
            {
                case DamageType.Melee:
                    return ModText.Get(ModStrings.Combat.MeleeDamage);
                case DamageType.Ranged:
                    return ModText.Get(ModStrings.Combat.RangedDamage);
                case DamageType.Spell:
                    return ModText.Get(ModStrings.Combat.SpellDamage);
                case DamageType.MapEntity:
                    return ModText.Get(ModStrings.Combat.MapEntityDamage);
                case DamageType.ExplosiveBarrel:
                case DamageType.ExplosiveBarrelViaSpell:
                    return ModText.Get(ModStrings.Combat.ExplosiveBarrel);
                case DamageType.MothersLove:
                    return ModText.Get(ModStrings.Combat.MothersLove);
                case DamageType.Bacteria:
                    return ModText.Get(ModStrings.Combat.Effect);
                default:
                    return ModText.Get(ModStrings.Combat.Unknown);
            }
        }

        public static string FormatModifierType(BacteriaModifierType type)
        {
            string name = type.ToString();
            if (name.StartsWith("Troop", StringComparison.Ordinal))
            {
                name = name.Substring("Troop".Length);
            }
            else if (name.StartsWith("Commander", StringComparison.Ordinal))
            {
                name = name.Substring("Commander".Length);
            }
            else if (name.StartsWith("Team", StringComparison.Ordinal))
            {
                name = name.Substring("Team".Length);
            }

            return SplitPascalCase(name).ToLowerInvariant();
        }

        public static string SplitPascalCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            List<char> chars = new List<char>();
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c) && !char.IsWhiteSpace(value[i - 1]))
                {
                    chars.Add(' ');
                }

                chars.Add(c);
            }

            return new string(chars.ToArray());
        }
    }
}
