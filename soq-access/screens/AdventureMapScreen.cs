using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AdventureMapScreen : Screen
    {
        private readonly AdventureMapAdapter _adapter;
        private readonly AdventureMapEventListener _eventListener;

        public AdventureMapScreen(AdventureMapAdapter adapter, AdventureMapEventListener eventListener)
            : base(adapter != null ? adapter.SourceKey : null, BuildRoot(adapter))
        {
            _adapter = adapter;
            _eventListener = eventListener;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnPush()
        {
            _eventListener?.Attach();
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
        }

        public override void OnPop()
        {
            _eventListener?.Detach();
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
