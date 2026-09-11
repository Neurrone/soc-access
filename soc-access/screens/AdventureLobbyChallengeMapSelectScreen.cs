using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The lobby's challenge map page, made navigable as a graph. The map select page's shape with
    /// the header band taken away: three stops, and Tab moves between them - the table of challenges,
    /// the preview panel beside it, and the page's buttons.
    ///
    /// Measured 2026-09-06 at 1280x800 through `/gui/unity`: `MapEntryContainer` at [87,96,858,613]
    /// holding `ChallengeMapEntry(Clone)` rows 48 px tall at x 95, each drawing its name (x 152) and
    /// its win-condition icons (x 739) and NOTHING ELSE - no heading band, no sort buttons, no
    /// filters; `LobbyMapPreview` at x 954 with the challenge's name at y 307, its dossier in a scroll
    /// rect at y 356 and the win-condition icons at y 287; Confirm at [982,679]; the lobby's Back at
    /// [21,20] and Options at [1233,11] in the header band, under the drawn title "Challenge maps".
    ///
    /// The table is a <see cref="GraphSheet"/> of one region with Name as its primary column and the
    /// win condition read as one piece per drawn icon, as on the map select page. Because the game
    /// draws NO heading band here, none is declared: the column captions live only as the edge labels
    /// the sheet speaks on the way into a column.
    ///
    /// Arriving on a row selects that challenge (the menu's own `SetSelectedEntry`, which fills the
    /// preview), so the row says "selected" for the challenge the page opened on and Enter is the
    /// same selection again.
    ///
    /// Escape: `ChallengeMapsMenu.SetupAndAnimateAfterLoad` registers only
    /// `InputActions.UI.Confirm` on its keyboard branch (decompiled, line 232) and `LobbyNavigation`
    /// registers no input callback at all, so the screen claims it and presses the drawn Back button.
    /// </summary>
    public sealed class AdventureLobbyChallengeMapSelectScreen : LiveScreen<AdventureLobbyChallengeMapSelectAdapter>
    {
        private const string TableStop = "challenge-map-table";
        private const string DetailsStop = "challenge-map-details";
        private const string ButtonsStop = "challenge-map-buttons";
        private const string SheetKey = "challenge-map:";

        // A subject of its own for the preview line, kept across rebuilds so the reconciler seats the
        // cursor on the same node while the selection under it changes.
        private readonly object _detailsMarker = new object();

        /// <summary>The lobby navigator's own challenge maps page (<see cref="LobbySources"/>).</summary>
        protected override object ResolveMenu()
        {
            return LobbySources.ChallengeMaps.Current;
        }

        protected override AdventureLobbyChallengeMapSelectAdapter Adapt(object menu)
        {
            return new AdventureLobbyChallengeMapSelectAdapter((ChallengeMapsMenu)menu, LobbySources.Navigation.Current);
        }

        public override string Key
        {
            get { return "challenge-map-select"; }
        }

        /// <summary>Layer 2: over the map type page that opens it.</summary>
        public override int Layer
        {
            get { return 2; }
        }

        /// <summary>The page's own drawn title ("Challenge maps").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return TableStop; }
        }

        public override bool ConsumesBack
        {
            get { return Live != null && Live.BackButton != null && Live.BackButton.IsVisible(); }
        }

        public override bool Back()
        {
            return Live != null && Live.BackButton != null && Live.BackButton.Activate();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TableStop);
            BuildTable(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        private void BuildTable(GraphBuilder builder)
        {
            string[] captions =
            {
                Live.NameColumnLabel,
                Live.WinConditionColumnLabel,
                Live.CompletedColumnLabel
            };

            GraphSheet sheet = new GraphSheet(builder, SheetKey);
            sheet.Region(Live.Title, captions);
            IReadOnlyList<AdventureLobbyChallengeMapRowAdapter> rows = Live.GetVisibleRows();
            object selected = null;
            for (int i = 0; i < rows.Count; i++)
            {
                AdventureLobbyChallengeMapRowAdapter row = rows[i];
                if (row == null || row.Entry == null)
                {
                    continue;
                }

                sheet.RowAt(Primary(row), row.NativeKey, Cells(row, captions), row.Entry);
                if (row.IsSelected)
                {
                    selected = row.NativeKey;
                }
            }

            sheet.Finish();
            if (sheet.FirstRow != null)
            {
                // Tab into the table lands on the selected map, else on the first one - never on the
                // heading band above them.
                builder.LandStopOn(sheet.RowId(selected) ?? sheet.FirstRow);
            }
        }

        /// <summary>The challenge's own cell: its name, whether it is the one the page has selected,
        /// and the game's own selection.</summary>
        private static NodeVtable Primary(AdventureLobbyChallengeMapRowAdapter row)
        {
            AdventureLobbyChallengeMapRowAdapter it = row;
            return new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    GraphNodes.LabelPart(() => it.Name),
                    GraphNodes.SelectedPart(() => it.IsSelected),
                },
                OnActivate = () => it.Select(),
                OnFocusVisual = it.FocusNative,
            };
        }

        private static List<GraphSheet.SheetCell> Cells(
            AdventureLobbyChallengeMapRowAdapter row,
            IReadOnlyList<string> captions)
        {
            List<GraphSheet.SheetCell> cells = new List<GraphSheet.SheetCell>();
            AdventureLobbyChallengeMapRowAdapter it = row;
            IReadOnlyList<string> conditions = row.WinConditionLabels;
            IReadOnlyList<Tooltip> tooltips = row.WinConditionTooltips;
            if (conditions.Count == 0)
            {
                cells.Add(new GraphSheet.SheetCell(1, 0, Cell(captions, 1, row, () => string.Empty, null)));
            }
            else
            {
                for (int i = 0; i < conditions.Count; i++)
                {
                    string condition = conditions[i];
                    cells.Add(new GraphSheet.SheetCell(1, i, Cell(
                        captions,
                        1,
                        row,
                        () => condition,
                        i < tooltips.Count ? tooltips[i] : null)));
                }
            }

            cells.Add(new GraphSheet.SheetCell(2, 0, Cell(
                captions,
                2,
                row,
                () => it.IsCompleted ? it.CompletedLabel : it.NotCompletedLabel,
                null)));
            return cells;
        }

        /// <summary>One read-only cell: the drawn value alone, the column's caption being spoken as
        /// the edge crossed into it, with the caption and the value as the buffer's head. Every cell
        /// carries the row's click, as the map select table's do: Enter anywhere along a row means
        /// that row.</summary>
        private static NodeVtable Cell(
            IReadOnlyList<string> captions,
            int column,
            AdventureLobbyChallengeMapRowAdapter row,
            Func<string> value,
            Tooltip tooltip)
        {
            AdventureLobbyChallengeMapRowAdapter it = row;
            string caption = captions != null && column < captions.Count ? captions[column] : string.Empty;
            Func<string> text = () => CellText.Filled(value());
            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement> { GraphNodes.ValuePart(text, watch: false) },
                Sections = GraphNodes.Sections(null, tooltip),
                SearchText = () => it.Name,
                BufferHead = () => ModText.Get(ModStrings.Common.ListSeparator, caption, text()),
                OnActivate = () => it.Select(),
            };
            GraphNodes.Aim(vtable, tooltip);
            return vtable;
        }

        /// <summary>The preview beside the table, as the one line it is: the challenge's name as the
        /// panel draws it, watched live, with the dossier as a section - read on arrival and held in
        /// the review buffer one drawn line at a time.
        ///
        /// THE WIN CONDITIONS ARE NOT READ OUT, as on the map select page: the panel draws them as
        /// ICONS whose words the game only reveals on hover, so they are buffer-only, where the
        /// player who wants them goes to look. The dossier, by contrast, is drawn text
        /// (<c>LobbyMapPreviewText.GetInfo</c>) and stays in the readout.</summary>
        private void BuildDetails(GraphBuilder builder)
        {
            AdventureLobbyChallengeMapRowAdapter selected = Live.SelectedRow;
            if (selected == null)
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => PreviewTitle(), live: true, kind: AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    NodeSection.Composed(() => SpokenLines.Of(new[] { Description() })),
                    NodeSection.Buffer(() => SpokenLines.Of(new[] { PreviewWinConditions() })),
                },
            };
            builder.AddItem(new SyntheticNode(
                ControlId.For(_detailsMarker, "challenge-map:preview"),
                vtable));
        }

        private string PreviewTitle()
        {
            string title = Live.PreviewTitle;
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            AdventureLobbyChallengeMapRowAdapter selected = Live.SelectedRow;
            return selected != null ? selected.Name : string.Empty;
        }

        private string Description()
        {
            AdventureLobbyChallengeMapRowAdapter selected = Live.SelectedRow;
            return selected != null ? selected.Description : string.Empty;
        }

        private string PreviewWinConditions()
        {
            AdventureLobbyChallengeMapRowAdapter selected = Live.SelectedRow;
            return selected != null ? ModText.JoinList(selected.WinConditionLabels) : string.Empty;
        }

        private void BuildButtons(GraphBuilder builder)
        {
            // Back (x 21) and Options (x 1233) in the header band, then Confirm at the bottom right.
            AddButton(builder, "challenge-map:back", Live.BackButton);
            AddButton(builder, "challenge-map:options", Live.OptionsButton);
            AddButton(builder, "challenge-map:confirm", Live.ConfirmButton);
        }

        private static void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(button.GetLabel, () => button.Activate(), button.IsEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(button.Button);
            builder.AddItem(new DrawnNode(ControlId.For(button.Button, key), vtable, button.Button));
        }
    }
}
