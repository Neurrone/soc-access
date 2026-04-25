using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureMapScreen : Screen
    {
        private readonly AdventureMapAdapter _adapter;

        public AdventureMapScreen(AdventureMapAdapter adapter)
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _adapter?.ClearFocusedTileOverlay();
        }

        private static ContainerWidget BuildRoot(AdventureMapAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("adventure_map_screen", "Adventure map");
            root.AddChild(new AdventureMapGrid(adapter));
            return root;
        }
    }
}
