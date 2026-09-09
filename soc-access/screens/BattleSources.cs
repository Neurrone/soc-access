using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.UI;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The battle scene, and the HUD settings the menus drawn over it hang off.
    ///
    /// <c>BattleSceneInstaller</c> is a component on the battle scene's SceneContext object, exactly
    /// as the adventure's view installer is on its own, and its container is where the whole fight is
    /// bound - so the installer IS the battle as far as the mod is concerned. The HUD's settings are
    /// bound plainly in that same container (<c>BattleHUDStateHandlerInstaller</c> is one of the
    /// scene context's own installers, not a <c>GameObjectContext</c>), so they come straight out of
    /// it rather than through a sub-container walk.
    ///
    /// The two sources are shared: the battlefield reads the installer and the spellbook reads the
    /// HUD's settings, and both are resolved once per set of loaded scenes.
    /// </summary>
    public static class BattleSources
    {
        /// <summary>The battle: one installer instance per fight, because the scene is loaded afresh
        /// for each and unloaded when it ends.</summary>
        public static readonly ScreenSource<BattleSceneInstaller> Scene =
            ScreenSource<BattleSceneInstaller>.FromSceneRoot(LoadedScenes.BattleScene);

        /// <summary>The battle HUD's settings, which hold the spellbook among the containers the HUD
        /// shows and hides.</summary>
        public static readonly ScreenSource<BattleHUDStateHandler.Settings> Hud =
            ScreenSource<BattleHUDStateHandler.Settings>.FromScene(LoadedScenes.BattleScene);

        /// <summary>The battle's spellbook. A different object from the adventure's, which hangs off
        /// the commander HUD (<see cref="HudSources.Spellbook"/>).</summary>
        public static SpellBook Spellbook(BattleHUDStateHandler.Settings settings)
        {
            return settings.SpellBook;
        }
    }
}
