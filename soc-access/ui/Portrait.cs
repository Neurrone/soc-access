using System;
using SongsOfConquestAccess.Adapters;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    public static class Portrait
    {
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
                VisualTooltipMetadata.ForComponent(target),
                isLong: () => NativeTooltipUtility.IsLongForComponent(target, refreshTooltip));
        }

        public static void FocusNative(Func<Component> getTarget, Action refreshTooltip = null)
        {
            refreshTooltip?.Invoke();
            Component target = getTarget != null ? getTarget() : null;
            NativeSelectionUtility.Select(target);
        }
    }
}
