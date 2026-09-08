using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The map and battle tiles' USAGE HINTS: what the game's own tooltip said a click here would do,
    /// said as the mod's keyboard sentence instead - "Enter visits", "Backslash attacks".
    ///
    /// The game draws those as instruction rows in the tile's tooltip; the adapter strips the rows
    /// (they name a mouse gesture nobody here is making) and reports the kind. This turns the kind
    /// into the hint on the key that performs it - the primary row on left click, the secondary on
    /// right click - so the wording follows the player's bindings like every other hint.
    ///
    /// One hint is declared per kind and gated on the live tooltip, because the vtable is built once
    /// per frame while the cursor moves from tile to tile inside it. <paramref name="tooltip"/> is
    /// therefore asked once per hint per buffer read and must be the screen's CACHED tile tooltip,
    /// never a fresh adapter read.
    /// </summary>
    public static class TileInstructionHints
    {
        /// <summary>What each kind is called on the key that performs it.</summary>
        private static readonly Entry[] Table =
        {
            new Entry(TileInstruction.Select, ModStrings.Screens.TileSelectHint),
            new Entry(TileInstruction.Visit, ModStrings.Screens.TileVisitHint),
            new Entry(TileInstruction.Trade, ModStrings.Screens.TileTradeHint),
            new Entry(TileInstruction.Repair, ModStrings.Screens.TileRepairHint),
            new Entry(TileInstruction.Interact, ModStrings.Screens.TileInteractHint),
            new Entry(TileInstruction.Pillage, ModStrings.Screens.TilePillageHint),
            new Entry(TileInstruction.Attack, ModStrings.Screens.TileAttackHint),
            new Entry(TileInstruction.Pickup, ModStrings.Screens.TilePickupHint),
            new Entry(TileInstruction.Claim, ModStrings.Screens.TileClaimHint),
            new Entry(TileInstruction.Teleport, ModStrings.Screens.TileTeleportHint),
            new Entry(TileInstruction.Move, ModStrings.Screens.TileMoveHint),
        };

        public static void Add(NodeVtable vtable, Func<Tooltip> tooltip, Func<bool> when = null)
        {
            if (vtable == null || tooltip == null)
            {
                return;
            }

            for (int i = 0; i < Table.Length; i++)
            {
                Entry entry = Table[i];
                NodeHints.Add(
                    vtable,
                    entry.Template,
                    AccessibilityActions.UiLeftClick.Key,
                    0,
                    () => Says(tooltip, when, entry.Kind, primary: true));
            }

            for (int i = 0; i < Table.Length; i++)
            {
                Entry entry = Table[i];
                NodeHints.Add(
                    vtable,
                    entry.Template,
                    AccessibilityActions.UiRightClick.Key,
                    0,
                    () => Says(tooltip, when, entry.Kind, primary: false));
            }
        }

        private static bool Says(Func<Tooltip> tooltip, Func<bool> when, TileInstruction kind, bool primary)
        {
            if (when != null && !when())
            {
                return false;
            }

            Tooltip it = tooltip();
            if (it == null)
            {
                return false;
            }

            return (primary ? it.PrimaryInstruction : it.SecondaryInstruction) == kind;
        }

        private struct Entry
        {
            public Entry(TileInstruction kind, ModString template)
            {
                Kind = kind;
                Template = template;
            }

            public readonly TileInstruction Kind;
            public readonly ModString Template;
        }
    }
}
