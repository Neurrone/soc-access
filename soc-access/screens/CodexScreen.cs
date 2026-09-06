using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Menu;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;
using CodexContentItem = SongsOfConquestAccess.Adapters.CodexMenuAdapter.CodexContentItem;
using CodexContentItemKind = SongsOfConquestAccess.Adapters.CodexMenuAdapter.CodexContentItemKind;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The tutorials and codex window, made navigable as a graph. Four places to be, and Tab moves
    /// between them: the icon tab row, the categories with their articles, the article's body, and
    /// the footer.
    ///
    /// Measured 2026-09-06 at 1280x800 through <c>/gui/unity</c>: the window
    /// (<c>CodexContainer</c>) at [205,85,870,630], drawing a title PAIR - <c>SubHeader</c>
    /// ("Tutorials &amp; Codex") at y 106 over <c>Title</c> at y 125, which is the showing tab's own
    /// name; eight <c>CodexCategoryTabButton(Clone)</c> icons at y 157 (x 360 to 847);
    /// <c>NavigationScrollView</c> at [223,206,259,444] holding one <c>CodexCategorySection</c> per
    /// category, each drawing its name over its article buttons; <c>ContentScrollView</c> at
    /// [505,206,522,444] holding the article; and, on the Tutorials tab only,
    /// <c>TutorialSettings</c> at [231,657,819,45] with "Reset Tutorials" (x 264) and the "Show
    /// tutorials" toggle (x 466). The Close button is drawn at [1032,93,34,34] on every tab.
    ///
    /// The categories and their articles are ONE stop (owner ruling): each drawn category name is a
    /// REGION over its own articles, so Alt+Up and Alt+Down jump between categories and the name is
    /// spoken on the way in. The stop lands on the article the window is showing.
    ///
    /// ARRIVING ON AN ARTICLE DOES NOT SHOW IT. <c>CodexContentButton</c> raises its OnClicked only
    /// from <c>UIButton</c>'s click and submit paths - <c>UIButton.OnSelect</c> plays a hover sound
    /// and nothing else (decompiled) - so the focus visual is the native selection alone and Enter
    /// is what draws the article.
    ///
    /// The tab row switches ON FOCUS, as the options window's does: <c>CodexMenu.SetActiveTab</c>
    /// re-spawns the categories and draws the first article at once (decompiled, only the tab marker
    /// is tweened), so arriving at a tab and arriving at its page are one event. Enter does it too.
    /// The tab already showing is never re-selected natively, because that selection is where the
    /// game records WHICH ARTICLE it is drawing (<c>CodexMenu.HandleContentButtonClicked</c> sets
    /// the event system's selection) and taking it would lose the article stop's landing.
    ///
    /// The article's body is a stop of its own, NAMED after the article's top-level heading, so
    /// entering it says which article is being read. Every other heading in the body is the REGION
    /// its lines belong to rather than a line of its own - unless it heads nothing, which is the
    /// options window's rule for a caption over an empty group.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): <c>CodexMenu.Show</c> registers
    /// <c>InputActions.UI.ExitMenu</c> on <c>Hide</c> outside its gamepad branch (decompiled,
    /// line 108), so the key already closes the window.
    /// </summary>
    public sealed class CodexScreen : GraphScreen
    {
        private const string TabsStop = "codex-tabs";
        private const string ArticlesStop = "codex-articles";
        private const string ContentStop = "codex-content";
        private const string FooterStop = "codex-footer";

        private static readonly PropertyInfo ContainerProperty = AccessTools.Property(typeof(MonoInstallerBase), "Container");
        private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(MonoInstallerBase), "_container");

        private readonly CodexMenuAdapter _adapter;

        // A subject of its own per synthesized node, kept across rebuilds so the reconciler seats the
        // cursor on the same line: the body's lines, which the mod reads off text meshes several of
        // them share, and the footer's Close.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public CodexScreen(CodexMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CodexMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CodexMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CodexMenuAdapter adapter = new CodexMenuAdapter(ResolveCodexMenu(installers[i]));
                if (adapter.IsPresent())
                {
                    return new CodexScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "codex"; }
        }

        /// <summary>The window's own drawn heading ("Tutorials &amp; Codex"); the line under it is
        /// the showing tab's name, which the tab bar reads.</summary>
        public override string ScreenName
        {
            get { return _adapter != null ? _adapter.Title : null; }
        }

        /// <summary>The tab row, so arrival reads which page is showing before its first line - and,
        /// with the switch-on-focus above, so arriving does not change the page.</summary>
        public override object InitialFocusStop
        {
            get { return TabsStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>Kept for the detector, which calls it when the tab or the article changes. The
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

            builder.BeginStop(ArticlesStop);
            BuildArticles(builder);

            builder.BeginStop(ContentStop);
            BuildContent(builder);

            builder.BeginStop(FooterStop);
            BuildFooter(builder);
        }

        // ---- the icon tab row ----

        private void BuildTabs(GraphBuilder builder)
        {
            IReadOnlyList<CodexMenuAdapter.TabItem> tabs = _adapter.GetTabs();
            for (int i = 0; i < tabs.Count; i++)
            {
                CodexMenuAdapter.TabItem tab = tabs[i];
                if (tab == null)
                {
                    continue;
                }

                int index = tab.Index;
                string label = tab.Label;
                NodeVtable vtable = GraphNodes.Tab(
                    () => label,
                    () => _adapter.GetActiveTabIndex() == index);
                // Focusing the tab IS switching to it; the guard keeps the showing tab's native
                // selection - which is the article the window is drawing - where the game put it.
                vtable.OnFocusVisual = () =>
                {
                    if (_adapter.GetActiveTabIndex() != index)
                    {
                        _adapter.FocusTab(index);
                    }
                };
                vtable.OnActivate = () => _adapter.FocusTab(index);
                builder.AddItem(new SyntheticNode(ControlId.Structural("codex:tab/" + index), vtable));
            }
        }

        // ---- the categories with their articles ----

        private void BuildArticles(GraphBuilder builder)
        {
            IReadOnlyList<CodexMenuAdapter.ArticleGroupItem> groups = _adapter.GetArticleGroups();
            ControlId landing = null;
            for (int g = 0; g < groups.Count; g++)
            {
                CodexMenuAdapter.ArticleGroupItem group = groups[g];
                if (group == null || group.Articles.Count == 0)
                {
                    continue;
                }

                builder.PushContext(group.Label);
                builder.SetRegion("codex:category/" + g);
                for (int a = 0; a < group.Articles.Count; a++)
                {
                    CodexMenuAdapter.ArticleItem article = group.Articles[a];
                    Component subject = article != null ? article.Button : null;
                    if (subject == null)
                    {
                        continue;
                    }

                    string label = article.Label;
                    CodexMenuAdapter.ArticleItem it = article;
                    NodeVtable vtable = GraphNodes.Button(() => label, () => _adapter.ActivateArticle(it));
                    vtable.OnFocusVisual = () => _adapter.FocusArticle(it);
                    ControlId id = ControlId.For(subject, "codex:article/" + g + "/" + a);
                    builder.AddItem(new DrawnNode(id, vtable, subject));
                    if (article.IsSelected)
                    {
                        landing = id;
                    }
                }

                builder.PopContext();
            }

            builder.SetRegion(null);
            // The article the window is drawing, so Tab into the list lands on what is being read.
            builder.LandStopOn(landing);
        }

        // ---- the article's body ----

        private void BuildContent(GraphBuilder builder)
        {
            IReadOnlyList<CodexContentItem> items = _adapter.GetContentItems();
            if (items.Count == 0)
            {
                return;
            }

            // The article's own heading names the stop rather than being a line in it, so entering
            // the body says which article is open, once.
            bool namedStop = items[0] != null
                && items[0].Kind == CodexContentItemKind.Heading
                && HeadsSomething(items, 0);
            if (namedStop)
            {
                builder.PushContext(items[0].Text);
                builder.SetRegion("codex:content/0");
            }

            bool inHeading = false;
            for (int i = namedStop ? 1 : 0; i < items.Count; i++)
            {
                CodexContentItem item = items[i];
                if (item == null)
                {
                    continue;
                }

                if (item.Kind == CodexContentItemKind.Heading)
                {
                    if (inHeading)
                    {
                        builder.PopContext();
                        inHeading = false;
                    }

                    builder.SetRegion("codex:content/" + i);
                    if (HeadsSomething(items, i))
                    {
                        builder.PushContext(item.Text);
                        inHeading = true;
                        continue;
                    }

                    // A heading with nothing under it is the only thing there is to say about that
                    // part of the article, so it is read as a line rather than lost as an empty
                    // region - the options window's rule for a caption over no rows.
                }
                AddContentLine(builder, item, i);
            }

            if (inHeading)
            {
                builder.PopContext();
            }

            if (namedStop)
            {
                builder.PopContext();
            }

            builder.SetRegion(null);
        }

        /// <summary>Whether the heading at <paramref name="index"/> heads anything: the next item
        /// exists and is not a heading of its own.</summary>
        private static bool HeadsSomething(IReadOnlyList<CodexContentItem> items, int index)
        {
            int next = index + 1;
            return next < items.Count
                && items[next] != null
                && items[next].Kind != CodexContentItemKind.Heading;
        }

        private void AddContentLine(GraphBuilder builder, CodexContentItem item, int index)
        {
            CodexContentItem it = item;
            string text = LineText(item);
            if (string.IsNullOrWhiteSpace(text))
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Text(() => text);
            vtable.OnFocusVisual = () => _adapter.ScrollContentItemIntoView(it);
            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker("content/" + index), "codex:content-line/" + index),
                vtable));
        }

        /// <summary>What one item of the body reads as: its own text, or - for the essence block a
        /// unit draws as icons - its label and the amounts behind them.</summary>
        private static string LineText(CodexContentItem item)
        {
            if (item.Kind != CodexContentItemKind.Essence)
            {
                return item.Text;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < item.Essences.Count; i++)
            {
                if (item.Essences[i] != null && !string.IsNullOrWhiteSpace(item.Essences[i].Text))
                {
                    parts.Add(item.Essences[i].Text);
                }
            }

            if (parts.Count == 0)
            {
                return string.Empty;
            }

            string values = ModText.JoinList(parts);
            return string.IsNullOrWhiteSpace(item.Text)
                ? values
                : ModText.Get(ModStrings.UI.LabelValue, item.Text, values);
        }

        // ---- the footer ----

        private void BuildFooter(GraphBuilder builder)
        {
            if (_adapter.IsTutorialSettingsVisible())
            {
                Component reset = _adapter.ResetButton;
                if (reset != null)
                {
                    NodeVtable vtable = GraphNodes.Button(
                        () => _adapter.ResetButtonLabel,
                        () => _adapter.ResetTutorials());
                    vtable.OnFocusVisual = () => NativeSelectionUtility.Select(reset);
                    builder.AddItem(new DrawnNode(
                        ControlId.For(reset, "codex:reset-tutorials"),
                        vtable,
                        reset));
                }

                Component toggle = _adapter.TutorialsToggle;
                if (toggle != null)
                {
                    NodeVtable vtable = GraphNodes.Checkbox(
                        () => _adapter.TutorialsToggleLabel,
                        _adapter.IsTutorialsChecked,
                        _adapter.ToggleTutorials);
                    vtable.OnFocusVisual = () => NativeSelectionUtility.Select(toggle);
                    builder.AddItem(new DrawnNode(
                        ControlId.For(toggle, "codex:show-tutorials"),
                        vtable,
                        toggle));
                }
            }

            Component close = _adapter.CloseButton;
            if (close != null)
            {
                NodeVtable vtable = GraphNodes.Button(
                    () => ModText.Get(ModStrings.Screens.Close),
                    () => _adapter.Close());
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
                builder.AddItem(new DrawnNode(ControlId.For(close, "codex:close"), vtable, close));
            }
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

        private static CodexMenu ResolveCodexMenu(CodexMenuInstaller installer)
        {
            DiContainer container = GetContainer(installer);
            if (container == null)
            {
                return null;
            }

            return container.HasBinding<CodexMenu>()
                ? container.Resolve<CodexMenu>()
                : null;
        }

        private static DiContainer GetContainer(CodexMenuInstaller installer)
        {
            if (installer == null)
            {
                return null;
            }

            if (ContainerProperty != null)
            {
                return ContainerProperty.GetValue(installer, null) as DiContainer;
            }

            return ContainerField != null ? ContainerField.GetValue(installer) as DiContainer : null;
        }
    }
}
