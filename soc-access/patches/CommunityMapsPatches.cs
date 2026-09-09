using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser.Implementation;
using TMPro;
using UnityEngine;

namespace SongsOfConquestAccess
{
    /// <summary>
    /// What is left of the community maps hooks after the six screens learned to find their own
    /// panel (AGENTS.md, "Screen Resolution"). Everything that only told the detector something had
    /// opened, closed or refreshed is gone: mod.io's panels are singletons whose game objects say
    /// which one is drawn, and <c>Navigating.GoToPanel</c> deactivates every other panel before it
    /// shows one, so a per-frame read answers all of it.
    ///
    /// The two below stay because each ALTERS BEHAVIOUR rather than reporting readiness: the first
    /// keeps the collection's search box usable across a list refresh, the second swallows a
    /// duplicate key in the code box.
    /// </summary>
    [HarmonyPatch]
    public static class CommunityMapsCollectionRefreshListPatches
    {
        private static readonly FieldInfo SearchFieldInfo = AccessTools.Field(typeof(Collection), "CollectionPanelSearchField");

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(Collection), "RefreshList");
        }

        /// <summary>INTERCEPTION. Refreshing the list rebuilds the panel under the keyboard, which
        /// drops the search field's focus and caret mid-word; both are put back afterwards.</summary>
        private static void Prefix(Collection __instance, out SearchFocusState __state)
        {
            __state = CaptureSearchFocus(__instance);
        }

        private static void Postfix(SearchFocusState __state)
        {
            RestoreSearchFocus(__state);
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

        public sealed class SearchFocusState
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
    public static class CommunityMapsFiveDigitInputDuplicateKeyPatches
    {
        private static readonly Dictionary<int, FrameInput> LastInputByInstance =
            new Dictionary<int, FrameInput>();

        /// <summary>Per-load state, cleared by <c>SocAccessMod.Stop</c>: a dictionary keyed on
        /// instance ids that would otherwise hold entries for code boxes of a previous load.</summary>
        public static void Reset()
        {
            LastInputByInstance.Clear();
        }

        private static MethodBase TargetMethod()
        {
            return AccessTools.Method(typeof(KeyInput5Digits), "AddToInput");
        }

        /// <summary>INTERCEPTION. The code box takes the same key twice in one frame - once from
        /// mod.io's own scan and once from the key the mod pressed - so the second is dropped.
        /// </summary>
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
