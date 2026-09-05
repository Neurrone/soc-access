using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client.Adventure.Menu.Lobby;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Ai;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class AdventureLobbyIconDropdownAdapter
    {
        private static readonly FieldInfo MainContainerField = AccessTools.Field(typeof(IconDropdown), "_mainContainer");
        private static readonly FieldInfo SpawnedEntriesField = AccessTools.Field(typeof(IconDropdown), "_spawnedEntries");
        private static readonly FieldInfo EntryContainerField = AccessTools.Field(typeof(IconDropdown), "_entryContainer");
        private static readonly FieldInfo EntryTypeField = AccessTools.Field(typeof(IconDropdownEntry), "_type");
        private static readonly FieldInfo EntryReturnColorField = AccessTools.Field(typeof(IconDropdownEntry), "_returnColor");
        private static readonly FieldInfo EntryFactionIdField = AccessTools.Field(typeof(IconDropdownEntry), "_factionId");
        private static readonly FieldInfo EntryWielderRefField = AccessTools.Field(typeof(IconDropdownEntry), "_wielderRef");
        private static readonly FieldInfo EntryAiDifficultyField = AccessTools.Field(typeof(IconDropdownEntry), "_aiDifficulty");
        private static readonly FieldInfo EntryPartnershipTextField = AccessTools.Field(typeof(IconDropdownEntry), "_partnershipText");
        private static readonly FieldInfo EntryFactionLookupField = AccessTools.Field(typeof(IconDropdownEntry), "_factionLookup");
        private static readonly FieldInfo EntryWielderLookupField = AccessTools.Field(typeof(IconDropdownEntry), "_wielderLookup");
        private static readonly FieldInfo EntryLocalizationField = AccessTools.Field(typeof(IconDropdownEntry), "_localization");
        private static readonly FieldInfo DropdownLocalizationField = AccessTools.Field(typeof(IconDropdown), "_localizationHandler");

        private readonly IconDropdown _dropdown;

        public AdventureLobbyIconDropdownAdapter(IconDropdown dropdown)
        {
            _dropdown = dropdown;
        }

        public object SourceKey
        {
            get { return _dropdown; }
        }

        public bool IsPresent()
        {
            GameObject container = GetMainContainer();
            return _dropdown != null
                && IsLiveSceneObject(((Component)_dropdown).gameObject)
                && container != null
                && container.activeInHierarchy;
        }

        public string Title
        {
            get { return GetTitleFromFirstEntry(); }
        }

        public string CancelLabel
        {
            get { return SpeechTextSanitizer.Normalize(GameText.Get(GetDropdownLocalization(), "Common/Cancel", string.Empty)); }
        }

        public IReadOnlyList<OptionItem> GetOptions()
        {
            List<OptionItem> items = new List<OptionItem>();
            IReadOnlyList<IconDropdownEntry> entries = GetSpawnedEntries();
            for (int i = 0; i < entries.Count; i++)
            {
                IconDropdownEntry entry = entries[i];
                if (entry != null && IsVisible(entry as Component))
                {
                    items.Add(new OptionItem(this, entry, i));
                }
            }

            return items;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public bool Cancel()
        {
            if (_dropdown == null || !IsPresent())
            {
                return false;
            }

            _dropdown.Hide();
            return true;
        }

        private string GetTitleFromFirstEntry()
        {
            IReadOnlyList<OptionItem> options = GetOptions();
            if (options.Count == 0)
            {
                return string.Empty;
            }

            string type = options[0].TypeName;
            string title = string.Empty;
            ILocalizationHandler localization = GetLocalization(options[0].Entry);
            switch (type)
            {
                case "Color":
                    title = GameText.Get(localization, "Lobby/LobbyPlayerMenu/SetColor", string.Empty);
                    break;
                case "Faction":
                    title = GameText.Get(localization, "Adventure/TeamQueueHUD/Faction", string.Empty);
                    break;
                case "Wielder":
                    title = GameText.Get(localization, "Lobby/LobbyPlayerMenu/SetStartingWielder", string.Empty);
                    break;
                case "AiMode":
                case "QuickPlayAiMode":
                    title = GameText.Get(localization, "Lobby/LobbyPlayerMenu/SetAiDifficulty", string.Empty);
                    break;
                case "Partnership":
                    title = GameText.Get(localization, "Lobby/LobbyPlayerMenu/Coop", string.Empty);
                    break;
                default:
                    break;
            }

            title = SpeechTextSanitizer.Normalize(title);
            if (!string.IsNullOrWhiteSpace(title))
            {
                return title;
            }

            return options[0].FirstTooltipLine;
        }

        private GameObject GetMainContainer()
        {
            return _dropdown != null && MainContainerField != null
                ? MainContainerField.GetValue(_dropdown) as GameObject
                : null;
        }

        private IReadOnlyList<IconDropdownEntry> GetSpawnedEntries()
        {
            List<IconDropdownEntry> entries = _dropdown != null && SpawnedEntriesField != null
                ? SpawnedEntriesField.GetValue(_dropdown) as List<IconDropdownEntry>
                : null;
            if (entries != null)
            {
                return entries;
            }

            List<IconDropdownEntry> discovered = new List<IconDropdownEntry>();
            Transform container = _dropdown != null && EntryContainerField != null
                ? EntryContainerField.GetValue(_dropdown) as Transform
                : null;
            if (container == null)
            {
                return discovered;
            }

            foreach (Transform child in container)
            {
                IconDropdownEntry entry = child != null ? child.GetComponent<IconDropdownEntry>() : null;
                if (entry != null)
                {
                    discovered.Add(entry);
                }
            }

            return discovered;
        }

        private ILocalizationHandler GetLocalization(IconDropdownEntry entry)
        {
            ILocalizationHandler localization = entry != null && EntryLocalizationField != null
                ? EntryLocalizationField.GetValue(entry) as ILocalizationHandler
                : null;
            if (localization != null)
            {
                return localization;
            }

            return GetDropdownLocalization();
        }

        private ILocalizationHandler GetDropdownLocalization()
        {
            return _dropdown != null && DropdownLocalizationField != null
                ? DropdownLocalizationField.GetValue(_dropdown) as ILocalizationHandler
                : null;
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsLiveSceneObject(GameObject gameObject)
        {
            return gameObject != null && gameObject.scene.IsValid() && gameObject.scene.isLoaded;
        }

        public sealed class OptionItem
        {
            private readonly AdventureLobbyIconDropdownAdapter _adapter;
            private readonly IconDropdownEntry _entry;
            private readonly int _index;

            public OptionItem(AdventureLobbyIconDropdownAdapter adapter, IconDropdownEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                _index = index;
            }

            public IconDropdownEntry Entry
            {
                get { return _entry; }
            }

            public string Id
            {
                get { return "icon-dropdown-option-" + _index; }
            }

            public string TypeName
            {
                get
                {
                    object value = _entry != null && EntryTypeField != null ? EntryTypeField.GetValue(_entry) : null;
                    return value != null ? value.ToString() : string.Empty;
                }
            }

            public string Label
            {
                get { return GetLabel(); }
            }

            public string FirstTooltipLine
            {
                get
                {
                    IReadOnlyList<string> lines = TooltipLines;
                    return lines.Count > 0 ? lines[0] : string.Empty;
                }
            }

            public bool IsVisible
            {
                get { return AdventureLobbyIconDropdownAdapter.IsVisible(_entry as Component); }
            }

            public bool IsEnabled
            {
                get { return _entry != null && _entry.Button != null && _entry.Button.Interactable; }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(_entry != null ? _entry.Button as Component : null, _adapter.GetLocalization(_entry)); }
            }

            private IReadOnlyList<string> TooltipLines
            {
                get
                {
                    Tooltip tooltip = Tooltip;
                    return tooltip != null && tooltip.TextLines != null ? tooltip.TextLines : new string[0];
                }
            }

            public void FocusNative()
            {
                if (_entry != null && _entry.Button != null)
                {
                    NativeSelectionUtility.Select(_entry.Button);
                }
            }

            public bool Activate()
            {
                return _entry != null && NativeSelectionUtility.Click(_entry.Button);
            }

            private string GetLabel()
            {
                switch (TypeName)
                {
                    case "Color":
                        return GetColorLabel();
                    case "Faction":
                        return GetFactionLabel();
                    case "Wielder":
                        return GetWielderLabel();
                    case "AiMode":
                        return GetAiDifficultyLabel();
                    case "QuickPlayAiMode":
                        return GetQuickPlayAiModeLabel();
                    case "Partnership":
                        return GetPartnershipLabel();
                    default:
                        return string.Empty;
                }
            }

            private string GetColorLabel()
            {
                int color = GetFieldValue(EntryReturnColorField, -1);
                if (color < 0)
                {
                    return string.Empty;
                }

                TeamColor teamColor = TeamColorExtensions.GetTeamColorFromIndex(color);
                return TeamColorText.Get(teamColor);
            }

            private string GetFactionLabel()
            {
                int factionId = GetFieldValue(EntryFactionIdField, -1);
                if (factionId == 99)
                {
                    return Localize("Factions/Random/Name");
                }

                IFactionLookup lookup = _entry != null && EntryFactionLookupField != null
                    ? EntryFactionLookupField.GetValue(_entry) as IFactionLookup
                    : null;
                IFactionDefinition faction = lookup != null ? lookup.GetFaction(factionId) : null;
                return faction != null ? Localize(faction.NameKey) : string.Empty;
            }

            private string GetWielderLabel()
            {
                CommanderReference reference = GetFieldValue(EntryWielderRefField, CommanderReference.Random);
                if (reference == CommanderReference.Random)
                {
                    return Localize("Factions/Random/Name");
                }

                IWielderLookup lookup = _entry != null && EntryWielderLookupField != null
                    ? EntryWielderLookupField.GetValue(_entry) as IWielderLookup
                    : null;
                ICommanderDefinition wielder = lookup != null ? lookup.Get(reference) : null;
                return wielder != null ? Localize(wielder.NameKey) : string.Empty;
            }

            private string GetAiDifficultyLabel()
            {
                AiDifficulty difficulty = GetFieldValue(EntryAiDifficultyField, AiDifficulty.Worthy);
                string label = Localize("Common/AiMode/" + difficulty);
                return !string.IsNullOrWhiteSpace(label) ? label : LastTooltipLine();
            }

            private string GetQuickPlayAiModeLabel()
            {
                return LastTooltipLine();
            }

            private string LastTooltipLine()
            {
                Tooltip tooltip = Tooltip;
                if (tooltip == null)
                {
                    return string.Empty;
                }

                IReadOnlyList<string> lines = tooltip.TextLines;
                return lines.Count > 0 ? lines[lines.Count - 1] : string.Empty;
            }

            private string GetPartnershipLabel()
            {
                UITextMesh text = _entry != null && EntryPartnershipTextField != null
                    ? EntryPartnershipTextField.GetValue(_entry) as UITextMesh
                    : null;
                return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(text));
            }

            private string Localize(string key)
            {
                if (string.IsNullOrWhiteSpace(key))
                {
                    return string.Empty;
                }

                return SpeechTextSanitizer.Normalize(GameText.Get(_adapter.GetLocalization(_entry), key, string.Empty));
            }

            private T GetFieldValue<T>(FieldInfo field, T fallback)
            {
                if (_entry == null || field == null)
                {
                    return fallback;
                }

                object value = field.GetValue(_entry);
                return value is T ? (T)value : fallback;
            }
        }
    }
}
