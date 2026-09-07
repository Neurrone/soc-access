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
    public sealed class RallyPointInteractionMenuAdapter
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

        public RallyPointInteractionMenuAdapter(RallyPointInteractionMenu menu)
        {
            _menu = menu;
            _facade = GetField<IClientAdventureFacade>(_menu, AdventureFacadeField);
            _localization = GetField<ILocalizationHandler>(_menu, LocalizationField);
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

        /// <summary>The name of the place the recruits are coming from, which the menu writes in a
        /// line of its own over the grid: a town's name, or its own word for taking from all of
        /// them.</summary>
        public string SelectedSourceName
        {
            get { return GetText(GetField<UITextMesh>(_menu, SelectedTownNameField)); }
        }

        public Component SelectedSourceLine
        {
            get { return GetField<UITextMesh>(_menu, SelectedTownNameField) as Component; }
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

        public PurchaseTroopsSubMenuAdapter PurchaseTroops
        {
            get { return new PurchaseTroopsSubMenuAdapter(GetPurchaseSubMenu(), _facade, _localization); }
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

                result.Add(new SourceItem(this, entry));
            }

            return result;
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

            /// <summary>The button the entry draws, which is the whole of it.</summary>
            public Component Button
            {
                get { return GetField<UIButton>(_entry, EntryButtonField) as Component; }
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
