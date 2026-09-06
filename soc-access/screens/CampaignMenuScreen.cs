using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

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
    public sealed class CampaignMenuScreen : GraphScreen
    {
        private const string CardsStop = "campaign-cards";
        private const string HeaderStop = "campaign-header";

        private readonly CampaignMenuAdapter _adapter;

        public CampaignMenuScreen(CampaignMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CampaignMenuAdapter adapter = FindActiveCampaignMenu();
            return adapter != null ? new CampaignMenuScreen(adapter) : null;
        }

        public override string Key
        {
            get { return "campaign-menu"; }
        }

        /// <summary>The page's own drawn title ("Choose Campaign or Tale").</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.GetTitle() : null; }
        }

        public override object InitialFocusStop
        {
            get { return CardsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.IsVisible(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.Activate();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            List<KeyValuePair<string, IMenuButtonAdapter>> cards = DrawnCards();
            if (cards.Count > 0)
            {
                builder.BeginStop(CardsStop);
                foreach (KeyValuePair<string, IMenuButtonAdapter> card in cards)
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(card.Value.Button, card.Key),
                        Card(card.Value),
                        card.Value.Button));
                }
            }

            // Back at x 21 and Options at x 1233 of the header band: declared left to right, the
            // order they are drawn in.
            List<KeyValuePair<string, IMenuButtonAdapter>> header = new List<KeyValuePair<string, IMenuButtonAdapter>>(2);
            Add(header, "campaign:back", _adapter.BackButton);
            Add(header, "campaign:options", _adapter.OptionsButton);
            if (header.Count > 0)
            {
                builder.BeginStop(HeaderStop);
                foreach (KeyValuePair<string, IMenuButtonAdapter> button in header)
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(button.Value.Button, button.Key),
                        GraphNodes.Button(button.Value.GetLabel, () => button.Value.Activate(), button.Value.IsEnabled),
                        button.Value.Button));
                }
            }
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
            Func<string> description = campaign != null ? (Func<string>)campaign.GetDescription : null;
            // No details section beside these: an announcement part is a buffer line already, so a
            // section repeating the description and the progress line would put each of them in the
            // review buffer twice (measured on the first build of this screen).
            NodeVtable vtable = GraphNodes.Button(item.GetLabel, () => item.Activate(), item.IsEnabled);
            if (description != null)
            {
                vtable.Announcements.Add(GraphNodes.ValuePart(description, watch: false));
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

        /// <summary>
        /// The cards, in the order the page draws them. The campaign cards and the Tales card share
        /// one band and are sorted by their measured left edge every build, so a layout the game
        /// changes is followed; Community Campaigns is drawn below the whole band and reads last.
        /// </summary>
        private List<KeyValuePair<string, IMenuButtonAdapter>> DrawnCards()
        {
            List<KeyValuePair<string, IMenuButtonAdapter>> band = new List<KeyValuePair<string, IMenuButtonAdapter>>();
            IReadOnlyList<CampaignButtonAdapter> campaigns = _adapter.CampaignButtons;
            for (int i = 0; campaigns != null && i < campaigns.Count; i++)
            {
                Add(band, "campaign:card/" + i, campaigns[i]);
            }

            Add(band, "campaign:tales", _adapter.TalesButton);
            SortByDrawnLeft(band);
            Add(band, "campaign:community", _adapter.CustomCampaignButton);
            return band;
        }

        private static void Add(List<KeyValuePair<string, IMenuButtonAdapter>> list, string key, IMenuButtonAdapter item)
        {
            if (item != null && item.Button != null && item.IsVisible())
            {
                list.Add(new KeyValuePair<string, IMenuButtonAdapter>(key, item));
            }
        }

        // Insertion sort by drawn left edge, leftmost first; stable, so two cards at one x keep
        // declaration order.
        private static void SortByDrawnLeft(List<KeyValuePair<string, IMenuButtonAdapter>> items)
        {
            List<float> lefts = new List<float>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                lefts.Add(Left(items[i].Value));
            }

            for (int i = 1; i < items.Count; i++)
            {
                KeyValuePair<string, IMenuButtonAdapter> moving = items[i];
                float left = lefts[i];
                int j = i - 1;
                while (j >= 0 && lefts[j] > left)
                {
                    items[j + 1] = items[j];
                    lefts[j + 1] = lefts[j];
                    j--;
                }

                items[j + 1] = moving;
                lefts[j + 1] = left;
            }
        }

        private static float Left(IMenuButtonAdapter item)
        {
            Component component = item.Button;
            return component != null ? component.transform.position.x : 0f;
        }

        private static CampaignMenuAdapter FindActiveCampaignMenu()
        {
            CampaignMenu[] campaignMenus = Resources.FindObjectsOfTypeAll<CampaignMenu>();
            for (int i = 0; i < campaignMenus.Length; i++)
            {
                CampaignMenu campaignMenu = campaignMenus[i];
                if (!IsLiveSceneCampaignMenu(campaignMenu))
                {
                    continue;
                }

                CampaignMenuAdapter adapter = new CampaignMenuAdapter(campaignMenu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneCampaignMenu(CampaignMenu campaignMenu)
        {
            if (campaignMenu == null)
            {
                return false;
            }

            GameObject gameObject = campaignMenu.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
