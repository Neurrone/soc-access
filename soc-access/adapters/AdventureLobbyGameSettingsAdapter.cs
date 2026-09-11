using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// The lobby's game settings window, read as facts. Its rows are whatever its
    /// <see cref="MenuFactoryController"/> drew, so they are read by <see cref="MenuRows"/> like
    /// every other settings form's - the window owns only what its rows are called, the handler its
    /// tooltips resolve through, and its own Cancel and Apply.
    /// </summary>
    public sealed class AdventureLobbyGameSettingsAdapter : IPresent
    {
        private static readonly FieldInfo ContainerField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_container");
        private static readonly FieldInfo ContentContainerField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_contentContainer");
        private static readonly FieldInfo FactoryField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_factory");
        private static readonly FieldInfo ApplyButtonField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_applyButton");
        private static readonly FieldInfo CancelButtonField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_cancelButton");
        private static readonly FieldInfo LocalizationField =
            AccessTools.Field(typeof(LobbyMapSettingsMenu), "_localizationHandler");

        private readonly LobbyMapSettingsMenu _menu;
        private readonly ILocalizationHandler _localization;
        private readonly MenuRowSettings _rowSettings;

        // The rows of the window, re-read only when its content column is redrawn (the menu's own
        // Refresh destroys every child of it and draws new ones).
        private MenuRowMemo _rows;

        public AdventureLobbyGameSettingsAdapter(LobbyMapSettingsMenu menu)
        {
            _menu = menu;
            _localization = menu != null && LocalizationField != null
                ? LocalizationField.GetValue(menu) as ILocalizationHandler
                : GlobalLocalizationVariables.LocalizationHandler;
            _rowSettings = new MenuRowSettings
            {
                IdPrefix = "game-settings",
                ButtonIdKind = "content-button",
                Localization = _localization,
            };
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get
            {
                return SpokenLines.Clean(
                    GameText.Get(_localization, "Lobby/CreateLobby/SetMapSettings", string.Empty));
            }
        }

        public bool IsPresent()
        {
            UITransform container = GetField<UITransform>(ContainerField);
            return _menu != null
                && container != null
                && container.Active
                && ((Component)container).gameObject.activeInHierarchy;
        }

        public IReadOnlyList<MenuRow> GetContentControls()
        {
            if (_rows == null)
            {
                IMenuFactoryCollection factory = GetField<IMenuFactoryCollection>(FactoryField);
                if (factory == null)
                {
                    return new MenuRow[0];
                }

                UITransform content = GetField<UITransform>(ContentContainerField);
                _rows = new MenuRowMemo(factory, content != null ? content.MonoTransform : null, _rowSettings);
            }

            return _rows.Rows;
        }

        public MenuRowButton GetCancelButton()
        {
            return MenuRows.Button("game-settings-cancel", GetField<UIButton>(CancelButtonField), _localization);
        }

        public MenuRowButton GetApplyButton()
        {
            return MenuRows.Button("game-settings-confirm", GetField<UIButton>(ApplyButtonField), _localization);
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _menu != null && field != null ? field.GetValue(_menu) as T : null;
        }
    }
}
