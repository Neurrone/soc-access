using System;
using System.Collections.Generic;
using System.Linq;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // WHAT THE GAME KNOWS ABOUT A TROOP, moved out of CombatAdapter.cs unchanged: the facts a
    // spoken stack row is made of, the beam facings, the sides and teams, and the roster queries
    // that answer which troops are alive or acting.

    public sealed partial class CombatAdapter
    {
        /// <summary>Everything a spoken stack row is made of, read from the game in one go.</summary>
        public CombatTroopFacts GetTroopFacts(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return new CombatTroopFacts(string.Empty, 0, 0, 0, false, false);
            }

            bool reloading;
            IReadOnlyList<string> restrictions = GetTroopRestrictionNames(troop, out reloading);
            return new CombatTroopFacts(
                SpokenLines.Clean(_facade.Troops.GetName(troop.Id, troop.Stats.Size)),
                troop.Stats.Size,
                troop.CurrentHealth,
                troop.Stats.MaxHealth.GetValue(),
                IsEnemyTroop(troop),
                IsActingTroop(troop),
                reloading,
                restrictions,
                Hud != null ? Hud.GetTroopEffectNames(troop.Id) : null);
        }

        /// <summary>The game's own names for the restrictions the stack itself carries, and whether
        /// one of them is Reloading, which the mod words for itself.
        ///
        /// Three of the game's six are said: the two that change what can be done TO the stack
        /// (Invulnerable, MagicImmunity) and the one that changes what it can do this turn
        /// (Reloading). The other three - the retaliation pair and the zone-of-control pass - are
        /// facts about an exchange rather than about the stack standing there.</summary>
        private IReadOnlyList<string> GetTroopRestrictionNames(IBattleTroopState troop, out bool reloading)
        {
            reloading = false;
            List<string> names = new List<string>();
            try
            {
                IList<BattleTroopRestriction> restrictions = troop.Restrictions;
                for (int i = 0; restrictions != null && i < restrictions.Count; i++)
                {
                    BattleTroopRestriction restriction = restrictions[i];
                    if (restriction == BattleTroopRestriction.Reloading)
                    {
                        reloading = true;
                        continue;
                    }

                    if (restriction != BattleTroopRestriction.Invulnerable
                        && restriction != BattleTroopRestriction.MagicImmunity)
                    {
                        continue;
                    }

                    string name = SpokenLines.Clean(LocalizeText("Units/Restrictions/" + restriction));
                    if (!string.IsNullOrWhiteSpace(name) && !names.Contains(name))
                    {
                        names.Add(name);
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("GetTroopRestrictionNames", exception);
            }

            return names;
        }

        /// <summary>Everything a spoken row for an attackable thing is made of.</summary>
        public CombatEntityFacts GetEntityFacts(IMapEntity entity)
        {
            IHealthComponent health = entity != null ? entity.GetComponent<IHealthComponent>() : null;
            return new CombatEntityFacts(
                GetMapEntityName(entity),
                health != null,
                health != null ? health.HealthLeft : 0,
                health != null ? health.MaxHealth.GetValue() : 0);
        }

        public bool PerformsBeamAttacks(IBattleTroopState troop)
        {
            return troop != null && troop.PerformsBeamAttacks();
        }

        public BeamFacing? GetBeamFacing(IBattleTroopState troop)
        {
            if (!PerformsBeamAttacks(troop) || _battleViewManager == null)
            {
                return null;
            }

            IBattleTroopView view = _battleViewManager.GetTroopView(troop.Id);
            if (view == null)
            {
                return null;
            }

            return view.IsLookingRight ? BeamFacing.Right : BeamFacing.Left;
        }

        public BeamFacing? GetTeamSideBeamDirection(IBattleTroopState troop)
        {
            if (!PerformsBeamAttacks(troop) || _facade == null || _facade.Teams == null)
            {
                return null;
            }

            return troop.TeamId == _facade.Teams.AttackingTeam.Id ? BeamFacing.Right : BeamFacing.Left;
        }

        public int GetCommanderGeneratedEssenceAmount(int commanderId, EssenceType essenceType)
        {
            try
            {
                ICommanderState commander = _facade != null && _facade.Commanders != null ? _facade.Commanders.Get(commanderId) : null;
                return commander != null && commander.Stats != null && commander.Stats.Essences != null
                    ? commander.Stats.Essences.GetValue(essenceType)
                    : 0;
            }
            catch (Exception exception)
            {
                _faults.Report("GetCommanderGeneratedEssenceAmount", exception);
                return 0;
            }
        }

        public int LocalTeamId
        {
            get { return GetLocalTeamId(); }
        }

        public IReadOnlyList<int> GetAliveBattleTroopIdsForSide(bool enemySide)
        {
            List<int> ids = new List<int>();
            try
            {
                if (_facade == null || _facade.Troops == null || _facade.Troops.All == null)
                {
                    return ids;
                }

                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return ids;
                }

                foreach (IBattleTroopState troop in _facade.Troops.All)
                {
                    if (troop == null || !troop.GetIsAlive())
                    {
                        continue;
                    }

                    bool isEnemy = troop.TeamId != localTeamId;
                    if (isEnemy == enemySide)
                    {
                        ids.Add(troop.Id);
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("GetAliveBattleTroopIdsForSide", exception);
            }

            return ids;
        }

        public IReadOnlyList<int> GetAliveMeleeBattleTroopIdsForSide(bool enemySide)
        {
            return GetAliveBattleTroopIdsForSide(enemySide, troop => troop.HasMeleeAttack() && !troop.HasRangedAttack());
        }

        public IReadOnlyList<int> GetAliveRangedBattleTroopIdsForSide(bool enemySide)
        {
            return GetAliveBattleTroopIdsForSide(enemySide, troop => troop.HasRangedAttack());
        }

        private IReadOnlyList<int> GetAliveBattleTroopIdsForSide(bool enemySide, Func<IBattleTroopState, bool> predicate)
        {
            List<int> ids = new List<int>();
            try
            {
                if (_facade == null || _facade.Troops == null || _facade.Troops.All == null)
                {
                    return ids;
                }

                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return ids;
                }

                foreach (IBattleTroopState troop in _facade.Troops.All)
                {
                    if (troop == null || !troop.GetIsAlive())
                    {
                        continue;
                    }

                    bool isEnemy = troop.TeamId != localTeamId;
                    if (isEnemy == enemySide && (predicate == null || predicate(troop)))
                    {
                        ids.Add(troop.Id);
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("GetAliveBattleTroopIdsForSide.Filtered", exception);
            }

            return ids;
        }

        public bool IsActingTroop(IBattleTroopState troop)
        {
            IBattleTroopState current = GetCurrentTroop();
            return troop != null && current != null && troop.Id == current.Id;
        }

        public bool IsEnemyTroop(IBattleTroopState troop)
        {
            int localTeamId = GetLocalTeamId();
            return troop != null && localTeamId >= 0 && troop.TeamId != localTeamId;
        }

        private int GetLocalTeamId()
        {
            return BattleFacadeState.LocalTeamId(_facade);
        }

        /// <summary>Which side's HUD column is the local player's, where either is.</summary>
        public CombatHudSide? GetLocalCombatHudSide()
        {
            if (Hud == null || Hud.Commanders == null)
            {
                return null;
            }

            int localTeamId = GetLocalTeamId();
            if (localTeamId < 0)
            {
                return null;
            }

            if (Hud.Commanders.GetCommanderTeamId(CombatHudSide.Attacker) == localTeamId)
            {
                return CombatHudSide.Attacker;
            }

            if (Hud.Commanders.GetCommanderTeamId(CombatHudSide.Defender) == localTeamId)
            {
                return CombatHudSide.Defender;
            }

            return null;
        }

        public CombatHudSide? GetEnemyCombatHudSide()
        {
            CombatHudSide? localSide = GetLocalCombatHudSide();
            if (!localSide.HasValue)
            {
                return null;
            }

            return localSide.Value == CombatHudSide.Attacker ? CombatHudSide.Defender : CombatHudSide.Attacker;
        }

        public IBattleTroopState GetTroop(int troopId)
        {
            try
            {
                return _facade != null && _facade.Troops != null ? _facade.Troops.Get(troopId) : null;
            }
            catch (Exception exception)
            {
                _faults.Report("GetTroop", exception);
                return null;
            }
        }

        public IMapEntity GetMapEntity(int entityId)
        {
            try
            {
                return _facade != null && _facade.MapEntities != null ? _facade.MapEntities.Get(entityId) : null;
            }
            catch (Exception exception)
            {
                _faults.Report("GetMapEntity", exception);
                return null;
            }
        }

        /// <summary>The battle's own turn counter: <c>EndBattleTurnCommand</c> raises
        /// <c>Queue.CurrentTurn</c> by one whenever a turn ends, so it is the generation anything
        /// that changes with the turn - reach, the moves left, whose turn it is - can be keyed on.
        /// One field read.</summary>
        public int GetCurrentTurn()
        {
            try
            {
                return _facade != null && _facade.Queue != null ? _facade.Queue.CurrentTurn : 0;
            }
            catch (Exception exception)
            {
                _faults.Report("GetCurrentTurn", exception);
                return 0;
            }
        }

        public int GetCurrentRound()
        {
            return BattleFacadeState.CurrentRound(_facade);
        }

        public IReadOnlyList<int> GetLocalActingTroopIds()
        {
            return GetActingTroopIds(CombatTroopSideFilter.CurrentPlayer);
        }

        public IReadOnlyList<int> GetEnemyActingTroopIds()
        {
            return GetActingTroopIds(CombatTroopSideFilter.Enemy);
        }

        private IReadOnlyList<int> GetActingTroopIds(CombatTroopSideFilter side)
        {
            List<int> troopIds = new List<int>();
            try
            {
                if (_facade == null
                    || _facade.Queue == null
                    || _facade.Troops == null
                    || _facade.Teams == null
                    || !_facade.Teams.IsCurrentLocal
                    || _facade.Queue.Count <= 0)
                {
                    return troopIds;
                }

                int localTeamId = GetLocalTeamId();
                if (localTeamId < 0)
                {
                    return troopIds;
                }

                for (int i = 0; i < _facade.Queue.Count; i++)
                {
                    QueuedTroop queuedTroop = _facade.Queue[i];
                    if (queuedTroop.Id < 0)
                    {
                        continue;
                    }

                    IBattleTroopState troop = GetTroop(queuedTroop.Id);
                    if (troop == null || !troop.GetIsAlive() || !IsValidTile(troop.Position))
                    {
                        continue;
                    }

                    bool isEnemy = troop.TeamId != localTeamId;
                    bool include = side == CombatTroopSideFilter.Enemy ? isEnemy : !isEnemy;
                    if (include && !troopIds.Contains(troop.Id))
                    {
                        troopIds.Add(troop.Id);
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("GetActingTroopIds", exception);
                return troopIds;
            }

            return troopIds;
        }

        public bool TryGetTroopPosition(int troopId, out Vector2Int position, bool requireLocalCurrentTurn)
        {
            position = Vector2Int.zero;
            if (_facade == null || _facade.Teams == null)
            {
                return false;
            }

            if (requireLocalCurrentTurn && !_facade.Teams.IsCurrentLocal)
            {
                return false;
            }

            IBattleTroopState troop = GetTroop(troopId);
            if (troop == null)
            {
                return false;
            }

            int localTeamId = GetLocalTeamId();
            if (requireLocalCurrentTurn && localTeamId >= 0 && troop.TeamId != localTeamId)
            {
                return false;
            }

            position = troop.Position;
            return IsValidTile(position);
        }

        public string LocalizeText(string key)
        {
            return GameText.Get(_localization, key, string.Empty);
        }
    }
}
