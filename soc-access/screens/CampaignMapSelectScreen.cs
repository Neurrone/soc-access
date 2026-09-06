using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// A campaign's mission map, made navigable as a graph. Four places to be, and Tab moves between
    /// them: the missions, the panel describing the one chosen, the difficulty, and the buttons.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the missions are
    /// <c>CampaignMapSelectButton(Clone)</c>s scattered over the map picture rather than listed
    /// (x 485, 452, 370, 293 at y 383, 445, 396, 303 for this campaign's four), so they are declared
    /// in the game's own order and not sorted by where they are drawn; <c>MapInformationView</c> down
    /// the right at [882,0,365,743] with the mission counter at y 164, the map's title at y 183, the
    /// description in a scroll rect at y 229, the completed line at y 583, the difficulty dropdown at
    /// [953,603,222,27], START MISSION at y 658 and Replay cutscene at y 707; and the main menu's
    /// header band with Back at [21,20] and Options at [1233,11] over the drawn campaign title pair
    /// ("The First Song" over "The Song of Stoutheart").
    ///
    /// The difficulty is a real <c>UITextMeshDropdown</c> (measured), so it is a combo box opening
    /// the mod's drop list over the game's own popup, not a row of radio buttons.
    ///
    /// ARRIVING ON A MISSION DOES NOT CHOOSE IT: <c>CampaignMapButton</c> answers its buttons'
    /// OnClicked and nothing else (decompiled), so the focus visual is the native selection alone and
    /// Enter is what redraws the panel.
    ///
    /// Escape is CLAIMED and presses the drawn Back button: neither <c>CampaignMapSelectMenu</c> nor
    /// <c>CampaignMapSelectedInformationView</c> registers <c>UI.ExitMenu</c> - the view registers
    /// <c>UI.Confirm</c> on Start Mission and two gamepad buttons, and nothing else (decompiled,
    /// lines 143 to 148) - so the key would otherwise do nothing.
    /// </summary>
    public sealed class CampaignMapSelectScreen : GraphScreen
    {
        private const string MissionsStop = "campaign-map-missions";
        private const string DetailsStop = "campaign-map-details";
        private const string DifficultyStop = "campaign-map-difficulty";
        private const string ButtonsStop = "campaign-map-buttons";

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(CampaignMapSelectMenuInstaller), "Container");

        // Taking a difficulty makes the game redraw the page, which pushes a NEW screen over this
        // one through the detector's RefreshTop - and a new screen has no cursor memory. The flag
        // carries the one thing worth keeping across that: that the player was at the difficulty.
        private static bool _focusDifficultyAfterNextRebuild;

        private readonly CampaignMapSelectAdapter _adapter;
        private readonly bool _focusDifficulty;

        // A subject of its own for the details line, kept across rebuilds so the reconciler seats the
        // cursor on the same node while the mission under it changes.
        private readonly object _detailsMarker = new object();

        public CampaignMapSelectScreen(CampaignMapSelectAdapter adapter)
            : this(adapter, false)
        {
        }

        public CampaignMapSelectScreen(CampaignMapSelectAdapter adapter, bool focusDifficulty)
        {
            _adapter = adapter;
            _focusDifficulty = focusDifficulty;
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

        public override string Key
        {
            get { return "campaign-map-select"; }
        }

        /// <summary>The campaign's own drawn title pair ("The First Song. The Song of Stoutheart").
        /// </summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.GetCampaignTitle() : null; }
        }

        /// <summary>The missions, or the difficulty when the page was redrawn by taking one.</summary>
        public override object InitialFocusStop
        {
            get { return _focusDifficulty ? DifficultyStop : MissionsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.IsVisible(); }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.BackButton != null && _adapter.BackButton.Activate();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(MissionsStop);
            BuildMissions(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(DifficultyStop);
            BuildDifficulty(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        // ---- the missions ----

        private void BuildMissions(GraphBuilder builder)
        {
            IReadOnlyList<CampaignMapButtonAdapter> missions = _adapter.Missions;
            int selected = _adapter.SelectedMissionIndex;
            ControlId landing = null;
            for (int i = 0; missions != null && i < missions.Count; i++)
            {
                CampaignMapButtonAdapter mission = missions[i];
                if (mission == null || mission.Source == null || !mission.IsVisible())
                {
                    continue;
                }

                CampaignMapButtonAdapter it = mission;
                NodeVtable vtable = GraphNodes.Button(() => MissionLabel(it), () => it.Activate());
                vtable.OnFocusVisual = () => it.FocusNative();
                ControlId id = ControlId.For(it.Source, "campaign-map:mission/" + i);
                builder.AddItem(new DrawnNode(id, vtable, it.Source));
                if (i == selected)
                {
                    landing = id;
                }
            }

            // The mission the page is describing, so Tab into the map lands on what the panel says -
            // and so does the FIRST seating, which is the start node's business rather than the
            // stop's: the missions are the first stop, so the cursor is seated here before the
            // screen's initial stop is consulted.
            builder.LandStopOn(landing);
            if (landing != null)
            {
                builder.SetStart(landing);
            }
        }

        /// <summary>What a mission reads as: the game's own "Mission N" counter with its title where
        /// the panel is describing it, as the widget screen read them.</summary>
        private string MissionLabel(CampaignMapButtonAdapter mission)
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            if (information == null || mission == null)
            {
                return mission != null ? mission.GetLabel() : string.Empty;
            }

            if (information.MapDefinition != null && ReferenceEquals(information.MapDefinition, mission.Definition))
            {
                string selectedLabel = MenuButtonTextUtility.JoinParts(
                    information.GetMissionCounter(),
                    information.GetTitle());
                if (!string.IsNullOrWhiteSpace(selectedLabel))
                {
                    return selectedLabel;
                }
            }

            string counter = information.GetMissionCounter(mission.GetDisplayName());
            return string.IsNullOrWhiteSpace(counter) ? mission.GetLabel() : counter;
        }

        // ---- the panel describing the chosen mission ----

        /// <summary>The panel as one line: the mission's counter and title as the label, watched live
        /// because the mission changes from the map, with what the panel says about it as a section so
        /// the review buffer holds it a drawn line at a time.</summary>
        private void BuildDetails(GraphBuilder builder)
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            if (information == null)
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(DetailsTitle, live: true, kind: AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection> { NodeSection.Composed(DetailsLines) },
            };
            builder.AddItem(new SyntheticNode(
                ControlId.For(_detailsMarker, "campaign-map:details"),
                vtable));
        }

        private string DetailsTitle()
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            if (information == null)
            {
                return string.Empty;
            }

            return JoinSentences(information.GetMissionCounter(), information.GetTitle());
        }

        private IList<string> DetailsLines()
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            if (information == null)
            {
                return new List<string>();
            }

            return SpokenLines.Of(new[]
            {
                information.GetDescription(),
                information.GetWinConditions(),
                EnsureSentenceTerminated(information.GetCompletedStatus()),
            });
        }

        // ---- the difficulty ----

        private void BuildDifficulty(GraphBuilder builder)
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            if (information == null || !information.HasDifficultyMenu())
            {
                return;
            }

            CampaignMapSelectedInformationAdapter.DifficultyDropList list = information.Difficulty;
            Component subject = list.Subject;
            if (subject == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.ComboBox(
                () => GameText.Get("Campaign/Difficulty/Prefix", string.Empty),
                () => list.CurrentLabel,
                () => DropListScreen.Open(
                    list,
                    GameText.Get("Campaign/Difficulty/Prefix", string.Empty),
                    index => TakeDifficulty(list, index)),
                list.IsEnabled);
            vtable.OnFocusVisual = () => list.Focus();
            builder.AddItem(new DrawnNode(
                ControlId.For(subject, "campaign-map:difficulty"),
                vtable,
                subject));
        }

        /// <summary>Take a difficulty from the open list. The game answers by redrawing the page,
        /// which pushes a new screen over this one, so the flag is set BEFORE the value changes: the
        /// redraw can happen inside this call.</summary>
        private static void TakeDifficulty(CampaignMapSelectedInformationAdapter.DifficultyDropList list, int index)
        {
            _focusDifficultyAfterNextRebuild = true;
            if (!list.SetValue(index))
            {
                _focusDifficultyAfterNextRebuild = false;
            }
        }

        // ---- the buttons ----

        private void BuildButtons(GraphBuilder builder)
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            // The panel's own commands, in the order it draws them (Start above Replay).
            AddButton(builder, "campaign-map:start", information != null ? information.StartButton : null);
            AddButton(builder, "campaign-map:replay", information != null ? information.ReplayButton : null);
            // Then the header band above the page, left to right.
            AddButton(builder, "campaign-map:back", _adapter.BackButton);
            AddButton(builder, "campaign-map:options", _adapter.OptionsButton);
        }

        private void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            IMenuButtonAdapter it = button;
            NodeVtable vtable = GraphNodes.Button(it.GetLabel, () => it.Activate(), it.IsVisible);
            vtable.OnFocusVisual = () => FocusNativeButton(it.Button);
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, key), vtable, it.Button));
        }

        private void FocusNativeButton(UIButton button)
        {
            CampaignMapSelectedInformationAdapter information = _adapter.Information;
            if (information != null && button != null)
            {
                information.FocusButton(button);
            }
        }

        private static string JoinSentences(params string[] parts)
        {
            List<string> cleaned = new List<string>();
            for (int i = 0; parts != null && i < parts.Length; i++)
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
