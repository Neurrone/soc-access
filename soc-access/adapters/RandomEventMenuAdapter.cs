using System.Text.RegularExpressions;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class RandomEventMenuAdapter : IMessageDialogAdapter
    {
        private static readonly Regex RichTextTagRegex = new Regex("<.*?>", RegexOptions.Compiled);

        private static readonly AccessTools.FieldRef<RandomEventMenu, RandomEventMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<RandomEventMenu, RandomEventMenu.Settings>("_settings");
        private static readonly AccessTools.FieldRef<RandomEventMenu, ILocalizationHandler> LocalizationHandlerRef =
            AccessTools.FieldRefAccess<RandomEventMenu, ILocalizationHandler>("_localization");

        private readonly RandomEventMenu _menu;

        public RandomEventMenuAdapter(RandomEventMenu menu)
        {
            _menu = menu;
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get
            {
                RandomEventMenu.Settings settings = GetSettings();
                return GetText(settings != null ? settings.HeaderText : null);
            }
        }

        public string Body
        {
            get
            {
                RandomEventMenu.Settings settings = GetSettings();
                if (settings == null)
                {
                    return string.Empty;
                }

                return JoinNonEmpty(
                    GetActiveMultilineText(settings.ChainNameText),
                    GetActiveMultilineText(settings.DescriptionText));
            }
        }

        public string PositiveLabel
        {
            get
            {
                RandomEventMenu.Settings settings = GetSettings();
                string label = GetButtonText(settings != null ? settings.ConfirmButton : null);
                return FirstNonEmpty(label, GetLocalizedText("Common/Confirm"));
            }
        }

        public string NegativeLabel
        {
            get { return string.Empty; }
        }

        public bool HasPositiveAction
        {
            get
            {
                RandomEventMenu.Settings settings = GetSettings();
                return IsButtonActive(settings != null ? settings.ConfirmButton : null);
            }
        }

        public bool HasNegativeAction
        {
            get { return false; }
        }

        public bool IsPresent()
        {
            RandomEventMenu.Settings settings = GetSettings();
            if (settings == null || settings.TopGameObject == null)
            {
                return false;
            }

            return settings.TopGameObject.activeInHierarchy
                && settings.ContainerCanvasGroup != null
                && ((Component)settings.ContainerCanvasGroup).gameObject.activeInHierarchy
                && HasPositiveAction;
        }

        public void SyncNativeSelection(DialogAction action)
        {
            if (action == DialogAction.Body)
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }

                return;
            }

            if (action != DialogAction.Positive)
            {
                return;
            }

            UIButton button = GetConfirmButton();
            Selectable selectable = button != null ? button.GetSelectable() : null;
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        public bool ActivateAction(DialogAction action)
        {
            return action == DialogAction.Positive && InvokeButton(GetConfirmButton());
        }

        private RandomEventMenu.Settings GetSettings()
        {
            return _menu != null ? SettingsRef(_menu) : null;
        }

        private UIButton GetConfirmButton()
        {
            RandomEventMenu.Settings settings = GetSettings();
            return settings != null ? settings.ConfirmButton : null;
        }

        private ILocalizationHandler GetLocalizationHandler()
        {
            return _menu != null ? LocalizationHandlerRef(_menu) : null;
        }

        private static bool IsButtonActive(UIButton button)
        {
            return button != null && button.Active && MenuButtonAdapterBase.IsButtonVisible(button);
        }

        private static bool InvokeButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetActiveMultilineText(IUITextMesh textMesh)
        {
            IUITransform transform = textMesh as IUITransform;
            if (transform != null && !transform.Active)
            {
                return string.Empty;
            }

            return StripRichTextPreservingLines(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(IUIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private string GetLocalizedText(string key)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(GetLocalizationHandler(), key, string.Empty));
        }

        private static string StripRichTextPreservingLines(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            string withoutTags = RichTextTagRegex.Replace(value, string.Empty);
            return withoutTags.Trim();
        }

        private static string JoinNonEmpty(string first, string second)
        {
            if (string.IsNullOrWhiteSpace(first))
            {
                return second ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(second))
            {
                return first;
            }

            return first + "\n\n" + second;
        }

        private static string FirstNonEmpty(string first, string fallback)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : fallback ?? string.Empty;
        }
    }
}
