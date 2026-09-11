using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// One community map or mod's details page, made navigable as a graph. Four places to be, and Tab
    /// moves between them: the side panel's commands, the facts and categories under them, the prose
    /// in the main view, and the way back.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: a side panel down the right
    /// ([873,0,403,800]) drawing the mod's name at y 133, the Subscribe button at [873,213,361,43],
    /// the vote pair at y 283 ("Vote Up" at [874,283,153,32] and "Vote Down" at [1038,283,153,32],
    /// each carrying its count), a <c>Mod Stats</c> block from y 341 to y 461 and the tags at y 488;
    /// and a main view holding the Back prompt at [50,24,74,27], the picture gallery, and
    /// <c>Verbose Details</c> at y 633 - the summary at y 633 over a "Full description" heading at
    /// y 683 with the description under it.
    ///
    /// THE FACTS ARE A LIST, NOT A TABLE. <c>Mod Stats</c> draws five label/value pairs in two aligned
    /// columns (labels at x 873, values at x 1043) with no heading band over them, so they are
    /// read-only rows whose value is a value part - the shape every labelled fact in this mod takes -
    /// rather than a <c>GraphSheet</c>.
    ///
    /// Escape is CLAIMED and runs the panel's own <c>Close</c>, which is exactly what mod.io's
    /// <c>Navigating.Cancel</c> reaches while this panel is up (decompiled).
    /// </summary>
    public sealed class CommunityMapsDetailsScreen : LiveScreen<CommunityMapsDetailsAdapter>
    {
        private const string CommandsStop = "community-maps-details-commands";
        private const string FactsStop = "community-maps-details-facts";
        private const string TextStop = "community-maps-details-text";
        private const string FooterStop = "community-maps-details-footer";

        /// <summary>The details page, read off mod.io's own singleton every frame
        /// (<see cref="CommunityMapsSources"/>).</summary>
        protected override object ResolveMenu()
        {
            return CommunityMapsSources.Details;
        }

        protected override CommunityMapsDetailsAdapter Adapt(object menu)
        {
            return new CommunityMapsDetailsAdapter((ModIOBrowser.Implementation.Details)menu);
        }

        public override string Key
        {
            get { return "community-maps-details"; }
        }

        /// <summary>Layer 3: over the collection page that opens it.</summary>
        public override int Layer
        {
            get { return 3; }
        }

        /// <summary>The mod's own name, as the panel draws it.</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        public override object InitialFocusStop
        {
            get { return CommandsStop; }
        }

        public override bool ConsumesBack
        {
            get { return IsActive(); }
        }

        public override bool Back()
        {
            return Live != null && Live.Close();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(CommandsStop);
            BuildCommands(builder);

            builder.BeginStop(FactsStop);
            BuildFacts(builder);
            BuildTags(builder);

            builder.BeginStop(TextStop);
            BuildProse(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);
        }

        // ---- the side panel's commands ----

        private void BuildCommands(GraphBuilder builder)
        {
            string title = Live.Title;
            if (!string.IsNullOrWhiteSpace(title))
            {
                builder.AddItem(Synthetic("title", GraphNodes.Text(() => Live.Title)));
            }

            if (!string.IsNullOrWhiteSpace(Live.SubscribeLabel))
            {
                NodeVtable subscribe = GraphNodes.Button(
                    () => Live.SubscribeLabel,
                    () => Live.Subscribe());
                builder.AddItem(Synthetic("subscribe", subscribe));
            }

            NodeVtable downloads = GraphNodes.Button(
                () => Live.DownloadsLabel,
                () => Live.OpenDownloadsMenu(),
                () => Live.HasDownloadsMenu);
            builder.AddItem(Synthetic("downloads", downloads));

            IReadOnlyList<CommunityMapsDetailsAdapter.ActionItem> votes = Live.GetVoteActions();
            for (int i = 0; i < votes.Count; i++)
            {
                CommunityMapsDetailsAdapter.ActionItem vote = votes[i];
                if (vote == null)
                {
                    continue;
                }

                CommunityMapsDetailsAdapter.ActionItem captured = vote;
                NodeVtable vtable = GraphNodes.Button(
                    () => captured.Label,
                    () => { if (captured.Activate != null) { captured.Activate(); } });
                // The vote the player has already cast is marked on the drawn button; its count is
                // beside the label, and both are watched because a vote changes them under the cursor.
                vtable.Announcements.Add(GraphNodes.SelectedPart(() => captured.IsSelected));
                vtable.Announcements.Add(GraphNodes.ValuePart(() => captured.Status));
                builder.AddItem(Synthetic("vote/" + captured.Id, vtable));
            }

            if (!string.IsNullOrWhiteSpace(Live.ReportLabel))
            {
                NodeVtable report = GraphNodes.Button(
                    () => Live.ReportLabel,
                    () => Live.Report());
                builder.AddItem(Synthetic("report", report));
            }
        }

        // ---- the facts and the categories ----

        private void BuildFacts(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsDetailsAdapter.DetailItem> details = Live.GetDetails();
            if (details.Count == 0)
            {
                return;
            }

            builder.PushContext(ModText.Get(ModStrings.UI.ColumnDetails));
            builder.SetRegion("community-maps-details:facts");
            for (int i = 0; i < details.Count; i++)
            {
                CommunityMapsDetailsAdapter.DetailItem detail = details[i];
                if (detail == null)
                {
                    continue;
                }

                CommunityMapsDetailsAdapter.DetailItem captured = detail;
                NodeVtable vtable = GraphNodes.Text(() => captured.Label);
                vtable.Announcements.Add(GraphNodes.ValuePart(() => captured.Value));
                builder.AddItem(Synthetic("fact/" + captured.Id, vtable));
            }

            builder.PopContext();
            builder.SetRegion(null);
        }

        private void BuildTags(GraphBuilder builder)
        {
            IReadOnlyList<CommunityMapsDetailsAdapter.TagItem> tags = Live.GetTags();
            if (tags.Count == 0)
            {
                return;
            }

            builder.PushContext(ModText.Get(ModStrings.Screens.Categories));
            builder.SetRegion("community-maps-details:tags");
            for (int i = 0; i < tags.Count; i++)
            {
                CommunityMapsDetailsAdapter.TagItem tag = tags[i];
                if (tag == null)
                {
                    continue;
                }

                CommunityMapsDetailsAdapter.TagItem captured = tag;
                builder.AddItem(Synthetic(
                    "tag/" + captured.Index,
                    GraphNodes.Text(() => captured.Label)));
            }

            builder.PopContext();
            builder.SetRegion(null);
        }

        // ---- the prose in the main view ----

        private void BuildProse(GraphBuilder builder)
        {
            AddParagraph(builder, "summary", null, Live.Summary);
            AddParagraph(builder, "description", Live.DescriptionLabel, Live.Description);
        }

        /// <summary>
        /// A drawn block of prose as ONE node: its heading, or its first line where the block has no
        /// heading, is the label, and the prose under it READS AFTER THE LABEL as a value part - the
        /// rule the campaign cards set, because the paragraph is always drawn and a player standing on
        /// "Full description" who hears only those two words has been told nothing.
        ///
        /// ONE PART PER DRAWN LINE, and no section beside them: a part is a review-buffer line
        /// already, so the buffer still gives the block a line at a time, and a section repeating the
        /// same lines would put every one of them in the buffer twice (the rule the campaign cards
        /// wrote down, and the engine's own ruling that what is spoken beside the name is a buffer
        /// line by construction).
        /// </summary>
        private void AddParagraph(GraphBuilder builder, string key, string heading, string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            // Nothing on this page changes under the cursor - a details page is opened for one mod
            // and closed again - so the block is split into paragraphs when it is first READ and the
            // lines are read off that snapshot after. A description can run to several hundred words
            // and the page draws four of these, so splitting them all on every build was the page's
            // whole cost (AGENTS.md, Performance).
            IList<string> lines = null;
            Func<IList<string>> read = () => lines ?? (lines = SpokenLines.Of(new[] { text }));
            NodeVtable vtable;
            if (string.IsNullOrWhiteSpace(heading))
            {
                vtable = GraphNodes.Paragraphs(read);
            }
            else
            {
                string label = heading;
                vtable = GraphNodes.Text(() => label);
                GraphNodes.ParagraphParts(vtable, read);
            }

            builder.AddItem(Synthetic(key, vtable));
        }

        // ---- the way back ----

        private void BuildFooter(GraphBuilder builder)
        {
            NodeVtable back = GraphNodes.Button(
                () => Live.BackLabel,
                () => Live.Close());
            builder.AddItem(Synthetic("back", back));
        }

        private SyntheticNode Synthetic(string key, NodeVtable vtable)
        {
            return new SyntheticNode(
                ControlId.For(Marker(key), "community-maps-details:" + key),
                vtable);
        }
    }
}
