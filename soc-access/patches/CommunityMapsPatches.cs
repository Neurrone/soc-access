using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    public static class CommunityMapsPatches
    {
        [HarmonyPatch(typeof(Browser), "Close")]
        [HarmonyPostfix]
        private static void BrowserClosePostfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsClosed();
        }

        [HarmonyPatch(typeof(Home), "RefreshHomePanel")]
        [HarmonyPostfix]
        private static void HomeRefreshHomePanelPostfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsHomeOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Home), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsFeaturedLoadedPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Home), "AddModProfilesToFeaturedCarousel");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsHomeContentChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsRowLoadedPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(ModListRow), "PopulateRowFromModPage");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsHomeContentChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsContextMenuOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.ModioContextMenu"), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsContextMenuClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.ModioContextMenu"), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsDetailsOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Details), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsDetailsClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Details), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsReportOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Reporting), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsReportClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Reporting), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsDownloadQueueOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DownloadQueue), "OpenDownloadQueuePanel");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsDownloadQueueClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(DownloadQueue), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsCollectionOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Collection), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsAuthenticationOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AuthenticationPanels), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsAuthenticationClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AuthenticationPanels), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsAuthenticationLogoutOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(AuthenticationPanels), "OpenPanel_Logout");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsNotificationPopupOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                AccessTools.TypeByName("ModIOBrowser.Implementation.NotificationPopup"),
                "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsNotificationPopupClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(
                AccessTools.TypeByName("ModIOBrowser.Implementation.NotificationPopup"),
                "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchPanelOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel"), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchFilterChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchPanelClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel"), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchFilterChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchPanelApplyFilterPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel"), "ApplyFilter");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchFilterChanged();
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchPanelClearFilterPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel"), "ClearFilter");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchFilterChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchPanelCreateTagsPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(AccessTools.TypeByName("ModIOBrowser.Implementation.SearchPanel"), "CreateTagCategoryListItems");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchFilterContentsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchResultsOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SearchResults), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchResultsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchResultsOpenWithoutRefreshingPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SearchResults), "OpenWithoutRefreshing");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchResultsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchResultsRefreshPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SearchResults), "Refresh");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchResultsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsSearchResultsGetPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(SearchResults), "Get");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsSearchResultsChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsCollectionRefreshListPatches
    {
        private static readonly FieldInfo SearchFieldInfo = AccessTools.Field(typeof(Collection), "CollectionPanelSearchField");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Collection), "RefreshList");
        }

        private static void Prefix(Collection __instance, out SearchFocusState __state)
        {
            __state = CaptureSearchFocus(__instance);
        }

        private static void Postfix(SearchFocusState __state)
        {
            RestoreSearchFocus(__state);
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsCollectionChanged();
        }

        private static SearchFocusState CaptureSearchFocus(Collection collection)
        {
            if (collection == null)
            {
                return null;
            }

            TMP_InputField field = SearchFieldInfo != null ? SearchFieldInfo.GetValue(collection) as TMP_InputField : null;
            if (field == null || !field.gameObject.activeInHierarchy || !field.isFocused)
            {
                return null;
            }

            return new SearchFocusState(
                field,
                field.caretPosition,
                field.selectionAnchorPosition,
                field.selectionFocusPosition);
        }

        private static void RestoreSearchFocus(SearchFocusState state)
        {
            if (state == null || state.Field == null || !state.Field.gameObject.activeInHierarchy)
            {
                return;
            }

            TMP_InputField field = state.Field;
            field.Select();
            field.ActivateInputField();
            field.caretPosition = ClampTextPosition(field, state.CaretPosition);
            field.selectionAnchorPosition = ClampTextPosition(field, state.SelectionAnchorPosition);
            field.selectionFocusPosition = ClampTextPosition(field, state.SelectionFocusPosition);
        }

        private static int ClampTextPosition(TMP_InputField field, int position)
        {
            string text = field != null ? field.text ?? string.Empty : string.Empty;
            if (position < 0)
            {
                return 0;
            }

            return position > text.Length ? text.Length : position;
        }

        private sealed class SearchFocusState
        {
            public SearchFocusState(
                TMP_InputField field,
                int caretPosition,
                int selectionAnchorPosition,
                int selectionFocusPosition)
            {
                Field = field;
                CaretPosition = caretPosition;
                SelectionAnchorPosition = selectionAnchorPosition;
                SelectionFocusPosition = selectionFocusPosition;
            }

            public TMP_InputField Field { get; private set; }

            public int CaretPosition { get; private set; }

            public int SelectionAnchorPosition { get; private set; }

            public int SelectionFocusPosition { get; private set; }
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsOpenUninstallConfirmationPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Collection), "OpenUninstallConfirmation");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsCloseUninstallConfirmationPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Collection), "CloseUninstallConfirmation");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsFiveDigitInputOpenPatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(KeyInput5DigitsUi), "Open");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsFiveDigitInputClosePatches
    {
        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(KeyInput5DigitsUi), "Close");
        }

        private static void Postfix()
        {
            SocAccessMod.Instance?.ScreenDetector?.OnCommunityMapsModalChanged();
        }
    }

    [HarmonyPatch]
    public static class CommunityMapsFiveDigitInputDuplicateKeyPatches
    {
        private static readonly Dictionary<int, FrameInput> LastInputByInstance =
            new Dictionary<int, FrameInput>();

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(KeyInput5Digits), "AddToInput");
        }

        private static bool Prefix(KeyInput5Digits __instance, KeyCode keyCode)
        {
            if (__instance == null)
            {
                return true;
            }

            string value = GetInputValue(keyCode);
            if (string.IsNullOrEmpty(value))
            {
                return true;
            }

            int instanceId = __instance.GetInstanceID();
            int frame = Time.frameCount;
            FrameInput previous;
            if (LastInputByInstance.TryGetValue(instanceId, out previous)
                && previous.Frame == frame
                && previous.Value == value)
            {
                return false;
            }

            LastInputByInstance[instanceId] = new FrameInput(frame, value);
            return true;
        }

        private static string GetInputValue(KeyCode keyCode)
        {
            int raw = (int)keyCode;
            if (raw >= 48 && raw <= 57)
            {
                return ((char)raw).ToString();
            }

            if (raw >= 256 && raw <= 265)
            {
                return ((char)('0' + raw - 256)).ToString();
            }

            if (raw >= 97 && raw <= 122)
            {
                return ((char)raw).ToString().ToUpperInvariant();
            }

            return string.Empty;
        }

        private struct FrameInput
        {
            public FrameInput(int frame, string value)
            {
                Frame = frame;
                Value = value;
            }

            public int Frame { get; private set; }

            public string Value { get; private set; }
        }
    }
}
