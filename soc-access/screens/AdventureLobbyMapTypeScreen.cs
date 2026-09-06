using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's map type page, made navigable as a graph in the shape its family's representative
    /// (<see cref="CampaignMenuScreen"/>) established: a stop of the cards the page is made of, then
    /// the header band above them.
    ///
    /// Measured 2026-09-06 at 1280x800: three cards in one band inside `MapTypeMenu` &gt;
    /// `Container` at x 221, 510 and 799 (Conquest maps, Challenge maps, Random maps), each drawing a
    /// sub-header, a name and a paragraph of description. The name leads the label with the
    /// sub-header after it, and the description is always drawn so it reads after the label. The
    /// declaration order in the adapter is not the drawn one (Challenge is declared last and drawn in
    /// the middle), so the band is sorted by measured left edge every build. Online the page draws
    /// only two of the cards; the same build serves it, because the cards are read off what is drawn.
    ///
    /// The header band is Back ("Main Menu", x 21, the lobby's own `CanvasForeground` &gt; `Header`)
    /// and Options (x 1233, the main menu's `UtilityButtons`), declared left to right.
    ///
    /// ESCAPE: neither `MapTypeMenu` nor `LobbyNavigation` registers an input callback (checked
    /// 2026-09-06 in `decompiled/Lavapotion.SongsOfConquest.UILayer.Runtime/`; `LobbyNavigation` only
    /// subscribes to the sub-menus' own OnCancel events), so the screen claims Escape and presses the
    /// drawn Back button, which leaves the lobby scene for the main menu.
    /// </summary>
    public sealed class AdventureLobbyMapTypeScreen : GraphScreen
    {
        private const string CardsStop = "map-type-cards";
        private const string HeaderStop = "map-type-header";

        private readonly AdventureLobbyMapTypeAdapter _adapter;

        public AdventureLobbyMapTypeScreen(AdventureLobbyMapTypeAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            AdventureLobbyMapTypeAdapter adapter = FindActiveMapTypeMenu();
            return adapter != null ? new AdventureLobbyMapTypeScreen(adapter) : null;
        }

        public override string Key
        {
            get { return "adventure-lobby-map-type"; }
        }

        /// <summary>The page's own drawn title ("Map type").</summary>
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

        /// <summary>The page hides its header band as it leaves for the next scene, and the cursor
        /// standing on a header button falls onto a card: that recovery is the page leaving, not a
        /// move.</summary>
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

            List<KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>> cards = DrawnCards();
            if (cards.Count > 0)
            {
                builder.BeginStop(CardsStop);
                foreach (KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter> card in cards)
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(card.Value.Button, card.Key),
                        Card(card.Value),
                        card.Value.Button));
                }
            }

            List<KeyValuePair<string, IMenuButtonAdapter>> header = new List<KeyValuePair<string, IMenuButtonAdapter>>(2);
            Add(header, "map-type:back", _adapter.BackButton);
            Add(header, "map-type:options", _adapter.OptionsButton);
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

        /// <summary>A card, activated through the game's own click; its always-drawn description
        /// follows the label, and is a review-buffer line by being a part.</summary>
        private static NodeVtable Card(AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter item)
        {
            NodeVtable vtable = GraphNodes.Button(item.GetLabel, () => item.Activate(), item.IsEnabled);
            vtable.Announcements.Add(GraphNodes.ValuePart(item.GetDescription, watch: false));
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(item.Button);
            return vtable;
        }

        /// <summary>The cards, in the order the page draws them: sorted by measured left edge every
        /// build, because the adapter declares Challenge last while the page draws it in the
        /// middle.</summary>
        private List<KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>> DrawnCards()
        {
            List<KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>> band =
                new List<KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>>();
            AddCard(band, "map-type:all-maps", _adapter.AllMapsButton);
            AddCard(band, "map-type:challenge-maps", _adapter.ChallengeMapsButton);
            AddCard(band, "map-type:random-maps", _adapter.RandomMapsButton);
            SortByDrawnLeft(band);
            return band;
        }

        private static void AddCard(
            List<KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>> list,
            string key,
            AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter item)
        {
            if (item != null && item.Button != null && item.IsVisible())
            {
                list.Add(new KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>(key, item));
            }
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
        private static void SortByDrawnLeft(
            List<KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter>> items)
        {
            List<float> lefts = new List<float>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                lefts.Add(Left(items[i].Value));
            }

            for (int i = 1; i < items.Count; i++)
            {
                KeyValuePair<string, AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter> moving = items[i];
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

        private static float Left(AdventureLobbyMapTypeAdapter.MapTypeMenuButtonAdapter item)
        {
            Component component = item.Button;
            return component != null ? component.transform.position.x : 0f;
        }

        private static AdventureLobbyMapTypeAdapter FindActiveMapTypeMenu()
        {
            MapTypeMenu[] menus = Resources.FindObjectsOfTypeAll<MapTypeMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MapTypeMenu menu = menus[i];
                if (!IsLiveSceneMapTypeMenu(menu))
                {
                    continue;
                }

                AdventureLobbyMapTypeAdapter adapter = new AdventureLobbyMapTypeAdapter(menu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneMapTypeMenu(MapTypeMenu menu)
        {
            if (menu == null)
            {
                return false;
            }

            GameObject gameObject = ((Component)menu).gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
