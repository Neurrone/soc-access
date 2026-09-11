using System;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// THE FIVE ESSENCE COUNTERS a commander's HUD draws, as one region named by the game's own
    /// Essences caption - the adventure map's band under the wielder portrait and each side's band in
    /// combat are the same five rows off two different adapters.
    ///
    /// The order is the game's own (Order, Creation, Chaos, Arcana, Destruction) rather than the
    /// enum's declaration order, so the rows read as the HUD draws them.
    /// </summary>
    public static class EssenceRows
    {
        public static readonly EssenceType[] RowOrder =
        {
            EssenceType.Order,
            EssenceType.Creation,
            EssenceType.Chaos,
            EssenceType.Arcana,
            EssenceType.Destruction
        };

        /// <summary>The band, into whatever stop the caller has opened. <paramref name="keyPrefix"/>
        /// is the screen's, and is what the region and every row id are built from. A caption the
        /// game has no text for opens no context.</summary>
        public static void Build(
            GraphBuilder builder,
            string keyPrefix,
            Func<EssenceType, string> label,
            Func<EssenceType, Tooltip> tooltip,
            Action<EssenceType> focus)
        {
            string caption = GameText.Get("Common/CommanderInventory/Essences", string.Empty);
            bool named = !string.IsNullOrWhiteSpace(caption);
            if (named)
            {
                builder.PushContext(caption);
            }

            builder.SetRegion(keyPrefix + "essences");
            for (int i = 0; i < RowOrder.Length; i++)
            {
                EssenceType essence = RowOrder[i];
                NodeVtable vtable = GraphNodes.Text(() => label(essence), null, tooltip(essence));
                vtable.OnFocusVisual = () => focus(essence);
                builder.AddItem(new SyntheticNode(ControlId.Structural(keyPrefix + "essence:" + essence), vtable));
            }

            builder.SetRegion(null);
            if (named)
            {
                builder.PopContext();
            }
        }
    }
}
