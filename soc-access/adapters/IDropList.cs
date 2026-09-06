using System;
using System.Collections.Generic;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// A drop list as the mod's own list screen needs it: the options the game offers, which one the
    /// setting is on, and the game's own popup underneath - opened, closed, asked about and
    /// highlighted one entry at a time.
    ///
    /// Every page that draws a dropdown draws the same control (<c>UITextMeshDropdown</c>), so every
    /// adapter's dropdown answers the same questions and one list screen serves them all. What
    /// TAKING an entry means is not here: only the page that opened the list knows that, and it
    /// hands it over when it opens the list.
    /// </summary>
    public interface IDropList
    {
        string Id { get; }
        Func<IReadOnlyList<string>> GetOptions { get; }
        Func<int> GetValue { get; }
        Func<bool> IsEnabled { get; }
        Func<bool> IsVisible { get; }

        /// <summary>Open the game's own list popup, close it, ask whether it is open, and put the
        /// game's highlight on one of its entries.</summary>
        Func<bool> OpenPopup { get; }
        Func<bool> ClosePopup { get; }
        Func<bool> IsPopupOpen { get; }
        Func<int, bool> FocusOption { get; }
    }
}
