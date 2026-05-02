using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CombatScreen : Screen
    {
        private readonly CombatAdapter _adapter;

        public CombatScreen(CombatAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public CombatAdapter Adapter
        {
            get { return _adapter; }
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.ClearNativeTooltip();
        }

        public override void OnPop()
        {
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private static ContainerWidget BuildRoot(CombatAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("combat-screen", "Combat");
            root.AddChild(new CombatHexGrid(adapter));
            return root;
        }
    }
}
