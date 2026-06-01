using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class TeleportMenuAdapter
    {
        private static readonly FieldInfo CurrentTeleportIndexField = AccessTools.Field(typeof(TeleportMenu), "_currentTeleportIndex");
        private static readonly FieldInfo CurrentTeleportPositionsField = AccessTools.Field(typeof(TeleportMenu), "_currentTeleportPositions");
        private static readonly FieldInfo ContainerField = AccessTools.Field(typeof(TeleportMenu), "_container");
        private static readonly FieldInfo PreviousButtonField = AccessTools.Field(typeof(TeleportMenu), "_previousButton");
        private static readonly FieldInfo NextButtonField = AccessTools.Field(typeof(TeleportMenu), "_nextButton");
        private static readonly FieldInfo ConfirmButtonField = AccessTools.Field(typeof(TeleportMenu), "_confirmButton");
        private static readonly FieldInfo CancelButtonField = AccessTools.Field(typeof(TeleportMenu), "_cancelButton");
        private static readonly FieldInfo HeaderTextField = AccessTools.Field(typeof(TeleportMenu), "_headerText");
        private static readonly FieldInfo GamepadInputTextField = AccessTools.Field(typeof(TeleportMenu), "_gamepadInputText");
        private static readonly FieldInfo GamepadPreviousTextField = AccessTools.Field(typeof(TeleportMenu), "_gamepadPreviousText");
        private static readonly FieldInfo GamepadNextTextField = AccessTools.Field(typeof(TeleportMenu), "_gamepadNextText");
        private static readonly FieldInfo LocalizationHandlerField = AccessTools.Field(typeof(TeleportMenu), "_localizationHandler");

        private readonly TeleportMenu _menu;

        public TeleportMenuAdapter(TeleportMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public int DestinationCount
        {
            get
            {
                Vector2Int[] positions = Positions;
                return positions != null ? positions.Length : 0;
            }
        }

        public Vector2Int CurrentDestination
        {
            get
            {
                Vector2Int[] positions = Positions;
                int index = CurrentIndex;
                return positions != null && index >= 0 && index < positions.Length
                    ? positions[index]
                    : Vector2Int.zero;
            }
        }

        public string InstructionText
        {
            get { return FindInstructionText(); }
        }

        public string PreviousLabel
        {
            get { return FirstNonEmpty(GetButtonText(GetPreviousButton()), GameText.Get(LocalizationHandler, "Common/Previous", "Previous")); }
        }

        public string NextLabel
        {
            get { return FirstNonEmpty(GetButtonText(GetNextButton()), GameText.Get(LocalizationHandler, "Common/Next", "Next")); }
        }

        public string ConfirmLabel
        {
            get { return FirstNonEmpty(GetButtonText(GetConfirmButton()), GameText.Get(LocalizationHandler, "Common/Confirm", "Confirm")); }
        }

        public string CancelLabel
        {
            get { return FirstNonEmpty(GetButtonText(GetCancelButton()), GameText.Get(LocalizationHandler, "Common/Cancel", "Cancel")); }
        }

        private int CurrentIndex
        {
            get
            {
                if (_menu == null || CurrentTeleportIndexField == null)
                {
                    return 0;
                }

                object value = CurrentTeleportIndexField.GetValue(_menu);
                return value is int ? (int)value : 0;
            }
        }

        private Vector2Int[] Positions
        {
            get
            {
                return _menu != null && CurrentTeleportPositionsField != null
                    ? CurrentTeleportPositionsField.GetValue(_menu) as Vector2Int[]
                    : null;
            }
        }

        private ILocalizationHandler LocalizationHandler
        {
            get
            {
                return _menu != null && LocalizationHandlerField != null
                    ? LocalizationHandlerField.GetValue(_menu) as ILocalizationHandler
                    : null;
            }
        }

        public bool IsPresent()
        {
            GameObject container = GetContainer();
            return _menu != null
                && container != null
                && container.activeInHierarchy
                && DestinationCount > 1;
        }

        public bool SelectPrevious()
        {
            return NativeSelectionUtility.Click(GetPreviousButton());
        }

        public bool SelectNext()
        {
            return NativeSelectionUtility.Click(GetNextButton());
        }

        public bool Confirm()
        {
            return NativeSelectionUtility.Click(GetConfirmButton());
        }

        public bool Cancel()
        {
            return NativeSelectionUtility.Click(GetCancelButton());
        }

        private string FindInstructionText()
        {
            GameObject container = GetContainer();
            if (container == null)
            {
                return string.Empty;
            }

            UITextMesh header = GetTextMesh(HeaderTextField);
            UITextMesh gamepadInput = GetTextMesh(GamepadInputTextField);
            UITextMesh gamepadPrevious = GetTextMesh(GamepadPreviousTextField);
            UITextMesh gamepadNext = GetTextMesh(GamepadNextTextField);
            HashSet<UITextMesh> excludedTexts = new HashSet<UITextMesh>();
            AddIfNotNull(excludedTexts, header);
            AddIfNotNull(excludedTexts, gamepadInput);
            AddIfNotNull(excludedTexts, gamepadPrevious);
            AddIfNotNull(excludedTexts, gamepadNext);
            AddButtonTexts(excludedTexts, GetPreviousButton());
            AddButtonTexts(excludedTexts, GetNextButton());
            AddButtonTexts(excludedTexts, GetConfirmButton());
            AddButtonTexts(excludedTexts, GetCancelButton());

            string title = GetText(header);
            HashSet<string> excludedLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            AddIfNotEmpty(excludedLabels, title);
            AddIfNotEmpty(excludedLabels, PreviousLabel);
            AddIfNotEmpty(excludedLabels, NextLabel);
            AddIfNotEmpty(excludedLabels, ConfirmLabel);
            AddIfNotEmpty(excludedLabels, CancelLabel);

            UITextMesh[] texts = container.GetComponentsInChildren<UITextMesh>(true);
            for (int i = 0; texts != null && i < texts.Length; i++)
            {
                UITextMesh text = texts[i];
                if (text == null || excludedTexts.Contains(text) || !((Component)text).gameObject.activeInHierarchy)
                {
                    continue;
                }

                string value = GetText(text).Trim();
                if (string.IsNullOrWhiteSpace(value) || excludedLabels.Contains(value))
                {
                    continue;
                }

                return value;
            }

            SocAccessPlugin.Instance?.LogWarning("TeleportMenuAdapter could not discover teleport instruction text from native UI");
            return string.Empty;
        }

        private GameObject GetContainer()
        {
            return _menu != null && ContainerField != null
                ? ContainerField.GetValue(_menu) as GameObject
                : null;
        }

        private UIButton GetPreviousButton()
        {
            return GetField<UIButton>(PreviousButtonField);
        }

        private UIButton GetNextButton()
        {
            return GetField<UIButton>(NextButtonField);
        }

        private UIButton GetConfirmButton()
        {
            return GetField<UIButton>(ConfirmButtonField);
        }

        private UIButton GetCancelButton()
        {
            return GetField<UIButton>(CancelButtonField);
        }

        private UITextMesh GetTextMesh(FieldInfo field)
        {
            return GetField<UITextMesh>(field);
        }

        private T GetField<T>(FieldInfo field) where T : class
        {
            return _menu != null && field != null ? field.GetValue(_menu) as T : null;
        }

        private static string GetButtonText(UIButton button)
        {
            return UITextMeshTextUtility.GetEffectiveButtonText(button);
        }

        private static string GetText(UITextMesh text)
        {
            return UITextMeshTextUtility.GetEffectiveText(text);
        }

        private static void AddButtonTexts(HashSet<UITextMesh> texts, UIButton button)
        {
            if (texts == null || button == null || button.TextMesh == null)
            {
                return;
            }

            UITextMesh textMesh = button.TextMesh as UITextMesh;
            if (textMesh != null)
            {
                texts.Add(textMesh);
            }
        }

        private static void AddIfNotNull(HashSet<UITextMesh> texts, UITextMesh text)
        {
            if (texts != null && text != null)
            {
                texts.Add(text);
            }
        }

        private static void AddIfNotEmpty(HashSet<string> labels, string value)
        {
            if (labels != null && !string.IsNullOrWhiteSpace(value))
            {
                labels.Add(value.Trim());
            }
        }

        private static string FirstNonEmpty(string preferred, string fallback)
        {
            return string.IsNullOrWhiteSpace(preferred) ? fallback ?? string.Empty : preferred;
        }
    }
}
