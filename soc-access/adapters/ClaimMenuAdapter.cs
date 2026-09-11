using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ClaimMenuAdapter : IPresent
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(ClaimMenu), "_settings");

        private readonly ClaimMenu _menu;
        private readonly ClaimMenu.Settings _settings;

        public ClaimMenuAdapter(ClaimMenu menu)
        {
            _menu = menu;
            _settings = Reflect.Get<ClaimMenu.Settings>(menu, SettingsField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return UITextMeshTextUtility.Spoken(_settings != null ? _settings.HeaderText : null); }
        }

        public string Body
        {
            get { return string.Join(" ", BodyLines); }
        }

        /// <summary>The paragraphs the game broke the menu's description into, kept apart rather than
        /// collapsed: the menu reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get { return UITextMeshTextUtility.SpokenLines(_settings != null ? _settings.DescriptionText : null); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && GameObjects.IsLive(_settings.ContainerCanvasGroup as Component)
                && GetChoices().Count > 0;
        }

        public IReadOnlyList<ChoiceItem> GetChoices()
        {
            List<ChoiceItem> choices = new List<ChoiceItem>();
            if (_settings == null)
            {
                return choices;
            }

            AddChoice(choices, ClaimChoiceKind.Occupy, _settings.OccupyContainer, _settings.OccupyToggle);
            AddChoice(choices, ClaimChoiceKind.Raze, _settings.RazeContainer, _settings.RazeToggle);
            AddChoice(choices, ClaimChoiceKind.Loot, _settings.LootContainer, _settings.LootToggle);
            AddChoice(choices, ClaimChoiceKind.Convert, _settings.ConvertContainer, _settings.ConvertToggle);
            return choices;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        // The three texts each choice draws are separate meshes of its container: the title in
        // TitleLayout/Title, the duration beside it in TitleLayout/Duration, and the paragraph below
        // in DescriptionText. They are handed out as they are drawn; what the reading order makes of
        // them is the screen's business.
        private static void AddChoice(List<ChoiceItem> choices, ClaimChoiceKind kind, UITransform container, Toggle toggle)
        {
            Component containerComponent = container as Component;
            GameObject root = containerComponent != null ? containerComponent.gameObject : null;
            if (!GameObjects.IsLive(root) || toggle == null)
            {
                return;
            }

            UITextMesh title = FindText(root, "TitleLayout/Title");
            UITextMesh duration = FindText(root, "TitleLayout/Duration");
            UITextMesh description = FindText(root, "DescriptionText");
            choices.Add(new ChoiceItem(
                kind,
                toggle,
                () => UITextMeshTextUtility.Spoken(title),
                () => UITextMeshTextUtility.Spoken(duration),
                () => UITextMeshTextUtility.SpokenLines(description),
                () => toggle.interactable,
                () => FocusToggle(toggle),
                () => ActivateToggle(toggle)));
        }

        private static bool FocusToggle(Toggle toggle)
        {
            return toggle != null && NativeSelectionUtility.Select(toggle);
        }

        private static bool ActivateToggle(Toggle toggle)
        {
            if (toggle == null || !toggle.IsActive() || !toggle.IsInteractable())
            {
                return false;
            }

            return NativeSelectionUtility.PointerClick(toggle);
        }

        private static UITextMesh FindText(GameObject root, string relativePath)
        {
            Transform transform = root != null ? root.transform.Find(relativePath) : null;
            return transform != null ? transform.GetComponent<UITextMesh>() : null;
        }

        public sealed class ChoiceItem
        {
            private readonly Func<bool> _isEnabled;

            public ChoiceItem(
                ClaimChoiceKind kind,
                Component toggle,
                Func<string> getTitle,
                Func<string> getDuration,
                Func<IList<string>> getDescriptionLines,
                Func<bool> isEnabled,
                Func<bool> focus,
                Func<bool> activate)
            {
                Kind = kind;
                Toggle = toggle;
                GetTitle = getTitle;
                GetDuration = getDuration;
                GetDescriptionLines = getDescriptionLines;
                _isEnabled = isEnabled;
                Focus = focus;
                Activate = activate;
            }

            /// <summary>Which of the game's four choices this is.</summary>
            public ClaimChoiceKind Kind { get; private set; }

            /// <summary>The component the game draws this choice with - its toggle.</summary>
            public Component Toggle { get; private set; }

            public Func<string> GetTitle { get; private set; }
            public Func<string> GetDuration { get; private set; }
            /// <summary>The choice's paragraph, one line each as the game wrote it.</summary>
            public Func<IList<string>> GetDescriptionLines { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<bool> Activate { get; private set; }

            public bool IsEnabled
            {
                get { return _isEnabled == null || _isEnabled(); }
            }
        }
    }

    /// <summary>The four things the game offers to do with a settlement just taken, in the order its
    /// settings list them.</summary>
    public enum ClaimChoiceKind
    {
        Occupy,
        Raze,
        Loot,
        Convert
    }
}
