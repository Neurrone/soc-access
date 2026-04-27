using HarmonyLib;
using SongsOfConquest.Client.Menu.Tooltip;
using UnityEngine;

namespace SongsOfConquestAccess
{
    // The game only shows selected-control tooltips from UITooltipManager.Tick while
    // the current input mode is Gamepad. Accessibility focus uses keyboard input, so
    // selecting the native UI object is not enough to show its tooltip visually.
    //
    // This patch captures the live UITooltipManager instance, then lets accessibility
    // adapters force-display the selected object's native ITooltipable through the
    // same lower-level ITooltipManager used by the game.
    [HarmonyPatch(typeof(UITooltipManager), "Tick")]
    internal static class TooltipPatches
    {
        private static readonly AccessTools.FieldRef<UITooltipManager, ITooltipManager> TooltipManagerRef =
            AccessTools.FieldRefAccess<UITooltipManager, ITooltipManager>("_tooltipManager");

        private static UITooltipManager _currentManager;
        private static ITooltipManager.Handle _accessibilityTooltipHandle;
        private static StaticTooltipLocation _temporaryStaticTooltipLocation;
        private static StaticTooltipLocation _overriddenStaticTooltipLocation;
        private static bool _overriddenStaticTooltipUse;
        private static RectTransform _overriddenStaticTooltipGamepadLocation;
        private static TooltipAnchor[] _overriddenStaticTooltipDesiredAnchors;

        [HarmonyPrefix]
        private static void TickPrefix(UITooltipManager __instance)
        {
            if (__instance != null)
            {
                _currentManager = __instance;
            }
        }

        public static void ShowAccessibilityTooltip(GameObject gameObject)
        {
            ShowAccessibilityTooltip(gameObject, null);
        }

        public static void ShowAccessibilityTooltip(GameObject gameObject, RectTransform tooltipAnchor)
        {
            HideAccessibilityTooltip();

            if (gameObject == null || !gameObject.activeInHierarchy || _currentManager == null)
            {
                return;
            }

            ITooltipable tooltipable = ResolveTooltipable(gameObject);
            ITooltipManager tooltipManager = TooltipManagerRef(_currentManager);
            RectTransform rectTransform = tooltipAnchor != null
                ? tooltipAnchor
                : gameObject.GetComponent<RectTransform>();
            if (tooltipable == null || tooltipManager == null || rectTransform == null)
            {
                return;
            }

            TooltipAnchor[] anchors = null;
            if (tooltipAnchor != null)
            {
                ApplyStaticTooltipLocationToSelectedObject(gameObject, use: true, tooltipAnchor, desiredAnchors: null);
            }
            else
            {
                StaticTooltipLocation staticTooltipLocation;
                if (gameObject.TryGetComponent<StaticTooltipLocation>(out staticTooltipLocation)
                    && staticTooltipLocation.Use
                    && staticTooltipLocation.GamepadLocation != null)
                {
                    rectTransform = staticTooltipLocation.GamepadLocation;
                    anchors = staticTooltipLocation.DesiredAnchors;
                }
            }

            _accessibilityTooltipHandle = tooltipManager.ForceDisplayTooltip(
                tooltipable,
                new TooltipLocation(rectTransform, anchors));
        }

        public static void HideAccessibilityTooltip()
        {
            ReleaseStaticTooltipLocationOverride();

            if (_currentManager == null)
            {
                return;
            }

            ITooltipManager tooltipManager = TooltipManagerRef(_currentManager);
            if (tooltipManager != null)
            {
                tooltipManager.HideTooltip(_accessibilityTooltipHandle);
            }

            _accessibilityTooltipHandle = new ITooltipManager.Handle(0);
        }

        public static void Reset()
        {
            HideAccessibilityTooltip();
            _currentManager = null;
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

        private static void ApplyStaticTooltipLocationToSelectedObject(
            GameObject gameObject,
            bool use,
            RectTransform gamepadLocation,
            TooltipAnchor[] desiredAnchors)
        {
            if (gameObject == null)
            {
                return;
            }

            StaticTooltipLocation location;
            if (gameObject.TryGetComponent<StaticTooltipLocation>(out location))
            {
                OverrideSelectedStaticTooltipLocation(location, use, gamepadLocation, desiredAnchors);
                return;
            }

            // UITooltipManager.Tick only checks the currently selected GameObject for
            // StaticTooltipLocation. If a caller supplies an explicit anchor, mirror it
            // there while this tooltip is active so the game's native tick agrees with us.
            _temporaryStaticTooltipLocation = gameObject.AddComponent<StaticTooltipLocation>();
            _temporaryStaticTooltipLocation.Use = use;
            _temporaryStaticTooltipLocation.GamepadLocation = gamepadLocation;
            _temporaryStaticTooltipLocation.DesiredAnchors = desiredAnchors;
        }

        private static void OverrideSelectedStaticTooltipLocation(
            StaticTooltipLocation location,
            bool use,
            RectTransform gamepadLocation,
            TooltipAnchor[] desiredAnchors)
        {
            if (location == null || _overriddenStaticTooltipLocation != null)
            {
                return;
            }

            _overriddenStaticTooltipLocation = location;
            _overriddenStaticTooltipUse = location.Use;
            _overriddenStaticTooltipGamepadLocation = location.GamepadLocation;
            _overriddenStaticTooltipDesiredAnchors = location.DesiredAnchors;
            location.Use = use;
            location.GamepadLocation = gamepadLocation;
            location.DesiredAnchors = desiredAnchors;
        }

        private static void ReleaseStaticTooltipLocationOverride()
        {
            if (_overriddenStaticTooltipLocation != null)
            {
                _overriddenStaticTooltipLocation.Use = _overriddenStaticTooltipUse;
                _overriddenStaticTooltipLocation.GamepadLocation = _overriddenStaticTooltipGamepadLocation;
                _overriddenStaticTooltipLocation.DesiredAnchors = _overriddenStaticTooltipDesiredAnchors;
                _overriddenStaticTooltipLocation = null;
                _overriddenStaticTooltipGamepadLocation = null;
                _overriddenStaticTooltipDesiredAnchors = null;
            }

            if (_temporaryStaticTooltipLocation != null)
            {
                Object.Destroy(_temporaryStaticTooltipLocation);
                _temporaryStaticTooltipLocation = null;
            }
        }
    }
}
