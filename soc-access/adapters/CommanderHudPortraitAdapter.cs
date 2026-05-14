using System;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CommanderHudPortraitAdapter
    {
        private static readonly FieldInfo WielderPortraitButtonField =
            AccessTools.Field(typeof(CommanderHUDPortrait), "_wielderPortraitButton");
        private static readonly MethodInfo RefreshTooltipMethod =
            AccessTools.Method(typeof(CommanderHUDPortrait), "RefreshTooltip");

        private readonly string _id;
        private readonly Func<string> _getName;
        private readonly Func<bool> _isVisible;
        private readonly Func<bool> _canActivate;
        private readonly CommanderHUDPortrait _portrait;
        private readonly UIButton _button;
        private readonly ILocalizationHandler _localization;

        public CommanderHudPortraitAdapter(
            string id,
            Func<string> getName,
            CommanderHUDPortrait portrait,
            ILocalizationHandler localization,
            Func<bool> isVisible = null,
            Func<bool> canActivate = null)
            : this(id, getName, portrait, GetButton(portrait), localization, isVisible, canActivate)
        {
        }

        public CommanderHudPortraitAdapter(
            string id,
            Func<string> getName,
            CommanderHUDPortrait portrait,
            UIButton button,
            ILocalizationHandler localization,
            Func<bool> isVisible = null,
            Func<bool> canActivate = null)
        {
            _id = id ?? string.Empty;
            _getName = getName;
            _portrait = portrait;
            _button = button;
            _localization = localization;
            _isVisible = isVisible;
            _canActivate = canActivate;
        }

        public string Id
        {
            get { return _id; }
        }

        public string Name
        {
            get { return _getName != null ? _getName() ?? string.Empty : string.Empty; }
        }

        public bool IsVisible
        {
            get { return (_isVisible == null || _isVisible()) && IsButtonVisible(_button); }
        }

        public bool IsEnabled
        {
            get { return _button != null && _button.Active && _button.Interactable; }
        }

        public Component TooltipTarget
        {
            get { return _button as Component; }
        }

        public ILocalizationHandler Localization
        {
            get { return _localization; }
        }

        public void Focus()
        {
            RefreshTooltip();
            NativeSelectionUtility.Select(_button as Component);
        }

        public bool Click()
        {
            return (_canActivate == null || _canActivate()) && NativeSelectionUtility.Click(_button);
        }

        public static UIButton GetButton(CommanderHUDPortrait portrait)
        {
            return portrait != null && WielderPortraitButtonField != null
                ? WielderPortraitButtonField.GetValue(portrait) as UIButton
                : null;
        }

        public static void RefreshTooltip(CommanderHUDPortrait portrait)
        {
            if (portrait == null || portrait.Commander == null || RefreshTooltipMethod == null)
            {
                return;
            }

            try
            {
                RefreshTooltipMethod.Invoke(portrait, null);
            }
            catch (Exception exception)
            {
                SocAccessPlugin.Instance?.LogWarning("CommanderHudPortraitAdapter failed to refresh commander tooltip: " + exception.Message);
            }
        }

        public void RefreshTooltip()
        {
            RefreshTooltip(_portrait);
        }

        private static bool IsButtonVisible(UIButton button)
        {
            if (button == null || !button.Active)
            {
                return false;
            }

            GameObject gameObject = ((Component)button).gameObject;
            return gameObject != null && gameObject.activeInHierarchy;
        }
    }
}
