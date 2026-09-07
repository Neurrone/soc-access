using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The menu the map puts up when a wielder walks into something that offers a choice - a reward
    /// to pick, a penalty to take, a question to answer. Three places to be, in the order the menu
    /// draws them: the wielder band across the top, the choice itself, and the close cross.
    ///
    /// THE CARDS ARE A RADIO GROUP that never chooses on arrival: walking onto a card must not commit
    /// the player to it, and the game's own model is select-then-confirm - a click on a card selects
    /// it and turns the Confirm button on, and only Confirm closes the menu. So Enter is the card's
    /// own pointer click and arriving on it only draws it (the widget screen selected on focus, which
    /// this replaces). A card the game will not take is unavailable and says the game's own red
    /// reason, which the card draws as part of its text.
    ///
    /// The wielder band comes from the shared contributor (<c>ui/TroopHudRows.cs</c>), because the
    /// menu rebuilds its cards whenever the army changes - which is why it draws the army at all -
    /// and because every wielder band in the game reads the same way.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): the menu's <c>AdventureMenuBackground</c>
    /// registers its own exit and closes on it. The navigator claims the key only while something is
    /// being carried.
    /// </summary>
    public sealed class WorldChoiceMenuScreen : GraphScreen
    {
        private const string WielderStop = "world-choice-wielder";
        private const string ChoiceStop = "world-choice";
        private const string CloseStop = "world-choice-close";
        private const string WielderKey = "world-choice:wielder";

        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(WorldChoiceMenuInstaller), "Container");

        private readonly WorldChoiceMenuAdapter _adapter;

        // A subject of its own for the body, which the menu draws as a plain text rather than as a
        // control, kept across rebuilds so the reconciler seats the cursor on the same one.
        private readonly object _bodyMarker = new object();

        public WorldChoiceMenuScreen(WorldChoiceMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            WorldChoiceMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<WorldChoiceMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                WorldChoiceMenu menu = TryResolveWorldChoiceMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                WorldChoiceMenuAdapter adapter = new WorldChoiceMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new WorldChoiceMenuScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "world-choice-menu"; }
        }

        /// <summary>The title the menu draws.</summary>
        public override string ScreenName
        {
            get
            {
                return _adapter == null
                    ? null
                    : TroopHudRows.NameWithPlace(_adapter.Title, _adapter.Wielder);
            }
        }

        public override object InitialFocusStop
        {
            get { return ChoiceStop; }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            TroopHudRows.WielderStop(builder, WielderStop, WielderKey, _adapter.Wielder);

            builder.BeginStop(ChoiceStop);
            BuildBody(builder);
            BuildChoices(builder);
            BuildConfirm(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        /// <summary>The game's Ctrl+digit quick splits, on the band's troop rows.</summary>
        public override bool ClaimsAction(string actionKey)
        {
            return TroopHudRows.ClaimsAction(actionKey, Navigator, Troops, WielderKey);
        }

        public override bool OnAction(string actionKey)
        {
            return TroopHudRows.OnAction(actionKey, Navigator, Troops, WielderKey);
        }

        private TroopHudAdapter Troops
        {
            get { return _adapter == null || _adapter.Wielder == null ? null : _adapter.Wielder.Troops; }
        }

        /// <summary>What the menu says the choice is about, as one node of its paragraphs.</summary>
        private void BuildBody(GraphBuilder builder)
        {
            string body = _adapter.Body;
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(_bodyMarker, "world-choice:body"),
                GraphNodes.Paragraphs(() => SpokenLines.Of(new[] { body }))));
        }

        /// <summary>One card per row, walked with Up and Down: exactly one of them is the choice, and
        /// arriving is not choosing.</summary>
        private void BuildChoices(GraphBuilder builder)
        {
            IReadOnlyList<WorldChoiceMenuAdapter.ChoiceItem> choices = Items("choices", _adapter.GetChoices);
            for (int i = 0; i < choices.Count; i++)
            {
                WorldChoiceMenuAdapter.ChoiceItem it = choices[i];
                if (it == null || it.Button == null)
                {
                    continue;
                }

                NodeVtable vtable = GraphNodes.Radio(
                    () => it.Label,
                    () => it.IsSelected,
                    () => it.Choose(),
                    () => it.IsEnabled,
                    it.Tooltip);
                vtable.OnFocusVisual = () => it.Select();
                builder.AddItem(new DrawnNode(
                    ControlId.For(it.Button, "world-choice:card/" + i),
                    vtable,
                    it.Button));
            }
        }

        /// <summary>The button that commits the choice. The game turns it on when a card is chosen,
        /// so it is watched live under a cursor waiting here.</summary>
        private void BuildConfirm(GraphBuilder builder)
        {
            Component confirm = _adapter.ConfirmButton;
            if (confirm == null)
            {
                return;
            }

            NodeVtable vtable = GraphNodes.Button(
                () => _adapter.ConfirmLabel,
                () => _adapter.ActivateConfirm(),
                _adapter.IsConfirmEnabled);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(confirm);
            builder.AddItem(new DrawnNode(
                ControlId.For(confirm, "world-choice:confirm"),
                vtable,
                confirm));
        }

        /// <summary>The cross the wielder band draws, which is the game's own way out of the menu.
        /// </summary>
        private void BuildClose(GraphBuilder builder)
        {
            WielderInteract wielder = _adapter.Wielder;
            Component close = wielder == null ? null : wielder.CloseButton;
            if (close == null || !wielder.IsCloseVisible)
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => wielder.ActivateClose());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(ControlId.For(close, "world-choice:close"), vtable, close));
        }

        /// <summary>One section's items, or none where reading them threw: a part of the menu the game
        /// has stopped answering for costs its own rows and never the rest of the page.</summary>
        private static IReadOnlyList<T> Items<T>(string section, Func<IReadOnlyList<T>> getter)
        {
            try
            {
                IReadOnlyList<T> items = getter != null ? getter() : null;
                return items ?? new T[0];
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("WorldChoiceMenuScreen section " + section + " failed to build: " + exception);
                return new T[0];
            }
        }

        private static WorldChoiceMenu TryResolveWorldChoiceMenu(WorldChoiceMenuInstaller installer)
        {
            if (!IsLiveSceneInstaller(installer) || InstallerContainerProperty == null)
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
                return container.Resolve<WorldChoiceMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(WorldChoiceMenuInstaller installer)
        {
            if (installer == null)
            {
                return false;
            }

            GameObject gameObject = installer.gameObject;
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }
    }
}
