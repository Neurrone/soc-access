using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class QuitToDesktopPopupAdapter
    {
        private static readonly AccessTools.FieldRef<QuitToDesktopPopup, QuitToDesktopPopup.Settings> SettingsRef =
            AccessTools.FieldRefAccess<QuitToDesktopPopup, QuitToDesktopPopup.Settings>("_settings");

        private readonly QuitToDesktopPopup _popup;

        public QuitToDesktopPopupAdapter(QuitToDesktopPopup popup)
        {
            _popup = popup;
        }

        public string Title
        {
            get { return GetText(Settings != null ? Settings.Title : null); }
        }

        public string Description
        {
            get { return GetText(Settings != null ? Settings.Description : null); }
        }

        public string FollowTitle
        {
            get { return GetText(Settings != null ? Settings.FollowTitle : null); }
        }

        public bool HasConfirm
        {
            get { return IsButtonActive(Settings != null ? Settings.ConfirmButton : null); }
        }

        public bool HasCancel
        {
            get { return IsButtonActive(Settings != null ? Settings.CancelButton : null); }
        }

        public bool HasSteamFollow
        {
            get
            {
                QuitToDesktopPopup.Settings settings = Settings;
                return settings != null
                    && IsGameObjectActive(settings.SteamFollowContainer)
                    && IsButtonActive(settings.OpenSteamPageButton);
            }
        }

        public string ConfirmLabel
        {
            get { return GetButtonText(Settings != null ? Settings.ConfirmButton : null); }
        }

        public string CancelLabel
        {
            get { return GetButtonText(Settings != null ? Settings.CancelButton : null); }
        }

        public string SteamFollowLabel
        {
            get { return GetButtonText(Settings != null ? Settings.OpenSteamPageButton : null); }
        }

        public bool IsPresent()
        {
            QuitToDesktopPopup.Settings settings = Settings;
            if (settings == null)
            {
                return false;
            }

            return IsTransformActive(settings.ContainerTransform)
                && IsTransformActive(settings.UIBlockerTransform)
                && (HasConfirm || HasCancel || HasSteamFollow);
        }

        public void SelectBody()
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        public void SelectConfirm()
        {
            SelectButton(Settings != null ? Settings.ConfirmButton : null);
        }

        public void SelectCancel()
        {
            SelectButton(Settings != null ? Settings.CancelButton : null);
        }

        public void SelectSteamFollow()
        {
            SelectButton(Settings != null ? Settings.OpenSteamPageButton : null);
        }

        public bool ActivateConfirm()
        {
            return InvokeButton(Settings != null ? Settings.ConfirmButton : null);
        }

        public bool ActivateCancel()
        {
            return InvokeButton(Settings != null ? Settings.CancelButton : null);
        }

        public bool ActivateSteamFollow()
        {
            return InvokeButton(Settings != null ? Settings.OpenSteamPageButton : null);
        }

        private QuitToDesktopPopup.Settings Settings
        {
            get
            {
                if (_popup == null)
                {
                    return null;
                }

                try
                {
                    return SettingsRef(_popup);
                }
                catch (System.Exception exception)
                {
                    SoqAccessPlugin.Instance?.LogWarning("Failed to read QuitToDesktopPopup settings: " + exception.Message);
                    return null;
                }
            }
        }

        private static void SelectButton(UIButton button)
        {
            Selectable selectable = button != null ? button.GetSelectable() : null;
            if (selectable != null)
            {
                NativeSelectionUtility.Select(selectable);
            }
        }

        private static bool InvokeButton(UIButton button)
        {
            if (button == null || !button.Active || !button.Interactable)
            {
                return false;
            }

            return NativeSelectionUtility.Click(button);
        }

        private static bool IsButtonActive(UIButton button)
        {
            return button != null && button.Active && MenuButtonAdapterBase.IsButtonVisible(button);
        }

        private static bool IsTransformActive(IUITransform transform)
        {
            return transform != null && transform.Active;
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(IUIButton button)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }
    }
}
