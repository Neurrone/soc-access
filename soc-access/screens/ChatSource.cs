using SongsOfConquest.Client.Chat;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The chat window and the chat button the game has up now, and the one adapter over them.
    ///
    /// Both live in a <c>GameObjectContext</c> of their own (the adventure HUD's, the lobby's), bound
    /// with <c>BindInterfacesTo</c> and carrying no interface of their own, so they are reached as
    /// that sub-container's <c>IInitializable</c>s (<see cref="SceneSubContainers"/>). The answer is
    /// memoised on the set of loaded scenes like every other source's.
    ///
    /// Shared rather than owned by a screen: four screens read it - the chat page itself, and the
    /// map, the battle and the lobby, each of which draws the chat BUTTON in a band of its own - and
    /// the adapter caches the message history, which none of them should be rebuilding for the
    /// others.
    /// </summary>
    public static class ChatSource
    {
        private static readonly ScreenSource<ChatWindowBehavior> Window =
            ScreenSource<ChatWindowBehavior>.From(SceneSubContainers.ResolveInitializable<ChatWindowBehavior>);

        private static readonly ScreenSource<ChatButtonBehavior> Button =
            ScreenSource<ChatButtonBehavior>.From(SceneSubContainers.ResolveInitializable<ChatButtonBehavior>);

        private static ChatAdapter _adapter;
        private static ChatWindowBehavior _window;
        private static ChatButtonBehavior _button;

        /// <summary>The adapter over the chat the game has now, or null where the game has none (a
        /// single-player skirmish binds no chat system at all). Kept while the window and the button
        /// it wraps are the same objects, so a frame costs two memo comparisons.</summary>
        public static ChatAdapter Current
        {
            get
            {
                ChatWindowBehavior window = Window.Current;
                if (window == null)
                {
                    _adapter = null;
                    _window = null;
                    _button = null;
                    return null;
                }

                ChatButtonBehavior button = Button.Current;
                if (_adapter == null || !ReferenceEquals(_window, window) || !ReferenceEquals(_button, button))
                {
                    _adapter = new ChatAdapter(window, button);
                    _window = window;
                    _button = button;
                }

                return _adapter;
            }
        }

        public static void Reset()
        {
            _adapter = null;
            _window = null;
            _button = null;
        }
    }
}
