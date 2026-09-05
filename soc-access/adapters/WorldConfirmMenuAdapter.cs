using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.WorldMenuComponents;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class WorldConfirmMenuAdapter
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(WorldConfirmMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldConfirmMenu), "_async");
        private static readonly FieldInfo CostEntryPoolField = AccessTools.Field(typeof(WorldConfirmMenu), "_costEntryPool");

        private readonly WorldConfirmMenu _menu;
        private readonly WorldConfirmMenu.Settings _settings;

        public WorldConfirmMenuAdapter(WorldConfirmMenu menu)
        {
            _menu = menu;
            _settings = GetField<WorldConfirmMenu.Settings>(menu, SettingsField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return GetText(_settings != null ? _settings.HeaderText : null); }
        }

        public string Body
        {
            get { return GetText(_settings != null ? _settings.BodyText : null); }
        }

        public string ConfirmLabel
        {
            get { return GetButtonText(_settings != null ? _settings.OkButton : null); }
        }

        public string CancelLabel
        {
            get { return GetButtonText(_settings != null ? _settings.CancelButton : null); }
        }

        public bool IsPresent()
        {
            return _menu != null
                && _settings != null
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null;
        }

        public bool IsConfirmEnabled()
        {
            return _settings != null
                && _settings.OkButton != null
                && _settings.OkButton.Interactable;
        }

        public bool ActivateConfirm()
        {
            return IsConfirmEnabled() && NativeSelectionUtility.Click(_settings.OkButton);
        }

        public bool ActivateCancel()
        {
            return _settings != null
                && _settings.CancelButton != null
                && NativeSelectionUtility.Click(_settings.CancelButton);
        }

        public IReadOnlyList<string> GetCostLabels()
        {
            Cost cost = WorldConfirmMenuPatches.GetCost(_menu);
            if (cost != null && cost.SortedCostEntries.Count > 0)
            {
                List<string> labels = new List<string>(cost.SortedCostEntries.Count);
                foreach (Cost.CostEntry entry in cost.SortedCostEntries)
                {
                    labels.Add(BuildCostLabel(entry));
                }

                return labels;
            }

            return GetVisibleCostEntryLabels();
        }

        public void ClearNativeSelection()
        {
            NativeSelectionUtility.Select((UnityEngine.GameObject)null);
        }

        private List<string> GetVisibleCostEntryLabels()
        {
            IUIPool<IWorldMenuIconTextEntry> pool = GetField<IUIPool<IWorldMenuIconTextEntry>>(_menu, CostEntryPoolField);
            List<string> labels = new List<string>();
            if (pool == null || pool.ActiveItems == null)
            {
                return labels;
            }

            for (int i = 0; i < pool.ActiveItems.Count; i++)
            {
                IWorldMenuIconTextEntry entry = pool.ActiveItems[i];
                string label = NormalizeCostText(GetText(entry != null ? entry.TypeTextMesh : null));
                if (!string.IsNullOrWhiteSpace(label))
                {
                    labels.Add(label);
                }
            }

            return labels;
        }

        private static string BuildCostLabel(Cost.CostEntry entry)
        {
            string resourceName = string.Empty;
            if (GlobalLocalizationVariables.LocalizationHandler != null)
            {
                Resource resource = new Resource(entry.Type, entry.Amount);
                resourceName = GlobalLocalizationVariables.LocalizationHandler.GetPluralText(resource.GetLocalizationKey(), entry.Amount);
            }
            else
            {
                resourceName = entry.Type.ToString();
            }

            return NormalizeCostText("-" + entry.Amount + " " + resourceName);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetButtonText(UIButton button)
        {
            return MenuButtonTextUtility.GetStandardButtonLabel(button);
        }

        private static string NormalizeCostText(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return string.Empty;
            }

            return System.Text.RegularExpressions.Regex.Replace(
                SpeechTextSanitizer.Normalize(text),
                @"-\s+(\d)",
                "-$1");
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }
    }
}
