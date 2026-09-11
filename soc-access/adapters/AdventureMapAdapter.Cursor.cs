using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using HarmonyLib;
using Lavapotion.Cartography;
using Lavapotion.Pathfinding;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Adventure;
using SongsOfConquest.Client.Adventure.Map;
using SongsOfConquest.Client.Adventure.Menu;
using SongsOfConquest.Client.Adventure.View;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Client.Grid;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Loading;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Client.UI;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Commander;
using SongsOfConquest.Common.Localization;
using SongsOfConquest.Common.Map;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Bookmarks;
using SongsOfConquestAccess.Events;
using SongsOfConquestAccess.Scanner;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using Unity.Mathematics;
using UnityEngine;
using UnityEngine.UI;
using Zenject;

namespace SongsOfConquestAccess.Adapters
{
    /// <summary>
    /// THE CURSOR AND THE CAMERA: the outline the mod draws around the focused tile, keeping that
    /// tile in view, moving the camera to it, and the world and screen points both of those need.
    ///
    /// Split out of AdventureMapAdapter.cs as a pure move; nothing here changed with the split.
    /// </summary>
    public sealed partial class AdventureMapAdapter
    {
        public void SetFocusedTileOverlay(Vector2Int tile)
        {
            if (!IsWithinMap(tile))
            {
                return;
            }

            try
            {
                if (!_cursorOverlay.Ensure())
                {
                    return;
                }

                _cursorOverlay.MoveTo(GetScreenPoint(tile));
                _focusedOverlayTile = tile;
            }
            catch (Exception exception)
            {
                LogFailureOnce("moving the focused tile overlay", exception);
            }
        }

        public void EnsureTileInView(Vector2Int tile)
        {
            if (!IsWithinMap(tile) || _cameraController == null)
            {
                return;
            }

            try
            {
                Vector3 world = GetWorldCenter(tile);
                _cameraController.MoveToIncludePosition(
                    world,
                    0.10f,
                    0.10f,
                    0.10f,
                    0.10f);
            }
            catch (Exception exception)
            {
                LogFailureOnce("keeping the focused tile in view", exception);
            }
        }

        public void MoveCameraToTile(Vector2Int tile)
        {
            if (!IsWithinMap(tile) || _cameraController == null)
            {
                return;
            }

            try
            {
                Vector3 world = GetWorldCenter(tile);
                _cameraController.MoveToPosition(world, false, Vector3.zero, null, true);
                SocAccessMod.Instance?.StartCoroutine(RefreshFocusedTileOverlayAfterCameraMove(tile));
            }
            catch (Exception exception)
            {
                LogFailureOnce("moving the camera to the focused tile", exception);
            }
        }

        public void ClearFocusedTileOverlay()
        {
            if (!_cursorOverlay.IsCreated)
            {
                _focusedOverlayTile = null;
                return;
            }

            try
            {
                _cursorOverlay.Destroy();
                _focusedOverlayTile = null;
                _tooltipManager?.HideTileTooltip();
            }
            catch (Exception exception)
            {
                LogFailureOnce("clearing the focused tile overlay", exception);
            }
        }

        private Vector2Int GetCameraCenterTile()
        {
            if (_cameraController != null && _worldToPointMethod != null)
            {
                try
                {
                    Vector3 centerPosition = _cameraController.CalculateCenterPosition();
                    object point = _worldToPointMethod.Invoke(_cartographyConverter, new object[] { new float3(centerPosition.x, centerPosition.y, centerPosition.z) });
                    if (point is int2)
                    {
                        int2 intPoint = (int2)point;
                        return ClampToMap(new Vector2Int(intPoint.x, intPoint.y));
                    }
                }
                catch (Exception exception)
                {
                    LogFailureOnce("resolving the camera centre tile", exception);
                }
            }

            return ClampToMap(new Vector2Int(_facade.Level.Width / 2, _facade.Level.Height / 2));
        }

        private IEnumerator RefreshFocusedTileOverlayAfterCameraMove(Vector2Int tile)
        {
            for (int i = 0; i < 30; i++)
            {
                yield return null;

                if (!_focusedOverlayTile.HasValue || _focusedOverlayTile.Value != tile)
                {
                    yield break;
                }

                SetFocusedTileOverlay(tile);
                if (_cameraController == null || !_cameraController.IsMoving)
                {
                    yield break;
                }
            }
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
                    LogFailureOnce("resolving a tile world position", exception);
                }
            }

            return new Vector3(tile.x, 0f, tile.y);
        }

        private Vector2 GetScreenPoint(Vector2Int tile)
        {
            Vector3 world = GetWorldCenter(tile);
            if (_cameraController == null || _cameraController.Camera == null)
            {
                return new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            }

            Vector3 point = _cameraController.Camera.WorldToScreenPoint(world);
            return new Vector2(point.x, point.y);
        }
    }
}
