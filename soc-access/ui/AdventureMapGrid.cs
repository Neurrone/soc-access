using System;
using System.Collections.Generic;
using Lavapotion.Utilities;
using SongsOfConquest.Client;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class AdventureMapGrid : Widget
    {
        private const string ScannerWrapCueKey = "Common_ClickUnfold";
        private const int DefaultLookAroundRadius = 15;
        private const int MinimumLookAroundRadius = 5;
        private const int MaximumLookAroundRadius = 30;
        private const int LookAroundRadiusStep = 5;

        private readonly AdventureMapAdapter _adapter;
        private Vector2Int _cursorTile;
        private readonly ScannerController _scanner;
        private readonly AdventureBookmarkManager _bookmarks;
        private readonly AdventureBeaconAudio _beacons;
        private int _lookAroundRadius = DefaultLookAroundRadius;

        public AdventureMapGrid(AdventureMapAdapter adapter)
            : base("adventure_map_grid")
        {
            _adapter = adapter;
            _cursorTile = adapter != null ? adapter.GetInitialTile() : Vector2Int.zero;
            _bookmarks = new AdventureBookmarkManager(new AdventureBookmarkStore());
            _beacons = new AdventureBeaconAudio();
            HydrateBookmarks();
            _scanner = new ScannerController(
                origin => _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null,
                () => _cursorTile,
                result => _adapter != null && _adapter.ValidateScannerResult(result),
                JumpToScannerResult,
                (result, directions, index, count) => new AdventureScannerSpeechContext(
                    result,
                    _adapter.GetTile(result.Position),
                    directions,
                    index,
                    count),
                ScannerDirectionMode.Square);
        }

        public override string GetRole()
        {
            return string.Empty;
        }

        public override string GetLabel()
        {
            AdventureMapTile tile = _adapter != null ? _adapter.GetTile(_cursorTile) : null;
            return new AdventureMapTileSpeechFormatter().DescribeTile(tile);
        }

        public override Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetTooltip(_cursorTile) : null;
        }

        public override bool ClaimsAction(string actionKey)
        {
            // TODO: Enter on an already-selected wielder should eventually open an
            // accessible selected-wielder HUD screen. That is explicitly outside
            // the initial adventure-map interaction scope; see wielders.md.
            return actionKey == AccessibilityActions.MapMoveNorth.Key
                || actionKey == AccessibilityActions.MapMoveSouth.Key
                || actionKey == AccessibilityActions.MapMoveWest.Key
                || actionKey == AccessibilityActions.MapMoveEast.Key
                || actionKey == AccessibilityActions.MapSkipNorth.Key
                || actionKey == AccessibilityActions.MapSkipSouth.Key
                || actionKey == AccessibilityActions.MapSkipWest.Key
                || actionKey == AccessibilityActions.MapSkipEast.Key
                || actionKey == AccessibilityActions.Activate.Key
                || actionKey == AccessibilityActions.MapSecondaryAction.Key
                || actionKey == AccessibilityActions.NextWielder.Key
                || actionKey == AccessibilityActions.NextSettlement.Key
                || actionKey == AccessibilityActions.SummarizeReachableEntities.Key
                || IsBookmarkAction(actionKey)
                || IsBeaconAction(actionKey)
                || IsScannerAction(actionKey);
        }

        public override bool HandleAction(InputAction action)
        {
            if (action == null || _adapter == null)
            {
                return false;
            }

            if (HandleScannerAction(action))
            {
                return true;
            }

            if (HandleBookmarkAction(action))
            {
                return true;
            }

            if (action.Key == AccessibilityActions.MapMoveNorth.Key)
            {
                return Move(0, 1);
            }

            if (action.Key == AccessibilityActions.MapSkipNorth.Key)
            {
                return SkipMove(0, 1);
            }

            if (action.Key == AccessibilityActions.MapMoveSouth.Key)
            {
                return Move(0, -1);
            }

            if (action.Key == AccessibilityActions.MapSkipSouth.Key)
            {
                return SkipMove(0, -1);
            }

            if (action.Key == AccessibilityActions.MapMoveWest.Key)
            {
                return Move(-1, 0);
            }

            if (action.Key == AccessibilityActions.MapSkipWest.Key)
            {
                return SkipMove(-1, 0);
            }

            if (action.Key == AccessibilityActions.MapMoveEast.Key)
            {
                return Move(1, 0);
            }

            if (action.Key == AccessibilityActions.MapSkipEast.Key)
            {
                return SkipMove(1, 0);
            }

            if (action.Key == AccessibilityActions.Activate.Key)
            {
                return _adapter.HandlePrimaryAction(_cursorTile);
            }

            if (action.Key == AccessibilityActions.MapSecondaryAction.Key)
            {
                return _adapter.HandleSecondaryAction(_cursorTile);
            }

            if (action.Key == AccessibilityActions.NextWielder.Key)
            {
                return _adapter.TrySelectNextWielder();
            }

            if (action.Key == AccessibilityActions.NextSettlement.Key)
            {
                return _adapter.TrySelectNextSettlement();
            }

            if (action.Key == AccessibilityActions.SummarizeReachableEntities.Key)
            {
                return SpeakReachableEntities();
            }

            return false;
        }

        protected override void OnFocus()
        {
            _adapter?.SetFocusedTileOverlay(_cursorTile);
        }

        protected override void OnUnfocus()
        {
            _adapter?.ClearFocusedTileOverlay();
        }

        public void SetBeaconAudible(bool isAudible)
        {
            _beacons.SetAudible(isAudible, _cursorTile);
        }

        public void DisposeAudio()
        {
            _beacons.Dispose();
        }

        public bool FocusTile(Vector2Int tile)
        {
            return FocusTile(tile, updateUiManager: true);
        }

        public bool FocusTileSilently(Vector2Int tile)
        {
            return FocusTile(tile, updateUiManager: false);
        }

        private bool FocusTile(Vector2Int tile, bool updateUiManager)
        {
            if (_adapter == null)
            {
                return false;
            }

            _cursorTile = tile;
            _adapter.SetFocusedTileOverlay(_cursorTile);
            if (updateUiManager)
            {
                UIManager.SetFocusedWidget(this);
            }

            _beacons.UpdateListener(_cursorTile);
            return true;
        }

        private bool Move(int xDelta, int yDelta)
        {
            Vector2Int nextTile = _adapter.Move(_cursorTile, xDelta, yDelta);
            if (nextTile == _cursorTile)
            {
                return true;
            }

            _cursorTile = nextTile;
            _adapter.EnsureTileInView(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            return true;
        }

        private bool SkipMove(int xDelta, int yDelta)
        {
            HydrateBookmarks();
            TileSkipResult result = TileSkipNavigator.FindTarget(
                _cursorTile,
                point => new Vector2Int(point.x + xDelta, point.y + yDelta),
                point => _adapter != null && _adapter.IsValidMapTile(point),
                point => AdventureTileSkipSignature.FromTile(_adapter.GetTile(point), HasBookmark(point)));
            if (result.Target == _cursorTile)
            {
                return true;
            }

            SpeakSkipped(result.SkippedCount);
            _cursorTile = result.Target;
            _adapter.EnsureTileInView(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            return true;
        }

        private bool JumpToScannerResult(Vector2Int point)
        {
            if (_adapter == null)
            {
                return false;
            }

            _cursorTile = point;
            _adapter.MoveCameraToTile(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            return true;
        }

        private bool JumpToBookmark(Vector2Int point)
        {
            if (_adapter == null || !_adapter.IsValidMapTile(point))
            {
                return false;
            }

            _cursorTile = point;
            _adapter.MoveCameraToTile(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            return true;
        }

        private bool HandleScannerAction(InputAction action)
        {
            if (action.Key == AccessibilityActions.ScannerRefresh.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteRefresh());
            }

            if (action.Key == AccessibilityActions.ScannerSearch.Key)
            {
                return OpenScannerSearch();
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

            if (action.Key == AccessibilityActions.ScannerLookAround.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteLookAround(_lookAroundRadius));
            }

            if (action.Key == AccessibilityActions.ScannerIncreaseLookAroundRadius.Key)
            {
                return ChangeLookAroundRadius(LookAroundRadiusStep);
            }

            if (action.Key == AccessibilityActions.ScannerDecreaseLookAroundRadius.Key)
            {
                return ChangeLookAroundRadius(-LookAroundRadiusStep);
            }

            return false;
        }

        private bool ChangeLookAroundRadius(int delta)
        {
            int next = _lookAroundRadius + delta;
            if (next < MinimumLookAroundRadius || next > MaximumLookAroundRadius)
            {
                return true;
            }

            _lookAroundRadius = next;
            SpeechPipeline.Output(new SpeechRequest(
                ModText.Get(ModStrings.Scanner.LookAroundRadius, _lookAroundRadius),
                interrupt: false));
            return true;
        }

        private bool HandleBookmarkAction(InputAction action)
        {
            string slot;
            if (TryGetBookmarkSlot(action, AccessibilityActions.ToggleBookmarkBeacons, out slot))
            {
                HydrateBookmarks();
                Vector2Int point;
                if (!_bookmarks.TryGet(slot, out point) || _adapter == null || !_adapter.IsValidMapTile(point))
                {
                    SpeakNoBookmark();
                    return true;
                }

                bool activated = _beacons.Toggle(slot, point, _cursorTile);
                ModString message = activated
                    ? ModStrings.Bookmarks.BeaconActivated
                    : ModStrings.Bookmarks.BeaconDeactivated;
                SpeechPipeline.Output(new SpeechRequest(ModText.Get(message, slot), interrupt: false));
                return true;
            }

            if (TryGetBookmarkSlot(action, AccessibilityActions.SaveBookmarks, out slot))
            {
                HydrateBookmarks();
                SpeechPipeline.Output(new SpeechRequest(_bookmarks.Save(slot, _cursorTile), interrupt: false));
                if (_beacons.IsActive(slot))
                {
                    _beacons.Start(slot, _cursorTile, _cursorTile);
                }

                return true;
            }

            if (TryGetBookmarkSlot(action, AccessibilityActions.JumpToBookmarks, out slot))
            {
                HydrateBookmarks();
                Vector2Int point;
                if (!_bookmarks.TryGet(slot, out point) || !JumpToBookmark(point))
                {
                    SpeakNoBookmark();
                }

                return true;
            }

            if (TryGetBookmarkSlot(action, AccessibilityActions.SpeakBookmarkDirections, out slot))
            {
                HydrateBookmarks();
                Vector2Int point;
                if (!_bookmarks.TryGet(slot, out point) || _adapter == null || !_adapter.IsValidMapTile(point))
                {
                    SpeakNoBookmark();
                    return true;
                }

                string directions = ScannerSpeechUtility.FormatDirections(
                    ScannerDirectionUtility.BuildSquareDirections(_cursorTile, point));
                SpeechPipeline.Output(new SpeechRequest(directions, interrupt: false));
                return true;
            }

            return false;
        }

        private void HydrateBookmarks()
        {
            _bookmarks.EnsureLoaded(_adapter != null ? _adapter.GetBookmarkGameIdentity() : null);
        }

        private static void SpeakNoBookmark()
        {
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Bookmarks.NoBookmark), interrupt: false));
        }

        private bool OpenScannerSearch()
        {
            ISystemPopups systemPopups = _adapter != null ? _adapter.SystemPopups : null;
            if (systemPopups == null)
            {
                SocAccessPlugin.Instance?.LogWarning("Scanner search could not open because system popups are unavailable");
                return false;
            }

            string search = ModText.Get(ModStrings.Scanner.Search);
            string cancel = GameText.Get(_adapter.LocalizationHandler, "Common/Cancel", "Cancel");
            systemPopups
                .AskForInput(
                    search,
                    string.Empty,
                    search,
                    cancel,
                    null,
                    InputFieldContentType.Standard,
                    InputLevel.Popup)
                .Then((Action<AsyncResponse>)HandleScannerSearchResponse);
            return true;
        }

        private void HandleScannerSearchResponse(AsyncResponse response)
        {
            if (!response.Success)
            {
                return;
            }

            HandleScannerNavigationResult(_scanner.ExecuteSearch(response.Message));
        }

        private bool HandleScannerNavigationResult(ScannerCommandResult result)
        {
            if (result != null && result.Status == ScannerCommandStatus.Result && result.Wrapped)
            {
                NativeSoundUtility.PostEvent(ScannerWrapCueKey);
            }

            if (result != null && result.Status == ScannerCommandStatus.Result && result.Result != null)
            {
                Vector2Int origin = result.HasOrigin ? result.Origin : _cursorTile;
                ScannerDirectionalBeepAudio.Play(origin, result.Result.Position, DirectionalBeepGridGeometry.Square);
            }

            _scanner.Output(result);
            return true;
        }

        private bool SpeakReachableEntities()
        {
            IReadOnlyList<ReachableAdventureEntity> entities = _adapter.GetReachableAdventureEntities();
            SpeechPipeline.Output(new SpeechRequest(ReachableAdventureEntitySummaryFormatter.Format(entities), interrupt: false));
            return true;
        }

        private bool HasBookmark(Vector2Int point)
        {
            Vector2Int bookmark;
            for (int i = 0; i < AdventureBookmarkSlots.All.Length; i++)
            {
                if (_bookmarks.TryGet(AdventureBookmarkSlots.All[i], out bookmark) && bookmark == point)
                {
                    return true;
                }
            }

            return false;
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

        private static bool IsScannerAction(string actionKey)
        {
            return actionKey == AccessibilityActions.ScannerRefresh.Key
                || actionKey == AccessibilityActions.ScannerSearch.Key
                || actionKey == AccessibilityActions.ScannerPreviousCategory.Key
                || actionKey == AccessibilityActions.ScannerNextCategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousSubcategory.Key
                || actionKey == AccessibilityActions.ScannerNextSubcategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousResult.Key
                || actionKey == AccessibilityActions.ScannerNextResult.Key
                || actionKey == AccessibilityActions.ScannerJumpToResult.Key
                || actionKey == AccessibilityActions.ScannerSpeakOrientation.Key
                || actionKey == AccessibilityActions.ScannerLookAround.Key
                || actionKey == AccessibilityActions.ScannerIncreaseLookAroundRadius.Key
                || actionKey == AccessibilityActions.ScannerDecreaseLookAroundRadius.Key;
        }

        private static bool IsBookmarkAction(string actionKey)
        {
            return ContainsActionKey(AccessibilityActions.SaveBookmarks, actionKey)
                || ContainsActionKey(AccessibilityActions.JumpToBookmarks, actionKey)
                || ContainsActionKey(AccessibilityActions.SpeakBookmarkDirections, actionKey);
        }

        private static bool IsBeaconAction(string actionKey)
        {
            return ContainsActionKey(AccessibilityActions.ToggleBookmarkBeacons, actionKey);
        }

        private static bool TryGetBookmarkSlot(InputAction action, InputAction[] actions, out string slot)
        {
            slot = null;
            if (action == null || actions == null)
            {
                return false;
            }

            for (int i = 0; i < actions.Length && i < AdventureBookmarkSlots.All.Length; i++)
            {
                if (actions[i] != null && action.Key == actions[i].Key)
                {
                    slot = AdventureBookmarkSlots.All[i];
                    return true;
                }
            }

            return false;
        }

        private static bool ContainsActionKey(InputAction[] actions, string actionKey)
        {
            if (actions == null || string.IsNullOrWhiteSpace(actionKey))
            {
                return false;
            }

            for (int i = 0; i < actions.Length; i++)
            {
                if (actions[i] != null && actions[i].Key == actionKey)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
