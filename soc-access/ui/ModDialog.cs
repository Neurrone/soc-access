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

        private static readonly PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(OptionsMenuInstaller), "Container");

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
        private MenuFactoryController _controller;
        private UIButton _tabPrefab;
        private Color _selectedTabColor;
        private Color _blankTabColor;
        private int _selected = -1;

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
            Transform canvas = FindCanvas();
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

        /// <summary>The rows this dialog has drawn, read the way Options is read.</summary>
        public IReadOnlyList<MenuRow> Rows
        {
            get { return MenuRows.Read(_factory); }
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
            DrawContent(_selected);
            if (_autoScroller != null)
            {
                _autoScroller.Refresh();
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
        public IUIToggle AddToggle(string label, bool value, Action<bool> changed, bool enabled = true)
        {
            IUIToggle toggle = _controller.AddToggle(label, value, changed);
            if (toggle != null && !enabled)
            {
                toggle.Interactable = false;
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

        public IUITextMeshInputField AddInputField(string label, string value, Action<string> changed)
        {
            IUITextMeshInputField field = _controller.AddInputField(label, value ?? string.Empty, changed);
            ClearTooltip(field);
            ClearPlaceholder(field);
            return field;
        }

        public IUITextMeshDropdown AddDropdown(string label, List<UITextMeshDropdown.Option> options, int value, Action<int> changed)
        {
            return _controller.AddTextMeshDropdown(label, options, value, changed);
        }

        public IUITextMesh AddText(string text)
        {
            return _controller.AddSimpleText(text);
        }

        /// <summary>
        /// Start a row of controls drawn side by side. Only ever TWO of them: a toggle is a
        /// full-width row with its box at the right, and four controls in one layout gave each
        /// toggle 381 px of a 486 px column and squeezed both buttons to nothing (measured
        /// 2026-09-07). The game itself only ever puts two buttons in one.
        ///
        /// The layout is marked as a scroll parent, because <c>AutoScrollToSelected.SearchTransform</c>
        /// walks only the DIRECT children of the content column unless a child carries
        /// <c>AutoScrollParent</c> - so without this a control inside a row is one the panel will
        /// never scroll to, and the cursor lands on a Confirm the player cannot see.
        /// </summary>
        public void StartRow()
        {
            IUITransform layout = _controller.StartHorizontalLayout();
            Transform transform = layout != null ? layout.MonoTransform : null;
            if (transform != null && transform.GetComponent<AutoScrollParent>() == null)
            {
                transform.gameObject.AddComponent<AutoScrollParent>();
            }
        }

        public void EndRow()
        {
            _controller.EndLayout();
        }

        // ---- the copy itself ----

        private bool Build(string title, bool withTabs)
        {
            OptionsMenuInstaller installer = FindInstaller();
            DiContainer container = Container(installer);
            OptionsMenu.Settings settings = installer != null ? installer.settings : null;
            Transform panel = settings != null && settings.parent != null
                ? settings.parent.MonoTransform.Find("Panel")
                : null;
            Transform canvas = panel != null ? panel.parent.parent : null;
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

            _root = null;
            _tabs.Clear();
            _factory = null;
            _controller = null;
            DrawContent = null;
            OnClose = null;
        }

        // ---- odds and ends ----

        /// <summary>Take the prefab's design-time placeholder ("XXXXXXXXXX") off an empty box, which
        /// is otherwise what an empty keyword field draws.</summary>
        private static void ClearPlaceholder(IUITextMeshInputField field)
        {
            Component component = field as Component;
            TMP_InputField input = component != null ? component.GetComponentInChildren<TMP_InputField>(true) : null;
            TMP_Text placeholder = input != null ? input.placeholder as TMP_Text : null;
            if (placeholder != null)
            {
                placeholder.text = string.Empty;
            }
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

        private static void StripLocalization(GameObject cloned)
        {
            UITextMeshLocalization[] bindings = cloned.GetComponentsInChildren<UITextMeshLocalization>(true);
            for (int i = 0; i < bindings.Length; i++)
            {
                UnityEngine.Object.DestroyImmediate(bindings[i]);
            }
        }

        private static Transform FindCanvas()
        {
            OptionsMenuInstaller installer = FindInstaller();
            OptionsMenu.Settings settings = installer != null ? installer.settings : null;
            Transform panel = settings != null && settings.parent != null
                ? settings.parent.MonoTransform.Find("Panel")
                : null;
            return panel != null ? panel.parent.parent : null;
        }

        private static OptionsMenuInstaller FindInstaller()
        {
            OptionsMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<OptionsMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                OptionsMenuInstaller installer = installers[i];
                if (installer != null
                    && installer.gameObject != null
                    && installer.gameObject.scene.isLoaded
                    && installer.settings != null)
                {
                    return installer;
                }
            }

            return null;
        }

        private static DiContainer Container(OptionsMenuInstaller installer)
        {
            if (installer == null || InstallerContainerProperty == null)
            {
                return null;
            }

            return InstallerContainerProperty.GetValue(installer, null) as DiContainer;
        }

        private static void Warn(string message)
        {
            SocAccessMod.Instance?.LogWarning("Mod dialog: " + message);
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
