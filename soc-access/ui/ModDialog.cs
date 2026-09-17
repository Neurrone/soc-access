using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Addons;
using SongsOfConquest.Client;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.Menu.Options;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.Menu.Utils;
using SongsOfConquest.Client.Settings;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Adapters;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// A WINDOW OF THE MOD'S OWN, MADE OUT OF THE GAME'S PARTS.
    ///
    /// Owner ruling J: the mod's settings are a real window a sighted player can use with the mouse,
    /// not a menu that exists only in speech. Rather than draw one, this clones the live options
    /// window's <c>Panel</c> - its background, title, tab column, scrolling content column and close
    /// button - and gives the copy a <see cref="MenuFactoryController"/> of its own, so every row the
    /// mod adds is a real game toggle, slider, dropdown, field or button, drawn by the game's own
    /// factory and read back by <see cref="MenuRows"/>, which is the reader the Options screen uses.
    ///
    /// Three facts make the copy possible, measured 2026-09-06:
    ///
    /// - The options window is NOT per scene. It lives at
    ///   <c>ProjectContext(Clone)/Overlay Canvas/OptionsMenu(Clone)</c> in <c>DontDestroyOnLoad</c>,
    ///   so one source serves the main menu and a game in progress alike and the copy is made
    ///   wherever the player is.
    /// - <c>OptionsMenuInstaller</c> carries the window's <c>Settings</c> as a plain serialized field
    ///   and its <c>FactorySettings</c> beside it, and <c>MenuFactoryCollectionSettings.Install</c>
    ///   builds a WHOLE NEW collection out of a container. So the mod gets a factory collection of
    ///   its own rather than sharing the window's, and the two forms cannot clear each other's lists.
    /// - The panel's components expect Zenject (<c>UISelectionLayer.Awake</c> dereferences an
    ///   injected stack), so the copy is made INSIDE AN INACTIVE HOLDER, injected from the window's
    ///   own container, and only then switched on. Instantiated straight into a live canvas it would
    ///   wake before it had been injected and throw.
    ///
    /// Mod-owned means mod-owned: a full-screen raycast target sits behind the panel and swallows
    /// every click meant for the page underneath, the dialog beneath a stacked one has its canvas
    /// group switched off, and the screen over the top dialog claims Escape.
    ///
    /// Teardown destroys every dialog BY NAME from the canvas, so a hot reload with a dialog open
    /// leaves nothing behind for the next load to trip over.
    /// </summary>
    public sealed class ModDialog
    {
        /// <summary>What every dialog's root object is called. Teardown finds them by this.</summary>
        public const string RootName = "SocAccessModDialog";

        /// <summary>The raycast target behind one dialog.</summary>
        public const string BlockerName = "SocAccessModDialogBlocker";

        private static readonly List<ModDialog> Stack = new List<ModDialog>();

        private readonly List<Tab> _tabs = new List<Tab>();
        private GameObject _root;
        private CanvasGroup _panelGroup;
        private UITextMesh _title;
        private Transform _tabContainer;
        private Transform _activeTab;
        private AutoScrollToSelected _autoScroller;
        private UIButton _closeButton;
        private IMenuFactoryCollection _factory;
        private MenuRowMemo _rows;
        private MenuFactoryController _controller;
        private UIButton _tabPrefab;
        private Color _selectedTabColor;
        private Color _blankTabColor;
        private int _selected = -1;

        // What the reader cannot read off the game, rebuilt with the column it describes.
        private readonly FormFacts _facts = new FormFacts();

        // The table being drawn, and the rows it has so far - the drawing's own bookkeeping, alive
        // only between BeginTable and EndTable.
        private readonly List<Transform> _tableRows = new List<Transform>();
        private string _table;

        private ModDialog()
        {
        }

        /// <summary>The dialog on top, or null when none is open.</summary>
        public static ModDialog Top
        {
            get { return Stack.Count > 0 ? Stack[Stack.Count - 1] : null; }
        }

        /// <summary>
        /// Open a window. <paramref name="withTabs"/> false is the popup shape: the tab column is
        /// switched off and the caller draws its own Cancel and Confirm as rows at the foot of the
        /// content column.
        /// </summary>
        public static ModDialog Open(string title, bool withTabs)
        {
            ModDialog dialog = new ModDialog();
            if (!dialog.Build(title, withTabs))
            {
                dialog.Destroy();
                return null;
            }

            ModDialog covered = Top;
            if (covered != null)
            {
                covered.SetInteractable(false);
            }

            Stack.Add(dialog);
            NativeSoundUtility.PostEvent("Common_OpenPauseMenu");
            return dialog;
        }

        /// <summary>Destroy every dialog and forget the stack. Called from
        /// <c>SocAccessMod.Stop()</c>, and by name, because after a hot reload nothing else says
        /// which objects the mod put on the canvas.</summary>
        public static void CloseAll()
        {
            for (int i = Stack.Count - 1; i >= 0; i--)
            {
                Stack[i].Destroy();
            }

            Stack.Clear();
            Transform canvas = OptionsMenuHost.FindCanvas();
            if (canvas == null)
            {
                return;
            }

            for (int i = canvas.childCount - 1; i >= 0; i--)
            {
                Transform child = canvas.GetChild(i);
                if (child != null && child.name == RootName)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        /// <summary>Whether this dialog is still standing.</summary>
        public bool IsOpen
        {
            get { return _root != null && Stack.Contains(this); }
        }

        /// <summary>Whether this dialog is the one the player is working, rather than one covered by
        /// a dialog stacked over it.</summary>
        public bool IsTop
        {
            get { return ReferenceEquals(Top, this); }
        }

        /// <summary>The rows this dialog has drawn, read the way Options is read. A dialog that is
        /// standing has a memo: Open destroys and discards one whose Build did not finish.</summary>
        public IReadOnlyList<MenuRow> Rows
        {
            get { return _rows.Rows; }
        }

        /// <summary>The panel's own close button, as a row.</summary>
        public MenuRowButton CloseButton
        {
            get { return MenuRows.Button("mod-dialog-close", _closeButton); }
        }

        public IReadOnlyList<Tab> Tabs
        {
            get { return _tabs; }
        }

        public int SelectedTab
        {
            get { return _selected; }
        }

        /// <summary>What the panel's close button does. Assigned by whoever opened the dialog, so
        /// the mouse and the keyboard leave by the same door.</summary>
        public Action OnClose { get; set; }

        /// <summary>What to draw for a tab, or for the whole content column when there are no tabs.
        /// Called on every switch and whenever the caller asks for a redraw.</summary>
        public Action<int> DrawContent { get; set; }

        /// <summary>Add a tab. The words are the mod's; the button is a clone of the game's own tab
        /// prefab, so it is drawn, coloured and clicked like the game's.</summary>
        public Tab AddTab(string label)
        {
            if (_tabPrefab == null || _tabContainer == null)
            {
                return null;
            }

            UIButton button = UnityEngine.Object.Instantiate(_tabPrefab);
            button.transform.SetParent(_tabContainer, false);
            button.transform.localScale = Vector3.one;
            StripLocalization(button.gameObject);
            button.Text = label;
            int index = _tabs.Count;
            button.OnClicked = () => Select(index);
            Tab tab = new Tab(this, index, button);
            _tabs.Add(tab);
            return tab;
        }

        /// <summary>Show a tab's content. Redrawing is the game's own sequence: clear the column,
        /// draw, refresh the scroller.</summary>
        public bool Select(int index)
        {
            if (index < 0 || (_tabs.Count > 0 && index >= _tabs.Count))
            {
                return false;
            }

            _selected = index;
            Redraw();
            PaintTabs();
            return true;
        }

        /// <summary>Draw the content column again, keeping the tab that is showing.</summary>
        public void Redraw()
        {
            if (_controller == null || DrawContent == null)
            {
                return;
            }

            // The game's own order (OptionsMenu.DrawContent): throw the column away, forget what the
            // factory made, draw, then let the scroller measure what is there now.
            _controller.Clear();
            _factory.Clear();
            // Every fact is about a control the clear just destroyed.
            _facts.Clear();
            _table = null;
            _tableRows.Clear();
            DrawContent(_selected);
            if (_autoScroller != null)
            {
                _autoScroller.Refresh();
            }
        }

        /// <summary>Put new words in the panel's title bar - for a dialog whose title names the very
        /// thing the dialog can rename.</summary>
        public void SetTitle(string title)
        {
            if (_title != null)
            {
                _title.Text = title;
            }
        }

        /// <summary>Close this dialog and hand the page or the dialog beneath it back.</summary>
        public void Close()
        {
            bool wasOpen = Stack.Remove(this);
            Destroy();
            ModDialog covered = Top;
            if (covered != null)
            {
                covered.SetInteractable(true);
            }

            if (wasOpen)
            {
                NativeSoundUtility.PostEvent("Common_ClosePauseMenu");
            }
        }

        // ---- drawing rows ----

        /// <summary>
        /// A toggle row. The words are already localized, and the factory draws a key it does not
        /// know verbatim (<c>MenuFactoryController.GetText</c> is a TryGet with the key as its own
        /// fallback), which is what lets the mod's text through unchanged.
        /// </summary>
        public IUIToggle AddToggle(string label, bool value, Action<bool> changed, bool enabled = true, string tooltip = null)
        {
            IUIToggle toggle = _controller.AddToggle(label, value, changed);
            if (toggle != null && !enabled)
            {
                toggle.Interactable = false;
            }

            // The toggle forwards its Tooltip to the label's text mesh, which is where
            // MenuRows reads a row's tooltip from, so setting it here is what the row will speak.
            if (toggle != null && !string.IsNullOrEmpty(tooltip))
            {
                toggle.Tooltip = new TooltipDescription(tooltip);
            }

            return toggle;
        }

        public IUISlider AddSlider(string label, float value, float minimum, float maximum, Action<float> changed)
        {
            return _controller.AddSlider(label, value, minimum, maximum, changed);
        }

        /// <summary>
        /// A button row. Its tooltip is cleared: the controller asks for one under
        /// <c>Options/&lt;key&gt;/Tooltip</c> with a plain <c>GetText</c>, which answers a key it does
        /// not know with the key itself, so a mod row would otherwise carry
        /// "Options/&lt;the whole label&gt;/Tooltip" as its tooltip.
        /// </summary>
        public IUIButton AddButton(string label, Action clicked, MenuColor color = MenuColor.Blue)
        {
            IUIButton button = _controller.AddButton(label, clicked, color);
            ClearTooltip(button);
            return button;
        }

        /// <summary>
        /// A text box. <paramref name="changed"/> is the game's own event and fires on every
        /// keystroke; <paramref name="ended"/> is the END of the edit, which the game does not
        /// offer - see <see cref="AddEndEditListener"/>.
        /// </summary>
        public IUITextMeshInputField AddInputField(string label, string value, Action<string> changed, Action<string> ended = null)
        {
            IUITextMeshInputField field = _controller.AddInputField(label, value ?? string.Empty, changed);
            ClearTooltip(field);
            ClearPlaceholder(field);
            AddEndEditListener(field, ended);
            return field;
        }

        public IUITextMeshDropdown AddDropdown(string label, List<UITextMeshDropdown.Option> options, int value, Action<int> changed)
        {
            return _controller.AddTextMeshDropdown(label, options, value, changed);
        }

        /// <summary>
        /// A line of text. By default it is a CAPTION: a reader that heads regions with captions
        /// makes it the region of the rows drawn under it, so it is said on the way into them.
        /// <paramref name="standalone"/> says this text heads nothing and is a line of its own, to
        /// be landed on and read where it stands - the Bookmarks tab's file path, the answer a
        /// message dialog gives.
        /// </summary>
        public IUITextMesh AddText(string text, bool standalone = false)
        {
            IUITextMesh mesh = _controller.AddSimpleText(text);
            Component component = standalone ? mesh as Component : null;
            if (component != null)
            {
                _facts.SetStandaloneText(component.transform);
            }

            return mesh;
        }

        /// <summary>
        /// The game's own key-binding row - the label, a binding chip and the "+" - drawn for a
        /// gesture of the mod's. The widget takes plain text and callbacks
        /// (<c>OptionsMenuKeyBindContent.Draw</c> hands it the same), so nothing about it needs a game
        /// input action. The chip is drawn separately by <see cref="ShowBinding"/>, since it is
        /// what a rebind or a clear redraws.
        /// </summary>
        public IUIKeyBinding AddKeyBinding(string label, string plusTooltip, Action rebind, string tooltip = null)
        {
            IUIKeyBinding widget = _controller.AddKeyBinding();
            if (widget == null)
            {
                return null;
            }

            widget.Text = label;
            // The row's own tooltip, on the widget's transform, which is where the row reader asks.
            if (!string.IsNullOrEmpty(tooltip))
            {
                widget.Tooltip = new TooltipDescription(tooltip);
            }

            widget.AddPlusButton(plusTooltip, true, rebind);
            return widget;
        }

        /// <summary>
        /// Draw a key-binding row's chip the way the game does (<c>SetupBindButton</c>, decompiled):
        /// a dead label for the default, an interactable "remove" button for an override. Redraws in
        /// place, so the rest of the column and its scroll position stay; the row's focus has the
        /// scroller measure the new chip when it is next reached.
        /// </summary>
        public void ShowBinding(IUIKeyBinding widget, string id, string text, bool overridden, string removeTooltip, Action remove)
        {
            if (widget == null)
            {
                return;
            }

            widget.ClearButtons();
            if (overridden)
            {
                widget.AddOverrideButton(new ActionReference(id), text, removeTooltip, null, remove);
            }
            else
            {
                widget.AddDefaultBinding(text);
            }
        }

        /// <summary>Let the panel's scroller measure the column again - after a control inside a
        /// row was replaced, which a redraw of the whole column would also do.</summary>
        private void RefreshScroll()
        {
            if (_autoScroller != null)
            {
                _autoScroller.Refresh();
            }
        }

        /// <summary>
        /// Start a row of controls drawn side by side. Only ever TWO of them, unless the row is a
        /// table's (<see cref="StartTableRow"/>, which sizes its cells): a toggle is a full-width
        /// row with its box at the right, and four controls in one layout gave each toggle 381 px of
        /// a 486 px column and squeezed both buttons to nothing (measured 2026-09-07). The game
        /// itself only ever puts two buttons in one.
        ///
        /// The layout is marked as a scroll parent, because <c>AutoScrollToSelected.SearchTransform</c>
        /// walks only the DIRECT children of the content column unless a child carries
        /// <c>AutoScrollParent</c> - so without this a control inside a row is one the panel will
        /// never scroll to, and the cursor lands on a Confirm the player cannot see.
        /// </summary>
        public void StartRow()
        {
            BeginLayout();
        }

        public void EndRow()
        {
            _controller.EndLayout();
        }

        private Transform BeginLayout()
        {
            IUITransform layout = _controller.StartHorizontalLayout();
            Transform transform = layout != null ? layout.MonoTransform : null;
            if (transform != null && transform.GetComponent<AutoScrollParent>() == null)
            {
                transform.gameObject.AddComponent<AutoScrollParent>();
            }

            return transform;
        }

        // ---- tables ----

        /// <summary>
        /// Begin a table: rows drawn as horizontal layouts of one cell per column, laid out so the
        /// columns line up down the table and read as one.
        ///
        /// <paramref name="captions"/> are what the reader SAYS on crossing into each column, the
        /// primary's first and a null entry for a column crossed in silence. They are not the drawn
        /// header - a table that wants one draws it with <see cref="StartHeaderRow"/> - because the
        /// two are separate decisions: the move columns here are captioned by their buttons' own
        /// spoken labels and draw no header at all.
        /// </summary>
        public void BeginTable(string key, string[] captions)
        {
            _table = key;
            _tableRows.Clear();
            _facts.SetColumns(key, captions);
        }

        /// <summary>One row of the table being drawn, known across redraws by
        /// <paramref name="rowRef"/> - so the cursor can be put back on the cell of the very row the
        /// player moved, wherever the redraw put it.</summary>
        public void StartTableRow(string rowRef)
        {
            Transform row = BeginLayout();
            if (row != null)
            {
                _tableRows.Add(row);
                _facts.SetRow(row, _table, rowRef);
            }
        }

        /// <summary>The table's drawn header band: a row of texts over the columns, which is DRAWN
        /// and not read - the reader says the captions instead, on crossing.</summary>
        public void StartHeaderRow()
        {
            StartTableRow(null);
        }

        /// <summary>Finish the table and give every column the width its content needs: each cell is
        /// as wide as the widest thing in its column and no wider, so the table is compact and left
        /// aligned with the room it does not need left over at its right.</summary>
        public void EndTable()
        {
            LayOutColumns();
            _table = null;
            _tableRows.Clear();
        }

        /// <summary>A checkbox cell: the game's toggle with the full-width strip and the label it
        /// draws them for switched off, so what is left is the box. A table cell says what it is
        /// through its column, not through words of its own beside every box.</summary>
        public IUIToggle AddToggleCell(bool value, Action<bool> changed)
        {
            IUIToggle toggle = _controller.AddToggle(string.Empty, value, changed);
            Compact(toggle);
            return toggle;
        }

        /// <summary>What the reader says for a control whose drawn words are not words at all - an
        /// arrow glyph on a button. The drawing stays the game's; only the reading is replaced.
        /// </summary>
        public void SpeakAs(IUITransform control, string spoken)
        {
            Transform transform = control != null ? control.MonoTransform : null;
            if (transform != null)
            {
                _facts.SetSpokenLabel(transform, spoken);
            }
        }

        /// <summary>What a reader has to be told about this form that it cannot read off the game:
        /// which drawn layouts are a table's rows, what that table's spoken column captions are, and
        /// what a control whose drawn words are a glyph is called.</summary>
        public FormFacts Facts
        {
            get { return _facts; }
        }

        /// <summary>
        /// Width by content, column by column: every cell of a column is given the widest of the
        /// column's own preferred widths, and the layout is told to stop stretching its children, so
        /// nothing is padded out to fill the panel.
        ///
        /// DRAWING: the table is measured once, as it is finished, and never again.
        /// </summary>
        private void LayOutColumns()
        {
            List<float> widths = new List<float>();
            for (int i = 0; i < _tableRows.Count; i++)
            {
                Transform row = _tableRows[i];
                for (int c = 0; c < row.childCount; c++)
                {
                    float width = FindCellWidth(row.GetChild(c));
                    if (c < widths.Count)
                    {
                        widths[c] = Mathf.Max(widths[c], width);
                    }
                    else
                    {
                        widths.Add(width);
                    }
                }
            }

            for (int i = 0; i < _tableRows.Count; i++)
            {
                Transform row = _tableRows[i];
                HorizontalLayoutGroup group = row.GetComponent<HorizontalLayoutGroup>();
                if (group != null)
                {
                    // The game's row stretches its children across the whole column; a table's
                    // columns are as wide as what is in them. The row is already aligned to the
                    // left (the prefab's alignment is MiddleLeft), so the spare room falls at the
                    // right where nothing is drawn.
                    group.childForceExpandWidth = false;
                }

                for (int c = 0; c < row.childCount && c < widths.Count; c++)
                {
                    LayoutElement element = CellElement(row.GetChild(c));
                    element.minWidth = widths[c];
                    element.preferredWidth = widths[c];
                    element.flexibleWidth = 0f;
                }
            }
        }

        /// <summary>How wide one cell has to be: the checkbox where the cell is a bare one, and
        /// otherwise the widest line of text it draws plus the frame its control draws around it.
        /// DRAWING: one walk per cell, as the table is laid out.</summary>
        private static float FindCellWidth(Transform cell)
        {
            if (cell.GetComponent<UIToggle>() != null)
            {
                return CheckboxSize.x;
            }

            float text = 0f;
            TMP_Text[] meshes = cell.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < meshes.Length; i++)
            {
                text = Mathf.Max(text, meshes[i].GetPreferredValues().x);
            }

            return text + (cell.GetComponent<UIButton>() != null ? ButtonPadding : 0f);
        }

        private static LayoutElement CellElement(Transform cell)
        {
            LayoutElement element = cell.GetComponent<LayoutElement>();
            return element != null ? element : cell.gameObject.AddComponent<LayoutElement>();
        }

        /// <summary>
        /// Strip the game's toggle down to its box. The widget is a full-width row because of two
        /// children - <c>Background</c>, the strip the label sits on, and <c>Text</c>, the label -
        /// and because its <c>Toggle</c> child is a 450-wide band held 558 px in from the left, with
        /// the box itself at the band's left edge. Switch the two off, pull the band back to the
        /// left and shrink it to the box, and what is drawn is a checkbox and nothing else.
        /// </summary>
        private static void Compact(IUIToggle toggle)
        {
            Component component = toggle as Component;
            Transform root = component != null ? component.transform : null;
            if (root == null)
            {
                return;
            }

            // The factory answers a label it does not know with the key it was asked for, prefixed:
            // an empty label came back as "Options/", which is what a reader of the drawn row would
            // then say. A cell draws no label at all, so it has none to say.
            toggle.Text = string.Empty;
            Deactivate(root.Find("Background"));
            Deactivate(root.Find("Text"));
            RectTransform box = root.Find("Toggle") as RectTransform;
            if (box != null)
            {
                box.anchoredPosition = new Vector2(0f, box.anchoredPosition.y);
                box.sizeDelta = CheckboxSize;
            }
        }

        private static void Deactivate(Transform child)
        {
            if (child != null)
            {
                child.gameObject.SetActive(false);
            }
        }

        /// <summary>The box of the game's toggle, measured off the prefab's own checkbox art.
        /// </summary>
        private static readonly Vector2 CheckboxSize = new Vector2(56f, 57f);

        /// <summary>The frame a button draws around its words, in canvas units.</summary>
        private const float ButtonPadding = 64f;

        // ---- the copy itself ----

        private bool Build(string title, bool withTabs)
        {
            OptionsMenuInstaller installer = OptionsMenuHost.FindInstaller();
            DiContainer container = OptionsMenuHost.ContainerOf(installer);
            OptionsMenu.Settings settings = installer != null ? installer.settings : null;
            Transform panel = OptionsMenuHost.PanelOf(installer);
            Transform canvas = OptionsMenuHost.CanvasOf(panel);
            if (container == null || settings == null || panel == null || canvas == null)
            {
                Warn("the options window's panel could not be found, so no dialog was drawn");
                return false;
            }

            _root = new GameObject(RootName, typeof(RectTransform));
            RectTransform rootRect = (RectTransform)_root.transform;
            // Off while the copy is made: the panel's components are injected, and one of them
            // dereferences its injected stack in Awake.
            _root.SetActive(false);
            rootRect.SetParent(canvas, false);
            Stretch(rootRect);
            rootRect.SetAsLastSibling();

            AddBlocker(rootRect);
            GameObject clone = UnityEngine.Object.Instantiate(panel.gameObject, rootRect);
            clone.name = "Panel";
            container.InjectGameObject(clone);
            FillCanvas(clone.transform as RectTransform, canvas as RectTransform);
            _root.SetActive(true);

            _panelGroup = clone.GetComponent<CanvasGroup>();
            if (_panelGroup != null)
            {
                _panelGroup.alpha = 1f;
                _panelGroup.interactable = true;
                _panelGroup.blocksRaycasts = true;
            }

            Transform cloned = clone.transform;
            _title = Find<UITextMesh>(cloned, "Title");
            if (_title != null)
            {
                StripLocalization(_title.gameObject);
                _title.Text = title;
            }

            _tabContainer = cloned.Find("TabContainer");
            if (_tabContainer != null)
            {
                _activeTab = _tabContainer.Find("ActiveTab");
                DestroyChildrenExcept(_tabContainer, _activeTab);
                _tabContainer.gameObject.SetActive(withTabs);
            }

            Transform scroll = cloned.Find("ContentScrollEntry");
            _autoScroller = scroll != null ? scroll.GetComponent<AutoScrollToSelected>() : null;
            Transform content = scroll != null ? scroll.Find("Viewport/Content") : null;
            IUITransform contentParent = content != null ? content.GetComponent<UITransform>() : null;
            if (contentParent == null)
            {
                Warn("the copied panel has no content column");
                return false;
            }

            DestroyChildrenExcept(content, null);
            _closeButton = Find<UIButton>(cloned, "CloseButton");
            if (_closeButton != null)
            {
                _closeButton.OnClicked = HandleCloseClicked;
            }

            _tabPrefab = settings.tabButtonPrefab;
            _selectedTabColor = settings.selectedTextColor;
            _blankTabColor = settings.blankTextColor;

            DiContainer own = container.CreateSubContainer();
            installer.FactorySettings.Install(own, null);
            _factory = own.Resolve<IMenuFactoryCollection>();
            // No rebindable game actions here; the source only lends the key-binding rows the
            // scroller refresh their focus asks for after a chip was redrawn.
            _rows = new MenuRowMemo(_factory, content, new KeyBindingSource { RefreshScroll = RefreshScroll });
            _controller = new MenuFactoryController(
                _factory,
                container.Resolve<IClientSettings>(),
                container.Resolve<IInputManager>(),
                contentParent,
                container.Resolve<ILocalizationHandler>(),
                // The prefix the options window itself uses, so a row whose words happen to be a
                // real Options key is localized exactly as that window would localize it.
                "Options",
                container.Resolve<IAddonManager>());
            return true;
        }

        /// <summary>
        /// How much smaller than the canvas the panel is drawn - the frame of page left showing
        /// around it, in canvas units. The source panel is a fixed 2000 x 1400 on a 3456 x 2160
        /// canvas, which leaves the mod's own rows a third of the screen to be drawn in and its
        /// tables no room for their columns.
        /// </summary>
        private static readonly Vector2 CanvasMargin = new Vector2(156f, 120f);

        /// <summary>
        /// Give the copy the whole screen. The panel is centre-anchored, so its size is its
        /// <c>sizeDelta</c> and its offset from the middle its <c>anchoredPosition</c>; nothing else
        /// has to move, because the background is a nine-sliced sprite and every part of the panel
        /// is anchored to an edge or a corner of it - the decorations stay centred, the title and
        /// the content column stretch, the tabs stay top-left and the close button top-right.
        /// </summary>
        private static void FillCanvas(RectTransform panel, RectTransform canvas)
        {
            if (panel == null || canvas == null)
            {
                return;
            }

            panel.sizeDelta = canvas.rect.size - CanvasMargin;
            panel.anchoredPosition = Vector2.zero;
        }

        private void AddBlocker(RectTransform parent)
        {
            GameObject blocker = new GameObject(BlockerName, typeof(RectTransform), typeof(Image));
            RectTransform rect = (RectTransform)blocker.transform;
            rect.SetParent(parent, false);
            Stretch(rect);
            Image image = blocker.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0.55f);
            image.raycastTarget = true;
        }

        private void HandleCloseClicked()
        {
            try
            {
                Action close = OnClose;
                if (close != null)
                {
                    close();
                }
                else
                {
                    Close();
                }
            }
            catch (Exception exception)
            {
                // Inside the engine's own dispatch: never throw into it.
                Warn("closing from the drawn button threw: " + exception);
            }
        }

        private void PaintTabs()
        {
            for (int i = 0; i < _tabs.Count; i++)
            {
                UIButton button = _tabs[i].Button;
                if (button == null)
                {
                    continue;
                }

                // Rebuilt without its alpha, as OptionsMenu.SetSelectedTab does: the colours are
                // authored on the settings asset with no alpha, so the words are invisible if the
                // stored value is used as it stands.
                Color color = i == _selected ? _selectedTabColor : _blankTabColor;
                button.TextColor = new Color(color.r, color.g, color.b);
                if (i == _selected && _activeTab != null)
                {
                    _activeTab.localPosition = button.transform.localPosition;
                }
            }
        }

        private void SetInteractable(bool value)
        {
            if (_panelGroup != null)
            {
                _panelGroup.interactable = value;
            }
        }

        private void Destroy()
        {
            if (_root != null)
            {
                UnityEngine.Object.DestroyImmediate(_root);
            }

            // Every one of these pointed into the object just destroyed, and a destroyed Unity
            // object compares equal to null but is not null: a field still holding one answers a
            // later read with a dead component rather than with nothing.
            _root = null;
            _panelGroup = null;
            _title = null;
            _tabContainer = null;
            _activeTab = null;
            _autoScroller = null;
            _closeButton = null;
            _tabPrefab = null;
            _rows = null;
            _tabs.Clear();
            _selected = -1;
            _factory = null;
            _controller = null;
            DrawContent = null;
            OnClose = null;
        }

        // ---- odds and ends ----

        /// <summary>
        /// Where a value the mod has to check is committed. The game's box reports a CHANGE per
        /// keystroke and nothing at all when the player is done, but the TextMeshPro field
        /// underneath raises <c>onEndEdit</c> on Enter, on Escape - by which time TMP has already
        /// put the pre-edit text back - and when the focus leaves the box. The listener sits on
        /// that field, which is destroyed with the column it was drawn into, so a redraw or a
        /// teardown leaves nothing subscribed.
        /// </summary>
        private static void AddEndEditListener(IUITextMeshInputField field, Action<string> ended)
        {
            if (ended == null)
            {
                return;
            }

            TMP_InputField input = NativeInputOf(field);
            if (input != null)
            {
                input.onEndEdit.AddListener(text => ended(text));
            }
        }

        /// <summary>Take the prefab's design-time placeholder ("XXXXXXXXXX") off an empty box, which
        /// is otherwise what an empty keyword field draws.</summary>
        private static void ClearPlaceholder(IUITextMeshInputField field)
        {
            TMP_InputField input = NativeInputOf(field);
            TMP_Text placeholder = input != null ? input.placeholder as TMP_Text : null;
            if (placeholder != null)
            {
                placeholder.text = string.Empty;
            }
        }

        /// <summary>The TextMeshPro field inside one of the game's text rows: the row is a wrapper
        /// and the box itself is a child of it. DRAWING, not a build: a row is walked once, as it is
        /// made, and never again.</summary>
        private static TMP_InputField NativeInputOf(IUITextMeshInputField field)
        {
            Component component = field as Component;
            return component != null ? component.GetComponentInChildren<TMP_InputField>(true) : null;
        }

        private static void ClearTooltip(IUITransform control)
        {
            if (control != null)
            {
                control.Tooltip = new TooltipDescription(string.Empty);
            }
        }

        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            rect.localScale = Vector3.one;
        }

        private static T Find<T>(Transform root, string name) where T : Component
        {
            Transform child = root != null ? root.Find(name) : null;
            return child != null ? child.GetComponent<T>() : null;
        }

        private static void DestroyChildrenExcept(Transform parent, Transform kept)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child != kept)
                {
                    UnityEngine.Object.DestroyImmediate(child.gameObject);
                }
            }
        }

        // CONSTRUCTION: a freshly instantiated clone of the mod's own dialog, walked once while it
        // is being built.
        private static void StripLocalization(GameObject cloned)
        {
            UITextMeshLocalization[] bindings = cloned.GetComponentsInChildren<UITextMeshLocalization>(true);
            for (int i = 0; i < bindings.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(bindings[i]);
            }
        }

        private static void Warn(string message)
        {
            SocAccessMod.Instance?.LogWarning("Mod dialog: " + message);
        }

        /// <summary>
        /// WHAT A READER CANNOT READ OFF THE DRAWN FORM.
        ///
        /// Everything else about a row the reader gets from the game - its words, its state, whether
        /// it is drawn at all. These are the mod's own decisions, taken as the form was drawn:
        /// which horizontal layouts are a TABLE's rows and which row each one is, what that table's
        /// columns are CALLED in speech, what a control whose drawn words are a glyph is called, and
        /// which texts are lines of their own rather than captions over the rows under them.
        ///
        /// Facts, not nodes: nothing here knows about node ids, stops or regions. What a table
        /// becomes in the tree is <see cref="MenuFormNodes"/>'s business.
        /// </summary>
        public sealed class FormFacts
        {
            private readonly Dictionary<Transform, DrawnRow> _rows = new Dictionary<Transform, DrawnRow>();
            private readonly Dictionary<string, string[]> _columns = new Dictionary<string, string[]>();
            private readonly Dictionary<Transform, string> _spoken = new Dictionary<Transform, string>();
            private readonly HashSet<Transform> _standalone = new HashSet<Transform>();

            /// <summary>One drawn horizontal layout that is a table's row.</summary>
            public sealed class DrawnRow
            {
                public DrawnRow(Transform layout, string table, string rowRef)
                {
                    Layout = layout;
                    Table = table;
                    RowRef = rowRef;
                }

                /// <summary>The layout the row is drawn as: what its cells are scrolled into view
                /// by, and the evidence the game is still drawing them.</summary>
                public Transform Layout { get; private set; }

                /// <summary>Which table it belongs to.</summary>
                public string Table { get; private set; }

                /// <summary>The row's identity across redraws, or null for the drawn header band,
                /// which is not a row at all.</summary>
                public string RowRef { get; private set; }
            }

            /// <summary>Whether this form drew any table.</summary>
            public bool HasTables
            {
                get { return _rows.Count > 0; }
            }

            /// <summary>The table row a layout is, or null for a layout that is not one.</summary>
            public DrawnRow RowOf(Transform layout)
            {
                DrawnRow row;
                return layout != null && _rows.TryGetValue(layout, out row) ? row : null;
            }

            /// <summary>What a table's columns are called in speech, the primary's first; a null
            /// entry is a column crossed in silence.</summary>
            public string[] ColumnsOf(string table)
            {
                string[] columns;
                return table != null && _columns.TryGetValue(table, out columns) ? columns : null;
            }

            /// <summary>What a control is called, where its drawn words are not what it is called;
            /// null where they are.</summary>
            public string SpokenLabelOf(Transform control)
            {
                string spoken;
                return control != null && _spoken.TryGetValue(control, out spoken) ? spoken : null;
            }

            public void SetRow(Transform layout, string table, string rowRef)
            {
                if (layout != null)
                {
                    _rows[layout] = new DrawnRow(layout, table, rowRef);
                }
            }

            public void SetColumns(string table, string[] columns)
            {
                if (table != null)
                {
                    _columns[table] = columns;
                }
            }

            public void SetSpokenLabel(Transform control, string spoken)
            {
                if (control != null)
                {
                    _spoken[control] = spoken;
                }
            }

            /// <summary>Whether a text is a line of its own rather than a caption over the rows
            /// under it.</summary>
            public bool IsStandaloneText(Transform text)
            {
                return text != null && _standalone.Contains(text);
            }

            public void SetStandaloneText(Transform text)
            {
                if (text != null)
                {
                    _standalone.Add(text);
                }
            }

            public void Clear()
            {
                _rows.Clear();
                _columns.Clear();
                _spoken.Clear();
                _standalone.Clear();
            }
        }

        /// <summary>One of the dialog's category tabs.</summary>
        public sealed class Tab
        {
            private readonly ModDialog _dialog;
            private readonly int _index;

            public Tab(ModDialog dialog, int index, UIButton button)
            {
                _dialog = dialog;
                _index = index;
                Button = button;
            }

            public UIButton Button { get; private set; }

            public string GetLabel()
            {
                return MenuRows.Label(Button);
            }

            public bool IsSelected()
            {
                return _dialog._selected == _index;
            }

            public bool IsVisible()
            {
                return Button != null && Button.gameObject.activeInHierarchy;
            }

            public bool Select()
            {
                return _dialog.Select(_index);
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(Button);
            }
        }
    }
}
