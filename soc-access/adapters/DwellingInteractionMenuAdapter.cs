using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class DwellingInteractionMenuAdapter
    {
        private static readonly FieldInfo WielderInteractHeaderField = AccessTools.Field(typeof(DwellingInteractionMenu), "_wielderInteractHeader");
        private static readonly FieldInfo BuildingNameField = AccessTools.Field(typeof(DwellingInteractionMenu), "_buildingName");
        private static readonly FieldInfo PurchaseTroopsSubMenuField = AccessTools.Field(typeof(DwellingInteractionMenu), "_purchaseTroopsSubMenu");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(DwellingInteractionMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(DwellingInteractionMenu), "_localizationHandler");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(DwellingInteractionMenu), "_async");
        private static readonly FieldInfo InteractingCommanderIdField = AccessTools.Field(typeof(DwellingInteractionMenu), "_interactingCommanderId");

        private static readonly FieldInfo HeaderPortraitField = AccessTools.Field(typeof(WielderInteractHeader), "_wielderPortrait");

        private static readonly FieldInfo RecruitmentPoolField = AccessTools.Field(typeof(DwellingInteractionMenu), "_recruitmentPool");
        private static readonly FieldInfo UpgradeTroopsSubMenuField = AccessTools.Field(typeof(DwellingInteractionMenu), "_upgradeTroopsSubMenu");
        private static readonly FieldInfo BackToTopButtonField = AccessTools.Field(typeof(DwellingInteractionMenu), "_backToTopButton");

        private readonly DwellingInteractionMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private WielderInteract _wielder;
        private PurchaseTroopsSubMenuAdapter _purchaseTroops;
        private UpgradeTroopsSubMenuAdapter _upgradeTroops;

        public DwellingInteractionMenuAdapter(DwellingInteractionMenu menu)
        {
            _menu = menu;
            _facade = Reflect.Get<IClientAdventureFacade>(_menu, AdventureFacadeField);
            _localization = Reflect.Get<ILocalizationHandler>(_menu, LocalizationField);
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
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, BuildingNameField)); }
        }

        public string WielderName
        {
            get
            {
                int commanderId = GetInteractingCommanderId();
                string name = _facade != null && _facade.Commanders != null && commanderId >= 0
                    ? _facade.Commanders.GetName(commanderId)
                    : string.Empty;
                return SpokenLines.Clean(name);
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
                && IsEntityAlive()
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
                && IsEntityAlive()
                && subMenu != null
                && subMenu.gameObject.activeInHierarchy;
        }

        /// <summary>Whether the building this menu is about is still in the game. A menu holds its
        /// recruitment pool - and with it the map entity - from one Show to the next and does not
        /// clear it on Close, so a menu left open over an entity that has gone (a save loaded under
        /// it, the building destroyed) keeps every other sign of being up: its object is still
        /// active, its sub-page is still drawn, and its Async is still uncompleted because Close
        /// never ran. The entity's own IsDisposed is what the game changes
        /// (<c>AbstractMapEntity.Dispose</c>), so that is what is read.</summary>
        private bool IsEntityAlive()
        {
            IRecruitmentPoolComponent pool = Reflect.Get<IRecruitmentPoolComponent>(_menu, RecruitmentPoolField);
            IMapEntity entity = pool != null ? pool.MapEntity : null;
            return entity != null && !entity.IsDisposed;
        }

        /// <summary>The draft sub-page. Kept, so the page's build reads one adapter rather than a new
        /// one every frame.</summary>
        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get
            {
                PurchaseTroopsSubMenu subMenu = GetPurchaseTroopsSubMenu();
                if (_purchaseTroops == null || !ReferenceEquals(_purchaseTroops.SubMenu, subMenu))
                {
                    _purchaseTroops = new PurchaseTroopsSubMenuAdapter(subMenu, _facade, _localization);
                }

                return _purchaseTroops;
            }
        }

        /// <summary>The upgrade sub-page. Kept for the same reason as the draft one.</summary>
        public UpgradeTroopsSubMenuAdapter UpgradeTroops
        {
            get
            {
                UpgradeTroopsSubMenu subMenu = GetUpgradeTroopsSubMenu();
                if (_upgradeTroops == null || !ReferenceEquals(_upgradeTroops.SubMenu, subMenu))
                {
                    _upgradeTroops = new UpgradeTroopsSubMenuAdapter(subMenu, _localization);
                }

                return _upgradeTroops;
            }
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

        /// <summary>The band the menu hangs across its top: the wielder who walked in, their army and
        /// the close cross.</summary>
        public WielderInteract Wielder
        {
            get
            {
                WielderInteractHeader header = GetHeader();
                if (_wielder == null || !ReferenceEquals(_wielder.Header, header))
                {
                    _wielder = new WielderInteract(header, _facade, _localization);
                }

                return _wielder;
            }
        }

        /// <summary>The button the menu draws over its upgrade page to get back to the draft page it
        /// opens on, and the word the prefab has written on it.</summary>
        public Component BackButton
        {
            get { return Reflect.Get<UIButton>(_menu, BackToTopButtonField) as Component; }
        }

        public string BackLabel
        {
            get { return SpokenLines.Clean(MenuButtonTextUtility.GetAllVisibleText(Reflect.Get<UIButton>(_menu, BackToTopButtonField))); }
        }

        public bool IsBackVisible()
        {
            Component button = BackButton;
            return button != null && button.gameObject != null && button.gameObject.activeInHierarchy;
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
            return Reflect.Get<WielderInteractHeader>(_menu, WielderInteractHeaderField);
        }

        private Component GetWielderPortrait()
        {
            return Reflect.Get<UIImage>(GetHeader(), HeaderPortraitField);
        }

        private PurchaseTroopsSubMenu GetPurchaseTroopsSubMenu()
        {
            return Reflect.Get<PurchaseTroopsSubMenu>(_menu, PurchaseTroopsSubMenuField);
        }

        private UpgradeTroopsSubMenu GetUpgradeTroopsSubMenu()
        {
            return Reflect.Get<UpgradeTroopsSubMenu>(_menu, UpgradeTroopsSubMenuField);
        }

        private int GetInteractingCommanderId()
        {
            object value = InteractingCommanderIdField != null && _menu != null
                ? InteractingCommanderIdField.GetValue(_menu)
                : null;
            return value is int ? (int)value : -1;
        }
    }
}
