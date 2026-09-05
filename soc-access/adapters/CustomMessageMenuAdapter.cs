using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CustomMessageMenuAdapter : IMessageDialogAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(CustomMessageMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(CustomMessageMenu), "_async");

        private readonly CustomMessageMenu _menu;

        public CustomMessageMenuAdapter(CustomMessageMenu menu)
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
                CustomMessageMenu.Settings settings = GetSettings();
                return GetText(settings != null ? settings.HeaderText : null);
            }
        }

        public string Body
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return GetText(settings != null ? settings.BodyText : null);
            }
        }

        public string PositiveLabel
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return GetText(settings != null ? settings.PositiveButtonText : null);
            }
        }

        public string NegativeLabel
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return GetText(settings != null ? settings.NegativeButtonText : null);
            }
        }

        public bool HasPositiveAction
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return IsButtonActive(settings != null ? settings.PositiveButton : null);
            }
        }

        public bool HasNegativeAction
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return IsButtonActive(settings != null ? settings.NegativeButton : null);
            }
        }

        public bool IsPositiveActionEnabled
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return IsButtonEnabled(settings != null ? settings.PositiveButton : null);
            }
        }

        public bool IsNegativeActionEnabled
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return IsButtonEnabled(settings != null ? settings.NegativeButton : null);
            }
        }

        public bool IsPresent()
        {
            CustomMessageMenu.Settings settings = GetSettings();
            return _menu != null
                && settings != null
                && settings.Parent != null
                && settings.Parent.activeInHierarchy
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && (HasPositiveAction || HasNegativeAction);
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

            CustomMessageMenu.Settings settings = GetSettings();
            UIButton button = action == DialogAction.Positive
                ? settings != null ? settings.PositiveButton : null
                : settings != null ? settings.NegativeButton : null;
            Selectable selectable = button != null ? button.GetSelectable() : null;
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        public bool ActivateAction(DialogAction action)
        {
            CustomMessageMenu.Settings settings = GetSettings();
            if (settings == null)
            {
                return false;
            }

            switch (action)
            {
                case DialogAction.Positive:
                    return InvokeButton(settings.PositiveButton);
                case DialogAction.Negative:
                    return InvokeButton(settings.NegativeButton);
                default:
                    return false;
            }
        }

        private CustomMessageMenu.Settings GetSettings()
        {
            return _menu != null && SettingsField != null
                ? SettingsField.GetValue(_menu) as CustomMessageMenu.Settings
                : null;
        }

        private static bool IsButtonActive(UIButton button)
        {
            return button != null && button.Active && MenuButtonAdapterBase.IsButtonVisible(button);
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return IsButtonActive(button) && button.Interactable;
        }

        private static bool InvokeButton(UIButton button)
        {
            if (!IsButtonEnabled(button))
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }
    }
}
