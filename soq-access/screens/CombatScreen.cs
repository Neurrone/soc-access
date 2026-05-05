using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CombatScreen : Screen
    {
        private readonly CombatAdapter _adapter;
        private readonly CombatHexGrid _grid;

        public CombatScreen(CombatAdapter adapter)
            : this(adapter, new CombatHexGrid(adapter))
        {
        }

        private CombatScreen(CombatAdapter adapter, CombatHexGrid grid)
            : base(BuildRoot(grid))
        {
            _adapter = adapter;
            _grid = grid;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public CombatAdapter Adapter
        {
            get { return _adapter; }
        }

        public override void OnPush()
        {
            _grid?.AttachSpellCastBegin();
            _adapter?.AttachSpellTargetingNarration();
            _adapter?.AnnounceVisibleSpellTargetInstruction();
        }

        public void MoveCursorToLocalActingTroop(int troopId)
        {
            Vector2Int position;
            if (_adapter != null
                && _adapter.TryGetLocalActingTroopPosition(troopId, out position)
                && _grid != null)
            {
                _grid.MoveToActingTroop(position);
            }
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        public override void OnPop()
        {
            _grid?.DetachSpellCastBegin();
            _adapter?.DetachSpellTargetingNarration();
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private static ContainerWidget BuildRoot(CombatHexGrid grid)
        {
            ContainerWidget root = new ContainerWidget("combat-screen", "Combat");
            root.AddChild(grid);
            return root;
        }
    }
}
