using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>The two turns a drawn table cell's text takes between the game's mesh and the node
    /// that speaks it. Four screens - the two lobby map tables, the online game list and the player
    /// stats page - all did both for themselves.</summary>
    public static class CellText
    {
        /// <summary>One spoken line out of what the page drew: the renderer's markup gone (a game
        /// list draws its player count with the game's own colour tags, "2/&lt;low&gt;4&lt;/low&gt;",
        /// which a screen reader must not spell out) and the break a prefab wraps a caption on
        /// ("Games\nPlayed: 5", a rendering accident rather than two things to say) joined back up.
        /// </summary>
        public static string Plain(string value)
        {
            return string.Join(" ", SpokenLines.Of(new[] { value }));
        }

        /// <summary>An empty cell reads the sheet's own blank word rather than being dropped, so the
        /// columns stay the same all the way down.</summary>
        public static string Filled(string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }

            return GraphSheet.BlankText != null ? GraphSheet.BlankText() : string.Empty;
        }
    }
}
