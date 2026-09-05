using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    public sealed class TutorialSlideshowScreen : Screen
    {
        private readonly TutorialSlideshowAdapter _adapter;

        public TutorialSlideshowScreen(TutorialSlideshowAdapter adapter)
            : base(BuildRoot(adapter))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TutorialMenu[] menus = Resources.FindObjectsOfTypeAll<TutorialMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TutorialMenu menu = menus[i];
                if (!IsLiveSceneMenu(menu))
                {
                    continue;
                }

                TutorialSlideshowAdapter adapter = new TutorialSlideshowAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new TutorialSlideshowScreen(adapter);
                }
            }

            return null;
        }

        public static bool IsLiveSceneMenu(TutorialMenu menu)
        {
            return menu != null
                && menu.gameObject != null
                && menu.gameObject.scene.IsValid()
                && menu.gameObject.scene.isLoaded;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (_adapter != null && _adapter.IsCloseAvailable())
                {
                    _adapter.ActivateClose();
                }

                return true;
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(TutorialSlideshowAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("tutorial-slideshow-screen", adapter != null ? adapter.Header : string.Empty);
            root.AddChild(new TextWidget(
                "tutorial-slideshow-header",
                () => adapter != null ? adapter.Header : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));
            root.AddChild(new TextWidget(
                "tutorial-slideshow-description",
                () => adapter != null ? adapter.Description : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));
            root.AddChild(new ButtonWidget(
                "tutorial-slideshow-previous",
                ModText.Get(ModStrings.Screens.Previous),
                () => adapter != null && adapter.ActivatePrevious(),
                null,
                () => adapter != null && adapter.IsPreviousAvailable(),
                () => adapter != null && adapter.IsPreviousAvailable()));
            root.AddChild(new ButtonWidget(
                "tutorial-slideshow-next",
                ModText.Get(ModStrings.Screens.Next),
                () => adapter != null && adapter.ActivateNext(),
                null,
                () => adapter != null && adapter.IsNextAvailable(),
                () => adapter != null && adapter.IsNextAvailable()));
            root.AddChild(new CheckboxWidget(
                "tutorial-slideshow-tutorials-toggle",
                adapter != null ? adapter.TutorialsToggleLabel : string.Empty,
                () => { if (adapter != null) adapter.ToggleTutorials(); },
                () => adapter != null && adapter.IsTutorialsChecked(),
                () => adapter != null));
            root.AddChild(new ButtonWidget(
                "tutorial-slideshow-close",
                ModText.Get(ModStrings.Screens.Close),
                () => adapter != null && adapter.ActivateClose(),
                null,
                () => adapter != null && adapter.IsCloseAvailable(),
                () => adapter != null && adapter.IsCloseAvailable()));
            return root;
        }
    }
}
