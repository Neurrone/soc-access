using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.WorldMenuComponents;
using SongsOfConquest.Client.Menu;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class WorldConfirmMenuAdapter : IPresent
    {
        private static readonly FieldInfo SettingsField = AccessTools.Field(typeof(WorldConfirmMenu), "_settings");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(WorldConfirmMenu), "_async");
        private static readonly FieldInfo CostEntryPoolField = AccessTools.Field(typeof(WorldConfirmMenu), "_costEntryPool");
        private static readonly FieldInfo IconsField = AccessTools.Field(typeof(WorldConfirmMenu), "_icons");

        private readonly WorldConfirmMenu _menu;
        private readonly WorldConfirmMenu.Settings _settings;

        public WorldConfirmMenuAdapter(WorldConfirmMenu menu)
        {
            _menu = menu;
            _settings = Reflect.Get<WorldConfirmMenu.Settings>(menu, SettingsField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public string Title
        {
            get { return UITextMeshTextUtility.Spoken(_settings != null ? _settings.HeaderText : null); }
        }

        public string Body
        {
            get { return string.Join(" ", BodyLines); }
        }

        /// <summary>The paragraphs the game broke the body into, kept apart rather than collapsed:
        /// the menu reads a paragraph at a time.</summary>
        public IList<string> BodyLines
        {
            get
            {
                return SpokenLines.Of(new[]
                {
                    UITextMeshTextUtility.GetEffectiveText(_settings != null ? _settings.BodyText : null),
                });
            }
        }

        public string ConfirmLabel
        {
            get { return GetButtonText(_settings != null ? _settings.OkButton : null); }
        }

        public string CancelLabel
        {
            get { return GetButtonText(_settings != null ? _settings.CancelButton : null); }
        }

        /// <summary>The component the game draws the confirm button with, or null where there is
        /// none - where it sits on the screen and what selects it are game facts.</summary>
        public Component ConfirmButton
        {
            get { return _settings != null ? _settings.OkButton : null; }
        }

        /// <summary>The component the game draws the cancel button with, or null where there is
        /// none.</summary>
        public Component CancelButton
        {
            get { return _settings != null ? _settings.CancelButton : null; }
        }

        /// <summary>The text mesh inside the warning the menu shows when the player cannot afford the
        /// cost (<c>WorldConfirmMenu.Setup</c> turns the warning object on and the OK button off
        /// together), or null while the game is not drawing it.</summary>
        public UITextMesh ResourceWarning
        {
            get
            {
                GameObject warning = _settings != null ? _settings.ResourceWarning : null;
                if (warning == null || !warning.activeInHierarchy)
                {
                    return null;
                }

                // Which mesh the warning draws on is fixed; whether it is drawn is not, so the
                // search runs once and the answer is checked live.
                if (!_warningProbed)
                {
                    _warningProbed = true;
                    _warningText = warning.GetComponentInChildren<UITextMesh>(true);
                }

                return _warningText != null && _warningText.gameObject.activeInHierarchy
                    ? _warningText
                    : null;
            }
        }

        private UITextMesh _warningText;
        private bool _warningProbed;

        /// <summary>The wording the warning draws, with the game's rich-text tags taken off.</summary>
        public string ResourceWarningLabel
        {
            get
            {
                return string.Join(
                    " ",
                    SpokenLines.Of(new[] { UITextMeshTextUtility.GetEffectiveText(ResourceWarning) }));
            }
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

        /// <summary>The cost lines the menu is drawing. The menu keeps no <c>Cost</c> of its own:
        /// <c>WorldConfirmMenu.Setup</c> spends the one it was shown with on the pooled entries and
        /// lets it go, so the entries the pool has out are what the menu was asked for
        /// (<c>WorldConfirmMenu.cs</c> lines 124 to 159). Each entry draws the amount and leaves the
        /// resource to its icon, so the icon is named back into words here.</summary>
        public IReadOnlyList<string> GetCostLabels()
        {
            IUIPool<IWorldMenuIconTextEntry> pool = Reflect.Get<IUIPool<IWorldMenuIconTextEntry>>(_menu, CostEntryPoolField);
            List<string> labels = new List<string>();
            if (pool == null || pool.ActiveItems == null)
            {
                return labels;
            }

            for (int i = 0; i < pool.ActiveItems.Count; i++)
            {
                IWorldMenuIconTextEntry entry = pool.ActiveItems[i];
                string amount = NormalizeCostText(UITextMeshTextUtility.Spoken(entry != null ? entry.TypeTextMesh : null));
                if (!string.IsNullOrWhiteSpace(amount))
                {
                    labels.Add(WithResourceName(entry, amount));
                }
            }

            return labels;
        }

        /// <summary>The amount the entry draws with the resource it is for, or the amount alone where
        /// the icon is not one the manifest knows.</summary>
        private string WithResourceName(IWorldMenuIconTextEntry entry, string amount)
        {
            Image icon = entry != null ? entry.Icon : null;
            Sprite sprite = icon != null ? icon.sprite : null;
            ResourceType type;
            if (sprite == null || !ResourcesBySprite.TryGetValue(sprite, out type))
            {
                return amount;
            }

            string name = GetResourceName(type, ParseAmount(amount));
            return string.IsNullOrWhiteSpace(name) ? amount : NormalizeCostText(amount + " " + name);
        }

        /// <summary>Which resource each drawn icon stands for. <c>WorldConfirmMenu.Setup</c> gives the
        /// entry its sprite from <c>IMenuIconFactory.GetResourceIcon</c> (<c>WorldConfirmMenu.cs</c>
        /// line 137, factory field <c>_icons</c> at line 52) and keeps nothing else of the cost, so
        /// the sprite is what is left to name it by. The manifest answers each type out of a
        /// serialized table and completes on the spot (<c>MenuIconManifest.cs</c> lines 64 to 73), so
        /// the whole enum is asked ONCE, on the first read of the costs, and the answer is kept for
        /// the life of the adapter.</summary>
        private Dictionary<Sprite, ResourceType> ResourcesBySprite
        {
            get
            {
                if (_resourcesBySprite != null)
                {
                    return _resourcesBySprite;
                }

                _resourcesBySprite = new Dictionary<Sprite, ResourceType>();
                IMenuIconFactory icons = Reflect.Get<IMenuIconFactory>(_menu, IconsField);
                if (icons == null)
                {
                    return _resourcesBySprite;
                }

                foreach (ResourceType type in Enum.GetValues(typeof(ResourceType)))
                {
                    Async<Sprite> icon = icons.GetResourceIcon(type);
                    Sprite sprite = icon != null && icon.IsCompleted ? icon.Result : null;
                    if (sprite != null && !_resourcesBySprite.ContainsKey(sprite))
                    {
                        _resourcesBySprite[sprite] = type;
                    }
                }

                return _resourcesBySprite;
            }
        }

        private Dictionary<Sprite, ResourceType> _resourcesBySprite;

        /// <summary>The game's own name for the resource, in the plural form the amount asks for -
        /// the phrasing every other cost line in the game is built from
        /// (<c>WorldChoiceMenu.cs</c> line 743).</summary>
        private static string GetResourceName(ResourceType type, int amount)
        {
            if (GlobalLocalizationVariables.LocalizationHandler == null)
            {
                return type.ToString();
            }

            return GlobalLocalizationVariables.LocalizationHandler.GetPluralText(
                new Resource(type, amount).GetLocalizationKey(),
                amount);
        }

        /// <summary>The number out of the drawn amount ("-30"), for the plural form.</summary>
        private static int ParseAmount(string amount)
        {
            System.Text.RegularExpressions.Match match =
                System.Text.RegularExpressions.Regex.Match(amount ?? string.Empty, @"\d+");
            int value;
            return match.Success && int.TryParse(match.Value, out value) ? value : 0;
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
                SpokenLines.Clean(text),
                @"-\s+(\d)",
                "-$1");
        }
    }
}
