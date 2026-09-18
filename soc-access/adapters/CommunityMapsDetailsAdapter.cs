using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using ModIOBrowser;
using ModIOBrowser.Implementation;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.UI;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class CommunityMapsDetailsAdapter : IPresent
    {
        private static readonly FieldInfo ContentRectField = AccessTools.Field(typeof(Details), "ModDetailsContentRect");
        private static readonly FieldInfo NameField = AccessTools.Field(typeof(Details), "ModDetailsName");
        private static readonly FieldInfo SummaryField = AccessTools.Field(typeof(Details), "ModDetailsSummary");
        private static readonly FieldInfo DescriptionField = AccessTools.Field(typeof(Details), "ModDetailsDescription");
        private static readonly FieldInfo SubscribeTextField = AccessTools.Field(typeof(Details), "ModDetailsSubscribeButtonText");
        private static readonly FieldInfo FileSizeField = AccessTools.Field(typeof(Details), "ModDetailsFileSize");
        private static readonly FieldInfo LastUpdatedField = AccessTools.Field(typeof(Details), "ModDetailsLastUpdated");
        private static readonly FieldInfo ReleaseDateField = AccessTools.Field(typeof(Details), "ModDetailsReleaseDate");
        private static readonly FieldInfo SubscribersField = AccessTools.Field(typeof(Details), "ModDetailsSubscribers");
        private static readonly FieldInfo CreatedByField = AccessTools.Field(typeof(Details), "ModDetailsCreatedBy");
        private static readonly FieldInfo UpVotesField = AccessTools.Field(typeof(Details), "ModDetailsUpVotes");
        private static readonly FieldInfo DownVotesField = AccessTools.Field(typeof(Details), "ModDetailsDownVotes");
        private static readonly FieldInfo UpVoteActiveOverlayField = AccessTools.Field(typeof(Details), "ModDetailsUpVoteActiveOverlay");
        private static readonly FieldInfo DownVoteActiveOverlayField = AccessTools.Field(typeof(Details), "ModDetailsDownVoteActiveOverlay");

        // The tag chips and the text inside each of them, walked at most once a frame: the outer
        // walk found the chips and then walked each chip again for its label, so a panel of twenty
        // tags paid twenty-one subtree walks per build. Keyed on the frame rather than held,
        // because mod.io pools the chips and a different map draws different ones.
        private readonly FrameSweep<ModDetailsTagListItem> _tagItems =
            new FrameSweep<ModDetailsTagListItem>("community maps details tags", inactiveToo: false);
        private readonly FrameSweep<TMP_Text> _tagTexts =
            new FrameSweep<TMP_Text>("community maps details tag text", inactiveToo: false);

        // The static label beside each stat value ("File size:"). The value mesh is instantiated
        // with the panel and keeps its label for the panel's life, so the walk that finds it is
        // paid once per mesh rather than once per detail row per frame.
        private readonly Dictionary<TMP_Text, string> _nearbyLabels = new Dictionary<TMP_Text, string>();

        private readonly Details _details;

        // mod.io's own words for its controls, read once rather than once per row per frame. The
        // browser is instantiated once for the session and only hidden when it closes, so this
        // adapter outlives an options visit; mod.io re-translates its own UI when the language
        // changes under an open browser, so these and the stat labels above are read again when it
        // does (SyncLabelLanguage).
        private ILanguageDefinition _labelLanguage;
        private string _voteUpLabel;
        private string _voteDownLabel;
        private string _reportLabel;
        private string _backLabel;
        private string _downloadsLabel;

        public CommunityMapsDetailsAdapter(Details details)
        {
            _details = details;
            SyncLabelLanguage();
        }

        private void ReadLabels()
        {
            _voteUpLabel = CommunityMapsText.Translate("Vote up");
            _voteDownLabel = CommunityMapsText.Translate("Vote down");
            _reportLabel = CommunityMapsText.Translate("Report");
            _downloadsLabel = CommunityMapsText.Translate("Downloads");
            _backLabel = CommunityMapsText.FindTopBar("Back / Exit");
            if (string.IsNullOrWhiteSpace(_backLabel))
            {
                _backLabel = CommunityMapsText.Translate("Back");
            }
        }

        /// <summary>Read the words above again where the game has changed language since. Asked from
        /// <see cref="IsPresent"/>, which the screen asks every frame before it reads anything.
        /// </summary>
        private void SyncLabelLanguage()
        {
            ILocalizationHandler localization = GlobalLocalizationVariables.LocalizationHandler;
            ILanguageDefinition language = localization != null ? localization.CurrentLanguage : null;
            if (ReferenceEquals(language, _labelLanguage))
            {
                return;
            }

            _labelLanguage = language;
            _nearbyLabels.Clear();
            ReadLabels();
        }

        public bool IsPresent()
        {
            SyncLabelLanguage();
            return Browser.IsOpen
                && _details != null
                && _details.ModDetailsPanel != null
                && _details.ModDetailsPanel.activeInHierarchy;
        }

        public string Title { get { return CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_details, NameField)); } }

        public string Summary { get { return CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_details, SummaryField)); } }

        public string Description { get { return CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_details, DescriptionField)); } }

        public string DescriptionLabel { get { return FindHeaderLabel(Reflect.Cast<TMP_Text>(_details, DescriptionField)); } }

        public string SubscribeLabel { get { return CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_details, SubscribeTextField)); } }

        public string BackLabel { get { return _backLabel; } }

        public string ReportLabel { get { return _reportLabel; } }

        public IReadOnlyList<ActionItem> GetVoteActions()
        {
            List<ActionItem> actions = new List<ActionItem>();
            AddAction(actions, "vote-up", _voteUpLabel, CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_details, UpVotesField)), () => IsVoteUpSelected, RatePositive);
            AddAction(actions, "vote-down", _voteDownLabel, CommunityMapsText.Of(Reflect.Cast<TMP_Text>(_details, DownVotesField)), () => IsVoteDownSelected, RateNegative);
            return actions;
        }

        public IReadOnlyList<DetailItem> GetDetails()
        {
            List<DetailItem> details = new List<DetailItem>();
            AddDetail(details, "file-size", Reflect.Cast<TMP_Text>(_details, FileSizeField));
            AddDetail(details, "last-updated", Reflect.Cast<TMP_Text>(_details, LastUpdatedField));
            AddDetail(details, "release-date", Reflect.Cast<TMP_Text>(_details, ReleaseDateField));
            AddDetail(details, "subscribers", Reflect.Cast<TMP_Text>(_details, SubscribersField));
            AddDetail(details, "created-by", Reflect.Cast<TMP_Text>(_details, CreatedByField));
            return details;
        }

        public IReadOnlyList<TagItem> GetTags()
        {
            List<TagItem> tags = new List<TagItem>();
            if (_details == null)
            {
                return tags;
            }

            ModDetailsTagListItem[] nativeTags = _tagItems.Under(_details);
            for (int i = 0; i < nativeTags.Length; i++)
            {
                TMP_Text[] texts = _tagTexts.Under(nativeTags[i]);
                string label = CommunityMapsText.Of(texts.Length > 0 ? texts[0] : null);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    tags.Add(new TagItem(i, label));
                }
            }

            return tags;
        }

        public bool Subscribe()
        {
            if (_details == null)
            {
                return false;
            }

            _details.SubscribeButtonPress();
            return true;
        }

        public bool Close()
        {
            if (_details == null)
            {
                return false;
            }

            _details.Close();
            return true;
        }

        public bool Report()
        {
            if (_details == null)
            {
                return false;
            }

            EnsureSelectedGameObjectForReport();
            _details.ReportButtonPress();
            return true;
        }

        /// <summary>mod.io's own word for the download queue, as the collection page reads it.
        /// </summary>
        public string DownloadsLabel
        {
            get { return _downloadsLabel; }
        }

        public bool HasDownloadsMenu
        {
            get { return Browser.IsOpen; }
        }

        public bool OpenDownloadsMenu()
        {
            if (!HasDownloadsMenu)
            {
                return false;
            }

            InputReceiver.OnMenu();
            return true;
        }

        private static void AddAction(List<ActionItem> actions, string key, string label, string status, Func<bool> isSelected, Func<bool> activate)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                actions.Add(new ActionItem(key, label, status, isSelected, activate));
            }
        }

        private void AddDetail(List<DetailItem> details, string key, TMP_Text valueText)
        {
            string value = CommunityMapsText.Of(valueText);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string label = GetNearbyLabel(valueText);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            details.Add(new DetailItem(key, label, value));
        }

        private bool RatePositive()
        {
            if (_details == null)
            {
                return false;
            }

            _details.RatePositiveButtonPress();
            return true;
        }

        private bool RateNegative()
        {
            if (_details == null)
            {
                return false;
            }

            _details.RateNegativeButtonPress();
            return true;
        }

        private bool IsVoteUpSelected
        {
            get { return GameObjects.IsLive(Reflect.Cast<GameObject>(_details, UpVoteActiveOverlayField)); }
        }

        private bool IsVoteDownSelected
        {
            get { return GameObjects.IsLive(Reflect.Cast<GameObject>(_details, DownVoteActiveOverlayField)); }
        }

        // LAZY: only from Report(), which mod.io refuses without a selected object. The walk is paid
        // when the player reports a map.
        private void EnsureSelectedGameObjectForReport()
        {
            if (EventSystem.current == null || EventSystem.current.currentSelectedGameObject != null || _details == null)
            {
                return;
            }

            Selectable selectable = _details.GetComponentInChildren<Selectable>(false);
            if (selectable != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable.gameObject);
            }
        }

        private string GetNearbyLabel(TMP_Text valueText)
        {
            if (valueText == null || valueText.transform == null)
            {
                return string.Empty;
            }

            string remembered;
            if (_nearbyLabels.TryGetValue(valueText, out remembered))
            {
                return remembered;
            }

            string label = FindNearbyLabel(valueText);
            if (!string.IsNullOrEmpty(label))
            {
                // Only an answer is remembered: a panel still being built has no label yet, and a
                // remembered blank would outlive the frame that had none.
                _nearbyLabels[valueText] = label;
            }

            return label;
        }

        private string FindNearbyLabel(TMP_Text valueText)
        {
            Transform dynamicTexts = valueText.transform.parent;
            Transform statsRoot = dynamicTexts != null ? dynamicTexts.parent : null;
            if (statsRoot == null)
            {
                return string.Empty;
            }

            string labelObjectName = StripGeneratedSuffix(valueText.transform.name);
            for (int i = 0; i < statsRoot.childCount; i++)
            {
                Transform child = statsRoot.GetChild(i);
                if (child == null
                    || ReferenceEquals(child, dynamicTexts)
                    || child.name != labelObjectName)
                {
                    continue;
                }

                return CommunityMapsText.Of(child.GetComponentInChildren<TMP_Text>(false)).TrimEnd(':');
            }

            return string.Empty;
        }

        private static string FindHeaderLabel(TMP_Text bodyText)
        {
            if (bodyText == null || bodyText.transform == null)
            {
                return string.Empty;
            }

            Transform current = bodyText.transform.parent;
            for (int depth = 0; depth < 4 && current != null; depth++)
            {
                Transform header = current.Find("Header");
                TMP_Text headerText = header != null ? header.GetComponentInChildren<TMP_Text>(false) : null;
                string label = CommunityMapsText.Of(headerText);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label.TrimEnd(':');
                }

                current = current.parent;
            }

            return string.Empty;
        }

        private static string StripGeneratedSuffix(string name)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return string.Empty;
            }

            return name.EndsWith(" (1)", StringComparison.Ordinal)
                ? name.Substring(0, name.Length - 4)
                : name;
        }

        public sealed class ActionItem
        {
            private readonly Func<bool> _isSelected;

            public ActionItem(string key, string label, string status, Func<bool> isSelected, Func<bool> activate)
            {
                Key = key ?? string.Empty;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                _isSelected = isSelected;
                Activate = activate;
            }

            public string Key { get; private set; }
            public string Label { get; private set; }
            public string Status { get; private set; }
            public bool IsSelected { get { return _isSelected != null && _isSelected(); } }
            public Func<bool> Activate { get; private set; }
        }

        public sealed class DetailItem
        {
            public DetailItem(string key, string label, string value)
            {
                Key = key ?? string.Empty;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string Key { get; private set; }
            public string Label { get; private set; }
            public string Value { get; private set; }
        }

        public sealed class TagItem
        {
            public TagItem(int index, string label)
            {
                Index = index;
                Label = label ?? string.Empty;
            }

            public int Index { get; private set; }
            public string Label { get; private set; }
        }
    }
}
