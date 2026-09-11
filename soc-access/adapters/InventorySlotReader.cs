using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// One wielder's <c>InventoryHUD</c> as the accessible tree reads it - the equipment column, the
    /// backpack, and the gestures the game gives an artifact. The wielder sheet, the artifact market
    /// and each half of a trade draw the SAME HUD, so each of them owns one of these and answers
    /// <see cref="IArtifactSlots"/> out of it rather than reading the HUD itself.
    ///
    /// What differs between the three menus is the game's, not the mod's, and is what this takes as
    /// arguments: which rows the artifact tooltip ends with (a trade offers the other backpack where
    /// a shop offers a sale), and whether the menu listens to the inventory's left click at all -
    /// only the market does (<c>InventoryHUD.OnLeftClicked</c>), and its own answer takes the
    /// artifact's tooltip down with it, so the cell is selected again afterwards.
    ///
    /// The HUD and the wielder are read through functions because a trade's side reads them off the
    /// menu's settings; everything else is fixed for the menu instance, and so is this reader, which
    /// the adapter owns and which therefore lives exactly as long as the menu does.
    /// </summary>
    public sealed class InventorySlotReader : IArtifactSlots
    {
        /// <summary>The rows <c>ArtifactDetails</c> writes for a mouse where Ctrl and the right click
        /// SELL the artifact - the wielder sheet's and the artifact market's tooltips.</summary>
        public static readonly string[] MouseInstructionKeys =
        {
            "Adventure/TooltipInstruction/Equip",
            "Adventure/TooltipInstruction/Unequip",
            "Adventure/TooltipInstruction/Sell",
            "Adventure/TooltipInstruction/Destroy",
            "Adventure/TooltipInstruction/Destroy.Gamepad",
            "Adventure/TooltipInstruction/Drop",
            "Adventure/TooltipInstruction/Drop.Gamepad",
            "Adventure/TooltipInstruction/AutoArrange",
            "Adventure/TooltipInstruction/AutoArrange.Gamepad"
        };

        /// <summary>The same rows in a trade, where the right click hands the artifact to the other
        /// backpack and nothing is for sale.</summary>
        public static readonly string[] TradeMouseInstructionKeys =
        {
            "Adventure/TooltipInstruction/Trade",
            "Adventure/TooltipInstruction/Equip",
            "Adventure/TooltipInstruction/Unequip",
            "Adventure/TooltipInstruction/Destroy",
            "Adventure/TooltipInstruction/Destroy.Gamepad",
            "Adventure/TooltipInstruction/Drop",
            "Adventure/TooltipInstruction/Drop.Gamepad",
            "Adventure/TooltipInstruction/AutoArrange",
            "Adventure/TooltipInstruction/AutoArrange.Gamepad"
        };

        private static readonly FieldInfo InventoryArtifactMapField = AccessTools.Field(typeof(InventoryHUD), "_artifactStateToGOMap");
        private static readonly FieldInfo MovableButtonField = AccessTools.Field(typeof(InventoryArtifactMovable), "_button");

        // The wielder's two artifact lists, kept while the game's own inventory is unchanged.
        // Building them costs a localized inventory caption and slot name per drawn slot, a
        // rarity-formatted artifact name per artifact, and the instruction lines the tooltip strips
        // per occupied slot. The key is read off the game every frame (the wielder the menu is
        // showing, the HUD, the pooled cells and what is in each of them), so an equip, an unequip,
        // a move, a sale, a trade and an auto-arrange all rebuild with nothing having to say so
        // (AGENTS.md, Screen Resolution).
        private readonly SlotSnapshot<InventorySlotInfo> _equipment = new SlotSnapshot<InventorySlotInfo>();

        private readonly SlotSnapshot<InventorySlotInfo> _backpack = new SlotSnapshot<InventorySlotInfo>();

        private readonly Func<InventoryHUD> _inventory;
        private readonly Func<int> _ownerId;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private readonly IArtifactLookup _artifactLookup;
        private readonly string _logContext;
        private readonly string[] _instructionKeys;
        private readonly bool _answersLeftClick;

        // The rows the game writes for a mouse. They are the same for every slot and for the menu's
        // whole life, so they are looked up once instead of nine times a slot a frame.
        private List<string> _mouseInstructionLines;

        public InventorySlotReader(
            Func<InventoryHUD> inventory,
            Func<int> ownerId,
            IClientAdventureFacade facade,
            ILocalizationHandler localization,
            IArtifactLookup artifactLookup,
            string logContext,
            string[] instructionKeys,
            bool answersLeftClick = false)
        {
            _inventory = inventory;
            _ownerId = ownerId;
            _facade = facade;
            _localization = localization;
            _artifactLookup = artifactLookup;
            _logContext = logContext;
            _instructionKeys = instructionKeys ?? MouseInstructionKeys;
            _answersLeftClick = answersLeftClick;
        }

        public string EquipmentLabel
        {
            get { return SpokenText.Get(_localization, "Common/CommanderInventory/Equipment", string.Empty); }
        }

        public string InventoryLabel
        {
            get { return GetInventoryLabel(); }
        }

        public IReadOnlyList<InventorySlotInfo> GetEquipmentSlots()
        {
            InventoryHUD inventory = Inventory;
            InventorySlot[] slots = InventorySlotInfo.DrawnEquipmentSlots;
            int ownerId = OwnerId;
            List<int> key = _equipment.BeginKey();
            key.Add(ownerId);
            key.Add(InstanceId(inventory));
            for (int i = 0; i < slots.Length; i++)
            {
                InventoryHUDSlot keySlot = inventory != null ? inventory.GetSlot(slots[i]) : null;
                IArtifactState keyArtifact = GetDisplayArtifactForEquipmentSlot(slots[i]);
                key.Add(InstanceId(keySlot));
                key.Add(InstanceId(keySlot != null ? keySlot.TryGetArtifact(0) : null));
                key.Add(keyArtifact != null ? keyArtifact.Id : 0);
                key.Add(keyArtifact != null ? (int)keyArtifact.EquippedInSlot : -1);
                key.Add(keyArtifact != null ? keyArtifact.PositionIndex : -1);
            }

            IReadOnlyList<InventorySlotInfo> unchanged = _equipment.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            string ownerName = GetOwnerName(ownerId);
            string inventoryName = GetInventoryLabel();
            for (int i = 0; i < slots.Length; i++)
            {
                InventorySlot slot = slots[i];
                InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(slot) : null;
                IArtifactState artifact = GetDisplayArtifactForEquipmentSlot(slot);
                bool displayOnly = IsDisplayOnlyEquipmentArtifact(slot, artifact);
                InventoryArtifactMovable nativeMovable = nativeSlot != null ? nativeSlot.TryGetArtifact(0) : null;
                InventoryArtifactMovable artifactMovable = nativeMovable ?? GetArtifactMovable(artifact);
                InventoryArtifactMovable movable = displayOnly ? null : artifactMovable;
                InventoryHUDSlot capturedNativeSlot = nativeSlot;
                InventoryArtifactMovable capturedMovable = movable;
                Selectable tooltipSelectable = movable != null
                    ? movable.GetSelectable()
                    : displayOnly && artifactMovable != null
                        ? artifactMovable.GetSelectable()
                        : GetEquipmentSlotSelectable(capturedNativeSlot);
                slotsInfo.Add(new InventorySlotInfo(
                    ownerId,
                    ownerName,
                    slot,
                    0,
                    isBackpackSlot: false,
                    GetInventorySlotName(slot.ToString()),
                    inventoryName,
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildArtifactTooltip(artifact, artifactMovable, tooltipSelectable),
                    () => SelectInventoryCell(capturedNativeSlot, capturedMovable, 0)));
            }

            return _equipment.Keep(slotsInfo);
        }

        public IReadOnlyList<InventorySlotInfo> GetBackpackSlots()
        {
            InventoryHUD inventory = Inventory;
            InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(InventorySlot.None) : null;
            int ownerId = OwnerId;
            int cellCount = nativeSlot != null ? nativeSlot.CellsCount : 0;
            List<int> key = _backpack.BeginKey();
            key.Add(ownerId);
            key.Add(InstanceId(nativeSlot));
            for (int i = 0; i < cellCount; i++)
            {
                InventoryArtifactMovable keyMovable = nativeSlot.TryGetArtifact(i);
                key.Add(InstanceId(keyMovable));
                key.Add(keyMovable != null && keyMovable.State != null ? keyMovable.State.Id : 0);
            }

            IReadOnlyList<InventorySlotInfo> unchanged = _backpack.Unchanged();
            if (unchanged != null)
            {
                return unchanged;
            }

            List<InventorySlotInfo> slotsInfo = new List<InventorySlotInfo>();
            string ownerName = GetOwnerName(ownerId);
            string inventoryName = GetInventoryLabel();

            // What the backpack holds is read off the DRAWN cell, which already knows, rather than
            // out of the owner's whole artifact list: the same source the key above is read from,
            // and the two must agree. They did not while the list came from the facade - a move
            // within the backpack reaches the HUD a frame before it reaches the facade, so a key
            // that had already changed froze a list built from the position the artifact had just
            // left (measured in-game 2026-09-10: HUD cell 5, facade position 9, for one frame).
            for (int i = 0; i < cellCount; i++)
            {
                InventoryArtifactMovable movable = nativeSlot.TryGetArtifact(i);
                IArtifactState artifact = movable != null ? movable.State : null;
                int capturedIndex = i;
                InventoryArtifactMovable capturedMovable = movable;
                slotsInfo.Add(new InventorySlotInfo(
                    ownerId,
                    ownerName,
                    InventorySlot.None,
                    i,
                    isBackpackSlot: true,
                    string.Empty,
                    inventoryName,
                    artifact != null ? GetArtifactName(artifact) : string.Empty,
                    movable,
                    nativeSlot,
                    BuildArtifactTooltip(artifact, movable, movable != null ? movable.GetSelectable() : GetInventorySlotSelectable(nativeSlot, i)),
                    () => SelectInventoryCell(nativeSlot, capturedMovable, capturedIndex)));
            }

            return _backpack.Keep(slotsInfo);
        }

        // ---- the gestures the game gives an artifact ----

        /// <summary>Put an artifact down on a slot, through the game's own check and its own move.
        /// The utility branches on the owner, so a drop on the OTHER wielder of a trade goes down the
        /// game's give path rather than its rearrange path.</summary>
        public DropResult DropArtifact(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return ArtifactDropUtility.DropArtifact(_facade, movable, target, _logContext + " artifact drop");
        }

        /// <summary>Whether the game would accept this artifact here, asked without doing anything -
        /// the same two checks its own drop makes, chosen the same way.</summary>
        public bool CanRearrangeArtifactTo(InventoryArtifactMovable movable, InventorySlotInfo target)
        {
            return ArtifactDropUtility.CanDrop(_facade, movable, target, _logContext + " artifact drop check");
        }

        /// <summary>The game's own notification for a rearrangement its Command skill blocks - what it
        /// shows itself when the drop is refused with error code 10.</summary>
        public string RearrangeRefusalText
        {
            get { return SpokenText.Get(_localization, "Common/CommanderInventory/RearrangeArtifact/CannotRearrangeBecauseOfCommand", string.Empty); }
        }

        /// <summary>Whether the game does anything with a plain left click here: only the artifact
        /// market subscribes to the inventory's left click, and only a slot with an artifact in it can
        /// be clicked at all.</summary>
        public bool AnswersLeftClick(InventorySlotInfo slot)
        {
            return _answersLeftClick && slot != null && slot.Movable != null;
        }

        /// <summary>The artifact's LEFT click, through the button the game hangs its own handler on:
        /// inert on the wielder sheet and in a trade, and with Ctrl physically held the game's own
        /// drop on the ground. Where the menu answers the click, its answer takes the artifact's
        /// tooltip down with it (the market rebuilds its band and the game stops drawing the hover),
        /// so the cell is selected again afterwards: the cursor has not moved, and the tooltip the
        /// player was reading comes back.</summary>
        public bool LeftClickArtifact(InventorySlotInfo slot)
        {
            bool clicked = NativeSelectionUtility.Click(GetMovableButton(slot));
            if (clicked && _answersLeftClick && slot != null)
            {
                slot.FocusNative();
            }

            return clicked;
        }

        /// <summary>The artifact's RIGHT click, through the same button: equip, unequip or use, and
        /// with Ctrl physically held the game's own destroy - which in a shop is a sale instead
        /// (<c>InventoryArtifactMovable.HandleRightClick</c> branches on
        /// <c>InventoryHUD.IsArtifactShopInventory</c>).</summary>
        public bool RightClickArtifact(InventorySlotInfo slot)
        {
            return NativeSelectionUtility.RightClick(GetMovableButton(slot));
        }

        /// <summary>What a right click on this artifact does, as the GAME decides it in
        /// <c>InventoryArtifactMovable.GetDetails</c>: an artifact whose definition carries an action
        /// is used, and every other one is equipped or unequipped by where it currently is.</summary>
        public ArtifactDetails.EquipInstruction GetArtifactInstruction(InventorySlotInfo slot)
        {
            InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
            IArtifactState artifact = movable != null ? movable.State : null;
            if (artifact == null)
            {
                return ArtifactDetails.EquipInstruction.None;
            }

            IArtifactDataDefinition definition = _artifactLookup != null ? _artifactLookup.GetDefinition(artifact.Type) : null;
            if (definition != null && definition.Action != null)
            {
                return ArtifactDetails.EquipInstruction.Use;
            }

            return artifact.IsEquipped
                ? ArtifactDetails.EquipInstruction.Unequip
                : ArtifactDetails.EquipInstruction.Equip;
        }

        /// <summary>Whether the artifact in the main hand takes BOTH hands - the definition's own slot
        /// (<c>IArtifactLookup.GetSlot</c>), which is what makes the game draw a ghost of it in the off
        /// hand. Read off the drawn main-hand cell, as the equipment rows are.</summary>
        public bool IsMainHandTwoHanded()
        {
            return GetMainHandTwoHander() != null;
        }

        /// <summary>Whether an artifact fits the off hand ALONE - the one case the game's own
        /// right-click resolution (<c>InventoryHUD.GetSlot(ArtifactSlot)</c>) sends to the off hand
        /// rather than the main one.</summary>
        public bool IsOffHandOnlyArtifact(InventoryArtifactMovable movable)
        {
            return movable != null
                && movable.State != null
                && _artifactLookup != null
                && _artifactLookup.GetSlot(movable.State.Type) == ArtifactSlot.OffHand;
        }

        /// <summary>The game's own name for the two-handed slot ("Both Hands").</summary>
        public string BothHandsSlotName
        {
            get { return GetInventorySlotName(ArtifactSlot.BothHands.ToString()); }
        }

        /// <summary>The game's own text for the auto-arrange instruction, as it draws it in an
        /// artifact's tooltip.</summary>
        public string AutoArrangeText
        {
            get { return SpokenText.Get(_localization, "Adventure/TooltipInstruction/AutoArrange", string.Empty); }
        }

        /// <summary>Auto-arrange, the game's own middle click (<c>InventoryHUD.AutoArrangeArtifacts</c>).
        /// Its second half only remembers which cell to re-select afterwards and needs an artifact to
        /// remember, so with nothing in the inventory the command it runs is called on its own.</summary>
        public bool AutoArrangeArtifacts()
        {
            InventoryHUD inventory = Inventory;
            if (inventory == null)
            {
                return false;
            }

            InventoryArtifactMovable anyArtifact = FirstArtifactMovable();
            if (anyArtifact != null)
            {
                inventory.AutoArrangeArtifacts(anyArtifact);
                return true;
            }

            int ownerId = OwnerId;
            if (_facade == null || ownerId < 0)
            {
                return false;
            }

            _facade.Commands.EquipBestArtifacts(ownerId);
            return true;
        }

        // ---- what the drawn cells hold ----

        private InventoryHUD Inventory
        {
            get { return _inventory != null ? _inventory() : null; }
        }

        private int OwnerId
        {
            get { return _ownerId != null ? _ownerId() : -1; }
        }

        /// <summary>The artifact an equipment slot DRAWS, read off the drawn cell rather than out of
        /// the adventure's whole artifact list. <c>ArtifactFacade.GetForOwner</c> is a Where over
        /// EVERY artifact in the game, and it was asked nine times a frame here and a tenth in
        /// <see cref="IsMainHandTwoHanded"/>; the cell already holds the answer
        /// (<c>InventoryHUDSlot.TryGetArtifact(0)</c>), and the movable's <c>State</c> is the same
        /// <c>IArtifactState</c> the facade would have found.
        ///
        /// The off hand is the one slot with no movable of its own to read: when the main hand holds
        /// a two-hander the game draws a GHOST there from the main hand's own movable
        /// (<c>InventoryHUD.Refresh</c> calls <c>AddTintedArtifactImage</c> with the main-hand state
        /// when <c>_lookup.GetSlot</c> answers <c>BothHands</c>), and that is what is answered here.
        ///
        /// The drawn cell lags the facade by at most one frame after a move - the HUD refreshes in
        /// its own Update - so for that frame this answers what the player is looking at, which is
        /// what the graph is for.</summary>
        private IArtifactState GetDisplayArtifactForEquipmentSlot(InventorySlot slot)
        {
            InventoryArtifactMovable movable = GetEquippedMovable(slot);
            if (movable != null && movable.State != null)
            {
                return movable.State;
            }

            return slot == InventorySlot.OffHand ? GetMainHandTwoHander() : null;
        }

        /// <summary>What the drawn equipment cell holds, or null for an empty one.</summary>
        private InventoryArtifactMovable GetEquippedMovable(InventorySlot slot)
        {
            InventoryHUD inventory = Inventory;
            InventoryHUDSlot nativeSlot = inventory != null ? inventory.GetSlot(slot) : null;
            return nativeSlot != null ? nativeSlot.TryGetArtifact(0) : null;
        }

        /// <summary>The main hand's artifact when it takes both hands, which is what the game draws
        /// the off hand's ghost from; null otherwise.</summary>
        private IArtifactState GetMainHandTwoHander()
        {
            InventoryArtifactMovable movable = GetEquippedMovable(InventorySlot.MainHand);
            IArtifactState artifact = movable != null ? movable.State : null;
            return artifact != null
                && _artifactLookup != null
                && _artifactLookup.GetSlot(artifact.Type) == ArtifactSlot.BothHands
                ? artifact
                : null;
        }

        private static bool IsDisplayOnlyEquipmentArtifact(InventorySlot slot, IArtifactState artifact)
        {
            return slot == InventorySlot.OffHand
                && artifact != null
                && artifact.EquippedInSlot == InventorySlot.MainHand;
        }

        /// <summary>The movable the HUD drew for an artifact whose own cell is empty - the off hand's
        /// ghost, which is the main hand's movable.</summary>
        private InventoryArtifactMovable GetArtifactMovable(IArtifactState artifact)
        {
            InventoryHUD inventory = Inventory;
            if (artifact == null || InventoryArtifactMapField == null || inventory == null)
            {
                return null;
            }

            IDictionary artifactMap = InventoryArtifactMapField.GetValue(inventory) as IDictionary;
            if (artifactMap == null || !artifactMap.Contains(artifact))
            {
                return null;
            }

            return artifactMap[artifact] as InventoryArtifactMovable;
        }

        private InventoryArtifactMovable FirstArtifactMovable()
        {
            InventoryHUD inventory = Inventory;
            IDictionary artifactMap = InventoryArtifactMapField != null && inventory != null
                ? InventoryArtifactMapField.GetValue(inventory) as IDictionary
                : null;
            if (artifactMap == null)
            {
                return null;
            }

            foreach (object movable in artifactMap.Values)
            {
                InventoryArtifactMovable artifact = movable as InventoryArtifactMovable;
                if (artifact != null)
                {
                    return artifact;
                }
            }

            return null;
        }

        private void SelectInventoryCell(InventoryHUDSlot nativeSlot, InventoryArtifactMovable movable, int positionIndex)
        {
            if (movable != null)
            {
                NativeSelectionUtility.Select(movable.GetSelectable());
                return;
            }

            Selectable selectable = GetInventorySlotSelectable(nativeSlot, positionIndex);
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        private static Selectable GetEquipmentSlotSelectable(InventoryHUDSlot nativeSlot)
        {
            return nativeSlot != null ? nativeSlot.GetFirstSelectable() : null;
        }

        private static Selectable GetInventorySlotSelectable(InventoryHUDSlot nativeSlot, int positionIndex)
        {
            InventoryHUDGridEntry entry = nativeSlot != null ? nativeSlot.TryGetEntry(positionIndex) : null;
            return entry != null ? (Selectable)entry : null;
        }

        private static IUIButton GetMovableButton(InventorySlotInfo slot)
        {
            InventoryArtifactMovable movable = slot != null ? slot.Movable : null;
            return movable != null && MovableButtonField != null
                ? MovableButtonField.GetValue(movable) as IUIButton
                : null;
        }

        /// <summary>What a game object is, as a number a key can hold: zero for one the game has not
        /// made or has destroyed, which Unity's own null answers for.</summary>
        private static int InstanceId(Component component)
        {
            return component == null ? 0 : component.GetInstanceID();
        }

        // ---- the words ----

        /// <summary>
        /// An artifact's own tooltip, without the lines that tell a MOUSE what to press.
        ///
        /// <c>ArtifactDetails</c> ends its tooltip with a row per gesture ("&lt;rmb&gt; Equip",
        /// "&lt;hl&gt;CTRL&lt;/hl&gt; + &lt;rmb&gt; Destroy", the drop and the auto-arrange), and the
        /// keyboard gets those same gestures as usage hints on the slot itself, so the rows would be
        /// said twice. They are removed by the localized text the game DREW them from rather than by
        /// English, so a row this mod does not know about is left where it is and the player still
        /// hears that something may be available.
        /// </summary>
        private Tooltip BuildArtifactTooltip(IArtifactState artifact, InventoryArtifactMovable movable, Selectable selectable)
        {
            Tooltip tooltip = Tooltip.ForComponent(selectable, _localization);
            if (tooltip == null || artifact == null || movable == null || _localization == null)
            {
                return tooltip;
            }

            List<string> instructionLines = GetMouseInstructionLines();
            return new Tooltip(
                () => TooltipLines.Without(tooltip.TextLines, instructionLines),
                tooltip.VisualMetadata,
                isLong: () => tooltip.IsLong);
        }

        private List<string> GetMouseInstructionLines()
        {
            if (_mouseInstructionLines != null)
            {
                return _mouseInstructionLines;
            }

            List<string> lines = new List<string>();
            for (int i = 0; i < _instructionKeys.Length; i++)
            {
                string line = _localization != null ? _localization.GetText(_instructionKeys[i]) : string.Empty;
                if (!string.IsNullOrWhiteSpace(line) && !lines.Contains(line))
                {
                    lines.Add(line);
                }
            }

            _mouseInstructionLines = lines;
            return lines;
        }

        private string GetArtifactName(IArtifactState artifact)
        {
            if (artifact == null)
            {
                return string.Empty;
            }

            try
            {
                return ArtifactSpeechFormatter.FormatName(artifact, _artifactLookup, _localization);
            }
            catch (Exception ex)
            {
                SocAccessMod.Instance?.LogWarning(_logContext + " could not get artifact name: " + ex.Message);
                return _artifactLookup != null ? _artifactLookup.GetLocalizedName(artifact.Type) : artifact.Type.ToString();
            }
        }

        private string GetOwnerName(int ownerId)
        {
            string name = ownerId >= 0 && _facade != null ? _facade.Commanders.GetName(ownerId) : string.Empty;
            return SpokenLines.Clean(name);
        }

        private string GetInventoryLabel()
        {
            return SpokenText.Get(_localization, "Common/CommanderInventory/Inventory", "Inventory");
        }

        /// <summary>The game's own name for a slot. The localization answers a key it does not hold
        /// with the key itself, so an echo is no name at all and the slot's own words are said
        /// instead.</summary>
        private string GetInventorySlotName(string slotName)
        {
            string key = "InventorySlots/" + slotName;
            string text = _localization != null ? _localization.GetText(key) : string.Empty;
            return string.IsNullOrWhiteSpace(text) || text == key
                ? FormatSlotName(slotName)
                : SpokenLines.Clean(text);
        }

        /// <summary>A slot's own words where the game has none for it: the name the game gives the
        /// slot in its own code, split where it changes case ("MainHand" reads "main hand").</summary>
        public static string FormatSlotName(string value)
        {
            string formatted = string.Empty;
            for (int i = 0; i < value.Length; i++)
            {
                char c = value[i];
                if (i > 0 && char.IsUpper(c))
                {
                    formatted += " ";
                }

                formatted += char.ToLowerInvariant(c);
            }

            return formatted;
        }
    }
}
