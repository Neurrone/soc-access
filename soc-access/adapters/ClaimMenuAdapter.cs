using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class ClaimMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(ClaimMenu), "_settings");

        private readonly ClaimMenu _menu;
        private readonly ClaimMenu.Settings _settings;

        public ClaimMenuAdapter(ClaimMenu menu)
        {
            _menu = menu;
            _settings = GetField<ClaimMenu.Settings>(menu, SettingsField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return GetText(_settings != null ? _settings.HeaderText : null); }
        }

        public string Body
        {
            get { return GetText(_settings != null ? _settings.DescriptionText : null); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && IsVisible(_settings.ContainerCanvasGroup as Component)
                && GetChoices().Count > 0;
        }

        public IReadOnlyList<ChoiceItem> GetChoices()
        {
            List<ChoiceItem> choices = new List<ChoiceItem>();
            if (_settings == null)
            {
                return choices;
            }

            AddChoice(choices, "occupy", _settings.OccupyContainer, _settings.OccupyToggle);
            AddChoice(choices, "raze", _settings.RazeContainer, _settings.RazeToggle);
            AddChoice(choices, "loot", _settings.LootContainer, _settings.LootToggle);
            AddChoice(choices, "convert", _settings.ConvertContainer, _settings.ConvertToggle);
            return choices;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        private static void AddChoice(List<ChoiceItem> choices, string idSuffix, UITransform container, Toggle toggle)
        {
            Component containerComponent = container as Component;
            GameObject root = containerComponent != null ? containerComponent.gameObject : null;
            if (!IsVisible(root))
            {
                return;
            }

            UITextMesh title = FindText(root, "TitleLayout/Title");
            UITextMesh duration = FindText(root, "TitleLayout/Duration");
            UITextMesh description = FindText(root, "DescriptionText");
            choices.Add(new ChoiceItem(
                idSuffix,
                () => JoinParts(GetText(title), GetText(duration), GetText(description)),
                () => toggle != null && toggle.interactable,
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

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string JoinParts(params string[] parts)
        {
            if (parts == null || parts.Length == 0)
            {
                return string.Empty;
            }

            List<string> cleaned = new List<string>();
            for (int i = 0; i < parts.Length; i++)
            {
                string part = SpeechTextSanitizer.Normalize(parts[i]);
                if (!string.IsNullOrWhiteSpace(part))
                {
                    cleaned.Add(part);
                }
            }

            return cleaned.Count == 0 ? string.Empty : string.Join(". ", cleaned.ToArray());
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }

        public sealed class ChoiceItem
        {
            private readonly Func<bool> _isEnabled;

            public ChoiceItem(string idSuffix, Func<string> getLabel, Func<bool> isEnabled, Func<bool> focus, Func<bool> activate)
            {
                IdSuffix = idSuffix ?? string.Empty;
                GetLabel = getLabel;
                _isEnabled = isEnabled;
                Focus = focus;
                Activate = activate;
            }

            public string IdSuffix { get; private set; }
            public Func<string> GetLabel { get; private set; }
            public Func<bool> Focus { get; private set; }
            public Func<bool> Activate { get; private set; }

            public bool IsEnabled
            {
                get { return _isEnabled == null || _isEnabled(); }
            }
        }
    }
}
