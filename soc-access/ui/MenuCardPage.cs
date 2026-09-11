using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// A MAIN-MENU CARD PAGE, AS GRAPH NODES.
    ///
    /// The campaign menu, the tale select page and the community campaigns page are the same page
    /// three times: a band of cards read in the order the game draws them, leftmost first, and the
    /// main menu's own header band after it with Back at the left edge and Options at the right.
    /// What a card is, and what it says when it is read, is the vtable each screen passes in; the
    /// band, its order, the two stops and the header are here, so the three cannot drift apart.
    ///
    /// A card or a header button is in the band when the game is DRAWING it: it exists, it has a
    /// drawn button, and it says it is visible. Whether it is enabled is a separate question its own
    /// node answers, so a drawn button the game refuses stays in the tree and is marked disabled.
    ///
    /// Every node id is minted by the screen: the key it passes with each card is what the id is
    /// built from.
    /// </summary>
    public static class MenuCardPage
    {
        /// <summary>Appends one card to the band if the game is drawing it.</summary>
        public static void AddDrawn<T>(List<KeyValuePair<string, T>> band, string key, T item)
            where T : class, IMenuButtonAdapter
        {
            if (item != null && item.Button != null && item.IsVisible())
            {
                band.Add(new KeyValuePair<string, T>(key, item));
            }
        }

        /// <summary>The same, for a card that is not an <see cref="IMenuButtonAdapter"/> and answers
        /// the two questions through its own members.</summary>
        public static void AddDrawn<T>(
            List<KeyValuePair<string, T>> band,
            string key,
            T item,
            Func<T, Component> button,
            Func<T, bool> isVisible)
            where T : class
        {
            if (item != null && button(item) != null && isVisible(item))
            {
                band.Add(new KeyValuePair<string, T>(key, item));
            }
        }

        /// <summary>Orders the band by drawn left edge, leftmost first, so a layout the game changes
        /// is followed.</summary>
        public static void SortByDrawnLeft<T>(List<KeyValuePair<string, T>> band)
            where T : class, IMenuButtonAdapter
        {
            SortByDrawnLeft(band, ButtonOf);
        }

        /// <summary>Insertion sort by drawn left edge; stable, so two cards at one x keep declaration
        /// order.</summary>
        public static void SortByDrawnLeft<T>(List<KeyValuePair<string, T>> band, Func<T, Component> button)
            where T : class
        {
            DrawnOrder.SortByLeft(band, card => button(card.Value));
        }

        /// <summary>The card band as one stop, in the order the band is already in. An empty band
        /// opens no stop at all.</summary>
        public static void BuildCards<T>(
            GraphBuilder builder,
            object stop,
            List<KeyValuePair<string, T>> cards,
            Func<T, NodeVtable> card)
            where T : class, IMenuButtonAdapter
        {
            BuildCards(builder, stop, cards, ButtonOf, card);
        }

        public static void BuildCards<T>(
            GraphBuilder builder,
            object stop,
            List<KeyValuePair<string, T>> cards,
            Func<T, Component> button,
            Func<T, NodeVtable> card)
            where T : class
        {
            if (cards.Count == 0)
            {
                return;
            }

            builder.BeginStop(stop);
            foreach (KeyValuePair<string, T> item in cards)
            {
                Component drawn = button(item.Value);
                builder.AddItem(new DrawnNode(
                    ControlId.For(drawn, item.Key),
                    card(item.Value),
                    drawn));
            }
        }

        /// <summary>The main menu's header band: Back at the left edge and Options at the right,
        /// declared left to right, the order they are drawn in. An empty band opens no stop.
        /// </summary>
        public static void BuildHeader(
            GraphBuilder builder,
            object stop,
            string backKey,
            IMenuButtonAdapter back,
            string optionsKey,
            IMenuButtonAdapter options)
        {
            List<KeyValuePair<string, IMenuButtonAdapter>> header =
                new List<KeyValuePair<string, IMenuButtonAdapter>>(2);
            AddDrawn(header, backKey, back);
            AddDrawn(header, optionsKey, options);
            BuildCards(builder, stop, header, HeaderButton);
        }

        private static NodeVtable HeaderButton(IMenuButtonAdapter item)
        {
            return GraphNodes.Button(item.GetLabel, () => item.Activate(), item.IsEnabled);
        }

        private static Component ButtonOf<T>(T item) where T : class, IMenuButtonAdapter
        {
            return item.Button;
        }
    }
}
