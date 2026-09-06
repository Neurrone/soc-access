using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Main;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The game's main menu, made navigable as a graph. Two stops: the column of menu buttons, in the
    /// order the game draws them, and the Options button in the corner.
    ///
    /// Two of the entries (Extras, Multiplayer) are foldouts the game opens on hover, so there is no
    /// keyboard route to what they hold. Here each is an expandable group whose children are the
    /// foldout's buttons: Right opens the game's own foldout and lands on its first entry, Left
    /// closes it again. The group's expanded state is read off the foldout itself every build, so a
    /// foldout the game hides on its own (Join With Code hides the multiplayer container directly)
    /// collapses the group with no hook needed.
    ///
    /// Entries the game hides (Hotseat, the map editor) are not declared; entries it disables stay
    /// in the list and say so.
    /// </summary>
    public sealed class MainMenuScreen : GraphScreen
    {
        private const string MenuStop = "main-menu";
        private const string OptionsStop = "options";

        private static readonly string[] TopLevelItemIds =
        {
            "continue",
            "campaign",
            "skirmish",
            "load-game",
            // Map editor is not accessible yet, so do not expose it in the main menu.
            // "map-editor",
            "community-maps",
            "extras",
            "hotseat",
            "multiplayer",
            "quit"
        };

        private readonly MainMenuAdapter _adapter;

        public MainMenuScreen(MainMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            MainMenuAdapter adapter = FindActiveMainMenu();
            return adapter != null ? new MainMenuScreen(adapter) : null;
        }

        public static MainMenuAdapter FindActiveMainMenu()
        {
            MainMenu[] mainMenus = Resources.FindObjectsOfTypeAll<MainMenu>();
            for (int i = 0; i < mainMenus.Length; i++)
            {
                MainMenu mainMenu = mainMenus[i];
                if (!IsLiveSceneMainMenu(mainMenu))
                {
                    continue;
                }

                MainMenuAdapter adapter = new MainMenuAdapter(mainMenu);
                if (adapter.IsPresent())
                {
                    return adapter;
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "main-menu"; }
        }

        public override string ScreenName
        {
            get { return ModText.Get(ModStrings.Screens.MainMenu); }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        /// <summary>The menu fades out after a click (Conquest fades the whole canvas group before it
        /// starts the lobby), and every button reads disabled on the way; that is the page leaving,
        /// not the button changing.</summary>
        public override bool IsWorkable
        {
            get { return _adapter != null && !_adapter.IsFading(); }
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(MenuStop);
            IReadOnlyList<IMenuButtonAdapter> items = _adapter.TopLevelItems;
            foreach (int index in DrawnOrder(items))
            {
                IMenuButtonAdapter item = items[index];
                string key = "mainmenu:" + GetTopLevelItemId(index);
                MainMenuAdapter.NativeFoldoutAdapter foldout = FoldoutFor(item);
                if (foldout == null)
                {
                    builder.AddItem(new DrawnNode(ControlId.For(item.Button, key), Button(item), item.Button));
                    continue;
                }

                // A group and nothing else: the game wires no click to a foldout header (it opens on
                // hover), so Enter does nothing here and Right is the way in. Opening and closing go
                // through the foldout itself, which is what keeps a foldout the game closed on its
                // own in step with the group.
                MainMenuAdapter.NativeFoldoutAdapter it = foldout;
                NodeVtable group = GraphNodes.Group(item.GetLabel, null, item.IsEnabled);
                group.OnExpand = () => it.Open();
                group.OnCollapse = () => it.Close();
                builder.BeginGroup(new DrawnNode(ControlId.For(item.Button, key), group, item.Button), expanded: foldout.IsOpen());
                IReadOnlyList<IMenuButtonAdapter> children = foldout.Items;
                foreach (int childIndex in DrawnOrder(children))
                {
                    IMenuButtonAdapter child = children[childIndex];
                    builder.AddItem(new DrawnNode(
                        ControlId.For(child.Button, key + "/" + childIndex),
                        Button(child),
                        child.Button));
                }

                builder.EndGroup();
            }

            IMenuButtonAdapter options = _adapter.OptionsButton;
            IMenuButtonAdapter modOptions = ModOptionsEntries.MainMenuEntry;
            bool hasOptions = options != null && options.IsVisible();
            bool hasModOptions = modOptions != null && modOptions.IsVisible();
            if (hasOptions || hasModOptions)
            {
                builder.BeginStop(OptionsStop);
                if (hasOptions)
                {
                    builder.AddItem(new DrawnNode(ControlId.For(options.Button, "mainmenu:options"), Button(options), options.Button));
                }

                // The mod's own entry, drawn beside the game's by ModOptionsEntries and read here
                // exactly as the game's is: after Options, because that is what it is next to.
                if (hasModOptions)
                {
                    builder.AddItem(new DrawnNode(
                        ControlId.For(modOptions.Button, "mainmenu:mod-options"),
                        Button(modOptions),
                        modOptions.Button));
                }
            }
        }

        private static NodeVtable Button(IMenuButtonAdapter item)
        {
            return GraphNodes.Button(item.GetLabel, () => item.Activate(), item.IsEnabled);
        }

        private MainMenuAdapter.NativeFoldoutAdapter FoldoutFor(IMenuButtonAdapter item)
        {
            if (_adapter.ExtrasFoldout != null && ReferenceEquals(item, _adapter.ExtrasFoldout.TriggerButton))
            {
                return _adapter.ExtrasFoldout;
            }

            if (_adapter.MultiplayerFoldout != null && ReferenceEquals(item, _adapter.MultiplayerFoldout.TriggerButton))
            {
                return _adapter.MultiplayerFoldout;
            }

            return null;
        }

        /// <summary>
        /// The indexes of the visible items, top to bottom as the game draws them. The adapter lists
        /// the buttons in the order the game's class declares them, which is not the order on screen
        /// (Conquest is drawn above Campaigns, Multiplayer above Load Game), and the reading order is
        /// the drawn one. Measured off each button's own rectangle every build, so a layout the game
        /// changes is followed; a stable index tiebreak keeps two buttons at one height in list order.
        /// </summary>
        private static List<int> DrawnOrder(IReadOnlyList<IMenuButtonAdapter> items)
        {
            List<int> order = new List<int>();
            List<float> tops = new List<float>();
            for (int i = 0; items != null && i < items.Count; i++)
            {
                IMenuButtonAdapter item = items[i];
                if (item == null || item.Button == null || !item.IsVisible())
                {
                    continue;
                }

                order.Add(i);
                tops.Add(Top(item));
            }

            // Insertion sort by drawn top, highest first; stable, so equal heights keep list order.
            for (int i = 1; i < order.Count; i++)
            {
                int index = order[i];
                float top = tops[i];
                int j = i - 1;
                while (j >= 0 && tops[j] < top)
                {
                    order[j + 1] = order[j];
                    tops[j + 1] = tops[j];
                    j--;
                }

                order[j + 1] = index;
                tops[j + 1] = top;
            }

            return order;
        }

        private static float Top(IMenuButtonAdapter item)
        {
            Component component = item.Button;
            return component != null ? component.transform.position.y : 0f;
        }

        private static string GetTopLevelItemId(int index)
        {
            return index >= 0 && index < TopLevelItemIds.Length
                ? TopLevelItemIds[index]
                : "main-menu-item-" + index;
        }

        private static bool IsLiveSceneMainMenu(MainMenu mainMenu)
        {
            if (mainMenu == null)
            {
                return false;
            }

            GameObject gameObject = mainMenu.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
