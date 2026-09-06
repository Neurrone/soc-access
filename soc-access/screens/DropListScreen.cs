using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The list a combo box opens, as a screen of its own. Enter on a setting's drop list puts the
    /// player in here; Up and Down walk the entries, Enter picks one, Escape leaves the setting as it
    /// was.
    ///
    /// It is a screen rather than a mode of the page underneath because that is what it is to the
    /// player: a smaller thing on top of a bigger one, with its own name (the setting being chosen),
    /// its own contents and its own way out. The page underneath stays where it was and gets the
    /// cursor back on the setting the player just answered.
    ///
    /// Which list is open is the mod's own state: the game's dropdown has no notion of being opened by
    /// keyboard. The game's real popup is opened alongside it so the picture shows what the player is
    /// doing, and the entry under the cursor is highlighted the way hovering it would be - but the
    /// setting itself is not touched until Enter, so walking the list and leaving changes nothing.
    ///
    /// This is the one surface here the MOD put on the screen, so it is the one that takes Escape away
    /// from the game (<see cref="ConsumesBack"/>), per the phase B ruling. The game closing the popup
    /// underneath - a click elsewhere, the page going away - is noticed in <see cref="Update"/> and
    /// takes the screen with it.
    /// </summary>
    public sealed class DropListScreen : GraphScreen
    {
        /// <summary>What a caller has to say about a list for it to be navigable: the control, what to
        /// call it, and what taking an entry means. Everything else is the same for every drop list.
        /// </summary>
        public sealed class Request
        {
            public OptionsMenuAdapter.DropdownItem Item;
            public string Title;
            public Action<int> Choose;
        }

        private const string OptionsStop = "drop-list";

        /// <summary>The list that is open, or null. Static because opening it is a decision the page
        /// underneath makes and this screen's existence is the consequence.</summary>
        private static Request _open;

        private readonly Request _request;

        /// <summary>The frame the popup was opened on. The game needs the frame to build the list, so
        /// "the game closed it underneath us" is only a question worth asking after it.</summary>
        private int _openedFrame = -1;

        public DropListScreen(Request request)
        {
            _request = request;
        }

        /// <summary>Open <paramref name="item"/>'s list as a screen over whatever is showing.
        /// <paramref name="choose"/> is what taking an entry means, which only the page that owns the
        /// list knows.</summary>
        public static void Open(OptionsMenuAdapter.DropdownItem item, string title, Action<int> choose)
        {
            ScreenManager screens = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            if (item == null || screens == null)
            {
                return;
            }

            Request request = new Request { Item = item, Title = title, Choose = choose };
            _open = request;
            screens.Push(new DropListScreen(request), "drop list opened");
        }

        /// <summary>Forget any open list - the mod is going away.</summary>
        public static void Reset()
        {
            _open = null;
        }

        public override string Key
        {
            get { return "drop-list"; }
        }

        /// <summary>The setting being chosen, in the game's own words, so opening the list reads
        /// "Language" and then the language currently set.</summary>
        public override string ScreenName
        {
            get { return _request != null ? _request.Title : null; }
        }

        /// <summary>Ours while this is still the list the mod asked for and the control it belongs to
        /// is still drawn.</summary>
        public override bool IsPresent()
        {
            return _request != null
                && ReferenceEquals(_open, _request)
                && _request.Item != null
                && _request.Item.IsVisible();
        }

        /// <summary>A mod-owned surface denies the game the key: Escape closes the list and leaves the
        /// setting as it was, rather than reaching the page underneath and closing that.</summary>
        public override bool ConsumesBack
        {
            get { return true; }
        }

        public override bool Back()
        {
            Close();
            return true;
        }

        public override void OnPush()
        {
            if (_request == null || _request.Item == null)
            {
                return;
            }

            if (_request.Item.OpenPopup != null)
            {
                _request.Item.OpenPopup();
            }

            _openedFrame = Time.frameCount;
        }

        /// <summary>Shut the game's popup on the way out, whichever way the player left. Harmless when
        /// the game has already closed it.</summary>
        public override void OnPop()
        {
            base.OnPop();
            if (_request != null && _request.Item != null && _request.Item.ClosePopup != null)
            {
                _request.Item.ClosePopup();
            }

            if (ReferenceEquals(_open, _request))
            {
                _open = null;
            }
        }

        /// <summary>The game closing the popup underneath - a click outside it, the page going away -
        /// is the list ending, so the screen goes with it.</summary>
        public override void Update()
        {
            base.Update();
            if (_request == null || _request.Item == null || _openedFrame < 0 || Time.frameCount <= _openedFrame)
            {
                return;
            }

            bool open = _request.Item.IsPopupOpen == null || _request.Item.IsPopupOpen();
            if (!open)
            {
                Close();
            }
        }

        public override void Build(GraphBuilder builder)
        {
            OptionsMenuAdapter.DropdownItem item = _request != null ? _request.Item : null;
            if (item == null || !IsPresent())
            {
                return;
            }

            IReadOnlyList<string> options = item.GetOptions();
            if (options == null || options.Count == 0)
            {
                return;
            }

            builder.BeginStop(OptionsStop);
            for (int i = 0; i < options.Count; i++)
            {
                int index = i;
                NodeVtable vtable = GraphNodes.Choice(
                    () => Option(item, index),
                    () => item.GetValue() == index,
                    () => Choose(index),
                    item.IsEnabled);
                if (item.FocusOption != null)
                {
                    // The game's own highlight follows the cursor, so someone watching sees the entry
                    // being considered; what the setting is on does not move until Enter.
                    vtable.OnFocusVisual = () => item.FocusOption(index);
                }

                // Synthesized from the game's own option list: TMP builds a row per entry only while
                // the popup is open, and the mod's row answers for the option either way.
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("droplist:" + item.Id + "/" + index),
                    vtable));
            }
        }

        private static string Option(OptionsMenuAdapter.DropdownItem item, int index)
        {
            IReadOnlyList<string> options = item.GetOptions();
            return options != null && index < options.Count ? options[index] : string.Empty;
        }

        private void Choose(int index)
        {
            if (_request != null && _request.Choose != null)
            {
                _request.Choose(index);
            }

            Close();
        }

        private void Close()
        {
            if (ReferenceEquals(_open, _request))
            {
                _open = null;
            }

            ScreenManager screens = SocAccessMod.Instance != null ? SocAccessMod.Instance.ScreenManager : null;
            if (screens != null)
            {
                screens.Pop<DropListScreen>("drop list closed");
            }
        }
    }
}
