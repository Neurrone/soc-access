using System.Collections.Generic;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Common.Details;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// A wielder's artifacts as every screen that draws an <c>InventoryHUD</c> reads them - the
    /// wielder sheet and the artifact market today, the trade next. One interface so the slot rows
    /// are declared in one place (<c>ui/ArtifactSlotNodes.cs</c>) rather than copied per screen.
    ///
    /// The two clicks are named after the MOUSE BUTTON rather than after what they do, because what
    /// they do differs by screen and is the game's answer, not the mod's: on the sheet the left click
    /// is inert and the right click equips, and in the market the left click selects the artifact for
    /// sale and Ctrl and the right click sell it. Each screen says which sentences it puts on a slot.
    /// </summary>
    public interface IArtifactSlots
    {
        /// <summary>The game's own caption over the equipment column.</summary>
        string EquipmentLabel { get; }

        /// <summary>The game's own caption over the backpack.</summary>
        string InventoryLabel { get; }

        /// <summary>The equipment slots in the order the screen DRAWS them.</summary>
        IReadOnlyList<InventorySlotInfo> GetEquipmentSlots();

        /// <summary>Every drawn backpack cell, filled or not, in drawn order.</summary>
        IReadOnlyList<InventorySlotInfo> GetBackpackSlots();

        /// <summary>Whether the artifact in the main hand takes both hands, which is what makes the
        /// game draw a ghost of it in the off hand.</summary>
        bool IsMainHandTwoHanded();

        /// <summary>Whether an artifact fits the off hand ALONE - the one case the game's own
        /// right-click resolution sends to the off hand rather than the main one.</summary>
        bool IsOffHandOnlyArtifact(InventoryArtifactMovable movable);

        /// <summary>The game's own name for the two-handed slot ("Both Hands").</summary>
        string BothHandsSlotName { get; }

        /// <summary>Whether the game would accept this artifact here, asked without doing
        /// anything.</summary>
        bool CanRearrangeArtifactTo(InventoryArtifactMovable movable, InventorySlotInfo target);

        /// <summary>Put an artifact down on a slot, through the game's own check and its own
        /// move.</summary>
        DropResult DropArtifact(InventoryArtifactMovable movable, InventorySlotInfo target);

        /// <summary>The game's own notification for a rearrangement its Command skill blocks.
        /// </summary>
        string RearrangeRefusalText { get; }

        /// <summary>The artifact's LEFT click, through the button the game hangs its own handler on.
        /// </summary>
        bool LeftClickArtifact(InventorySlotInfo slot);

        /// <summary>The artifact's RIGHT click, through the same button.</summary>
        bool RightClickArtifact(InventorySlotInfo slot);

        /// <summary>What a right click on this artifact does, as the GAME decides it.</summary>
        ArtifactDetails.EquipInstruction GetArtifactInstruction(InventorySlotInfo slot);

        /// <summary>The game's own text for the auto-arrange instruction.</summary>
        string AutoArrangeText { get; }

        /// <summary>Auto-arrange, the game's own middle click.</summary>
        bool AutoArrangeArtifacts();
    }
}
