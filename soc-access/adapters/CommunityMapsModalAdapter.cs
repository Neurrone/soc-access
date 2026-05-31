using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class CommunityMapsModalAdapter
    {
        private static readonly HashSet<string> LoggedMissingAuthButtonLabels = new HashSet<string>();

        private readonly AuthenticationPanels _authPanels;
        private readonly GameObject _panel;
        private readonly CommunityMapsModalState? _cachedState;
        private readonly IReadOnlyList<TextItem> _cachedTexts;
        private readonly IReadOnlyList<ActionItem> _cachedActions;

        private CommunityMapsModalAdapter(
            GameObject panel,
            AuthenticationPanels authPanels = null,
            CommunityMapsModalState? cachedState = null,
            IReadOnlyList<TextItem> cachedTexts = null,
            IReadOnlyList<ActionItem> cachedActions = null)
        {
            _panel = panel;
            _authPanels = authPanels;
            _cachedState = cachedState;
            _cachedTexts = cachedTexts;
            _cachedActions = cachedActions;
        }

        public static CommunityMapsModalAdapter TryCreate()
        {
            KeyInput5DigitsUi keyInput = GetActiveKeyInput();
            if (keyInput != null)
            {
                IReadOnlyList<ActionItem> actions = GetFiveDigitActions(keyInput);
                return new CommunityMapsModalAdapter(
                    keyInput.gameObject,
                    cachedState: CommunityMapsModalState.InputFiveDigits,
                    cachedActions: actions);
            }

            AuthenticationPanels authPanels = GetActiveAuthenticationPanels();
            if (authPanels != null)
            {
                return new CommunityMapsModalAdapter(authPanels.AuthenticationPanel, authPanels);
            }

            CommunityMapsModalAdapter confirmUninstall = TryCreateConfirmUninstall();
            if (confirmUninstall != null)
            {
                return confirmUninstall;
            }

            object contextMenuComponent = FindFirst("ModIOBrowser.Implementation.ModioContextMenu");
            GameObject contextMenu = GetActiveContextMenuPanel(contextMenuComponent);
            if (contextMenu != null)
            {
                IReadOnlyList<ActionItem> actions = GetContextMenuActions(contextMenuComponent);
                return new CommunityMapsModalAdapter(
                    contextMenu,
                    cachedState: CommunityMapsModalState.ContextMenu,
                    cachedActions: actions);
            }

            GameObject downloadQueue = GetActiveDownloadQueuePanel();
            if (downloadQueue != null)
            {
                return new CommunityMapsModalAdapter(
                    downloadQueue,
                    cachedState: CommunityMapsModalState.DownloadQueue);
            }

            GameObject panel = GetActivePanel();
            return panel != null ? new CommunityMapsModalAdapter(panel) : null;
        }

        public static bool HasActiveModal()
        {
            return GetActivePanel() != null;
        }

        private static CommunityMapsModalAdapter TryCreateConfirmUninstall()
        {
            ConfirmUninstallPanel confirmUninstall = GetActiveConfirmUninstallPanel();
            if (confirmUninstall == null)
            {
                return null;
            }

            return new CommunityMapsModalAdapter(
                confirmUninstall.Panel,
                cachedState: CommunityMapsModalState.ConfirmUninstall,
                cachedTexts: GetConfirmUninstallTexts(confirmUninstall),
                cachedActions: GetPanelActions(confirmUninstall.Panel));
        }

        public bool IsPresent()
        {
            return _panel != null && _panel.activeInHierarchy;
        }

        public string Title
        {
            get
            {
                IReadOnlyList<TextItem> texts = GetTexts();
                return texts.Count > 0 ? texts[0].Text : string.Empty;
            }
        }

        public CommunityMapsModalState State
        {
            get
            {
                if (_cachedState.HasValue)
                {
                    return _cachedState.Value;
                }

                if (_authPanels != null)
                {
                    return GetAuthenticationState();
                }

                if (_panel == null)
                {
                    return CommunityMapsModalState.None;
                }

                if (_panel.GetComponentInParent<KeyInput5DigitsUi>() != null)
                {
                    return CommunityMapsModalState.InputFiveDigits;
                }

                if (_panel.GetComponentInParent<Reporting>() != null)
                {
                    return CommunityMapsModalState.Report;
                }

                if (_panel.GetComponentInParent<Collection>() != null)
                {
                    return CommunityMapsModalState.ConfirmUninstall;
                }

                if (HasComponentInParent(_panel.transform, "ModIOBrowser.Implementation.NotificationPopup"))
                {
                    return CommunityMapsModalState.Notification;
                }

                if (HasComponentInParent(_panel.transform, "ModIOBrowser.Implementation.ModioContextMenu"))
                {
                    return CommunityMapsModalState.ContextMenu;
                }

                if (IsDownloadQueuePanel(_panel))
                {
                    return CommunityMapsModalState.DownloadQueue;
                }

                return CommunityMapsModalState.Unknown;
            }
        }

        public IReadOnlyList<TextItem> GetTexts()
        {
            if (_cachedTexts != null)
            {
                return _cachedTexts;
            }

            if (_authPanels != null)
            {
                return GetAuthenticationTexts();
            }

            if (State == CommunityMapsModalState.ContextMenu)
            {
                return new TextItem[0];
            }

            if (State == CommunityMapsModalState.DownloadQueue)
            {
                return GetDownloadQueueTexts();
            }

            if (State == CommunityMapsModalState.InputFiveDigits)
            {
                return new TextItem[0];
            }

            return GetPanelTexts(_panel);
        }

        public IReadOnlyList<FiveDigitInputItem> GetFiveDigitInputs()
        {
            List<FiveDigitInputItem> result = new List<FiveDigitInputItem>();
            if (State != CommunityMapsModalState.InputFiveDigits)
            {
                return result;
            }

            KeyInput5DigitsUi keyInput = GetKeyInput(_panel);
            if (keyInput == null || keyInput.keyInput5Digits == null)
            {
                return result;
            }

            string label = CleanText(keyInput.instructionText != null ? keyInput.instructionText.text : string.Empty);
            result.Add(new FiveDigitInputItem(0, label, keyInput));
            return result;
        }

        public IReadOnlyList<InputItem> GetInputs()
        {
            if (_authPanels != null)
            {
                return GetAuthenticationInputs();
            }

            if (State == CommunityMapsModalState.InputFiveDigits
                || State == CommunityMapsModalState.ConfirmUninstall)
            {
                return new InputItem[0];
            }

            List<InputItem> result = new List<InputItem>();
            if (_panel == null)
            {
                return result;
            }

            TMP_InputField[] fields = _panel.GetComponentsInChildren<TMP_InputField>(false);
            for (int i = 0; i < fields.Length; i++)
            {
                TMP_InputField field = fields[i];
                if (field == null || !field.gameObject.activeInHierarchy)
                {
                    continue;
                }

                result.Add(new InputItem(result.Count, GetInputLabel(field), field));
            }

            return result;
        }

        public IReadOnlyList<ActionItem> GetActions()
        {
            if (_cachedActions != null)
            {
                return _cachedActions;
            }

            if (_authPanels != null)
            {
                return GetAuthenticationActions();
            }

            if (State == CommunityMapsModalState.ContextMenu)
            {
                return new ActionItem[0];
            }

            if (State == CommunityMapsModalState.DownloadQueue)
            {
                return GetDownloadQueueActions();
            }

            if (State == CommunityMapsModalState.InputFiveDigits)
            {
                return new ActionItem[0];
            }

            return GetPanelActions(_panel);
        }

        public bool Cancel()
        {
            if (State == CommunityMapsModalState.ContextMenu)
            {
                return CloseContextMenu();
            }

            if (State == CommunityMapsModalState.InputFiveDigits)
            {
                KeyInput5DigitsUi keyInput = GetKeyInput(_panel);
                if (keyInput == null)
                {
                    return false;
                }

                keyInput.CancelButton();
                return true;
            }

            Type navigating = AccessTools.TypeByName("ModIOBrowser.Navigating");
            MethodInfo cancel = navigating != null ? AccessTools.Method(navigating, "Cancel") : null;
            if (cancel == null)
            {
                return false;
            }

            cancel.Invoke(null, null);
            return true;
        }

        private static bool CloseContextMenu()
        {
            object contextMenu = FindFirst("ModIOBrowser.Implementation.ModioContextMenu");
            MethodInfo close = contextMenu != null ? AccessTools.Method(contextMenu.GetType(), "Close") : null;
            if (close == null)
            {
                return false;
            }

            close.Invoke(contextMenu, null);
            return true;
        }

        private static GameObject GetActivePanel()
        {
            object contextMenuComponent = FindFirst("ModIOBrowser.Implementation.ModioContextMenu");
            GameObject contextMenu = GetActiveContextMenuPanel(contextMenuComponent);
            if (contextMenu != null && contextMenu.activeInHierarchy)
            {
                return contextMenu;
            }

            GameObject notification = GetGameObject(FindFirst("ModIOBrowser.Implementation.NotificationPopup"));
            if (notification != null && notification.activeInHierarchy)
            {
                return notification;
            }

            KeyInput5DigitsUi keyInput = GetActiveKeyInput();
            if (keyInput != null)
            {
                return keyInput.gameObject;
            }

            AuthenticationPanels authPanels = GetActiveAuthenticationPanels();
            if (authPanels != null)
            {
                return authPanels.AuthenticationPanel;
            }

            GameObject downloadQueue = GetActiveDownloadQueuePanel();
            if (downloadQueue != null)
            {
                return downloadQueue;
            }

            ConfirmUninstallPanel confirmUninstall = GetActiveConfirmUninstallPanel();
            if (confirmUninstall != null)
            {
                return confirmUninstall.Panel;
            }

            Reporting[] reports = Resources.FindObjectsOfTypeAll<Reporting>();
            for (int i = 0; i < reports.Length; i++)
            {
                Reporting report = reports[i];
                if (report != null && report.Panel != null && report.Panel.activeInHierarchy)
                {
                    return report.Panel;
                }
            }

            return null;
        }

        private static ConfirmUninstallPanel GetActiveConfirmUninstallPanel()
        {
            Collection[] collections = Resources.FindObjectsOfTypeAll<Collection>();
            for (int i = 0; i < collections.Length; i++)
            {
                Collection collection = collections[i];
                if (collection != null
                    && collection.uninstallConfirmationPanel != null
                    && collection.uninstallConfirmationPanel.activeInHierarchy)
                {
                    return new ConfirmUninstallPanel(collection, collection.uninstallConfirmationPanel);
                }
            }

            return null;
        }

        private static IReadOnlyList<TextItem> GetConfirmUninstallTexts(ConfirmUninstallPanel confirmUninstall)
        {
            List<TextItem> result = new List<TextItem>();
            if (confirmUninstall == null || confirmUninstall.Panel == null)
            {
                return result;
            }

            string modName = CleanText(GetText(GetField<TMP_Text>(confirmUninstall.Collection, "uninstallConfirmationPanelModName")));
            string fileSize = CleanText(GetText(GetField<TMP_Text>(confirmUninstall.Collection, "uninstallConfirmationPanelFileSize")));
            List<string> lines = new List<string>();
            IReadOnlyList<string> panelTexts = GetPanelTextValues(confirmUninstall.Panel);
            for (int i = 0; i < panelTexts.Count; i++)
            {
                string text = panelTexts[i];
                if (string.IsNullOrWhiteSpace(text) || text == modName || text == fileSize)
                {
                    continue;
                }

                AddUnique(lines, text);
            }

            AddUnique(lines, modName);
            AddUnique(lines, fileSize);
            if (lines.Count > 0)
            {
                result.Add(new TextItem(0, string.Join("\n", lines.ToArray())));
            }

            return result;
        }

        private static IReadOnlyList<TextItem> GetPanelTexts(GameObject panel)
        {
            List<TextItem> result = new List<TextItem>();
            IReadOnlyList<string> values = GetPanelTextValues(panel);
            for (int i = 0; i < values.Count; i++)
            {
                result.Add(new TextItem(result.Count, values[i]));
            }

            return result;
        }

        private static IReadOnlyList<string> GetPanelTextValues(GameObject panel)
        {
            List<string> result = new List<string>();
            if (panel == null)
            {
                return result;
            }

            HashSet<string> seen = new HashSet<string>();
            TMP_Text[] texts = panel.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null || !text.gameObject.activeInHierarchy || text.GetComponentInParent<Button>() != null)
                {
                    continue;
                }

                TMP_InputField input = text.GetComponentInParent<TMP_InputField>();
                if (input != null)
                {
                    continue;
                }

                string value = CleanText(text.text);
                if (string.IsNullOrWhiteSpace(value) || !seen.Add(value))
                {
                    continue;
                }

                result.Add(value);
            }

            return result;
        }

        private static void AddUnique(List<string> values, string value)
        {
            if (string.IsNullOrWhiteSpace(value) || values.Contains(value))
            {
                return;
            }

            values.Add(value);
        }

        private static string GetText(TMP_Text text)
        {
            return text != null ? text.text : string.Empty;
        }

        private static IReadOnlyList<ActionItem> GetPanelActions(GameObject panel)
        {
            List<ActionItem> result = new List<ActionItem>();
            if (panel == null)
            {
                return result;
            }

            Button[] buttons = panel.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                Button button = buttons[i];
                if (button == null || !button.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string label = GetButtonLabel(button);
                if (string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                result.Add(new ActionItem(result.Count, label, button));
            }

            return result;
        }

        private static KeyInput5DigitsUi GetActiveKeyInput()
        {
            KeyInput5DigitsUi[] keyInputs = Resources.FindObjectsOfTypeAll<KeyInput5DigitsUi>();
            for (int i = 0; i < keyInputs.Length; i++)
            {
                if (keyInputs[i] != null && keyInputs[i].gameObject.activeInHierarchy)
                {
                    return keyInputs[i];
                }
            }

            return null;
        }

        private static KeyInput5DigitsUi GetKeyInput(GameObject panel)
        {
            return panel != null ? panel.GetComponentInParent<KeyInput5DigitsUi>() : null;
        }

        private static IReadOnlyList<ActionItem> GetFiveDigitActions(KeyInput5DigitsUi keyInput)
        {
            List<ActionItem> result = new List<ActionItem>();
            if (keyInput == null)
            {
                return result;
            }

            Button[] buttons = keyInput.GetComponentsInChildren<Button>(false);
            for (int i = 0; i < buttons.Length; i++)
            {
                AddButtonAction(result, buttons[i]);
            }

            return result;
        }

        private static GameObject GetActiveDownloadQueuePanel()
        {
            DownloadQueue[] queues = Resources.FindObjectsOfTypeAll<DownloadQueue>();
            for (int i = 0; i < queues.Length; i++)
            {
                DownloadQueue queue = queues[i];
                if (queue != null
                    && queue.DownloadQueuePanel != null
                    && queue.DownloadQueuePanel.activeInHierarchy)
                {
                    return queue.DownloadQueuePanel;
                }
            }

            return null;
        }

        private static bool IsDownloadQueuePanel(GameObject panel)
        {
            if (panel == null)
            {
                return false;
            }

            DownloadQueue[] queues = Resources.FindObjectsOfTypeAll<DownloadQueue>();
            for (int i = 0; i < queues.Length; i++)
            {
                DownloadQueue queue = queues[i];
                if (queue != null && queue.DownloadQueuePanel == panel)
                {
                    return true;
                }
            }

            return false;
        }

        private IReadOnlyList<TextItem> GetDownloadQueueTexts()
        {
            List<TextItem> result = new List<TextItem>();
            DownloadQueue queue = GetDownloadQueueForPanel(_panel);
            if (queue == null)
            {
                return result;
            }

            TMP_Text currentHeading = GetField<TMP_Text>(queue, "DownloadQueueCurrentJobText");
            GameObject noCurrentNotice = GetField<GameObject>(queue, "DownloadQueueNoCurrentNotice");
            AddHeadingAndBody(result, currentHeading, noCurrentNotice);

            GameObject noPendingNotice = GetField<GameObject>(queue, "DownloadQueueNoPendingNotice");
            TMP_Text queueHeading = FindNearestPreviousVisibleText(noPendingNotice);
            AddHeadingAndBody(result, queueHeading, noPendingNotice);

            return result;
        }

        private IReadOnlyList<ActionItem> GetDownloadQueueActions()
        {
            List<ActionItem> result = new List<ActionItem>();
            DownloadQueue queue = GetDownloadQueueForPanel(_panel);
            if (queue == null)
            {
                return result;
            }

            AddButtonAction(
                result,
                GetField<Button>(queue, "DownloadQueueCurrentUnsubscribeButton"));
            AddButtonAction(
                result,
                GetField<Button>(queue, "DownloadQueueCurrentLogoutButton"));
            return result;
        }

        private static DownloadQueue GetDownloadQueueForPanel(GameObject panel)
        {
            if (panel == null)
            {
                return null;
            }

            DownloadQueue[] queues = Resources.FindObjectsOfTypeAll<DownloadQueue>();
            for (int i = 0; i < queues.Length; i++)
            {
                DownloadQueue queue = queues[i];
                if (queue != null && queue.DownloadQueuePanel == panel)
                {
                    return queue;
                }
            }

            return null;
        }

        private static void AddHeadingAndBody(List<TextItem> result, TMP_Text heading, GameObject bodyRoot)
        {
            string body = GetFirstText(bodyRoot != null ? bodyRoot.transform : null);
            if (string.IsNullOrWhiteSpace(body))
            {
                return;
            }

            string label = CleanText(heading != null ? heading.text : string.Empty);
            string text = string.IsNullOrWhiteSpace(label) ? body : label + "\n" + body;
            result.Add(new TextItem(result.Count, text));
        }

        private static TMP_Text FindNearestPreviousVisibleText(GameObject source)
        {
            Transform current = source != null ? source.transform : null;
            while (current != null && current.parent != null)
            {
                int siblingIndex = current.GetSiblingIndex();
                Transform parent = current.parent;
                for (int i = siblingIndex - 1; i >= 0; i--)
                {
                    TMP_Text text = FindLastVisibleText(parent.GetChild(i));
                    if (text != null)
                    {
                        return text;
                    }
                }

                current = parent;
            }

            return null;
        }

        private static TMP_Text FindLastVisibleText(Transform root)
        {
            TMP_Text[] texts = root != null ? root.GetComponentsInChildren<TMP_Text>(false) : null;
            if (texts == null)
            {
                return null;
            }

            for (int i = texts.Length - 1; i >= 0; i--)
            {
                TMP_Text text = texts[i];
                if (text == null
                    || !text.gameObject.activeInHierarchy
                    || text.GetComponentInParent<Button>() != null
                    || string.IsNullOrWhiteSpace(CleanText(text.text)))
                {
                    continue;
                }

                return text;
            }

            return null;
        }

        private static IReadOnlyList<ActionItem> GetContextMenuActions(object contextMenu)
        {
            List<ActionItem> result = new List<ActionItem>();
            Transform list = GetField<Transform>(contextMenu, "ContextMenuList");
            if (list == null)
            {
                return result;
            }

            for (int i = 0; i < list.childCount; i++)
            {
                Transform child = list.GetChild(i);
                if (child == null || !child.gameObject.activeInHierarchy)
                {
                    continue;
                }

                string label = GetFirstText(child);
                Button button = child.GetComponentInChildren<Button>(false);
                if (button == null || !button.gameObject.activeInHierarchy || string.IsNullOrWhiteSpace(label))
                {
                    continue;
                }

                result.Add(new ActionItem(result.Count, label, button));
            }

            return result;
        }

        private IReadOnlyList<TextItem> GetAuthenticationTexts()
        {
            List<TextItem> result = new List<TextItem>();
            AddText(result, _authPanels.AuthenticationPanelTitleText);
            AddText(result, _authPanels.AuthenticationPanelInfoText);
            AddText(result, _authPanels.AuthenticationPanelExternalCode);
            AddText(result, _authPanels.AuthenticationPanelExternalUrl);
            AddText(result, _authPanels.AuthenticationPanelExternalCodeTimer);
            return result;
        }

        private IReadOnlyList<InputItem> GetAuthenticationInputs()
        {
            List<InputItem> result = new List<InputItem>();
            if (_authPanels.AuthenticationPanelEnterEmail != null
                && _authPanels.AuthenticationPanelEnterEmail.activeInHierarchy
                && _authPanels.AuthenticationPanelEmailField != null
                && _authPanels.AuthenticationPanelEmailField.gameObject.activeInHierarchy)
            {
                result.Add(new InputItem(result.Count, GetInputLabel(_authPanels.AuthenticationPanelEmailField), _authPanels.AuthenticationPanelEmailField));
            }

            if (_authPanels.AuthenticationPanelEnterCode != null
                && _authPanels.AuthenticationPanelEnterCode.activeInHierarchy
                && _authPanels.AuthenticationPanelCodeFields != null)
            {
                for (int i = 0; i < _authPanels.AuthenticationPanelCodeFields.Length; i++)
                {
                    TMP_InputField field = _authPanels.AuthenticationPanelCodeFields[i];
                    if (field != null && field.gameObject.activeInHierarchy)
                    {
                        result.Add(new InputItem(result.Count, GetInputLabel(field), field));
                    }
                }
            }

            return result;
        }

        private IReadOnlyList<ActionItem> GetAuthenticationActions()
        {
            List<ActionItem> result = new List<ActionItem>();
            AddButtonAction(
                result,
                _authPanels.AuthenticationPanelBackButton,
                CleanText(_authPanels.AuthenticationPanelBackButtonText != null ? _authPanels.AuthenticationPanelBackButtonText.text : string.Empty),
                "AuthenticationPanelBackButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaSteamButton, null, "AuthenticationPanelConnectViaSteamButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaEmailButton, null, "AuthenticationPanelConnectViaEmailButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaExternalButton, null, "AuthenticationPanelConnectViaExternalButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaEpicButton, null, "AuthenticationPanelConnectViaEpicButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaGOGButton, null, "AuthenticationPanelConnectViaGOGButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaXboxButton, null, "AuthenticationPanelConnectViaXboxButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaSwitchButton, null, "AuthenticationPanelConnectViaSwitchButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaPlayStationButton, null, "AuthenticationPanelConnectViaPlayStationButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelAgreeButton, null, "AuthenticationPanelAgreeButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelSendCodeButton, null, "AuthenticationPanelSendCodeButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelSubmitButton, null, "AuthenticationPanelSubmitButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelCompletedButton, null, "AuthenticationPanelCompletedButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelLogoutButton, null, "AuthenticationPanelLogoutButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelTOSButton, null, "AuthenticationPanelTOSButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelPrivacyPolicyButton, null, "AuthenticationPanelPrivacyPolicyButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelCancelButton, null, "AuthenticationPanelCancelButton");
            AddButtonAction(result, _authPanels.AuthenticationPanelExternalCancelButton, null, "AuthenticationPanelExternalCancelButton");
            return result;
        }

        private CommunityMapsModalState GetAuthenticationState()
        {
            if (_authPanels.AuthenticationPanelWaitingForResponseAnimation != null
                && _authPanels.AuthenticationPanelWaitingForResponseAnimation.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthWaiting;
            }

            if (_authPanels.AuthenticationPanelEnterEmail != null
                && _authPanels.AuthenticationPanelEnterEmail.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthEmail;
            }

            if (_authPanels.AuthenticationPanelEnterCode != null
                && _authPanels.AuthenticationPanelEnterCode.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthCode;
            }

            if (_authPanels.AuthenticationPanelTermsOfUseLinks != null
                && _authPanels.AuthenticationPanelTermsOfUseLinks.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthTerms;
            }

            if (_authPanels.AuthenticationPanelCompletedButton != null
                && _authPanels.AuthenticationPanelCompletedButton.gameObject.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthComplete;
            }

            if (_authPanels.AuthenticationPanelLogoutButton != null
                && _authPanels.AuthenticationPanelLogoutButton.gameObject.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthLogout;
            }

            if (_authPanels.AuthenticationPanelCancelButton != null
                && _authPanels.AuthenticationPanelCancelButton.gameObject.activeInHierarchy)
            {
                return CommunityMapsModalState.AuthProblem;
            }

            return CommunityMapsModalState.AuthMain;
        }

        private static void AddText(List<TextItem> result, TMP_Text text)
        {
            if (text == null || !text.gameObject.activeInHierarchy)
            {
                return;
            }

            string value = CleanText(text.text);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            result.Add(new TextItem(result.Count, value));
        }

        private static void AddButtonAction(List<ActionItem> result, Button button, string fallbackLabel = null, string diagnosticName = null)
        {
            if (button == null || !button.gameObject.activeInHierarchy)
            {
                return;
            }

            string label = GetButtonLabel(button);
            if (string.IsNullOrWhiteSpace(label))
            {
                label = fallbackLabel;
            }

            if (string.IsNullOrWhiteSpace(label))
            {
                LogMissingAuthButtonLabel(button, diagnosticName);
                return;
            }

            result.Add(new ActionItem(result.Count, label, button));
        }

        private static AuthenticationPanels GetActiveAuthenticationPanels()
        {
            AuthenticationPanels[] authPanels = Resources.FindObjectsOfTypeAll<AuthenticationPanels>();
            for (int i = 0; i < authPanels.Length; i++)
            {
                AuthenticationPanels panels = authPanels[i];
                if (panels != null && panels.AuthenticationPanel != null && panels.AuthenticationPanel.activeInHierarchy)
                {
                    return panels;
                }
            }

            return null;
        }

        private static object FindFirst(string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return null;
            }

            UnityEngine.Object[] objects = Resources.FindObjectsOfTypeAll(type);
            return objects.Length > 0 ? objects[0] : null;
        }

        private static GameObject GetActiveContextMenuPanel(object contextMenu)
        {
            Component component = contextMenu as Component;
            if (component != null && component.gameObject.activeInHierarchy)
            {
                return component.gameObject;
            }

            GameObject panel = GetField<GameObject>(contextMenu, "ContextMenu");
            return panel != null && panel.activeInHierarchy ? panel : null;
        }

        private static bool HasComponentInParent(Transform transform, string typeName)
        {
            Type type = AccessTools.TypeByName(typeName);
            if (type == null)
            {
                return false;
            }

            Transform current = transform;
            while (current != null)
            {
                if (current.GetComponent(type) != null)
                {
                    return true;
                }

                current = current.parent;
            }

            return false;
        }

        private static GameObject GetGameObject(object component)
        {
            Component unityComponent = component as Component;
            return unityComponent != null ? unityComponent.gameObject : null;
        }

        private static T GetField<T>(object instance, string name)
        {
            if (instance == null)
            {
                return default(T);
            }

            FieldInfo field = AccessTools.Field(instance.GetType(), name);
            return field != null ? (T)field.GetValue(instance) : default(T);
        }

        private static string GetButtonLabel(Button button)
        {
            TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                string value = CleanText(texts[i] != null ? texts[i].text : string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static string GetFirstText(Transform transform)
        {
            TMP_Text[] texts = transform.GetComponentsInChildren<TMP_Text>(false);
            for (int i = 0; i < texts.Length; i++)
            {
                string value = CleanText(texts[i] != null ? texts[i].text : string.Empty);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            return string.Empty;
        }

        private static void LogMissingAuthButtonLabel(Button button, string diagnosticName)
        {
            if (button == null)
            {
                return;
            }

            string key = !string.IsNullOrWhiteSpace(diagnosticName) ? diagnosticName : button.name;
            if (!LoggedMissingAuthButtonLabels.Add(key))
            {
                return;
            }

            List<string> parts = new List<string>();
            TMP_Text[] texts = button.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < texts.Length; i++)
            {
                TMP_Text text = texts[i];
                if (text == null)
                {
                    continue;
                }

                parts.Add(
                    GetTransformPath(text.transform)
                    + " active="
                    + text.gameObject.activeInHierarchy
                    + " text=\""
                    + CleanText(text.text)
                    + "\"");
            }

            SocAccessPlugin.Instance?.LogWarning(
                "CommunityMapsModalAdapter could not extract auth button label for "
                + key
                + " object="
                + button.name
                + " path="
                + GetTransformPath(button.transform)
                + " textChildren=["
                + string.Join("; ", parts.ToArray())
                + "]");
        }

        private static string GetTransformPath(Transform transform)
        {
            if (transform == null)
            {
                return string.Empty;
            }

            List<string> names = new List<string>();
            Transform current = transform;
            while (current != null)
            {
                names.Add(current.name);
                current = current.parent;
            }

            names.Reverse();
            return string.Join("/", names.ToArray());
        }

        private static string GetInputLabel(TMP_InputField field)
        {
            TMP_Text placeholder = field.placeholder as TMP_Text;
            string placeholderText = CleanText(placeholder != null ? placeholder.text : string.Empty);
            return !string.IsNullOrWhiteSpace(placeholderText) ? placeholderText : field.name;
        }

        private static string CleanText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return text.Replace("<color=red>", string.Empty).Replace("</color>", string.Empty);
        }

        internal sealed class TextItem
        {
            public TextItem(int index, string text)
            {
                Index = index;
                Text = text ?? string.Empty;
            }

            public int Index { get; private set; }
            public string Text { get; private set; }
        }

        internal sealed class InputItem
        {
            public InputItem(int index, string label, TMP_InputField field)
            {
                Index = index;
                Label = label ?? string.Empty;
                Field = field;
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
            public TMP_InputField Field { get; private set; }
        }

        internal sealed class FiveDigitInputItem
        {
            private readonly KeyInput5DigitsUi _keyInput;

            public FiveDigitInputItem(int index, string label, KeyInput5DigitsUi keyInput)
            {
                Index = index;
                Label = label ?? string.Empty;
                _keyInput = keyInput;
            }

            public int Index { get; private set; }

            public string Label { get; private set; }

            public bool IsVisible
            {
                get { return _keyInput != null && _keyInput.gameObject.activeInHierarchy; }
            }

            public string Value
            {
                get
                {
                    string value = _keyInput != null && _keyInput.keyInput5Digits != null
                        ? _keyInput.keyInput5Digits.currentInputString
                        : string.Empty;
                    return value != null ? value.TrimEnd() : string.Empty;
                }
            }

            public void Focus()
            {
                if (EventSystem.current != null)
                {
                    EventSystem.current.SetSelectedGameObject(null);
                }
            }

            public bool Activate()
            {
                if (!IsVisible)
                {
                    return false;
                }

                _keyInput.ContinueButton();
                return true;
            }
        }

        internal sealed class ActionItem
        {
            public ActionItem(int index, string label, Button button)
            {
                Index = index;
                Label = label ?? string.Empty;
                Button = button;
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
            public Button Button { get; private set; }

            public bool IsEnabled
            {
                get { return Button != null && Button.interactable; }
            }

            public void Focus()
            {
                Button?.Select();
            }

            public bool Activate()
            {
                if (!IsEnabled)
                {
                    return false;
                }

                Button.onClick.Invoke();
                return true;
            }
        }

        private sealed class ConfirmUninstallPanel
        {
            public ConfirmUninstallPanel(Collection collection, GameObject panel)
            {
                Collection = collection;
                Panel = panel;
            }

            public Collection Collection { get; private set; }

            public GameObject Panel { get; private set; }
        }
    }

    internal enum CommunityMapsModalState
    {
        None,
        Unknown,
        AuthMain,
        AuthEmail,
        AuthCode,
        AuthWaiting,
        AuthProblem,
        AuthTerms,
        AuthComplete,
        AuthLogout,
        ContextMenu,
        DownloadQueue,
        ConfirmUninstall,
        Report,
        Notification,
        InputFiveDigits
    }
}
