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
    public sealed class CustomCampaignSelectAdapter
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

        public CustomCampaignSelectAdapter(CustomCampaignSelectMenuBehavior behavior)
        {
            _behavior = behavior;
            BuildEntries(GetEntriesFromBehavior(behavior), GetDownloadTip(behavior));
            MainMenuManager.Settings settings = GetMainMenuSettings(behavior);
            BackButton = settings != null
                ? new StandardMenuButtonAdapter(
                    settings.BackButton,
                    () => settings.BackButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.BackButton),
                    () => NativeSelectionUtility.Click(settings.BackButton))
                : CreateFallbackBackButton();
            OptionsButton = settings != null
                ? new OptionsMenuButtonAdapter(
                    settings.OptionsButton,
                    () => settings.OptionsButton != null && MenuButtonAdapterBase.IsButtonVisible(settings.OptionsButton),
                    () => NativeSelectionUtility.Click(settings.OptionsButton))
                : CreateFallbackOptionsButton();
        }

        public IReadOnlyList<CustomCampaignEntryAdapter> CampaignEntries
        {
            get { return _campaignEntries; }
        }

        public CustomCampaignEntryAdapter DownloadTip { get; private set; }

        public IMenuButtonAdapter BackButton { get; private set; }

        public IMenuButtonAdapter OptionsButton { get; private set; }

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

        private void BuildEntries(IReadOnlyList<CustomCampaignEntry> entries, CustomCampaignEntry downloadTip)
        {
            if (entries == null)
            {
                return;
            }

            for (int i = 0; i < entries.Count; i++)
            {
                CustomCampaignEntryAdapter adapter = new CustomCampaignEntryAdapter(entries[i]);
                if (!adapter.IsVisible())
                {
                    continue;
                }

                if (adapter.Matches(downloadTip))
                {
                    DownloadTip = adapter;
                    continue;
                }

                if (adapter.HasCampaignDefinition || adapter.HasModReference)
                {
                    _campaignEntries.Add(adapter);
                }
            }
        }

        private bool HasVisibleCampaignEntry()
        {
            for (int i = 0; i < _campaignEntries.Count; i++)
            {
                if (_campaignEntries[i] != null && _campaignEntries[i].IsVisible())
                {
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<CustomCampaignEntry> GetEntriesFromBehavior(CustomCampaignSelectMenuBehavior behavior)
        {
            List<CustomCampaignEntry> entries = new List<CustomCampaignEntry>();
            CustomCampaignSelectMenuBehavior.Settings settings = behavior != null ? SettingsRef(behavior) : null;
            UITransform contentContainer = settings != null ? settings.contentContainer : null;
            Transform contentTransform = contentContainer != null ? ((Component)contentContainer).transform : null;
            if (contentTransform != null)
            {
                for (int i = 0; i < contentTransform.childCount; i++)
                {
                    Transform child = contentTransform.GetChild(i);
                    CustomCampaignEntry entry = child != null ? ((Component)child).GetComponent<CustomCampaignEntry>() : null;
                    if (entry != null)
                    {
                        entries.Add(entry);
                    }
                }

                return entries;
            }

            CustomCampaignEntry[] found = Resources.FindObjectsOfTypeAll<CustomCampaignEntry>();
            for (int i = 0; i < found.Length; i++)
            {
                CustomCampaignEntry entry = found[i];
                if (entry != null && IsLiveSceneObject(((Component)entry).gameObject))
                {
                    entries.Add(entry);
                }
            }

            entries.Sort(CompareEntriesByHierarchy);
            return entries;
        }

        private static int CompareEntriesByHierarchy(CustomCampaignEntry left, CustomCampaignEntry right)
        {
            if (ReferenceEquals(left, right))
            {
                return 0;
            }

            Transform leftTransform = left != null ? ((Component)left).transform : null;
            Transform rightTransform = right != null ? ((Component)right).transform : null;
            Transform leftParent = leftTransform != null ? leftTransform.parent : null;
            Transform rightParent = rightTransform != null ? rightTransform.parent : null;
            if (leftParent != null && ReferenceEquals(leftParent, rightParent))
            {
                return leftTransform.GetSiblingIndex().CompareTo(rightTransform.GetSiblingIndex());
            }

            float leftX = leftTransform != null ? leftTransform.position.x : 0f;
            float rightX = rightTransform != null ? rightTransform.position.x : 0f;
            return leftX.CompareTo(rightX);
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

        private static IMenuButtonAdapter CreateFallbackBackButton()
        {
            UIBackButton[] buttons = Resources.FindObjectsOfTypeAll<UIBackButton>();
            for (int i = 0; i < buttons.Length; i++)
            {
                UIBackButton button = buttons[i];
                if (button != null && MenuButtonAdapterBase.IsButtonVisible(button))
                {
                    return new StandardMenuButtonAdapter(
                        button,
                        () => button != null && MenuButtonAdapterBase.IsButtonVisible(button),
                        () => NativeSelectionUtility.Click(button));
                }
            }

            return null;
        }

        private static IMenuButtonAdapter CreateFallbackOptionsButton()
        {
            UIButton[] buttons = Resources.FindObjectsOfTypeAll<UIButton>();
            for (int i = 0; i < buttons.Length; i++)
            {
                UIButton button = buttons[i];
                if (button == null || !MenuButtonAdapterBase.IsButtonVisible(button))
                {
                    continue;
                }

                Transform transform = ((Component)button).transform;
                Transform parent = transform != null ? transform.parent : null;
                if (parent == null || parent.Find("OptionsLabel") == null)
                {
                    continue;
                }

                return new OptionsMenuButtonAdapter(
                    button,
                    () => button != null && MenuButtonAdapterBase.IsButtonVisible(button),
                    () => NativeSelectionUtility.Click(button));
            }

            return null;
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
