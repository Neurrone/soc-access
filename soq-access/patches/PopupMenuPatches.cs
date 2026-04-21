using System.Collections.Generic;
using HarmonyLib;
using SongsOfConquest.Client.Menu.Popup;

namespace SongsOfConquestAccess
{
    [HarmonyPatch]
    internal static class PopupMenuPatches
    {
        private sealed class PendingQuestionDialog
        {
            public string Title;
            public string Body;
            public string PositiveLabel;
            public string NegativeLabel;
        }

        private static readonly AccessTools.FieldRef<PopupMenu, PopupMenu.Settings> SettingsRef =
            AccessTools.FieldRefAccess<PopupMenu, PopupMenu.Settings>("_settings");
        private static readonly Dictionary<PopupMenu, PendingQuestionDialog> PendingQuestions =
            new Dictionary<PopupMenu, PendingQuestionDialog>();

        [HarmonyPatch(typeof(PopupMenu), "AskQuestion")]
        [HarmonyPostfix]
        private static void PopupMenuAskQuestionPostfix(PopupMenu __instance, string header, string message, string positiveButton, string negativeButton)
        {
            if (__instance != null)
            {
                PendingQuestions[__instance] = new PendingQuestionDialog
                {
                    Title = header,
                    Body = message,
                    PositiveLabel = positiveButton,
                    NegativeLabel = negativeButton
                };
            }

            SoqAccessPlugin.Instance?.LogInfo(
                "PopupMenu.AskQuestion postfix hit; cached title="
                + Quote(header)
                + ", positive="
                + Quote(positiveButton)
                + ", negative="
                + Quote(negativeButton)
                + ", body="
                + Quote(Truncate(message)));
        }

        [HarmonyPatch(typeof(PopupMenu), "OnOpened")]
        [HarmonyPostfix]
        private static void PopupMenuOnOpenedPostfix(PopupMenu __instance)
        {
            SoqAccessPlugin.Instance?.LogInfo("PopupMenu.OnOpened postfix hit");
            PopupMenu.Settings settings = null;
            if (__instance != null)
            {
                settings = SettingsRef(__instance);
            }

            PendingQuestionDialog pending = null;
            if (__instance != null)
            {
                PendingQuestions.TryGetValue(__instance, out pending);
            }

            SoqAccessPlugin.Instance?.LogInfo(
                "Question dialog open signal with cached args: title="
                + Quote(pending != null ? pending.Title : null)
                + ", positive="
                + Quote(pending != null ? pending.PositiveLabel : null)
                + ", negative="
                + Quote(pending != null ? pending.NegativeLabel : null)
                + ", body="
                + Quote(Truncate(pending != null ? pending.Body : null)));
            SoqAccessPlugin.Instance?.LogInfo("Question dialog open signal with settings: " + DescribeSettings(settings));
            SoqAccessPlugin.Instance?.ScreenDetector?.OnQuestionDialogOpened(
                __instance,
                settings,
                pending != null ? pending.Title : null,
                pending != null ? pending.Body : null,
                pending != null ? pending.PositiveLabel : null,
                pending != null ? pending.NegativeLabel : null);
        }

        [HarmonyPatch(typeof(PopupMenu), "HandlePositiveButtonClicked")]
        [HarmonyPostfix]
        private static void PopupMenuHandlePositivePostfix(PopupMenu __instance)
        {
            SoqAccessPlugin.Instance?.LogInfo("PopupMenu.HandlePositiveButtonClicked postfix hit");
            RemovePendingQuestion(__instance);
            SoqAccessPlugin.Instance?.LogInfo("Question dialog close signal");
            SoqAccessPlugin.Instance?.ScreenDetector?.OnQuestionDialogClosed(__instance);
        }

        [HarmonyPatch(typeof(PopupMenu), "HandleNegativeButtonClicked")]
        [HarmonyPostfix]
        private static void PopupMenuHandleNegativePostfix(PopupMenu __instance)
        {
            SoqAccessPlugin.Instance?.LogInfo("PopupMenu.HandleNegativeButtonClicked postfix hit");
            RemovePendingQuestion(__instance);
            SoqAccessPlugin.Instance?.LogInfo("Question dialog close signal");
            SoqAccessPlugin.Instance?.ScreenDetector?.OnQuestionDialogClosed(__instance);
        }

        [HarmonyPatch(typeof(PopupMenu), "OnClosed")]
        [HarmonyPostfix]
        private static void PopupMenuOnClosedPostfix(PopupMenu __instance)
        {
            SoqAccessPlugin.Instance?.LogInfo("PopupMenu.OnClosed postfix hit");
            RemovePendingQuestion(__instance);
            SoqAccessPlugin.Instance?.LogInfo("Question dialog close signal");
            SoqAccessPlugin.Instance?.ScreenDetector?.OnQuestionDialogClosed(__instance);
        }

        private static void RemovePendingQuestion(PopupMenu popupMenu)
        {
            if (popupMenu == null)
            {
                PendingQuestions.Clear();
                return;
            }

            PendingQuestions.Remove(popupMenu);
        }

        private static string Quote(string value)
        {
            return "\"" + (value ?? string.Empty) + "\"";
        }

        private static string Truncate(string value)
        {
            if (string.IsNullOrEmpty(value) || value.Length <= 120)
            {
                return value ?? string.Empty;
            }

            return value.Substring(0, 120) + "...";
        }

        private static string DescribeSettings(PopupMenu.Settings settings)
        {
            if (settings == null)
            {
                return "null";
            }

            string title = settings.HeaderText != null ? settings.HeaderText.Text : "<null>";
            string body = settings.MessageText != null ? settings.MessageText.Text : "<null>";
            string positive = settings.PositiveButton != null ? settings.PositiveButton.Text : "<null>";
            string negative = settings.NegativeButton != null ? settings.NegativeButton.Text : "<null>";
            bool containerActive = settings.ContainerTransform != null && settings.ContainerTransform.Active;
            bool inputActive = settings.InputField != null && settings.InputField.Active;
            return "active=" + containerActive
                + ", inputActive=" + inputActive
                + ", title=" + Quote(title)
                + ", positive=" + Quote(positive)
                + ", negative=" + Quote(negative)
                + ", body=" + Quote(Truncate(body));
        }
    }
}
