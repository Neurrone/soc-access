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
    /// The menu that asks what to do with a settlement just taken, made navigable as a graph. One
    /// stop in the dialog order: the heading, which is also the screen name, the body as the start
    /// node, then one node per choice the menu is offering.
    ///
    /// The choices are BUTTONS rather than a radio group (owner ruling 2026-09-07): picking one is
    /// doing it. <c>ClaimMenu</c> hangs the command off the toggle's value-changed event and hides
    /// the menu in the same breath, so there is no chosen state for a player to arrive on.
    ///
    /// Each choice draws three texts of its own - the title, the duration beside it, and the
    /// paragraph below - and they read in that order as announcement parts of the one node, not as a
    /// details section: a part is a review-buffer line already, and a section repeating it would put
    /// each line in the buffer twice. Measured 2026-09-07: Occupy, Raze and Loot are stacked top to
    /// bottom, which is also the order the settings list them in; the order is taken off each
    /// toggle's own transform every build so a layout the game changes is followed.
    ///
    /// ESCAPE is the game's: <c>ClaimMenu.Open</c> registers <c>UI.ExitMenu</c> on
    /// <c>HandleExitPressed</c>, which hides the menu only when the entity is already owned and
    /// answers with the negative beep otherwise. There is NO close node here: the game draws no close
    /// control, the choices are the exits, and the one hide path Escape reaches is the game's own,
    /// which is entitled to refuse.
    /// </summary>
    public sealed class ClaimMenuScreen : GraphScreen
    {
        private const string DialogStop = "claim-menu";

        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(ClaimMenuInstaller), "Container");

        private readonly ClaimMenuAdapter _adapter;

        // A subject of its own for each node the menu gives no component for; two nodes sharing one
        // subject would collapse onto whichever was declared first.
        private readonly object _headingKey = new object();
        private readonly object _bodyKey = new object();

        public ClaimMenuScreen(ClaimMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            ClaimMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<ClaimMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                ClaimMenu menu = TryResolveClaimMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                ClaimMenuAdapter adapter = new ClaimMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new ClaimMenuScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "claim-menu"; }
        }

        /// <summary>The heading the menu draws over the choices ("Siege").</summary>
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

        public bool Matches(ClaimMenu menu)
        {
            return _adapter != null && ReferenceEquals(_adapter.SourceKey, menu);
        }

        public override void OnUnfocus()
        {
            base.OnUnfocus();
            _adapter?.HideNativeTooltip();
        }

        public override void OnPop()
        {
            base.OnPop();
            _adapter?.HideNativeTooltip();
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
                    ControlId.For(_headingKey, "claim-menu:heading"),
                    GraphNodes.Text(() => _adapter.Title)));
            }

            if (!string.IsNullOrWhiteSpace(_adapter.Body))
            {
                ControlId bodyId = ControlId.For(_bodyKey, "claim-menu:body");
                builder.AddItem(new SyntheticNode(
                    bodyId,
                    GraphNodes.Paragraphs(() => _adapter.BodyLines)));
                // Focus starts on the body, so arrival reads the heading once as the screen name and
                // then what the menu is asking before the choices.
                builder.SetStart(bodyId);
            }

            List<ClaimMenuAdapter.ChoiceItem> choices = DrawnOrder(_adapter.GetChoices());
            for (int i = 0; i < choices.Count; i++)
            {
                ClaimMenuAdapter.ChoiceItem choice = choices[i];
                builder.AddItem(new DrawnNode(
                    ControlId.For(choice.Toggle, "claim-menu:" + choice.IdSuffix),
                    Choice(choice),
                    choice.Toggle));
            }
        }

        /// <summary>One choice: what it is called, then the duration drawn beside it and the
        /// paragraphs of the text drawn below it, then the game's own click.</summary>
        private static NodeVtable Choice(ClaimMenuAdapter.ChoiceItem choice)
        {
            NodeVtable vtable = GraphNodes.Button(
                choice.GetTitle,
                () => choice.Activate(),
                () => choice.IsEnabled);
            vtable.Announcements.Add(GraphNodes.ValuePart(choice.GetDuration, watch: false));
            GraphNodes.ParagraphParts(vtable, choice.GetDescriptionLines);
            // The choice the cursor is on is the one the game shows as selected; the menu pushes a
            // selection layer with the first toggle as its default, and this keeps that in step.
            vtable.OnFocusVisual = () => choice.Focus();
            return vtable;
        }

        /// <summary>The choices top to bottom as the game draws them, measured off each toggle's own
        /// transform every build; the insertion sort is stable, so two choices at one height keep the
        /// adapter's order.</summary>
        private static List<ClaimMenuAdapter.ChoiceItem> DrawnOrder(IReadOnlyList<ClaimMenuAdapter.ChoiceItem> choices)
        {
            List<ClaimMenuAdapter.ChoiceItem> drawn = new List<ClaimMenuAdapter.ChoiceItem>();
            List<float> tops = new List<float>();
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                ClaimMenuAdapter.ChoiceItem choice = choices[i];
                if (choice == null || choice.Toggle == null)
                {
                    continue;
                }

                drawn.Add(choice);
                tops.Add(choice.Toggle.transform.position.y);
            }

            for (int i = 1; i < drawn.Count; i++)
            {
                ClaimMenuAdapter.ChoiceItem moving = drawn[i];
                float top = tops[i];
                int j = i - 1;
                while (j >= 0 && tops[j] < top)
                {
                    drawn[j + 1] = drawn[j];
                    tops[j + 1] = tops[j];
                    j--;
                }

                drawn[j + 1] = moving;
                tops[j + 1] = top;
            }

            return drawn;
        }

        private static ClaimMenu TryResolveClaimMenu(ClaimMenuInstaller installer)
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
                return container.Resolve<IClaimMenu>() as ClaimMenu;
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(ClaimMenuInstaller installer)
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
