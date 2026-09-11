using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The campaign and tale select page, made navigable as a graph. Two stops: the cards the page
    /// is made of, and the header band above them.
    ///
    /// The cards are the four campaign cards and the Tales card, read in the order the game draws
    /// them (measured 2026-09-06 at 1280x800: x 35, 287, 529, 770 and 1012, all in one band inside
    /// `CampaignColumns`), followed by Community Campaigns, which is drawn in a band of its own
    /// below them (rect [493,681,294,87] under `ForegroundCanvas` > `BottomButtonsLayout`, clear of
    /// the card band's bottom edge at y 644).
    ///
    /// A card draws more than its name: a paragraph of description and a progress line ("Completed:
    /// 4 / 4 missions"). Both are always on the screen, so both read after the label rather than
    /// waiting in the buffer, and both are in the buffer too. The progress line is also what the
    /// game says about a card it refuses, which is why it is declared with the availability state:
    /// an unavailable card says "unavailable" and then why.
    ///
    /// The menu registers no keyboard input of its own (`CampaignMenu` wires no input callbacks), so
    /// Escape would do nothing here; the screen claims it and presses the drawn Back button.
    /// </summary>
    public sealed class CampaignMenuScreen : LiveScreen<CampaignMenuAdapter>
    {
        private const string CardsStop = "campaign-cards";
        private const string HeaderStop = "campaign-header";

        protected override object ResolveMenu()
        {
            return MainMenuSources.Campaign.Current;
        }

        protected override CampaignMenuAdapter Adapt(object menu)
        {
            return new CampaignMenuAdapter((CampaignMenu)menu);
        }

        public override string Key
        {
            get { return "campaign-menu"; }
        }

        /// <summary>Layer 1: a main-menu page, over the main menu.</summary>
        public override int Layer
        {
            get { return 1; }
        }

        /// <summary>The page's own drawn title ("Choose Campaign or Tale").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.GetTitle() : null; }
        }

        public override object InitialFocusStop
        {
            get { return CardsStop; }
        }

        /// <summary>The page hides its header band as it closes (Back and Options go first), and the
        /// cursor standing on a header button falls onto a card: that recovery is the page leaving,
        /// not a move, and stays silent while the band is gone.</summary>
        public override bool IsWorkable
        {
            get { return Live != null && Live.BackButton != null && Live.BackButton.IsVisible(); }
        }

        public override IMenuButtonAdapter BackButton
        {
            get { return Live != null ? Live.BackButton : null; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            MenuCardPage.BuildCards(builder, CardsStop, DrawnCards(), Card);

            // Back at x 21 and Options at x 1233 of the header band: declared left to right, the
            // order they are drawn in.
            MenuCardPage.BuildHeader(
                builder,
                HeaderStop,
                "campaign:back",
                Live.BackButton,
                "campaign:options",
                Live.OptionsButton);
        }

        /// <summary>
        /// A card, activated through the game's own click. The description and the progress line the
        /// card also draws follow the label in the readout, and are review-buffer lines by being parts.
        ///
        /// The progress line is declared with the availability state rather than beside the
        /// description because it is both things at once: on an available card it is the progress,
        /// and on one the game refuses it is the refusal's reason, which has to be heard after
        /// "unavailable" rather than before it.
        /// </summary>
        private static NodeVtable Card(IMenuButtonAdapter item)
        {
            // Only the campaign cards read their description apart from their name: the game gives
            // CampaignButton named fields for the number, name, subtitle and paragraph, while the
            // Tales and Community Campaigns buttons are plain buttons whose whole visible text is
            // one label.
            CampaignButtonAdapter campaign = item as CampaignButtonAdapter;
            // No details section beside these: an announcement part is a buffer line already, so a
            // section repeating the description and the progress line would put each of them in the
            // review buffer twice (measured on the first build of this screen). The description is a
            // part per paragraph, which is one spoken line and one buffer line each.
            Func<string> label = campaign != null ? () => CardLabel(campaign) : (Func<string>)item.GetLabel;
            NodeVtable vtable = GraphNodes.Button(label, () => item.Activate(), item.IsEnabled);
            if (campaign != null)
            {
                GraphNodes.ParagraphParts(vtable, campaign.GetDescriptionLines);
            }

            // Watched live: the page is ready before the game has filled the campaign state in, so a
            // card focused on arrival has no progress line yet (measured: the first readout after
            // entering the menu says the description and stops, and the line is there a moment
            // later). The watch is what speaks it when it lands.
            vtable.Announcements.Add(new NodeAnnouncement(item.GetStatus, live: true, kind: AnnouncementKinds.Enabled));
            if (campaign != null)
            {
                // The card the cursor is on is the card the game highlights: CampaignButton's own
                // hover is what paints it, and the adapter already offers that path.
                vtable.OnFocusVisual = campaign.FocusNative;
            }

            return vtable;
        }

        /// <summary>A campaign card's label: which campaign of the page it is - the page draws the
        /// number as a figure on the card, which reads as a bare digit - and then the card's own
        /// title and subtitle.</summary>
        private static string CardLabel(CampaignButtonAdapter campaign)
        {
            string number = campaign.CampaignNumber > 0
                ? ModText.Get(ModStrings.Screens.CampaignNumber, campaign.CampaignNumber)
                : string.Empty;
            return MenuButtonTextUtility.JoinParts(number, campaign.GetLabel());
        }

        /// <summary>
        /// The cards, in the order the page draws them. The campaign cards and the Tales card share
        /// one band and are sorted by their measured left edge every build, so a layout the game
        /// changes is followed; Community Campaigns is drawn below the whole band and reads last.
        /// </summary>
        private List<KeyValuePair<string, IMenuButtonAdapter>> DrawnCards()
        {
            List<KeyValuePair<string, IMenuButtonAdapter>> band = new List<KeyValuePair<string, IMenuButtonAdapter>>();
            IReadOnlyList<CampaignButtonAdapter> campaigns = Live.CampaignButtons;
            for (int i = 0; campaigns != null && i < campaigns.Count; i++)
            {
                MenuCardPage.AddDrawn(band, "campaign:card/" + i, campaigns[i]);
            }

            MenuCardPage.AddDrawn<IMenuButtonAdapter>(band, "campaign:tales", Live.TalesButton);
            MenuCardPage.SortByDrawnLeft(band);
            MenuCardPage.AddDrawn<IMenuButtonAdapter>(band, "campaign:community", Live.CustomCampaignButton);
            return band;
        }
    }
}
