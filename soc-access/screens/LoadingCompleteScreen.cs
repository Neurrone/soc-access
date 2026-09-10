using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// A load, made navigable as a graph in the shape Endless Space 2 Access's loading screen has:
    /// read-only rows for what the page draws, plus the one thing there is to do.
    ///
    /// The page is the player's for the WHOLE load, not only for its last second (owner's ruling of
    /// 2026-09-10): the cursor lands on the tip, which is what arrival reads, and Down reaches the
    /// bar's percentage, which is read on demand because the bar moves every frame. The status line
    /// the page writes above the bar ("Loading scene", the loader's own messages) follows it, and the
    /// prompt is declared only once the game writes one - the bar's text mesh is switched off for the
    /// whole load and carries "PRESS ANY KEY TO CONTINUE" only in the wait at the end. Activating it
    /// runs the game's own <c>FinalizeLoadingScreen</c> - the same adapter member
    /// <c>DevProbe.ContinueLoading</c> presses.
    ///
    /// The tip carries the game's own "Tip:" prefix, so the mod adds none (measured 2026-09-06 at
    /// 1280x800: "Tip: Most troop buildings of Yulan can be upgraded into...").
    ///
    /// It has no screen name: the two rows already say where the player is, and a name spoken over a
    /// page that exists to be dismissed is one line in the way.
    ///
    /// KEYS, per the owner's ruling of 2026-09-06: the arrows are the mod's, so the two lines can be
    /// read; everything the navigator does not claim reaches the game, which is what keeps "press any
    /// key" true of every other key. Type-ahead is therefore OFF - a letter here is one of the keys
    /// the game is waiting for - and Escape is left alone for the same reason.
    /// </summary>
    public sealed class LoadingCompleteScreen : LiveScreen<LoadingScreenAdapter>
    {
        private const string RowsStop = "loading-complete";

        // A subject of its own for each row, because the reconciler seats the cursor by SUBJECT before
        // it looks at the structural key and the two rows would otherwise collapse onto one another
        // when the page draws no tip (the rule the message dialog's port established).
        private readonly object _tipKey = new object();
        private readonly object _progressKey = new object();
        private readonly object _statusKey = new object();
        private readonly object _promptKey = new object();

        /// <summary>The loading screen's own menu, bound into a <c>GameObjectContext</c> of the scene
        /// the load puts up over everything else (<see cref="MenuSceneSources"/>).</summary>
        protected override object ResolveMenu()
        {
            return MenuSceneSources.LoadingScreen.Current;
        }

        protected override LoadingScreenAdapter Adapt(object menu)
        {
            return new LoadingScreenAdapter((LoadingScreenMenu)menu);
        }

        public LoadingScreenAdapter Adapter
        {
            get { return Live; }
        }

        public override string Key
        {
            get { return "loading-complete"; }
        }

        /// <summary>Layer 1000: nothing can be worked while the game is loading.</summary>
        public override int Layer
        {
            get { return 1000; }
        }

        /// <summary>Off: the page is waiting for any key, and a letter that started a search here
        /// would be a letter the game never saw.</summary>
        public override bool AllowsTypeahead
        {
            get { return false; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            // The tip is read as the LAID-OUT line, and TextMeshPro lays a text it was handed out at
            // the end of that frame: for the one frame after the game switches loading screen
            // definition the mesh already holds the tip and the parsed line is still empty. Declaring
            // the bar in that frame would seat the cursor on the percentage and the tip would never be
            // read at all, so the page declares nothing until its own text has caught up.
            if (Live.HasTip && string.IsNullOrWhiteSpace(Live.TipText))
            {
                return;
            }

            builder.BeginStop(RowsStop);
            ControlId start = null;

            string tip = Live.TipText;
            if (!string.IsNullOrWhiteSpace(tip))
            {
                ControlId tipId = ControlId.For(Live.TipLabel ?? _tipKey, "loading:tip");
                builder.AddItem(Row(tipId, GraphNodes.Paragraphs(() => Live.TipLines), Live.TipLabel));
                start = tipId;
            }

            // Read on demand and never watched: the bar moves every frame, and a live node would
            // interrupt the tip with a new percentage as often as the game repainted it.
            if (Live.HasProgressBar)
            {
                ControlId progressId = ControlId.For(Live.ProgressLabel ?? _progressKey, "loading:progress");
                NodeVtable progress = GraphNodes.Text(
                    () => ModText.Get(ModStrings.Screens.LoadingProgress, Mathf.RoundToInt(Live.Progress * 100f)));
                builder.AddItem(Row(progressId, progress, Live.ProgressLabel));
                if (start == null)
                {
                    start = progressId;
                }
            }

            // After the progress, so Down from the tip reaches the percentage first. The menu clears
            // this line when it enters the wait for a key, so it reads as absent there.
            string status = Live.StatusText;
            if (!string.IsNullOrWhiteSpace(status))
            {
                ControlId statusId = ControlId.For(Live.StatusLabel ?? _statusKey, "loading:status");
                builder.AddItem(Row(statusId, GraphNodes.Paragraphs(() => Live.StatusLines), Live.StatusLabel));
                if (start == null)
                {
                    start = statusId;
                }
            }

            // Only at the end: the bar's text mesh is switched off for the whole load and carries the
            // prompt only once the scene loader is waiting for a key.
            if (!string.IsNullOrWhiteSpace(Live.PromptText))
            {
                NodeVtable prompt = GraphNodes.Button(
                    () => Live.PromptText,
                    () => Live.Continue());
                ControlId promptId = ControlId.For(Live.PromptLabel ?? _promptKey, "loading:continue");
                builder.AddItem(Row(promptId, prompt, Live.PromptLabel));
                if (start == null)
                {
                    start = promptId;
                }
            }

            if (start != null)
            {
                builder.SetStart(start);
            }
        }

        private static NodeDeclaration Row(ControlId id, NodeVtable vtable, Component drawnBy)
        {
            return drawnBy != null
                ? (NodeDeclaration)new DrawnNode(id, vtable, drawnBy)
                : new SyntheticNode(id, vtable);
        }
    }
}
