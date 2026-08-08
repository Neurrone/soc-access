using System.Collections.Generic;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AudioGlossaryScreen : Screen
    {
        private const string ScreenId = "audio-glossary";

        private string _selectedCueKey;

        public AudioGlossaryScreen()
            : base(new ContainerWidget(ScreenId, ModText.Get(ModStrings.Screens.AudioGlossary)))
        {
            IReadOnlyList<CueDefinition> cues = CueLibrary.AllCues;
            _selectedCueKey = cues.Count > 0 ? cues[0].Key : string.Empty;
            RootWidget = BuildRoot();
        }

        public override bool IsPresent()
        {
            return true;
        }

        public override bool HasClaimed(string actionKey)
        {
            return actionKey == AccessibilityActions.Cancel.Key
                || base.HasClaimed(actionKey);
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return Close();
            }

            return base.OnActionJustPressed(action);
        }

        private ContainerWidget BuildRoot()
        {
            ContainerWidget root = new ContainerWidget(ScreenId, ModText.Get(ModStrings.Screens.AudioGlossary));
            root.AddChild(BuildCueMenu());
            root.AddChild(new ButtonWidget(
                ScreenId + "-configure",
                () => ModText.Get(ModStrings.Screens.ConfigureAnnouncementElement, GetSelectedCueName()),
                OpenSelectedCueSettings,
                null,
                () => true));
            root.AddChild(new ButtonWidget(
                ScreenId + "-back",
                ModText.Get(ModStrings.Screens.Back),
                Close,
                null,
                () => true));
            return root;
        }

        private MenuWidget BuildCueMenu()
        {
            MenuWidget menu = new MenuWidget(ScreenId + "-cues", ModText.Get(ModStrings.Screens.AudioGlossary));
            IReadOnlyList<CueDefinition> cues = CueLibrary.AllCues;
            for (int i = 0; i < cues.Count; i++)
            {
                CueDefinition cue = cues[i];
                menu.AddItem(new MenuItemWidget(
                    ScreenId + "-cue-" + cue.Key,
                    () => ModText.Get(cue.Name),
                    null,
                    () => Play(cue.Key),
                    () => _selectedCueKey = cue.Key,
                    () => true));
            }

            return menu;
        }

        private bool OpenSelectedCueSettings()
        {
            CueDefinition cue = CueLibrary.GetCue(_selectedCueKey);
            if (cue == null || SocAccessPlugin.Instance == null || SocAccessPlugin.Instance.ScreenManager == null)
            {
                return false;
            }

            SocAccessPlugin.Instance.ScreenManager.Push(
                new AudioCueSettingsScreen(cue),
                "audio cue settings opened");
            return true;
        }

        private string GetSelectedCueName()
        {
            CueDefinition cue = CueLibrary.GetCue(_selectedCueKey);
            return cue != null ? ModText.Get(cue.Name) : string.Empty;
        }

        private static bool Play(string cueKey)
        {
            CueLibrary.PlayCue(cueKey);
            return true;
        }

        private static bool Close()
        {
            return SocAccessPlugin.Instance != null
                && SocAccessPlugin.Instance.ScreenManager != null
                && SocAccessPlugin.Instance.ScreenManager.Pop<AudioGlossaryScreen>("audio glossary screen closed");
        }
    }
}
