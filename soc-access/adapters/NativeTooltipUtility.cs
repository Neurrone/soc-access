using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Localization;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public static class NativeTooltipUtility
    {
        private static readonly IReadOnlyList<string> EmptyLines = new string[0];
        private static readonly FieldInfo OverriddenDetailsField =
            AccessTools.Field(typeof(UITransform), "_overriddenDetails");

        // The game's LONG details: the wielder and troop dossiers, whose text is a stat block the
        // player walks in the review buffer rather than a sentence about the control. Every other
        // details class is short. Named by type because that is what the game itself distinguishes;
        // fully qualified because four of these names repeat across namespaces.
        private static readonly HashSet<Type> LongDetailTypes = new HashSet<Type>
        {
            typeof(SongsOfConquest.Client.Gamestate.Facade.AdventureTroopDetails),
            typeof(SongsOfConquest.Common.Battle.Facade.BattleTroopDetails),
            typeof(SongsOfConquest.Common.Details.BattlegroundsTroopDetails),
            typeof(SongsOfConquest.Common.Details.TroopDetails),
            typeof(SongsOfConquest.Client.Battle.InspectBattleTroopDetails),
            typeof(SongsOfConquest.Client.Adventure.PurchaseTroopsEntryDetails),
            typeof(SongsOfConquest.Common.Details.UpgradeFullStackDetails),
            typeof(SongsOfConquest.Client.Gamestate.Facade.AdventureCommanderDetails),
            typeof(SongsOfConquest.Client.Gamestate.Facade.BattleCommanderDetails),
            typeof(SongsOfConquest.Common.Details.CommanderDetails),
            typeof(SongsOfConquest.Client.Lobby.CommanderLobbyDetails),
            typeof(SongsOfConquest.Client.Adventure.UI.DeadCommanderHUDDetails),
            typeof(SongsOfConquest.Common.Entities.Adventure.ReviveCommanderDetails),
        };

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
                SocAccessMod.Instance?.LogWarning("NativeTooltipUtility failed to read UI tooltip details: " + exception.Message);
                return false;
            }
        }

        // Whether the details object behind this component's tooltip is one of the game's long ones.
        // A native fact about the game's own tooltip, not a judgement about how it should read: what
        // the mod DOES with the answer is decided in ui/GraphNodes.ModeFor.
        //
        // REMEMBERED PER WIDGET, because the question is put when a NODE IS DECLARED - a section's
        // mode is fixed there (GraphNodes.TooltipSection) - and screens compose a fresh Tooltip on
        // every build, so Tooltip's own per-instance memo never saw a second call and
        // tooltipable.GetDetails ran for every tooltip-bearing node on every frame. AGENTS.md: a
        // game-side refresh runs when the text is read, never when the build asks whether a tooltip
        // exists. The details CLASS a widget answers with does not change under it - a pooled row
        // reused for another entity answers with the same class - so one answer per widget stands.
        // Only a DEFINITE answer is kept: a widget with no details yet is asked again, which is what
        // a row the game fills in later needs. The keys are weak, so an answer dies with its widget
        // and there is nothing to tear down.
        private static readonly ConditionalWeakTable<Component, object> LongByComponent =
            new ConditionalWeakTable<Component, object>();

        private static readonly object Long = true;

        private static readonly object Short = false;

        public static bool IsLongForComponent(Component component)
        {
            if (component == null)
            {
                return false;
            }

            object known;
            if (LongByComponent.TryGetValue(component, out known))
            {
                return (bool)known;
            }

            IDetails details;
            if (!TryGetUiDetails(component, out details))
            {
                return false;
            }

            bool answer = IsLong(details);
            LongByComponent.Add(component, answer ? Long : Short);
            return answer;
        }

        // The same question on a widget whose real details the game only composes when the pointer
        // arrives (a wielder portrait, a wielder list row): without the composition the widget still
        // answers with the plain text sitting on it, which would classify a dossier as short and
        // read it out on the one focus that mattered. The composition is provoked at most once per
        // widget, because SetDetails keeps what it composed, so this is not a per-frame refresh -
        // but only for a composition that yields a DOSSIER. SetDetails files plain text on the
        // widget's TooltipDescription and leaves _overriddenDetails null, so a plain-text refresh
        // would be provoked on every build and must not be passed here; it could not change the
        // answer anyway.
        public static bool IsLongForComponent(Component component, Action compose)
        {
            if (compose != null && !HasComposedDetails(component))
            {
                compose();

                // The widget's details have just been replaced; a remembered answer was about the
                // plain text that stood there before.
                if (component != null)
                {
                    LongByComponent.Remove(component);
                }
            }

            return IsLongForComponent(component);
        }

        // Whether the game has already put composed details on the widget, as opposed to the plain
        // tooltip text a prefab hangs there. A widget the mod cannot read this off answers true, so
        // an unknown one is never provoked every frame.
        public static bool HasComposedDetails(Component component)
        {
            UITransform transform = component == null
                ? null
                : ResolveTooltipable(component.gameObject) as UITransform;
            return transform == null
                || OverriddenDetailsField == null
                || OverriddenDetailsField.GetValue(transform) != null;
        }

        // The same question asked of a details object an adapter already holds - the map and battle
        // tile tooltips read theirs from the tooltipable directly and never go through a component.
        public static bool IsLong(IDetails details)
        {
            return details != null && LongDetailTypes.Contains(details.GetType());
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
            // Options menu controls store already-localized TooltipDescription
            // text on their label UITextMesh. Those details arrive here as
            // PlainTextDetails, but the options adapter has no localization
            // handler, so draw-time extraction would otherwise return no lines.
            if (localization == null && details is PlainTextDetails plainTextDetails)
            {
                List<string> lines = new List<string>();
                AddIfNotEmpty(lines, plainTextDetails.Title);
                AddIfNotEmpty(lines, plainTextDetails.Text);
                return lines.Count == 0 ? EmptyLines : lines;
            }

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

        [HookWritable]
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

        private static void AddIfNotEmpty(List<string> lines, string value)
        {
            if (!string.IsNullOrWhiteSpace(value))
            {
                lines.Add(value);
            }
        }
    }
}
