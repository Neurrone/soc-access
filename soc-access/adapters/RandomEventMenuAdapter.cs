using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class RandomEventMenuAdapter : IMessageDialogAdapter
    {
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
                return UITextMeshTextUtility.Spoken(settings != null ? settings.HeaderText : null);
            }
        }

        public string Body
        {
            get { return string.Join(" ", BodyLines); }
        }

        /// <summary>The event's chain name and its description, each its own line along with any
        /// paragraph the game broke them into: the menu reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get
            {
                RandomEventMenu.Settings settings = GetSettings();
                if (settings == null)
                {
                    return new List<string>();
                }

                return SpokenLines.Of(new[]
                {
                    GetActiveMultilineText(settings.ChainNameText),
                    GetActiveMultilineText(settings.DescriptionText),
                });
            }
        }

        public string PositiveLabel
        {
            get
            {
                RandomEventMenu.Settings settings = GetSettings();
                string label = GetButtonText(settings != null ? settings.ConfirmButton : null);
                return FirstNonEmpty(label, SpokenText.Get(GetLocalizationHandler(), "Common/Confirm", string.Empty));
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
                return MenuButtonAdapterBase.IsButtonVisible(settings != null ? settings.ConfirmButton : null);
            }
        }

        public bool HasNegativeAction
        {
            get { return false; }
        }

        public bool IsPositiveActionEnabled
        {
            get { return MenuButtonAdapterBase.IsButtonEnabledAndVisible(GetConfirmButton()); }
        }

        public bool IsNegativeActionEnabled
        {
            get { return false; }
        }

        /// <summary>True: <c>RandomEventMenu.Show</c> registers <c>InputActions.UI.ExitMenu</c> on
        /// <c>Hide</c> unconditionally, whatever the input mode.</summary>
        public bool GameHandlesEscape
        {
            get { return true; }
        }

        public Component ButtonOf(DialogAction action)
        {
            return action == DialogAction.Positive ? GetConfirmButton() : null;
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
            return action == DialogAction.Positive && NativeSelectionUtility.Click(GetConfirmButton());
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

        private static string GetActiveMultilineText(IUITextMesh textMesh)
        {
            IUITransform transform = textMesh as IUITransform;
            if (transform != null && !transform.Active)
            {
                return string.Empty;
            }

            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(IUIButton button)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }

        private static string FirstNonEmpty(string first, string fallback)
        {
            return !string.IsNullOrWhiteSpace(first) ? first : fallback ?? string.Empty;
        }
    }
}
