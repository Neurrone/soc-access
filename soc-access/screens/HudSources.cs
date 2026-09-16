using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The two adventure HUDs the menus of this scene hang off, and the fields they hold them in.
    ///
    /// Neither HUD is visible from the scene container: <c>KingdomInformationHUDInstaller</c> and
    /// <c>CommanderHUDInstaller</c> install into a <c>GameObjectContext</c> of their own, so both are
    /// resolved from the sub-containers (<see cref="SceneSubContainers"/>). The kingdom HUD's four
    /// menus are bound <c>WhenInjectedInto&lt;KingdomInformationHUD&gt;</c> and so cannot be resolved
    /// at all; they are read off the HUD's own fields, which is what the owner-field shape of
    /// <see cref="ScreenSource{T}.FromOwner{TOwner}"/> exists for. The commander HUD's are on its
    /// <c>Settings</c>, which IS bound plainly in the same sub-container.
    ///
    /// The two owner sources are shared: every screen that hangs off one of them makes its own leaf
    /// source with <c>FromOwner</c>, and they all share the one memo, so the scene's contexts are
    /// walked once per scene load however many screens ask.
    /// </summary>
    public static class HudSources
    {
        private static readonly AccessTools.FieldRef<KingdomInformationHUD, KingdomInformationHUD.Settings> KingdomSettingsRef =
            AccessTools.FieldRefAccess<KingdomInformationHUD, KingdomInformationHUD.Settings>("_settings");
        private static readonly AccessTools.FieldRef<KingdomInformationHUD, IResearchMenu> ResearchRef =
            AccessTools.FieldRefAccess<KingdomInformationHUD, IResearchMenu>("_researchMenu");
        private static readonly AccessTools.FieldRef<KingdomInformationHUD, IKingdomTroopOverviewMenu> TroopOverviewRef =
            AccessTools.FieldRefAccess<KingdomInformationHUD, IKingdomTroopOverviewMenu>("_troopOverviewMenu");
        private static readonly AccessTools.FieldRef<KingdomInformationHUD, IKingdomEntityOverviewMenu> EntityOverviewRef =
            AccessTools.FieldRefAccess<KingdomInformationHUD, IKingdomEntityOverviewMenu>("_entityOverviewMenu");
        private static readonly AccessTools.FieldRef<KingdomInformationHUD, IAdventurePlayerMenu> PlayerMenuRef =
            AccessTools.FieldRefAccess<KingdomInformationHUD, IAdventurePlayerMenu>("_adventurePlayerMenu");
        private static readonly AccessTools.FieldRef<AdventurePlayerMenu, SendResourcePopup> SendResourceRef =
            AccessTools.FieldRefAccess<AdventurePlayerMenu, SendResourcePopup>("_sendResourcePopup");
        private static readonly AccessTools.FieldRef<AdventurePlayerMenu, GiftTownPopup> GiftTownRef =
            AccessTools.FieldRefAccess<AdventurePlayerMenu, GiftTownPopup>("_giftTownPopup");
        private static readonly AccessTools.FieldRef<AdventureSpellbookOpener, SpellBook> SpellBookRef =
            AccessTools.FieldRefAccess<AdventureSpellbookOpener, SpellBook>("_spellBook");

        /// <summary>The kingdom HUD: the research, troop overview, building overview and player
        /// menus, and the marketplace on its settings.</summary>
        public static readonly ScreenSource<KingdomInformationHUD> Kingdom =
            ScreenSource<KingdomInformationHUD>.FromSubContainer(LoadedScenes.AdventureScene);

        /// <summary>The commander HUD's settings: the wielder sheet and the spellbook's opener. The
        /// SETTINGS rather than the HUD, because they are bound plainly and the HUD holds nothing
        /// else the mod reads.</summary>
        public static readonly ScreenSource<CommanderHUD.Settings> Commander =
            ScreenSource<CommanderHUD.Settings>.FromSubContainer(LoadedScenes.AdventureScene);

        /// <summary>The player menu, which the two ally popups hang off in turn.</summary>
        public static readonly ScreenSource<AdventurePlayerMenu> PlayerMenu =
            ScreenSource<AdventurePlayerMenu>.FromOwner(Kingdom, hud => PlayerMenuRef(hud) as AdventurePlayerMenu);

        public static ResearchMenu Research(KingdomInformationHUD hud)
        {
            return ResearchRef(hud) as ResearchMenu;
        }

        public static KingdomTroopOverviewMenu TroopOverview(KingdomInformationHUD hud)
        {
            return TroopOverviewRef(hud) as KingdomTroopOverviewMenu;
        }

        public static KingdomEntityOverviewMenu EntityOverview(KingdomInformationHUD hud)
        {
            return EntityOverviewRef(hud) as KingdomEntityOverviewMenu;
        }

        public static MarketplaceMenu Marketplace(KingdomInformationHUD hud)
        {
            KingdomInformationHUD.Settings settings = KingdomSettingsRef(hud);
            return settings == null ? null : settings.MarketplaceMenu;
        }

        public static SendResourcePopup SendResource(AdventurePlayerMenu menu)
        {
            return SendResourceRef(menu);
        }

        public static GiftTownPopup GiftTown(AdventurePlayerMenu menu)
        {
            return GiftTownRef(menu);
        }

        public static CommanderSheet Sheet(CommanderHUD.Settings settings)
        {
            return settings.Inventory;
        }

        public static SpellBook Spellbook(CommanderHUD.Settings settings)
        {
            AdventureSpellbookOpener opener = settings.SpellbookOpener;
            return opener == null ? null : SpellBookRef(opener);
        }
    }
}
