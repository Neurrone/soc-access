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
                    _updateCurrentTileMethod.Invoke(_mouseKeyboardInputModule, Array.Empty<object>());
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
