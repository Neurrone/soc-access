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
    /// The commander level-up menu, made navigable as a graph. Four places to be, in the order the
    /// menu draws them: the heading, the commander's stats, the skill cards, and the close cross.
    ///
    /// Measured 2026-09-07: header, level, name and title at the top, then a stats row of bare
    /// numbers, then "Choose a Skill", then three cards side by side with the headers New Skill /
    /// New Skill / Upgrade Command. The cards are declared in the order of their drawn left edges,
    /// measured every build, because the settings list them left, middle, right regardless of layout.
    ///
    /// FOCUS LANDS ON THE FIRST CARD: the page exists to choose a skill, and choosing one closes the
    /// menu (there is no confirm step - <c>CommanderLevelUpMenu.ConfirmSkill</c> completes the
    /// request and closes).
    ///
    /// A card only shows what it is worth while the mouse is over it:
    /// <c>CommanderLevelUpSkillComponent</c> fades its icon, its frame and its description in on
    /// <c>OnPointerEnter</c> and out again on <c>OnPointerExit</c>, and native selection does nothing
    /// for it. So a card's focus visual is <see cref="PointerHover"/> rather than a selection, and
    /// the pointer is released when the screen loses focus or goes away.
    ///
    /// A card the menu filled in with "no more skills" is turned non-interactable by
    /// <c>SetupInactive</c>; it stays declared, reads its message, and says it is unavailable.
    ///
    /// Escape is the game's (<c>ConsumesBack</c> false): this prefab's
    /// <c>AdventureMenuBackground</c> has <c>_canClose</c> true, so it draws the close cross and
    /// registers <c>UI.ExitMenu</c> on its own close in <c>AnimateEntry</c>.
    /// </summary>
    public sealed class LevelUpScreen : GraphScreen
    {
        private const string HeaderStop = "level-up-header";
        private const string StatsStop = "level-up-stats";
        private const string SkillsStop = "level-up-skills";
        private const string CloseStop = "level-up-close";

        private static readonly System.Reflection.PropertyInfo InstallerContainerProperty =
            AccessTools.Property(typeof(CommanderLevelUpMenuInstaller), "Container");

        private readonly LevelUpMenuAdapter _adapter;

        // A subject of its own per synthesized line, kept across rebuilds so the reconciler seats the
        // cursor on the same one: the menu gives no component for its heading, its identity line, the
        // "Choose a Skill" caption or the max-level notice.
        private readonly Dictionary<string, object> _markers = new Dictionary<string, object>();

        public LevelUpScreen(LevelUpMenuAdapter adapter)
        {
            _adapter = adapter;
        }

        public static Screen TryBuildActiveScreen()
        {
            CommanderLevelUpMenuInstaller[] installers = Resources.FindObjectsOfTypeAll<CommanderLevelUpMenuInstaller>();
            for (int i = 0; i < installers.Length; i++)
            {
                CommanderLevelUpMenu menu = TryResolveLevelUpMenu(installers[i]);
                if (menu == null)
                {
                    continue;
                }

                LevelUpMenuAdapter adapter = new LevelUpMenuAdapter(menu);
                if (adapter.IsPresent())
                {
                    return new LevelUpScreen(adapter);
                }
            }

            return null;
        }

        public override string Key
        {
            get { return "level-up"; }
        }

        /// <summary>The heading the menu draws ("New Level", with the level it reached); read again
        /// as the first node of the header stop.</summary>
        public override string ScreenName
        {
            get
            {
                string title = _adapter != null ? _adapter.GetTitle() : null;
                return string.IsNullOrWhiteSpace(title) ? null : title;
            }
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public override void OnUnfocus()
        {
            PointerHover.Release();
            base.OnUnfocus();
        }

        public override void OnPop()
        {
            PointerHover.Release();
            base.OnPop();
        }

        public override void Build(GraphBuilder builder)
        {
            if (!IsPresent())
            {
                return;
            }

            builder.BeginStop(HeaderStop);
            BuildHeader(builder);

            builder.BeginStop(StatsStop);
            BuildStats(builder);

            builder.BeginStop(SkillsStop);
            BuildSkills(builder);

            builder.BeginStop(CloseStop);
            BuildClose(builder);
        }

        // ---- the heading ----

        private void BuildHeader(GraphBuilder builder)
        {
            AddLine(builder, "title", () => _adapter.GetTitle());

            // The wielder's line is the portrait the game draws at the top centre: its details tooltip
            // fills the buffer, and selecting it is what makes the game draw that tooltip, as the
            // mouse resting on it would.
            Component portrait = _adapter.Portrait;
            if (portrait == null)
            {
                AddLine(builder, "commander", () => _adapter.GetCommanderIdentity());
                return;
            }

            NodeVtable vtable = GraphNodes.Text(() => _adapter.GetCommanderIdentity(), null, _adapter.PortraitTooltip);
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(portrait);
            builder.AddItem(new DrawnNode(ControlId.For(portrait, "level-up:commander"), vtable, portrait));
        }

        // ---- the stats row ----

        private void BuildStats(GraphBuilder builder)
        {
            IReadOnlyList<LevelUpMenuAdapter.StatItem> stats = _adapter.GetStats();
            for (int i = 0; i < stats.Count; i++)
            {
                LevelUpMenuAdapter.StatItem stat = stats[i];
                if (stat == null || stat.Target == null)
                {
                    continue;
                }

                string label = stat.Label;
                string value = stat.Value;
                NodeVtable vtable = GraphNodes.Text(() => label, null, stat.Tooltip);
                vtable.Announcements.Add(GraphNodes.ValuePart(() => value, watch: false));
                Component target = stat.Target;
                // The row is drawn as an icon with a number beside it; selecting the icon is what the
                // mouse resting on it would do, and is what the game draws its tooltip for.
                vtable.OnFocusVisual = () => NativeSelectionUtility.Select(target);
                builder.AddItem(new DrawnNode(ControlId.For(target, "level-up:" + stat.Id), vtable, target));
            }
        }

        // ---- the skill cards ----

        private void BuildSkills(GraphBuilder builder)
        {
            // "Choose a Skill", the line the game draws over the cards, names the stop: it is said
            // once on entering the cards, not as a line of its own (owner ruling 2026-09-07).
            string caption = _adapter.GetChooseSkillText();
            if (!string.IsNullOrWhiteSpace(caption))
            {
                builder.PushContext(caption);
            }

            List<LevelUpMenuAdapter.SkillChoice> choices = DrawnOrder(_adapter.GetSkillChoices());
            for (int i = 0; i < choices.Count; i++)
            {
                LevelUpMenuAdapter.SkillChoice choice = choices[i];
                Component button = choice.Button;
                string header = choice.Header;
                string nameAndLevel = choice.NameAndLevel;
                string description = choice.Description;
                LevelUpMenuAdapter.SkillChoice it = choice;
                NodeVtable vtable = GraphNodes.Button(
                    () => header,
                    () => { if (it.Activate != null) it.Activate(); },
                    it.IsEnabled);
                vtable.Announcements.Add(GraphNodes.ValuePart(() => nameAndLevel, watch: false));
                // The description is DRAWN on the card, so it reads as part of the card rather than
                // waiting in the buffer as a tooltip would.
                vtable.Announcements.Add(GraphNodes.ValuePart(() => description, watch: false));
                // The card reveals itself on pointer enter and hides again on pointer exit; nothing
                // else makes the game show which card the keyboard is on.
                vtable.OnFocusVisual = () => PointerHover.MoveTo(button);
                ControlId id = ControlId.For(button, "level-up:" + choice.Id);
                builder.AddItem(button == null
                    ? (NodeDeclaration)new SyntheticNode(id, vtable)
                    : new DrawnNode(id, vtable, button));
                if (i == 0)
                {
                    // What the page is for.
                    builder.SetStart(id);
                }
            }

            if (_adapter.IsMaxLevelMessageVisible())
            {
                AddLine(builder, "max-level", () => _adapter.GetMaxLevelMessage());
            }

            if (!string.IsNullOrWhiteSpace(caption))
            {
                builder.PopContext();
            }
        }

        /// <summary>The cards left to right as the menu draws them, measured off each card's own
        /// transform every build; the insertion sort is stable, so two cards at one x keep the
        /// settings' order.</summary>
        private static List<LevelUpMenuAdapter.SkillChoice> DrawnOrder(
            IReadOnlyList<LevelUpMenuAdapter.SkillChoice> choices)
        {
            List<LevelUpMenuAdapter.SkillChoice> drawn = new List<LevelUpMenuAdapter.SkillChoice>();
            List<float> lefts = new List<float>();
            for (int i = 0; choices != null && i < choices.Count; i++)
            {
                LevelUpMenuAdapter.SkillChoice choice = choices[i];
                if (choice == null || (choice.IsVisible != null && !choice.IsVisible()))
                {
                    continue;
                }

                drawn.Add(choice);
                lefts.Add(choice.Button != null ? choice.Button.transform.position.x : 0f);
            }

            for (int i = 1; i < drawn.Count; i++)
            {
                LevelUpMenuAdapter.SkillChoice moving = drawn[i];
                float left = lefts[i];
                int j = i - 1;
                while (j >= 0 && lefts[j] > left)
                {
                    drawn[j + 1] = drawn[j];
                    lefts[j + 1] = lefts[j];
                    j--;
                }

                drawn[j + 1] = moving;
                lefts[j + 1] = left;
            }

            return drawn;
        }

        // ---- the close cross ----

        private void BuildClose(GraphBuilder builder)
        {
            Component close = _adapter.CloseButton;
            if (close == null || !_adapter.IsCloseVisible())
            {
                return;
            }

            // An icon with no text of its own, so the mod names it.
            NodeVtable vtable = GraphNodes.Button(
                () => ModText.Get(ModStrings.Screens.Close),
                () => _adapter.ActivateClose());
            vtable.OnFocusVisual = () => NativeSelectionUtility.Select(close);
            builder.AddItem(new DrawnNode(ControlId.For(close, "level-up:close"), vtable, close));
        }

        private void AddLine(GraphBuilder builder, string key, Func<string> text)
        {
            if (string.IsNullOrWhiteSpace(text()))
            {
                return;
            }

            builder.AddItem(new SyntheticNode(
                ControlId.For(Marker(key), "level-up:" + key),
                GraphNodes.Text(text)));
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

        private static CommanderLevelUpMenu TryResolveLevelUpMenu(CommanderLevelUpMenuInstaller installer)
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
                return container.Resolve<CommanderLevelUpMenu>();
            }
            catch (Exception)
            {
                return null;
            }
        }

        private static bool IsLiveSceneInstaller(CommanderLevelUpMenuInstaller installer)
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
