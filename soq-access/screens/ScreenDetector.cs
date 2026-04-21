using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Central detector that translates game lifecycle hooks into accessibility screens.
    /// Harmony patches should call this class directly instead of making stack decisions.
    ///
    /// It also owns the registry of runtime probes used for startup / hot-reload recovery,
    /// so probe selection stays here rather than leaking into the plugin bootstrap.
    /// </summary>
    internal sealed class ScreenDetector
    {
        private readonly ScreenManager _screenManager;
        private readonly List<IRuntimeScreenProbe> _runtimeScreenProbes;

        public ScreenDetector(ScreenManager screenManager)
        {
            _screenManager = screenManager;
            _runtimeScreenProbes = new List<IRuntimeScreenProbe>
            {
                new QuestionDialogRuntimeScreenProbe()
            };
        }

        public void OnQuestionDialogOpened(
            object sourceKey,
            PopupMenu.Settings settings,
            string title,
            string body,
            string positiveLabel,
            string negativeLabel)
        {
            if (settings == null)
            {
                SoqAccessPlugin.Instance?.LogWarning("ScreenDetector.OnQuestionDialogOpened received null settings; falling back to runtime resync");
                ResyncFromRuntimeState();
                return;
            }

            QuestionDialogAdapter adapter = new QuestionDialogAdapter(
                sourceKey,
                settings.ContainerTransform,
                settings.InputField,
                settings.PositiveButton,
                settings.NegativeButton,
                title,
                body,
                positiveLabel,
                negativeLabel);
            if (!adapter.IsPresent())
            {
                SoqAccessPlugin.Instance?.LogInfo("ScreenDetector.OnQuestionDialogOpened ignored popup settings because the popup is not a live question dialog");
                return;
            }

            QuestionDialogScreen screen = new QuestionDialogScreen(sourceKey, adapter);
            if (_screenManager.IsTopScreen<QuestionDialogScreen>())
            {
                _screenManager.ReplaceTopScreen(screen);
                SoqAccessPlugin.Instance?.LogInfo("ScreenDetector replaced top screen: " + screen.GetType().Name);
            }
            else
            {
                _screenManager.PushScreen(screen);
                SoqAccessPlugin.Instance?.LogInfo("ScreenDetector pushed screen: " + screen.GetType().Name);
            }
        }

        public void OnQuestionDialogClosed(object sourceKey)
        {
            bool removed = _screenManager.RemoveScreenForSource(sourceKey);
            if (!removed)
            {
                removed = _screenManager.RemoveTopScreen<QuestionDialogScreen>();
            }

            if (removed)
            {
                SoqAccessPlugin.Instance?.LogInfo("ScreenDetector removed question dialog screen");
            }
        }

        public void ResyncFromRuntimeState()
        {
            SoqAccessPlugin.Instance?.LogInfo("ResyncFromRuntimeState started");
            Screen activeScreen = null;
            for (int i = 0; i < _runtimeScreenProbes.Count; i++)
            {
                activeScreen = _runtimeScreenProbes[i].TryGetActiveScreen();
                if (activeScreen != null)
                {
                    break;
                }
            }

            if (activeScreen == null)
            {
                SoqAccessPlugin.Instance?.LogInfo("ResyncFromRuntimeState found no active question dialog");
                if (_screenManager.RemoveTopScreen<QuestionDialogScreen>())
                {
                    SoqAccessPlugin.Instance?.LogInfo("Removed top question dialog screen because runtime probe found nothing");
                }

                return;
            }

            if (_screenManager.CurrentScreen != null && _screenManager.CurrentScreen.GetType() == activeScreen.GetType())
            {
                _screenManager.ReplaceTopScreen(activeScreen);
                SoqAccessPlugin.Instance?.LogInfo("ResyncFromRuntimeState replaced top screen from live runtime probe: " + activeScreen.GetType().Name);
            }
            else
            {
                _screenManager.PushScreen(activeScreen);
                SoqAccessPlugin.Instance?.LogInfo("ResyncFromRuntimeState pushed screen from live runtime probe: " + activeScreen.GetType().Name);
            }
        }
    }
}
