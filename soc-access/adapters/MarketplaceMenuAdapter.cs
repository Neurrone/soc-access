using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class MarketplaceMenuAdapter : IPresent
    {
        private static readonly FieldInfo ButtonsField = AccessTools.Field(typeof(MarketplaceMenu), "_buttons");
        private static readonly FieldInfo TitleTextField = AccessTools.Field(typeof(MarketplaceMenu), "_titleText");
        private static readonly FieldInfo NumberOfMarketplacesTextField = AccessTools.Field(typeof(MarketplaceMenu), "_numberOfMarketplacesText");
        private static readonly FieldInfo FacadeField = AccessTools.Field(typeof(MarketplaceMenu), "_facade");
        private static readonly FieldInfo LocalizationField = AccessTools.Field(typeof(MarketplaceMenu), "_localizationHandler");
        private static readonly FieldInfo TeamIdField = AccessTools.Field(typeof(MarketplaceMenu), "_teamId");

        // The rows the menu draws, in their drawn order (measured 2026-09-07). Gold is the currency the
        // grid prices everything in, and the menu draws no row for it.
        private static readonly ResourceType[] TradedResources =
        {
            ResourceType.Stone,
            ResourceType.Wood,
            ResourceType.Glimmerweave,
            ResourceType.AncientAmber,
            ResourceType.CelestialOre
        };

        private readonly MarketplaceMenu _menu;
        private readonly IClientAdventureFacade _facade;
        private readonly ILocalizationHandler _localization;
        private GridSnapshot _grid;

        public MarketplaceMenuAdapter(MarketplaceMenu menu)
        {
            _menu = menu;
            _facade = Reflect.Get<IClientAdventureFacade>(menu, FacadeField);
            _localization = Reflect.Get<ILocalizationHandler>(menu, LocalizationField);
        }

        public MarketplaceMenu Source
        {
            get { return _menu; }
        }

        public IClientAdventureFacade Facade
        {
            get { return _facade; }
        }

        // The meshes the menu draws its tip and its captions on. Which meshes they ARE is fixed for
        // the menu's life and finding them walked the whole page twice a frame; what they SAY, and
        // whether they are drawn, is still read live.
        private UITextMesh _tipText;
        private bool _tipProbed;
        private List<UITextMesh> _headerTexts;

        public bool IsPresent()
        {
            return _menu != null && ((Component)_menu).gameObject.activeInHierarchy;
        }

        /// <summary>The menu's own drawn heading, which is the marketplace building's name ("Court of
        /// Trade").</summary>
        public string Title
        {
            get
            {
                string title = UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, TitleTextField));
                return string.IsNullOrWhiteSpace(title) ? string.Empty : title;
            }
        }

        /// <summary>The line the menu draws under the heading, counting the marketplaces the team owns
        /// ("Owning: 2").</summary>
        public string OwningSummary
        {
            get { return UITextMeshTextUtility.Spoken(Reflect.Get<UITextMesh>(_menu, NumberOfMarketplacesTextField)); }
        }

        /// <summary>One entry per resource the grid trades, in drawn order.</summary>
        public IReadOnlyList<ResourceItem> GetResources()
        {
            List<ResourceItem> resources = new List<ResourceItem>(TradedResources.Length);
            for (int i = 0; i < TradedResources.Length; i++)
            {
                ResourceType type = TradedResources[i];
                resources.Add(new ResourceItem(type, GetResourceName(type), GetResourceAmount(type)));
            }

            return resources;
        }

        /// <summary>
        /// The grid's columns, left to right as the menu draws them: one per fixed trade amount, each
        /// carrying the caption drawn over it and the caption of the band it sits under.
        ///
        /// The columns themselves come from the buttons - a column IS a (buy or sell, amount) pair, and
        /// the menu's own button list is the only place that pairing is written down. Their captions are
        /// read off the menu's Header texts and paired with the columns by what is drawn where: the
        /// headers drawn highest are the band captions ("Sell", "Purchase"), the rest are the column
        /// captions ("-1", "-5", "+1", "+5"), and each column takes the nearest of each by drawn centre.
        /// </summary>
        public IReadOnlyList<TradeColumn> GetTradeColumns()
        {
            return Grid().Columns;
        }

        /// <summary>The grid as the menu laid it out: the columns in drawn order and the button at
        /// every crossing of resource and column. Both are fixed once the menu has spawned its
        /// buttons, so the whole thing is built when the menu's own button list is replaced or grows
        /// and not on every build - the page used to sort the layout twice a frame and then scan the
        /// button list once per cell.
        ///
        /// A grid whose column captions are not all drawn yet is not kept: the menu spawns its
        /// buttons before it turns its headers on, and a snapshot taken in between would name the
        /// columns nothing for the life of the menu.
        ///
        /// The captions are localized, and the options menu the pause menu opens changes the
        /// language without closing anything, so the language they were read under is part of the
        /// key.</summary>
        private GridSnapshot Grid()
        {
            List<MarketplaceButton> buttons = Reflect.Get<List<MarketplaceButton>>(_menu, ButtonsField);
            int count = buttons != null ? buttons.Count : 0;
            ILanguageDefinition language = _localization != null ? _localization.CurrentLanguage : null;
            if (_grid != null
                && ReferenceEquals(_grid.Source, buttons)
                && _grid.Count == count
                && ReferenceEquals(_grid.Language, language)
                && _grid.IsCaptioned)
            {
                return _grid;
            }

            GridSnapshot grid = new GridSnapshot();
            grid.Source = buttons;
            grid.Count = count;
            grid.Language = language;
            grid.Columns = BuildTradeColumns();
            grid.ByCell = new Dictionary<long, MarketplaceButton>();
            for (int i = 0; buttons != null && i < buttons.Count; i++)
            {
                MarketplaceButton button = buttons[i];
                if (button != null)
                {
                    grid.ByCell[CellKey(button.ResourceType, button.IsBuyButton, button.Amount)] = button;
                }
            }

            _grid = grid;
            return grid;
        }

        /// <summary>One crossing of the grid as a single number, so a cell is found without walking
        /// the menu's button list.</summary>
        private static long CellKey(ResourceType resourceType, bool isBuyButton, int amount)
        {
            return ((long)(int)resourceType << 33) | ((long)(uint)amount << 1) | (isBuyButton ? 1L : 0L);
        }

        private List<TradeColumn> BuildTradeColumns()
        {
            List<ColumnGeometry> geometry = GetColumnGeometry();
            List<TradeColumn> columns = new List<TradeColumn>(geometry.Count);
            if (geometry.Count == 0)
            {
                return columns;
            }

            float topmost = float.MinValue;
            for (int i = 0; i < geometry.Count; i++)
            {
                topmost = Math.Max(topmost, geometry[i].Top);
            }

            List<UITextMesh> bandCaptions;
            List<UITextMesh> columnCaptions;
            SplitHeaderBands(GetHeaderTexts(topmost), geometry.Count, out bandCaptions, out columnCaptions);

            for (int i = 0; i < geometry.Count; i++)
            {
                ColumnGeometry column = geometry[i];
                columns.Add(new TradeColumn(
                    column.IsBuyButton,
                    column.Amount,
                    UITextMeshTextUtility.Spoken(NearestByX(bandCaptions, column.Centre)),
                    UITextMeshTextUtility.Spoken(NearestByX(columnCaptions, column.Centre))));
            }

            return columns;
        }

        /// <summary>The button at one crossing of the grid, or null where the menu draws none.</summary>
        public TradeButtonItem GetTradeButton(ResourceType resourceType, bool isBuyButton, int amount)
        {
            MarketplaceButton button;
            return Grid().ByCell.TryGetValue(CellKey(resourceType, isBuyButton, amount), out button) && button != null
                ? new TradeButtonItem(button)
                : null;
        }

        /// <summary>The paragraphs of the tip the menu draws under the trade, kept apart rather than
        /// collapsed.</summary>
        public IList<string> TipLines
        {
            get
            {
                UITextMesh tip = GetTipText();
                return tip == null || !tip.gameObject.activeInHierarchy
                    ? new List<string>()
                    : SpokenLines.Of(new[] { UITextMeshTextUtility.GetEffectiveText(tip) });
            }
        }

        private UITextMesh GetTipText()
        {
            if (_tipProbed)
            {
                return _tipText;
            }

            if (_menu == null)
            {
                return null;
            }

            _tipProbed = true;
            UITextMesh[] textMeshes = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh != null && string.Equals(textMesh.gameObject.name, "TipText", StringComparison.OrdinalIgnoreCase))
                {
                    _tipText = textMesh;
                    return textMesh;
                }
            }

            return null;
        }

        public bool Close()
        {
            if (_menu == null)
            {
                return false;
            }

            _menu.Hide();
            return true;
        }

        public void HideNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
        }

        /// <summary>The game's own name for a resource.</summary>
        public string GetResourceName(ResourceType resourceType)
        {
            return ResourceCosts.Name(_localization, resourceType);
        }

        // The grid as the menu laid it out, kept until the menu's own button list is replaced.
        private sealed class GridSnapshot
        {
            public List<MarketplaceButton> Source;
            public int Count;
            public ILanguageDefinition Language;
            public List<TradeColumn> Columns;
            public Dictionary<long, MarketplaceButton> ByCell;

            /// <summary>Whether every column the menu drew has the caption that names it.</summary>
            public bool IsCaptioned
            {
                get
                {
                    for (int i = 0; i < Columns.Count; i++)
                    {
                        if (string.IsNullOrWhiteSpace(Columns[i].Caption))
                        {
                            return false;
                        }
                    }

                    return true;
                }
            }
        }

        // One column of the grid as its buttons are drawn: which trade it makes and where it sits, so
        // the captions above it can be paired with it and the columns put in drawn order.
        private sealed class ColumnGeometry
        {
            public bool IsBuyButton;
            public int Amount;
            public float Sum;
            public int Count;
            public float Top;

            public float Centre
            {
                get { return Count > 0 ? Sum / Count : 0f; }
            }
        }

        private List<ColumnGeometry> GetColumnGeometry()
        {
            List<ColumnGeometry> geometry = new List<ColumnGeometry>();
            IReadOnlyList<MarketplaceButton> buttons = GetButtons();
            for (int i = 0; i < buttons.Count; i++)
            {
                MarketplaceButton button = buttons[i];
                Component component = button as Component;
                if (button == null || component == null)
                {
                    continue;
                }

                Vector3 position = component.transform.position;
                ColumnGeometry column = null;
                for (int c = 0; c < geometry.Count; c++)
                {
                    if (geometry[c].IsBuyButton == button.IsBuyButton && geometry[c].Amount == button.Amount)
                    {
                        column = geometry[c];
                        break;
                    }
                }

                if (column == null)
                {
                    column = new ColumnGeometry
                    {
                        IsBuyButton = button.IsBuyButton,
                        Amount = button.Amount,
                        Top = position.y
                    };
                    geometry.Add(column);
                }

                column.Sum += position.x;
                column.Count++;
                column.Top = Math.Max(column.Top, position.y);
            }

            geometry.Sort((left, right) => left.Centre.CompareTo(right.Centre));
            return geometry;
        }

        // The menu's Header texts above the grid: the band captions and the column captions, and
        // nothing else it draws. Its heading and its marketplace count are known by their own fields,
        // and everything a row draws sits below the top row of buttons.
        private List<UITextMesh> GetHeaderTexts(float above)
        {
            List<UITextMesh> headers = new List<UITextMesh>();
            List<UITextMesh> candidates = GetHeaderCandidates();
            for (int i = 0; i < candidates.Count; i++)
            {
                UITextMesh textMesh = candidates[i];
                if (textMesh == null
                    || !textMesh.gameObject.activeInHierarchy
                    || textMesh.transform.position.y <= above)
                {
                    continue;
                }

                headers.Add(textMesh);
            }

            return headers;
        }

        // Which meshes are named Header, and are neither the heading nor the marketplace count: a
        // fact about the menu's own layout, so the walk that finds them runs once. Whether one is
        // drawn, and how high it is drawn, are still read per call.
        private List<UITextMesh> GetHeaderCandidates()
        {
            if (_headerTexts != null)
            {
                return _headerTexts;
            }

            List<UITextMesh> candidates = new List<UITextMesh>();
            if (_menu == null)
            {
                return candidates;
            }

            UITextMesh title = Reflect.Get<UITextMesh>(_menu, TitleTextField);
            UITextMesh owning = Reflect.Get<UITextMesh>(_menu, NumberOfMarketplacesTextField);
            UITextMesh[] textMeshes = ((Component)_menu).GetComponentsInChildren<UITextMesh>(includeInactive: true);
            for (int i = 0; i < textMeshes.Length; i++)
            {
                UITextMesh textMesh = textMeshes[i];
                if (textMesh == null
                    || ReferenceEquals(textMesh, title)
                    || ReferenceEquals(textMesh, owning)
                    || textMesh.gameObject.name.IndexOf("Header", StringComparison.OrdinalIgnoreCase) < 0)
                {
                    continue;
                }

                candidates.Add(textMesh);
            }

            _headerTexts = candidates;
            return candidates;
        }

        // The band captions are the ones drawn ABOVE the column captions, so the two bands part at the
        // widest gap between neighbouring heights. Where there are no more headers than there are
        // columns, there is no band above them at all.
        private static void SplitHeaderBands(
            List<UITextMesh> headers,
            int columnCount,
            out List<UITextMesh> bandCaptions,
            out List<UITextMesh> columnCaptions)
        {
            bandCaptions = new List<UITextMesh>();
            columnCaptions = headers;
            if (headers.Count <= columnCount)
            {
                return;
            }

            headers.Sort((left, right) => Top(right).CompareTo(Top(left)));
            int split = 0;
            float widest = float.MinValue;
            for (int i = 0; i < headers.Count - 1; i++)
            {
                float gap = Top(headers[i]) - Top(headers[i + 1]);
                if (gap > widest)
                {
                    widest = gap;
                    split = i;
                }
            }

            bandCaptions = headers.GetRange(0, split + 1);
            columnCaptions = headers.GetRange(split + 1, headers.Count - split - 1);
        }

        private static UITextMesh NearestByX(List<UITextMesh> candidates, float x)
        {
            UITextMesh nearest = null;
            float distance = float.MaxValue;
            for (int i = 0; i < candidates.Count; i++)
            {
                float apart = Math.Abs(candidates[i].transform.position.x - x);
                if (apart < distance)
                {
                    distance = apart;
                    nearest = candidates[i];
                }
            }

            return nearest;
        }

        private static float Top(UITextMesh textMesh)
        {
            return textMesh != null ? textMesh.transform.position.y : 0f;
        }

        private IReadOnlyList<MarketplaceButton> GetButtons()
        {
            List<MarketplaceButton> buttons = Reflect.Get<List<MarketplaceButton>>(_menu, ButtonsField);
            return buttons ?? new List<MarketplaceButton>();
        }

        private int GetResourceAmount(ResourceType resourceType)
        {
            ITeamState team = _facade != null && _facade.Teams != null ? _facade.Teams.Get(TeamId) : null;
            Resource resource = team != null && team.Resources != null ? team.Resources.GetResource(resourceType) : null;
            return resource != null ? resource.Amount : 0;
        }

        private int TeamId
        {
            get { return GetFieldValue(_menu, TeamIdField, _facade != null && _facade.Teams != null ? _facade.Teams.LocalTeamInControlId : -1); }
        }

        private static T GetFieldValue<T>(object owner, FieldInfo field, T fallback)
        {
            if (owner == null || field == null)
            {
                return fallback;
            }

            object value = field.GetValue(owner);
            return value is T ? (T)value : fallback;
        }

        public sealed class ResourceItem
        {
            public ResourceItem(ResourceType resourceType, string resourceName, int amount)
            {
                ResourceType = resourceType;
                ResourceName = resourceName ?? string.Empty;
                Amount = amount;
            }

            public ResourceType ResourceType { get; private set; }
            public string ResourceName { get; private set; }
            public int Amount { get; private set; }
        }

        /// <summary>One column of the trade grid: the trade every button in it makes, and the two
        /// captions the menu draws over it.</summary>
        public sealed class TradeColumn
        {
            public TradeColumn(bool isBuyButton, int amount, string bandCaption, string caption)
            {
                IsBuyButton = isBuyButton;
                Amount = amount;
                BandCaption = bandCaption ?? string.Empty;
                Caption = caption ?? string.Empty;
            }

            public bool IsBuyButton { get; private set; }

            public int Amount { get; private set; }

            /// <summary>The caption of the band the column sits under ("Sell", "Purchase").</summary>
            public string BandCaption { get; private set; }

            /// <summary>The caption drawn directly over the column ("-1", "+5").</summary>
            public string Caption { get; private set; }
        }

        /// <summary>One crossing of the grid: the button the menu draws, and the price it draws on it.
        /// </summary>
        public sealed class TradeButtonItem
        {
            private readonly MarketplaceButton _button;

            public TradeButtonItem(MarketplaceButton button)
            {
                _button = button;
            }

            /// <summary>The button the menu draws this trade as.</summary>
            public Component Component
            {
                get { return _button as Component; }
            }

            /// <summary>The gold the trade costs or gains, which the menu writes onto the button
            /// (<c>MarketplaceMenu.ValidateButtons</c>).</summary>
            public string Price
            {
                get { return MenuButtonTextUtility.GetDirectButtonText(_button); }
            }

            public bool IsVisible
            {
                get { return MenuButtonAdapterBase.IsButtonVisible(_button); }
            }

            public bool IsEnabled
            {
                get { return _button != null && _button.Active && _button.Interactable; }
            }

            public bool Activate()
            {
                return NativeSelectionUtility.Click(_button);
            }

            public void Focus()
            {
                NativeSelectionUtility.Select(_button as Component);
            }
        }
    }
}
