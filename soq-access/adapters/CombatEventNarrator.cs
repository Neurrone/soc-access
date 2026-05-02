using System;
using System.Collections.Generic;
using System.Linq;
using Lavapotion.Networking;
using Lavapotion.Pathfinding;
using Lavapotion.Utilities;
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
        private static readonly Queue<PendingCombatEvent> PendingEvents = new Queue<PendingCombatEvent>();
        private static int _activeBlockingVisuals;
        private static int _currentTurnTroopId = -1;

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
                TryStartDirectEvents();
            }
            catch (Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("CombatEventNarrator failed to queue " + response.GetType().Name + ": " + exception.Message);
            }
        }

        public static void AnnounceNativeNotification(string localizedText)
        {
            string text = SpeechTextSanitizer.Normalize(localizedText);
            if (string.IsNullOrWhiteSpace(text))
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

        public static void NotifyMoveStarted(int troopId, IList<Vector2Int> path, Async completion)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.Move
                    && pending.TroopId == troopId
                    && SamePath(pending.Path, path),
                completion);
        }

        public static void NotifyAttackStarted(int attackerId, bool targetIsMapEntity, DamageResult result, Async completion)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.Attack
                    && pending.TroopId == attackerId
                    && pending.TargetIsMapEntity == targetIsMapEntity
                    && SameDamageResult(pending.DamageResult, result),
                completion);
        }

        public static void NotifyPushStarted(int troopId, Vector2Int from, Vector2Int to, Async completion)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.Push
                    && pending.TroopId == troopId
                    && pending.From == from
                    && pending.To == to,
                completion);
        }

        public static void NotifyAbilityStarted(int troopId, TroopAbilityType abilityType, Vector2Int? targetingPosition, Vector2Int[] movementPath, Async completion)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.Ability
                    && pending.TroopId == troopId
                    && pending.AbilityType == abilityType,
                completion);
        }

        public static void NotifyNewTurnStarted(int troopId)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.NewTurn
                    && pending.TroopId == troopId,
                null);
        }

        public static void NotifyNewRoundPopupVisible()
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.NewRound,
                null);
        }

        public static void NotifySpellStarted(SpellCastResponse response)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.Spell
                    && pending.Spell == response.Identifier
                    && pending.SpellTier == response.Tier
                    && SamePoints(pending.TargetPoints, response.TargetPoints),
                null);
        }

        public static void NotifyFaeyFireStarted(int attackerId, IList<DamageResult> results)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.FaeyFire
                    && pending.TroopId == attackerId,
                null);
        }

        public static void NotifyDamageIndicatorStarted(DamageResult result, bool targetIsMapEntity)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.Damage
                    && pending.TargetIsMapEntity == targetIsMapEntity
                    && SameDamageResult(pending.DamageResult, result),
                null);
        }

        public static void NotifyBacteriaAddedStarted(int troopId, string localizedText)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.BacteriaAdded
                    && pending.TroopId == troopId,
                null);
        }

        public static void NotifyBacteriaRemovedStarted(int troopId, BacteriaReference bacteriaReference)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.BacteriaRemoved
                    && pending.TroopId == troopId
                    && SameBacteria(pending.Bacteria, bacteriaReference),
                null);
        }

        public static void NotifyBacteriaModifierAppliedStarted(int troopId, BacteriaReference bacteriaReference)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.BacteriaModifierApplied
                    && pending.TroopId == troopId
                    && SameBacteria(pending.Bacteria, bacteriaReference),
                null);
        }

        public static void NotifyEssenceGeneratedStarted(int troopId)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.EssenceGenerated
                    && pending.TroopId == troopId,
                null);
        }

        public static void NotifyMapEntityCreatedStarted(int entityId)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.MapEntityCreated
                    && pending.EntityId == entityId,
                null);
        }

        public static void NotifyMapEntityDestroyedStarted(int entityId)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.MapEntityDestroyed
                    && pending.EntityId == entityId,
                null);
        }

        public static void NotifyBurrowUpStarted(int troopId, bool burrowSuccess)
        {
            StartMatching(
                pending => pending.Kind == PendingCombatEventKind.BurrowUp
                    && pending.TroopId == troopId
                    && pending.BurrowSuccess == burrowSuccess,
                null);
        }

        public static void NotifyBattleResultPopupStarted(bool isVictory)
        {
            bool releasedQueuedResult = StartMatching(
                pending => pending.Kind == PendingCombatEventKind.BattleResult,
                null);
            if (releasedQueuedResult)
            {
                return;
            }

            _currentTurnTroopId = -1;
            PublishEvent(new BattleResultEvent(
                -1,
                isVictory ? BattleOutcome.Victory : BattleOutcome.Defeat,
                -1,
                BattleOutcome.Inconclusive,
                -1,
                BattleOutcome.Inconclusive));
            TryStartDirectEvents();
        }

        public static void Reset()
        {
            PendingEvents.Clear();
            _activeBlockingVisuals = 0;
            _currentTurnTroopId = -1;
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
            CombatAdapter adapter = CombatRuntimeScreenProbe.FindActiveCombatAdapter();
            if (adapter == null || !adapter.IsPresent())
            {
                return null;
            }

            if (_currentTurnTroopId < 0)
            {
                SyncCurrentTurnTroop(adapter);
            }

            return adapter;
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
                    Enqueue(PendingCombatEvent.Visual(
                        PendingCombatEventKind.Move,
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
                    Enqueue(PendingCombatEvent.Visual(
                        PendingCombatEventKind.NewTurn,
                        new NewTurnEvent(adapter.CreateTroopRef(troop)),
                        troop.Id));
                }

                return;
            }

            if (response is NewBattleRoundCommand.Response)
            {
                int round = adapter.GetCurrentRound() + 1;
                Enqueue(PendingCombatEvent.Visual(
                    PendingCombatEventKind.NewRound,
                    new NewRoundEvent(round)));
                return;
            }

            if (response is UpdateBattleQueueCommand.Response)
            {
                Enqueue(PendingCombatEvent.Direct(PendingCombatEventKind.QueueChanged, new QueueChangedEvent()));
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
                Enqueue(PendingCombatEvent.Damage(
                    BuildTroopDamageEvent(troopDamage.Damage, troopDamage.AttackingTroopId, troopDamage.IsSplashDamage, troopDamage.BacteriaType, adapter),
                    troopDamage.AttackingTroopId,
                    targetIsMapEntity: false,
                    troopDamage.Damage));
                return;
            }

            DamageBattleMapEntityCommand.Response entityDamage = response as DamageBattleMapEntityCommand.Response;
            if (entityDamage != null)
            {
                Enqueue(PendingCombatEvent.Damage(
                    BuildEntityDamageEvent(entityDamage.AttackerId, entityDamage.MapEntityId, entityDamage.Damage, entityDamage.IsSplashDamage, adapter),
                    entityDamage.AttackerId,
                    targetIsMapEntity: true,
                    entityDamage.Damage));
                return;
            }

            CastBattleSpellCommand.Response spell = response as CastBattleSpellCommand.Response;
            if (spell != null)
            {
                Enqueue(PendingCombatEvent.CreateSpell(
                    BuildSpellEvent(spell, adapter),
                    spell.CastResponse.Identifier,
                    spell.CastResponse.Tier,
                    spell.CastResponse.TargetPoints));
                return;
            }

            FaeyFireCommand.Response faeyFire = response as FaeyFireCommand.Response;
            if (faeyFire != null)
            {
                Enqueue(PendingCombatEvent.FaeyFire(
                    BuildFaeyFireEvent(faeyFire, adapter),
                    faeyFire.AttackerId,
                    faeyFire.DamageResults));
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
                Enqueue(PendingCombatEvent.Visual(
                    PendingCombatEventKind.EssenceGenerated,
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
                Enqueue(PendingCombatEvent.Direct(
                    PendingCombatEventKind.TroopCreated,
                    new TroopCreatedEvent(adapter.CreateTroopRef(createdTroop.Troop), isSummon: true)));
                return;
            }

            CreateBattleMapEntityCommand.Response createdEntity = response as CreateBattleMapEntityCommand.Response;
            if (createdEntity != null && createdEntity.State != null)
            {
                IMapEntity entity = adapter.GetMapEntity(createdEntity.State.Id);
                Enqueue(PendingCombatEvent.Visual(
                    PendingCombatEventKind.MapEntityCreated,
                    new MapEntityCreatedEvent(adapter.CreateEntityRef(entity)),
                    entityId: createdEntity.State.Id));
                return;
            }

            DestroyBattleMapEntityCommand.Response destroyedEntity = response as DestroyBattleMapEntityCommand.Response;
            if (destroyedEntity != null)
            {
                IMapEntity entity = adapter.GetMapEntity(destroyedEntity.MapEntityId);
                Enqueue(PendingCombatEvent.Visual(
                    PendingCombatEventKind.MapEntityDestroyed,
                    new MapEntityDestroyedEvent(adapter.CreateEntityRef(entity)),
                    entityId: destroyedEntity.MapEntityId));
                return;
            }

            PushBattleTroopCommand.Response push = response as PushBattleTroopCommand.Response;
            if (push != null && push.Path != null && push.Path.Length > 1)
            {
                IBattleTroopState troop = adapter.GetTroop(push.TroopId);
                Vector2Int from = push.Path[0];
                Vector2Int to = push.Path[push.Path.Length - 1];
                Enqueue(PendingCombatEvent.Push(
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
                Enqueue(PendingCombatEvent.Ability(
                    PendingCombatEventKind.Ability,
                    new AbilityUsedEvent(CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, from), adapter.CreateAbilityRef(TroopAbilityType.BullRush), null, bullRush.Path),
                    bullRush.TroopId,
                    TroopAbilityType.BullRush));
                return;
            }

            LeapCommand.Response leap = response as LeapCommand.Response;
            if (leap != null)
            {
                IBattleTroopState troop = adapter.GetTroop(leap.TroopId);
                Enqueue(PendingCombatEvent.Ability(
                    PendingCombatEventKind.Ability,
                    new AbilityUsedEvent(CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, leap.OriginalPosition), adapter.CreateAbilityRef(TroopAbilityType.Leap), leap.TargetPosition, new[] { leap.OriginalPosition, leap.TargetPosition }),
                    leap.TroopId,
                    TroopAbilityType.Leap));
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
                Enqueue(PendingCombatEvent.Ability(
                    PendingCombatEventKind.Ability,
                    new AbilityUsedEvent(CreateActor(adapter, troop), adapter.CreateAbilityRef(ability.TroopAbility), null, null),
                    ability.BattleTroopId,
                    ability.TroopAbility));
                return;
            }

            TroopAbilityActivationCompleteCommand.Response abilityComplete = response as TroopAbilityActivationCompleteCommand.Response;
            if (abilityComplete != null)
            {
                Enqueue(PendingCombatEvent.Direct(PendingCombatEventKind.AbilityComplete, new AbilityCompleteEvent()));
                return;
            }

            BurrowUpCommand.Response burrowUp = response as BurrowUpCommand.Response;
            if (burrowUp != null)
            {
                IBattleTroopState troop = adapter.GetTroop(burrowUp.TroopId);
                Enqueue(PendingCombatEvent.BurrowUp(new BurrowUpEvent(CreateActor(adapter, troop), burrowUp.BurrowSuccess), burrowUp.TroopId, burrowUp.BurrowSuccess));
                return;
            }

            BattleResultCommand.Response battleResult = response as BattleResultCommand.Response;
            if (battleResult != null)
            {
                Enqueue(PendingCombatEvent.Visual(PendingCombatEventKind.BattleResult, BuildBattleResultEvent(battleResult, adapter)));
            }
        }

        private static void EnqueueAttack(int attackerId, bool targetIsMapEntity, DamageResult damage, CombatAdapter adapter)
        {
            IBattleTroopState attacker = adapter.GetTroop(attackerId);
            TargetRef target = targetIsMapEntity
                ? TargetRef.FromEntity(adapter.CreateEntityRef(adapter.GetMapEntity(damage.StateId)))
                : TargetRef.FromTroop(adapter.CreateTroopRef(adapter.GetTroop(damage.StateId), Math.Max(damage.SizeAfter + damage.Kills, 0)));
            Enqueue(PendingCombatEvent.Attack(new AttackEvent(CreateActor(adapter, attacker), target, damage.AttackTrigger), attackerId, targetIsMapEntity, damage));
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
                if (!IsBattleTroopType(entry.StateTypeName) || entry.BacteriaReference == null)
                {
                    continue;
                }

                IBattleTroopState troop = adapter.GetTroop(entry.StateId);
                Enqueue(PendingCombatEvent.CreateBacteria(
                    PendingCombatEventKind.BacteriaAdded,
                    new BacteriaAddedEvent(adapter.CreateTroopRef(troop), adapter.CreateBacteriaRef(entry.BacteriaReference)),
                    entry.StateId,
                    entry.BacteriaReference,
                    optionalVisual: true));
            }
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
                Enqueue(PendingCombatEvent.CreateBacteria(
                    PendingCombatEventKind.BacteriaRemoved,
                    new BacteriaRemovedEvent(adapter.CreateTroopRef(troop), adapter.CreateBacteriaRef(entry.BacteriaReference)),
                    entry.StateId,
                    entry.BacteriaReference));
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
                    Enqueue(PendingCombatEvent.CreateBacteria(
                        PendingCombatEventKind.BacteriaModifierApplied,
                        new BacteriaModifierAppliedEvent(adapter.CreateTroopRef(troop), adapter.CreateBacteriaRef(bacteria), CreateModifierChanges(group.Value)),
                        changeSet.TargetId,
                        bacteria));
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
                Enqueue(PendingCombatEvent.Visual(
                    PendingCombatEventKind.Teleport,
                    new TeleportEvent(CreateActor(adapter, troop, troop != null ? troop.Stats.Size : 0, entry.OldPosition), entry.OldPosition, entry.NewPosition, response.Source),
                    entry.Id));
            }
        }

        private static void Enqueue(PendingCombatEvent pending)
        {
            if (pending == null)
            {
                return;
            }

            PendingEvents.Enqueue(pending);
        }

        private static bool StartMatching(Func<PendingCombatEvent, bool> predicate, Async completion)
        {
            PendingCombatEvent pending = FindAndPromoteMatching(predicate);
            if (pending == null)
            {
                return false;
            }

            if (pending.Kind == PendingCombatEventKind.NewTurn)
            {
                _currentTurnTroopId = pending.TroopId;
            }

            PublishEvent(pending.Event);

            if (pending.Kind == PendingCombatEventKind.BattleResult)
            {
                _currentTurnTroopId = -1;
            }

            if (completion != null && !completion.IsCompleted)
            {
                _activeBlockingVisuals++;
                completion.Wait(CompleteBlockingVisual);
                return true;
            }

            TryStartDirectEvents();
            return true;
        }

        private static PendingCombatEvent FindAndPromoteMatching(Func<PendingCombatEvent, bool> predicate)
        {
            if (predicate == null || PendingEvents.Count == 0)
            {
                return null;
            }

            PendingCombatEvent[] events = PendingEvents.ToArray();
            int index = -1;
            for (int i = 0; i < events.Length; i++)
            {
                if (predicate(events[i]))
                {
                    index = i;
                    break;
                }
            }

            if (index < 0)
            {
                return null;
            }

            for (int i = 0; i < index; i++)
            {
                SoqAccessPlugin.Instance?.LogWarning("CombatEventNarrator force-completed pending event before visual match: " + events[i].Kind);
            }

            PendingEvents.Clear();
            PendingCombatEvent matched = null;
            for (int i = 0; i < events.Length; i++)
            {
                if (i < index)
                {
                    continue;
                }

                if (i == index)
                {
                    matched = events[i];
                    continue;
                }

                PendingEvents.Enqueue(events[i]);
            }

            return matched;
        }

        private static void TryStartDirectEvents()
        {
            if (_activeBlockingVisuals > 0)
            {
                return;
            }

            while (PendingEvents.Count > 0)
            {
                PendingCombatEvent pending = PendingEvents.Peek();
                if (!pending.IsDirect)
                {
                    if (pending.OptionalVisual && PendingEvents.Count > 1)
                    {
                        PendingEvents.Dequeue();
                        continue;
                    }

                    return;
                }

                PendingEvents.Dequeue();
                PublishEvent(pending.Event);
            }
        }

        private static void CompleteBlockingVisual()
        {
            if (_activeBlockingVisuals > 0)
            {
                _activeBlockingVisuals--;
            }

            TryStartDirectEvents();
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

        private static bool SamePath(IList<Vector2Int> left, IList<Vector2Int> right)
        {
            if (left == null || right == null || left.Count != right.Count)
            {
                return false;
            }

            for (int i = 0; i < left.Count; i++)
            {
                if (left[i] != right[i])
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SamePoints(IList<Vector2Int> left, IList<Vector2Int> right)
        {
            return SamePath(left, right);
        }

        private static bool SameDamageResult(DamageResult left, DamageResult right)
        {
            return left.StateId == right.StateId
                && left.Damage == right.Damage
                && left.Kills == right.Kills
                && left.HealthAfter == right.HealthAfter
                && left.SizeAfter == right.SizeAfter
                && left.Type == right.Type
                && left.AttackTrigger == right.AttackTrigger;
        }

        private static bool SameBacteria(BacteriaReference left, BacteriaReference right)
        {
            if (left == null || right == null)
            {
                return false;
            }

            return left.Id == right.Id && left.BacteriaType == right.BacteriaType;
        }

        private static void PublishEvent(IAccessibilityEvent accessibilityEvent)
        {
            if (accessibilityEvent == null)
            {
                return;
            }

            if (accessibilityEvent is QueueChangedEvent || accessibilityEvent is AbilityCompleteEvent)
            {
                return;
            }

            AccessibilityEventBus.Publish(accessibilityEvent);
        }

        private sealed class PendingCombatEvent
        {
            public PendingCombatEventKind Kind;
            public IAccessibilityEvent Event;
            public int TroopId = -1;
            public int EntityId = -1;
            public bool TargetIsMapEntity;
            public DamageResult DamageResult;
            public List<Vector2Int> Path;
            public Vector2Int From;
            public Vector2Int To;
            public TroopAbilityType AbilityType;
            public SpellTypes Spell;
            public int SpellTier;
            public List<Vector2Int> TargetPoints;
            public BacteriaReference Bacteria;
            public bool BurrowSuccess;
            public bool IsDirect;
            public bool OptionalVisual;

            public static PendingCombatEvent Direct(PendingCombatEventKind kind, IAccessibilityEvent accessibilityEvent)
            {
                return new PendingCombatEvent
                {
                    Kind = kind,
                    Event = accessibilityEvent,
                    IsDirect = true
                };
            }

            public static PendingCombatEvent Visual(PendingCombatEventKind kind, IAccessibilityEvent accessibilityEvent, int troopId = -1, List<Vector2Int> path = null, int entityId = -1)
            {
                return new PendingCombatEvent
                {
                    Kind = kind,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    Path = path,
                    EntityId = entityId
                };
            }

            public static PendingCombatEvent Attack(IAccessibilityEvent accessibilityEvent, int troopId, bool targetIsMapEntity, DamageResult damageResult)
            {
                return new PendingCombatEvent
                {
                    Kind = PendingCombatEventKind.Attack,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    TargetIsMapEntity = targetIsMapEntity,
                    DamageResult = damageResult
                };
            }

            public static PendingCombatEvent Damage(IAccessibilityEvent accessibilityEvent, int troopId, bool targetIsMapEntity, DamageResult damageResult)
            {
                return new PendingCombatEvent
                {
                    Kind = PendingCombatEventKind.Damage,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    TargetIsMapEntity = targetIsMapEntity,
                    DamageResult = damageResult
                };
            }

            public static PendingCombatEvent CreateSpell(IAccessibilityEvent accessibilityEvent, SpellTypes spell, int tier, List<Vector2Int> targetPoints)
            {
                return new PendingCombatEvent
                {
                    Kind = PendingCombatEventKind.Spell,
                    Event = accessibilityEvent,
                    Spell = spell,
                    SpellTier = tier,
                    TargetPoints = targetPoints
                };
            }

            public static PendingCombatEvent FaeyFire(IAccessibilityEvent accessibilityEvent, int troopId, List<DamageResult> results)
            {
                return new PendingCombatEvent
                {
                    Kind = PendingCombatEventKind.FaeyFire,
                    Event = accessibilityEvent,
                    TroopId = troopId
                };
            }

            public static PendingCombatEvent CreateBacteria(PendingCombatEventKind kind, IAccessibilityEvent accessibilityEvent, int troopId, BacteriaReference bacteria, bool optionalVisual = false)
            {
                return new PendingCombatEvent
                {
                    Kind = kind,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    Bacteria = bacteria,
                    OptionalVisual = optionalVisual
                };
            }

            public static PendingCombatEvent Push(IAccessibilityEvent accessibilityEvent, int troopId, Vector2Int from, Vector2Int to)
            {
                return new PendingCombatEvent
                {
                    Kind = PendingCombatEventKind.Push,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    From = from,
                    To = to
                };
            }

            public static PendingCombatEvent Ability(PendingCombatEventKind kind, IAccessibilityEvent accessibilityEvent, int troopId, TroopAbilityType abilityType)
            {
                return new PendingCombatEvent
                {
                    Kind = kind,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    AbilityType = abilityType
                };
            }

            public static PendingCombatEvent BurrowUp(IAccessibilityEvent accessibilityEvent, int troopId, bool burrowSuccess)
            {
                return new PendingCombatEvent
                {
                    Kind = PendingCombatEventKind.BurrowUp,
                    Event = accessibilityEvent,
                    TroopId = troopId,
                    BurrowSuccess = burrowSuccess
                };
            }
        }

        private enum PendingCombatEventKind
        {
            NewTurn,
            NewRound,
            QueueChanged,
            Move,
            Attack,
            Damage,
            Spell,
            FaeyFire,
            BacteriaAdded,
            BacteriaRemoved,
            BacteriaModifierApplied,
            EssenceGenerated,
            TroopCreated,
            MapEntityCreated,
            MapEntityDestroyed,
            Push,
            Ability,
            Teleport,
            BurrowUp,
            AbilityComplete,
            BattleResult
        }
    }
}
