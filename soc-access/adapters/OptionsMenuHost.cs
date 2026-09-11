using UnityEngine;
using Zenject;
using SongsOfConquest.Client.Menu.Options;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// WHERE THE MOD'S OWN DIALOG IS DRAWN: the game's options window, found through its installer.
    ///
    /// The mod draws its settings as a copy of that window, so it needs the window's Zenject
    /// container to inject the copy with, the panel to copy, and the canvas to put the copy on. All
    /// three come off the installer, which is a scene object with no binding to resolve, so it is
    /// looked for among the loaded objects of its type - which is adapter work and not something a
    /// <c>ui/</c> builder should be doing (AGENTS.md, Project Structure).
    ///
    /// Nothing here is on a build path: the dialog asks once when it is opened, and again when the
    /// mod tears its dialogs down.
    /// </summary>
    public static class OptionsMenuHost
    {
        /// <summary>The game's own options installer, or null while no scene holds one.</summary>
        public static OptionsMenuInstaller FindInstaller()
        {
            OptionsMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<OptionsMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                OptionsMenuInstaller installer = installers[i];
                if (installer != null
                    && installer.gameObject != null
                    && installer.gameObject.scene.isLoaded
                    && installer.settings != null)
                {
                    return installer;
                }
            }

            return null;
        }

        /// <summary>The container that window's own objects are injected from.</summary>
        public static DiContainer ContainerOf(OptionsMenuInstaller installer)
        {
            return Reflect.InstallerContainer(installer);
        }

        /// <summary>The window's own panel - what the mod copies - or null while the window is not
        /// in the scene.</summary>
        public static Transform PanelOf(OptionsMenuInstaller installer)
        {
            OptionsMenu.Settings settings = installer != null ? installer.settings : null;
            return settings != null && settings.parent != null
                ? settings.parent.MonoTransform.Find("Panel")
                : null;
        }

        /// <summary>The canvas that panel is drawn on, which is what the mod's copy is parented to.
        /// </summary>
        public static Transform CanvasOf(Transform panel)
        {
            return panel != null ? panel.parent.parent : null;
        }

        /// <summary>The canvas, found from nothing: for the teardown, which has no installer in
        /// hand.</summary>
        public static Transform FindCanvas()
        {
            return CanvasOf(PanelOf(FindInstaller()));
        }
    }
}
