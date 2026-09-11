using System;
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
    /// The save and load window, made navigable as a graph. One window serves both modes; this port
    /// was measured and verified on the LOAD variant, reached from the main menu, and the save
    /// variant is verified in phase C.
    ///
    /// Four places to be, and Tab moves between them: the band at the top of the window, the list of
    /// saves, the preview panel beside it, and the buttons. Measured 2026-09-06 at 1280x800 through
    /// <c>/gui/unity</c>: the window <c>Panel</c> at [256,96,768,608] with the title "Load Game" at
    /// y 120 and the close cross at [981,104,34,34]; the band at [374,147,532,45] holding the three
    /// category tabs (Single Player x 392, Online x 559, Hotseat x 725) in load mode and the "Saved
    /// as ..." line in save mode; <c>SaveGameScrollEntry</c> at [275,208,448,466] holding the saves
    /// as <c>SaveLoadGameMenuEntry(Clone)</c> rows 43 px apart, NEWEST FIRST, over 844 px of content;
    /// and <c>PreviewEntries</c> at [734,208,272,466] with the thumbnail and details above the
    /// buttons - Delete at y 602 and Load at y 637 once a save is selected, both hidden until then.
    ///
    /// The rows are declared in DRAWN order, top edge first, because the menu's own list is oldest
    /// first and the page draws it the other way up.
    ///
    /// ARRIVING ON A SAVE DOES NOT SELECT IT, and this is the one thing about the page that is not
    /// obvious: <c>SaveLoadGameMenuEntry</c> answers Unity's selection by showing an input icon and
    /// nothing else, while its CLICK is what fills the details, shows Load and Delete and, in save
    /// mode, copies the name into the box (decompiled). So the row's focus visual is the native
    /// selection - which the list's own <c>AutoScrollToSelected</c> follows - and Enter is the click.
    /// The game's double-click loads; the mod declares no second activation, so loading is Load.
    ///
    /// The tabs switch on ENTER, not on focus: selecting a tab button natively left
    /// <c>UITabGroup.CurrentTab</c> alone (measured), and only the click moved it.
    ///
    /// The name box is the save variant's, declared with the buttons it is drawn beside. It follows
    /// the plain edit contract (Enter ends the edit, Escape restores the text): the menu's own
    /// <c>HandleSubmit</c> SAVES THE GAME when the Save button is interactable (decompiled, line
    /// 352), which is the game's business on its own key and not something the mod asks for - Save
    /// is the deliberate act.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): <c>SaveLoadGameMenu</c> registers
    /// <c>InputActions.UI.ExitMenu</c> on <c>TryClose</c> (decompiled, line 233).
    /// </summary>
    public sealed class SaveLoadGameScreen : LiveScreen<SaveLoadGameMenuAdapter>
    {
        private const string TabsStop = "save-load-tabs";
        private const string SavesStop = "save-load-saves";
        private const string DetailsStop = "save-load-details";
        private const string ButtonsStop = "save-load-buttons";

        /// <summary>The one save/load window the project container holds for the whole game; the
        /// pause menu's Save and Load buttons open it in different modes.</summary>
        private readonly ScreenSource<SaveLoadGameMenu> _source = ScreenSource<SaveLoadGameMenu>.FromProject();

        private readonly GameTextEditor _editor = new GameTextEditor();

        public object SourceKey
        {
            get { return Live != null ? Live.SourceKey : null; }
        }

        protected override object ResolveMenu()
        {
            return _source.Current;
        }

        protected override SaveLoadGameMenuAdapter Adapt(object menu)
        {
            return new SaveLoadGameMenuAdapter((SaveLoadGameMenu)menu);
        }

        public override string Key
        {
            get { return "save-load-game"; }
        }

        /// <summary>Layer 44: over the pause menu that opens it.</summary>
        public override int Layer
        {
            get { return 44; }
        }

        /// <summary>The window's own drawn title ("Load Game", "Save Game").</summary>
        public override string ScreenName
        {
            get { return Live != null ? Live.Title : null; }
        }

        /// <summary>The list, which is what the player came for; the tabs are one Shift+Tab away and
        /// switching to one costs a keypress rather than happening on arrival.</summary>
        public override object InitialFocusStop
        {
            get { return SavesStop; }
        }

        /// <summary>While the keyboard is on its way to the game's box, what the player types next is
        /// meant for that box and must not start a search.</summary>
        public override bool CapturesRawInput
        {
            get { return _editor.Pending; }
        }

        public override bool OwnsGameField
        {
            get { return _editor.Pending || _editor.Editing; }
        }

        public override void OnUpdate()
        {
            base.OnUpdate();
            _editor.Update(IsActive());
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _editor.Abandon();
        }

        public override void OnPop()
        {
            base.OnPop();
            _editor.Abandon();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsActive())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildBand(builder);

            builder.BeginStop(SavesStop);
            BuildSaves(builder);

            builder.BeginStop(DetailsStop);
            BuildDetails(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        // ---- the band at the top of the window ----

        private void BuildBand(GraphBuilder builder)
        {
            // Save mode draws its "Saved as ..." line where load mode draws the tabs. The
            // description is read ONCE: the guard and the paragraph count are the same question.
            if (Live.IsSaveDescriptionVisible())
            {
                IList<string> description = Live.GetSaveDescriptionLines();
                if (description.Count > 0)
                {
                    builder.AddItem(new SyntheticNode(
                        ControlId.For(Marker("description"), "save-load:description"),
                        GraphNodes.Paragraphs(() => description)));
                }
            }

            IReadOnlyList<SaveLoadGameMenuAdapter.TabItem> tabs = Live.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                SaveLoadGameMenuAdapter.TabItem tab = tabs[i];
                if (tab == null || tab.Button == null || !tab.IsVisible())
                {
                    continue;
                }

                SaveLoadGameMenuAdapter.TabItem it = tab;
                NodeVtable vtable = GraphNodes.Tab(it.GetLabel, it.IsSelected, it.IsEnabled);
                vtable.OnActivate = () => it.Activate();
                vtable.OnFocusVisual = () => it.Focus();
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Button, "save-load:tab/" + it.Index),
                    vtable,
                    it.Button));
            }
        }

        // ---- the list of saves ----

        private void BuildSaves(GraphBuilder builder)
        {
            List<SaveLoadGameMenuAdapter.SaveEntry> entries = new List<SaveLoadGameMenuAdapter.SaveEntry>();
            IReadOnlyList<SaveLoadGameMenuAdapter.SaveEntry> all = Live.GetEntries();
            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Entry != null && all[i].IsVisible())
                {
                    entries.Add(all[i]);
                }
            }

            SortByDrawnTop(entries);
            ControlId landing = null;
            for (int i = 0; i < entries.Count; i++)
            {
                SaveLoadGameMenuAdapter.SaveEntry entry = entries[i];
                SaveLoadGameMenuAdapter.SaveEntry it = entry;
                NodeVtable vtable = GraphNodes.Button(() => it.SaveName, () => it.Select());
                vtable.Announcements.Add(GraphNodes.ValuePart(() => it.DateText, watch: false));
                vtable.Announcements.Add(GraphNodes.ValuePart(
                    () => it.IsCorrupt ? ModText.Get(ModStrings.UI.StatusCorrupt) : null,
                    watch: false));
                vtable.Announcements.Add(GraphNodes.SelectedPart(() => it.IsSelected));
                // Unity's selection is a highlight here, not a choice (the entry answers it with an
                // input icon and nothing else), and the list's AutoScrollToSelected follows it.
                vtable.OnFocusVisual = () => it.Focus();
                ControlId id = ControlId.For(it.Entry, "save-load:entry/" + it.Index);
                builder.AddItem(new DrawnNode(id, vtable, it.Entry));
                if (entry.IsSelected)
                {
                    landing = id;
                }
            }

            builder.LandStopOn(landing);
        }

        /// <summary>
        /// The rows in drawn order, topmost first (Unity's y grows upwards, so that is the largest
        /// y). Rows the layout has not placed yet - every row on the frame the list is built, where
        /// they all sit at one y - fall back to the newest save first, which is the order the page
        /// settles into a frame later.
        /// </summary>
        private static void SortByDrawnTop(List<SaveLoadGameMenuAdapter.SaveEntry> items)
        {
            List<float> tops = new List<float>(items.Count);
            List<long> times = new List<long>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                Component component = items[i].Entry;
                tops.Add(component != null ? component.transform.position.y : 0f);
                times.Add(items[i].LastWriteTime.Ticks);
            }

            for (int i = 1; i < items.Count; i++)
            {
                SaveLoadGameMenuAdapter.SaveEntry moving = items[i];
                float top = tops[i];
                long time = times[i];
                int j = i - 1;
                while (j >= 0 && IsAbove(top, time, tops[j], times[j]))
                {
                    items[j + 1] = items[j];
                    tops[j + 1] = tops[j];
                    times[j + 1] = times[j];
                    j--;
                }

                items[j + 1] = moving;
                tops[j + 1] = top;
                times[j + 1] = time;
            }
        }

        private static bool IsAbove(float top, long time, float otherTop, long otherTime)
        {
            if (Mathf.Abs(top - otherTop) > 0.5f)
            {
                return top > otherTop;
            }

            return time > otherTime;
        }

        // ---- the preview panel ----

        /// <summary>The details the panel draws for the selected save, as one line: the save's own
        /// name (watched live, because the selection changes from the list stop) with the rest of the
        /// block as a section, so the review buffer holds it one drawn line at a time.</summary>
        private void BuildDetails(GraphBuilder builder)
        {
            if (!Live.HasDetailsText())
            {
                return;
            }

            NodeVtable vtable = new NodeVtable
            {
                ControlType = ControlTypes.Text,
                Announcements = new List<NodeAnnouncement>
                {
                    new NodeAnnouncement(() => DetailsLine(0), live: true, kind: AnnouncementKinds.Label),
                },
                Sections = new List<NodeSection>
                {
                    NodeSection.Composed(DetailsRest),
                },
            };
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("details"), "save-load:details"),
                vtable));
        }

        private string DetailsLine(int index)
        {
            IList<string> lines = DetailsLines();
            return index >= 0 && index < lines.Count ? lines[index] : string.Empty;
        }

        private IList<string> DetailsRest()
        {
            IList<string> lines = DetailsLines();
            List<string> rest = new List<string>();
            for (int i = 1; i < lines.Count; i++)
            {
                rest.Add(lines[i]);
            }

            return rest;
        }

        private IList<string> DetailsLines()
        {
            return SpokenLines.Of(new[] { Live.GetDetailsText() });
        }

        // ---- the buttons ----

        private void BuildButtons(GraphBuilder builder)
        {
            AddNameField(builder);

            List<SaveLoadGameMenuAdapter.ButtonItem> buttons = new List<SaveLoadGameMenuAdapter.ButtonItem>();
            Add(buttons, Live.DeleteButton);
            Add(buttons, Live.LoadAsHotseatButton);
            Add(buttons, Live.LoadAsOnlineButton);
            Add(buttons, Live.LoadButton);
            Add(buttons, Live.SaveButton);
            DrawnOrder.SortByTop(buttons, item => item.Button);
            for (int i = 0; i < buttons.Count; i++)
            {
                AddButton(builder, buttons[i]);
            }

            // The window's cross, drawn in its top corner rather than in the column of commands, so
            // it reads last: it is the way out, not one of the things to do to a save.
            AddButton(builder, Live.CancelButton, () => ModText.Get(ModStrings.Actions.Cancel));
        }

        private static void Add(List<SaveLoadGameMenuAdapter.ButtonItem> buttons, SaveLoadGameMenuAdapter.ButtonItem button)
        {
            if (button != null && button.Button != null && button.IsVisible())
            {
                buttons.Add(button);
            }
        }

        private void AddButton(
            GraphBuilder builder,
            SaveLoadGameMenuAdapter.ButtonItem button,
            Func<string> label = null)
        {
            if (button == null || button.Button == null || !button.IsVisible())
            {
                return;
            }

            SaveLoadGameMenuAdapter.ButtonItem it = button;
            NodeVtable vtable = GraphNodes.Button(
                label ?? it.GetLabel,
                () => it.Activate(),
                it.IsEnabled);
            vtable.OnFocusVisual = () => it.Focus();
            builder.AddItem(new DrawnNode(ControlId.For(it.Button, "save-load:" + it.Id), vtable, it.Button));
        }

        /// <summary>The save variant's name box, drawn beside the commands it belongs to. The plain
        /// edit contract: activating asks for the game's keyboard, Enter ends the edit and Escape puts
        /// the old name back.</summary>
        private void AddNameField(GraphBuilder builder)
        {
            IUITextMeshInputField field = Live.IsInputVisible() ? Live.InputField : null;
            Component subject = field != null ? field.MonoTransform : null;
            if (subject == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.EditField(
                () => Live.Title,
                // Nothing while the game holds the keyboard: the echo is already speaking the keys.
                () => _editor.Editing ? null : field.InputFieldValue,
                () => _editor.Request(field),
                Live.IsInputEnabled);
            vtable.OnFocusVisual = () => Live.FocusInput();
            builder.AddItem(new DrawnNode(ControlId.For(subject, "save-load:name"), vtable, subject));
        }
    }
}
