using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.UI;
using SongsOfConquest.Common.Economy;
using SongsOfConquest.Common.Gamestate;
using UnityEngine;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class CombatScreen : Screen
    {
        private const int GridIndex = 0;
        private readonly CombatAdapter _adapter;
        private readonly CombatHexGrid _grid;

        public CombatScreen(CombatAdapter adapter)
            : this(adapter, new CombatHexGrid(adapter))
        {
        }

        private CombatScreen(CombatAdapter adapter, CombatHexGrid grid)
            : base(BuildRoot(adapter, grid))
        {
            _adapter = adapter;
            _grid = grid;
        }

        public override bool IsPresent()
        {
            return _adapter != null && _adapter.IsPresent();
        }

        public CombatAdapter Adapter
        {
            get { return _adapter; }
        }

        public override void OnPush()
        {
            AccessibilityEventBus.Subscribe(HandleAccessibilityEvent);
            _grid?.AttachSpellCastBegin();
            _adapter?.AttachSpellTargetingNarration();
            _adapter?.AnnounceVisibleSpellTargetInstruction();
        }

        public void MoveCursorToLocalActingTroop(int troopId)
        {
            Vector2Int position;
            if (_adapter != null
                && _adapter.TryGetLocalActingTroopPosition(troopId, out position)
                && _grid != null)
            {
                _grid.MoveToActingTroop(position);
            }
        }

        public void FocusGrid()
        {
            RootWidget?.SetFocusByIndex(GridIndex);
        }

        public override void OnUnfocus()
        {
            RootWidget?.Unfocus();
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        public override void OnPop()
        {
            AccessibilityEventBus.Unsubscribe(HandleAccessibilityEvent);
            _grid?.DetachSpellCastBegin();
            _adapter?.DetachSpellTargetingNarration();
            _adapter?.ClearNativeTooltip();
            _adapter?.ClearFocusedTileOverlay();
        }

        private void HandleAccessibilityEvent(IAccessibilityEvent accessibilityEvent)
        {
            MapHudVisibilityChangedEvent hudVisibility = accessibilityEvent as MapHudVisibilityChangedEvent;
            if (hudVisibility != null && !hudVisibility.IsVisible)
            {
                FocusGrid();
            }
        }

        private static ContainerWidget BuildRoot(CombatAdapter adapter, CombatHexGrid grid)
        {
            ContainerWidget root = new ContainerWidget("combat-screen", "Combat");
            root.AddChild(grid);
            if (adapter == null)
            {
                return root;
            }

            root.AddChild(Portrait.StaticNative(
                "combat-attacker-portrait",
                () => adapter.Hud.Commanders.GetPortraitLabel(CombatHudSide.Attacker),
                () => adapter.Hud.Commanders.GetPortraitButton(CombatHudSide.Attacker),
                adapter.Hud.Commanders.Localization,
                isVisible: () => adapter.Hud.Commanders.IsPortraitVisible(CombatHudSide.Attacker)));
            root.AddChild(BuildEssenceMenu(adapter, CombatHudSide.Attacker, "combat-attacker-essence", "Attacker essence"));
            root.AddChild(Portrait.StaticNative(
                "combat-defender-portrait",
                () => adapter.Hud.Commanders.GetPortraitLabel(CombatHudSide.Defender),
                () => adapter.Hud.Commanders.GetPortraitButton(CombatHudSide.Defender),
                adapter.Hud.Commanders.Localization,
                isVisible: () => adapter.Hud.Commanders.IsPortraitVisible(CombatHudSide.Defender)));
            root.AddChild(BuildEssenceMenu(adapter, CombatHudSide.Defender, "combat-defender-essence", "Defender essence"));
            root.AddChild(new ButtonWidget(
                "combat-spellbook",
                () => adapter.Hud.SpellbookButtonLabel,
                adapter.Hud.ClickSpellbookButton,
                adapter.Hud.FocusSpellbookButton,
                adapter.Hud.IsSpellbookButtonEnabled,
                adapter.Hud.IsSpellbookButtonVisible,
                () => adapter.Hud.SpellbookButtonTooltip));
            root.AddChild(new ButtonWidget(
                "combat-end-turn",
                () => adapter.Hud.EndTurnButtonLabel,
                adapter.Hud.ClickEndTurnButton,
                adapter.Hud.FocusEndTurnButton,
                adapter.Hud.IsEndTurnButtonEnabled,
                adapter.Hud.IsEndTurnButtonVisible,
                () => adapter.Hud.EndTurnButtonTooltip));
            return root;
        }

        private static MenuWidget BuildEssenceMenu(CombatAdapter adapter, CombatHudSide side, string id, string label)
        {
            MenuWidget menu = new MenuWidget(id, label, () => adapter.Hud.Commanders.IsEssenceMenuVisible(side));
            AddEssenceItem(menu, adapter, side, EssenceType.Order);
            AddEssenceItem(menu, adapter, side, EssenceType.Creation);
            AddEssenceItem(menu, adapter, side, EssenceType.Chaos);
            AddEssenceItem(menu, adapter, side, EssenceType.Arcana);
            AddEssenceItem(menu, adapter, side, EssenceType.Destruction);
            return menu;
        }

        private static void AddEssenceItem(MenuWidget menu, CombatAdapter adapter, CombatHudSide side, EssenceType essenceType)
        {
            CombatHudSide capturedSide = side;
            EssenceType capturedType = essenceType;
            string sideId = capturedSide == CombatHudSide.Attacker ? "attacker" : "defender";
            menu.AddItem(new MenuItemWidget(
                "combat-" + sideId + "-essence-" + capturedType.ToString().ToLowerInvariant(),
                () => adapter.Hud.Commanders.GetEssenceLabel(capturedSide, capturedType),
                null,
                null,
                () => adapter.Hud.Commanders.FocusEssence(capturedSide, capturedType),
                () => adapter.Hud.Commanders.IsEssenceMenuVisible(capturedSide),
                () => adapter.Hud.Commanders.GetEssenceTooltip(capturedSide, capturedType)));
        }
    }
}
