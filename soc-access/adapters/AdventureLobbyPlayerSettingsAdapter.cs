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
    /// The lobby's per-player settings popup, read as facts. Its rows are whatever its
    /// <see cref="MenuFactoryController"/> drew, so they are read by <see cref="MenuRows"/> like
    /// every other settings form's; the popup owns only what its rows are called, the handler its
    /// tooltips resolve through, and its own Cancel and Confirm.
    /// </summary>
    public sealed class AdventureLobbyPlayerSettingsAdapter : IPresent
    {
        private static readonly FieldInfo SettingsContainerField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_settingsContainer");
        private static readonly FieldInfo FactoryField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_factory");
        private static readonly FieldInfo CancelButtonField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_cancelButton");
        private static readonly FieldInfo ConfirmButtonField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_confirmButton");
        private static readonly FieldInfo LocalizationField =
            AccessTools.Field(typeof(LobbyPlayerSettingsMenu), "_localizationHandler");

        private readonly LobbyPlayerSettingsMenu _menu;
        private readonly ILocalizationHandler _localization;
        private readonly MenuRowSettings _rowSettings;

        // The rows of the popup, read once: the menu draws them in its own Awake and never again.
        private MenuRowMemo _rows;

        public AdventureLobbyPlayerSettingsAdapter(LobbyPlayerSettingsMenu menu)
        {
            _menu = menu;
            _localization = menu != null && LocalizationField != null
                ? LocalizationField.GetValue(menu) as ILocalizationHandler
                : GlobalLocalizationVariables.LocalizationHandler;
            _rowSettings = new MenuRowSettings
            {
                IdPrefix = "player-settings",
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
                    GameText.Get(_localization, "Lobby/PlayerSettingsMenu/Header", string.Empty));
            }
        }

        public bool IsPresent()
        {
            UITransform container = Reflect.Get<UITransform>(_menu, SettingsContainerField);
            GameObject gameObject = _menu != null ? ((Component)_menu).gameObject : null;
            return _menu != null
                && gameObject != null
                && gameObject.activeInHierarchy
                && container != null
                && container.Active
                && ((Component)container).gameObject.activeInHierarchy;
        }

        public IReadOnlyList<MenuRow> GetContentControls()
        {
            if (_rows == null)
            {
                IMenuFactoryCollection factory = Reflect.Get<IMenuFactoryCollection>(_menu, FactoryField);
                if (factory == null)
                {
                    return new MenuRow[0];
                }

                UITransform content = Reflect.Get<UITransform>(_menu, SettingsContainerField);
                _rows = new MenuRowMemo(factory, content != null ? content.MonoTransform : null, _rowSettings);
            }

            return _rows.Rows;
        }

        public MenuRowButton GetCancelButton()
        {
            return MenuRows.Button("player-settings-cancel", Reflect.Get<UIButton>(_menu, CancelButtonField), _localization);
        }

        public MenuRowButton GetConfirmButton()
        {
            return MenuRows.Button("player-settings-confirm", Reflect.Get<UIButton>(_menu, ConfirmButtonField), _localization);
        }
    }
}
