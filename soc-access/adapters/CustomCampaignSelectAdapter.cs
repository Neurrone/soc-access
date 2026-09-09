using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Common;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CustomCampaignSelectAdapter : IPresent
    {
        private static readonly AccessTools.FieldRef<CustomCampaignSelectMenuBehavior, CustomCampaignSelectMenuBehavior.Settings> SettingsRef =
            AccessTools.FieldRefAccess<CustomCampaignSelectMenuBehavior, CustomCampaignSelectMenuBehavior.Settings>("_settings");
        private static readonly AccessTools.FieldRef<CustomCampaignSelectMenuBehavior, MainMenuManagerContainer> ManagerContainerRef =
            AccessTools.FieldRefAccess<CustomCampaignSelectMenuBehavior, MainMenuManagerContainer>("_mainMenuManagerContainer");
        private static readonly AccessTools.FieldRef<MainMenuManager, MainMenuManager.Settings> MainMenuSettingsRef =
            AccessTools.FieldRefAccess<MainMenuManager, MainMenuManager.Settings>("_settings");
        private static readonly AccessTools.FieldRef<CustomCampaignSelectMenuBehavior, CustomCampaignEntry> DownloadTipRef =
            AccessTools.FieldRefAccess<CustomCampaignSelectMenuBehavior, CustomCampaignEntry>("_downloadTip");

        private readonly CustomCampaignSelectMenuBehavior _behavior;
        private readonly List<CustomCampaignEntryAdapter> _campaignEntries = new List<CustomCampaignEntryAdapter>();

        // The content container's children when the list above was built: how many there are, and
        // how many of them are drawn. The page instantiates its cards into that container after the
        // scene is up, so the adapter can be made before there is a single one; both numbers are
        // read from the game on every access and the list is rebuilt when either changes.
        private int _campaignEntriesKey = -1;
        private CustomCampaignEntryAdapter _downloadTip;
        private bool _headerFound;
        private IMenuButtonAdapter _backButton;
        private IMenuButtonAdapter _optionsButton;

        public CustomCampaignSelectAdapter(CustomCampaignSelectMenuBehavior behavior)
        {
            _behavior = behavior;
        }

        /// <summary>The campaign cards. Rebuilt only when the content container's children change,
        /// which is two field reads and a walk of four transforms per access.</summary>
        public IReadOnlyList<CustomCampaignEntryAdapter> CampaignEntries
        {
            get
            {
                SyncEntries();
                return _campaignEntries;
            }
        }

        /// <summary>The card the page draws instead of a campaign when there are none to show.
        /// Picked out of the same rebuild the entries come from.</summary>
        public CustomCampaignEntryAdapter DownloadTip
        {
            get
            {
                SyncEntries();
                return _downloadTip;
            }
        }

        /// <summary>The main menu's own header band, shared with the campaign menu. Built the first
        /// time the manager answers with its settings, which it may not do on the frame the page's
        /// behaviour is first found, and kept once it has.</summary>
        public IMenuButtonAdapter BackButton
        {
            get
            {
                SyncHeader();
                return _backButton;
            }
        }

        public IMenuButtonAdapter OptionsButton
        {
            get
            {
                SyncHeader();
                return _optionsButton;
            }
        }

        private void SyncHeader()
        {
            if (_headerFound)
            {
                return;
            }

            MainMenuManager.Settings settings = GetMainMenuSettings(_behavior);
            if (settings == null)
            {
                return;
            }

            _headerFound = true;
            _backButton = new StandardMenuButtonAdapter(
                settings.BackButton,
                () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                () => NativeSelectionUtility.Click(settings.BackButton));
            _optionsButton = new OptionsMenuButtonAdapter(
                settings.OptionsButton,
                () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                () => NativeSelectionUtility.Click(settings.OptionsButton));
        }

        public string GetTitle()
        {
            return GlobalLocalizationVariables.LocalizationHandler != null
                ? GlobalLocalizationVariables.LocalizationHandler.GetText("Campaign/Custom/Title")
                : string.Empty;
        }

        public bool IsPresent()
        {
            return IsReadySceneOrBehavior()
                && (HasVisibleCampaignEntry() || (DownloadTip != null && DownloadTip.IsVisible()));
        }

        /// <summary>Rebuild the cards when the container they are drawn in has changed.</summary>
        private void SyncEntries()
        {
            int key = ContentKey();
            if (key == _campaignEntriesKey)
            {
                return;
            }

            _campaignEntriesKey = key;
            _campaignEntries.Clear();
            _downloadTip = null;

            Transform contentTransform = GetContentTransform();
            CustomCampaignEntry downloadTip = GetDownloadTip(_behavior);
            for (int i = 0; contentTransform != null && i < contentTransform.childCount; i++)
            {
                Transform child = contentTransform.GetChild(i);
                CustomCampaignEntry entry = child != null ? child.GetComponent<CustomCampaignEntry>() : null;
                if (entry == null)
                {
                    continue;
                }

                CustomCampaignEntryAdapter adapter = new CustomCampaignEntryAdapter(entry);
                if (!adapter.IsVisible())
                {
                    continue;
                }

                if (adapter.Matches(downloadTip))
                {
                    _downloadTip = adapter;
                    continue;
                }

                if (adapter.HasCampaignDefinition || adapter.HasModReference)
                {
                    _campaignEntries.Add(adapter);
                }
            }
        }

        /// <summary>What the cards are keyed on: how many children the content container has and how
        /// many of them are drawn. Instantiating a card changes the first, showing or hiding one
        /// changes the second.</summary>
        private int ContentKey()
        {
            Transform contentTransform = GetContentTransform();
            if (contentTransform == null)
            {
                return 0;
            }

            int childCount = contentTransform.childCount;
            int active = 0;
            for (int i = 0; i < childCount; i++)
            {
                Transform child = contentTransform.GetChild(i);
                if (child != null && child.gameObject.activeInHierarchy)
                {
                    active++;
                }
            }

            return childCount * 397 + active;
        }

        private Transform GetContentTransform()
        {
            CustomCampaignSelectMenuBehavior.Settings settings = _behavior != null ? SettingsRef(_behavior) : null;
            UITransform contentContainer = settings != null ? settings.contentContainer : null;
            return contentContainer != null ? ((Component)contentContainer).transform : null;
        }

        private bool HasVisibleCampaignEntry()
        {
            IReadOnlyList<CustomCampaignEntryAdapter> entries = CampaignEntries;
            for (int i = 0; i < entries.Count; i++)
            {
                if (entries[i] != null && entries[i].IsVisible())
                {
                    return true;
                }
            }

            return false;
        }

        private static MainMenuManager.Settings GetMainMenuSettings(CustomCampaignSelectMenuBehavior behavior)
        {
            MainMenuManagerContainer container = behavior != null ? ManagerContainerRef(behavior) : null;
            MainMenuManager manager = container != null ? container.CurrentManager as MainMenuManager : null;
            return manager != null ? MainMenuSettingsRef(manager) : null;
        }

        private static CustomCampaignEntry GetDownloadTip(CustomCampaignSelectMenuBehavior behavior)
        {
            return behavior != null ? DownloadTipRef(behavior) : null;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private bool IsReadySceneOrBehavior()
        {
            if (IsLoadedMainMenuScene(MainMenuSceneType.CustomCampaign))
            {
                return true;
            }

            CustomCampaignSelectMenuBehavior.Settings settings = _behavior != null ? SettingsRef(_behavior) : null;
            UITransform contentContainer = settings != null ? settings.contentContainer : null;
            GameObject gameObject = contentContainer != null ? ((Component)contentContainer).gameObject : null;
            return IsLiveSceneObject(gameObject) && gameObject.activeInHierarchy;
        }

        private static bool IsLoadedMainMenuScene(MainMenuSceneType sceneType)
        {
            MainMenuSceneLoader loader = MainMenuSceneLoader.UnsafeInstance;
            return loader != null && loader.CurrentlyLoadedScene == sceneType;
        }
    }
}
