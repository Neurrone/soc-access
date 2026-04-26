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

        [HarmonyPrefix]
        private static void TickPrefix(UITooltipManager __instance)
        {
            if (__instance == null)
            {
                return;
            }

            _currentManager = __instance;
        }

        public static void ShowAccessibilityTooltip(GameObject gameObject)
        {
            HideAccessibilityTooltip();

            if (gameObject == null || !gameObject.activeInHierarchy || _currentManager == null)
            {
                return;
            }

            ITooltipable tooltipable = ResolveTooltipable(gameObject);
            ITooltipManager tooltipManager = TooltipManagerRef(_currentManager);
            RectTransform rectTransform = gameObject.GetComponent<RectTransform>();
            if (tooltipable == null || tooltipManager == null || rectTransform == null)
            {
                return;
            }

            TooltipAnchor[] anchors = null;
            StaticTooltipLocation staticTooltipLocation;
            if (gameObject.TryGetComponent<StaticTooltipLocation>(out staticTooltipLocation) && staticTooltipLocation.Use)
            {
                rectTransform = staticTooltipLocation.GamepadLocation;
                anchors = staticTooltipLocation.DesiredAnchors;
            }

            _accessibilityTooltipHandle = tooltipManager.ForceDisplayTooltip(
                tooltipable,
                new TooltipLocation(rectTransform, anchors));
        }

        public static void HideAccessibilityTooltip()
        {
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
