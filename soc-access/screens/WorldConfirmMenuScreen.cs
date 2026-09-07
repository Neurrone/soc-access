using System;
using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Adventure;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.UI;
using SongsOfConquestAccess.UI.Graph;
using UnityEngine;
using Zenject;

namespace SongsOfConquestAccess.Screens
{
    /// <summary>
    /// The map's confirmation menu - the box that asks whether to pay a cost - made navigable as a
    /// graph. One stop in the dialog order: the heading, which is also the screen name, the body as
    /// the start node, one read-only line per cost the game lists, the not-enough-resources warning
    /// while the game draws it, and the two buttons.
    ///
    /// Measured 2026-09-07: Cancel is drawn at x 511, LEFT of Confirm at x 641, while the menu's
    /// settings list the OK button first. So the buttons read in the order of their drawn left edges,
    /// measured every build rather than taken from the settings order.
    ///
    /// The warning reads after the costs because that is where it is drawn: <c>WorldConfirmMenu.Setup</c>
    /// turns the warning object on and the OK button off together, so the reason the confirm is
    /// refusing is a line of the dialog rather than a state of the button.
    ///
    /// ESCAPE is the mod's here, which is unusual and measured: <c>Setup</c> registers only
    /// <c>UI.Cancel</c>, the gamepad binding, and this prefab's <c>AdventureMenuBackground</c> has
    /// <c>_canClose</c> false, so the game draws no close cross and answers the keyboard's Escape with
    /// nothing. The screen claims Back and presses the drawn Cancel through the game's own click.
    /// </summary>
    public sealed class WorldConfirmMenuScreen : GraphScreen
    {
        private const string DialogStop = "world-confirm-menu";

        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(WorldConfirmMenuInstaller), "Container");

        private readonly WorldConfirmMenuAdapter _adapter;

        // A subject of its own for each node the menu gives no component for, so that two nodes
        // sharing one subject do not collapse onto whichever was declared first (the reconciler seats
        // the cursor by subject before it looks at the structural key).
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        public WorldConfirmMenuScreen(WorldConfirmMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            WorldConfirmMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<WorldConfirmMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                WorldConfirmMenu menu = TryResolveWorldConfirmMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                WorldConfirmMenuAdapter adapter = new WorldConfirmMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new WorldConfirmMenuScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "world-confirm-menu"; }
        }

        /// <summary>The heading the menu draws, spoken once on arrival and read again as the first
        /// node.</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.Title : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override bool ConsumesBack
        {
            get { return _adapter != null && _adapter.CancelButton != null; }
        }

        public override bool Back()
        {
            return _adapter != null && _adapter.ActivateCancel();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(DialogStop);

            if (!string.IsNullOrWhiteSpace(_adapter.Title))
            {
                builder.AddItem(new SyntheticNode(
                    ControlId.For(_headingKey, "world-confirm:heading"),
                    GraphNodes.Text(() => _adapter.Title)));
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Body))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "world-confirm:body");
                builder.AddItem(new SyntheticNode(
                    bodyId,
                    GraphNodes.Paragraphs(() => _adapter.BodyLines)));
                // Focus starts on the body, so arrival reads the heading once as the screen name and
                // then what the menu is actually asking.
                builder.SetStart(bodyId);
            }

            AddCosts(builder);
            AddResourceWarning(builder);
            AddButtons(builder);
        }

        /// <summary>The cost lines the menu lists, read out of the game's cost model rather than off a
        /// widget of their own: the entries are pooled, so each line is keyed by its position in the
        /// list the adapter enumerates.</summary>
        private void AddCosts(GraphBuilder builder)
        {
            IReadOnlyList<string> costs = _adapter.GetCostLabels();
            for (int i = 0; i < costs.Count; i++)
            {
                int index = i;
                builder.AddItem(new SyntheticNode(
                    ControlId.Structural("world-confirm:cost/" + index),
                    GraphNodes.Text(() => CostLabel(index))));
            }
        }

        private string CostLabel(int index)
        {
            IReadOnlyList<string> costs = _adapter.GetCostLabels();
            return index >= 0 && index < costs.Count ? costs[index] : string.Empty;
        }

        /// <summary>The warning the game draws when the player cannot afford the cost, declared only
        /// while it is drawn and only where it has wording to read.</summary>
        private void AddResourceWarning(GraphBuilder builder)
        {
            Component warning = _adapter.ResourceWarning;
            if (warning == null || string.IsNullOrWhiteSpace(_adapter.ResourceWarningLabel))
            {
                return;
            }

            builder.AddItem(new DrawnNode(
                ControlId.For(warning, "world-confirm:resource-warning"),
                GraphNodes.Text(() => _adapter.ResourceWarningLabel),
                warning));
        }

        /// <summary>The two buttons, leftmost first, read off their rectangles every build.</summary>
        private void AddButtons(GraphBuilder builder)
        {
            List<DrawnButton> buttons = new List<DrawnButton>(2);
            Component confirm = _adapter.ConfirmButton;
            if (confirm != null)
            {
                buttons.Add(new DrawnButton(
                    confirm,
                    "world-confirm:confirm",
                    Vtable(confirm, () => _adapter.ConfirmLabel, () => _adapter.ActivateConfirm(), _adapter.IsConfirmEnabled)));
            }

            Component cancel = _adapter.CancelButton;
            if (cancel != null)
            {
                buttons.Add(new DrawnButton(
                    cancel,
                    "world-confirm:cancel",
                    Vtable(cancel, () => _adapter.CancelLabel, () => _adapter.ActivateCancel(), null)));
            }

            if (buttons.Count == 2 && Left(buttons[1].Component) < Left(buttons[0].Component))
            {
                DrawnButton first = buttons[0];
                buttons[0] = buttons[1];
                buttons[1] = first;
            }

            for (int i = 0; i < buttons.Count; i++)
            {
                DrawnButton button = buttons[i];
                builder.AddItem(new DrawnNode(
                    ControlId.For(button.Component, button.Key),
                    button.Vtable,
                    button.Component));
            }
        }

        private static NodeVtable Vtable(Component component, Func<string> label, Action activate, Func<bool> enabled)
        {
            NodeVtable vtable = GraphNodes.Button(label, activate, enabled);
            // The button the cursor is on is the button the game shows as selected, which is also what
            // its own Confirm key would press.
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(component);
            return vtable;
        }

        private static float Left(Component component)
        {
            return component != null ? component.transform.position.x : 0f;
        }

        private struct DrawnButton
        {
            public readonly Component Component;
            public readonly string Key;
            public readonly NodeVtable Vtable;

            public DrawnButton(Component component, string key, NodeVtable vtable)
            {
                Component = component;
                Key = key;
                Vtable = vtable;
            }
        }

        private static WorldConfirmMenu TryResolveWorldConfirmMenu(WorldConfirmMenuInstaller installer)
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
                return container.Resolve<WorldConfirmMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(WorldConfirmMenuInstaller installer)
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
