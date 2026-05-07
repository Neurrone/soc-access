using System.Collections.Generic;
using System;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class Tooltip
    {
        private static readonly IReadOnlyList<string> EmptyLines = new string[0];
        private static readonly IReadOnlyList<TooltipAction> EmptyActions = new TooltipAction[0];
        private readonly Func<IReadOnlyList<string>> _getTextLines;

        public Tooltip(
            Func<IReadOnlyList<string>> getTextLines,
            VisualTooltipMetadata visualMetadata,
            IReadOnlyList<TooltipAction> actions = null)
        {
            _getTextLines = getTextLines;
            VisualMetadata = visualMetadata;
            Actions = actions ?? EmptyActions;
        }

        // Final tooltip lines after optional adapter enrichment. These are raw
        // localized semantic lines and may still contain native rich-text tags
        // or layout spacing. Focus speech sanitizes them when composing output.
        public IReadOnlyList<string> TextLines
        {
            get { return _getTextLines != null ? _getTextLines() ?? EmptyLines : EmptyLines; }
        }

        public VisualTooltipMetadata VisualMetadata { get; private set; }

        // Structured actions the accessibility layer can invoke for this
        // tooltip. These are added by adapters that understand the native source
        // object; generic tooltip extraction does not infer actions from text.
        public IReadOnlyList<TooltipAction> Actions { get; private set; }

        public static Tooltip ForComponent(Component component, ILocalizationHandler localization)
        {
            if (component == null)
            {
                return null;
            }

            return new Tooltip(
                () => NativeTooltipUtility.GetTooltipLinesForComponent(component, localization),
                VisualTooltipMetadata.ForComponent(component));
        }

        public static Tooltip ForComponent(Component component, RectTransform anchor, ILocalizationHandler localization)
        {
            return ForComponent(component, anchor, null, localization);
        }

        public static Tooltip ForComponent(
            Component component,
            RectTransform anchor,
            TooltipAnchor[] anchors,
            ILocalizationHandler localization)
        {
            if (component == null)
            {
                return null;
            }

            return new Tooltip(
                () => NativeTooltipUtility.GetTooltipLinesForComponent(component, localization),
                VisualTooltipMetadata.ForComponent(component, anchor, anchors));
        }

    }

    internal sealed class TooltipAction
    {
        public TooltipAction(string label, Func<bool> invoke)
        {
            Label = label ?? string.Empty;
            Invoke = invoke;
        }

        public string Label { get; private set; }

        public Func<bool> Invoke { get; private set; }
    }

    internal sealed class VisualTooltipMetadata
    {
        private VisualTooltipMetadata()
        {
        }

        public VisualTooltipMetadata(ITooltipable mapTooltipable, Vector2 screenPoint, IDetails mapDetails)
        {
            IsMapTooltip = true;
            MapTooltipable = mapTooltipable;
            ScreenPoint = screenPoint;
            MapDetails = mapDetails;
        }

        public Component Component { get; private set; }

        public RectTransform Anchor { get; private set; }

        public TooltipAnchor[] Anchors { get; private set; }

        public ITooltipable MapTooltipable { get; private set; }

        public Vector2 ScreenPoint { get; private set; }

        public IDetails MapDetails { get; private set; }

        public bool IsMapTooltip { get; private set; }

        public static VisualTooltipMetadata ForComponent(Component component)
        {
            if (component == null)
            {
                return null;
            }

            return new VisualTooltipMetadata
            {
                Component = component
            };
        }

        public static VisualTooltipMetadata ForComponent(Component component, RectTransform anchor)
        {
            return ForComponent(component, anchor, null);
        }

        public static VisualTooltipMetadata ForComponent(Component component, RectTransform anchor, TooltipAnchor[] anchors)
        {
            if (component == null)
            {
                return null;
            }

            return new VisualTooltipMetadata
            {
                Component = component,
                Anchor = anchor,
                Anchors = anchors
            };
        }

    }
}
