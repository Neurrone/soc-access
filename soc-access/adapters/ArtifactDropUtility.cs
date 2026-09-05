using System;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;

namespace SongsOfConquestAccess.Adapters
{
    internal enum DropResult
    {
        Invalid,
        Dropped,
        DeniedWithFeedback,
        DeniedWithoutFeedback
    }

    internal static class ArtifactDropUtility
    {
        private const byte NativeFeedbackErrorCode = 10;

        public static DropResult DropInventoryArtifact(
            IClientAdventureFacade facade,
            InventorySlotInfo source,
            InventorySlotInfo target,
            string logContext)
        {
            InventoryArtifactMovable movable = source != null ? source.Movable : null;
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
