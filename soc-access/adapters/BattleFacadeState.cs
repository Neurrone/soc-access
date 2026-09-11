using System;
using SongsOfConquest.Client.Battle;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The few facts about a battle that both the board and the HUD ask the same facade for.
    /// Each answers a fallback rather than throwing, because a caller reads them while the
    /// battle is starting or already over.
    /// </summary>
    public static class BattleFacadeState
    {
        /// <summary>The team the local player is playing right now; -1 when there is none.</summary>
        public static int LocalTeamId(IClientBattleFacade facade)
        {
            try
            {
                if (facade == null || facade.Teams == null)
                {
                    return -1;
                }

                int localTeamId = facade.Teams.LocalTeamIdInControl;
                if (localTeamId >= 0)
                {
                    return localTeamId;
                }

                return facade.Teams.Current != null ? facade.Teams.Current.Id : -1;
            }
            catch (Exception exception)
            {
                LogOnce.Warn("BattleFacadeState.LocalTeamId", exception);
                return -1;
            }
        }

        /// <summary>The troop whose turn it is; -1 when no troop is acting.</summary>
        public static int CurrentTroopId(IClientBattleFacade facade)
        {
            return facade != null && facade.Troops != null && facade.Troops.Current != null
                ? facade.Troops.Current.Id
                : -1;
        }

        public static int CurrentRound(IClientBattleFacade facade)
        {
            return facade != null && facade.Queue != null ? facade.Queue.CurrentRound : 0;
        }
    }
}
