using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Screens;
using UnityEngine;

namespace SongsOfConquestAccess.Input
{
    /// <summary>
    /// Whether the game's key-binding capture owns the keyboard right now - a STAND-DOWN signal, the
    /// same shape as <see cref="GameTextFocus"/>.
    ///
    /// When the player rebinds a hotkey the game runs an interactive rebind
    /// (<c>UnityInputManager.AddOverride</c>) that ends on the FIRST key pressed, with no cancel key
    /// and no timeout: every key - arrows, Escape, letters - is the game's to bind. So while it is
    /// running the mod's input layer answers nothing at all, and the key reaches the game as a
    /// physical press would. The game marks the moment with a BUTTON-LESS
    /// <see cref="ConfirmPopup"/> (<c>OptionsMenuKeyBindContent.ShowBindingInstructions</c> is its one
    /// caller in the game, decompiled), so "the project-container ConfirmPopup is active in the
    /// hierarchy AND both its yes and no buttons are inactive" is that capture's unique native trace.
    ///
    /// A static query over live engine state. The one thing it holds is a
    /// <see cref="ScreenSource{T}"/> over the project-container popup, memoised and reload-safe the
    /// same way <see cref="MessageDialogScreen"/> resolves it; it re-resolves on a scene change and
    /// dies with the assembly on a hot reload, so there is nothing to reset.
    /// </summary>
    public static class KeyCaptureFocus
    {
        private static readonly AccessTools.FieldRef<ConfirmPopup, UIButton> YesButtonRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UIButton>("_yesButton");
        private static readonly AccessTools.FieldRef<ConfirmPopup, UIButton> NoButtonRef =
            AccessTools.FieldRefAccess<ConfirmPopup, UIButton>("_noButton");

        private static readonly ScreenSource<ConfirmPopup> Source = ScreenSource<ConfirmPopup>.FromProject();

        /// <summary>The project-container popup, whatever state it is in - so the announcement side can
        /// read the instruction text it is drawing the instant a capture starts. Null before it is
        /// resolved.</summary>
        public static ConfirmPopup Popup
        {
            get { return Source.Current; }
        }

        public static bool IsCapturing()
        {
            ConfirmPopup popup = Source.Current;
            if (popup == null)
            {
                return false;
            }

            GameObject gameObject = popup.gameObject;
            if (gameObject == null || !gameObject.activeInHierarchy)
            {
                return false;
            }

            UIButton yes = YesButtonRef(popup);
            UIButton no = NoButtonRef(popup);
            return yes != null && no != null && !yes.Active && !no.Active;
        }
    }
}
