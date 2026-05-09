using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DefencePanelWielderAdapter
    {
        private static readonly FieldInfo StoredCommanderField = AccessTools.Field(typeof(DefencePanelWielder), "_storedCommander");
        private static readonly FieldInfo NoStoredWielderContainerField = AccessTools.Field(typeof(DefencePanelWielder), "_noStoredWielderContainer");
        private static readonly FieldInfo StoredWielderContainerField = AccessTools.Field(typeof(DefencePanelWielder), "_storedWielderContainer");
        private static readonly FieldInfo StoreButtonField = AccessTools.Field(typeof(DefencePanelWielder), "_storeButton");
        private static readonly FieldInfo EjectButtonField = AccessTools.Field(typeof(DefencePanelWielder), "_ejectButton");
        private static readonly FieldInfo TradeButtonField = AccessTools.Field(typeof(DefencePanelWielder), "_tradeButton");
        private static readonly FieldInfo PortraitImageField = AccessTools.Field(typeof(DefencePanelWielder), "_portraitImage");
        private static readonly FieldInfo TroopHudField = AccessTools.Field(typeof(DefencePanelWielder), "_troopHUD");

        private readonly DefencePanelWielder _panel;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public DefencePanelWielderAdapter(DefencePanelWielder panel, IClientAdventureFacade facade, ILocalizationHandler localization)
        {
            _panel = panel;
            _facade = facade;
            _localization = localization;
        }

        public bool IsPresent
        {
            get { return IsVisible(_panel as Component); }
        }

        public bool IsStoredWielderVisible
        {
            get { return IsVisible(GetField<GameObject>(_panel, StoredWielderContainerField)); }
        }

        public string StoredWielderName
        {
            get
            {
                ICommanderState storedCommander = StoredCommander;
                string name = storedCommander != null && _facade != null
                    ? _facade.Commanders.GetName(storedCommander.Id)
                    : string.Empty;
                return SpeechTextSanitizer.Normalize(name);
            }
        }

        public int StoredWielderId
        {
            get
            {
                ICommanderState storedCommander = StoredCommander;
                return storedCommander != null ? storedCommander.Id : -1;
            }
        }

        public string Status
        {
            get
            {
                string name = StoredWielderName;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return "Defending wielder: " + name;
                }

                return GetVisibleText(GetField<GameObject>(_panel, NoStoredWielderContainerField));
            }
        }

        public Tooltip PortraitTooltip
        {
            get { return Tooltip.ForComponent(GetField<Component>(_panel, PortraitImageField), _localization); }
        }

        public void FocusPortrait()
        {
            NativeSelectionUtility.Select(GetField<Component>(_panel, PortraitImageField));
        }

        public TroopHudAdapter Troops
        {
            get { return new TroopHudAdapter(GetField<TroopHUD>(_panel, TroopHudField), _facade, _localization, BuildArmyLabel()); }
        }

        public string ArmyLabel
        {
            get { return BuildArmyLabel(); }
        }

        public string StoreLabel
        {
            get { return GetButtonLabel(GetStoreButton(), "GameActions/Adventure/StoreCommander", "Store Wielder"); }
        }

        public string EjectLabel
        {
            get { return GetButtonLabel(GetEjectButton(), "GameActions/Adventure/EjectCommander", "Eject Wielder"); }
        }

        public string TradeLabel
        {
            get { return GetButtonLabel(GetTradeButton(), "Adventure/TooltipInstruction/Trade", "Trade"); }
        }

        public bool IsStoreVisible()
        {
            return IsVisible(GetStoreButton() as Component);
        }

        public bool IsEjectVisible()
        {
            return IsVisible(GetEjectButton() as Component);
        }

        public bool IsTradeVisible()
        {
            return IsVisible(GetTradeButton() as Component);
        }

        public bool IsStoreEnabled()
        {
            return IsButtonEnabled(GetStoreButton());
        }

        public bool IsEjectEnabled()
        {
            return IsButtonEnabled(GetEjectButton());
        }

        public bool IsTradeEnabled()
        {
            return IsButtonEnabled(GetTradeButton());
        }

        public Tooltip StoreTooltip
        {
            get { return Tooltip.ForComponent(GetStoreButton() as Component, _localization); }
        }

        public Tooltip EjectTooltip
        {
            get { return Tooltip.ForComponent(GetEjectButton() as Component, _localization); }
        }

        public Tooltip TradeTooltip
        {
            get { return Tooltip.ForComponent(GetTradeButton() as Component, _localization); }
        }

        public bool ActivateStore()
        {
            return NativeSelectionUtility.Click(GetStoreButton());
        }

        public bool ActivateEject()
        {
            return NativeSelectionUtility.Click(GetEjectButton());
        }

        public bool ActivateTrade()
        {
            return NativeSelectionUtility.Click(GetTradeButton());
        }

        public void FocusStore()
        {
            NativeSelectionUtility.Select(GetStoreButton());
        }

        public void FocusEject()
        {
            NativeSelectionUtility.Select(GetEjectButton());
        }

        public void FocusTrade()
        {
            NativeSelectionUtility.Select(GetTradeButton());
        }

        private ICommanderState StoredCommander
        {
            get { return GetField<ICommanderState>(_panel, StoredCommanderField); }
        }

        private UIButton GetStoreButton()
        {
            return GetField<UIButton>(_panel, StoreButtonField);
        }

        private UIButton GetEjectButton()
        {
            return GetField<UIButton>(_panel, EjectButtonField);
        }

        private UIButton GetTradeButton()
        {
            return GetField<UIButton>(_panel, TradeButtonField);
        }

        private string BuildArmyLabel()
        {
            string name = StoredWielderName;
            return string.IsNullOrWhiteSpace(name) ? "defending wielder army" : name + "'s army";
        }

        private string GetButtonLabel(UIButton button, string localizationKey, string fallback)
        {
            string label = SpeechTextSanitizer.Normalize(MenuButtonTextUtility.GetAllVisibleText(button));
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            return GetLocalizedText(localizationKey, fallback);
        }

        private string GetLocalizedText(string key, string fallback)
        {
            string text = _localization != null ? _localization.GetText(key) : string.Empty;
            return string.IsNullOrWhiteSpace(text) || text == key ? fallback : SpeechTextSanitizer.Normalize(text);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static string GetVisibleText(GameObject root)
        {
            if (root == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            UITextMesh[] textMeshes = root.GetComponentsInChildren<UITextMesh>(includeInactive: false);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                string text = GetText(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            return string.Join(". ", parts.ToArray());
        }

        private static bool IsButtonEnabled(UIButton button)
        {
            return button != null && button.Active && button.Interactable && IsVisible(button as Component);
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }
    }
}
