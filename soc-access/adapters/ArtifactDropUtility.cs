using System;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;

namespace SongsOfConquestAccess.Adapters
{
    public enum DropResult
    {
        Invalid,
        Dropped,
        DeniedWithFeedback,
        DeniedWithoutFeedback
    }

    public static class ArtifactDropUtility
    {
        private const byte NativeFeedbackErrorCode = 10;

        public static DropResult DropInventoryArtifact(
            IClientAdventureFacade facade,
            InventorySlotInfo source,
            InventorySlotInfo target,
            string logContext)
        {
            return DropArtifact(facade, source != null ? source.Movable : null, target, logContext);
        }

        /// <summary>Whether the game would take this drop, asked without doing anything: the same
        /// validation <see cref="DropArtifact"/> runs, chosen the same way - its own rearrange check
        /// within one owner, and its give check across two, which is the branch a trade takes.
        /// </summary>
        public static bool CanDrop(
            IClientAdventureFacade facade,
            InventoryArtifactMovable movable,
            InventorySlotInfo target,
            string logContext)
        {
            InventoryHUDSlot targetSlot = target != null ? target.NativeSlot : null;
            if (facade == null || movable == null || movable.State == null || targetSlot == null || targetSlot.HudParent == null)
            {
                return false;
            }

            try
            {
                int targetOwnerId = targetSlot.HudParent.OwnerId;
                return movable.State.OwnerId == targetOwnerId
                    ? facade.Commands.CanRearrangeArtifact(movable.State.Id, targetSlot.Slot, target.PositionIndex).success
                    : facade.Commands.CanGiveArtifact(targetOwnerId, movable.State.Id, targetSlot.Slot, target.PositionIndex).success;
            }
            catch (Exception ex)
            {
                string prefix = string.IsNullOrWhiteSpace(logContext) ? "Artifact grid drop" : logContext;
                SocAccessMod.Instance?.LogWarning(prefix + " could not ask whether an artifact fits: " + ex.Message);
                return false;
            }
        }

        /// <summary>The same drop where the artifact is in hand rather than read off the slot it came
        /// from - the keyboard's carry, which holds the movable itself and may have walked away from
        /// the slot that gave it.</summary>
        public static DropResult DropArtifact(
            IClientAdventureFacade facade,
            InventoryArtifactMovable movable,
            InventorySlotInfo target,
            string logContext)
        {
            InventoryHUDSlot targetSlot = target != null ? target.NativeSlot : null;
            if (facade == null || movable == null || movable.State == null || targetSlot == null || targetSlot.HudParent == null)
            {
                return DropResult.Invalid;
            }

            try
            {
                int targetOwnerId = targetSlot.HudParent.OwnerId;
                var validation = movable.State.OwnerId == targetOwnerId
                    ? facade.Commands.CanRearrangeArtifact(movable.State.Id, targetSlot.Slot, target.PositionIndex)
                    : facade.Commands.CanGiveArtifact(targetOwnerId, movable.State.Id, targetSlot.Slot, target.PositionIndex);

                if (validation.success)
                {
                    targetSlot.HudParent.ArtifactDroppedOnSlot(movable, targetSlot, target.PositionIndex);
                    return DropResult.Dropped;
                }

                if (validation.errorCode == NativeFeedbackErrorCode)
                {
                    targetSlot.HudParent.ArtifactDroppedOnSlot(movable, targetSlot, target.PositionIndex);
                    return DropResult.DeniedWithFeedback;
                }

                return DropResult.DeniedWithoutFeedback;
            }
            catch (Exception ex)
            {
                string prefix = string.IsNullOrWhiteSpace(logContext) ? "Artifact grid drop" : logContext;
                SocAccessMod.Instance?.LogWarning(prefix + " failed: " + ex.Message);
                return DropResult.Invalid;
            }
        }
    }
}
