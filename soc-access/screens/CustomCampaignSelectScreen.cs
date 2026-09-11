using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The community campaigns page, made navigable as a graph in the shape its family's
    /// representative (<see cref="CampaignMenuScreen"/>) established: a stop of the cards the page is
    /// made of, then the header band above them.
    ///
    /// Measured 2026-09-06 at 1280x800: four cards in one band inside `Canvas` &gt; `Menu` &gt;
    /// `Scroll View`, at x 74, 362, 650 and 939. The first three are campaign entries drawing a
    /// title, a paragraph of description and a button ("DOWNLOAD CAMPAIGN"); the fourth is the
    /// download tip, which draws no title at all - only its sentence and a "Find More" button. So a
    /// campaign card is labelled with its title and the button's text is its status, while the tip is
    /// labelled with its button ("Find More") and its sentence reads after the label, both of them in
    /// the family's order of label, then always-drawn text, then status.
    ///
    /// The header band is the main menu's own: Back at x 21 and Options at x 1233, declared left to
    /// right.
    ///
    /// ESCAPE: `CustomCampaignSelectMenuBehavior` registers no input callback of any kind (checked
    /// 2026-09-06 in `decompiled/Lavapotion.SongsOfConquest.UILayer.Runtime/`), so Escape would do
    /// nothing here; the screen claims it and presses the drawn Back button.
    /// </summary>
    public sealed class CustomCampaignSelectScreen : LiveScreen<CustomCampaignSelectAdapter>
    {
        private const string CardsStop = "custom-campaign-cards";
        private const string HeaderStop = "custom-campaign-header";

        protected override object ResolveMenu()
        {
            return MainMenuSources.CustomCampaignSelect.Current;
        }

        protected override CustomCampaignSelectAdapter Adapt(object menu)
        {
            return new CustomCampaignSelectAdapter((CustomCampaignSelectMenuBehavior)menu);
        }

        public override string Key
        {
            get { return "custom-campaign-select"; }
        }

        /// <summary>Layer 1: a main-menu page, over the main menu.</summary>
        public override int Layer
        {
            get { return 1; }
        }

        /// <summary>The page's own drawn title ("Community Campaigns").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.GetTitle() : null; }
        }

        public override object InitialFocusStop
        {
            get { return CardsStop; }
        }

        /// <summary>The page hides its header band as it closes, and the cursor standing on a header
        /// button falls onto a card: that recovery is the page leaving, not a move.</summary>
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

            MenuCardPage.BuildCards(builder, CardsStop, DrawnCards(), CardButton, Card);
            MenuCardPage.BuildHeader(
                builder,
                HeaderStop,
                "custom-campaign:back",
                Live.BackButton,
                "custom-campaign:options",
                Live.OptionsButton);
        }

        /// <summary>
        /// A card, activated through the game's own click. Its always-drawn description follows the
        /// label, and its status line - the button's own text and, while a download is running, the
        /// installation line the card draws over itself - is watched live, because that line is what
        /// the game changes under the cursor as a download proceeds.
        /// </summary>
        private NodeVtable Card(CustomCampaignEntryAdapter item)
        {
            bool isTip = ReferenceEquals(item, Live.DownloadTip);
            NodeVtable vtable = GraphNodes.Button(
                isTip ? (System.Func<string>)item.GetActionText : item.GetTitle,
                () => item.Activate(),
                item.IsEnabled);
            // A part per paragraph: one spoken line, and one review-buffer line each. The tip card
            // draws its title under its action text, so the title leads its own paragraphs there.
            GraphNodes.ParagraphParts(
                vtable,
                isTip
                    ? (System.Func<IList<string>>)(() => SpokenLines.Of(new[] { item.GetTitle(), item.GetDescription() }))
                    : item.GetDescriptionLines);
            if (!isTip)
            {
                vtable.Announcements.Add(new NodeAnnouncement(
                    () => JoinNativeLines(item.GetActionText(), item.GetInstallationText()),
                    live: true,
                    kind: AnnouncementKinds.Enabled));
            }

            vtable.OnFocusVisual = item.FocusNative;
            return vtable;
        }

        /// <summary>The cards, in the order the page draws them: the campaign entries and the
        /// download tip share one band, sorted by their measured left edge every build.</summary>
        private List<KeyValuePair<string, CustomCampaignEntryAdapter>> DrawnCards()
        {
            List<KeyValuePair<string, CustomCampaignEntryAdapter>> band =
                new List<KeyValuePair<string, CustomCampaignEntryAdapter>>();
            IReadOnlyList<CustomCampaignEntryAdapter> entries = Live.CampaignEntries;
            for (int i = 0; entries != null && i < entries.Count; i++)
            {
                MenuCardPage.AddDrawn(band, "custom-campaign:card/" + i, entries[i], CardButton, CardVisible);
            }

            MenuCardPage.AddDrawn(band, "custom-campaign:find-more", Live.DownloadTip, CardButton, CardVisible);
            MenuCardPage.SortByDrawnLeft(band, CardButton);
            return band;
        }

        /// <summary>The card's drawn button, which is what the node stands on: a community campaign
        /// entry is not an <see cref="IMenuButtonAdapter"/>, so the band is told where to look.
        /// </summary>
        private static Component CardButton(CustomCampaignEntryAdapter item)
        {
            return item.Button;
        }

        private static bool CardVisible(CustomCampaignEntryAdapter item)
        {
            return item.IsVisible();
        }

        /// <summary>The card's own lines, joined as lines rather than as a sentence: they are the
        /// game's text and the card draws them one under the other.</summary>
        private static string JoinNativeLines(params string[] parts)
        {
            List<string> lines = new List<string>(parts != null ? parts.Length : 0);
            for (int i = 0; parts != null && i < parts.Length; i++)
            {
                string part = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (part.Length > 0)
                {
                    lines.Add(part);
                }
            }

            return lines.Count == 0 ? string.Empty : string.Join("\n", lines.ToArray());
        }
    }
}
