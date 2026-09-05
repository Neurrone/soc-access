using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// Tuning screen for a single cue. Every change replays the cue so the effect is audible
    /// without leaving the slider.
    /// </summary>
    public sealed class AudioCueSettingsScreen : Screen
    {
        private const int VolumeStep = 5;
        private const int PitchStep = 1;
        private const int DurationStep = 10;

        private readonly CueDefinition _cue;

        public AudioCueSettingsScreen(CueDefinition cue)
            : base(new ContainerWidget("audio-cue-settings-screen", GetTitle(cue)))
        {
            _cue = cue;
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
            ContainerWidget root = new ContainerWidget(GetScreenId(), GetTitle(_cue));
            root.AddChild(new CheckboxWidget(
                GetScreenId() + "-enabled",
                ModText.Get(ModStrings.Screens.Enabled),
                ToggleEnabled,
                () => ModSettings.GetCueEnabled(GetCueKey())));
            root.AddChild(new SliderWidget(
                GetScreenId() + "-volume",
                ModText.Get(ModStrings.Screens.Volume),
                () => ModText.Get(ModStrings.Screens.PercentValue, ModSettings.GetCueVolume(GetCueKey())),
                () => ModSettings.GetCueVolume(GetCueKey()),
                () => ModSettings.CueVolumeMinimum,
                () => ModSettings.CueVolumeMaximum,
                () => VolumeStep,
                SetVolume,
                () => true));
            root.AddChild(new SliderWidget(
                GetScreenId() + "-pitch",
                ModText.Get(ModStrings.Screens.Pitch),
                () => ModSettings.GetCuePitchSemitones(GetCueKey()).ToString(),
                () => ModSettings.GetCuePitchSemitones(GetCueKey()),
                () => ModSettings.CuePitchSemitonesMinimum,
                () => ModSettings.CuePitchSemitonesMaximum,
                () => PitchStep,
                SetPitchSemitones,
                () => true));
            root.AddChild(new SliderWidget(
                GetScreenId() + "-duration",
                ModText.Get(ModStrings.Screens.Duration),
                () => ModText.Get(ModStrings.Screens.PercentValue, ModSettings.GetCueDurationScale(GetCueKey())),
                () => ModSettings.GetCueDurationScale(GetCueKey()),
                () => ModSettings.CueDurationScaleMinimum,
                () => ModSettings.CueDurationScaleMaximum,
                () => DurationStep,
                SetDurationScale,
                () => true));
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-play",
                ModText.Get(ModStrings.Screens.Play),
                Play,
                null,
                () => true));
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-reset",
                ModText.Get(ModStrings.Screens.ResetToDefaults),
                ResetToDefaults,
                null,
                () => true));
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-back",
                ModText.Get(ModStrings.Screens.Back),
                Close,
                null,
                () => true));
            return root;
        }

        private void ToggleEnabled()
        {
            ModSettings.SetCueEnabled(GetCueKey(), !ModSettings.GetCueEnabled(GetCueKey()));
            CueLibrary.PlayCue(GetCueKey());
        }

        private bool SetVolume(int value)
        {
            ModSettings.SetCueVolume(GetCueKey(), value);
            CueLibrary.PlayCue(GetCueKey());
            return true;
        }

        private bool SetPitchSemitones(int value)
        {
            ModSettings.SetCuePitchSemitones(GetCueKey(), value);
            CueLibrary.PlayCue(GetCueKey());
            return true;
        }

        private bool SetDurationScale(int value)
        {
            ModSettings.SetCueDurationScale(GetCueKey(), value);
            CueLibrary.PlayCue(GetCueKey());
            return true;
        }

        private bool Play()
        {
            CueLibrary.PlayCue(GetCueKey());
            return true;
        }

        private bool ResetToDefaults()
        {
            ModSettings.ResetCue(GetCueKey());
            UIManager.RequestFocus(RootWidget);
            CueLibrary.PlayCue(GetCueKey());
            return true;
        }

        private string GetCueKey()
        {
            return _cue != null ? _cue.Key : string.Empty;
        }

        private string GetScreenId()
        {
            return "audio-cue-settings-" + (_cue != null ? _cue.Key : "unknown");
        }

        private static bool Close()
        {
            return SocAccessMod.Instance != null
                && SocAccessMod.Instance.ScreenManager != null
                && SocAccessMod.Instance.ScreenManager.Pop<AudioCueSettingsScreen>("audio cue settings screen closed");
        }

        private static string GetTitle(CueDefinition cue)
        {
            string name = cue != null ? ModText.Get(cue.Name) : string.Empty;
            return ModText.Get(ModStrings.Screens.ConfigureAnnouncementElement, name);
        }
    }
}
