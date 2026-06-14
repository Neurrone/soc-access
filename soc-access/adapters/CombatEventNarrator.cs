using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
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
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal static class CombatEventNarrator
    {
        private const int BacteriaDiagnosticLogLimit = 200;
        private const int MapEntityCreationNarrationDiagnosticLogLimit = 100;
        private const int CombatNarrationTimingDiagnosticLogLimit = 400;
        private const int CombatNarrationBatchFrames = 30;

        private static readonly CombatNarrationPlanner Planner = new CombatNarrationPlanner();
        private static readonly Queue<string> SuppressedNativeNotifications = new Queue<string>();
        private static int _currentTurnTroopId = -1;
        private static bool _flushPendingEventsScheduled;
        private static CombatAdapter _activeAdapter;
        private static int _bacteriaDiagnosticLogCount;
        private static int _mapEntityCreationNarrationDiagnosticLogCount;
        private static int _combatNarrationTimingDiagnosticLogCount;
        private static int _lastCombatNarrationTimingDiagnosticFrame = -1;
        private static float _lastCombatNarrationTimingDiagnosticTime = -1f;
        private static int _flushScheduleGeneration;
        private static int _flushScheduledFrame = -1;
        private static float _flushScheduledTime = -1f;
        private static bool _abilityBatchActive;
        private static bool _wielderEssenceCaptureActive;
        private static readonly Dictionary<int, WielderEssenceGeneration> CapturedWielderEssence =
            new Dictionary<int, WielderEssenceGeneration>();

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
                LogCombatNarrationTimingDiagnostic("response", DescribeResponse(response));
                if (_abilityBatchActive && isEndTurnResponse)
                {
                    _abilityBatchActive = false;
                    FlushPendingEventsImmediately("ability_end_turn");
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
                    FlushPendingEventsImmediately("spell_response");
                }
                else if (_abilityBatchActive)
                {
                    ScheduleFlushPendingEvents();
                }
                else if (bufferForSpell || _flushPendingEventsScheduled)
                {
                    ScheduleFlushPendingEvents();
                }
                else
                {
                    FlushPendingEventsImmediately("non_spell_response");
                }
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
            _bacteriaDiagnosticLogCount = 0;
            _mapEntityCreationNarrationDiagnosticLogCount = 0;
            _combatNarrationTimingDiagnosticLogCount = 0;
            _lastCombatNarrationTimingDiagnosticFrame = -1;
            _lastCombatNarrationTimingDiagnosticTime = -1f;
            _flushScheduleGeneration = 0;
            _flushScheduledFrame = -1;
            _flushScheduledTime = -1f;
            _abilityBatchActive = false;
            _wielderEssenceCaptureActive = false;
            CapturedWielderEssence.Clear();
        }

        public static void FlushPendingEventsForCombatEnd()
        {
            _abilityBatchActive = false;
            FlushPendingEventsImmediately("combat_ended");
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
                EntityRef entityRef = adapter.CreateEntityRef(entity);
                MapEntityCreatedEvent mapEntityCreatedEvent = new MapEntityCreatedEvent(entityRef);
                LogMapEntityCreationResponseDiagnostic(createdEntity, entity, mapEntityCreatedEvent);
                Enqueue(CombatNarrationItem.Create(
                    CombatNarrationItemKind.MapEntityCreated,
                    mapEntityCreatedEvent,
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

                LogBacteriaDiagnostic("added", entry.BacteriaReference, entry.StateId, entry.StateTypeName, adapter);
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
                LogBacteriaDiagnostic("removed", entry.BacteriaReference, entry.StateId, entry.StateTypeName, adapter);
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
                    LogBacteriaDiagnostic("modifier applied", bacteria, changeSet.TargetId, changeSet.TargetTypeName, adapter);
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

        private static void LogBacteriaDiagnostic(string action, BacteriaReference bacteria, int stateId, string stateTypeName, CombatAdapter adapter)
        {
            if (bacteria == null || _bacteriaDiagnosticLogCount >= BacteriaDiagnosticLogLimit)
            {
                return;
            }

            _bacteriaDiagnosticLogCount++;
            SocAccessPlugin.Instance?.LogInfo(
                "Combat bacteria diagnostic "
                + action
                + ": type="
                + bacteria.BacteriaType
                + " ("
                + (int)bacteria.BacteriaType
                + "), id="
                + bacteria.Id
                + ", name=\""
                + ResolveBacteriaName(adapter, bacteria)
                + "\", stateId="
                + stateId
                + ", stateType="
                + FormatStateTypeName(stateTypeName));

            if (_bacteriaDiagnosticLogCount == BacteriaDiagnosticLogLimit)
            {
                SocAccessPlugin.Instance?.LogInfo("Combat bacteria diagnostic log limit reached; further bacteria diagnostics suppressed until combat narrator reset.");
            }
        }

        private static string ResolveBacteriaName(CombatAdapter adapter, BacteriaReference bacteria)
        {
            if (adapter == null || bacteria == null)
            {
                return string.Empty;
            }

            try
            {
                BacteriaRef bacteriaRef = adapter.CreateBacteriaRef(bacteria);
                return bacteriaRef != null ? bacteriaRef.Name : string.Empty;
            }
            catch (Exception exception)
            {
                return "unavailable: " + exception.Message;
            }
        }

        private static string FormatStateTypeName(string stateTypeName)
        {
            if (string.IsNullOrWhiteSpace(stateTypeName))
            {
                return string.Empty;
            }

            Type type = Type.GetType(stateTypeName);
            return type != null ? type.Name : stateTypeName;
        }

        private static void LogMapEntityCreationResponseDiagnostic(
            CreateBattleMapEntityCommand.Response response,
            IMapEntity resolvedEntity,
            MapEntityCreatedEvent narrationEvent)
        {
            if (response == null || response.State == null || !TryBeginMapEntityCreationNarrationDiagnostic())
            {
                return;
            }

            EntityRef entity = narrationEvent != null ? narrationEvent.Entity : null;
            SocAccessPlugin.Instance?.LogInfo(
                "Combat map entity creation response: stateId="
                + response.State.Id
                + ", stateBlueprintId="
                + response.State.BlueprintId
                + ", statePosition="
                + FormatDiagnosticPoint(response.State.Position)
                + ", resolvedId="
                + (resolvedEntity != null ? resolvedEntity.Id : -1)
                + ", resolvedBlueprintId="
                + (resolvedEntity != null ? resolvedEntity.BlueprintId : -1)
                + ", resolvedPosition="
                + (resolvedEntity != null ? FormatDiagnosticPoint(resolvedEntity.Position) : string.Empty)
                + ", resolvedNameKey=\""
                + (resolvedEntity != null ? resolvedEntity.NameKey : string.Empty)
                + "\", resolvedName=\""
                + (entity != null ? entity.Name : string.Empty)
                + "\", pendingSpeech=\""
                + (narrationEvent != null ? narrationEvent.GetSpeechText() : string.Empty)
                + "\"");
        }

        private static void LogMapEntityCreationNarrationDiagnostic(IAccessibilityEvent accessibilityEvent)
        {
            MapEntityCreatedEvent created = accessibilityEvent as MapEntityCreatedEvent;
            if (created == null || !TryBeginMapEntityCreationNarrationDiagnostic())
            {
                return;
            }

            EntityRef entity = created.Entity;
            SocAccessPlugin.Instance?.LogInfo(
                "Combat map entity creation narration: entityId="
                + (entity != null ? entity.EntityId : -1)
                + ", blueprintId="
                + (entity != null ? entity.BlueprintId : -1)
                + ", position="
                + (entity != null ? FormatDiagnosticPoint(entity.Position) : string.Empty)
                + ", name=\""
                + (entity != null ? entity.Name : string.Empty)
                + "\", speech=\""
                + created.GetSpeechText()
                + "\"");
        }

        private static bool TryBeginMapEntityCreationNarrationDiagnostic()
        {
            if (_mapEntityCreationNarrationDiagnosticLogCount >= MapEntityCreationNarrationDiagnosticLogLimit)
            {
                return false;
            }

            _mapEntityCreationNarrationDiagnosticLogCount++;
            if (_mapEntityCreationNarrationDiagnosticLogCount == MapEntityCreationNarrationDiagnosticLogLimit)
            {
                SocAccessPlugin.Instance?.LogInfo("Combat map entity creation narration diagnostic log limit reached.");
            }

            return true;
        }

        private static string FormatDiagnosticPoint(Vector2Int point)
        {
            return point.x + ", " + point.y;
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

        internal static AbilityUsedEvent CreateLeapAbilityUsedEvent(
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

        private static void ScheduleFlushPendingEvents()
        {
            if (_flushPendingEventsScheduled)
            {
                LogCombatNarrationTimingDiagnostic("flush_already_scheduled", "waitFrames=" + CombatNarrationBatchFrames);
                return;
            }

            SocAccessPlugin plugin = SocAccessPlugin.Instance;
            if (plugin == null)
            {
                LogCombatNarrationTimingDiagnostic("flush_immediate_no_plugin", null);
                FlushPendingEvents();
                return;
            }

            _flushPendingEventsScheduled = true;
            _flushScheduleGeneration++;
            _flushScheduledFrame = Time.frameCount;
            _flushScheduledTime = Time.realtimeSinceStartup;
            LogCombatNarrationTimingDiagnostic("flush_scheduled", "waitFrames=" + CombatNarrationBatchFrames);
            plugin.StartCoroutine(FlushPendingEventsAfterResponseBatch(_flushScheduleGeneration, _flushScheduledFrame, _flushScheduledTime));
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

        private static IEnumerator FlushPendingEventsAfterResponseBatch(int generation, int scheduledFrame, float scheduledTime)
        {
            for (int i = 0; i < CombatNarrationBatchFrames; i++)
            {
                yield return null;
            }

            if (!_flushPendingEventsScheduled || generation != _flushScheduleGeneration)
            {
                LogCombatNarrationTimingDiagnostic(
                    "flush_wait_stale",
                    "scheduledGeneration=" + generation
                    + ", currentGeneration=" + _flushScheduleGeneration
                    + ", elapsedFrames=" + GetElapsedFrames(scheduledFrame)
                    + ", elapsedSeconds=" + FormatSeconds(GetElapsedSeconds(scheduledTime)));
                yield break;
            }

            LogCombatNarrationTimingDiagnostic(
                "flush_wait_complete",
                "scheduledFrame=" + scheduledFrame
                + ", elapsedFrames=" + GetElapsedFrames(scheduledFrame)
                + ", elapsedSeconds=" + FormatSeconds(GetElapsedSeconds(scheduledTime)));
            _flushPendingEventsScheduled = false;
            _abilityBatchActive = false;
            FlushPendingEvents();
        }

        private static void FlushPendingEventsImmediately(string reason)
        {
            if (_flushPendingEventsScheduled)
            {
                _flushScheduleGeneration++;
                _flushPendingEventsScheduled = false;
            }

            LogCombatNarrationTimingDiagnostic("flush_immediate", "reason=" + reason);
            FlushPendingEvents();
        }

        private static void FlushPendingEvents()
        {
            FlushCapturedWielderEssence(GetAdapter());
            if (!Planner.HasPendingEvents)
            {
                LogCombatNarrationTimingDiagnostic("flush_skipped_no_pending", null);
                return;
            }

            LogCombatNarrationTimingDiagnostic("flush_begin", "pending=" + Planner.PendingCount);
            IReadOnlyList<CombatNarrationItem> events = Planner.Flush();
            LogCombatNarrationTimingDiagnostic("flush_publish_batch", "count=" + events.Count);
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

                LogCombatNarrationTimingDiagnostic("publish", "kind=" + pending.Kind);
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

                string localizedName = adapter != null ? adapter.LocalizeModifierName(modifier.Type) : string.Empty;
                result.Add(new ModifierChange(modifier.Type, modifier.ApplicationType, modifier.AmountToAdd, localizedName));
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

            LogMapEntityCreationNarrationDiagnostic(accessibilityEvent);
            AccessibilityEventBus.Publish(accessibilityEvent);
        }

        private static void LogCombatNarrationTimingDiagnostic(string phase, string detail)
        {
            if (_combatNarrationTimingDiagnosticLogCount >= CombatNarrationTimingDiagnosticLogLimit)
            {
                return;
            }

            _combatNarrationTimingDiagnosticLogCount++;
            int frame = Time.frameCount;
            float time = Time.realtimeSinceStartup;
            string frameDelta = _lastCombatNarrationTimingDiagnosticFrame >= 0
                ? (frame - _lastCombatNarrationTimingDiagnosticFrame).ToString(CultureInfo.InvariantCulture)
                : "n/a";
            string secondsDelta = _lastCombatNarrationTimingDiagnosticTime >= 0f
                ? FormatSeconds(time - _lastCombatNarrationTimingDiagnosticTime)
                : "n/a";

            SocAccessPlugin.Instance?.LogInfo(
                "Combat narration timing: phase=" + phase
                + ", frame=" + frame
                + ", deltaFrames=" + frameDelta
                + ", time=" + FormatSeconds(time)
                + ", deltaSeconds=" + secondsDelta
                + ", flushScheduled=" + _flushPendingEventsScheduled
                + ", abilityBatchActive=" + _abilityBatchActive
                + ", pending=" + Planner.PendingCount
                + (string.IsNullOrWhiteSpace(detail) ? string.Empty : ", " + detail));

            _lastCombatNarrationTimingDiagnosticFrame = frame;
            _lastCombatNarrationTimingDiagnosticTime = time;

            if (_combatNarrationTimingDiagnosticLogCount == CombatNarrationTimingDiagnosticLogLimit)
            {
                SocAccessPlugin.Instance?.LogInfo("Combat narration timing diagnostic reached log limit");
            }
        }

        private static string DescribeResponse(ICommandResponse response)
        {
            if (response == null)
            {
                return "response=null";
            }

            Type type = response.GetType();
            return "response=" + (type.FullName ?? type.Name);
        }

        private static int GetElapsedFrames(int scheduledFrame)
        {
            return scheduledFrame >= 0 ? Time.frameCount - scheduledFrame : -1;
        }

        private static float GetElapsedSeconds(float scheduledTime)
        {
            return scheduledTime >= 0f ? Time.realtimeSinceStartup - scheduledTime : -1f;
        }

        private static string FormatSeconds(float seconds)
        {
            return seconds.ToString("0.000", CultureInfo.InvariantCulture);
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
