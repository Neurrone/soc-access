using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The page a wielder drafts troops on, the same on all three menus that open it: the dwelling,
    /// where it IS the landing page, and the town and the defence menu, where it sits over theirs.
    ///
    /// THE PAGE IS THE GRID OF RECRUIT CARDS (<c>ui/RecruitGroups.cs</c>), one expandable group each,
    /// which the rally point draws too. Everything else on the page - the tutorial button, the
    /// wielder's band, the back button and the close cross - is the base's
    /// (<c>screens/TroopManagementScreenBase.cs</c>), which also records where Escape goes.
    ///
    /// Against the widget page it replaced (walked on all three hosts 2026-09-08), nothing spoken
    /// was lost: the page's title is now the screen's name rather than a line of its own, the buy
    /// button says "Purchase" with its price as the value instead of "Purchase for 3500 Gold", the
    /// slider says "5 of 5" instead of "5 /5", a card whose pool is empty reads the game's own line
    /// ("Pool is empty") where the widget page said "0 available", and the slider, the price and the
    /// pool-upgrade button live inside the card's group rather than beside a selection of the mod's
    /// own. The wielder's rows lost "slot 5": the graph says where a row sits.
    /// </summary>
    public sealed class DraftTroopsScreen : TroopManagementScreenBase
    {
        public DraftTroopsScreen(ITroopManagementHostAdapter host)
            : base(host)
        {
        }

        public static Screen TryBuildActiveDwellingScreen()
        {
            DwellingInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<DwellingInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DwellingInteractionMenuAdapter adapter = new DwellingInteractionMenuAdapter(menus[i]);
                if (adapter.IsDraftPresent())
                {
                    return new DraftTroopsScreen(new DwellingTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        public static Screen TryBuildActiveSettlementScreen()
        {
            TownInteractionMenu[] menus = Resources.FindObjectsOfTypeAll<TownInteractionMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                TownInteractionMenuAdapter adapter = new TownInteractionMenuAdapter(menus[i]);
                if (adapter.IsDraftPresent())
                {
                    return new DraftTroopsScreen(new SettlementTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        public static Screen TryBuildActiveDefenceScreen()
        {
            DefenceMenu[] menus = Resources.FindObjectsOfTypeAll<DefenceMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                DefenceMenuAdapter adapter = new DefenceMenuAdapter(menus[i]);
                if (adapter.IsDraftPresent())
                {
                    return new DraftTroopsScreen(new DefenceTroopManagementHostAdapter(adapter));
                }
            }

            return null;
        }

        protected override string ScreenSuffix { get { return "draft-troops"; } }

        protected override bool IsContentPresent()
        {
            return Host != null && Host.IsDraftPresent();
        }

        protected override void BuildContent(GraphBuilder builder)
        {
            RecruitGroups.Region(builder, Host.PurchaseTroops, Key);
        }
    }
}
