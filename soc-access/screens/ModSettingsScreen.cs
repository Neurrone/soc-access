using System;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class ModSettingsScreen : Screen
    {
        private const string ReadEnemyInfluenceLabel = "Read attack, deadly and movement range for enemies on tiles in combat";
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
            ContainerWidget root = new ContainerWidget("mod-settings-screen", "Mod settings");
            root.AddChild(new CheckboxWidget(
                "mod-settings-read-enemy-influence",
                ReadEnemyInfluenceLabel,
                ToggleReadEnemyInfluence,
                () => ModSettings.ReadEnemyInfluence));
            root.AddChild(new ButtonWidget(
                "mod-settings-close",
                "Close",
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
