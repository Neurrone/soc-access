using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    public sealed class LoadingCompleteScreen : Screen
    {
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(LoadingScreenMenuInstaller), "Container");

        private readonly LoadingScreenAdapter _adapter;

        public LoadingCompleteScreen(LoadingScreenAdapter adapter)
            : base(BuildRoot(adapter))
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

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool HasClaimed(string actionKey)
        {
            return false;
        }

        public override bool HasFocusedWidgetClaimed(string actionKey)
        {
            return false;
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            return false;
        }

        private static ContainerWidget BuildRoot(LoadingScreenAdapter adapter)
        {
            ContainerWidget root = new ContainerWidget("loading-complete-screen", string.Empty);
            root.AddChild(new PassiveButtonWidget(
                "loading-complete-continue",
                () => adapter != null ? adapter.PromptText : string.Empty));
            return root;
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

        private sealed class PassiveButtonWidget : Widget
        {
            private readonly System.Func<string> _getLabel;

            public PassiveButtonWidget(string id, System.Func<string> getLabel)
                : base(id)
            {
                _getLabel = getLabel;
            }

            public override string GetLabel()
            {
                return _getLabel != null ? _getLabel() ?? string.Empty : string.Empty;
            }

            public override string GetRole()
            {
                return "button";
            }
        }
    }
}
