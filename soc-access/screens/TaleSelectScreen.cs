using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The tale select page, made navigable as a graph, in the shape its family's representative
    /// (<see cref="CampaignMenuScreen"/>) established: two stops, the cards the page is made of and
    /// the header band above them.
    ///
    /// The cards are read in the order the game draws them, measured every build off their left
    /// edges (2026-09-06 at 1280x800: x 113, 355, 586, 827, 1064, 1300 and 1536 inside
    /// `Canvas` &gt; `TaleSelectMenu` &gt; `Scroll View`, wider than the window, so the last two are
    /// scrolled to). A card draws a name, a paragraph of description under it and a progress line
    /// ("Completed: 0 / 2 missions"); the description is always on the screen, so it reads after the
    /// label, and the progress line is declared with the availability state because it is also what
    /// the game says about a card it refuses (a tale in a DLC the account does not own draws its
    /// purchase text there instead).
    ///
    /// The header band is the main menu's own, shared with the campaign menu: Back at x 21 and
    /// Options at x 1233, declared left to right.
    ///
    /// ESCAPE: neither `TaleButton` nor `TaleButtonLayoutCoordinator` registers any input callback
    /// (checked 2026-09-06 in `decompiled/Lavapotion.SongsOfConquest.UILayer.Runtime/`), so Escape
    /// would do nothing here; the screen claims it and presses the drawn Back button, as the widget
    /// screen it replaces did.
    /// </summary>
    public sealed class TaleSelectScreen : GraphScreen
    {
        private const string CardsStop = "tale-cards";
        private const string HeaderStop = "tale-header";

        private readonly TaleSelectAdapter _adapter;

        public TaleSelectScreen(TaleSelectAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            TaleSelectAdapter adapter = FindActiveTaleSelect();
            return adapter != null ? new TaleSelectScreen(adapter) : null;
        }

        public override string Key
        {
            get { return "tale-select"; }
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

        /// <summary>The page hides its header band as it closes, and the cursor standing on a header
        /// button falls onto a card: that recovery is the page leaving, not a move, and stays silent
        /// while the band is gone.</summary>
        public override bool IsWorkable
        {
            get { return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.IsVisible(); }
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

            List<KeyValuePair<string, TaleButtonAdapter>> cards = DrawnCards();
            if (cards.Count > 0)
            {
                builder.BeginStop(CardsStop);
                foreach (KeyValuePair<string, TaleButtonAdapter> card in cards)
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(card.Value.Button, card.Key),
                        Card(card.Value),
                        card.Value.Button));
                }
            }

            List<KeyValuePair<string, IMenuButtonAdapter>> header = new List<KeyValuePair<string, IMenuButtonAdapter>>(2);
            Add(header, "tale:back", _adapter.BackButton);
            Add(header, "tale:options", _adapter.OptionsButton);
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
        /// A card, activated through the game's own click. The description reads after the label and
        /// is a review-buffer line by being a part; no details section beside it, because an
        /// announcement part is a buffer line already and a section would put it there twice.
        /// </summary>
        private static NodeVtable Card(TaleButtonAdapter item)
        {
            NodeVtable vtable = GraphNodes.Button(item.GetLabel, () => item.Activate(), item.IsEnabled);
            vtable.Announcements.Add(GraphNodes.ValuePart(item.GetDescription, watch: false));

            // Watched live, as the campaign cards' is: the page is ready before the game has read the
            // campaign state back, so a card focused on arrival has no progress line yet.
            vtable.Announcements.Add(new NodeAnnouncement(item.GetStatus, live: true, kind: AnnouncementKinds.Enabled));

            // The card the cursor is on is the card the game raises: TaleButton's own hover is what
            // paints it, and the adapter already offers that path.
            vtable.OnFocusVisual = item.FocusNative;
            return vtable;
        }

        /// <summary>The cards, in the order the page draws them: sorted by their measured left edge
        /// every build, so a layout the game changes is followed.</summary>
        private List<KeyValuePair<string, TaleButtonAdapter>> DrawnCards()
        {
            List<KeyValuePair<string, TaleButtonAdapter>> band = new List<KeyValuePair<string, TaleButtonAdapter>>();
            IReadOnlyList<TaleButtonAdapter> tales = _adapter.Tales;
            for (int i = 0; tales != null && i < tales.Count; i++)
            {
                TaleButtonAdapter tale = tales[i];
                if (tale != null && tale.Button != null && tale.IsVisible())
                {
                    band.Add(new KeyValuePair<string, TaleButtonAdapter>("tale:card/" + i, tale));
                }
            }

            SortByDrawnLeft(band);
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
        private static void SortByDrawnLeft(List<KeyValuePair<string, TaleButtonAdapter>> items)
        {
            List<float> lefts = new List<float>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                lefts.Add(Left(items[i].Value));
            }

            for (int i = 1; i < items.Count; i++)
            {
                KeyValuePair<string, TaleButtonAdapter> moving = items[i];
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

        private static float Left(TaleButtonAdapter item)
        {
            Component component = item.Button;
            return component != null ? component.transform.position.x : 0f;
        }

        private static TaleSelectAdapter FindActiveTaleSelect()
        {
            TaleButtonLayoutCoordinator[] coordinators = Resources.FindObjectsOfTypeAll<TaleButtonLayoutCoordinator>();
            for (int i = 0; i < coordinators.Length; i++)
            {
                TaleButtonLayoutCoordinator coordinator = coordinators[i];
                if (!IsLiveSceneCoordinator(coordinator))
                {
                    continue;
                }

                TaleSelectAdapter adapter = new TaleSelectAdapter(coordinator);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneCoordinator(TaleButtonLayoutCoordinator coordinator)
        {
            if (coordinator == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)coordinator).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
