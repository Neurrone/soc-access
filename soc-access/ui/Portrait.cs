using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    public static class Portrait
    {
        public static Widget Static(
            string id,
            Func<string> getName,
            Action onFocus,
            Func<Tooltip> getTooltip,
            Func<bool> isVisible = null)
        {
            return new TextWidget(
                id,
                getName,
                onFocus,
                includeParentLabelInAnnouncement: false,
                getTooltip,
                isVisible);
        }

        public static Widget StaticNative(
            string id,
            Func<string> getName,
            Func<Component> getTarget,
            ILocalizationHandler localization,
            Action refreshTooltip = null,
            Func<bool> isVisible = null)
        {
            return Static(
                id,
                getName,
                () => FocusNative(getTarget, refreshTooltip),
                () => BuildNativeTooltip(getTarget, localization, refreshTooltip),
                isVisible);
        }

        public static Widget Button(
            string id,
            Func<string> getName,
            Func<bool> activate,
            Action onFocus,
            Func<Tooltip> getTooltip,
            Func<bool> isEnabled = null,
            Func<bool> isVisible = null)
        {
            return new ButtonWidget(
                id,
                getName,
                activate,
                onFocus,
                isEnabled,
                isVisible,
                getTooltip);
        }

        public static Tooltip BuildNativeTooltip(
            Func<Component> getTarget,
            ILocalizationHandler localization,
            Action refreshTooltip = null)
        {
            Component target = getTarget != null ? getTarget() : null;
            if (target == null || localization == null)
            {
                return null;
            }

            return new Tooltip(
                () =>
                {
                    refreshTooltip?.Invoke();
                    return NativeTooltipUtility.GetTooltipLinesForComponent(target, localization);
                },
                VisualTooltipMetadata.ForComponent(target));
        }

        public static void FocusNative(Func<Component> getTarget, Action refreshTooltip = null)
        {
            refreshTooltip?.Invoke();
            Component target = getTarget != null ? getTarget() : null;
            NativeSelectionUtility.Select(target);
        }
    }
}
