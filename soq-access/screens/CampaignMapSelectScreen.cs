using System.Collections.Generic;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Campaign;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.UI;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CampaignMapSelectScreen : Screen
    {
        private const string DifficultyMenuId = "campaign-map-difficulty";

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

            MenuWidget missions = new MenuWidget("campaign-map-missions", "Missions");
            AddMissionItems(missions, adapter);
            if (adapter != null)
            {
                missions.SetFocusedItemById(BuildMissionId(adapter.SelectedMissionIndex));
            }

            root.AddChild(missions);
            AddDetails(root, adapter);
            AddDifficultyMenu(root, adapter);
            AddOptionalButton(root, "start-mission", adapter != null ? adapter.Information.StartButton : null, adapter);
            AddOptionalButton(root, "replay-cutscene", adapter != null ? adapter.Information.ReplayButton : null, adapter);
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

            return MenuButtonTextUtility.JoinParts(
                information.GetMissionCounter(),
                information.GetTitle(),
                information.GetDescription(),
                information.GetCompletedStatus(),
                information.GetWinConditions());
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
    }
}
