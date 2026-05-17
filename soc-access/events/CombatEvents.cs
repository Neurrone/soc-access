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

    internal enum EffectTargetSummaryKind
    {
        ExplicitTargets,
        YourTroops,
        EnemyTroops
    }

    internal sealed class TroopRef
    {
        public TroopRef(int troopId, int teamId, int localTeamId, string name, int count, Vector2Int position)
        {
            TroopId = troopId;
            TeamId = teamId;
            LocalTeamId = localTeamId;
            Name = string.IsNullOrWhiteSpace(name) ? "troop" : SpeechTextSanitizer.Normalize(name);
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
            string text = Count + (IsEnemy ? " enemy " : " ") + Name;
            return includePosition ? text + " at " + FormatPoint(Position) : text;
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
                : Troop != null ? Troop.Format(includePosition: true) : "unknown troop";
        }
    }

    internal sealed class EntityRef
    {
        public EntityRef(int entityId, string name, Vector2Int position)
        {
            EntityId = entityId;
            Name = string.IsNullOrWhiteSpace(name) ? "attackable entity" : SpeechTextSanitizer.Normalize(name);
            Position = position;
        }

        public int EntityId { get; private set; }
        public string Name { get; private set; }
        public Vector2Int Position { get; private set; }

        public string Format()
        {
            return Name + " at " + FormatPoint(Position);
        }
    }

    internal sealed class CommanderRef
    {
        public CommanderRef(int commanderId, int teamId, int localTeamId, string name)
        {
            CommanderId = commanderId;
            TeamId = teamId;
            LocalTeamId = localTeamId;
            Name = string.IsNullOrWhiteSpace(name) ? "wielder" : SpeechTextSanitizer.Normalize(name);
        }

        public int CommanderId { get; private set; }
        public int TeamId { get; private set; }
        public int LocalTeamId { get; private set; }
        public string Name { get; private set; }
        public bool IsEnemy { get { return LocalTeamId >= 0 && TeamId != LocalTeamId; } }

        public string Format()
        {
            return IsEnemy ? "enemy wielder " + Name : Name;
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
                    return Troop != null ? Troop.Format(includePosition: true) : "unknown troop";
                case TargetKind.MapEntity:
                    return Entity != null ? Entity.Format() : "unknown entity";
                case TargetKind.Commander:
                    return Commander != null ? Commander.Format() : "wielder";
                case TargetKind.Tile:
                    return "tile " + FormatPoint(Tile);
                default:
                    return "unknown target";
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
                case EffectTargetSummaryKind.YourTroops:
                    return "your troops";
                case EffectTargetSummaryKind.EnemyTroops:
                    return "enemy troops";
                default:
                    return FormatExplicitTargets(targets);
            }
        }

        public static string FormatExplicitTargets(IList<TroopRef> targets)
        {
            return FormatList(targets != null
                ? targets.Select(t => t != null ? t.Format(includePosition: true) : "unknown troop").ToList()
                : new List<string>());
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
            return BoltCount == 1
                ? "1 bolt at " + Target.Format()
                : BoltCount + " bolts at " + Target.Format();
        }
    }

    internal sealed class NewTurnEvent : IAccessibilityEvent
    {
        public NewTurnEvent(TroopRef troop) { Troop = troop; }
        public string Kind { get { return AccessibilityEvents.Combat.NewTurn; } }
        public TroopRef Troop { get; private set; }
        public string GetSpeechText() { return "It is " + Troop.Format(includePosition: true) + "'s turn"; }
    }

    internal sealed class NewRoundEvent : IAccessibilityEvent
    {
        public NewRoundEvent(int roundNumber) { RoundNumber = roundNumber; }
        public string Kind { get { return AccessibilityEvents.Combat.NewRound; } }
        public int RoundNumber { get; private set; }
        public string GetSpeechText() { return RoundNumber > 0 ? "Round " + RoundNumber : string.Empty; }
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
        public string GetSpeechText() { return Actor.Format() + " moves to " + FormatPoint(To); }
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
        public string GetSpeechText() { return Attacker.Format() + " " + FormatAttackVerb(AttackTrigger) + " " + Target.Format(); }
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
                suffix = ", killing " + Kills;
            }
            else if (Target.TargetKind == TargetKind.MapEntity && SizeAfter <= 0)
            {
                suffix = ", destroying it";
            }

            if (Attacker == null)
            {
                return Target.Format() + " takes " + Damage + " " + kind + " damage" + suffix;
            }

            return Attacker.Format() + " deals " + Damage + " " + kind + " damage to " + Target.Format() + suffix;
        }
    }

    internal sealed class SpellCastEvent : IAccessibilityEvent
    {
        public SpellCastEvent(CommanderRef caster, SpellRef spell, IList<Vector2Int> selectedTargetPoints, IList<TargetRef> affectedTargets)
        {
            Caster = caster;
            Spell = spell;
            SelectedTargetPoints = Copy(selectedTargetPoints);
            AffectedTargets = affectedTargets != null ? new List<TargetRef>(affectedTargets) : new List<TargetRef>();
        }

        public string Kind { get { return AccessibilityEvents.Combat.SpellCast; } }
        public CommanderRef Caster { get; private set; }
        public SpellRef Spell { get; private set; }
        public IReadOnlyList<Vector2Int> SelectedTargetPoints { get; private set; }
        public IReadOnlyList<TargetRef> AffectedTargets { get; private set; }

        public string GetSpeechText()
        {
            string text = Caster.Format() + " casts " + Spell.Name;
            if (SelectedTargetPoints.Count > 0)
            {
                text += " at " + FormatList(SelectedTargetPoints.Select(FormatPoint).ToList());
            }

            return text;
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
                return Attacker.Format() + " casts Faey Fire";
            }

            return Attacker.Format() + " casts Faey Fire, " + FormatList(DamageSummaries.Select(s => s.FormatBoltText()).ToList());
        }
    }

    internal sealed class BacteriaRemovedEvent : IAccessibilityEvent
    {
        public BacteriaRemovedEvent(TroopRef target, BacteriaRef bacteria) { Target = target; Bacteria = bacteria; }
        public string Kind { get { return AccessibilityEvents.Combat.BacteriaRemoved; } }
        public TroopRef Target { get; private set; }
        public BacteriaRef Bacteria { get; private set; }
        public string GetSpeechText() { return Bacteria.Name + " removed from " + Target.Format(includePosition: true); }
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
            string text = Bacteria.Name + " affects " + Target.Format(includePosition: true);
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
            string name = Bacteria != null ? Bacteria.Name : "Effect";
            return name + " removed from " + EffectTargetSummary.FormatTargets(Targets.ToList(), TargetSummaryKind);
        }
    }

    internal sealed class BacteriaModifierSummaryEvent : IAccessibilityEvent
    {
        public BacteriaModifierSummaryEvent(BacteriaRef bacteria, IList<BacteriaModifierTargetSummary> targets, EffectTargetSummaryKind targetSummaryKind)
        {
            Bacteria = bacteria;
            Targets = targets != null ? new List<BacteriaModifierTargetSummary>(targets) : new List<BacteriaModifierTargetSummary>();
            TargetSummaryKind = targetSummaryKind;
        }

        public string Kind { get { return AccessibilityEvents.Combat.BacteriaModifierSummary; } }
        public BacteriaRef Bacteria { get; private set; }
        public IReadOnlyList<BacteriaModifierTargetSummary> Targets { get; private set; }
        public EffectTargetSummaryKind TargetSummaryKind { get; private set; }

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
                EffectTargetSummaryKind kind = byChanges.Count == 1 ? TargetSummaryKind : EffectTargetSummaryKind.ExplicitTargets;
                string targets = EffectTargetSummary.FormatTargets(groupTargets, kind);
                parts.Add(string.IsNullOrWhiteSpace(group.Key) ? targets : targets + ", " + group.Key);
            }

            string name = Bacteria != null ? Bacteria.Name : "Effect";
            return name + " affects " + FormatList(parts);
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
            List<string> parts = new List<string>();
            AddEssenceAmount(parts, Order, "order");
            AddEssenceAmount(parts, Creation, "creation");
            AddEssenceAmount(parts, Chaos, "chaos");
            AddEssenceAmount(parts, Arcana, "arcana");
            AddEssenceAmount(parts, Destruction, "destruction");
            return FormatList(parts);
        }

        private static void AddEssenceAmount(List<string> parts, int amount, string name)
        {
            if (amount > 0)
            {
                parts.Add("+" + amount + " " + name + " essence");
            }
        }
    }

    internal sealed class TroopCreatedEvent : IAccessibilityEvent
    {
        public TroopCreatedEvent(TroopRef troop, bool isSummon) { Troop = troop; IsSummon = isSummon; }
        public string Kind { get { return AccessibilityEvents.Combat.TroopCreated; } }
        public TroopRef Troop { get; private set; }
        public bool IsSummon { get; private set; }
        public string GetSpeechText() { return Troop.Format(includePosition: true) + (IsSummon ? " summoned" : " created"); }
    }

    internal sealed class MapEntityCreatedEvent : IAccessibilityEvent
    {
        public MapEntityCreatedEvent(EntityRef entity) { Entity = entity; }
        public string Kind { get { return AccessibilityEvents.Combat.MapEntityCreated; } }
        public EntityRef Entity { get; private set; }
        public string GetSpeechText() { return Entity.Format() + " appears"; }
    }

    internal sealed class MapEntityDestroyedEvent : IAccessibilityEvent
    {
        public MapEntityDestroyedEvent(EntityRef entity) { Entity = entity; }
        public string Kind { get { return AccessibilityEvents.Combat.MapEntityDestroyed; } }
        public EntityRef Entity { get; private set; }
        public string GetSpeechText() { return Entity.Format() + " destroyed"; }
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
        public string GetSpeechText() { return Troop.Format(includePosition: true) + " pushed to " + FormatPoint(To); }
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
        public string GetSpeechText() { return Actor.Format() + " uses " + Ability.Name; }
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
        public string GetSpeechText() { return Actor.Format() + " " + (Source == TeleportBattleTroopCommand.Source.Ravenform ? "ravenforms to " : "teleports to ") + FormatPoint(To); }
    }

    internal sealed class BurrowUpEvent : IAccessibilityEvent
    {
        public BurrowUpEvent(ActorRef actor, bool succeeded) { Actor = actor; Succeeded = succeeded; }
        public string Kind { get { return AccessibilityEvents.Combat.BurrowUp; } }
        public ActorRef Actor { get; private set; }
        public bool Succeeded { get; private set; }
        public string GetSpeechText() { return Succeeded ? Actor.Format() + " burrows up" : Actor.Format() + ", failed burrow"; }
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
                    return "Victory";
                case BattleOutcome.Defeat:
                    return "Defeat";
                case BattleOutcome.Draw:
                    return "Draw";
                case BattleOutcome.Walkover:
                    return "Walkover";
                default:
                    return "Battle over";
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
                return values[0] + " and " + values[1];
            }

            return string.Join(", ", values.Take(values.Count - 1).ToArray()) + ", and " + values[values.Count - 1];
        }

        public static string FormatAttackVerb(AttackTrigger trigger)
        {
            switch (trigger)
            {
                case AttackTrigger.Retaliation:
                    return "retaliates against";
                case AttackTrigger.Overwatch:
                    return "makes an overwatch attack against";
                case AttackTrigger.Opportunity:
                    return "makes an opportunity attack against";
                case AttackTrigger.Spearwall:
                    return "makes a spearwall attack against";
                default:
                    return "attacks";
            }
        }

        public static string FormatDamageKind(DamageType type, AttackTrigger trigger, bool isSplashDamage, BacteriaRef bacteria)
        {
            List<string> parts = new List<string>();
            if (isSplashDamage)
            {
                parts.Add("splash");
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
                    return "retaliation";
                case AttackTrigger.Overwatch:
                    return "overwatch";
                case AttackTrigger.Opportunity:
                    return "opportunity attack";
                case AttackTrigger.Spearwall:
                    return "spearwall";
                case AttackTrigger.BlindFury:
                    return "blind fury";
                case AttackTrigger.Lunge:
                    return "lunge";
                case AttackTrigger.Challenge:
                    return "challenge";
                default:
                    return string.Empty;
            }
        }

        public static string FormatDamageType(DamageType type)
        {
            switch (type)
            {
                case DamageType.Melee:
                    return "melee";
                case DamageType.Ranged:
                    return "ranged";
                case DamageType.Spell:
                    return "spell";
                case DamageType.MapEntity:
                    return "map entity";
                case DamageType.ExplosiveBarrel:
                case DamageType.ExplosiveBarrelViaSpell:
                    return "explosive barrel";
                case DamageType.MothersLove:
                    return "mother's love";
                case DamageType.Bacteria:
                    return "effect";
                default:
                    return "unknown";
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
