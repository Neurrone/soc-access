using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class DwellingInteractionMenuAdapter
    {
        private static readonly FieldInfo WielderInteractHeaderField = AccessTools.Field(typeof(DwellingInteractionMenu), "_wielderInteractHeader");
        private static readonly FieldInfo BuildingNameField = AccessTools.Field(typeof(DwellingInteractionMenu), "_buildingName");
        private static readonly FieldInfo PurchaseTroopsSubMenuField = AccessTools.Field(typeof(DwellingInteractionMenu), "_purchaseTroopsSubMenu");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(DwellingInteractionMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(DwellingInteractionMenu), "_localizationHandler");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(DwellingInteractionMenu), "_async");
        private static readonly FieldInfo InteractingCommanderIdField = AccessTools.Field(typeof(DwellingInteractionMenu), "_interactingCommanderId");

        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");
        private static readonly FieldInfo HeaderPortraitField = AccessTools.Field(typeof(WielderInteractHeader), "_wielderPortrait");

        private static readonly FieldInfo UpgradeTroopsSubMenuField = AccessTools.Field(typeof(DwellingInteractionMenu), "_upgradeTroopsSubMenu");

        private readonly DwellingInteractionMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public DwellingInteractionMenuAdapter(DwellingInteractionMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(_menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(_menu, LocalizationField);
        }

        public object SourceKey
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public string Title
        {
            get { return GetText(GetField<UITextMesh>(_menu, BuildingNameField)); }
        }

        public string WielderName
        {
            get
            {
                int commanderId = GetInteractingCommanderId();
                string name = _facade != null && _facade.Commanders != null && commanderId >= 0
                    ? _facade.Commanders.GetName(commanderId)
                    : string.Empty;
                return string.IsNullOrWhiteSpace(name) ? "Wielder" : SpeechTextSanitizer.Normalize(name);
            }
        }

        public Tooltip WielderTooltip
        {
            get { return Tooltip.ForComponent(GetWielderPortrait(), _localization); }
        }

        public bool IsPresent()
        {
            PurchaseTroopsSubMenu subMenu = GetPurchaseTroopsSubMenu();
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && subMenu != null
                && subMenu.gameObject.activeInHierarchy;
        }

        public bool IsDraftPresent()
        {
            return IsPresent();
        }

        public bool IsUpgradePresent()
        {
            UpgradeTroopsSubMenu subMenu = GetUpgradeTroopsSubMenu();
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && AsyncField != null
                && AsyncField.GetValue(_menu) != null
                && subMenu != null
                && subMenu.gameObject.activeInHierarchy;
        }

        public TroopHudAdapter Troops
        {
            get { return new TroopHudAdapter(GetTroopHud(), _facade, _localization); }
        }

        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get { return new PurchaseTroopsSubMenuAdapter(GetPurchaseTroopsSubMenu(), _facade, _localization); }
        }

        public UpgradeTroopsSubMenuAdapter UpgradeTroops
        {
            get { return new UpgradeTroopsSubMenuAdapter(GetUpgradeTroopsSubMenu(), _localization); }
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Close();
            return true;
        }

        public string CloseLabel
        {
            get { return "Close"; }
        }

        public bool BackToTop()
        {
            if (_menu == null || !IsUpgradePresent())
            {
                return false;
            }

            MethodInfo method = AccessTools.Method(typeof(DwellingInteractionMenu), "HandleBackClicked");
            if (method == null)
            {
                return false;
            }

            method.Invoke(_menu, null);
            return true;
        }

        private WielderInteractHeader GetHeader()
        {
            return GetField<WielderInteractHeader>(_menu, WielderInteractHeaderField);
        }

        private TroopHUD GetTroopHud()
        {
            return GetField<TroopHUD>(GetHeader(), HeaderTroopHudField);
        }

        private Component GetWielderPortrait()
        {
            return GetField<UIImage>(GetHeader(), HeaderPortraitField);
        }

        private PurchaseTroopsSubMenu GetPurchaseTroopsSubMenu()
        {
            return GetField<PurchaseTroopsSubMenu>(_menu, PurchaseTroopsSubMenuField);
        }

        private UpgradeTroopsSubMenu GetUpgradeTroopsSubMenu()
        {
            return GetField<UpgradeTroopsSubMenu>(_menu, UpgradeTroopsSubMenuField);
        }

        private int GetInteractingCommanderId()
        {
            object value = InteractingCommanderIdField != null && _menu != null
                ? InteractingCommanderIdField.GetValue(_menu)
                : null;
            return value is int ? (int)value : -1;
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static T GetField<T>(object owner, FieldInfo field) where T : class
        {
            return owner != null && field != null ? field.GetValue(owner) as T : null;
        }
    }
}
