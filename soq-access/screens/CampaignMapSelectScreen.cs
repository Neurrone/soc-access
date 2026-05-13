using System.Collections.Generic;
using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Campaign;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CampaignMapSelectScreen : Screen
    {
        private const string DifficultyMenuId = "campaign-map-difficulty";
        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(CampaignMapSelectMenuInstaller), "Container");

        // Moving through the difficulty menu immediately changes the native dropdown.
        // The game responds by redrawing the selected mission details and calling Show(...)
        // again, which rebuilds this accessibility screen. Preserve focus in the difficulty
        // menu across that rebuild instead of falling back to the first root widget.
        private static bool _focusDifficultyAfterNextRebuild;

        private readonly CampaignMapSelectAdapter _adapter;

        public CampaignMapSelectScreen(CampaignMapSelectAdapter adapter)
            : this(adapter, false)
        {
        }

        public CampaignMapSelectScreen(CampaignMapSelectAdapter adapter, bool focusDifficulty)
            : base(BuildRootWidget(adapter, focusDifficulty))
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CampaignMapSelectAdapter adapter = FindActiveCampaignMapSelect(null);
            return adapter != null ? new CampaignMapSelectScreen(adapter) : null;
        }

        public static bool ConsumeFocusDifficultyAfterNextRebuild()
        {
            bool result = _focusDifficultyAfterNextRebuild;
            _focusDifficultyAfterNextRebuild = false;
            return result;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool OnActionJustPressed(InputAction action)
        {
            if (action != null && action.Key == AccessibilityActions.Cancel.Key)
            {
                return _adapter != null
                    && _adapter.BackButton != null
                    && _adapter.BackButton.Activate();
            }

            return base.OnActionJustPressed(action);
        }

        private static ContainerWidget BuildRootWidget(CampaignMapSelectAdapter adapter, bool focusDifficulty)
        {
            ContainerWidget root = new ContainerWidget(
                "campaign-map-select-screen",
                adapter != null ? adapter.GetCampaignTitle() : string.Empty);

            MenuWidget missions = new MenuWidget("campaign-map-missions", string.Empty);
            AddMissionItems(missions, adapter);
            if (adapter != null)
            {
                missions.SetFocusedItemById(BuildMissionId(adapter.SelectedMissionIndex));
            }

            root.AddChild(missions);
            AddDetails(root, adapter);
            AddDifficultyMenu(root, adapter);
            AddOptionalButton(root, "replay-cutscene", adapter != null ? adapter.Information.ReplayButton : null, adapter);
            AddOptionalButton(root, "start-mission", adapter != null ? adapter.Information.StartButton : null, adapter);
            AddOptionalButton(root, "options", adapter != null ? adapter.OptionsButton : null, adapter);
            AddOptionalButton(root, "back", adapter != null ? adapter.BackButton : null, adapter);
            if (focusDifficulty)
            {
                root.SetFocusedChildById(DifficultyMenuId);
            }

            return root;
        }

        private static void AddMissionItems(MenuWidget menu, CampaignMapSelectAdapter adapter)
        {
            if (menu == null || adapter == null || adapter.Missions == null)
            {
                return;
            }

            for (int i = 0; i < adapter.Missions.Count; i++)
            {
                CampaignMapButtonAdapter item = adapter.Missions[i];
                if (item == null)
                {
                    continue;
                }

                menu.AddItem(new MenuItemWidget(
                    BuildMissionId(i),
                    () => GetMissionLabel(adapter, item),
                    item.GetStatus,
                    item.Activate,
                    item.FocusNative,
                    item.IsVisible));
            }
        }

        private static void AddDetails(ContainerWidget root, CampaignMapSelectAdapter adapter)
        {
            if (root == null || adapter == null || adapter.Information == null)
            {
                return;
            }

            root.AddChild(new TextWidget(
                "campaign-map-details",
                () => BuildDetailsText(adapter),
                null,
                includeParentLabelInAnnouncement: false));
        }

        private static string BuildDetailsText(CampaignMapSelectAdapter adapter)
        {
            CampaignMapSelectedInformationAdapter information = adapter != null ? adapter.Information : null;
            if (information == null)
            {
                return string.Empty;
            }

            string main = JoinSentences(
                information.GetMissionCounter(),
                information.GetTitle(),
                information.GetDescription(),
                information.GetWinConditions());
            string completed = EnsureSentenceTerminated(information.GetCompletedStatus());

            if (string.IsNullOrWhiteSpace(completed))
            {
                return main;
            }

            return string.IsNullOrWhiteSpace(main)
                ? completed
                : main + Environment.NewLine + completed;
        }

        private static string BuildMissionId(int index)
        {
            return index >= 0 ? "campaign-map-mission-" + index : string.Empty;
        }

        private static string GetMissionLabel(CampaignMapSelectAdapter adapter, CampaignMapButtonAdapter item)
        {
            if (adapter != null
                && adapter.Information != null
                && adapter.Information.MapDefinition != null
                && item != null
                && ReferenceEquals(adapter.Information.MapDefinition, item.Definition))
            {
                string selectedLabel = MenuButtonTextUtility.JoinParts(
                    adapter.Information.GetMissionCounter(),
                    adapter.Information.GetTitle());
                if (!string.IsNullOrWhiteSpace(selectedLabel))
                {
                    return selectedLabel;
                }
            }

            if (adapter != null && adapter.Information != null && item != null)
            {
                string missionCounter = adapter.Information.GetMissionCounter(item.GetDisplayName());
                if (!string.IsNullOrWhiteSpace(missionCounter))
                {
                    return missionCounter;
                }
            }

            return item != null ? item.GetLabel() : string.Empty;
        }

        private static void AddDifficultyMenu(ContainerWidget root, CampaignMapSelectAdapter adapter)
        {
            CampaignMapSelectedInformationAdapter information = adapter != null ? adapter.Information : null;
            if (root == null || information == null || !information.HasDifficultyMenu())
            {
                return;
            }

            MenuWidget difficultyMenu = new MenuWidget(DifficultyMenuId, "Difficulty");
            IReadOnlyList<CampaignDifficulty> difficulties = information.CurrentDifficulties;
            for (int i = 0; i < difficulties.Count; i++)
            {
                CampaignDifficulty difficulty = difficulties[i];
                string id = BuildDifficultyId(difficulty);
                difficultyMenu.AddItem(new MenuItemWidget(
                    id,
                    () => information.GetDifficultyLabel(difficulty),
                    null,
                    () => ActivateDifficultyOption(information, difficulty),
                    () => FocusDifficultyOption(information, difficulty),
                    () => information.HasDifficultyMenu()));
            }

            difficultyMenu.SetFocusedItemById(BuildDifficultyId(information.CurrentDifficulty));
            root.AddChild(difficultyMenu);
        }

        private static void AddOptionalButton(ContainerWidget root, string id, IMenuButtonAdapter button, CampaignMapSelectAdapter adapter)
        {
            if (root == null || button == null || !button.IsVisible())
            {
                return;
            }

            string label = button.GetLabel();
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            root.AddChild(new ButtonWidget(
                id,
                label,
                button.Activate,
                () => FocusNativeButton(adapter, button.Button),
                () => button.IsVisible(),
                () => button.IsVisible()));
        }

        private static void FocusNativeButton(CampaignMapSelectAdapter adapter, UIButton button)
        {
            if (adapter == null || adapter.Information == null || button == null)
            {
                return;
            }

            adapter.Information.FocusButton(button);
        }

        private static string BuildDifficultyId(CampaignDifficulty difficulty)
        {
            return "difficulty-" + difficulty.ToString().ToLowerInvariant();
        }

        private static string JoinSentences(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> cleaned = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = EnsureSentenceTerminated(parts[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    cleaned.Add(part);
                }
            }

            return cleaned.Count == 0 ? string.Empty : string.Join(" ", cleaned.ToArray());
        }

        private static string EnsureSentenceTerminated(string value)
        {
            value = value != null ? value.Trim() : string.Empty;
            if (value.Length == 0)
            {
                return string.Empty;
            }

            char last = value[value.Length - 1];
            return last == '.' || last == '!' || last == '?' || last == ':' || last == ';'
                ? value
                : value + ".";
        }

        private static bool ActivateDifficultyOption(CampaignMapSelectedInformationAdapter information, CampaignDifficulty difficulty)
        {
            if (information == null)
            {
                return false;
            }

            bool changesDifficulty = information.CurrentDifficulty != difficulty;
            if (changesDifficulty)
            {
                // Set this before changing the native dropdown because the game's redraw path
                // can synchronously rebuild the accessibility screen from the Show(...) hook.
                _focusDifficultyAfterNextRebuild = true;
            }

            bool selected = information.SelectDifficulty(difficulty);
            if (!selected && changesDifficulty)
            {
                _focusDifficultyAfterNextRebuild = false;
            }

            return selected;
        }

        private static void FocusDifficultyOption(CampaignMapSelectedInformationAdapter information, CampaignDifficulty difficulty)
        {
            if (information == null)
            {
                return;
            }

            information.FocusDifficultyDropdown();
            if (information.CurrentDifficulty != difficulty)
            {
                ActivateDifficultyOption(information, difficulty);
            }
        }

        private static CampaignMapSelectAdapter FindActiveCampaignMapSelect(CampaignMapSelectedInformationView targetInformationView)
        {
            CampaignMapSelectMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CampaignMapSelectMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CampaignMapSelectMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                CampaignMapSelectMenu menu = TryResolve<CampaignMapSelectMenu>(installer);
                CampaignMapSelectedInformationView informationView = TryResolve<CampaignMapSelectedInformationView>(installer);
                if (menu == null || informationView == null)
                {
                    continue;
                }

                if (targetInformationView != null && !ReferenceEquals(targetInformationView, informationView))
                {
                    continue;
                }

                CampaignMapSelectAdapter adapter = new CampaignMapSelectAdapter(menu, informationView);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(CampaignMapSelectMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(CampaignMapSelectMenuInstaller installer) where T : class
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            DiContainer container = InstallerContainerProperty.GetValue(installer, null) as DiContainer;
            if (container == null)
            {
                return null;
            }

            try
            {
                return container.Resolve<T>();
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
