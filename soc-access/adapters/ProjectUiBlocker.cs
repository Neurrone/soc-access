using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The image the game fades in over everything while it takes the player somewhere else: Quit
    /// to Main Menu, Quit to Map Editor, loading a save, restarting a mission, starting a game from
    /// a menu, a main menu page changing scene. The menu that asked has already closed when that
    /// fade starts (or closes behind it) and the scene loader only goes busy when the fade ends,
    /// 0.2 s later, so this image being active is the one fact that says "what is underneath is on
    /// its way out" during that gap. Quit to Desktop does not show it.
    /// </summary>
    public sealed class ProjectUiBlocker
    {
        private readonly Image _image;

        public ProjectUiBlocker(IProjectUIBlocker blocker)
        {
            try
            {
                FieldInfo settingsField = blocker == null ? null : AccessTools.Field(blocker.GetType(), "_settings");
                SongsOfConquest.Client.Menu.ProjectUIBlocker.Settings settings = settingsField == null
                    ? null
                    : settingsField.GetValue(blocker) as SongsOfConquest.Client.Menu.ProjectUIBlocker.Settings;
                _image = settings == null ? null : settings.BlockerImage;
            }
            catch (Exception exception)
            {
                // Constructor-time and once per blocker, so it says so every time.
                SocAccessMod.Instance?.LogWarning("ProjectUiBlocker could not read the blocker image: " + exception.Message);
            }
        }

        /// <summary>Whether the blocker is showing, which it is from the moment the game starts the
        /// fade until the next scene is up (or, where the game hides it itself, three seconds
        /// later). False when the image could not be reached; the gate then never fires.</summary>
        public bool IsShowing
        {
            get { return _image != null && _image.gameObject.activeSelf; }
        }
    }
}
