using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.UI
{
    /// <summary>What the lobby's two map pages - the map select table and the challenge table - are
    /// built out of. The two pages draw different columns over different game types, but a cell of
    /// either reads the same way and the preview panel beside either says the same three things, so
    /// the nodes are declared once here.
    /// </summary>
    public static class LobbyMapNodes
    {
        /// <summary>
        /// One read-only cell of a map table: the drawn value alone, since the column's caption is
        /// spoken as the edge crossed into it, with the caption and the value together as what the
        /// review buffer opens with - nobody arrives at a buffer across an edge. An empty cell reads
        /// the sheet's own blank word rather than being dropped, so the columns stay the same all the
        /// way down.
        ///
        /// EVERY CELL CARRIES THE ROW'S CLICK: a player reading across a map's size or player count
        /// and pressing Enter means "this map", and having to walk back to the Name column first is a
        /// rule the drawn table does not have - clicking anywhere on the row selects it. One search
        /// result per map too, whichever column the cursor is standing in.
        /// </summary>
        public static NodeVtable Cell(
            Func<string> caption,
            Func<string> value,
            Func<string> searchText,
            Action activate,
            Tooltip tooltip)
        {
            Func<string> text = () => CellText.Filled(value());
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(text, watch: false) },
                Sections = GraphNodes.Sections(null, tooltip),
                SearchText = searchText,
                BufferHead = () => ModText.Get(ModStrings.Common.ListSeparator, caption(), text()),
                OnActivate = activate,
            };
            GraphNodes.Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>
        /// The preview beside either table, as the one line it is: the map's name as the panel draws
        /// it, then the dossier it draws under it, read on arrival and held in the review buffer one
        /// drawn line at a time, a map's dossier running to a paragraph or more. The name is watched
        /// live, so the panel being refilled under a standing cursor says which map it is now showing;
        /// while the panel has drawn no name yet, the selected row's own name stands in.
        ///
        /// THE WIN CONDITIONS ARE NOT READ OUT. The panel draws them as ICONS whose words the game
        /// only reveals on hover (<c>LobbyMapPreview</c> hangs each icon's <c>GameModes/*/Name</c> and
        /// objective on it as a tooltip), so a sentence naming them is not something the page says: it
        /// is buffer-only, where the player who wants it goes to look. The dossier, by contrast, is
        /// drawn text (<c>LobbyMapPreviewText.GetInfo</c> reads the panel's own <c>_mpInfo</c> mesh)
        /// and stays in the readout.
        /// </summary>
        public static NodeVtable Preview(Func<string> panelTitle, Func<ILobbyMapRow> selectedRow)
        {
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => Title(panelTitle, selectedRow), live: true, kind: AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    NodeSection.Composed(() => SpokenLines.Of(new[] { Description(selectedRow) })),
                    NodeSection.Buffer(() => SpokenLines.Of(new[] { WinConditions(selectedRow) })),
                },
            };
        }

        private static string Title(Func<string> panelTitle, Func<ILobbyMapRow> selectedRow)
        {
            string title = panelTitle();
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            ILobbyMapRow selected = selectedRow();
            return selected != null ? selected.Name : string.Empty;
        }

        private static string Description(Func<ILobbyMapRow> selectedRow)
        {
            ILobbyMapRow selected = selectedRow();
            return selected != null ? selected.Description : string.Empty;
        }

        private static string WinConditions(Func<ILobbyMapRow> selectedRow)
        {
            ILobbyMapRow selected = selectedRow();
            return selected != null ? ModText.JoinList(selected.WinConditionLabels) : string.Empty;
        }
    }
}
