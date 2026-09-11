using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
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
                return UITextMeshTextUtility.Spoken(settings != null ? settings.HeaderText : null);
            }
        }

        public string Body
        {
            get { return string.Join(" ", BodyLines); }
        }

        /// <summary>The paragraphs the game broke the body into, kept apart rather than collapsed:
        /// the dialog reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return SpokenLines.Of(new[]
                {
                    UITextMeshTextUtility.GetEffectiveText(settings != null ? settings.BodyText : null),
                });
            }
        }

        public string PositiveLabel
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return UITextMeshTextUtility.Spoken(settings != null ? settings.PositiveButtonText : null);
            }
        }

        public string NegativeLabel
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return UITextMeshTextUtility.Spoken(settings != null ? settings.NegativeButtonText : null);
            }
        }

        public bool HasPositiveAction
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return MenuButtonAdapterBase.IsButtonVisible(settings != null ? settings.PositiveButton : null);
            }
        }

        public bool HasNegativeAction
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return MenuButtonAdapterBase.IsButtonVisible(settings != null ? settings.NegativeButton : null);
            }
        }

        public bool IsPositiveActionEnabled
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return MenuButtonAdapterBase.IsButtonEnabledAndVisible(settings != null ? settings.PositiveButton : null);
            }
        }

        public bool IsNegativeActionEnabled
        {
            get
            {
                CustomMessageMenu.Settings settings = GetSettings();
                return MenuButtonAdapterBase.IsButtonEnabledAndVisible(settings != null ? settings.NegativeButton : null);
            }
        }

        /// <summary>False: <c>CustomMessageMenu.Show</c> only calls
        /// <c>RegisterAsButtonPoller</c> and registers no keyboard input callback, so nothing in the
        /// game answers Escape while it is up.</summary>
        public bool GameHandlesEscape
        {
            get { return false; }
        }

        public Component ButtonOf(DialogAction action)
        {
            CustomMessageMenu.Settings settings = GetSettings();
            if (settings == null)
            {
                return null;
            }

            switch (action)
            {
                case DialogAction.Positive:
                    return settings.PositiveButton;
                case DialogAction.Negative:
                    return settings.NegativeButton;
                default:
                    return null;
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

        private static bool InvokeButton(UIButton button)
        {
            if (!MenuButtonAdapterBase.IsButtonEnabledAndVisible(button))
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }
    }
}
