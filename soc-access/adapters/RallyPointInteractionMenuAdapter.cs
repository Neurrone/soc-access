using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Utilities;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.UI;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal sealed class RallyPointInteractionMenuAdapter
    {
        private static readonly FieldInfo HeaderField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_wielderInteractHeader");
        private static readonly FieldInfo PurchaseTroopsSubMenuField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_purchaseTroopsSubMenu");
        private static readonly FieldInfo BuildingNameField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_buildingName");
        private static readonly FieldInfo SelectedTownNameField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_selectedTownName");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_localizationHandler");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_async");
        private static readonly FieldInfo ActiveEntriesField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_activeEntries");
        private static readonly FieldInfo RecruitmentPoolField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_recruitmentPool");
        private static readonly FieldInfo PurchaseSubMenuParentIdField = AccessTools.Field(typeof(PurchaseTroopsSubMenu), "_parentId");

        private static readonly FieldInfo HeaderPortraitField = AccessTools.Field(typeof(WielderInteractHeader), "_wielderPortrait");
        private static readonly FieldInfo HeaderTroopHudField = AccessTools.Field(typeof(WielderInteractHeader), "_troopHUD");
        private static readonly FieldInfo HeaderCloseButtonField = AccessTools.Field(typeof(WielderInteractHeader), "_closeButton");

        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(RallyPointTownEntry), "_button");
        private static readonly FieldInfo EntrySelectedField = AccessTools.Field(typeof(RallyPointTownEntry), "_selected");
        private static readonly FieldInfo EntryLevelField = AccessTools.Field(typeof(RallyPointTownEntry), "_level");
        private static readonly FieldInfo EntryLevelContainerField = AccessTools.Field(typeof(RallyPointTownEntry), "_levelContainer");

        private readonly RallyPointInteractionMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;

        public RallyPointInteractionMenuAdapter(RallyPointInteractionMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(_menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(_menu, LocalizationField);
        }

        public RallyPointInteractionMenu Source
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        public int InteractingCommanderId
        {
            get
            {
                PurchaseTroopsSubMenu subMenu = GetPurchaseSubMenu();
                object value = PurchaseSubMenuParentIdField != null && subMenu != null
                    ? PurchaseSubMenuParentIdField.GetValue(subMenu)
                    : null;
                return value is int ? (int)value : -1;
            }
        }

        public int RallyPointMapEntityId
        {
            get
            {
                IRallyPointRecruitmentPoolComponent pool = GetField<IRallyPointRecruitmentPoolComponent>(_menu, RecruitmentPoolField);
                return pool != null && pool.MapEntity != null ? pool.MapEntity.Id : -1;
            }
        }

        public bool IsPresent()
        {
            PurchaseTroopsSubMenu subMenu = GetPurchaseSubMenu();
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && GetField<Async>(_menu, AsyncField) != null
                && subMenu != null
                && subMenu.gameObject != null
                && subMenu.gameObject.activeInHierarchy
                && GetSourceItems().Count > 0;
        }

        public string Title
        {
            get { return GetText(GetField<UITextMesh>(_menu, BuildingNameField)); }
        }

        public string SelectedSourceName
        {
            get { return GetText(GetField<UITextMesh>(_menu, SelectedTownNameField)); }
        }

        public string WielderName
        {
            get
            {
                int commanderId = InteractingCommanderId;
                string name = commanderId >= 0 && _facade != null && _facade.Commanders != null
                    ? _facade.Commanders.GetName(commanderId)
                    : string.Empty;
                return SpeechTextSanitizer.Normalize(name);
            }
        }

        public Tooltip WielderTooltip
        {
            get { return Tooltip.ForComponent(GetField<UIImage>(GetHeader(), HeaderPortraitField) as Component, _localization); }
        }

        public TroopHudAdapter Troops
        {
            get { return new TroopHudAdapter(GetField<TroopHUD>(GetHeader(), HeaderTroopHudField), _facade, _localization); }
        }

        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get { return new PurchaseTroopsSubMenuAdapter(GetPurchaseSubMenu(), _facade, _localization); }
        }

        public string CloseLabel
        {
            get
            {
                return GetButtonLabel(GetField<UIButton>(GetHeader(), HeaderCloseButtonField));
            }
        }

        public bool Close()
        {
            if (_menu == null || !IsMenuOpen())
            {
                return false;
            }

            _menu.Close();
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        public IReadOnlyList<SourceItem> GetSourceItems()
        {
            List<RallyPointTownEntry> entries = GetField<List<RallyPointTownEntry>>(_menu, ActiveEntriesField);
            if (entries == null || entries.Count == 0)
            {
                return new SourceItem[0];
            }

            List<SourceItem> result = new List<SourceItem>();
            for (int i = 0; i < entries.Count; i++)
            {
                RallyPointTownEntry entry = entries[i];
                if (entry == null || entry.gameObject == null || !entry.gameObject.activeInHierarchy)
                {
                    continue;
                }

                result.Add(new SourceItem(this, entry, i));
            }

            return result;
        }

        public int SelectedSourceIndex
        {
            get
            {
                IReadOnlyList<SourceItem> sources = GetSourceItems();
                for (int i = 0; i < sources.Count; i++)
                {
                    if (sources[i].IsSelected)
                    {
                        return sources[i].Index;
                    }
                }

                return -1;
            }
        }

        private bool IsMenuOpen()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && GetField<Async>(_menu, AsyncField) != null;
        }

        private WielderInteractHeader GetHeader()
        {
            return GetField<WielderInteractHeader>(_menu, HeaderField);
        }

        private PurchaseTroopsSubMenu GetPurchaseSubMenu()
        {
            return GetField<PurchaseTroopsSubMenu>(_menu, PurchaseTroopsSubMenuField);
        }

        private string GetLocalizedText(string key)
        {
            return SpeechTextSanitizer.Normalize(GameText.Get(_localization, key, string.Empty));
        }

        private string GetTownName(IMapEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string customNameKey;
            if (entity.TryGetCustomNameKey(out customNameKey)
                || (_facade != null
                    && _facade.MapEntities != null
                    && _facade.MapEntities.GetParentEntity(entity) != null
                    && _facade.MapEntities.GetParentEntity(entity).TryGetCustomNameKey(out customNameKey)))
            {
                string customName = _localization != null ? _localization.GetText(customNameKey) : customNameKey;
                if (!string.IsNullOrWhiteSpace(customName) && customName != customNameKey)
                {
                    return SpeechTextSanitizer.Normalize(customName);
                }
            }

            string name = _localization != null ? _localization.GetText(entity.NameKey) : entity.NameKey;
            return SpeechTextSanitizer.Normalize(string.IsNullOrWhiteSpace(name) || name == entity.NameKey ? entity.NameKey : name);
        }

        private static string GetButtonLabel(UIButton button)
        {
            return SpeechTextSanitizer.Normalize(MenuButtonTextUtility.GetAllVisibleText(button));
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpeechTextSanitizer.Normalize(UITextMeshTextUtility.GetEffectiveText(textMesh));
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

        internal sealed class SourceItem
        {
            private readonly RallyPointInteractionMenuAdapter _adapter;
            private readonly RallyPointTownEntry _entry;
            public SourceItem(RallyPointInteractionMenuAdapter adapter, RallyPointTownEntry entry, int index)
            {
                _adapter = adapter;
                _entry = entry;
                Index = index;
            }

            public int Index { get; private set; }

            public int MapEntityId
            {
                get
                {
                    IMapEntity entity = _entry != null ? _entry.MapEntity : null;
                    return entity != null ? entity.Id : -1;
                }
            }

            public bool IsAllSources
            {
                get { return _entry != null && _entry.MapEntity == null; }
            }

            public string Name
            {
                get
                {
                    if (IsAllSources)
                    {
                        return _adapter.GetLocalizedText("Adventure/PurchaseTroopsMenu/RallyPoint/PurchaseFromAll");
                    }

                    return _adapter.GetTownName(_entry != null ? _entry.MapEntity : null);
                }
            }

            public string Level
            {
                get { return GetText(GetField<UITextMesh>(_entry, EntryLevelField)); }
            }

            public bool IsLevelVisible
            {
                get { return IsVisible(GetField<GameObject>(_entry, EntryLevelContainerField)); }
            }

            public bool IsSelected
            {
                get { return IsVisible(GetField<Image>(_entry, EntrySelectedField) as Component); }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(GetField<UIButton>(_entry, EntryButtonField) as Component, _adapter._localization); }
            }

            public void Focus()
            {
                UIButton button = GetField<UIButton>(_entry, EntryButtonField);
                NativeSelectionUtility.Select(button);
            }

            public bool Select()
            {
                UIButton button = GetField<UIButton>(_entry, EntryButtonField);
                return NativeSelectionUtility.Click(button);
            }
        }
    }
}
