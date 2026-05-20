using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ModSettingsScreen : Screen
    {
        private readonly Func<bool> _close;

        public ModSettingsScreen(Func<bool> close)
            : base(BuildRoot(close))
        {
            _close = close;
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
                return _close != null && _close();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRoot(Func<bool> close)
        {
            ContainerWidget root = new ContainerWidget("mod-settings-screen", ModText.Get(ModStrings.Screens.ModSettings));
            root.AddChild(new CheckboxWidget(
                "mod-settings-read-enemy-influence",
                ModText.Get(ModStrings.Screens.ReadEnemyInfluence),
                ToggleReadEnemyInfluence,
                () => ModSettings.ReadEnemyInfluence));
            root.AddChild(new ButtonWidget(
                "mod-settings-close",
                ModText.Get(ModStrings.Screens.Close),
                () => close != null && close(),
                null,
                () => true));
            return root;
        }

        private static void ToggleReadEnemyInfluence()
        {
            ModSettings.SetReadEnemyInfluence(!ModSettings.ReadEnemyInfluence);
        }
    }
}
