using System;
using System.Linq;
using System.Reflection;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // THE MOUSE, SYNTHESISED, moved out of CombatAdapter.cs unchanged. A key that is meant to do
    // what a click does never reconstructs the game's rules: it puts the game into the state a mouse
    // hovering that tile would have left it in - the four battle managers pointed at the tile, the
    // controller's hover, path and melee reach filled in, its state machine changed - and then calls
    // the game's own click handler. The screen-point maths is here because that is what the override
    // needs: the game reads the pointer's position out of its own input response, so the tile's
    // place on the screen is put there for the length of the call and taken back out after it.

    public sealed partial class CombatAdapter
    {
        // WHO OWNS THE GAME'S HOVER. MouseKeyboardHumanBattleControllerModule.UpdateCurrentTile
        // raycasts the physical mouse every frame and, whenever that answers a different tile,
        // overwrites the hover, the path, the four managers and the damage previews. One frame after
        // every keyboard step it therefore undid the sync below and faded the preview out. So the
        // keyboard TAKES the hover whenever it syncs one, the game's own update is intercepted
        // (CombatPatches) while it holds it, and the mouse takes it back the moment the physical
        // pointer MOVES - the only sign the game gives that the player has gone back to it.
        //
        // The claim is per battle and the adapter IS the battle, but the prefix has no adapter to
        // ask: the owner is held statically as the one battle that can be running, and the pointer
        // watch, Dispose, ClearNativeTooltip and Reset all let go of it.
        private static CombatAdapter _hoverOwner;

        /// <summary>Whether the keyboard cursor owns the game's hover. The interception prefix on
        /// <c>UpdateCurrentTile</c> reads it and lets the game's own update run untouched whenever it
        /// answers false - including while the mod is deliberately calling that update itself, which
        /// is how a synthesised click puts the game on the tile it is about to act on.</summary>
        public static bool KeyboardOwnsHover
        {
            get { return _hoverOwner != null && !_hoverOwner._invokingNativeUpdate; }
        }

        // Where the game's own pointer was when the claim was last read: a MOVE rather than a
        // position is what hands the hover back.
        private Vector2 _pointerPosition;
        private bool _pointerRead;
        private bool _invokingNativeUpdate;

        // The tile an inspection pinned the game's hover to. The cursor walking the inspected ranges
        // does not move it, and a board key pressed after the mouse took the hover puts the game
        // back here rather than on the cursor.
        private bool _hoverPinned;
        private Vector2Int _hoverPinTile;

        /// <summary>The keyboard has just put the game's hover where its cursor is: hold it there
        /// until the pointer moves. The pointer is baselined here, so the position it already had
        /// is not read as a move.</summary>
        public void TakeHoverOwnership()
        {
            _hoverOwner = this;
            _pointerRead = TryReadPointerPosition(out _pointerPosition);
        }

        /// <summary>Whether the game's hover already stands on this tile, as the controller reads it,
        /// whichever of the keyboard and the mouse put it there.</summary>
        public bool IsNativeHoverOn(Vector2Int point)
        {
            return _humanBattleController != null && _humanBattleController.CurrentHoverTile == point;
        }

        /// <summary>Give the hover back: the game's own <c>UpdateCurrentTile</c> runs untouched from
        /// here on. The inspection's pin is NOT dropped - a board key re-asserts it.</summary>
        public void ReleaseHoverOwnership()
        {
            if (ReferenceEquals(_hoverOwner, this))
            {
                _hoverOwner = null;
            }
        }

        /// <summary>Read the game's own pointer, once a frame, and hand the hover back the moment it
        /// has moved. Nothing tells the mod the player reached for the mouse; the screen asks.
        /// </summary>
        public void WatchHoverOwnership()
        {
            Vector2 position;
            if (!ReferenceEquals(_hoverOwner, this) || !TryReadPointerPosition(out position))
            {
                return;
            }

            if (!_pointerRead)
            {
                _pointerRead = true;
                _pointerPosition = position;
                return;
            }

            if (position == _pointerPosition)
            {
                return;
            }

            _pointerPosition = position;
            ReleaseHoverOwnership();
        }

        private bool TryReadPointerPosition(out Vector2 position)
        {
            position = Vector2.zero;
            if (_inputManager == null || _inputManager.Screen == null || _inputManager.Screen.Primary == null)
            {
                return false;
            }

            position = _inputManager.Screen.Primary.Position;
            return true;
        }

        private void PinHoverOn(Vector2Int point)
        {
            _hoverPinned = true;
            _hoverPinTile = point;
        }

        private void ClearHoverPin()
        {
            _hoverPinned = false;
        }

        /// <summary>Point the game's four battle managers at a tile, which is what the mouse moving
        /// over it does.</summary>
        private void SetNativeCursorTile(Vector2Int point, PathNode[] path)
        {
            _cursorManager?.SetCurrentTile(point);
            _gridManager?.SetCurrentTile(point, path);
            _pathManager?.SetCurrentTile(point, path);
            _highlightManager?.SetCurrentTile(point);
        }

        /// <summary>Put all four back into the state the game draws for the troop whose turn it is.
        /// </summary>
        private void SetNativeCurrentTroopState()
        {
            _cursorManager?.SetState(BattleCursorManager.State.CurrentTroop);
            _gridManager?.SetState(BattleGridManager.State.CurrentTroop);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.CurrentTroop);
        }

        private void SynchronizeNativeHoverForInput(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            SynchronizeNativeHoverForInput(point, GetTile(point), GetPathTo(point));
        }

        /// <summary>The same hover sync for a caller that has already read the tile and the path this
        /// frame.</summary>
        private void SynchronizeNativeHoverForInput(Vector2Int point, CombatTile tile, PathNode[] path)
        {
            SetNativeCursorTile(point, path);

            if (_humanBattleController == null || tile == null)
            {
                return;
            }

            // The game's own update does two more things on a tile change, and while the keyboard
            // owns the hover that update does not run: the spell HUD is told the hover left its
            // target, and the spell controller is told where the hover is now. The aiming path calls
            // SetCurrentTile itself (FocusTargetTile), so it is not doubled here.
            if (_humanBattleController.CurrentHoverTile != point)
            {
                if (_battleHudSignals != null)
                {
                    _battleHudSignals.OnEndHoverSpellTarget.SafeInvoke();
                }

                if (GetTargetingMode() != CombatTargetingMode.Spell)
                {
                    _battleSpellController?.SetCurrentTile(point);
                }
            }

            _humanBattleController.CurrentHoverTile = point;
            _humanBattleController.CurrentTroopAtPosition = tile.Troop;
            _humanBattleController.TroopToInspect = tile.Troop;
            _humanBattleController.EntityToInspect = tile.Entity;
            _humanBattleController.TileToInspect = new int2(point.x, point.y);
            _humanBattleController.PathToCurrentTile = (!tile.IsImpassable && !tile.IsBlocked) ? path : null;
            _humanBattleController.EnemiesWithinMeleeReach = _facade.Level.AllEnemiesWithinMeleeReach(_facade.Troops.Current).ToList();
            _humanBattleController.MapEntitiesWithinMeleeReach = _facade.Level.AllMapEntitiesWithinMeleeReach(_facade.Troops.Current).ToList();

            HumanBattleController.State currentState = _humanBattleController.StateMachine.CurrentStateType;
            if (currentState == HumanBattleController.State.ChoosingAbilityTarget)
            {
                return;
            }

            if (IsAnySpellCastingStateActive())
            {
                return;
            }

            // This is native hover synchronization for mouse-equivalent input.
            // It is intentionally separate from CombatHexGrid's accessibility inspect mode.
            if (tile.Troop != null)
            {
                if (currentState == HumanBattleController.State.InspectTroop
                    && _humanBattleController.TroopToInspect != null
                    && _humanBattleController.TroopToInspect.Id == tile.Troop.Id)
                {
                    return;
                }

                _gridManager?.SetInspectedTroop(tile.Troop);
                _cursorManager?.SetState(BattleCursorManager.State.InspectTroop);
                _gridManager?.SetState(BattleGridManager.State.InspectTroop);
                _highlightManager?.SetState(BattleHighlightManager.State.InspectTroop);
                _pathManager?.SetState(BattlePathManager.State.InspectTroop);
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.InspectTroop);
            }
            else if (tile.Entity != null)
            {
                if (currentState == HumanBattleController.State.InspectEntity
                    && _humanBattleController.EntityToInspect != null
                    && _humanBattleController.EntityToInspect.Id == tile.Entity.Id)
                {
                    return;
                }

                _cursorManager?.SetState(BattleCursorManager.State.InspectTile);
                _gridManager?.SetState(BattleGridManager.State.InspectEntity);
                _highlightManager?.SetState(BattleHighlightManager.State.InspectEntity);
                _pathManager?.SetState(BattlePathManager.State.InspectEntity);
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.InspectEntity);
            }
            else
            {
                if (currentState == HumanBattleController.State.InspectTile && IsNativeTileToInspect(point))
                {
                    return;
                }

                SetNativeCurrentTroopState();
                _humanBattleController.StateMachine.ChangeState(HumanBattleController.State.ShowCurrentTroop);
            }
        }

        private void SynchronizeNativeHoverForPreview(Vector2Int point)
        {
            if (!IsValidTile(point))
            {
                return;
            }

            SynchronizeNativeHoverForPreview(point, GetTile(point), GetPathTo(point));
        }

        /// <summary>The hover sync plus the game's attack preview, for a caller that has already read
        /// the tile and the path. The two melee sweeps the preview wants are the ones the hover sync
        /// itself puts on the controller, so they are not swept a second time here.</summary>
        private void SynchronizeNativeHoverForPreview(Vector2Int point, CombatTile tile, PathNode[] path)
        {
            if (tile == null)
            {
                return;
            }

            SynchronizeNativeHoverForInput(point, tile, path);

            if (_humanBattleController == null)
            {
                return;
            }

            UpdateNativeAttackPreviews();
        }

        private bool IsNativeTileToInspect(Vector2Int point)
        {
            if (_humanBattleController == null)
            {
                return false;
            }

            int2 tile = _humanBattleController.TileToInspect;
            return tile.x == point.x && tile.y == point.y;
        }

        private void InvokeNativeClickWithHover(Vector2Int point, MethodInfo clickMethod, string clickName)
        {
            ScreenInputOverride screenInputOverride;
            if (!TryBeginScreenInputOverride(point, out screenInputOverride))
            {
                return;
            }

            try
            {
                if (_updateCurrentTileMethod != null)
                {
                    // Deliberate, and over the pointer position the override has just put on the
                    // tile: the interception stands aside for the length of the call.
                    _invokingNativeUpdate = true;
                    try
                    {
                        _updateCurrentTileMethod.Invoke(_mouseKeyboardInputModule, Array.Empty<object>());
                    }
                    finally
                    {
                        _invokingNativeUpdate = false;
                    }
                }

                SynchronizeNativeHoverForInput(point);
                InvokeNativeClick(clickMethod, clickName);
            }
            finally
            {
                screenInputOverride.Restore();
            }
        }

        private bool InvokeNativeClick(MethodInfo clickMethod, string clickName)
        {
            if (clickMethod == null || _mouseKeyboardInputModule == null)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter cannot emulate " + clickName + " click because the native mouse input module was not resolved.");
                return false;
            }

            try
            {
                clickMethod.Invoke(_mouseKeyboardInputModule, Array.Empty<object>());
                return true;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to emulate native " + clickName + " click: " + exception.Message);
                return false;
            }
        }

        private bool TryBeginScreenInputOverride(Vector2Int tilePosition, out ScreenInputOverride screenInputOverride)
        {
            screenInputOverride = null;
            if (_inputManager == null || _inputManager.Screen == null || _inputManager.Screen.Primary == null)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not override native screen input because primary screen input was unavailable");
                return false;
            }

            object response = ScreenInputOverride.ResolveWritableResponse(_inputManager.Screen.Primary);
            if (response == null)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not override native screen input because no writable ScreenInputResponse could be resolved from " + _inputManager.Screen.Primary.GetType().FullName);
                return false;
            }

            Vector2 screenPosition = GetScreenPoint(tilePosition);
            if (screenPosition.x < 0f
                || screenPosition.y < 0f
                || screenPosition.x > Screen.width
                || screenPosition.y > Screen.height)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not target tile " + FormatDiagnosticPoint(tilePosition) + " because its screen position is outside the current view: " + screenPosition);
                return false;
            }

            screenInputOverride = ScreenInputOverride.ApplyMouseClick(response, screenPosition, "CombatAdapter");
            return screenInputOverride != null;
        }

        private Vector2 GetScreenPoint(Vector2Int tile)
        {
            Vector3 world = GetWorldCenter(tile);
            ICamera camera = _cameraLookup != null ? _cameraLookup.GetBrainCamera() : null;
            if (camera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 point = camera.WorldToScreenPoint(world);
            return new Vector2(point.x, point.y);
        }

        private Vector3 GetWorldCenter(Vector2Int tile)
        {
            if (_pointToWorldMethod != null)
            {
                try
                {
                    object world = _pointToWorldMethod.Invoke(_cartographyConverter, new object[] { new int2(tile.x, tile.y), -1 });
                    if (world is float3)
                    {
                        float3 point = (float3)world;
                        return new Vector3(point.x, point.y, point.z);
                    }
                }
                catch (Exception exception)
                {
                    SocAccessMod.Instance?.LogWarning("CombatAdapter failed to resolve tile world position: " + exception.Message);
                }
            }

            return new Vector3(tile.x, 0f, tile.y);
        }

        private static string FormatDiagnosticPoint(Vector2Int point)
        {
            return point.x + ", " + point.y;
        }
    }
}
