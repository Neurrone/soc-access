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
    public sealed class LoadingCompleteScreen : GraphScreen
    {
        private const string RowsStop = "loading-complete";

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(LoadingScreenMenuInstaller), "Container");

        private readonly LoadingScreenAdapter _adapter;

        // A subject of its own for each row, because the reconciler seats the cursor by SUBJECT before
        // it looks at the structural key and the two rows would otherwise collapse onto one another
        // when the page draws no tip (the rule the message dialog's port established).
        private readonly object _tipKey = new object();
        private readonly object _promptKey = new object();

        public LoadingCompleteScreen(LoadingScreenAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
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
                    return new LoadingCompleteScreen(adapter);
                }
            }

            return null;
        }

        public LoadingScreenAdapter Adapter
        {
            get { return _adapter; }
        }

        public override string Key
        {
            get { return "loading-complete"; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Off: the page is waiting for any key, and a letter that started a search here
        /// would be a letter the game never saw.</summary>
        public override bool AllowsTypeahead
        {
            get { return false; }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(RowsStop);
            ControlId start = null;

            string tip = _adapter.TipText;
            if (!string.IsNullOrWhiteSpace(tip))
            {
                ControlId tipId = ControlId.For(_adapter.TipLabel ?? _tipKey, "loading:tip");
                builder.AddItem(Row(tipId, GraphNodes.Text(() => _adapter.TipText), _adapter.TipLabel));
                start = tipId;
            }

            NodeVtable prompt = GraphNodes.Button(
                () => _adapter.PromptText,
                () => _adapter.Continue());
            ControlId promptId = ControlId.For(_adapter.PromptLabel ?? _promptKey, "loading:continue");
            builder.AddItem(Row(promptId, prompt, _adapter.PromptLabel));
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
