using System;
using _8_UILayer.ClientView.Menu.Paus;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE WAY INTO THE MOD'S OPTIONS FOR A MOUSE - one entry DRAWN on the main menu beside the
    /// game's own Options, and one DRAWN in the pause menu straight after it, both called
    /// "Mod options" and both opening the mod's options dialog.
    ///
    /// Owner ruling J (2026-09-06, `ui-graph-plan.md` section 10): the mod's settings stop being a
    /// keyboard-only menu on Ctrl+M. A sighted player sharing the machine has to be able to find
    /// them, so each entry is a REAL button of the game's own kind, clickable with the mouse and
    /// read by each screen exactly the way it reads the game's own buttons.
    ///
    /// Nothing here re-implements a menu; each menu is handed one more button of its own:
    ///
    /// - THE MAIN MENU draws Options as a corner pair in <c>CanvasForeground &gt; HeaderAndFooter &gt;
    ///   Header &gt; UtilityButtons</c>: a right-aligned <c>OptionsLabel</c> and a 32 px
    ///   <c>OptionsButton</c> at screen x 1233. That container is a bare <c>RectTransform</c> with no
    ///   layout group (measured 2026-09-06), so both children are placed by their own anchored
    ///   positions and the clone has to be placed too: the pair is laid out to the LEFT of the drawn
    ///   "Options" text, keeping the same label-to-button gap the game uses, and every number is read
    ///   off the live pair every time it is built so a font or a resolution change cannot strand it.
    ///
    /// - THE PAUSE MENU's items are fixed serialized <c>UIButton</c> fields
    ///   (<c>PauseMenu.Settings</c>), not a list, so the entry is a clone of <c>optionsButton</c>
    ///   dropped into the same parent at the sibling index straight after it; the container's own
    ///   layout spreads it with the rest.
    ///
    /// The entries are made LAZILY, from <see cref="Tick"/> on the mod's own pump, as soon as the
    /// menu that owns them is loaded and the entry is not there yet. That covers the cold start, a
    /// scene change and - the case a one-shot readiness hook does not - a hot reload with the menu
    /// already on screen, because teardown has just destroyed the previous load's entry.
    ///
    /// Teardown destroys both BY NAME, from the live parents, because after a hot reload the old
    /// load's types are not this one's and nothing else identifies what the mod put there.
    /// </summary>
    public static class ModOptionsEntries
    {
        /// <summary>The main menu's clone of the corner button. Distinctive, because teardown finds
        /// it by name.</summary>
        public const string MainMenuButtonName = "SocAccessModOptionsButton";

        /// <summary>The words drawn beside it, a clone of the game's own <c>OptionsLabel</c>.
        /// </summary>
        public const string MainMenuLabelName = "SocAccessModOptionsLabel";

        /// <summary>The pause menu's clone of its Options item.</summary>
        public const string PauseMenuButtonName = "SocAccessModOptionsPauseButton";

        /// <summary>The gap drawn between the game's Options text and the mod's button, in the
        /// header's own units. Wider than the label-to-button gap inside a pair, so the two pairs
        /// read as two.</summary>
        private const float MainMenuPairGap = 40f;

        /// <summary>What opening the dialog does. Set once by the mod, so the click and Ctrl+M go
        /// through one door.</summary>
        public static Func<bool> Open { get; set; }

        /// <summary>The words on both entries.</summary>
        public static string Title()
        {
            return ModText.Get(ModStrings.Screens.ModOptions);
        }

        /// <summary>The main menu's entry, for the screen that lists the header; null while the main
        /// menu is not drawn.</summary>
        public static IMenuButtonAdapter MainMenuEntry
        {
            get
            {
                return _mainMenuButton == null ? null : new HeaderEntry(_mainMenuButton, _mainMenuLabel);
            }
        }

        /// <summary>The pause menu's entry, for the adapter that lists its items; null while the
        /// pause menu is not drawn.</summary>
        public static UIButton PauseMenuButton
        {
            get { return _pauseMenuButton; }
        }

        /// <summary>Put each entry on whichever menu is drawn, and keep its words right. Called once
        /// a frame; the whole cost on a frame where both are already there is two null checks and a
        /// string comparison.</summary>
        public static void Tick()
        {
            if (_stopped)
            {
                return;
            }

            try
            {
                TickMainMenu();
            }
            catch (Exception exception)
            {
                Warn("the main menu threw: " + exception);
            }

            try
            {
                TickPauseMenu();
            }
            catch (Exception exception)
            {
                Warn("the pause menu threw: " + exception);
            }
        }

        /// <summary>Give both menus back exactly what they had. Called from
        /// <c>SocAccessMod.Stop()</c>.</summary>
        public static void Remove()
        {
            _stopped = true;
            try
            {
                DestroyByName(
                    _mainMenuOptions != null ? _mainMenuOptions : FindMainMenuOptionsButton(),
                    MainMenuButtonName,
                    MainMenuLabelName);
                DestroyByName(
                    _pauseMenuOptions != null ? _pauseMenuOptions : FindPauseMenuOptionsButton(),
                    PauseMenuButtonName);
            }
            catch (Exception exception)
            {
                Warn("unpicking threw: " + exception);
            }
            finally
            {
                _mainMenuButton = null;
                _mainMenuLabel = null;
                _pauseMenuButton = null;
                _mainMenuOptions = null;
                _pauseMenuOptions = null;
                Open = null;
                _stopped = false;
            }
        }

        // ---- the main menu ----

        private static void TickMainMenu()
        {
            UIButton options = MainMenuOptionsButton();
            if (options == null || !options.gameObject.activeInHierarchy)
            {
                _mainMenuButton = null;
                _mainMenuLabel = null;
                return;
            }

            Transform parent = options.transform.parent;
            Transform label = parent != null ? parent.Find("OptionsLabel") : null;
            if (parent == null || label == null)
            {
                return;
            }

            if (_mainMenuButton == null || _mainMenuButton.transform.parent != parent)
            {
                Transform standing = parent.Find(MainMenuButtonName);
                _mainMenuButton = standing != null ? standing.GetComponent<UIButton>() : null;
                Transform standingLabel = parent.Find(MainMenuLabelName);
                _mainMenuLabel = standingLabel != null ? standingLabel.GetComponent<UITextMesh>() : null;
            }

            if (_mainMenuButton == null || _mainMenuLabel == null)
            {
                Create(options, label);
            }

            Relabel();
        }

        private static void Create(UIButton options, Transform label)
        {
            Transform parent = options.transform.parent;
            GameObject madeLabel = UnityEngine.Object.Instantiate(label.gameObject, parent);
            madeLabel.name = MainMenuLabelName;
            // The clone brought the game's own localization binding with it, which would write
            // "Options" back over the mod's words on the next language change.
            StripLocalization(madeLabel);
            UITextMesh text = madeLabel.GetComponent<UITextMesh>();

            GameObject madeButton = UnityEngine.Object.Instantiate(options.gameObject, parent);
            madeButton.name = MainMenuButtonName;
            StripLocalization(madeButton);
            UIButton button = madeButton.GetComponent<UIButton>();
            if (text == null || button == null)
            {
                UnityEngine.Object.Destroy(madeLabel);
                UnityEngine.Object.Destroy(madeButton);
                Warn("the cloned header pair is missing its text or its button");
                return;
            }

            // A runtime delegate, so the clone arrived with none of the game's own handlers on it.
            button.OnClicked = HandleClicked;
            _mainMenuLabel = text;
            _mainMenuButton = button;
            Relabel();
            Place(options, label);
            SocAccessMod.Instance?.LogInfo("Mod options entry: added to the main menu header");
        }

        /// <summary>
        /// Put the pair where the header would have put it, had the header laid anything out.
        ///
        /// It does not: <c>UtilityButtons</c> carries a bare <c>RectTransform</c>, so the game's
        /// label and button sit at the anchored positions their prefab was authored with, and a
        /// child added to it lands wherever its own numbers say. The mod's pair therefore mirrors
        /// the game's arithmetic - the same label-to-button gap, the same anchors - a clear gap to
        /// the left of where the drawn word "Options" starts, which is the text's own rendered
        /// width rather than its rectangle's (the label is a right-aligned box the full width of
        /// the header).
        /// </summary>
        private static void Place(UIButton options, Transform label)
        {
            RectTransform gameButton = options.transform as RectTransform;
            RectTransform gameLabel = label as RectTransform;
            RectTransform myButton = _mainMenuButton != null ? _mainMenuButton.transform as RectTransform : null;
            RectTransform myLabel = _mainMenuLabel != null ? _mainMenuLabel.transform as RectTransform : null;
            if (gameButton == null || gameLabel == null || myButton == null || myLabel == null)
            {
                return;
            }

            float half = gameButton.rect.width * 0.5f;
            float labelRight = gameLabel.anchoredPosition.x;
            float labelToButton = gameButton.anchoredPosition.x - half - labelRight;
            float drawnWidth = RenderedWidth(label.GetComponent<TMP_Text>());

            float myButtonCentre = labelRight - drawnWidth - MainMenuPairGap - half;
            myButton.anchoredPosition = new Vector2(myButtonCentre, gameButton.anchoredPosition.y);
            myLabel.anchoredPosition = new Vector2(
                myButtonCentre - half - labelToButton,
                gameLabel.anchoredPosition.y);
        }

        private static float RenderedWidth(TMP_Text text)
        {
            if (text == null)
            {
                return 0f;
            }

            float width = text.preferredWidth;
            return width > 0f ? width : text.GetRenderedValues(false).x;
        }

        /// <summary>
        /// Write the mod's words over both clones, and keep writing them.
        ///
        /// Compared through <see cref="UITextMeshTextUtility"/> rather than through the text mesh's
        /// own property: <c>UITextMesh.Text</c> writes into a StringBuilder and TMP's <c>text</c>
        /// getter goes on answering with the string the prefab was serialized with, so a plain
        /// comparison reads the mod's clone as still saying "Options" and the label the mod would
        /// have re-written is exactly the one whoever asks reads back.
        /// </summary>
        private static void Relabel()
        {
            string title = Title();
            if (_mainMenuLabel != null && UITextMeshTextUtility.GetEffectiveText(_mainMenuLabel) != title)
            {
                _mainMenuLabel.Text = title;
            }

            if (_pauseMenuButton != null
                && UITextMeshTextUtility.GetEffectiveButtonText(_pauseMenuButton) != title)
            {
                _pauseMenuButton.Text = title;
            }
        }

        // ---- the pause menu ----

        private static void TickPauseMenu()
        {
            // Existence, not visibility: the pause menu builds the mod's accessible list the frame it
            // is shown, so an entry made only once the menu is up misses that first build. The clone
            // is a child of the menu's own container, so while the menu is closed it is not drawn and
            // whoever lists the items filters it out with the rest.
            UIButton options = PauseMenuOptionsButton();
            if (options == null)
            {
                _pauseMenuButton = null;
                return;
            }

            Transform parent = options.transform.parent;
            if (parent == null)
            {
                return;
            }

            if (_pauseMenuButton == null || _pauseMenuButton.transform.parent != parent)
            {
                Transform standing = parent.Find(PauseMenuButtonName);
                _pauseMenuButton = standing != null ? standing.GetComponent<UIButton>() : null;
            }

            if (_pauseMenuButton == null)
            {
                GameObject made = UnityEngine.Object.Instantiate(options.gameObject, parent);
                made.name = PauseMenuButtonName;
                StripLocalization(made);
                UIButton button = made.GetComponent<UIButton>();
                if (button == null)
                {
                    UnityEngine.Object.Destroy(made);
                    Warn("the cloned pause menu item has no UIButton");
                    return;
                }

                // Straight after Options: the container lays its children out in order, so the
                // sibling index is where the item is drawn.
                made.transform.SetSiblingIndex(options.transform.GetSiblingIndex() + 1);
                button.OnClicked = HandleClicked;
                button.Active = true;
                button.Interactable = true;
                _pauseMenuButton = button;
                SocAccessMod.Instance?.LogInfo("Mod options entry: added to the pause menu after Options");
            }

            Relabel();
        }

        // ---- shared ----

        private static void HandleClicked()
        {
            try
            {
                Func<bool> open = Open;
                if (open != null)
                {
                    open();
                }
            }
            catch (Exception exception)
            {
                // Runs inside the engine's own dispatch: never throw into it.
                Warn("opening from a drawn entry threw: " + exception);
            }
        }

        private static bool ClickButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        /// <summary>The game's own Options button on the main menu, or null while the menu is not
        /// loaded. Cached, and looked for again only every <see cref="RescanFrames"/> frames, because
        /// the scan behind it walks every loaded object of its type and this runs on the pump.
        /// </summary>
        private static UIButton MainMenuOptionsButton()
        {
            if (_mainMenuOptions != null)
            {
                return _mainMenuOptions;
            }

            if (!DueForRescan(ref _mainMenuRescanFrame))
            {
                return null;
            }

            return _mainMenuOptions = FindMainMenuOptionsButton();
        }

        private static UIButton FindMainMenuOptionsButton()
        {
            MainMenu[] menus = Resources.FindObjectsOfTypeAll<MainMenu>();
            for (int i = 0; i < menus.Length; i++)
            {
                MainMenu menu = menus[i];
                if (menu == null || menu.gameObject == null || !menu.gameObject.scene.isLoaded)
                {
                    continue;
                }

                MainMenuAdapter adapter = new MainMenuAdapter(menu);
                IMenuButtonAdapter options = adapter.OptionsButton;
                if (options != null && options.Button != null)
                {
                    return options.Button;
                }
            }

            return null;
        }

        /// <summary>The game's own Options item in the pause menu, read off the installer's
        /// serialized settings so no container has to be resolved. Cached and throttled like the
        /// main menu's.</summary>
        private static UIButton PauseMenuOptionsButton()
        {
            if (_pauseMenuOptions != null)
            {
                return _pauseMenuOptions;
            }

            if (!DueForRescan(ref _pauseMenuRescanFrame))
            {
                return null;
            }

            return _pauseMenuOptions = FindPauseMenuOptionsButton();
        }

        private static UIButton FindPauseMenuOptionsButton()
        {
            PauseMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<PauseMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                PauseMenuInstaller installer = installers[i];
                if (installer == null
                    || installer.gameObject == null
                    || !installer.gameObject.scene.isLoaded
                    || installer.Settings == null)
                {
                    continue;
                }

                UIButton options = installer.Settings.optionsButton;
                if (options != null)
                {
                    return options;
                }
            }

            return null;
        }

        private static void DestroyByName(UIButton sibling, params string[] names)
        {
            Transform parent = sibling != null ? sibling.transform.parent : null;
            if (parent == null)
            {
                return;
            }

            for (int i = 0; i < names.Length; i++)
            {
                Transform standing = parent.Find(names[i]);
                if (standing != null)
                {
                    UnityEngine.Object.DestroyImmediate(standing.gameObject);
                }
            }
        }

        /// <summary>Take the game's localization binding off a clone, so the mod's words stay on it.
        /// </summary>
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
            SocAccessMod.Instance?.LogWarning("Mod options entry: " + message);
        }

        /// <summary>
        /// The main menu's entry as a menu button, for whoever lists the header.
        ///
        /// The corner button carries no words of its own - the game draws "Options" as a separate
        /// right-aligned label beside its button, and the mod's pair copies that - so the label to
        /// read is the drawn text of the clone's own label, not the button's.
        /// </summary>
        private sealed class HeaderEntry : MenuButtonAdapterBase
        {
            private readonly UITextMesh _label;

            public HeaderEntry(UIButton button, UITextMesh label)
                : base(button, null, () => ClickButton(button))
            {
                _label = label;
            }

            protected override string BuildLabel()
            {
                return _label != null ? UITextMeshTextUtility.GetEffectiveText(_label) : string.Empty;
            }
        }

        private static bool DueForRescan(ref int lastFrame)
        {
            int frame = Time.frameCount;
            if (frame - lastFrame < RescanFrames)
            {
                return false;
            }

            lastFrame = frame;
            return true;
        }

        /// <summary>How many frames apart the two scans for a menu that is not there may run.
        /// </summary>
        private const int RescanFrames = 30;

        private static bool _stopped;
        private static UIButton _mainMenuButton;
        private static UITextMesh _mainMenuLabel;
        private static UIButton _pauseMenuButton;
        private static UIButton _mainMenuOptions;
        private static UIButton _pauseMenuOptions;
        private static int _mainMenuRescanFrame = int.MinValue / 2;
        private static int _pauseMenuRescanFrame = int.MinValue / 2;
    }
}
