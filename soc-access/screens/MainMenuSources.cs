using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Main;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Where the five main-menu pages are found, and the names of the scenes they live in.
    ///
    /// The main menu loads each of its pages as an ADDITIVE scene of its own and unloads the
    /// previous one (<c>MainMenuSceneLoader.GetSceneName</c>), so every source here is gated to one
    /// scene name and costs nothing while that scene is not loaded. The scene set is what the
    /// sources' memo is keyed on, so entering and leaving a page is the only event any of them
    /// needs.
    ///
    /// Each shape was checked in the REPL on 2026-09-09:
    ///
    /// - <c>MainMenu</c> is a component on the SceneContext's own ROOT object of
    ///   <c>MainMenuContentScene</c>, so a <c>GetComponent</c> per root finds it.
    /// - <c>CampaignMenu</c> (object <c>CampaignSelectMenu</c>) and
    ///   <c>TaleButtonLayoutCoordinator</c> (object <c>LayoutContainer</c>) are unbound
    ///   <c>MonoBehaviour</c>s under their scene's root, so each is one gated walk of that scene's
    ///   roots.
    /// - <c>CustomCampaignSelectMenuBehavior</c> and <c>CampaignMapSelectMenu</c> are plain classes
    ///   bound <c>NonLazy</c> into a <c>GameObjectContext</c>'s own sub-container, which neither the
    ///   scene nor the project container can see into, so both come from
    ///   <see cref="SceneSubContainers"/>.
    /// - The campaign map page's information view is bound LAZILY, so resolving it would construct
    ///   it; it is read off the menu's own field instead. The menu is <c>NonLazy</c> and takes the
    ///   view as a constructor argument, so the field is filled by the time the menu exists.
    /// </summary>
    public static class MainMenuSources
    {
        /// <summary>The main menu itself.</summary>
        public const string MainMenuScene = "MainMenuContentScene";

        /// <summary>The campaign and tale select page.</summary>
        public const string CampaignSelectScene = "CampaignSelectScene";

        /// <summary>The tale select page.</summary>
        public const string TaleSelectScene = "TaleSelectScene";

        /// <summary>The community campaigns page.</summary>
        public const string CustomCampaignSelectScene = "CustomCampaignSelectScene";

        /// <summary>A campaign's mission map.</summary>
        public const string CampaignMapSelectScene = "CampaignMapSelectScene";

        private static readonly AccessTools.FieldRef<CampaignMapSelectMenu, ICampaignMapSelectedInformationView> InformationViewRef =
            AccessTools.FieldRefAccess<CampaignMapSelectMenu, ICampaignMapSelectedInformationView>("_informationView");

        public static readonly ScreenSource<MainMenu> Main =
            ScreenSource<MainMenu>.FromSceneRoot(MainMenuScene);

        public static readonly ScreenSource<CampaignMenu> Campaign =
            ScreenSource<CampaignMenu>.FromRootWalk(CampaignSelectScene);

        public static readonly ScreenSource<TaleButtonLayoutCoordinator> TaleSelect =
            ScreenSource<TaleButtonLayoutCoordinator>.FromRootWalk(TaleSelectScene);

        public static readonly ScreenSource<CustomCampaignSelectMenuBehavior> CustomCampaignSelect =
            ScreenSource<CustomCampaignSelectMenuBehavior>.FromSubContainer(CustomCampaignSelectScene);

        public static readonly ScreenSource<CampaignMapSelectMenu> CampaignMapSelect =
            ScreenSource<CampaignMapSelectMenu>.FromSubContainer(CampaignMapSelectScene);

        /// <summary>The information panel down the right of the campaign map page, read off the
        /// menu that owns it rather than resolved, because its binding is lazy.</summary>
        public static readonly ScreenSource<CampaignMapSelectedInformationView> CampaignMapSelectInformation =
            ScreenSource<CampaignMapSelectedInformationView>.FromOwner(
                CampaignMapSelect,
                menu => InformationViewRef(menu) as CampaignMapSelectedInformationView);
    }
}
