using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The end of a load, made navigable as a graph in the shape Endless Space 2 Access's loading
    /// screen has: read-only rows for what the page draws, plus the one thing there is to do.
    ///
    /// The page draws two lines here (measured 2026-09-06 at 1280x800): a tip above the prompt ("Tip:
    /// Most troop buildings of Yulan can be upgraded into...") and "PRESS ANY KEY TO CONTINUE" under
    /// it. The widget screen only ever read the prompt, so the tip is what this port adds. Focus
    /// starts on the tip, which is what makes arrival read it; the prompt is the button, and
    /// activating it runs the game's own <c>FinalizeLoadingScreen</c> - the same adapter member
    /// <c>DevProbe.ContinueLoading</c> presses.
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

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(LoadingScreenMenuInstaller), "Container");

        // A subject of its own for each row, because the reconciler seats the cursor by SUBJECT before
        // it looks at the structural key and the two rows would otherwise collapse onto one another
        // when the page draws no tip (the rule the message dialog's port established).
        private readonly object _tipKey = new object();
        private readonly object _promptKey = new object();

        /// <summary>After a hot reload: point the slot at the menu already showing.
        /// Scanned once, from <c>ScreenDetector.RecoverRuntimeState</c>.</summary>
        public static void Recover()
        {
            Recovered<LoadingCompleteScreen>(FindActive());
        }

        public static LoadingScreenAdapter FindActive()
        {
            LoadingScreenMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<LoadingScreenMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                LoadingScreenMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                LoadingScreenMenu menu = TryResolve<LoadingScreenMenu>(GetContainer(installer));
                LoadingScreenAdapter adapter = new LoadingScreenAdapter(menu);
                if (adapter.IsPresent())
                {
                    return (adapter);
                }
            }

            return null;
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

        public override bool IsActive()
        {
            return Live != null && Live.IsPresent();
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

            builder.BeginStop(RowsStop);
            ControlId start = null;

            string tip = Live.TipText;
            if (!string.IsNullOrWhiteSpace(tip))
            {
                ControlId tipId = ControlId.For(Live.TipLabel ?? _tipKey, "loading:tip");
                builder.AddItem(Row(tipId, GraphNodes.Paragraphs(() => Live.TipLines), Live.TipLabel));
                start = tipId;
            }

            NodeVtable prompt = GraphNodes.Button(
                () => Live.PromptText,
                () => Live.Continue());
            ControlId promptId = ControlId.For(Live.PromptLabel ?? _promptKey, "loading:continue");
            builder.AddItem(Row(promptId, prompt, Live.PromptLabel));
            if (start == null)
            {
                start = promptId;
            }

            builder.SetStart(start);
        }

        private static NodeDeclaration Row(ControlId id, NodeVtable vtable, Component drawnBy)
        {
            return drawnBy != null
                ? (NodeDeclaration)new DrawnNode(id, vtable, drawnBy)
                : new SyntheticNode(id, vtable);
        }

        private static bool IsLiveSceneInstaller(LoadingScreenMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static DiContainer GetContainer(LoadingScreenMenuInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static T TryResolve<T>(DiContainer container) where T : class
        {
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (System.Exception)
            {
                return null;
            }
        }
    }
}
