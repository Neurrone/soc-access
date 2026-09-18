using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Audio;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    /// <summary>
    /// The scanner as a hex board drives it: the controller, the jump anchor, the eleven scanner keys
    /// and what a landing sounds like. Shared by the battlefield (<see cref="CombatHexGrid"/>) and
    /// the troop-placement board (<see cref="TroopPlacementHexGrid"/>), which drive it identically;
    /// the adventure map's scanner is not this one - it also owns the search popup, the look-around
    /// sweep and its radius, it jumps with a camera move, and its results carry their own origin.
    ///
    /// The grid keeps the cursor: this class asks for it and asks the grid to move it, so a grid that
    /// turns a move down (off the board, outside an inspection) still turns the jump down and the
    /// anchor stays where the player left it.
    /// </summary>
    public sealed class HexGridScanner
    {
        private readonly ScannerController _scanner;
        private readonly ScannerJumpAnchor _jumpAnchor = new ScannerJumpAnchor();
        private readonly Func<Vector2Int> _getCursor;
        private readonly Func<Vector2Int, bool> _setCursor;
        private readonly Action<Vector2Int, float, float, float> _playTileCues;

        /// <param name="playTileCues">The tile's own cues at a point, panned, gain-scaled and pitched
        /// - the grid's per-tile cue selection, which differs between the two boards.</param>
        public HexGridScanner(
            Func<Vector2Int, ScannerSnapshot> snapshotBuilder,
            Func<ScannerResult, Vector2Int, ScannerResultRefresh> refreshResult,
            Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, bool, IScannerSpeechContext> speechContextProvider,
            Func<Vector2Int> getCursor,
            Func<Vector2Int, bool> setCursor,
            Action<Vector2Int, float, float, float> playTileCues)
        {
            _getCursor = getCursor;
            _setCursor = setCursor;
            _playTileCues = playTileCues;
            _scanner = new ScannerController(
                snapshotBuilder,
                getCursor,
                refreshResult,
                JumpToResult,
                speechContextProvider,
                ScannerDirectionMode.Hex);
        }

        public static bool ClaimsAction(string actionKey)
        {
            return actionKey == AccessibilityActions.ScannerPreviousCategory.Key
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
                || actionKey == AccessibilityActions.ScannerReturnFromJump.Key;
        }

        /// <summary>Answers whether the key was the scanner's; the grid tries its own keys next.</summary>
        public bool HandleAction(InputAction action)
        {
            if (action.Key == AccessibilityActions.ScannerPreviousCategory.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveCategory(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextCategory.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveCategory(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousSubcategory.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveSubcategory(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextSubcategory.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveSubcategory(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousItem.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveItem(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextItem.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveItem(1));
            }

            if (action.Key == AccessibilityActions.ScannerPreviousInstance.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveInstance(-1));
            }

            if (action.Key == AccessibilityActions.ScannerNextInstance.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteMoveInstance(1));
            }

            if (action.Key == AccessibilityActions.ScannerJumpToResult.Key
                || action.Key == AccessibilityActions.ScannerJumpToResultAlternate.Key)
            {
                return _scanner.JumpToCurrent();
            }

            if (action.Key == AccessibilityActions.ScannerSpeakDistanceAndDirection.Key)
            {
                return HandleNavigationResult(_scanner.ExecuteSpeakDistanceAndDirection());
            }

            if (action.Key == AccessibilityActions.ScannerReturnFromJump.Key)
            {
                return ReturnFromJump();
            }

            return false;
        }

        private bool JumpToResult(Vector2Int point)
        {
            Vector2Int origin = _getCursor();
            if (point == origin)
            {
                SpeakHere();
                return true;
            }

            _setCursor(point);
            return _jumpAnchor.RememberIfMoved(origin, _getCursor());
        }

        /// <summary>
        /// A jump onto the tile the cursor already occupies moves nothing, so no
        /// tile announcement follows it. Say where the player is rather than
        /// letting the key fall silent.
        /// </summary>
        private static void SpeakHere()
        {
            SpeechPipeline.Output(new SpeechRequest(ModText.Get(ModStrings.Spatial.Here), interrupt: false));
        }

        private bool ReturnFromJump()
        {
            Vector2Int anchor;
            if (!_jumpAnchor.TryTake(out anchor))
            {
                CueLibrary.PlayCue(CueLibrary.MoveDenied);
                SpeechPipeline.Output(new SpeechRequest(
                    ModText.Get(ModStrings.Scanner.NoTileToReturnTo),
                    interrupt: false));
                return true;
            }

            return _setCursor(anchor);
        }

        private bool HandleNavigationResult(ScannerCommandResult result)
        {
            if (result != null && result.Status == ScannerCommandStatus.Result && result.Wrapped)
            {
                NativeSoundUtility.PostEvent(WrapCue.Key);
            }

            if (result != null && result.Status == ScannerCommandStatus.Result && result.Result != null)
            {
                PlayDirectionalTileCues(_getCursor(), result.Result.Position);
            }

            _scanner.Output(result);
            return true;
        }

        /// <summary>The remote tile's own cues carry the direction to it; out of range plays nothing.</summary>
        private void PlayDirectionalTileCues(Vector2Int origin, Vector2Int target)
        {
            float pan;
            float semitones;
            float gainScale;
            if (!DirectionalCueMath.TryCompute(origin, target, CueGridGeometry.Hex, out pan, out semitones, out gainScale))
            {
                return;
            }

            _playTileCues(target, pan, gainScale, semitones);
        }
    }
}
