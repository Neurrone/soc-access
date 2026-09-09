using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsModalAdapter
    {
        private readonly AuthenticationPanels _authPanels;
        private readonly GameObject _panel;
        private readonly CommunityMapsModalState? _cachedState;
        private readonly IReadOnlyList<TextItem> _cachedTexts;
        private readonly IReadOnlyList<ActionItem> _cachedActions;

        // What the plain panel turned out to be, and the download queue behind it. Both are settled
        // by the panel this adapter was made for, which never changes under it; answering them meant
        // a scan of the scene and seven parent walks, one to three times per build.
        private CommunityMapsModalState? _panelState;
        private DownloadQueue _downloadQueue;
        private bool _downloadQueueProbed;

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

        /// <summary>The panel the walk below would land on, and nothing more. This is what the
        /// screen's source answers with: it changes exactly when the modal changes, so the adapter is
        /// built once per panel rather than once per frame. Every read here is a mod.io singleton
        /// guarded by <see cref="CommunityMapsSources"/>.</summary>
        public static GameObject ActivePanel
        {
            get
            {
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

                ConfirmUninstallPanel confirmUninstall = GetActiveConfirmUninstallPanel();
                if (confirmUninstall != null)
                {
                    return confirmUninstall.Panel;
                }

                GameObject contextMenu = GetActiveContextMenuPanel(CommunityMapsSources.ContextMenu);
                if (contextMenu != null)
                {
                    return contextMenu;
                }

                GameObject downloadQueue = GetActiveDownloadQueuePanel();
                if (downloadQueue != null)
                {
                    return downloadQueue;
                }

                return GetActivePanel();
            }
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

            object contextMenuComponent = CommunityMapsSources.ContextMenu;
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

                // The authentication flow moves from panel to panel under one adapter, so its state is
                // the one that is asked again every time.
                if (_authPanels != null)
                {
                    return GetAuthenticationState();
                }

                if (_panelState.HasValue)
                {
                    return _panelState.Value;
                }

                _panelState = ResolvePanelState();
                return _panelState.Value;
            }
        }

        /// <summary>What the panel this adapter was made for turned out to be. Settled once: the
        /// parents of a panel do not change, and the answer was worth a scan of the scene plus seven
        /// walks up the hierarchy on every read.</summary>
        private CommunityMapsModalState ResolvePanelState()
        {
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

            if (GetDownloadQueue() != null)
            {
                return CommunityMapsModalState.DownloadQueue;
            }

            return CommunityMapsModalState.Unknown;
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
            object contextMenu = CommunityMapsSources.ContextMenu;
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
            GameObject contextMenu = GetActiveContextMenuPanel(CommunityMapsSources.ContextMenu);
            if (contextMenu != null && contextMenu.activeInHierarchy)
            {
                return contextMenu;
            }

            GameObject notification = GetGameObject(CommunityMapsSources.NotificationPopup);
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

            Reporting report = CommunityMapsSources.Reporting;
            return report != null && report.Panel != null && report.Panel.activeInHierarchy
                ? report.Panel
                : null;
        }

        private static ConfirmUninstallPanel GetActiveConfirmUninstallPanel()
        {
            Collection collection = CommunityMapsSources.Collection;
            return collection != null
                && collection.uninstallConfirmationPanel != null
                && collection.uninstallConfirmationPanel.activeInHierarchy
                    ? new ConfirmUninstallPanel(collection, collection.uninstallConfirmationPanel)
                    : null;
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
            KeyInput5DigitsUi keyInput = CommunityMapsSources.KeyInput;
            return keyInput != null && keyInput.gameObject.activeInHierarchy ? keyInput : null;
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
            DownloadQueue queue = CommunityMapsSources.DownloadQueue;
            return queue != null
                && queue.DownloadQueuePanel != null
                && queue.DownloadQueuePanel.activeInHierarchy
                    ? queue.DownloadQueuePanel
                    : null;
        }

        private IReadOnlyList<TextItem> GetDownloadQueueTexts()
        {
            List<TextItem> result = new List<TextItem>();
            DownloadQueue queue = GetDownloadQueue();
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
            DownloadQueue queue = GetDownloadQueue();
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

        /// <summary>The queue this modal's panel belongs to, or null for a panel that is not one.
        /// Scanned for once: the answer is a property of the panel, and both the texts and the actions
        /// ask for it on every build.</summary>
        private DownloadQueue GetDownloadQueue()
        {
            if (_downloadQueueProbed)
            {
                return _downloadQueue;
            }

            _downloadQueueProbed = true;
            _downloadQueue = GetDownloadQueueForPanel(_panel);
            return _downloadQueue;
        }

        private static DownloadQueue GetDownloadQueueForPanel(GameObject panel)
        {
            DownloadQueue queue = CommunityMapsSources.DownloadQueue;
            return panel != null && queue != null && queue.DownloadQueuePanel == panel ? queue : null;
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
                CleanText(_authPanels.AuthenticationPanelBackButtonText != null ? _authPanels.AuthenticationPanelBackButtonText.text : string.Empty));
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaSteamButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaEmailButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaExternalButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaEpicButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaGOGButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaXboxButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaSwitchButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelConnectViaPlayStationButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelAgreeButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelSendCodeButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelSubmitButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelCompletedButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelLogoutButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelTOSButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelPrivacyPolicyButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelCancelButton);
            AddButtonAction(result, _authPanels.AuthenticationPanelExternalCancelButton);
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

        private static void AddButtonAction(List<ActionItem> result, Button button, string fallbackLabel = null)
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
                return;
            }

            result.Add(new ActionItem(result.Count, label, button));
        }

        private static AuthenticationPanels GetActiveAuthenticationPanels()
        {
            AuthenticationPanels panels = CommunityMapsSources.AuthenticationPanels;
            return panels != null
                && panels.AuthenticationPanel != null
                && panels.AuthenticationPanel.activeInHierarchy
                    ? panels
                    : null;
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

        public sealed class TextItem
        {
            public TextItem(int index, string text)
            {
                Index = index;
                Lines = SpokenLines.Of(new[] { text });
                Text = string.Join(" ", Lines);
            }

            public int Index { get; private set; }

            /// <summary>The whole block as one line, its paragraphs joined by a space.</summary>
            public string Text { get; private set; }

            /// <summary>The block's paragraphs, one line each, as the modal wrote them.</summary>
            public IList<string> Lines { get; private set; }
        }

        public sealed class InputItem
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

        public sealed class FiveDigitInputItem
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

            /// <summary>The panel the game draws the code boxes on, so a screen can key a control on
            /// it and ask whether it is still painted.</summary>
            public Component Owner
            {
                get { return _keyInput; }
            }

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

        public sealed class ActionItem
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

    public enum CommunityMapsModalState
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
