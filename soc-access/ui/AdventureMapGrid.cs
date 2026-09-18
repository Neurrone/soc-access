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
    /// <summary>
    /// The adventure map's TILE CURSOR - the cursor of a mode whose cursor is not the focus cursor
    /// (<c>ui-graph-plan.md</c> phase E). <see cref="AdventureMapScreen"/> declares ONE node for the
    /// whole map and hands this class every key that belongs to the map: the moves, the skips, the
    /// scanner, the bookmarks, the beacons and the sonar sweep. Because the node's identity never
    /// changes as the cursor walks, the navigator has nothing of its own to announce, so a landing
    /// here asks it for the node's readout again (<see cref="GraphNavigator.ReannounceFocused"/>,
    /// wired in as <c>readTile</c>) rather than saying the tile itself: the label alone said from
    /// here would leave out the map node's tooltip and its usage hints.
    ///
    /// Queued rather than interrupting, which is what the widget engine's focus commit did
    /// (<c>UIManager.Update</c> spoke with <c>interrupt: false</c>): the input router has already
    /// silenced the reader for the claimed key, and a skip's "Skipped 3 tiles" is said before the
    /// tile it landed on, which an interrupting landing would cut off.
    /// </summary>
    public sealed class AdventureMapGrid
    {
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

        // The team whose bookmarks the beacons are sounding for. A bookmark file belongs to one team
        // (AdventureBookmarkGameIdentity.FileName), and in hot seat the map is handed from one player
        // to the next without the scene, the adapter or this grid being rebuilt, so a beacon the
        // outgoing player left pinging would go on pointing the incoming one at a tile that is not
        // theirs - and their own slot key, finding no bookmark of that number, could not silence it.
        // The team is read from the game and every beacon stopped when it changes (SyncBeaconTeam).
        private int _beaconTeamId = -1;

        // How a landing is read out: the screen hands the readout to the navigator, which composes
        // the map node exactly as it composes a landing on it from the HUD. Never speech of this
        // class's own - see ReadTile.
        private readonly Action _readTile;

        public AdventureMapGrid(AdventureMapAdapter adapter, Action readTile)
        {
            _adapter = adapter;
            _readTile = readTile;
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
                ScannerDirectionMode.Square,
                _adapter);
        }

        public string GetLabel()
        {
            return Describe(_adapter != null ? _adapter.GetTile(_cursorTile) : null);
        }

        /// <summary>The tile as it is said. Apart here so a caller holding a tile it has already read
        /// - the screen's cached cursor tile - words it exactly as a landing does.</summary>
        public static string Describe(AdventureMapTile tile)
        {
            return new AdventureMapTileSpeechFormatter().DescribeTile(tile);
        }

        public Tooltip GetTooltip()
        {
            return _adapter != null ? _adapter.GetTooltip(_cursorTile) : null;
        }

        public Vector2Int CursorTile
        {
            get { return _cursorTile; }
        }

        /// <summary>
        /// The keys the tile cursor owns, which is exactly what the screen answers
        /// <c>GraphScreen.ModeClaims</c> with while the map node is focused. The two CLICKS are not
        /// here: Enter and Backslash are the map node's own activation and contextual command, so
        /// they reach the tile through the graph rather than through this set.
        /// </summary>
        public bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.MapMoveNorth.Key
                || actionKey == AccessibilityActions.MapMoveSouth.Key
                || actionKey == AccessibilityActions.MapMoveWest.Key
                || actionKey == AccessibilityActions.MapMoveEast.Key
                || actionKey == AccessibilityActions.MapSkipNorth.Key
                || actionKey == AccessibilityActions.MapSkipSouth.Key
                || actionKey == AccessibilityActions.MapSkipWest.Key
                || actionKey == AccessibilityActions.MapSkipEast.Key
                || actionKey == AccessibilityActions.NextWielder.Key
                || actionKey == AccessibilityActions.NextSettlement.Key
                || actionKey == AccessibilityActions.SummarizeReachableEntities.Key
                || actionKey == AccessibilityActions.DescribePosition.Key
                || actionKey == AccessibilityActions.SonarSweep.Key
                || IsBookmarkAction(actionKey)
                || IsBeaconAction(actionKey)
                || IsScannerAction(actionKey);
        }

        public bool HandleAction(InputAction action)
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

        /// <summary>Draw the game's own highlight on the tile the cursor is standing on - what the
        /// screen does when the map node takes the focus.</summary>
        public void ShowOverlay()
        {
            _adapter?.SetFocusedTileOverlay(_cursorTile);
        }

        /// <summary>Take the highlight off again: the focus has gone to a HUD stop or off the
        /// screen.</summary>
        public void HideOverlay()
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

        /// <summary>Put the cursor on a tile and read it, as a move does.</summary>
        public bool FocusTile(Vector2Int tile)
        {
            return FocusTile(tile, announce: true);
        }

        /// <summary>Put the cursor on a tile without a word and without a cue - the player is
        /// somewhere else and the map moved under them.</summary>
        public bool FocusTileSilently(Vector2Int tile)
        {
            return FocusTile(tile, announce: false);
        }

        private bool FocusTile(Vector2Int tile, bool announce)
        {
            if (_adapter == null)
            {
                return false;
            }

            _cursorTile = tile;
            Land(announce);
            return true;
        }

        /// <summary>What every landing does: the highlight, the beacon listener, and - where the
        /// player went there themselves - the tile read out with its cues.</summary>
        private void Land(bool announce)
        {
            _adapter.SetFocusedTileOverlay(_cursorTile);
            _beacons.UpdateListener(_cursorTile);
            if (!announce)
            {
                return;
            }

            ReadTile();
            PlayTileCues();
        }

        /// <summary>
        /// The tile the cursor now stands on, read out - by the NAVIGATOR, deliberately, because the
        /// map node's tooltip and its usage hints are only read where a landing is composed. The
        /// label said from here instead would be the whole readout the player got.
        ///
        /// Called after whatever the same keypress has already said (a skip's "Skipped 3 tiles"),
        /// so the order the player hears is unchanged: the readout is queued behind it rather than
        /// interrupting, exactly as before.
        /// </summary>
        private void ReadTile()
        {
            if (_readTile != null)
            {
                _readTile();
            }
        }

        private void PlayTileCues()
        {
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
            Land(announce: true);
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

            TileSkipNavigator.SpeakSkipped(result.SkippedCount);
            _cursorTile = result.Target;
            _adapter.EnsureTileInView(_cursorTile);
            Land(announce: true);
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
            Land(announce: true);
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
            Land(announce: true);
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

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key
                || action.Key == AccessibilityActions.ScannerJumpToResultAlternate.Key)
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

            int slot;
            int delta;
            if (TryGetCustomEntrySlot(action.Key, out slot, out delta))
            {
                return MoveCustomCategoryEntry(slot, delta);
            }

            return false;
        }

        /// <summary>
        /// Steps the custom category in the slot this key walks. An empty slot
        /// says so rather than falling silent, because a dead keypress reads as
        /// the mod having missed it.
        /// </summary>
        private bool MoveCustomCategoryEntry(int slot, int delta)
        {
            if (ModSettings.GetScannerCustomCategory(ScannerTaxonomyKeys.Adventure, slot) == null)
            {
                SpeechPipeline.Output(new SpeechRequest(
                    ModText.Get(ModStrings.Scanner.CustomCategoryEmpty, slot + 1),
                    interrupt: false));
                return true;
            }

            return HandleScannerNavigationResult(_scanner.ExecuteMoveCustomCategoryEntry(
                ScannerCustomCategorySynthesizer.CategoryKeyFor(slot),
                delta));
        }

        /// <summary>Comma walks slot 1, period slot 2 and slash slot 3; Shift
        /// walks each of them backwards.</summary>
        private static bool TryGetCustomEntrySlot(string actionKey, out int slot, out int delta)
        {
            if (actionKey == AccessibilityActions.ScannerNextCustomEntryComma.Key)
            {
                slot = 0;
                delta = 1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerPreviousCustomEntryComma.Key)
            {
                slot = 0;
                delta = -1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerNextCustomEntryPeriod.Key)
            {
                slot = 1;
                delta = 1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerPreviousCustomEntryPeriod.Key)
            {
                slot = 1;
                delta = -1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerNextCustomEntrySlash.Key)
            {
                slot = 2;
                delta = 1;
                return true;
            }

            if (actionKey == AccessibilityActions.ScannerPreviousCustomEntrySlash.Key)
            {
                slot = 2;
                delta = -1;
                return true;
            }

            slot = -1;
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
            Land(announce: true);
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
            SyncBeaconTeam();
            _bookmarks.EnsureLoaded(_adapter != null ? _adapter.GetBookmarkGameIdentity() : null);
        }

        /// <summary>
        /// Silence every beacon when the map has changed hands: what they point at is the outgoing
        /// player's bookmarks, which the incoming one neither owns nor can switch off. The team in
        /// control is read from the game, never reported by a hook, and the read is one property off
        /// the facade, so the screen's update asks every frame and a hand-over is silent at the
        /// moment it happens rather than at the next bookmark gesture.
        ///
        /// Handing control BACK leaves the beacons off: a bookmark is a position and nothing else,
        /// so the file records no beacon of its own to restore, and re-starting them would be the
        /// mod deciding what the player last wanted rather than reading it.
        /// </summary>
        public void SyncBeaconTeam()
        {
            int teamId = _adapter != null ? _adapter.LocalTeamId : -1;
            if (teamId == _beaconTeamId)
            {
                return;
            }

            _beaconTeamId = teamId;
            _beacons.StopAll();
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
            string cancel = GameText.Get(_adapter.LocalizationHandler, "Common/Cancel", string.Empty);
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
                NativeSoundUtility.PostEvent(WrapCue.Key);
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
                || actionKey == AccessibilityActions.ScannerJumpToResultAlternate.Key
                || actionKey == AccessibilityActions.ScannerSpeakDistanceAndDirection.Key
                || actionKey == AccessibilityActions.ScannerReturnFromJump.Key
                || actionKey == AccessibilityActions.ScannerLookAround.Key
                || actionKey == AccessibilityActions.ScannerIncreaseLookAroundRadius.Key
                || actionKey == AccessibilityActions.ScannerDecreaseLookAroundRadius.Key
                || IsCustomEntryAction(actionKey);
        }

        private static bool IsCustomEntryAction(string actionKey)
        {
            int slot;
            int delta;
            return TryGetCustomEntrySlot(actionKey, out slot, out delta);
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
