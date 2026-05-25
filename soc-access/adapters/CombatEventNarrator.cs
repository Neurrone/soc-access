using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using Lavapotion.Networking;
using Lavapotion.Pathfinding;
using SongsOfConquest;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Battle.Bacterias;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Battle;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Spells;
using SongsOfConquest.Utilities;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal static class CombatEventNarrator
    {
        private static readonly CombatNarrationPlanner Planner = new CombatNarrationPlanner();
        private static readonly Queue<string> SuppressedNativeNotifications = new Queue<string>();
        private static int _currentTurnTroopId = -1;
        private static bool _flushPendingEventsScheduled;
        private static CombatAdapter _activeAdapter;

        public static void HandleResponse(ICommandResponse response)
        {
            if (response == null)
            {
                return;
            }

            CombatAdapter adapter = GetAdapter();
            if (adapter == null)
            {
                return;
            }

            try
            {
                EnqueueResponse(response, adapter);
                ScheduleFlushPendingEvents();
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CombatEventNarrator failed to queue " + response.GetType().Name + ": " + exception.Message);
            }
        }

        public static void AnnounceNativeNotification(string localizedText)
        {
            string text = SpeechTextSanitizer.Normalize(localizedText);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            if (ConsumeSuppressedNativeNotification(text))
            {
                return;
            }

            CombatAdapter adapter = GetAdapter();
            if (adapter == null)
            {
                return;
            }

            PublishEvent(new HudNotificationEvent(text));
        }

        private static void MoveCombatCursorToLocalActingTroop(int troopId)
        {
            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            screen?.MoveCursorToLocalActingTroop(troopId);
        }

        public static void NotifyBacteriaAddedStarted(int troopId, string localizedText)
        {
            string text = SpeechTextSanitizer.Normalize(localizedText);
            if (!string.IsNullOrWhiteSpace(text))
            {
                SuppressedNativeNotifications.Enqueue(text);
            }
        }

        public static void Reset()
        {
            Planner.Reset();
            SuppressedNativeNotifications.Clear();
            _currentTurnTroopId = -1;
            _flushPendingEventsScheduled = false;
            _activeAdapter = null;
        }

        public static void SetActiveAdapter(CombatAdapter adapter)
        {
            _activeAdapter = adapter;
            SyncCurrentTurnTroop(adapter);
        }

        public static void SyncCurrentTurnTroop(CombatAdapter adapter)
        {
            if (adapter == null)
            {
                return;
            }

            int currentTroopId = adapter.GetCurrentTroopId();
            if (currentTroopId >= 0)
            {
                _currentTurnTroopId = currentTroopId;
            }
        }

        private static CombatAdapter GetAdapter()
        {
            if (_activeAdapter == null || !_activeAdapter.IsPresent())
            {
                return null;
            }

            if (_currentTurnTroopId < 0)
            {
                SyncCurrentTurnTroop(_activeAdapter);
            }

            return _activeAdapter;
        }

        private static void EnqueueResponse(ICommandResponse response, CombatAdapter adapter)
        {
            MoveBattleTroopCommand.Response move = response as MoveBattleTroopCommand.Response;
            if (move != null)
            {
                IBattleTroopState troop = adapter.GetTroop(move.Id);
                if (troop != null && move.Path != null && move.Path.Length > 0)
                {
                    List<Vector2Int> path = ConvertPath(move.Path);
                    Vector2Int start = path[0];
                    Vector2Int end = path[path.Count - 1];
                    Enqueue(CombatNarrationItem.Create(
                        CombatNarrationItemKind.Move,
                        new TroopMovedEvent(CreateActor(adapter, troop, troop.Stats.Size, start), start, end, path),
                        troop.Id,
                        path));
                }

                return;
            }

            EndBattleTurnCommand.Response endTurn = response as EndBattleTurnCommand.Response;
            if (endTurn != null)
            {
                IBattleTroopState troop = adapter.GetTroop(endTurn.NewTurnTroopId);
                if (troop != null)
                {
                    _currentTurnTroopId = troop.Id;
                    Enqueue(CombatNarrationItem.Create(
                        CombatNarrationItemKind.NewTurn,
                        new NewTurnEvent(adapter.CreateTroopRef(troop)),
                        troop.Id));
                }

                return;
            }

            if (response is NewBattleRoundCommand.Response)
            {
                int round = adapter.GetCurrentRound() + 1;
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.NewRound,
                    new NewRoundEvent(round)));
                return;
            }

            if (response is UpdateBattleQueueCommand.Response)
            {
                Enqueue(CombatNarrationItem.Direct(CombatNarrationItemKind.QueueChanged, new QueueChangedEvent()));
                return;
            }

            PerformBattleTroopAttackCommand.Response troopAttack = response as PerformBattleTroopAttackCommand.Response;
            if (troopAttack != null)
            {
                EnqueueAttack(troopAttack.AttackerTroopId, targetIsMapEntity: false, troopAttack.Damage, adapter);
                return;
            }

            PerformBattleMapEntityAttackCommand.Response entityAttack = response as PerformBattleMapEntityAttackCommand.Response;
            if (entityAttack != null)
            {
                EnqueueAttack(entityAttack.AttackerTroopId, targetIsMapEntity: true, entityAttack.Damage, adapter);
                return;
            }

            DamageBattleTroopCommand.Response troopDamage = response as DamageBattleTroopCommand.Response;
            if (troopDamage != null)
            {
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Damage,
                    BuildTroopDamageEvent(troopDamage.Damage, troopDamage.AttackingTroopId, troopDamage.IsSplashDamage, troopDamage.BacteriaType, adapter),
                    troopDamage.AttackingTroopId));
                return;
            }

            DamageBattleMapEntityCommand.Response entityDamage = response as DamageBattleMapEntityCommand.Response;
            if (entityDamage != null)
            {
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Damage,
                    BuildEntityDamageEvent(entityDamage.AttackerId, entityDamage.MapEntityId, entityDamage.Damage, entityDamage.IsSplashDamage, adapter),
                    entityDamage.AttackerId));
                return;
            }

            CastBattleSpellCommand.Response spell = response as CastBattleSpellCommand.Response;
            if (spell != null)
            {
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Spell,
                    BuildSpellEvent(spell, adapter),
                    spell.CommanderId));
                return;
            }

            FaeyFireCommand.Response faeyFire = response as FaeyFireCommand.Response;
            if (faeyFire != null)
            {
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.FaeyFire,
                    BuildFaeyFireEvent(faeyFire, adapter),
                    faeyFire.AttackerId));
                return;
            }

            AddBattleBacteriaCommand.Response addBacteria = response as AddBattleBacteriaCommand.Response;
            if (addBacteria != null)
            {
                EnqueueBacteriaAdded(addBacteria, adapter);
                return;
            }

            RemoveBattleBacteriaCommand.Response removeBacteria = response as RemoveBattleBacteriaCommand.Response;
            if (removeBacteria != null)
            {
                EnqueueBacteriaRemoved(removeBacteria, adapter);
                return;
            }

            ChangeBattleBacteriaModifierCommand.Response modifier = response as ChangeBattleBacteriaModifierCommand.Response;
            if (modifier != null)
            {
                EnqueueBacteriaModifierApplied(modifier, adapter);
                return;
            }

            TroopGenerateEssenceCommand.Response essence = response as TroopGenerateEssenceCommand.Response;
            if (essence != null)
            {
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.EssenceGenerated,
                    new EssenceGeneratedEvent(
                        CreateActor(adapter, adapter.GetTroop(essence.BattleTroopId)),
                        essence.OrderGenerated,
                        essence.CreationGenerated,
                        essence.ChaosGenerated,
                        essence.ArcanaGenerated,
                        essence.DestructionGenerated),
                    essence.BattleTroopId));
                return;
            }

            CreateBattleTroopCommand.Response createdTroop = response as CreateBattleTroopCommand.Response;
            if (createdTroop != null && createdTroop.Troop != null)
            {
                Enqueue(CombatNarrationItem.Direct(
                    CombatNarrationItemKind.TroopCreated,
                    new TroopCreatedEvent(adapter.CreateTroopRef(createdTroop.Troop), isSummon: true)));
                return;
            }

            CreateBattleMapEntityCommand.Response createdEntity = response as CreateBattleMapEntityCommand.Response;
            if (createdEntity != null && createdEntity.State != null)
            {
                IMapEntity entity = adapter.GetMapEntity(createdEntity.State.Id);
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.MapEntityCreated,
                    new MapEntityCreatedEvent(adapter.CreateEntityRef(entity)),
                    entityId: createdEntity.State.Id));
                return;
            }

            DestroyBattleMapEntityCommand.Response destroyedEntity = response as DestroyBattleMapEntityCommand.Response;
            if (destroyedEntity != null)
            {
                return;
            }

            PushBattleTroopCommand.Response push = response as PushBattleTroopCommand.Response;
            if (push != null && push.Path != null && push.Path.Length > 1)
            {
                IBattleTroopState troop = adapter.GetTroop(push.TroopId);
                Vector2Int from = push.Path[0];
                Vector2Int to = push.Path[push.Path.Length - 1];
                Enqueue(CombatNarrationItem.Push(
                    new TroopPushedEvent(adapter.CreateTroopRef(troop, troop != null ? troop.Stats.Size : 0, from), from, to, push.Path),
                    push.TroopId,
                    from,
                    to));
                return;
            }

            BullRushCommand.Response bullRush = response as BullRushCommand.Response;
            if (bullRush != null && bullRush.Path != null && bullRush.Path.Length > 0)
            {
                IBattleTroopState troop = adapter.GetTroop(bullRush.TroopId);
                Vector2Int from = bullRush.Path[0];
                Vector2Int to = bullRush.Path[bullRush.Path.Length - 1];
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Ability,
                    new AbilityUsedEvent(CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, from), adapter.CreateAbilityRef(TroopAbilityType.BullRush), null, bullRush.Path),
                    bullRush.TroopId));
                return;
            }

            LeapCommand.Response leap = response as LeapCommand.Response;
            if (leap != null)
            {
                IBattleTroopState troop = adapter.GetTroop(leap.TroopId);
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Ability,
                    new AbilityUsedEvent(CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, leap.OriginalPosition), adapter.CreateAbilityRef(TroopAbilityType.Leap), leap.TargetPosition, new[] { leap.OriginalPosition, leap.TargetPosition }),
                    leap.TroopId));
                return;
            }

            TeleportBattleTroopCommand.Response teleport = response as TeleportBattleTroopCommand.Response;
            if (teleport != null)
            {
                EnqueueTeleport(teleport, adapter);
                return;
            }

            TroopAbilityActivationBeginCommand.Response ability = response as TroopAbilityActivationBeginCommand.Response;
            if (ability != null)
            {
                IBattleTroopState troop = adapter.GetTroop(ability.BattleTroopId);
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Ability,
                    new AbilityUsedEvent(CreateActor(adapter, troop), adapter.CreateAbilityRef(ability.TroopAbility), null, null),
                    ability.BattleTroopId));
                return;
            }

            TroopAbilityActivationCompleteCommand.Response abilityComplete = response as TroopAbilityActivationCompleteCommand.Response;
            if (abilityComplete != null)
            {
                return;
            }

            BurrowUpCommand.Response burrowUp = response as BurrowUpCommand.Response;
            if (burrowUp != null)
            {
                IBattleTroopState troop = adapter.GetTroop(burrowUp.TroopId);
                Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.BurrowUp, new BurrowUpEvent(CreateActor(adapter, troop), burrowUp.BurrowSuccess), burrowUp.TroopId));
                return;
            }

            BattleResultCommand.Response battleResult = response as BattleResultCommand.Response;
            if (battleResult != null)
            {
                Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.BattleResult, BuildBattleResultEvent(battleResult, adapter)));
            }
        }

        private static void EnqueueAttack(int attackerId, bool targetIsMapEntity, DamageResult damage, CombatAdapter adapter)
        {
            IBattleTroopState attacker = adapter.GetTroop(attackerId);
            TargetRef target = targetIsMapEntity
                ? TargetRef.FromEntity(adapter.CreateEntityRef(adapter.GetMapEntity(damage.StateId)))
                : TargetRef.FromTroop(adapter.CreateTroopRef(adapter.GetTroop(damage.StateId), Math.Max(damage.SizeAfter + damage.Kills, 0)));
            Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, new AttackEvent(CreateActor(adapter, attacker), target, damage.AttackTrigger), attackerId));
        }

        private static void EnqueueBacteriaAdded(AddBattleBacteriaCommand.Response response, CombatAdapter adapter)
        {
            // Bacteria applications are intentionally quiet for now. The useful
            // effect details are emitted by the follow-up modifier events.
        }

        private static void EnqueueBacteriaRemoved(RemoveBattleBacteriaCommand.Response response, CombatAdapter adapter)
        {
            if (response.Entries == null)
            {
                return;
            }

            for (int i = 0; i < response.Entries.Length; i++)
            {
                RemoveBattleBacteriaCommand.Entry entry = response.Entries[i];
                if (!IsBattleTroopType(entry.StateTypeName) || entry.BacteriaReference == null)
                {
                    continue;
                }

                IBattleTroopState troop = adapter.GetTroop(entry.StateId);
                EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaRemovalSummary(
                    adapter.CreateBacteriaRef(entry.BacteriaReference),
                    adapter.CreateTroopRef(troop),
                    entry.StateId),
                    adapter);
            }
        }

        private static void EnqueueBacteriaModifierApplied(ChangeBattleBacteriaModifierCommand.Response response, CombatAdapter adapter)
        {
            if (response.ChangeSets == null)
            {
                return;
            }

            for (int i = 0; i < response.ChangeSets.Length; i++)
            {
                ModifierChangeSet changeSet = response.ChangeSets[i];
                Type targetType = !string.IsNullOrWhiteSpace(changeSet.TargetTypeName) ? Type.GetType(changeSet.TargetTypeName) : null;
                if (targetType == null || !typeof(IBattleTroopState).IsAssignableFrom(targetType) || changeSet.Changes == null)
                {
                    continue;
                }

                Dictionary<int, List<ModifierChangeSet.Change>> byBacteria = new Dictionary<int, List<ModifierChangeSet.Change>>();
                for (int j = 0; j < changeSet.Changes.Length; j++)
                {
                    ModifierChangeSet.Change change = changeSet.Changes[j];
                    if (change == null || change.ChangeType != BacteriaModifierChangeType.Apply || change.Modifier == null)
                    {
                        continue;
                    }

                    List<ModifierChangeSet.Change> changes;
                    if (!byBacteria.TryGetValue(change.Modifier.BacteriaReferenceId, out changes))
                    {
                        changes = new List<ModifierChangeSet.Change>();
                        byBacteria[change.Modifier.BacteriaReferenceId] = changes;
                    }

                    changes.Add(change);
                }

                foreach (KeyValuePair<int, List<ModifierChangeSet.Change>> group in byBacteria)
                {
                    BacteriaReference bacteria = FindBacteriaReference(adapter, group.Value[0].Modifier);
                    if (bacteria == null)
                    {
                        continue;
                    }

                    IBattleTroopState troop = adapter.GetTroop(changeSet.TargetId);
                    EnqueueBacteriaSummary(CombatNarrationItem.CreateBacteriaModifierSummary(
                        adapter.CreateBacteriaRef(bacteria),
                        adapter.CreateTroopRef(troop),
                        CreateModifierChanges(group.Value),
                        changeSet.TargetId),
                        adapter);
                }
            }
        }

        private static BacteriaReference FindBacteriaReference(CombatAdapter adapter, BacteriaModifier modifier)
        {
            if (modifier == null)
            {
                return null;
            }

            IBattleTroopState ownerTroop = adapter.GetTroop(modifier.BacteriaReferenceOwnerId);
            if (ownerTroop != null && ownerTroop.Bacterias != null)
            {
                return ownerTroop.Bacterias.FirstOrDefault(reference => reference.Id == modifier.BacteriaReferenceId);
            }

            return null;
        }

        private static ActorRef CreateActor(CombatAdapter adapter, IBattleTroopState troop)
        {
            return CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, troop != null ? troop.Position : Vector2Int.zero);
        }

        private static ActorRef CreateActor(CombatAdapter adapter, IBattleTroopState troop, int sizeOverride, Vector2Int positionOverride)
        {
            TroopRef troopRef = adapter.CreateTroopRef(troop, sizeOverride, positionOverride);
            int currentTroopId = _currentTurnTroopId >= 0 ? _currentTurnTroopId : adapter.GetCurrentTroopId();
            return new ActorRef(troopRef, troopRef.TroopId == currentTroopId);
        }

        private static void EnqueueTeleport(TeleportBattleTroopCommand.Response response, CombatAdapter adapter)
        {
            if (response.Entries == null || response.Source == TeleportBattleTroopCommand.Source.Leap)
            {
                return;
            }

            for (int i = 0; i < response.Entries.Length; i++)
            {
                TeleportBattleTroopCommand.ResponseEntry entry = response.Entries[i];
                IBattleTroopState troop = adapter.GetTroop(entry.Id);
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Teleport,
                    new TeleportEvent(CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, entry.OldPosition), entry.OldPosition, entry.NewPosition, response.Source),
                    entry.Id));
            }
        }

        private static void Enqueue(CombatNarrationItem pending)
        {
            Planner.Enqueue(pending);
        }

        private static void EnqueueBacteriaSummary(CombatNarrationItem pending, CombatAdapter adapter)
        {
            Planner.EnqueueBacteriaSummary(pending, CreateNarrationSnapshot(adapter));
        }

        private static void ScheduleFlushPendingEvents()
        {
            if (_flushPendingEventsScheduled)
            {
                return;
            }

            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin == null)
            {
                FlushPendingEvents();
                return;
            }

            _flushPendingEventsScheduled = true;
            plugin.StartCoroutine(FlushPendingEventsNextFrame());
        }

        private static IEnumerator FlushPendingEventsNextFrame()
        {
            yield return null;
            _flushPendingEventsScheduled = false;
            FlushPendingEvents();
        }

        private static void FlushPendingEvents()
        {
            if (!Planner.HasPendingEvents)
            {
                return;
            }

            IReadOnlyList<CombatNarrationItem> events = Planner.Flush();
            for (int i = 0; i < events.Count; i++)
            {
                CombatNarrationItem pending = events[i];
                if (pending == null)
                {
                    continue;
                }

                if (pending.Kind == CombatNarrationItemKind.NewTurn)
                {
                    _currentTurnTroopId = pending.TroopId;
                    MoveCombatCursorToLocalActingTroop(pending.TroopId);
                }
                else if (pending.Kind == CombatNarrationItemKind.BattleResult)
                {
                    _currentTurnTroopId = -1;
                }

                PublishEvent(pending.Event);
            }
        }

        private static CombatNarrationSnapshot CreateNarrationSnapshot(CombatAdapter adapter)
        {
            if (adapter == null)
            {
                return CombatNarrationSnapshot.Empty;
            }

            return new CombatNarrationSnapshot(
                adapter.LocalTeamId,
                adapter.GetAliveBattleTroopIdsForSide(enemySide: false),
                adapter.GetAliveBattleTroopIdsForSide(enemySide: true),
                adapter.GetAliveMeleeBattleTroopIdsForSide(enemySide: false),
                adapter.GetAliveMeleeBattleTroopIdsForSide(enemySide: true),
                adapter.GetAliveRangedBattleTroopIdsForSide(enemySide: false),
                adapter.GetAliveRangedBattleTroopIdsForSide(enemySide: true));
        }

        private static DamageEvent BuildTroopDamageEvent(DamageResult result, int attackerId, bool isSplashDamage, int bacteriaType, CombatAdapter adapter)
        {
            IBattleTroopState target = adapter.GetTroop(result.StateId);
            int sizeBefore = target != null ? Math.Max(result.SizeAfter + result.Kills, target.Stats.Size) : Math.Max(result.SizeAfter + result.Kills, 0);
            IBattleTroopState attacker = adapter.GetTroop(attackerId);
            BacteriaRef bacteria = bacteriaType >= 0 ? adapter.CreateBacteriaRef((BacteriaTypes)bacteriaType) : null;
            return new DamageEvent(
                attacker != null ? CreateActor(adapter, attacker) : null,
                TargetRef.FromTroop(adapter.CreateTroopRef(target, sizeBefore)),
                result.Damage,
                result.Kills,
                sizeBefore,
                result.SizeAfter,
                result.Type,
                result.AttackTrigger,
                isSplashDamage,
                bacteria);
        }

        private static DamageEvent BuildEntityDamageEvent(int attackerId, int entityId, DamageResult damage, bool isSplashDamage, CombatAdapter adapter)
        {
            IMapEntity entity = adapter.GetMapEntity(entityId);
            IBattleTroopState attacker = adapter.GetTroop(attackerId);
            return new DamageEvent(
                attacker != null ? CreateActor(adapter, attacker) : null,
                TargetRef.FromEntity(adapter.CreateEntityRef(entity)),
                damage.Damage,
                damage.Kills,
                0,
                damage.HealthAfter,
                damage.Type,
                damage.AttackTrigger,
                isSplashDamage,
                null);
        }

        private static SpellCastEvent BuildSpellEvent(CastBattleSpellCommand.Response response, CombatAdapter adapter)
        {
            return new SpellCastEvent(
                adapter.CreateCommanderRef(response.CommanderId),
                adapter.CreateSpellRef(response.Spell, response.CastResponse.Tier),
                response.CastResponse.TargetPoints,
                CreateAffectedTargets(response.CastResponse.TargetsAffected, adapter));
        }

        private static FaeyFireEvent BuildFaeyFireEvent(FaeyFireCommand.Response response, CombatAdapter adapter)
        {
            IBattleTroopState attacker = adapter.GetTroop(response.AttackerId);
            Dictionary<int, List<DamageResult>> byTarget = new Dictionary<int, List<DamageResult>>();
            if (response.DamageResults != null)
            {
                for (int i = 0; i < response.DamageResults.Count; i++)
                {
                    int targetId = response.DamageResults[i].StateId;
                    List<DamageResult> results;
                    if (!byTarget.TryGetValue(targetId, out results))
                    {
                        results = new List<DamageResult>();
                        byTarget[targetId] = results;
                    }

                    results.Add(response.DamageResults[i]);
                }
            }

            List<FaeyFireDamageSummary> summaries = new List<FaeyFireDamageSummary>();
            foreach (KeyValuePair<int, List<DamageResult>> pair in byTarget)
            {
                IBattleTroopState target = adapter.GetTroop(pair.Key);
                summaries.Add(new FaeyFireDamageSummary(
                    TargetRef.FromTroop(adapter.CreateTroopRef(target)),
                    pair.Value.Count,
                    pair.Value.Sum(r => r.Damage),
                    pair.Value.Sum(r => r.Kills)));
            }

            return new FaeyFireEvent(CreateActor(adapter, attacker), summaries);
        }

        private static List<TargetRef> CreateAffectedTargets(IList<SpellTargetDefinition> targets, CombatAdapter adapter)
        {
            List<TargetRef> formatted = new List<TargetRef>();
            if (targets == null || targets.Count == 0)
            {
                return formatted;
            }

            for (int i = 0; i < targets.Count; i++)
            {
                SpellTargetDefinition target = targets[i];
                if (target == null)
                {
                    continue;
                }

                switch (target.TargetType)
                {
                    case SpellTargetDefinition.Type.Troop:
                        formatted.Add(TargetRef.FromTroop(adapter.CreateTroopRef(adapter.GetTroop(target.Id))));
                        break;
                    case SpellTargetDefinition.Type.MapEntity:
                        formatted.Add(TargetRef.FromEntity(adapter.CreateEntityRef(adapter.GetMapEntity(target.Id))));
                        break;
                    case SpellTargetDefinition.Type.Commander:
                        formatted.Add(TargetRef.FromCommander(adapter.CreateCommanderRef(target.Id)));
                        break;
                    case SpellTargetDefinition.Type.Tile:
                    case SpellTargetDefinition.Type.RandomNeighbour:
                        formatted.Add(TargetRef.FromTile(target.Position));
                        break;
                }
            }

            return formatted;
        }

        private static List<ModifierChange> CreateModifierChanges(IList<ModifierChangeSet.Change> changes)
        {
            List<ModifierChange> result = new List<ModifierChange>();
            if (changes == null)
            {
                return result;
            }

            for (int i = 0; i < changes.Count; i++)
            {
                BacteriaModifier modifier = changes[i].Modifier;
                if (modifier == null || modifier.AmountToAdd == 0)
                {
                    continue;
                }

                result.Add(new ModifierChange(modifier.Type, modifier.ApplicationType, modifier.AmountToAdd));
            }

            return result;
        }

        private static BattleResultEvent BuildBattleResultEvent(BattleResultCommand.Response response, CombatAdapter adapter)
        {
            IBattleResult result = response.Result;
            int localTeamId = adapter.LocalTeamId;
            if (result == null || result.Statistics == null)
            {
                return new BattleResultEvent(localTeamId, BattleOutcome.Inconclusive, -1, BattleOutcome.Inconclusive, -1, BattleOutcome.Inconclusive);
            }

            int attackerTeamId = result.Statistics.Attacker != null ? result.Statistics.Attacker.TeamId : -1;
            int defenderTeamId = result.Statistics.Defender != null ? result.Statistics.Defender.TeamId : -1;
            BattleOutcome attackerOutcome = result.Statistics.Attacker != null ? result.Statistics.Attacker.Outcome : BattleOutcome.Inconclusive;
            BattleOutcome defenderOutcome = result.Statistics.Defender != null ? result.Statistics.Defender.Outcome : BattleOutcome.Inconclusive;
            BattleOutcome localOutcome = attackerTeamId == localTeamId ? attackerOutcome : defenderOutcome;

            return new BattleResultEvent(localTeamId, localOutcome, attackerTeamId, attackerOutcome, defenderTeamId, defenderOutcome);
        }

        private static bool IsBattleTroopType(string typeName)
        {
            Type type = !string.IsNullOrWhiteSpace(typeName) ? Type.GetType(typeName) : null;
            return type != null && typeof(IBattleTroopState).IsAssignableFrom(type);
        }

        private static List<Vector2Int> ConvertPath(PathNode[] path)
        {
            List<Vector2Int> points = new List<Vector2Int>();
            if (path == null)
            {
                return points;
            }

            for (int i = 0; i < path.Length; i++)
            {
                points.Add(VectorExtensions.ToVector2Int(path[i].point));
            }

            return points;
        }

        private static bool ConsumeSuppressedNativeNotification(string text)
        {
            while (SuppressedNativeNotifications.Count > 0)
            {
                string suppressed = SuppressedNativeNotifications.Dequeue();
                if (string.Equals(suppressed, text, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        private static void PublishEvent(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                return;
            }

            AccessibilityEventBus.Publish(accessibilityEvent);
        }

    }
}
