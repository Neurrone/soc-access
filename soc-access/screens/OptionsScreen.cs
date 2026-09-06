using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The game's options window, made navigable as a graph. One window serves both the main menu and
    /// a game in progress, so making it navigable once covers every route to it.
    ///
    /// Three places to be, and Tab moves between them: the column of category tabs down the left, the
    /// settings of the category currently showing, and the button along the bottom. Measured
    /// 2026-09-06 at 1280x800: the tabs at x 270, seven of them 30 px apart; the content column at
    /// x 489, 486 wide, scrolling (904 px of rows inside a 519 px panel), each row with its label at
    /// the left and its control at the right; the OK button under it.
    ///
    /// The tab bar switches ON FOCUS rather than on Enter. A tab whose page you cannot see is not a
    /// thing a player wants to stand on, and the game's own switch is instant and costs nothing, so
    /// arriving at a tab and arriving at its page are the same event. Enter does it too, for the
    /// player who expects to have to ask.
    ///
    /// The page draws captions over groups of rows ("General", "Battle", "Adventure"). A caption is
    /// the REGION its rows belong to - Alt+Up and Alt+Down jump between them and the name is spoken on
    /// the way in - and not a row of its own, because there is nothing there to operate. WITH ONE
    /// EXCEPTION, measured on the Controls page: the key binding rows are drawn by
    /// <c>MenuFactoryController.AddKeyBinding</c>, which the adapter does not read, so the category
    /// captions there head nothing at all. A caption with no rows under it stays a read-only row, so
    /// the page still reads what it draws.
    ///
    /// Scrolling into view is the game's: the content panel's own <c>AutoScrollToSelected</c> follows
    /// the natively selected row, so every row's focus visual is its adapter <c>Focus</c>.
    ///
    /// Escape is the game's: <c>OptionsMenu.ReregisterInput</c> registers <c>UI.ExitMenu</c> on its
    /// keyboard branch (decompiled, lines 532 to 544), so the key already closes the window.
    /// </summary>
    public sealed class OptionsScreen : GraphScreen
    {
        private const string TabsStop = "options-tabs";
        private const string RowsStop = "options-rows";
        private const string ButtonsStop = "options-buttons";

        /// <summary>How many fine steps one coarse slider step is worth.</summary>
        private const int CoarseSteps = 10;

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(OptionsMenuInstaller), "Container");

        private readonly OptionsMenuAdapter _adapter;

        // A subject of its own per synthesized row, kept across rebuilds so the reconciler seats the
        // cursor on the same line: a caption that heads nothing, and the window's own Close button.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public OptionsScreen(OptionsMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            OptionsMenu menu = FindActiveOptionsMenu();
            if (menu == null)
            {
                return null;
            }

            OptionsMenuAdapter adapter = new OptionsMenuAdapter(menu);
            return adapter.IsPresent() ? new OptionsScreen(adapter) : null;
        }

        public override string Key
        {
            get { return "options"; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.Options); }
        }

        /// <summary>The tab bar, so arrival lands on the category actually showing rather than on the
        /// first tab - which, with the switch-on-focus above, would change the page just by arriving.
        /// </summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Kept for the detector, which calls it whenever the window's content changes. The
        /// graph is declared afresh on every operation, so there is nothing to rebuild.</summary>
        public void Refresh()
        {
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(TabsStop);
            BuildTabs(builder);

            builder.BeginStop(RowsStop);
            BuildRows(builder);

            builder.BeginStop(ButtonsStop);
            BuildButtons(builder);
        }

        // ---- the category tabs ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<OptionsMenuAdapter.TabItem> tabs = _adapter.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                OptionsMenuAdapter.TabItem tab = tabs[i];
                if (tab == null || !tab.IsVisible())
                {
                    continue;
                }

                int index = i;
                NodeVtable vtable = GraphNodes.Tab(
                    tab.GetLabel,
                    () => _adapter.GetActiveTabIndex() == index,
                    tab.IsVisible);
                // Focusing the tab IS switching to it; the guard makes re-focusing the showing tab a
                // no-op, so re-entering the bar does not restart the page.
                vtable.OnFocusVisual = () =>
                {
                    if (_adapter.GetActiveTabIndex() != index)
                    {
                        tab.Select();
                    }
                };
                vtable.OnActivate = () => tab.Select();
                builder.AddItem(new SyntheticNode(ControlId.Structural("options:tab/" + tab.Id), vtable));
            }
        }

        // ---- the settings of the showing category ----

        private void BuildRows(GraphBuilder builder)
        {
            IReadOnlyList<OptionsMenuAdapter.ControlItem> controls = _adapter.GetCurrentContentControls();
            bool inCaption = false;
            for (int i = 0; i < controls.Count; i++)
            {
                OptionsMenuAdapter.ControlItem control = controls[i];
                object item = control != null ? control.Item : null;
                OptionsMenuAdapter.TextItem caption = item as OptionsMenuAdapter.TextItem;
                if (caption != null)
                {
                    if (inCaption)
                    {
                        builder.PopContext();
                        inCaption = false;
                    }

                    builder.SetRegion(null);
                    if (!caption.IsVisible() || string.IsNullOrWhiteSpace(caption.GetText()))
                    {
                        continue;
                    }

                    if (HasRowsUnder(controls, i))
                    {
                        builder.PushContext(caption.GetText());
                        builder.SetRegion(caption.Id);
                        inCaption = true;
                    }
                    else
                    {
                        // A caption heading nothing the adapter can read - every category on the
                        // Controls page - is the only thing there is to say about that part of the
                        // page, so it is read as a line rather than lost as an empty region.
                        builder.AddItem(new SyntheticNode(
                            ControlId.For(Marker(caption.Id), "options:caption/" + caption.Id),
                            GraphNodes.Text(caption.GetText)));
                    }

                    continue;
                }

                AddRow(builder, control);
            }

            if (inCaption)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>Whether the caption at <paramref name="index"/> heads any rows: anything before the
        /// next caption that is not a caption itself.</summary>
        private static bool HasRowsUnder(IReadOnlyList<OptionsMenuAdapter.ControlItem> controls, int index)
        {
            for (int i = index + 1; i < controls.Count; i++)
            {
                object item = controls[i] != null ? controls[i].Item : null;
                if (item is OptionsMenuAdapter.TextItem)
                {
                    return false;
                }

                if (item != null)
                {
                    return true;
                }
            }

            return false;
        }

        private void AddRow(GraphBuilder builder, OptionsMenuAdapter.ControlItem control)
        {
            object item = control != null ? control.Item : null;
            Component subject = control != null ? control.Transform : null;
            if (item == null || subject == null)
            {
                return;
            }

            OptionsMenuAdapter.ToggleItem toggle = item as OptionsMenuAdapter.ToggleItem;
            if (toggle != null)
            {
                if (!toggle.IsVisible())
                {
                    return;
                }

                NodeVtable vtable = GraphNodes.Checkbox(
                    toggle.GetLabel,
                    toggle.IsChecked,
                    toggle.Toggle,
                    toggle.IsEnabled,
                    toggle.GetTooltip());
                vtable.OnFocusVisual = toggle.Focus;
                builder.AddItem(new DrawnNode(ControlId.For(subject, "options:row/" + toggle.Id), vtable, subject));
                return;
            }

            OptionsMenuAdapter.SliderItem slider = item as OptionsMenuAdapter.SliderItem;
            if (slider != null)
            {
                if (slider.IsVisible())
                {
                    AddSlider(builder, slider, subject);
                }

                return;
            }

            OptionsMenuAdapter.DropdownItem dropdown = item as OptionsMenuAdapter.DropdownItem;
            if (dropdown != null)
            {
                if (!dropdown.IsVisible())
                {
                    return;
                }

                NodeVtable vtable = GraphNodes.ComboBox(
                    dropdown.GetLabel,
                    () => CurrentOption(dropdown),
                    () => DropListScreen.Open(dropdown, dropdown.GetLabel(), index => dropdown.SetValue(index)),
                    dropdown.IsEnabled,
                    dropdown.GetTooltip());
                vtable.OnFocusVisual = dropdown.Focus;
                builder.AddItem(new DrawnNode(ControlId.For(subject, "options:row/" + dropdown.Id), vtable, subject));
                return;
            }

            OptionsMenuAdapter.ButtonItem button = item as OptionsMenuAdapter.ButtonItem;
            if (button != null && button.IsVisible())
            {
                builder.AddItem(new DrawnNode(
                    ControlId.For(subject, "options:row/" + button.Id),
                    Button(button, button.GetLabel),
                    subject));
            }
        }

        /// <summary>
        /// A slider row. Left and Right move the value; Enter opens the game's own "provide a number"
        /// popup through the value box the row draws beside the handle.
        ///
        /// The value box used to be a child node of its own, which put a "Please provide a number"
        /// button under every slider in the window and made the list of settings twice as long to
        /// walk. The box is one way of setting the same number the arrows set, so it is the row's
        /// activation instead; a row that draws no box has no activation at all.
        /// </summary>
        private void AddSlider(GraphBuilder builder, OptionsMenuAdapter.SliderItem slider, Component subject)
        {
            string editorLabel = slider.GetValueEditorLabel != null ? slider.GetValueEditorLabel() : null;
            NodeVtable vtable = GraphNodes.Slider(
                slider.GetLabel,
                slider.GetValueText,
                (sign, large) => Adjust(slider, sign, large),
                slider.IsEnabled,
                slider.GetTooltip(),
                activate: string.IsNullOrWhiteSpace(editorLabel)
                    ? (Action)null
                    : () => slider.OpenValueEditor());
            vtable.OnFocusVisual = slider.Focus;
            builder.AddItem(new DrawnNode(ControlId.For(subject, "options:row/" + slider.Id), vtable, subject));
        }

        private static void Adjust(OptionsMenuAdapter.SliderItem slider, int sign, bool large)
        {
            float step = slider.GetStep();
            if (step <= 0f)
            {
                step = 1f;
            }

            if (large)
            {
                step *= CoarseSteps;
            }

            slider.SetValue(slider.GetValue() + sign * step);
        }

        private static string CurrentOption(OptionsMenuAdapter.DropdownItem dropdown)
        {
            IReadOnlyList<string> options = dropdown.GetOptions();
            int value = dropdown.GetValue();
            return options != null && value >= 0 && value < options.Count ? options[value] : string.Empty;
        }

        // ---- the button along the bottom ----

        private void BuildButtons(GraphBuilder builder)
        {
            OptionsMenuAdapter.ButtonItem ok = _adapter.GetOkButton();
            if (ok == null || !ok.IsVisible())
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(ok.Id), "options:" + ok.Id),
                Button(ok, () => ModText.Get(ModStrings.Screens.Close))));
        }

        private static NodeVtable Button(OptionsMenuAdapter.ButtonItem button, Func<string> label)
        {
            NodeVtable vtable = GraphNodes.Button(
                label,
                () => button.Activate(),
                button.IsEnabled,
                button.GetTooltip());
            vtable.OnFocusVisual = button.Focus;
            return vtable;
        }

        private object Marker(string key)
        {
            object marker;
            if (!_markers.TryGetValue(key, out marker))
            {
                marker = new object();
                _markers.Add(key, marker);
            }

            return marker;
        }

        private static OptionsMenu FindActiveOptionsMenu()
        {
            OptionsMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<OptionsMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                OptionsMenuInstaller installer = installers[i];
                if (!IsLiveSceneInstaller(installer))
                {
                    continue;
                }

                OptionsMenu menu = TryResolve<OptionsMenu>(installer);
                if (menu == null)
                {
                    continue;
                }

                OptionsMenuAdapter adapter = new OptionsMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return menu;
                }
            }

            return null;
        }

        private static bool IsLiveSceneInstaller(OptionsMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        private static T TryResolve<T>(OptionsMenuInstaller installer) where T : class
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
