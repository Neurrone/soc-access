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

        /// <summary>Home and End take an amount slider to the ends of its pool.</summary>
        public override bool OnEdge(GraphNode node, bool first)
        {
            return RecruitGroups.OnEdge(Host == null ? null : Host.PurchaseTroops, Key, node, first);
        }
    }
}
