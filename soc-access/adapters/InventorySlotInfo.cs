using System;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class InventorySlotInfo
    {
        private readonly Action _focusNative;

        public InventorySlotInfo(
            int ownerId,
            string ownerName,
            InventorySlot slot,
            int positionIndex,
            bool isBackpackSlot,
            string slotName,
            string inventoryName,
            string artifactName,
            InventoryArtifactMovable movable,
            InventoryHUDSlot nativeSlot,
            Tooltip tooltip,
            Action focusNative)
        {
            OwnerId = ownerId;
            OwnerName = ownerName ?? string.Empty;
            Slot = slot;
            PositionIndex = positionIndex;
            IsBackpackSlot = isBackpackSlot;
            SlotName = slotName ?? string.Empty;
            InventoryName = inventoryName ?? string.Empty;
            ArtifactName = artifactName ?? string.Empty;
            Movable = movable;
            NativeSlot = nativeSlot;
            Tooltip = tooltip;
            _focusNative = focusNative;
        }

        public int OwnerId { get; private set; }
        public string OwnerName { get; private set; }
        public InventorySlot Slot { get; private set; }
        public int PositionIndex { get; private set; }
        public bool IsBackpackSlot { get; private set; }
        public string SlotName { get; private set; }
        public string InventoryName { get; private set; }
        public string ArtifactName { get; private set; }
        public InventoryArtifactMovable Movable { get; private set; }
        public InventoryHUDSlot NativeSlot { get; private set; }
        public Tooltip Tooltip { get; private set; }

        public bool IsEmpty
        {
            get { return Movable == null && string.IsNullOrWhiteSpace(ArtifactName); }
        }

        public bool CanDrag
        {
            get { return Movable != null; }
        }

        public void FocusNative()
        {
            _focusNative?.Invoke();
        }
    }
}
