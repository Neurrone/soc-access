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
        private readonly string _voteUpLabel;
        private readonly string _voteDownLabel;
        private readonly string _reportLabel;
        private readonly string _backLabel;
        private readonly string _downloadsLabel;

        public CommunityMapsDetailsAdapter(Details details)
        {
            _details = details;
            _voteUpLabel = Translate("Vote up");
            _voteDownLabel = Translate("Vote down");
            _reportLabel = Translate("Report");
            _downloadsLabel = Translate("Downloads");
            _backLabel = FindTopBarText("Back / Exit");
            if (string.IsNullOrWhiteSpace(_backLabel))
            {
                _backLabel = Translate("Back");
            }
        }

        public bool IsPresent()
        {
            return Browser.IsOpen
                && _details != null
                && _details.ModDetailsPanel != null
                && _details.ModDetailsPanel.activeInHierarchy;
        }

        public string Title { get { return GetText(GetField<TMP_Text>(NameField)); } }

        public string Summary { get { return GetText(GetField<TMP_Text>(SummaryField)); } }

        public string Description { get { return GetText(GetField<TMP_Text>(DescriptionField)); } }

        public string DescriptionLabel { get { return FindHeaderLabel(GetField<TMP_Text>(DescriptionField)); } }

        public string SubscribeLabel { get { return GetText(GetField<TMP_Text>(SubscribeTextField)); } }

        public string BackLabel { get { return _backLabel; } }

        public string ReportLabel { get { return _reportLabel; } }

        public IReadOnlyList<ActionItem> GetVoteActions()
        {
            List<ActionItem> actions = new List<ActionItem>();
            AddAction(actions, "vote-up", _voteUpLabel, GetText(GetField<TMP_Text>(UpVotesField)), () => IsVoteUpSelected, RatePositive);
            AddAction(actions, "vote-down", _voteDownLabel, GetText(GetField<TMP_Text>(DownVotesField)), () => IsVoteDownSelected, RateNegative);
            return actions;
        }

        public IReadOnlyList<DetailItem> GetDetails()
        {
            List<DetailItem> details = new List<DetailItem>();
            AddDetail(details, "file-size", GetField<TMP_Text>(FileSizeField));
            AddDetail(details, "last-updated", GetField<TMP_Text>(LastUpdatedField));
            AddDetail(details, "release-date", GetField<TMP_Text>(ReleaseDateField));
            AddDetail(details, "subscribers", GetField<TMP_Text>(SubscribersField));
            AddDetail(details, "created-by", GetField<TMP_Text>(CreatedByField));
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
                string label = GetText(texts.Length > 0 ? texts[0] : null);
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

        public string Translate(string key)
        {
            TranslationManager manager = CommunityMapsSources.Translations;
            if (manager == null || string.IsNullOrWhiteSpace(key))
            {
                return key ?? string.Empty;
            }

            return SpokenLines.Clean(manager.Get(key));
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

        private static void AddAction(List<ActionItem> actions, string id, string label, string status, Func<bool> isSelected, Func<bool> activate)
        {
            if (!string.IsNullOrWhiteSpace(label))
            {
                actions.Add(new ActionItem(id, label, status, isSelected, activate));
            }
        }

        private void AddDetail(List<DetailItem> details, string id, TMP_Text valueText)
        {
            string value = GetText(valueText);
            if (string.IsNullOrWhiteSpace(value))
            {
                return;
            }

            string label = GetNearbyLabel(valueText);
            if (string.IsNullOrWhiteSpace(label))
            {
                return;
            }

            details.Add(new DetailItem(id, label, value));
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
            get { return IsActive(GetField<GameObject>(UpVoteActiveOverlayField)); }
        }

        private bool IsVoteDownSelected
        {
            get { return IsActive(GetField<GameObject>(DownVoteActiveOverlayField)); }
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

                return GetText(child.GetComponentInChildren<TMP_Text>(false)).TrimEnd(':');
            }

            return string.Empty;
        }

        private static string FindTopBarText(string transformName)
        {
            if (string.IsNullOrWhiteSpace(transformName))
            {
                return string.Empty;
            }

            NavBar navBar = CommunityMapsSources.NavBar;
            TMP_Text[] texts = navBar != null ? navBar.GetComponentsInChildren<TMP_Text>(false) : null;
            if (texts != null)
            {
                for (int i = 0; i < texts.Length; i++)
                {
                    TMP_Text text = texts[i];
                    if (text == null || text.transform.parent == null || text.transform.parent.name != transformName)
                    {
                        continue;
                    }

                    string value = GetText(text);
                    if (!string.IsNullOrWhiteSpace(value))
                    {
                        return value;
                    }
                }
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
                string label = GetText(headerText);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return label.TrimEnd(':');
                }

                current = current.parent;
            }

            return string.Empty;
        }

        private T GetField<T>(FieldInfo field)
        {
            return field != null && _details != null ? (T)field.GetValue(_details) : default(T);
        }

        private static bool IsActive(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static string GetText(TMP_Text text)
        {
            return text != null && text.gameObject.activeInHierarchy
                ? SpokenLines.Clean(text.text)
                : string.Empty;
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

            public ActionItem(string id, string label, string status, Func<bool> isSelected, Func<bool> activate)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Status = status ?? string.Empty;
                _isSelected = isSelected;
                Activate = activate;
            }

            public string Id { get; private set; }
            public string Label { get; private set; }
            public string Status { get; private set; }
            public bool IsSelected { get { return _isSelected != null && _isSelected(); } }
            public Func<bool> Activate { get; private set; }
        }

        public sealed class DetailItem
        {
            public DetailItem(string id, string label, string value)
            {
                Id = id ?? string.Empty;
                Label = label ?? string.Empty;
                Value = value ?? string.Empty;
            }

            public string Id { get; private set; }
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
