using System.Collections.Generic;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

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
    public sealed class CampaignMapSelectScreen : LiveScreen<CampaignMapSelectAdapter>
    {
        private const string MissionsStop = "campaign-map-missions";
        private const string DetailsStop = "campaign-map-details";
        private const string DifficultyStop = "campaign-map-difficulty";
        private const string ButtonsStop = "campaign-map-buttons";

        // MOD-OWNED CURSOR INTENT, which outlives the menu (AGENTS.md, "Screen Resolution"): taking
        // a difficulty makes the game redraw the page, and a redraw the screen did not survive
        // starts with no cursor memory. This carries the one thing worth keeping across that - that
        // the player was at the difficulty - and is read once, by the seating that follows. It is
        // about THIS page, so leaving the page drops it: the game redraws in place
        // (CampaignMapSelectMenu.HandleDifficultyChanged re-clicks the selected button and the
        // information view never clears _map), the screen therefore never goes inactive, and an
        // intent nobody consumed would be waiting for the next campaign map opened.
        private bool _focusDifficultyAfterNextRebuild;

        // A subject of its own for the details line, kept across rebuilds so the reconciler seats the
        // cursor on the same node while the mission under it changes.
        private readonly object _detailsMarker = new object();

        /// <summary>The menu, once its information view is there to read. The view is bound lazily,
        /// so it comes off the menu's own field rather than out of the container
        /// (<see cref="MainMenuSources"/>).</summary>
        protected override object ResolveMenu()
        {
            CampaignMapSelectMenu menu = MainMenuSources.CampaignMapSelect.Current;
            return menu != null && MainMenuSources.CampaignMapSelectInformation.Current != null ? menu : null;
        }

        protected override CampaignMapSelectAdapter Adapt(object menu)
        {
            return new CampaignMapSelectAdapter(
                (CampaignMapSelectMenu)menu,
                MainMenuSources.CampaignMapSelectInformation.Current);
        }

        public override string Key
        {
            get { return "campaign-map-select"; }
        }

        /// <summary>Layer 3: over the campaign or tale page that opens it.</summary>
        public override int Layer
        {
            get { return 3; }
        }

        /// <summary>The campaign's own drawn title pair ("The First Song. The Song of Stoutheart").
        /// </summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.GetCampaignTitle() : null; }
        }

        /// <summary>The missions, or the difficulty when the page was redrawn by taking one. Read
        /// once: the seating this answers is the one the intent was kept for.</summary>
        public override object InitialFocusStop
        {
            get
            {
                if (!_focusDifficultyAfterNextRebuild)
                {
                    return MissionsStop;
                }

                _focusDifficultyAfterNextRebuild = false;
                return DifficultyStop;
            }
        }

        public override IMenuButtonAdapter BackButton
        {
            get { return Live != null ? Live.BackButton : null; }
        }

        /// <summary>The page has gone, so the intent about it goes too: it is read only when a
        /// seating finds no cursor, which on this page never happens, so an unconsumed one would be
        /// waiting for the next campaign map opened and put the player on "Difficulty, combo box"
        /// instead of on the missions.</summary>
        public override void OnPop()
        {
            base.OnPop();
            _focusDifficultyAfterNextRebuild = false;
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
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
            IReadOnlyList<CampaignMapButtonAdapter> missions = Live.Missions;
            int selected = Live.SelectedMissionIndex;
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
            CampaignMapSelectedInformationAdapter information = Live.Information;
            if (information == null || mission == null)
            {
                return mission != null ? mission.GetDisplayName() : string.Empty;
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
            return string.IsNullOrWhiteSpace(counter) ? mission.GetDisplayName() : counter;
        }

        // ---- the panel describing the chosen mission ----

        /// <summary>The panel as one line: the mission's counter and title as the label, watched live
        /// because the mission changes from the map, with what the panel says about it as a COMPOSED
        /// section - which is announced as well as reviewed, so the node reads the whole panel on
        /// arrival and the review buffer holds it a drawn line at a time.</summary>
        private void BuildDetails(GraphBuilder builder)
        {
            CampaignMapSelectedInformationAdapter information = Live.Information;
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
            CampaignMapSelectedInformationAdapter information = Live.Information;
            if (information == null)
            {
                return string.Empty;
            }

            return JoinSentences(information.GetMissionCounter(), information.GetTitle());
        }

        private IList<string> DetailsLines()
        {
            CampaignMapSelectedInformationAdapter information = Live.Information;
            if (information == null)
            {
                return new List<string>();
            }

            List<string> raw = new List<string>(information.GetDescriptionLines());
            raw.Add(information.GetWinConditions());
            raw.Add(EnsureSentenceTerminated(information.GetCompletedStatus()));
            return SpokenLines.Of(raw);
        }

        // ---- the difficulty ----

        private void BuildDifficulty(GraphBuilder builder)
        {
            CampaignMapSelectedInformationAdapter information = Live.Information;
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
        /// which reseats the cursor, so the flag is set BEFORE the value changes: the redraw can
        /// happen inside this call.</summary>
        private void TakeDifficulty(CampaignMapSelectedInformationAdapter.DifficultyDropList list, int index)
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
            CampaignMapSelectedInformationAdapter information = Live.Information;
            // The panel's own commands, in the order it draws them (Start above Replay).
            AddButton(builder, "campaign-map:start", information != null ? information.StartButton : null);
            AddButton(builder, "campaign-map:replay", information != null ? information.ReplayButton : null);
            // Then the header band above the page, left to right.
            AddButton(builder, "campaign-map:back", Live.BackButton);
            AddButton(builder, "campaign-map:options", Live.OptionsButton);
        }

        private void AddButton(GraphBuilder builder, string key, IMenuButtonAdapter button)
        {
            if (button != null)
            {
                GraphNodes.MenuButton(builder, key, button, onFocusVisual: () => FocusNativeButton(button.Button));
            }
        }

        private void FocusNativeButton(UIButton button)
        {
            CampaignMapSelectedInformationAdapter information = Live.Information;
            if (information != null && button != null)
            {
                information.FocusButton(button);
            }
        }

        /// <summary>Several of the card's lines run together as sentences. The stop and the space
        /// between them are the locale's rather than the mod's: a line the game already ended is
        /// followed by Common.PhraseSeparator and one it did not by Common.SentenceSeparator, and
        /// the whole is ended with Common.Sentence.</summary>
        private static string JoinSentences(params string[] parts)
        {
            string text = string.Empty;
            for (int i = 0; parts != null && i < parts.Length; i++)
            {
                string part = parts[i] != null ? parts[i].Trim() : string.Empty;
                if (part.Length == 0)
                {
                    continue;
                }

                if (text.Length == 0)
                {
                    text = part;
                    continue;
                }

                text = IsTerminated(text)
                    ? ModText.Get(ModStrings.Common.PhraseSeparator, text, part)
                    : ModText.Get(ModStrings.Common.SentenceSeparator, text, part);
            }

            return EnsureSentenceTerminated(text);
        }

        private static string EnsureSentenceTerminated(string value)
        {
            value = value != null ? value.Trim() : string.Empty;
            if (value.Length == 0)
            {
                return string.Empty;
            }

            return IsTerminated(value) ? value : ModText.Get(ModStrings.Common.Sentence, value);
        }

        /// <summary>Whether the game ended the line itself.</summary>
        private static bool IsTerminated(string value)
        {
            char last = value[value.Length - 1];
            return last == '.' || last == '!' || last == '?' || last == ':' || last == ';';
        }
    }
}
