using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AnnouncementElementSettingsScreen : Screen
    {
        private readonly AnnouncementGroupDefinition _group;
        private readonly AnnouncementElementDefinition _element;

        public AnnouncementElementSettingsScreen(
            AnnouncementGroupDefinition group,
            AnnouncementElementDefinition element)
            : base(new ContainerWidget("announcement-element-settings-screen", GetTitle(element)))
        {
            _group = group;
            _element = element;
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
            ContainerWidget root = new ContainerWidget(GetScreenId(), GetTitle(_element));
            root.AddChild(new CheckboxWidget(
                GetScreenId() + "-enabled",
                ModText.Get(ModStrings.Screens.Enabled),
                ToggleEnabled,
                () => ModSettings.GetAnnouncementElementEnabled(_group, _element)));
            root.AddChild(new CheckboxWidget(
                GetScreenId() + "-suffix",
                ModText.Get(ModStrings.Screens.Suffix),
                ToggleSuffix,
                () => ModSettings.GetAnnouncementElementSuffix(_group, _element)));
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
            ModSettings.SetAnnouncementElementEnabled(
                _group,
                _element,
                !ModSettings.GetAnnouncementElementEnabled(_group, _element));
        }

        private void ToggleSuffix()
        {
            ModSettings.SetAnnouncementElementSuffix(
                _group,
                _element,
                !ModSettings.GetAnnouncementElementSuffix(_group, _element));
        }

        private bool ResetToDefaults()
        {
            ModSettings.ResetAnnouncementElement(_group, _element);
            UIManager.RequestFocus(RootWidget);
            return true;
        }

        private static bool Close()
        {
            return SocAccessMod.Instance != null
                && SocAccessMod.Instance.ScreenManager != null
                && SocAccessMod.Instance.ScreenManager.Pop<AnnouncementElementSettingsScreen>(
                    "announcement element settings screen closed");
        }

        private string GetScreenId()
        {
            return "announcement-element-settings-" + (_group != null ? _group.Key : "unknown")
                + "-" + (_element != null ? _element.Key : "unknown");
        }

        private static string GetTitle(AnnouncementElementDefinition element)
        {
            string label = element != null ? ModText.Get(element.Label) : string.Empty;
            return ModText.Get(ModStrings.Screens.ConfigureAnnouncementElement, label);
        }
    }
}
