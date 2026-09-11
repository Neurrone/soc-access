using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The story text the game shows between scenes - the panel, the letterbox and the dialogue line
    /// - made navigable as a graph. One stop holding ONE BUTTON (owner ruling 2026-09-07): the
    /// speaker or heading and the first paragraph as "{speaker}: {text}", the further paragraphs as
    /// parts, so the review buffer holds one line per paragraph. Every part is live: the game writes
    /// the next line of a dialogue in place under a cursor that never moves, and the live watch is
    /// what reads it, speaker first, then the text. The headerless variants read the body alone.
    ///
    /// The paragraphs survive because the adapters keep the game's own line breaks
    /// (<c>IStoryTextAdapter.BodyLines</c>).
    ///
    /// ENTER advances, through the game's own handler that a click would reach
    /// (<c>AbortCurrentState</c> on the two story texts, <c>HandlePrimaryClicked</c> on the dialogue
    /// menu). ESCAPE is the game's on all three sources: each binds it to advancing, and every key
    /// the navigator does not claim reaches the game's press-anything handling unchanged.
    /// </summary>
    public sealed class StoryTextScreen : LiveScreen<IStoryTextAdapter>
    {
        private const string StoryStop = "story-text";

        // A subject of its own for the node: the sources draw the text in meshes the adapters do not
        // hand out.
        private readonly object _bodyKey = new object();

        // The three sources that draw the story text, each resolved from the adventure scene's
        // container and adapted once per object: the letterbox band, the lore panel and the dialogue
        // menu. The page belongs to whichever is drawing now, asked in the order the detector's own
        // handlers used to write them.
        private readonly AdaptedSource<ILetterboxStoryText, IStoryTextAdapter> _letterbox =
            new AdaptedSource<ILetterboxStoryText, IStoryTextAdapter>(
                ScreenSource<ILetterboxStoryText>.FromScene(LoadedScenes.AdventureScene),
                storyText => new LetterboxStoryTextAdapter((LetterboxStoryText)storyText));

        private readonly AdaptedSource<IStoryText, IStoryTextAdapter> _storyText =
            new AdaptedSource<IStoryText, IStoryTextAdapter>(
                ScreenSource<IStoryText>.FromScene(LoadedScenes.AdventureScene),
                storyText => new StoryTextAdapter((StoryText)storyText));

        private readonly AdaptedSource<DialogueMenu, IStoryTextAdapter> _dialogue =
            new AdaptedSource<DialogueMenu, IStoryTextAdapter>(
                ScreenSource<DialogueMenu>.FromScene(LoadedScenes.AdventureScene),
                menu => new DialogueMenuAdapter(menu));

        /// <summary>The adapter itself is what the slot holds here: three unrelated objects draw the
        /// one page, so the "menu" a source answers with IS the adapter over it, built once per
        /// object.</summary>
        protected override object ResolveMenu()
        {
            return Drawing(_letterbox.Current) ?? Drawing(_storyText.Current) ?? Drawing(_dialogue.Current);
        }

        protected override IStoryTextAdapter Adapt(object menu)
        {
            return (IStoryTextAdapter)menu;
        }

        private static IStoryTextAdapter Drawing(IStoryTextAdapter adapter)
        {
            return adapter != null && adapter.IsPresent() ? adapter : null;
        }

        public override string Key
        {
            get { return "story-text"; }
        }

        /// <summary>Layer 200: the story speaks over everything, dialogs included.</summary>
        public override int Layer
        {
            get { return 200; }
        }

        /// <summary>None: the one node says the speaker or heading itself.</summary>
        public override string ScreenName
        {
            get { return null; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(StoryStop);

            // ONE BUTTON: "{speaker}: {first paragraph}" then the further paragraphs as parts, every
            // part live, so the next line the game writes in place is read by the live watch under a
            // cursor that never moved. Enter advances (owner ruling 2026-09-07).
            if (Live.HasBody)
            {
                ControlId bodyId = ControlId.For(_bodyKey, "story-text:body");
                NodeVtable body = GraphNodes.Paragraphs(Lines, live: true);
                body.ControlType = ControlTypes.Button;
                body.OnActivate = Advance;
                builder.AddItem(new SyntheticNode(bodyId, body));
                builder.SetStart(bodyId);
            }
        }

        /// <summary>The body's paragraphs, the first carrying the speaker or heading where the source
        /// draws one ("Cecilia Stoutheart: Peradine, I came as quickly as I could.").</summary>
        private IList<string> Lines()
        {
            IList<string> lines = Live != null ? Live.BodyLines : null;
            string title = Live != null ? Live.Title : null;
            if (lines == null || lines.Count == 0 || string.IsNullOrWhiteSpace(title))
            {
                return lines;
            }

            List<string> named = new List<string>(lines);
            named[0] = ModText.Get(ModStrings.UI.LabelValue, title, lines[0]);
            return named;
        }

        private void Advance()
        {
            if (Live != null)
            {
                Live.AdvanceNow();
            }
        }

    }
}
