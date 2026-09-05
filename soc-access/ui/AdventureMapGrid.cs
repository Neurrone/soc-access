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
    public sealed class AdventureMapGrid : Widget
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
        private readonly ScannerJumpAnchor _jumpAnchor = new ScannerJumpAnchor();
        private int _lookAroundRadius = DefaultLookAroundRadius;
        private bool _tileCuesHandled;

        public AdventureMapGrid(AdventureMapAdapter adapter)
            : base("adventure_map_grid")
        {
            _adapter = adapter;
            _cursorTile = adapter != null ? adapter.GetInitialTile() : Vector2Int.zero;
            _bookmarks = new AdventureBookmarkManager(new AdventureBookmarkStore());
            _beacons = new AdventureBeaconAudio();
            HydrateBookmarks();
            _scanner = new ScannerController(
                origin => ScannerCustomCategorySynthesizer.ApplyFromSettings(
                    _adapter != null ? _adapter.BuildScannerSnapshot(origin) : null),
                () => _cursorTile,
                (result, cursorHint) => _adapter != null
                    ? _adapter.TryRefreshScannerResult(result, cursorHint)
                    : ScannerResultRefresh.Invalid,
                JumpToScannerResult,
                (result, directions, index, count, includeItemName) => new AdventureScannerSpeechContext(
                    result,
                    _adapter.GetTile(result.Position),
                    directions,
                    index,
                    count,
                    includeItemName),
                ScannerDirectionMode.Square);
        }

        public override string GetRole()
        {
            return string.Empty;
        }

        public override string GetAnnouncementKey()
        {
            return _cursorTile.ToString();
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

        public Vector2Int CursorTile
        {
            get { return _cursorTile; }
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
                || actionKey == AccessibilityActions.DescribePosition.Key
                || actionKey == AccessibilityActions.SonarSweep.Key
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

            if (action.Key == AccessibilityActions.DescribePosition.Key)
            {
                return SpeakPosition();
            }

            if (action.Key == AccessibilityActions.SonarSweep.Key)
            {
                return PlaySonarSweep();
            }

            return false;
        }

        protected override void OnFocus()
        {
            _adapter?.SetFocusedTileOverlay(_cursorTile);

            // Focus arrival announces the current tile, so it gets a cue too. Paths that already
            // cued while claiming focus mark the arrival handled so it only sounds once.
            if (!_tileCuesHandled)
            {
                PlayTileCues();
            }
        }

        protected override void OnUnfocus()
        {
            _tileCuesHandled = false;
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
            if (updateUiManager)
            {
                PlayTileCues();
            }
            else
            {
                // Silent variant: no announcement, so no cue, and the focus commit it still
                // triggers must not produce one either.
                _tileCuesHandled = true;
            }

            return true;
        }

        private void PlayTileCues()
        {
            _tileCuesHandled = true;
            PlayTileCuesFor(_cursorTile, 0f, 1f, 0f);
        }

        private void PlayTileCuesFor(Vector2Int point, float panOffset, float gainScale, float semitoneOffset)
        {
            if (_adapter == null)
            {
                return;
            }

            CueLibrary.PlayCues(
                TileCueSelector.ForAdventureTile(_adapter.GetTile(point)),
                panOffset,
                gainScale,
                semitoneOffset);
        }

        /// <summary>
        /// The result's own cues carry the direction to it; out of range plays nothing. A
        /// categorised entry sounds exactly as the sonar sweep pings it; terrain groups,
        /// unexplored regions and zones of control fall back to the tile they land on.
        /// </summary>
        private void PlayDirectionalTileCues(Vector2Int origin, ScannerResult result)
        {
            float pan;
            float semitones;
            float gainScale;
            if (!DirectionalCueMath.TryCompute(origin, result.Position, CueGridGeometry.Square, out pan, out semitones, out gainScale))
            {
                return;
            }

            IReadOnlyList<TileCue> cues = SweepSelector.ForScannerResult(result, point => _adapter.GetTile(point));
            if (cues.Count == 0)
            {
                PlayTileCuesFor(result.Position, pan, gainScale, semitones);
                return;
            }

            CueLibrary.PlayCues(cues, pan, gainScale, semitones);
        }

        /// <summary>Pings every scanner-visible entity within the look-around radius, west to east.
        /// Speech is untouched, and an empty sweep is silent because silence is the accurate answer.</summary>
        private bool PlaySonarSweep()
        {
            ScannerSnapshot lookAround = ScannerLookAround.Build(
                _adapter.BuildScannerSnapshot(_cursorTile),
                _cursorTile,
                _lookAroundRadius);
            SweepPlayer.Start(
                SweepSelector.ForLookAround(lookAround, point => _adapter.GetTile(point)),
                _cursorTile,
                CueGridGeometry.Square);
            return true;
        }

        private bool Move(int xDelta, int yDelta)
        {
            Vector2Int nextTile = _adapter.Move(_cursorTile, xDelta, yDelta);
            if (nextTile == _cursorTile)
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            _cursorTile = nextTile;
            _adapter.EnsureTileInView(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            PlayTileCues();
            return true;
        }

        private bool SkipMove(int xDelta, int yDelta)
        {
            HydrateBookmarks();
            // Asked once for the whole sweep rather than per tile: a skip walks a whole row, and
            // with road directions turned off no tile is ever asked to work its forks out.
            bool stopsAtRoadForks = ModSettings.GetAnnouncementElementEnabled(
                AdventureMapAnnouncementDefinitions.Tile,
                AdventureMapAnnouncementDefinitions.RoadDirectionsElement);
            TileSkipResult result = TileSkipNavigator.FindTarget(
                _cursorTile,
                point => new Vector2Int(point.x + xDelta, point.y + yDelta),
                point => _adapter != null && _adapter.IsValidMapTile(point),
                point => AdventureTileSkipSignature.FromTile(_adapter.GetTile(point), HasBookmark(point), stopsAtRoadForks));
            if (result.Target == _cursorTile)
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                return true;
            }

            SpeakSkipped(result.SkippedCount);
            _cursorTile = result.Target;
            _adapter.EnsureTileInView(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            PlayTileCues();
            return true;
        }

        private bool JumpToScannerResult(Vector2Int point)
        {
            if (_adapter == null)
            {
                return false;
            }

            if (point == _cursorTile)
            {
                SpeakHere();
                return true;
            }

            _jumpAnchor.Remember(_cursorTile);
            _cursorTile = point;
            _adapter.MoveCameraToTile(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            PlayTileCues();
            return true;
        }

        private bool JumpToBookmark(Vector2Int point)
        {
            if (_adapter == null || !_adapter.IsValidMapTile(point))
            {
                return false;
            }

            if (point == _cursorTile)
            {
                SpeakHere();
                return true;
            }

            _jumpAnchor.Remember(_cursorTile);
            _cursorTile = point;
            _adapter.MoveCameraToTile(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            PlayTileCues();
            return true;
        }

        private bool HandleScannerAction(InputAction action)
        {
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

            if (action.Key == AccessibilityActions.ScannerPreviousItem.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveItem(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextItem.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveItem(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousInstance.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveInstance(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextInstance.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteMoveInstance(1));
            }

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key)
            {
                return _scanner.JumpToCurrent();
            }

            if (action.Key == AccessibilityActions.ScannerSpeakDistanceAndDirection.Key)
            {
                return HandleScannerNavigationResult(_scanner.ExecuteSpeakDistanceAndDirection());
            }

            if (action.Key == AccessibilityActions.ScannerReturnFromJump.Key)
            {
                return ReturnFromJump();
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

            ScannerQuickKey quickKey;
            int delta;
            if (TryGetCustomEntryKey(action.Key, out quickKey, out delta))
            {
                return MoveCustomCategoryEntry(quickKey, delta);
            }

            return false;
        }

        /// <summary>
        /// Steps the custom category the player put on this key. A key nobody
        /// has taken says so rather than falling silent, because a dead
        /// keypress reads as the mod having missed it.
        /// </summary>
        private bool MoveCustomCategoryEntry(ScannerQuickKey quickKey, int delta)
        {
            ScannerCustomCategory category = ModSettings.GetScannerCustomCategoryByQuickKey(
                ScannerTaxonomyKeys.Adventure,
                quickKey);
            if (category == null)
            {
                SpeechPipeline.Output(new SpeechRequest(
                    ModText.Get(ModStrings.Scanner.NoCustomCategoryOnKey, ScannerQuickKeyText.Name(quickKey)),
                    interrupt: false));
                return true;
            }

            return HandleScannerNavigationResult(_scanner.ExecuteMoveCustomCategoryEntry(
                ScannerCustomCategorySynthesizer.CategoryKeyFor(category.Id),
                delta));
        }

        private static bool TryGetCustomEntryKey(string actionKey, out ScannerQuickKey quickKey, out int delta)
        {
            if (actionKey == AccessibilityActions.ScannerNextCustomEntryComma.Key)
            {
                quickKey = ScannerQuickKey.Comma;
                delta = 1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerPreviousCustomEntryComma.Key)
            {
                quickKey = ScannerQuickKey.Comma;
                delta = -1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerNextCustomEntryPeriod.Key)
            {
                quickKey = ScannerQuickKey.Period;
                delta = 1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerPreviousCustomEntryPeriod.Key)
            {
                quickKey = ScannerQuickKey.Period;
                delta = -1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerNextCustomEntrySlash.Key)
            {
                quickKey = ScannerQuickKey.Slash;
                delta = 1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerPreviousCustomEntrySlash.Key)
            {
                quickKey = ScannerQuickKey.Slash;
                delta = -1;
                return true;
            }

            quickKey = ScannerQuickKey.None;
            delta = 0;
            return false;
        }

        private bool ReturnFromJump()
        {
            Vector2Int anchor;
            if (!_jumpAnchor.TryTake(out anchor) || _adapter == null || !_adapter.IsValidMapTile(anchor))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                SpeechPipeline.Output(new SpeechRequest(
                    ModText.Get(ModStrings.Scanner.NoTileToReturnTo),
                    interrupt: false));
                return true;
            }

            _cursorTile = anchor;
            _adapter.MoveCameraToTile(_cursorTile);
            _adapter.SetFocusedTileOverlay(_cursorTile);
            UIManager.SetFocusedWidget(this);
            _beacons.UpdateListener(_cursorTile);
            PlayTileCues();
            return true;
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

        /// <summary>
        /// A jump onto the tile the cursor already occupies moves nothing, so the
        /// tile announcement is dropped as a repeat of the one just spoken. Say
        /// where the player is rather than letting the key fall silent.
        /// </summary>
        private static void SpeakHere()
        {
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Spatial.Here), interrupt: false));
        }

        private bool OpenScannerSearch()
        {
            ISystemPopups systemPopups = _adapter != null ? _adapter.SystemPopups : null;
            if (systemPopups == null)
            {
                SocAccessMod.Instance?.LogWarning("Scanner search could not open because system popups are unavailable");
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
                PlayDirectionalTileCues(origin, result.Result);
            }

            _scanner.Output(result);
            return true;
        }

        private bool SpeakPosition()
        {
            Vector2Int mapSize = _adapter.GetMapSize();
            SpeechPipeline.Output(new SpeechRequest(
                ModText.Get(ModStrings.Spatial.PositionAndMapSize, _cursorTile.x, _cursorTile.y, mapSize.x, mapSize.y),
                interrupt: false));
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
            return actionKey == AccessibilityActions.ScannerSearch.Key
                || actionKey == AccessibilityActions.ScannerPreviousCategory.Key
                || actionKey == AccessibilityActions.ScannerNextCategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousSubcategory.Key
                || actionKey == AccessibilityActions.ScannerNextSubcategory.Key
                || actionKey == AccessibilityActions.ScannerPreviousItem.Key
                || actionKey == AccessibilityActions.ScannerNextItem.Key
                || actionKey == AccessibilityActions.ScannerPreviousInstance.Key
                || actionKey == AccessibilityActions.ScannerNextInstance.Key
                || actionKey == AccessibilityActions.ScannerJumpToResult.Key
                || actionKey == AccessibilityActions.ScannerSpeakDistanceAndDirection.Key
                || actionKey == AccessibilityActions.ScannerReturnFromJump.Key
                || actionKey == AccessibilityActions.ScannerLookAround.Key
                || actionKey == AccessibilityActions.ScannerIncreaseLookAroundRadius.Key
                || actionKey == AccessibilityActions.ScannerDecreaseLookAroundRadius.Key
                || IsCustomEntryAction(actionKey);
        }

        private static bool IsCustomEntryAction(string actionKey)
        {
            ScannerQuickKey quickKey;
            int delta;
            return TryGetCustomEntryKey(actionKey, out quickKey, out delta);
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
