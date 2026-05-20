using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class TutorialSimpleScreen : Screen
    {
        private readonly TutorialSimpleAdapter _adapter;

        public TutorialSimpleScreen(TutorialSimpleAdapter adapter)
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
                if (!TutorialSlideshowScreen.IsLiveSceneMenu(menu))
                {
                    continue;
                }

                TutorialSlideshowAdapter slideshowAdapter = new TutorialSlideshowAdapter(menu);
                if (slideshowAdapter.IsPresent())
                {
                    continue;
                }

                TutorialSimpleAdapter adapter = new TutorialSimpleAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new TutorialSimpleScreen(adapter);
                }
            }

            return null;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                if (_adapter != null && _adapter.IsOkAvailable())
                {
                    _adapter.ActivateOk();
                }

                return true;
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(TutorialSimpleAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("tutorial-simple-screen", adapter != null ? adapter.Header : string.Empty);
            root.AddChild(new TextWidget(
                "tutorial-simple-header",
                () => adapter != null ? adapter.Header : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));
            root.AddChild(new TextWidget(
                "tutorial-simple-description",
                () => adapter != null ? adapter.Description : string.Empty,
                null,
                includeParentLabelInAnnouncement: false));
            root.AddChild(new CheckboxWidget(
                "tutorial-simple-tutorials-toggle",
                adapter != null ? adapter.TutorialsToggleLabel : string.Empty,
                () => { if (adapter != null) adapter.ToggleTutorials(); },
                () => adapter != null && adapter.IsTutorialsChecked(),
                () => adapter != null));
            root.AddChild(new ButtonWidget(
                "tutorial-simple-ok",
                adapter != null ? adapter.OkLabel : ModText.Get(ModStrings.Screens.Ok),
                () => adapter != null && adapter.ActivateOk(),
                null,
                () => adapter != null && adapter.IsOkAvailable(),
                () => adapter != null && adapter.IsOkAvailable()));
            return root;
        }
    }
}
