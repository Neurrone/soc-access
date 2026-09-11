using System.Collections.Generic;
using SongsOfConquestAccess.Localization;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// THE MOD'S OWN GESTURES, ARRANGED FOR THE KEYBINDS TAB.
    ///
    /// Which rebindable action belongs to which named region, and the region's caption.
    /// <see cref="AccessibilityActions"/> stays a flat vocabulary; the accessibility grouping and the
    /// region wording (localized <see cref="ModStrings"/>) live here, above it, per the adapter/screen
    /// split.
    ///
    /// The graph engine's navigation spine (the arrows, the stops, the regions, the clicks) is the
    /// first block, as it is in Endless Space 2 Access, so the keys a player meets first are the
    /// first they can see and change. Only the Ctrl+digit quick-split chords are held out, since
    /// their multi-key semantics a single-key capture cannot express. Everything a group lists is
    /// captured as one <see cref="KeyboardBinding"/>. The order is the owner's (2026-09-11): cursor,
    /// review buffer, adventure map, scanner, hex grid, combat, and the forty bookmark rows last.
    /// </summary>
    public static class ModGestureCatalog
    {
        public sealed class Group
        {
            public Group(ModString caption, IReadOnlyList<InputAction> actions)
            {
                Caption = caption;
                Actions = actions;
            }

            public ModString Caption { get; private set; }

            public IReadOnlyList<InputAction> Actions { get; private set; }
        }

        private static readonly Group[] _groups = Build();

        public static IReadOnlyList<Group> Groups
        {
            get { return _groups; }
        }

        /// <summary>Whether this action is one the Keybinds tab offers for rebinding - the set the
        /// persistence layer binds a config entry for.</summary>
        public static bool IsRebindable(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            for (int g = 0; g < _groups.Length; g++)
            {
                IReadOnlyList<InputAction> actions = _groups[g].Actions;
                for (int i = 0; i < actions.Count; i++)
                {
                    if (ReferenceEquals(actions[i], action))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        /// <summary>Every rebindable action, flattened - what ModSettings iterates to bind and
        /// re-apply overrides.</summary>
        public static IEnumerable<InputAction> RebindableActions()
        {
            for (int g = 0; g < _groups.Length; g++)
            {
                IReadOnlyList<InputAction> actions = _groups[g].Actions;
                for (int i = 0; i < actions.Count; i++)
                {
                    yield return actions[i];
                }
            }
        }

        private static Group[] Build()
        {
            List<Group> groups = new List<Group>();

            groups.Add(new Group(ModStrings.Screens.Cursor, new[]
            {
                AccessibilityActions.UiUp,
                AccessibilityActions.UiDown,
                AccessibilityActions.UiLeft,
                AccessibilityActions.UiRight,
                AccessibilityActions.UiNext,
                AccessibilityActions.UiPrev,
                AccessibilityActions.UiHome,
                AccessibilityActions.UiEnd,
                AccessibilityActions.UiRegionPrev,
                AccessibilityActions.UiRegionNext,
                AccessibilityActions.UiCoarseDecrease,
                AccessibilityActions.UiCoarseIncrease,
                AccessibilityActions.UiLeftClick,
                AccessibilityActions.UiRightClick,
                AccessibilityActions.UiCarry,
                AccessibilityActions.UiBack,
                AccessibilityActions.UiClearSearch,
            }));

            groups.Add(new Group(ModStrings.Screens.ReviewBuffer, new[]
            {
                AccessibilityActions.PreviousBuffer,
                AccessibilityActions.NextBuffer,
                AccessibilityActions.PreviousBufferLine,
                AccessibilityActions.NextBufferLine,
                AccessibilityActions.FirstBufferLine,
                AccessibilityActions.LastBufferLine,
            }));

            groups.Add(new Group(ModStrings.Screens.AdventureMap, new[]
            {
                AccessibilityActions.MapMoveNorth,
                AccessibilityActions.MapMoveSouth,
                AccessibilityActions.MapMoveWest,
                AccessibilityActions.MapMoveEast,
                AccessibilityActions.MapSkipNorth,
                AccessibilityActions.MapSkipSouth,
                AccessibilityActions.MapSkipWest,
                AccessibilityActions.MapSkipEast,
                AccessibilityActions.NextWielder,
                AccessibilityActions.NextSettlement,
                AccessibilityActions.SummarizeReachableEntities,
                AccessibilityActions.SummarizeResources,
                AccessibilityActions.DescribePosition,
                AccessibilityActions.SonarSweep,
                AccessibilityActions.ScannerLookAround,
                AccessibilityActions.ScannerIncreaseLookAroundRadius,
                AccessibilityActions.ScannerDecreaseLookAroundRadius,
                AccessibilityActions.FocusHudTroops,
                AccessibilityActions.FocusHudResources,
                AccessibilityActions.FocusHudObjectives,
                AccessibilityActions.FocusHudNotifications,
            }));

            groups.Add(new Group(ModStrings.Screens.Scanner, new[]
            {
                AccessibilityActions.ScannerSearch,
                AccessibilityActions.ScannerPreviousCategory,
                AccessibilityActions.ScannerNextCategory,
                AccessibilityActions.ScannerPreviousSubcategory,
                AccessibilityActions.ScannerNextSubcategory,
                AccessibilityActions.ScannerPreviousItem,
                AccessibilityActions.ScannerNextItem,
                AccessibilityActions.ScannerPreviousInstance,
                AccessibilityActions.ScannerNextInstance,
                AccessibilityActions.ScannerJumpToResult,
                AccessibilityActions.ScannerSpeakDistanceAndDirection,
                AccessibilityActions.ScannerReturnFromJump,
                AccessibilityActions.ScannerNextCustomEntryComma,
                AccessibilityActions.ScannerPreviousCustomEntryComma,
                AccessibilityActions.ScannerNextCustomEntryPeriod,
                AccessibilityActions.ScannerPreviousCustomEntryPeriod,
                AccessibilityActions.ScannerNextCustomEntrySlash,
                AccessibilityActions.ScannerPreviousCustomEntrySlash,
            }));

            groups.Add(new Group(ModStrings.Screens.HexGrid, new[]
            {
                AccessibilityActions.HexGridWest,
                AccessibilityActions.HexGridEast,
                AccessibilityActions.HexGridNorthWest,
                AccessibilityActions.HexGridNorthEast,
                AccessibilityActions.HexGridSouthWest,
                AccessibilityActions.HexGridSouthEast,
                AccessibilityActions.HexGridFocusCenterTile,
                AccessibilityActions.HexGridSkipWest,
                AccessibilityActions.HexGridSkipEast,
                AccessibilityActions.HexGridSkipNorthWest,
                AccessibilityActions.HexGridSkipNorthEast,
                AccessibilityActions.HexGridSkipSouthWest,
                AccessibilityActions.HexGridSkipSouthEast,
            }));

            groups.Add(new Group(ModStrings.Screens.Combat, new[]
            {
                AccessibilityActions.CombatInspect,
                AccessibilityActions.CombatNextActingTroop,
                AccessibilityActions.CombatPreviousActingTroop,
                AccessibilityActions.CombatFocusActingTroop,
                AccessibilityActions.CombatNextEnemyTroop,
                AccessibilityActions.CombatPreviousEnemyTroop,
                AccessibilityActions.CombatFocusTimeline,
                AccessibilityActions.ReadThreat,
                AccessibilityActions.SummarizeEnemyResources,
            }));

            // Last, and forty rows long: four gestures per bookmark slot.
            List<InputAction> bookmarks = new List<InputAction>();
            bookmarks.AddRange(AccessibilityActions.SaveBookmarks);
            bookmarks.AddRange(AccessibilityActions.JumpToBookmarks);
            bookmarks.AddRange(AccessibilityActions.SpeakBookmarkDirections);
            bookmarks.AddRange(AccessibilityActions.ToggleBookmarkBeacons);
            groups.Add(new Group(ModStrings.Screens.Bookmarks, bookmarks.ToArray()));

            return groups.ToArray();
        }
    }
}
