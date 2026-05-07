using System.Collections.Generic;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal static class NativeTooltipUtility
    {
        private static readonly IReadOnlyList<string> EmptyLines = new string[0];

        // Reads native tooltip details from a Unity component. Components are
        // behaviours attached to GameObjects, such as UIButton, UIImage,
        // Selectable, troop HUD entries, or commander sheet skill entries.
        public static bool TryGetUiDetails(Component component, out IDetails details)
        {
            details = null;
            if (component == null)
            {
                return false;
            }

            ITooltipable tooltipable = ResolveTooltipable(component.gameObject);
            if (tooltipable == null)
            {
                return false;
            }

            try
            {
                details = tooltipable.GetDetails(Vector2.zero);
                return details != null;
            }
            catch (System.Exception exception)
            {
                SoqAccessPlugin.Instance?.LogWarning("NativeTooltipUtility failed to read UI tooltip details: " + exception.Message);
                return false;
            }
        }

        public static IReadOnlyList<string> GetTooltipLinesForComponent(Component component, ILocalizationHandler localization)
        {
            IDetails details;
            return TryGetUiDetails(component, out details)
                ? ToSpeechLines(details, localization)
                : EmptyLines;
        }

        public static IReadOnlyList<string> ToSpeechLines(IDetails details, ILocalizationHandler localization)
        {
            IReadOnlyList<string> detailsLines = DetailsTextUtility.ToLines(details, localization);
            return detailsLines.Count == 0 ? EmptyLines : detailsLines;
        }

        public static void ShowVisualTooltip(VisualTooltipMetadata metadata)
        {
            if (metadata == null)
            {
                HideTooltip();
                return;
            }

            if (metadata.IsMapTooltip)
            {
                ShowMapTooltipAtScreenPoint(metadata.MapTooltipable, metadata.ScreenPoint, metadata.MapDetails);
                return;
            }

            if (metadata.Anchor != null)
            {
                ShowTooltipForComponent(metadata.Component, metadata.Anchor, metadata.Anchors);
                return;
            }

            ShowTooltipForComponent(metadata.Component);
        }

        public static void ShowTooltipForComponent(Component component)
        {
            if (component == null)
            {
                HideTooltip();
                return;
            }

            NativeSelectionUtility.Select(component);
            TooltipPatches.ShowAccessibilityTooltip(component.gameObject);
        }

        public static void ShowTooltipForComponent(Component component, RectTransform anchor)
        {
            ShowTooltipForComponent(component, anchor, null);
        }

        public static void ShowTooltipForComponent(Component component, RectTransform anchor, TooltipAnchor[] anchors)
        {
            if (component == null)
            {
                HideTooltip();
                return;
            }

            NativeSelectionUtility.Select(component);
            TooltipPatches.ShowAccessibilityTooltip(component.gameObject, anchor, anchors);
        }

        public static void ShowMapTooltipAtScreenPoint(ITooltipable tooltipable, Vector2 screenPoint, IDetails details)
        {
            TooltipPatches.ShowAccessibilityTooltipAtScreenPoint(tooltipable, screenPoint, details);
        }

        public static void HideTooltip()
        {
            TooltipPatches.HideAccessibilityTooltip();
        }

        private static ITooltipable ResolveTooltipable(GameObject gameObject)
        {
            if (gameObject == null)
            {
                return null;
            }

            global::UITooltipProxy proxy = gameObject.GetComponent<global::UITooltipProxy>();
            if (proxy != null && proxy.tooltip != null)
            {
                return proxy.tooltip;
            }

            return gameObject.GetComponent<ITooltipable>();
        }
    }
}
