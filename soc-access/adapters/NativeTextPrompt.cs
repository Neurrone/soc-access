using System;
using Lavapotion.Utilities;
using SongsOfConquest.Client;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Localization;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The game's own text entry popup, for mod screens that need a line of
    /// typing but have no native input field of their own to wrap.
    ///
    /// The popup manager is a project-wide singleton rather than something a
    /// scene owns, so it is reachable from the mod settings screen wherever that
    /// was opened from.
    /// </summary>
    internal static class NativeTextPrompt
    {
        /// <summary>
        /// Asks for a line of text and reports it back when the player confirms.
        /// A cancelled prompt calls nothing. Returns false when the game has no
        /// popup to show, so a caller can say so rather than appear to hang.
        /// </summary>
        public static bool Ask(string header, string prepopulate, string confirmLabel, Action<string> onConfirm)
        {
            ISystemPopups popups = Resolve();
            if (popups == null || onConfirm == null)
            {
                return false;
            }

            try
            {
                popups
                    .AskForInput(
                        header ?? string.Empty,
                        string.Empty,
                        confirmLabel ?? string.Empty,
                        GameText.Get("Common/Cancel", "Cancel"),
                        prepopulate,
                        InputFieldContentType.Standard,
                        InputLevel.Popup)
                    .Then((Action<AsyncResponse>)(response =>
                    {
                        if (!response.Success)
                        {
                            return;
                        }

                        // The game holds this callback for as long as the popup
                        // is open, which can outlive a script reload. Whatever
                        // it lands in by then is the mod's problem, not the
                        // game's, so it never escapes into the popup.
                        try
                        {
                            onConfirm(response.Message);
                        }
                        catch (Exception callbackException)
                        {
                            SocAccessMod.Instance?.LogWarning(
                                "Text prompt callback failed: " + callbackException);
                        }
                    }));
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to open the text prompt: " + exception.Message);
                return false;
            }
        }

        private static ISystemPopups Resolve()
        {
            try
            {
                ProjectContext context = ProjectContext.Instance;
                DiContainer container = context != null ? context.Container : null;
                return container != null ? container.TryResolve<ISystemPopups>() : null;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("Failed to resolve the system popups: " + exception.Message);
                return null;
            }
        }
    }
}
