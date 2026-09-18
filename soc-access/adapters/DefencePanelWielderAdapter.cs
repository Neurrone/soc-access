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
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public sealed class DefencePanelWielderAdapter
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
        private TroopHudAdapter _troops;

        // The prefab's own header mesh and the meshes under the no-wielder container. Both are fixed
        // for the life of the panel and both were a Find plus a subtree walk on every build, from the
        // settlement page and from the defence menu alike. Misses are remembered too.
        private bool _headerProbed;
        private UITextMesh _headerMesh;
        private bool _noStoredWielderProbed;
        private GameObject _noStoredWielderRoot;
        private UITextMesh[] _noStoredWielderMeshes;

        public DefencePanelWielderAdapter(DefencePanelWielder panel, IClientAdventureFacade facade, ILocalizationHandler localization)
        {
            _panel = panel;
            _facade = facade;
            _localization = localization;
        }

        /// <summary>The band this reads, so an owner keeping one of these can tell whether the menu
        /// has swapped its panel out from under it.</summary>
        public DefencePanelWielder Panel
        {
            get { return _panel; }
        }

        public bool IsPresent
        {
            get { return GameObjects.IsLive(_panel as Component); }
        }

        public bool IsStoredWielderVisible
        {
            get { return GameObjects.IsLive(Reflect.Get<GameObject>(_panel, StoredWielderContainerField)); }
        }

        public string StoredWielderName
        {
            get
            {
                ICommanderState storedCommander = StoredCommander;
                string name = storedCommander != null && _facade != null
                    ? _facade.Commanders.GetName(storedCommander.Id)
                    : string.Empty;
                return SpokenLines.Clean(name);
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

        /// <summary>The header the game draws over the band ("Defending wielder"), read off the
        /// prefab's own header layout. Empty where the panel draws none.</summary>
        public string HeaderText
        {
            get
            {
                if (!_headerProbed)
                {
                    _headerProbed = true;
                    _headerMesh = FindHeaderMesh();
                }

                return UITextMeshTextUtility.Spoken(_headerMesh);
            }
        }

        private UITextMesh FindHeaderMesh()
        {
            Component panel = _panel;
            Transform root = panel != null ? panel.transform : null;
            Transform header = root != null ? root.Find("Background/HeaderLayout") : null;
            if (header == null && root != null)
            {
                header = root.Find("HeaderLayout");
            }

            return header == null ? null : header.GetComponentInChildren<UITextMesh>(includeInactive: false);
        }

        /// <summary>What the game writes where no wielder is stored ("Place wielder inside building to
        /// strengthen the defences"). The game draws the Store button inside the same container, so
        /// the button's own caption is left out: it is the button's to say, not the prompt's.</summary>
        public string NoStoredWielderText
        {
            get
            {
                GameObject root = Reflect.Get<GameObject>(_panel, NoStoredWielderContainerField);
                if (!_noStoredWielderProbed || !ReferenceEquals(_noStoredWielderRoot, root))
                {
                    _noStoredWielderProbed = true;
                    _noStoredWielderRoot = root;
                    _noStoredWielderMeshes = root == null
                        ? new UITextMesh[0]
                        : OutsideOf(root.GetComponentsInChildren<UITextMesh>(includeInactive: true), GetStoreButton() as Component);
                }

                return JoinVisibleText(_noStoredWielderMeshes);
            }
        }

        /// <summary>The stored wielder's portrait, whose details are the stats the game draws on
        /// hover.</summary>
        public Component Portrait
        {
            get { return Reflect.Get<Component>(_panel, PortraitImageField); }
        }

        public Tooltip PortraitTooltip
        {
            get { return Tooltip.ForComponent(Reflect.Get<Component>(_panel, PortraitImageField), _localization); }
        }

        public void FocusPortrait()
        {
            NativeSelectionUtility.Select(Reflect.Get<Component>(_panel, PortraitImageField));
        }

        /// <summary>The stored wielder's army, or null before the band has been set up. Kept: the
        /// adapter wakes the game's drag ghost when it is made.</summary>
        public TroopHudAdapter Troops
        {
            get
            {
                TroopHUD hud = Reflect.Get<TroopHUD>(_panel, TroopHudField);
                if (hud == null)
                {
                    return null;
                }

                if (_troops == null || !ReferenceEquals(_troops.Hud, hud))
                {
                    _troops = new TroopHudAdapter(hud, _facade, _localization);
                }

                return _troops;
            }
        }

        /// <summary>The store button's own text, or empty: it draws an icon, and the game has no key
        /// for the action (GameActions/Adventure/StoreCommander is not in its tables; asked in-game
        /// 2026-09-12), so the name is the mod's, in <c>SettlementNodes</c>.</summary>
        public string StoreLabel
        {
            get { return SpokenLines.Clean(MenuButtonTextUtility.GetAllVisibleText(GetStoreButton())); }
        }

        public string EjectLabel
        {
            get { return GetButtonLabel(GetEjectButton(), "GameActions/Adventure/EjectCommander", string.Empty); }
        }

        public string TradeLabel
        {
            get { return GetButtonLabel(GetTradeButton(), "Adventure/TooltipInstruction/Trade", string.Empty); }
        }

        /// <summary>The three buttons the band draws, for the screens that declare them as controls.
        /// </summary>
        public Component StoreButton
        {
            get { return GetStoreButton() as Component; }
        }

        public Component EjectButton
        {
            get { return GetEjectButton() as Component; }
        }

        public Component TradeButton
        {
            get { return GetTradeButton() as Component; }
        }

        public bool IsStoreVisible()
        {
            return GameObjects.IsLive(GetStoreButton() as Component);
        }

        public bool IsEjectVisible()
        {
            return GameObjects.IsLive(GetEjectButton() as Component);
        }

        public bool IsTradeVisible()
        {
            return GameObjects.IsLive(GetTradeButton() as Component);
        }

        public bool IsStoreEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetStoreButton());
        }

        public bool IsEjectEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetEjectButton());
        }

        public bool IsTradeEnabled()
        {
            return MenuButtonAdapterBase.IsButtonEnabledAndDrawn(GetTradeButton());
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
            get { return Reflect.Get<ICommanderState>(_panel, StoredCommanderField); }
        }

        private UIButton GetStoreButton()
        {
            return Reflect.Get<UIButton>(_panel, StoreButtonField);
        }

        private UIButton GetEjectButton()
        {
            return Reflect.Get<UIButton>(_panel, EjectButtonField);
        }

        private UIButton GetTradeButton()
        {
            return Reflect.Get<UIButton>(_panel, TradeButtonField);
        }

        private string GetButtonLabel(UIButton button, string localizationKey, string fallback)
        {
            string label = SpokenLines.Clean(MenuButtonTextUtility.GetAllVisibleText(button));
            if (!string.IsNullOrWhiteSpace(label))
            {
                return label;
            }

            return SpokenText.Get(_localization, localizationKey, fallback);
        }

        private static UITextMesh[] OutsideOf(UITextMesh[] textMeshes, Component button)
        {
            if (button == null)
            {
                return textMeshes;
            }

            List<UITextMesh> kept = new List<UITextMesh>();
            for (int i = 0; i < textMeshes.Length; i++)
            {
                Component mesh = textMeshes[i] as Component;
                if (mesh == null || !mesh.transform.IsChildOf(button.transform))
                {
                    kept.Add(textMeshes[i]);
                }
            }

            return kept.ToArray();
        }

        private static string JoinVisibleText(UITextMesh[] textMeshes)
        {
            if (textMeshes == null)
            {
                return string.Empty;
            }

            List<string> parts = new List<string>();
            for (int i = 0; i < textMeshes.Length; i++)
            {
                if (!GameObjects.IsLive(textMeshes[i] as Component))
                {
                    continue;
                }

                string text = UITextMeshTextUtility.Spoken(textMeshes[i]);
                if (!string.IsNullOrWhiteSpace(text) && !parts.Contains(text))
                {
                    parts.Add(text);
                }
            }

            return string.Join(". ", parts.ToArray());
        }
    }
}
