using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Menu.Popup;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class QuitToDesktopPopupAdapter : IPresent
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
            get { return UITextMeshTextUtility.Spoken(Settings != null ? Settings.Title : null); }
        }

        public string Description
        {
            get { return string.Join(" ", DescriptionLines); }
        }

        /// <summary>The paragraphs the popup wrote its message in, kept apart rather than
        /// collapsed.</summary>
        public IList<string> DescriptionLines
        {
            get
            {
                return SpokenLines.Of(new[]
                {
                    UITextMeshTextUtility.GetEffectiveText(Settings != null ? Settings.Description : null),
                });
            }
        }

        public string FollowTitle
        {
            get { return UITextMeshTextUtility.Spoken(Settings != null ? Settings.FollowTitle : null); }
        }

        public bool HasConfirm
        {
            get { return MenuButtonAdapterBase.IsButtonVisible(Settings != null ? Settings.ConfirmButton : null); }
        }

        public bool HasCancel
        {
            get { return MenuButtonAdapterBase.IsButtonVisible(Settings != null ? Settings.CancelButton : null); }
        }

        public bool HasSteamFollow
        {
            get
            {
                QuitToDesktopPopup.Settings settings = Settings;
                return settings != null
                    && IsGameObjectActive(settings.SteamFollowContainer)
                    && MenuButtonAdapterBase.IsButtonVisible(settings.OpenSteamPageButton);
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

        /// <summary>The drawn Yes button itself, so a screen can key a control on it and read where
        /// the popup puts it. Null while the popup draws none.</summary>
        public Component ConfirmButton
        {
            get { return Settings != null ? Settings.ConfirmButton : null; }
        }

        /// <summary>The drawn No button itself.</summary>
        public Component CancelButton
        {
            get { return Settings != null ? Settings.CancelButton : null; }
        }

        /// <summary>The drawn FOLLOW button itself.</summary>
        public Component SteamFollowButton
        {
            get { return Settings != null ? Settings.OpenSteamPageButton : null; }
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
            return NativeSelectionUtility.Click(Settings != null ? Settings.ConfirmButton : null);
        }

        public bool ActivateCancel()
        {
            return NativeSelectionUtility.Click(Settings != null ? Settings.CancelButton : null);
        }

        public bool ActivateSteamFollow()
        {
            return NativeSelectionUtility.Click(Settings != null ? Settings.OpenSteamPageButton : null);
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
                    SocAccessMod.Instance?.LogWarning("Failed to read QuitToDesktopPopup settings: " + exception.Message);
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

        private static bool IsTransformActive(IUITransform transform)
        {
            return transform != null && transform.Active;
        }

        private static bool IsGameObjectActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static string GetButtonText(IUIButton button)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveButtonText(button));
        }
    }
}
