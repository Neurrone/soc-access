using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class AnnouncementOrderScreen : Screen
    {
        private readonly AnnouncementGroupDefinition _group;

        public AnnouncementOrderScreen(AnnouncementGroupDefinition group)
            : base(new ContainerWidget("announcement-order-screen", GetTitle(group)))
        {
            _group = group;
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
            ContainerWidget root = new ContainerWidget(GetScreenId(), GetTitle(_group));
            root.AddChild(new AnnouncementOrderMenuWidget(
                GetScreenId() + "-menu",
                _group,
                OpenElementSettings));
            root.AddChild(new ButtonWidget(
                GetScreenId() + "-reset-all",
                ModText.Get(ModStrings.Screens.ResetAllToDefaults),
                ResetAllToDefaults,
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

        private void OpenElementSettings(AnnouncementElementDefinition element)
        {
            if (element == null)
            {
                return;
            }

            SocAccessMod.Instance?.ScreenManager?.Push(
                new AnnouncementElementSettingsScreen(_group, element),
                "announcement element settings opened");
        }

        private bool ResetAllToDefaults()
        {
            ModSettings.ResetAnnouncementGroup(_group);
            UIManager.RequestFocus(RootWidget);
            return true;
        }

        private static bool Close()
        {
            return SocAccessMod.Instance != null
                && SocAccessMod.Instance.ScreenManager != null
                && SocAccessMod.Instance.ScreenManager.Pop<AnnouncementOrderScreen>("announcement order screen closed");
        }

        private string GetScreenId()
        {
            return "announcement-order-" + (_group != null ? _group.Key : "unknown");
        }

        private static string GetTitle(AnnouncementGroupDefinition group)
        {
            return group != null ? ModText.Get(group.Label) : string.Empty;
        }
    }
}
