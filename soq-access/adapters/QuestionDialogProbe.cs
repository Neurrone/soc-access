using SongsOfConquest.Client.Menu.Popup;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class QuestionDialogProbe
    {
        public QuestionDialogAdapter FindActiveQuestionDialog()
        {
            PopupMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<PopupMenuInstaller>();
            SoqAccessPlugin.Instance?.LogInfo("QuestionDialogProbe scanned " + installers.Length + " PopupMenuInstaller objects");
            for (int i = 0; i < installers.Length; i++)
            {
                PopupMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                QuestionDialogAdapter adapter = new QuestionDialogAdapter(installer);
                if (adapter.IsPresent())
                {
                    SoqAccessPlugin.Instance?.LogInfo("QuestionDialogProbe found active popup installer: " + installer.name);
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(PopupMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
