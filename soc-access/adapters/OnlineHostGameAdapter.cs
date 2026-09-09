using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class OnlineHostGameAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<GameListMenu, GameListMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<GameListMenu, GameListMenu.Settings>("_settings");

        private readonly GameListMenu.Settings _settings;

        public OnlineHostGameAdapter(GameListMenu menu)
        {
            GameListMenu.Settings settings = menu != null ? SettingsRef(menu) : null;
            _settings = settings;

            PositiveButton = CreateButton(settings != null ? settings.HostGamePositiveButton : null);
            NegativeButton = CreateButton(settings != null ? settings.HostGameNegativeButton : null);
        }

        public IMenuButtonAdapter PositiveButton { get; private set; }

        public IMenuButtonAdapter NegativeButton { get; private set; }

        /// <summary>Whether the popup is the page drawing: the game list menu shows and hides one
        /// container for it, and its <c>Active</c> is read every frame. The list underneath answers
        /// present at the same time, which is what stacks the popup over it.</summary>
        public bool IsPresent()
        {
            return _settings != null
                && IsLoadedMainMenuScene(MainMenuSceneType.OnlineGameList)
                && _settings.HostGameMenu != null
                && _settings.HostGameMenu.Active;
        }

        public string Title
        {
            get { return GetText(_settings != null ? _settings.HostGameHeader : null); }
        }

        /// <summary>The paragraphs of the line the page draws under its heading, kept apart rather
        /// than collapsed.</summary>
        public IList<string> DescriptionLines
        {
            get
            {
                return SpokenLines.Of(new[]
                {
                    UITextMeshTextUtility.GetEffectiveText(_settings != null ? _settings.HostGameSubheader : null),
                });
            }
        }

        public string InviteOnlyLabel
        {
            get { return GetText(_settings != null ? _settings.HostGameToggleLabel : null); }
        }

        public IUITextMeshInputField InputField
        {
            get { return _settings != null ? _settings.HostGameInputField : null; }
        }

        public UIToggle InviteOnlyToggle
        {
            get { return _settings != null ? _settings.HostGameTogglePublicGame : null; }
        }

        public bool HasDescription
        {
            get { return DescriptionLines.Count > 0; }
        }

        public bool IsInputVisible()
        {
            IUITextMeshInputField field = InputField;
            return field != null && field.Active;
        }

        public bool IsInputEnabled()
        {
            IUITextMeshInputField field = InputField;
            return field != null && field.Active && field.Interactable;
        }

        public void FocusInput()
        {
            IUITextMeshInputField field = InputField;
            if (field != null)
            {
                field.Select();
            }
        }

        public bool IsInviteOnlyVisible()
        {
            UIToggle toggle = InviteOnlyToggle;
            return toggle != null && toggle.Active;
        }

        public bool IsInviteOnlyEnabled()
        {
            UIToggle toggle = InviteOnlyToggle;
            return toggle != null && toggle.Active && toggle.Interactable;
        }

        public bool IsInviteOnlyChecked()
        {
            UIToggle toggle = InviteOnlyToggle;
            return toggle != null && toggle.ToggleValue;
        }

        public void ToggleInviteOnly()
        {
            UIToggle toggle = InviteOnlyToggle;
            if (toggle != null && toggle.Active && toggle.Interactable)
            {
                toggle.ToggleValue = !toggle.ToggleValue;
            }
        }

        public Tooltip GetInputTooltip()
        {
            return Tooltip.ForComponent(InputField as Component, null);
        }

        public Tooltip GetInviteOnlyTooltip()
        {
            UIToggle toggle = InviteOnlyToggle;
            return Tooltip.ForComponent(toggle != null ? toggle.GetTextMesh() : null, null);
        }

        public Tooltip GetButtonTooltip(IMenuButtonAdapter button)
        {
            return button != null ? Tooltip.ForComponent(button.Button as Component, null) : null;
        }

        public bool Cancel()
        {
            return NegativeButton != null && NegativeButton.Activate();
        }

        private static IMenuButtonAdapter CreateButton(UIButton button)
        {
            return button != null
                ? new StandardMenuButtonAdapter(button, () => MenuButtonAdapterBase.IsButtonVisible(button), () => NativeSelectionUtility.Click(button))
                : null;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return UITextMeshTextUtility.GetEffectiveText(textMesh);
        }

        private static bool IsLoadedMainMenuScene(MainMenuSceneType sceneType)
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == sceneType;
        }
    }
}
