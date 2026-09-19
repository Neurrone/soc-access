using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public static class CombatEventNarrator
    {
        private const int CombatNarrationBatchFrames = 15;

        /// <summary>How many bacteria texts may wait to be matched. The game shows nothing for a
        /// troop whose view or head node has gone, so an entry can be left standing; the oldest is
        /// dropped rather than letting a battle's worth of them pile up.</summary>
        private const int MaxSuppressedNativeNotifications = 16;

        private static readonly CombatNarrationPlanner Planner = new CombatNarrationPlanner();
        private static readonly List<string> SuppressedNativeNotifications = new List<string>();
        private static int _currentTurnTroopId = -1;
        private static bool _flushPendingEventsScheduled;
        private static CombatAdapter _activeAdapter;
        private static int _flushScheduleGeneration;
        private static bool _abilityBatchActive;
        private static bool _wielderEssenceCaptureActive;
        private static readonly Dictionary<int, WielderEssenceGeneration> CapturedWielderEssence =
            new Dictionary<int, WielderEssenceGeneration>();

        [HookWritable]
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
                bool isSpellResponse = response is CastBattleSpellCommand.Response;
                bool isAbilityBegin = response is TroopAbilityActivationBeginCommand.Response;
                bool isAbilityComplete = response is TroopAbilityActivationCompleteCommand.Response;
                bool isEndTurnResponse = response is EndBattleTurnCommand.Response;
                bool bufferForSpell = !isSpellResponse && ShouldBufferForSpellResponse(response, adapter);
                bool bufferForBacteriaSummary = ShouldBufferForBacteriaSummary(response);
                if (_abilityBatchActive && isEndTurnResponse)
                {
                    _abilityBatchActive = false;
                    FlushPendingEventsImmediately();
                }

                EnqueueResponse(response, adapter);
                // Spell and ability effects arrive as separate follow-up responses.
                // Batch them so repeated effect applications can be condensed into
                // summaries. Ability effects can arrive after activation complete,
                // so complete restarts the bounded wait instead of flushing.
                if (isAbilityBegin)
                {
                    _abilityBatchActive = true;
                    ScheduleFlushPendingEvents();
                }
                else if (isAbilityComplete)
                {
                    _abilityBatchActive = true;
                    RestartScheduledFlushPendingEvents();
                }
                else if (isSpellResponse)
                {
                    FlushPendingEventsImmediately();
                }
                else if (_abilityBatchActive)
                {
                    ScheduleFlushPendingEvents();
                }
                else if (bufferForSpell || bufferForBacteriaSummary || _flushPendingEventsScheduled)
                {
                    ScheduleFlushPendingEvents();
                }
                else
                {
                    FlushPendingEventsImmediately();
                }
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatEventNarrator failed to queue " + response.GetType().Name + ": " + exception.Message);
            }
        }

        public static bool ShouldBufferForBacteriaSummary(ICommandResponse response)
        {
            return response != null && ShouldBufferResponseTypeForBacteriaSummary(response.GetType());
        }

        public static bool ShouldBufferResponseTypeForBacteriaSummary(Type responseType)
        {
            return responseType == typeof(AddBattleBacteriaCommand.Response)
                || responseType == typeof(RemoveBattleBacteriaCommand.Response)
                || responseType == typeof(ChangeBattleBacteriaModifierCommand.Response);
        }

        [HookWritable]
        public static void AnnounceNativeNotification(string localizedText)
        {
            string text = SpokenLines.Clean(localizedText);
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

        [HookWritable]
        public static void NotifyBacteriaAddedStarted(int troopId, string localizedText)
        {
            string text = SpokenLines.Clean(localizedText);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            SuppressedNativeNotifications.Add(text);
            if (SuppressedNativeNotifications.Count > MaxSuppressedNativeNotifications)
            {
                SuppressedNativeNotifications.RemoveAt(0);
            }
        }

        [HookWritable]
        public static void Reset()
        {
            ResetNarration();
            _activeAdapter = null;
        }

        /// <summary>
        /// The battle <paramref name="adapter"/> reads is over. What the narration holds was that
        /// battle's - the turn it was on, the notifications it suppressed, the batch it was in the
        /// middle of - so it is let go whichever battle is being read now. Only the slot naming the
        /// battle being read is kept when another one has already taken it: <c>LiveScreen</c> builds
        /// the next adapter, and so sets the slot, BEFORE disposing this one.
        /// </summary>
        public static void EndNarration(CombatAdapter adapter)
        {
            bool wasActive = IsActiveAdapter(adapter);
            ResetNarration();
            if (wasActive)
            {
                _activeAdapter = null;
            }
        }

        private static void ResetNarration()
        {
            Planner.Reset();
            SuppressedNativeNotifications.Clear();
            _currentTurnTroopId = -1;
            _flushPendingEventsScheduled = false;
            _flushScheduleGeneration = 0;
            _abilityBatchActive = false;
            _wielderEssenceCaptureActive = false;
            CapturedWielderEssence.Clear();
        }

        public static void FlushPendingEventsForCombatEnd()
        {
            _abilityBatchActive = false;
            FlushPendingEventsImmediately();
        }

        /// <summary>Whether this adapter is the one the narration is being read from - so a battle
        /// that has already been replaced does not put the new one's narration back to rest.</summary>
        public static bool IsActiveAdapter(CombatAdapter adapter)
        {
            return adapter != null && ReferenceEquals(_activeAdapter, adapter);
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
            if (response is InitiateBattleCommand.Response)
            {
                BeginWielderEssenceCapture();
                return;
            }

            MoveBattleTroopCommand.Response move = response as MoveBattleTroopCommand.Response;
            if (move != null)
            {
                IBattleTroopState troop = adapter.GetTroop(move.Id);
                if (troop != null && move.Path != null && move.Path.Length > 0)
                {
                    List<Vector2Int> path = ConvertPath(move.Path);
                    Vector2Int start = path[0];
                    Vector2Int end = path[path.Count - 1];
                    TroopMovedEvent moved = new TroopMovedEvent(CreateActor(adapter, troop, troop.Stats.Size, start), start, end, path);
                    // A root spreader takes its roots with it, and the game only redraws the ground.
                    string rootEffect;
                    int rootTiles;
                    if (adapter.TryGetRootCoverage(troop, end, out rootEffect, out rootTiles))
                    {
                        moved.SetRoots(rootEffect, rootTiles);
                    }

                    Enqueue(CombatNarrationItem.Create(
                        CombatNarrationItemKind.Move,
                        moved,
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
                BeginWielderEssenceCapture();
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

            AddEssenceToWalletCommand.Response addEssence = response as AddEssenceToWalletCommand.Response;
            if (addEssence != null)
            {
                TryCaptureWielderEssence(addEssence, adapter);
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

            // A thing on the board being destroyed is deliberately quiet: the damage that
            // destroyed it has already said so ("... destroying it").
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

            int leapTroopId;
            Vector2Int leapOriginalPosition;
            Vector2Int leapTargetPosition;
            if (TryReadLeapAbilityResponse(response, out leapTroopId, out leapOriginalPosition, out leapTargetPosition))
            {
                IBattleTroopState troop = adapter.GetTroop(leapTroopId);
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Ability,
                    CreateLeapAbilityUsedEvent(
                        CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, leapOriginalPosition),
                        adapter.CreateAbilityRef(TroopAbilityType.Leap),
                        leapOriginalPosition,
                        leapTargetPosition),
                    leapTroopId));
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

            // The ability was already announced when it began. Completion is recognised here only
            // so that HandleResponse can restart the batching wait for the effects that follow it.
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
            if (IsSyntheticBeamReactAttack(attacker, targetIsMapEntity, damage, adapter))
            {
                if (damage.StateId < 0)
                {
                    BeamFacing? direction = adapter.GetTeamSideBeamDirection(attacker);
                    if (direction.HasValue)
                    {
                        Enqueue(CombatNarrationItem.Create(
                            CombatNarrationItemKind.Attack,
                            new DirectionalAttackEvent(CreateActor(adapter, attacker), direction.Value),
                            attackerId));
                    }

                    return;
                }

                IBattleTroopState beamTarget = adapter.GetTroop(damage.StateId);
                int targetSize = beamTarget != null ? beamTarget.Stats.Size : Math.Max(damage.SizeAfter + damage.Kills, 0);
                TargetRef beamTargetRef = TargetRef.FromTroop(adapter.CreateTroopRef(beamTarget, targetSize));
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.Attack,
                    new AttackEvent(CreateActor(adapter, attacker), beamTargetRef, damage.AttackTrigger),
                    attackerId));
                return;
            }

            TargetRef target = targetIsMapEntity
                ? TargetRef.FromEntity(adapter.CreateEntityRef(adapter.GetMapEntity(damage.StateId)))
                : TargetRef.FromTroop(adapter.CreateTroopRef(adapter.GetTroop(damage.StateId), Math.Max(damage.SizeAfter + damage.Kills, 0)));
            Enqueue(CombatNarrationItem.Create(CombatNarrationItemKind.Attack, new AttackEvent(CreateActor(adapter, attacker), target, damage.AttackTrigger), attackerId));
        }

        private static bool IsSyntheticBeamReactAttack(
            IBattleTroopState attacker,
            bool targetIsMapEntity,
            DamageResult damage,
            CombatAdapter adapter)
        {
            return !targetIsMapEntity
                && damage.AttackTrigger == AttackTrigger.Damage
                && damage.Type == DamageType.Invalid
                && adapter.PerformsBeamAttacks(attacker);
        }

        private static void EnqueueBacteriaAdded(AddBattleBacteriaCommand.Response response, CombatAdapter adapter)
        {
            if (response.Entries == null)
            {
                return;
            }

            for (int i = 0; i < response.Entries.Length; i++)
            {
                AddBattleBacteriaCommand.ResponseEntry entry = response.Entries[i];
                if (entry == null)
                {
                    continue;
                }

                if (IsBattleTroopType(entry.StateTypeName) && entry.BacteriaReference != null)
                {
                    Enqueue(CombatNarrationItem.CreateBacteriaAddedMarker(
                        adapter.CreateBacteriaRef(entry.BacteriaReference),
                        entry.StateId));
                }
            }

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
                        CreateModifierChanges(group.Value, adapter),
                        changeSet.TargetId),
                        adapter);
                }
            }
        }

        private static bool ShouldBufferForSpellResponse(ICommandResponse response, CombatAdapter adapter)
        {
            AddBattleBacteriaCommand.Response addBacteria = response as AddBattleBacteriaCommand.Response;
            if (addBacteria != null)
            {
                return ContainsSpellBacteria(addBacteria);
            }

            RemoveBattleBacteriaCommand.Response removeBacteria = response as RemoveBattleBacteriaCommand.Response;
            if (removeBacteria != null)
            {
                return ContainsSpellBacteria(removeBacteria);
            }

            ChangeBattleBacteriaModifierCommand.Response modifier = response as ChangeBattleBacteriaModifierCommand.Response;
            if (modifier != null)
            {
                return ContainsSpellBacteriaModifier(modifier, adapter);
            }

            DamageBattleTroopCommand.Response troopDamage = response as DamageBattleTroopCommand.Response;
            if (troopDamage != null)
            {
                return troopDamage.Damage != null
                    && (troopDamage.Damage.Type == DamageType.Spell || IsSpellBacteriaType(troopDamage.BacteriaType));
            }

            DamageBattleMapEntityCommand.Response entityDamage = response as DamageBattleMapEntityCommand.Response;
            if (entityDamage != null)
            {
                return entityDamage.Damage != null && entityDamage.Damage.Type == DamageType.Spell;
            }

            return false;
        }

        private static bool ContainsSpellBacteria(AddBattleBacteriaCommand.Response response)
        {
            if (response == null || response.Entries == null)
            {
                return false;
            }

            for (int i = 0; i < response.Entries.Length; i++)
            {
                AddBattleBacteriaCommand.ResponseEntry entry = response.Entries[i];
                if (entry != null && IsSpellBacteriaReference(entry.BacteriaReference))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSpellBacteria(RemoveBattleBacteriaCommand.Response response)
        {
            if (response == null || response.Entries == null)
            {
                return false;
            }

            for (int i = 0; i < response.Entries.Length; i++)
            {
                RemoveBattleBacteriaCommand.Entry entry = response.Entries[i];
                if (entry != null && IsSpellBacteriaReference(entry.BacteriaReference))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsSpellBacteriaModifier(ChangeBattleBacteriaModifierCommand.Response response, CombatAdapter adapter)
        {
            if (response == null || response.ChangeSets == null)
            {
                return false;
            }

            for (int i = 0; i < response.ChangeSets.Length; i++)
            {
                ModifierChangeSet changeSet = response.ChangeSets[i];
                if (changeSet == null || changeSet.Changes == null)
                {
                    continue;
                }

                for (int j = 0; j < changeSet.Changes.Length; j++)
                {
                    ModifierChangeSet.Change change = changeSet.Changes[j];
                    if (change == null || change.Modifier == null)
                    {
                        continue;
                    }

                    if (IsSpellBacteriaReference(FindBacteriaReference(adapter, change.Modifier)))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSpellBacteriaReference(BacteriaReference bacteria)
        {
            return bacteria != null && IsSpellBacteriaType(bacteria.BacteriaType);
        }

        private static bool IsSpellBacteriaType(int bacteriaType)
        {
            return bacteriaType >= 0 && IsSpellBacteriaType((BacteriaTypes)bacteriaType);
        }

        private static bool IsSpellBacteriaType(BacteriaTypes bacteriaType)
        {
            return bacteriaType.ToString().StartsWith("Spell", StringComparison.Ordinal);
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

        public static AbilityUsedEvent CreateLeapAbilityUsedEvent(
            ActorRef actor,
            AbilityRef ability,
            Vector2Int originalPosition,
            Vector2Int targetPosition)
        {
            return new AbilityUsedEvent(actor, ability, targetPosition, new[] { originalPosition, targetPosition });
        }

        private static bool TryReadLeapAbilityResponse(
            ICommandResponse response,
            out int troopId,
            out Vector2Int originalPosition,
            out Vector2Int targetPosition)
        {
            troopId = -1;
            originalPosition = Vector2Int.zero;
            targetPosition = Vector2Int.zero;
            if (response == null)
            {
                return false;
            }

            Type responseType = response.GetType();
            string fullName = responseType.FullName ?? string.Empty;
            bool isLegacyLeapResponse = fullName == "SongsOfConquest.Common.Battle.LeapCommand+Response";
            if (!isLegacyLeapResponse)
            {
                return false;
            }

            object id = GetFieldValue(response, "TroopId");
            object original = GetFieldValue(response, "OriginalPosition");
            object target = GetFieldValue(response, "TargetPosition");
            if (!(id is int) || !(original is Vector2Int) || !(target is Vector2Int))
            {
                return false;
            }

            troopId = (int)id;
            originalPosition = (Vector2Int)original;
            targetPosition = (Vector2Int)target;
            return true;
        }

        private static object GetFieldValue(object owner, string fieldName)
        {
            FieldInfo field = owner != null ? owner.GetType().GetField(fieldName) : null;
            return field != null ? field.GetValue(owner) : null;
        }

        private static void EnqueueTeleport(TeleportBattleTroopCommand.Response response, CombatAdapter adapter)
        {
            if (response.Entries == null)
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

        private static void BeginWielderEssenceCapture()
        {
            _wielderEssenceCaptureActive = true;
            CapturedWielderEssence.Clear();
        }

        private static void TryCaptureWielderEssence(AddEssenceToWalletCommand.Response response, CombatAdapter adapter)
        {
            if (!_wielderEssenceCaptureActive || response == null || adapter == null)
            {
                return;
            }

            int expectedAmount = adapter.GetCommanderGeneratedEssenceAmount(response.CommanderId, response.EssenceType);
            if (expectedAmount <= 0 || expectedAmount != response.EssenceAmount)
            {
                return;
            }

            WielderEssenceGeneration generation;
            if (!CapturedWielderEssence.TryGetValue(response.CommanderId, out generation))
            {
                generation = new WielderEssenceGeneration(response.CommanderId);
                CapturedWielderEssence[response.CommanderId] = generation;
            }

            generation.Add(response.EssenceType, response.EssenceAmount);
        }

        private static void FlushCapturedWielderEssence(CombatAdapter adapter)
        {
            if (!_wielderEssenceCaptureActive)
            {
                return;
            }

            _wielderEssenceCaptureActive = false;
            if (adapter == null || CapturedWielderEssence.Count == 0)
            {
                CapturedWielderEssence.Clear();
                return;
            }

            foreach (WielderEssenceGeneration generation in CapturedWielderEssence.Values)
            {
                if (!generation.HasEssence)
                {
                    continue;
                }

                Enqueue(CombatNarrationItem.Direct(
                    CombatNarrationItemKind.WielderEssenceGenerated,
                    new WielderEssenceGeneratedEvent(
                        adapter.CreateCommanderRef(generation.CommanderId),
                        generation.Order,
                        generation.Creation,
                        generation.Chaos,
                        generation.Arcana,
                        generation.Destruction)));
            }

            CapturedWielderEssence.Clear();
        }

        private static void EnqueueBacteriaSummary(CombatNarrationItem pending, CombatAdapter adapter)
        {
            Planner.EnqueueBacteriaSummary(pending, CreateNarrationSnapshot(adapter));
        }

        /// <summary>Wait a bounded number of frames for the rest of an effect's responses, then
        /// say the batch. The wait is a coroutine on the loader's own behaviour, which
        /// <c>SocAccessMod.Stop</c> stops wholesale before anything else is torn down, so a wait
        /// cannot outlive a reload; the generation counter is what makes a wait that was overtaken by
        /// an immediate flush give up rather than say the batch twice.</summary>
        private static void ScheduleFlushPendingEvents()
        {
            if (_flushPendingEventsScheduled)
            {
                return;
            }

            SocAccessMod plugin = SocAccessMod.Instance;
            if (plugin == null)
            {
                FlushPendingEvents();
                return;
            }

            _flushPendingEventsScheduled = true;
            _flushScheduleGeneration++;
            plugin.StartCoroutine(FlushPendingEventsAfterResponseBatch(_flushScheduleGeneration));
        }

        private static void RestartScheduledFlushPendingEvents()
        {
            if (_flushPendingEventsScheduled)
            {
                _flushScheduleGeneration++;
                _flushPendingEventsScheduled = false;
            }

            ScheduleFlushPendingEvents();
        }

        private static IEnumerator FlushPendingEventsAfterResponseBatch(int generation)
        {
            for (int i = 0; i < CombatNarrationBatchFrames; i++)
            {
                yield return null;
            }

            if (!_flushPendingEventsScheduled || generation != _flushScheduleGeneration)
            {
                yield break;
            }

            _flushPendingEventsScheduled = false;
            _abilityBatchActive = false;
            FlushPendingEvents();
        }

        private static void FlushPendingEventsImmediately()
        {
            if (_flushPendingEventsScheduled)
            {
                _flushScheduleGeneration++;
                _flushPendingEventsScheduled = false;
            }

            FlushPendingEvents();
        }

        private static void FlushPendingEvents()
        {
            FlushCapturedWielderEssence(GetAdapter());
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
                    _activeAdapter?.RequestActingTroopFocus(pending.TroopId);
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

        private static List<ModifierChange> CreateModifierChanges(IList<ModifierChangeSet.Change> changes, CombatAdapter adapter)
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

                ModifierChange modifierChange = adapter != null
                    ? adapter.CreateModifierChange(modifier)
                    : new ModifierChange(modifier.Type, modifier.ApplicationType, modifier.AmountToAdd);
                if (modifierChange != null)
                {
                    result.Add(modifierChange);
                }
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

        /// <summary>
        /// Take the notification's own text out of what is waiting to be suppressed, and only that:
        /// the bacteria prefix queues a text before the game decides whether to draw it, and the
        /// game draws nothing for a troop whose view or head node is gone (a stack killed by the
        /// same effect). Draining everything ahead of the match fed that orphan to the next
        /// unrelated HUD notification, which was silenced, and left a genuine bacteria panel later
        /// on to be announced twice - the very duplicate this queue exists to prevent.
        /// </summary>
        private static bool ConsumeSuppressedNativeNotification(string text)
        {
            int at = SuppressedNativeNotifications.IndexOf(text);
            if (at < 0)
            {
                return false;
            }

            SuppressedNativeNotifications.RemoveAt(at);
            return true;
        }

        private static void PublishEvent(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                return;
            }

            AccessibilityEventBus.Publish(accessibilityEvent);
        }

        private sealed class WielderEssenceGeneration
        {
            private readonly HashSet<EssenceType> _capturedTypes = new HashSet<EssenceType>();

            public WielderEssenceGeneration(int commanderId)
            {
                CommanderId = commanderId;
            }

            public int CommanderId { get; private set; }
            public int Order { get; private set; }
            public int Creation { get; private set; }
            public int Chaos { get; private set; }
            public int Arcana { get; private set; }
            public int Destruction { get; private set; }
            public bool HasEssence
            {
                get { return Order > 0 || Creation > 0 || Chaos > 0 || Arcana > 0 || Destruction > 0; }
            }

            public void Add(EssenceType essenceType, int amount)
            {
                if (!_capturedTypes.Add(essenceType))
                {
                    return;
                }

                switch (essenceType)
                {
                    case EssenceType.Order:
                        Order = amount;
                        break;
                    case EssenceType.Creation:
                        Creation = amount;
                        break;
                    case EssenceType.Chaos:
                        Chaos = amount;
                        break;
                    case EssenceType.Arcana:
                        Arcana = amount;
                        break;
                    case EssenceType.Destruction:
                        Destruction = amount;
                        break;
                }
            }
        }

    }
}
