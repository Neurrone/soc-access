using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CodexRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private static readonly PropertyInfo ContainerProperty = AccessTools.Property(typeof(MonoInstallerBase), "Container");
        private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(MonoInstallerBase), "_container");

        public void AddActiveScreens(List<Screen> screens)
        {
            CodexMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CodexMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CodexMenu menu = ResolveCodexMenu(installers[i]);
                CodexMenuAdapter adapter = new CodexMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    screens.Add(new CodexScreen(adapter));
                    return;
                }
            }
        }

        private static CodexMenu ResolveCodexMenu(CodexMenuInstaller installer)
        {
            DiContainer container = GetContainer(installer);
            if (container == null)
            {
                return null;
            }

            return container.HasBinding<CodexMenu>()
                ? container.Resolve<CodexMenu>()
                : null;
        }

        private static DiContainer GetContainer(CodexMenuInstaller installer)
        {
            if (installer == null)
            {
                return null;
            }

            if (ContainerProperty != null)
            {
                return ContainerProperty.GetValue(installer, null) as DiContainer;
            }

            return ContainerField != null ? ContainerField.GetValue(installer) as DiContainer : null;
        }
    }
}
