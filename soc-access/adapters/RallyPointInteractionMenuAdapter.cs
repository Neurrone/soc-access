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
using SongsOfConquestAccess.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class RallyPointInteractionMenuAdapter : IPresent
    {
        private static readonly FieldInfo HeaderField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_wielderInteractHeader");
        private static readonly FieldInfo PurchaseTroopsSubMenuField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_purchaseTroopsSubMenu");
        private static readonly FieldInfo BuildingNameField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_buildingName");
        private static readonly FieldInfo SelectedTownNameField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_selectedTownName");
        private static readonly FieldInfo AdventureFacadeField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_adventureFacade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_localizationHandler");
        private static readonly FieldInfo AsyncField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_async");
        private static readonly FieldInfo ActiveEntriesField = AccessTools.Field(typeof(RallyPointInteractionMenu), "_activeEntries");


        private static readonly FieldInfo EntryButtonField = AccessTools.Field(typeof(RallyPointTownEntry), "_button");
        private static readonly FieldInfo EntrySelectedField = AccessTools.Field(typeof(RallyPointTownEntry), "_selected");
        private static readonly FieldInfo EntryLevelField = AccessTools.Field(typeof(RallyPointTownEntry), "_level");
        private static readonly FieldInfo EntryLevelContainerField = AccessTools.Field(typeof(RallyPointTownEntry), "_levelContainer");

        private readonly RallyPointInteractionMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private WielderInteract _wielder;
        private PurchaseTroopsSubMenuAdapter _purchaseTroops;

        public RallyPointInteractionMenuAdapter(RallyPointInteractionMenu menu)
        {
            _menu = menu;
            _facade = Reflect.Get<IClientAdventureFacade>(_menu, AdventureFacadeField);
            _localization = Reflect.Get<ILocalizationHandler>(_menu, LocalizationField);
        }

        public bool IsPresent()
        {
            PurchaseTroopsSubMenu subMenu = GetPurchaseSubMenu();
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && Reflect.Get<Async>(_menu, AsyncField) != null
                && subMenu != null
                && subMenu.gameObject != null
                && subMenu.gameObject.activeInHierarchy
                && GetSourceItems().Count > 0;
        }

        public string Title
        {
            get { return GetText(Reflect.Get<UITextMesh>(_menu, BuildingNameField)); }
        }

        /// <summary>The name of the place the recruits are coming from, which the menu writes in a
        /// line of its own over the grid: a town's name, or its own word for taking from all of
        /// them.</summary>
        public string SelectedSourceName
        {
            get { return GetText(Reflect.Get<UITextMesh>(_menu, SelectedTownNameField)); }
        }

        public Component SelectedSourceLine
        {
            get { return Reflect.Get<UITextMesh>(_menu, SelectedTownNameField) as Component; }
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

        /// <summary>The draft sub-page. Kept, so the page's build reads one adapter rather than a new
        /// one every frame.</summary>
        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get
            {
                PurchaseTroopsSubMenu subMenu = GetPurchaseSubMenu();
                if (_purchaseTroops == null || !ReferenceEquals(_purchaseTroops.SubMenu, subMenu))
                {
                    _purchaseTroops = new PurchaseTroopsSubMenuAdapter(subMenu, _facade, _localization);
                }

                return _purchaseTroops;
            }
        }

        public IReadOnlyList<SourceItem> GetSourceItems()
        {
            List<RallyPointTownEntry> entries = Reflect.Get<List<RallyPointTownEntry>>(_menu, ActiveEntriesField);
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

                result.Add(new SourceItem(this, entry));
            }

            return result;
        }

        private bool IsMenuOpen()
        {
            return _menu != null
                && _menu.gameObject != null
                && _menu.gameObject.activeInHierarchy
                && Reflect.Get<Async>(_menu, AsyncField) != null;
        }

        private WielderInteractHeader GetHeader()
        {
            return Reflect.Get<WielderInteractHeader>(_menu, HeaderField);
        }

        private PurchaseTroopsSubMenu GetPurchaseSubMenu()
        {
            return Reflect.Get<PurchaseTroopsSubMenu>(_menu, PurchaseTroopsSubMenuField);
        }

        private string GetLocalizedText(string key)
        {
            return SpokenLines.Clean(GameText.Get(_localization, key, string.Empty));
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
                    return SpokenLines.Clean(customName);
                }
            }

            string name = _localization != null ? _localization.GetText(entity.NameKey) : entity.NameKey;
            return SpokenLines.Clean(string.IsNullOrWhiteSpace(name) || name == entity.NameKey ? entity.NameKey : name);
        }

        private static string GetText(IUITextMesh textMesh)
        {
            return SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(textMesh));
        }

        private static bool IsVisible(Component component)
        {
            return component != null && component.gameObject != null && component.gameObject.activeInHierarchy;
        }

        private static bool IsVisible(GameObject gameObject)
        {
            return gameObject != null && gameObject.activeInHierarchy;
        }

        public sealed class SourceItem
        {
            private readonly RallyPointInteractionMenuAdapter _adapter;
            private readonly RallyPointTownEntry _entry;
            public SourceItem(RallyPointInteractionMenuAdapter adapter, RallyPointTownEntry entry)
            {
                _adapter = adapter;
                _entry = entry;
            }

            /// <summary>The town this entry stands for, or the game's own word for taking from every
            /// town at once, which is the entry the game draws with no town behind it.</summary>
            public string Name
            {
                get
                {
                    if (_entry == null || _entry.MapEntity == null)
                    {
                        return _adapter.GetLocalizedText("Adventure/PurchaseTroopsMenu/RallyPoint/PurchaseFromAll");
                    }

                    return _adapter.GetTownName(_entry != null ? _entry.MapEntity : null);
                }
            }

            public string Level
            {
                get { return GetText(Reflect.Get<UITextMesh>(_entry, EntryLevelField)); }
            }

            public bool IsLevelVisible
            {
                get { return IsVisible(Reflect.Get<GameObject>(_entry, EntryLevelContainerField)); }
            }

            public bool IsSelected
            {
                get { return IsVisible(Reflect.Get<Image>(_entry, EntrySelectedField) as Component); }
            }

            public Tooltip Tooltip
            {
                get { return Tooltip.ForComponent(Reflect.Get<UIButton>(_entry, EntryButtonField) as Component, _adapter._localization); }
            }

            /// <summary>The button the entry draws, which is the whole of it.</summary>
            public Component Button
            {
                get { return Reflect.Get<UIButton>(_entry, EntryButtonField) as Component; }
            }

            public void Focus()
            {
                UIButton button = Reflect.Get<UIButton>(_entry, EntryButtonField);
                NativeSelectionUtility.Select(button);
            }

            public bool Select()
            {
                UIButton button = Reflect.Get<UIButton>(_entry, EntryButtonField);
                return NativeSelectionUtility.Click(button);
            }
        }
    }
}
