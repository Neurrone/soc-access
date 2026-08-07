using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Screens;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class CombatHexGrid : Widget
    {
        private const string ScannerWrapCueKey = "Common_ClickUnfold";
        private static readonly Vector2Int CenterTile = new Vector2Int(6, 4);

        private readonly CombatAdapter _adapter;
        private CombatSnapshot _snapshot;
        private Vector2Int _cursor;
        private CombatInspectContext _inspectContext;
        private bool _componentWarningSpoken;
        private readonly ScannerController _scanner;

        public CombatHexGrid(CombatAdapter adapter)
            : base("combat-hex-grid")
        {
            _adapter = adapter;
            RefreshSnapshot();
            _cursor = _adapter != null ? _adapter.GetInitialTile() : Vector2Int.zero;
            _scanner = new ScannerController(
                origin => _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null,
                () => _cursor,
                result => _adapter != null && _adapter.ValidateScannerResult(result),
                JumpToScannerResult,
                (result, directions, index, count) => new CombatScannerSpeechContext(
                    result,
                    _adapter.GetTile(result.Position),
                    _adapter,
                    directions,
                    index,
                    count),
                ScannerDirectionMode.Hex);
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetRole()
        {
            return string.Empty;
        }

        public override string GetAnnouncementKey()
        {
            return _cursor.ToString();
        }

        public override string GetLabel()
        {
            CombatTile tile = GetFocusedTile();
            bool selectedForSpellcast = _adapter != null
                && _adapter.GetTargetingMode() == CombatTargetingMode.Spell
                && _adapter.IsSpellTargetSelected(_cursor);
            return _adapter != null
                ? _adapter.DescribeTile(tile, GetEffectiveInspectContext(), selectedForSpellcast)
                : ModText.Get(ModStrings.UI.CombatGrid);
        }

        public override Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetInspectTooltip(_inspectContext, _cursor) : null;
        }

        public override bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.HexGridWest.Key
                || actionKey == AccessibilityActions.HexGridEast.Key
                || actionKey == AccessibilityActions.HexGridNorthWest.Key
                || actionKey == AccessibilityActions.HexGridNorthEast.Key
                || actionKey == AccessibilityActions.HexGridSouthWest.Key
                || actionKey == AccessibilityActions.HexGridSouthEast.Key
                || actionKey == AccessibilityActions.HexGridFocusCenterTile.Key
                || actionKey == AccessibilityActions.HexGridSkipWest.Key
                || actionKey == AccessibilityActions.HexGridSkipEast.Key
                || actionKey == AccessibilityActions.HexGridSkipNorthWest.Key
                || actionKey == AccessibilityActions.HexGridSkipNorthEast.Key
                || actionKey == AccessibilityActions.HexGridSkipSouthWest.Key
                || actionKey == AccessibilityActions.HexGridSkipSouthEast.Key
                || actionKey == AccessibilityActions.CombatInspect.Key
                || (CanNavigateLocalActingTroops()
                    && (actionKey == AccessibilityActions.CombatNextActingTroop.Key
                        || actionKey == AccessibilityActions.CombatPreviousActingTroop.Key
                        || actionKey == AccessibilityActions.CombatFocusActingTroop.Key))
                || (CanNavigateEnemyActingTroops()
                    && (actionKey == AccessibilityActions.CombatNextEnemyTroop.Key
                        || actionKey == AccessibilityActions.CombatPreviousEnemyTroop.Key))
                || actionKey == AccessibilityActions.CombatNextRelevantTile.Key
                || actionKey == AccessibilityActions.CombatPreviousRelevantTile.Key
                || actionKey == AccessibilityActions.CombatFocusTimeline.Key
                || actionKey == AccessibilityActions.ReadThreat.Key
                || (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None && actionKey == AccessibilityActions.Activate.Key)
                || actionKey == AccessibilityActions.MapSecondaryAction.Key
                || IsScannerAction(actionKey)
                || ((_inspectContext != null || (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)) && actionKey == AccessibilityActions.Cancel.Key);
        }

        public override bool HasClaimInTree(string actionKey)
        {
            return ClaimsAction(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null)
            {
                return false;
            }

            if (HandleScannerAction(action))
            {
                return true;
            }

            if (action.Key == AccessibilityActions.HexGridWest.Key)
            {
                return Move(-1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridSkipWest.Key)
            {
                return SkipMove(point => new Vector2Int(point.x - 1, point.y));
            }

            if (action.Key == AccessibilityActions.HexGridEast.Key)
            {
                return Move(1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridSkipEast.Key)
            {
                return SkipMove(point => new Vector2Int(point.x + 1, point.y));
            }

            if (action.Key == AccessibilityActions.HexGridNorthWest.Key)
            {
                return MoveDiagonal(north: true, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridSkipNorthWest.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: true, east: false));
            }

            if (action.Key == AccessibilityActions.HexGridNorthEast.Key)
            {
                return MoveDiagonal(north: true, east: true);
            }

            if (action.Key == AccessibilityActions.HexGridSkipNorthEast.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: true, east: true));
            }

            if (action.Key == AccessibilityActions.HexGridSouthWest.Key)
            {
                return MoveDiagonal(north: false, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridSkipSouthWest.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: false, east: false));
            }

            if (action.Key == AccessibilityActions.HexGridSouthEast.Key)
            {
                return MoveDiagonal(north: false, east: true);
            }

            if (action.Key == AccessibilityActions.HexGridFocusCenterTile.Key)
            {
                return MoveToCenterTile();
            }

            if (action.Key == AccessibilityActions.HexGridSkipSouthEast.Key)
            {
                return SkipMove(point => GetDiagonalNeighbor(point, north: false, east: true));
            }

            if (action.Key == AccessibilityActions.CombatInspect.Key)
            {
                if (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)
                {
                    return true;
                }

                return EnterInspect();
            }

            if (action.Key == AccessibilityActions.CombatNextActingTroop.Key)
            {
                CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
                return screen != null && screen.NavigateLocalActingTroop(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousActingTroop.Key)
            {
                CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
                return screen != null && screen.NavigateLocalActingTroop(-1);
            }

            if (action.Key == AccessibilityActions.CombatFocusActingTroop.Key)
            {
                CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
                return screen != null && screen.FocusActingTroop();
            }

            if (action.Key == AccessibilityActions.CombatNextEnemyTroop.Key)
            {
                CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
                return screen != null && screen.NavigateEnemyActingTroop(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousEnemyTroop.Key)
            {
                CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
                return screen != null && screen.NavigateEnemyActingTroop(-1);
            }

            if (action.Key == AccessibilityActions.CombatNextRelevantTile.Key)
            {
                return MoveOrdered(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousRelevantTile.Key)
            {
                return MoveOrdered(-1);
            }

            if (action.Key == AccessibilityActions.CombatFocusTimeline.Key)
            {
                CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
                return screen != null && screen.FocusTimeline();
            }

            if (action.Key == AccessibilityActions.ReadThreat.Key)
            {
                return ReadThreat();
            }

            if (action.Key == AccessibilityActions.MapSecondaryAction.Key)
            {
                _adapter?.HandleSecondaryAction(_cursor);
                UIManager.SetFocusedWidget(this);
                return true;
            }

            if (action.Key == AccessibilityActions.Activate.Key && _adapter != null)
            {
                CombatTargetingMode mode = _adapter.GetTargetingMode();
                bool handled = mode == CombatTargetingMode.Spell
                    ? _adapter.ConfirmSpellTarget(_cursor)
                    : mode == CombatTargetingMode.Ability && _adapter.ConfirmAbilityTarget(_cursor);
                UIManager.SetFocusedWidget(this);
                return handled;
            }

            if (action.Key == AccessibilityActions.Cancel.Key)
            {
                if (_adapter != null && _adapter.GetTargetingMode() == CombatTargetingMode.Spell && _adapter.CancelSpellTargeting())
                {
                    UIManager.SetFocusedWidget(this);
                    return true;
                }

                if (_adapter != null && _adapter.GetTargetingMode() == CombatTargetingMode.Ability && _adapter.CancelAbilityTargeting())
                {
                    UIManager.SetFocusedWidget(this);
                    return true;
                }

                return ExitInspect();
            }

            return false;
        }

        private bool CanNavigateLocalActingTroops()
        {
            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            return screen != null && screen.CanNavigateLocalActingTroops();
        }

        private bool CanNavigateEnemyActingTroops()
        {
            CombatScreen screen = SocAccessPlugin.Instance?.ScreenManager?.CurrentScreen as CombatScreen;
            return screen != null && screen.CanNavigateEnemyActingTroops();
        }

        protected override void OnFocus()
        {
            FocusCurrentTile(updateNativeFocus: _inspectContext == null);
        }

        protected override void OnUnfocus()
        {
            _adapter?.ClearFocusedTileOverlay();
        }

        public bool MoveToActingTroop(Vector2Int point)
        {
            return MoveToTroop(point);
        }

        public bool MoveToTroop(Vector2Int point)
        {
            RefreshSnapshot();
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                return false;
            }

            if (_inspectContext != null)
            {
                ExitInspect();
            }

            _cursor = point;
            FocusCurrentTile(updateNativeFocus: true);
            return true;
        }

        private bool MoveToCenterTile()
        {
            RefreshSnapshot();
            if (_snapshot == null || !_snapshot.IsValidTile(CenterTile))
            {
                return true;
            }

            if (_inspectContext != null)
            {
                ExitInspect();
            }

            if (_cursor == CenterTile)
            {
                FocusCurrentTile(updateNativeFocus: true);
                return true;
            }

            _cursor = CenterTile;
            FocusCurrentTile(updateNativeFocus: true);
            return true;
        }

        private bool Move(int xDelta, int yDelta)
        {
            return SetCursor(new Vector2Int(_cursor.x + xDelta, _cursor.y + yDelta));
        }

        private bool MoveDiagonal(bool north, bool east)
        {
            return SetCursor(GetDiagonalNeighbor(_cursor, north, east));
        }

        private bool SkipMove(System.Func<Vector2Int, Vector2Int> step)
        {
            RefreshSnapshot();
            TileSkipResult result = TileSkipNavigator.FindTarget(
                _cursor,
                step,
                IsValidSkipTile,
                point => CombatTileSkipSignature.FromTile(_snapshot != null ? _snapshot.Get(point) : null));
            if (result.Target == _cursor)
            {
                return true;
            }

            SpeakSkipped(result.SkippedCount);
            return SetCursor(result.Target);
        }

        private bool IsValidSkipTile(Vector2Int point)
        {
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                return false;
            }

            return _inspectContext == null || _inspectContext.Contains(point);
        }

        private static Vector2Int GetDiagonalNeighbor(Vector2Int point, bool north, bool east)
        {
            int yDelta = north ? 1 : -1;
            int xDelta;
            if ((point.y & 1) == 0)
            {
                xDelta = east ? 0 : -1;
            }
            else
            {
                xDelta = east ? 1 : 0;
            }

            return new Vector2Int(point.x + xDelta, point.y + yDelta);
        }

        private bool ReadThreat()
        {
            CombatTile tile = GetFocusedTile();
            string text = _adapter != null && tile != null
                ? _adapter.DescribeEnemyInfluenceForSpeech(tile.Point, tile.Troop)
                : string.Empty;
            if (!string.IsNullOrWhiteSpace(text))
            {
                SpeechPipeline.Output(new SpeechRequest(text, interrupt: false));
            }

            return true;
        }

        private bool EnterInspect()
        {
            if (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)
            {
                return true;
            }

            if (_inspectContext != null)
            {
                return true;
            }

            CombatInspectContext context = _adapter != null ? _adapter.BeginInspect(_cursor) : null;
            if (context == null)
            {
                return true;
            }

            _inspectContext = context;
            _inspectContext.FinalizeOrdering();
            _componentWarningSpoken = false;
            SpeakInspectStarted(_inspectContext);
            MaybeSpeakDisconnectedWarning();
            FocusCurrentTile(updateNativeFocus: false);
            return true;
        }

        private bool ExitInspect()
        {
            if (_inspectContext == null)
            {
                return false;
            }

            Vector2Int inspectedTile = _inspectContext.PinnedTile;
            _cursor = inspectedTile;
            _inspectContext = null;
            _componentWarningSpoken = false;
            _adapter?.ExitInspect(_cursor);
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.ExitedInspectMode), interrupt: false));
            FocusCurrentTile(updateNativeFocus: true);
            return true;
        }

        private bool MoveOrdered(int delta)
        {
            if (_inspectContext == null)
            {
                return true;
            }

            _inspectContext.FinalizeOrdering();
            List<Vector2Int> ordered = _inspectContext.OrderedTiles;
            if (ordered == null || ordered.Count == 0)
            {
                return true;
            }

            int currentIndex = 0;
            for (int i = 0; i < ordered.Count; i++)
            {
                if (ordered[i] == _cursor)
                {
                    currentIndex = i;
                    break;
                }
            }

            int nextIndex = Mod(currentIndex + delta, ordered.Count);
            return SetCursor(ordered[nextIndex]);
        }

        private bool SetCursor(Vector2Int point)
        {
            if (_snapshot == null || !_snapshot.IsValidTile(point))
            {
                return true;
            }

            if (_inspectContext != null
                && (_adapter == null || _adapter.GetTargetingMode() == CombatTargetingMode.None)
                && !_inspectContext.Contains(point))
            {
                return true;
            }

            if (point == _cursor)
            {
                return true;
            }

            _cursor = point;
            FocusCurrentTile(updateNativeFocus: _inspectContext == null);
            return true;
        }

        private bool JumpToScannerResult(Vector2Int point)
        {
            return SetCursor(point);
        }

        private bool HandleScannerAction(InputAction action)
        {
            if (action.Key == AccessibilityActions.ScannerRefresh.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteRefresh());
            }

            if (action.Key == AccessibilityActions.ScannerPreviousCategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveCategory(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextCategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveCategory(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousSubcategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveSubcategory(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextSubcategory.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveSubcategory(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousResult.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveResult(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextResult.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveResult(1));
            }

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key)
            {
                return _scanner.JumpToCurrent();
            }

            if (action.Key == AccessibilityActions.ScannerSpeakOrientation.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteSpeakOrientation());
            }

            return false;
        }

        private bool HandleScannerNavigationResult(ScannerCommandResult result)
        {
            if (result != null && result.Status == ScannerCommandStatus.Result && result.Wrapped)
            {
                NativeSoundUtility.PostEvent(ScannerWrapCueKey);
            }

            if (result != null && result.Status == ScannerCommandStatus.Result && result.Result != null)
            {
                ScannerDirectionalBeepAudio.Play(_cursor, result.Result.Position, DirectionalBeepGridGeometry.Hex);
            }

            _scanner.Output(result);
            return true;
        }

        private static bool IsScannerAction(string actionKey)
        {
            return actionKey == AccessibilityActions.ScannerRefresh.Key
                || actionKey == AccessibilityActions.ScannerPreviousCategory.Key
                || actionKey == AccessibilityActions.ScannerNextCategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousSubcategory.Key
                || actionKey == AccessibilityActions.ScannerNextSubcategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousResult.Key
                || actionKey == AccessibilityActions.ScannerNextResult.Key
                || actionKey == AccessibilityActions.ScannerJumpToResult.Key
                || actionKey == AccessibilityActions.ScannerSpeakOrientation.Key;
        }

        private void FocusCurrentTile(bool updateNativeFocus)
        {
            RefreshSnapshot();
            if (_adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None)
            {
                if (_inspectContext != null)
                {
                    ExitInspect();
                    return;
                }

                _adapter.FocusTargetTile(_cursor);
            }
            else if (updateNativeFocus)
            {
                _adapter?.FocusTile(_cursor);
            }

            _adapter?.SetFocusedTileOverlay(_cursor);
            UIManager.SetFocusedWidget(this);
        }

        public void HandleTargetingBegin()
        {
            if (_inspectContext != null)
            {
                ExitInspect();
            }

            FocusCurrentTile(updateNativeFocus: true);
        }

        private void MaybeSpeakDisconnectedWarning()
        {
            if (_componentWarningSpoken || _inspectContext == null || _inspectContext.Mode != CombatInspectMode.Stack)
            {
                return;
            }

            if (_inspectContext.CountConnectedComponents() > 1)
            {
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.CombatDisconnectedTiles), interrupt: false));
                _componentWarningSpoken = true;
            }
        }

        private static void SpeakInspectStarted(CombatInspectContext context)
        {
            string target = context != null ? context.TargetLabel : null;
            if (string.IsNullOrWhiteSpace(target))
            {
                target = ModText.Get(ModStrings.UI.Target);
            }

            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.UI.Inspecting, target), interrupt: false));
        }

        private static void SpeakSkipped(int skippedCount)
        {
            if (skippedCount <= 0)
            {
                return;
            }

            SpeechPipeline.Output(new SpeechRequest(
                ModText.Plural(ModStrings.Spatial.SkippedTileCount, skippedCount, skippedCount),
                interrupt: false));
        }

        private void RefreshSnapshot()
        {
            _snapshot = _adapter != null ? _adapter.BuildSnapshot() : null;
        }

        private CombatTile GetFocusedTile()
        {
            return _snapshot != null ? _snapshot.Get(_cursor) : null;
        }

        private CombatInspectContext GetEffectiveInspectContext()
        {
            return _adapter != null && _adapter.GetTargetingMode() != CombatTargetingMode.None ? null : _inspectContext;
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

    }
}
