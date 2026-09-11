using System;
using System.Collections.Generic;
using System.Linq;
using Lavapotion.Pathfinding;
using SongsOfConquest;
using SongsOfConquest.Client.Battle;
using SongsOfConquest.Client.InputManagement;
using SongsOfConquest.Client.Menu.Tooltip;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech.Spatial;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // INSPECTING A TILE, moved out of CombatAdapter.cs unchanged: pinning the inspection on a
    // stack, a path or an entity, the details object each one hands the game's tooltip composer,
    // the instruction line the tooltip carries, and the overlay that marks the focused tile.

    public sealed partial class CombatAdapter
    {
        /// <summary>Pin the inspection on a tile, or answer null with the reason the caller words:
        /// an empty tile the acting troop cannot walk to has no path to inspect.</summary>
        public CombatInspectContext BeginInspect(Vector2Int point, out bool notInMovementRange)
        {
            notInMovementRange = false;
            CombatTile tile = GetTile(point);
            if (tile == null)
            {
                return null;
            }

            if (tile.Troop != null)
            {
                return BeginStackInspect(tile.Troop);
            }

            if (tile.Entity != null)
            {
                return BeginEntityInspect(tile.Entity);
            }

            if (!IsReachable(point))
            {
                notInMovementRange = true;
                return null;
            }

            return BeginPathInspect(point);
        }

        public void HandleSecondaryAction(Vector2Int point)
        {
            if (CancelSpellTargeting())
            {
                return;
            }

            InvokeNativeClickWithHover(point, _secondaryClickMethod, "secondary");
        }

        public void ExitInspect(Vector2Int point)
        {
            ClearNativeTooltip();
            FocusTile(point);
        }

        public void ClearNativeTooltip()
        {
            NativeTooltipUtility.HideTooltip();
            _tooltipUtility?.ClearSpecific();
            _attackPreviewHandler?.Hide();
        }

        public Tooltip GetInspectTooltip(CombatInspectContext context, Vector2Int focusedTile)
        {
            if (context != null && context.TooltipDetails != null && focusedTile == context.PinnedTile)
            {
                return CreateDetailsTooltip(
                    context.TooltipDetails,
                    context.PinnedTile,
                    includeAttackPreview: true,
                    attackPreviewTargetIsEntity: IsEntityInspectMode(context.Mode));
            }

            if (context != null)
            {
                return null;
            }

            CombatTile tile = GetTile(focusedTile);
            if (tile == null)
            {
                return null;
            }

            if (tile.Troop != null && _tooltipUtility != null)
            {
                return CreateDetailsTooltip(
                    _tooltipUtility.GetInspectTroopDetails(tile.Troop),
                    focusedTile,
                    includeAttackPreview: true,
                    attackPreviewTargetIsEntity: false);
            }

            if (tile.Entity != null)
            {
                return CreateDetailsTooltip(
                    BuildEntityDetails(tile.Entity),
                    focusedTile,
                    includeAttackPreview: true,
                    attackPreviewTargetIsEntity: true);
            }

            if (tile.IsReachable)
            {
                return CreateDetailsTooltip(BuildTileDetails(focusedTile), focusedTile);
            }

            return null;
        }

        private Tooltip CreateDetailsTooltip(
            IDetails details,
            Vector2Int tile,
            bool includeAttackPreview = false,
            bool attackPreviewTargetIsEntity = false)
        {
            if (details == null)
            {
                return null;
            }

            DetailsTextUtility captured = DetailsTextUtility.Capture(details, _localization);
            List<string> textLines = new List<string>(captured.TextLines);
            TileInstruction secondary = TakeCombatTooltipInstruction(captured.InstructionRows, textLines);
            return new Tooltip(
                () => includeAttackPreview ? BuildTooltipLinesWithAttackPreview(textLines, attackPreviewTargetIsEntity) : textLines,
                CreateScreenPointTooltipMetadata(details, tile),
                TileInstruction.None,
                secondary,
                () => NativeTooltipUtility.IsLong(details));
        }

        private IReadOnlyList<string> BuildTooltipLinesWithAttackPreview(IReadOnlyList<string> detailsLines, bool targetIsEntity)
        {
            List<string> previewLines = CaptureAttackPreviewLines(targetIsEntity);
            if (previewLines.Count == 0)
            {
                return detailsLines;
            }

            List<string> lines = new List<string>();
            lines.Add(ModText.Get(ModStrings.Spatial.AttackPreview));
            lines.AddRange(previewLines);
            if (detailsLines != null)
            {
                lines.AddRange(detailsLines);
            }

            return lines;
        }

        private static bool IsEntityInspectMode(CombatInspectMode mode)
        {
            return mode == CombatInspectMode.EntityPath || mode == CombatInspectMode.EntityOnly;
        }

        private VisualTooltipMetadata CreateScreenPointTooltipMetadata(IDetails details, Vector2Int tile)
        {
            ITooltipable tooltipable = GetBattleTooltipable();
            if (tooltipable == null)
            {
                return null;
            }

            return new VisualTooltipMetadata(tooltipable, GetScreenPoint(tile), details);
        }

        private ITooltipable GetBattleTooltipable()
        {
            if (_tooltipBehaviorField == null || _tooltipUtility == null)
            {
                return null;
            }

            try
            {
                return _tooltipBehaviorField.GetValue(_tooltipUtility) as ITooltipable;
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to resolve battle tooltip behavior: " + exception.Message);
                return null;
            }
        }

        /// <summary>
        /// Take the native right-click instruction row out of the tooltip text and report what it
        /// said the click would DO. The board has no primary row. The row is stripped either way - it
        /// describes a mouse gesture the keyboard player is not making - and the screen says the kind
        /// as a usage hint on the key that performs it.
        /// </summary>
        private TileInstruction TakeCombatTooltipInstruction(
            IReadOnlyList<TooltipInstructionRow> instructionRows,
            List<string> textLines)
        {
            TileInstruction secondary = TileInstruction.None;
            if (instructionRows == null || instructionRows.Count == 0)
            {
                return secondary;
            }

            for (int i = 0; i < instructionRows.Count; i++)
            {
                TooltipInstructionRow row = instructionRows[i];
                if (row == null
                    || string.IsNullOrWhiteSpace(row.Text)
                    || !IsSecondaryCombatInstruction(row.InputType))
                {
                    continue;
                }

                TooltipLines.Remove(textLines, row.Text);
                secondary = ClassifyCombatInstruction(row.Text);
            }

            return secondary;
        }

        /// <summary>The kind a row's text names, matched against the game's own battle instruction
        /// strings, which are resolved once per adapter. A wording the table does not hold is logged
        /// once so a new game kind shows up in the log rather than vanishing.</summary>
        private TileInstruction ClassifyCombatInstruction(string text)
        {
            EnsureCombatInstructionKinds();
            TileInstruction kind;
            if (_combatInstructionKinds != null
                && _combatInstructionKinds.TryGetValue(text.Trim(), out kind))
            {
                return kind;
            }

            if (_unknownCombatInstructions.Add(text))
            {
                SocAccessMod.Instance?.LogWarning(
                    "CombatAdapter saw an unrecognized tooltip instruction row: " + text);
            }

            return TileInstruction.None;
        }

        private void EnsureCombatInstructionKinds()
        {
            if (_combatInstructionKindsProbed)
            {
                return;
            }

            _combatInstructionKindsProbed = true;
            if (_localization == null)
            {
                return;
            }

            Dictionary<string, TileInstruction> kinds =
                new Dictionary<string, TileInstruction>(StringComparer.Ordinal);
            AddCombatInstructionKind(kinds, "Battle/InspectTile/ClickToMove", TileInstruction.Move);
            AddCombatInstructionKind(kinds, "Battle/InspectTroop/AttackPreview/ClickToAttack", TileInstruction.Attack);
            _combatInstructionKinds = kinds;
        }

        private void AddCombatInstructionKind(
            Dictionary<string, TileInstruction> kinds,
            string key,
            TileInstruction kind)
        {
            string text = GameText.Get(_localization, key, string.Empty);
            if (!string.IsNullOrWhiteSpace(text))
            {
                kinds[text.Trim()] = kind;
            }
        }

        private bool IsSecondaryCombatInstruction(InputType inputType)
        {
            if (_inputManager != null)
            {
                return inputType == InputType.GetRightMouseClickOrCursorConfirm(_inputManager);
            }

            return inputType == InputType.RightMouseClickOrCursorConfirm;
        }

        public void SetFocusedTileOverlay(Vector2Int tile)
        {
            if (!IsPresent())
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
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to set focused tile overlay: " + exception.Message);
            }
        }

        public void ClearFocusedTileOverlay()
        {
            if (!_cursorOverlay.IsCreated)
            {
                return;
            }

            try
            {
                _cursorOverlay.Destroy();
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to clear focused tile overlay: " + exception.Message);
            }
        }

        public string DescribeTile(CombatTile tile, CombatInspectContext context)
        {
            return DescribeTile(tile, context, selectedForSpellcast: false);
        }

        public string DescribeTile(CombatTile tile, CombatInspectContext context, bool selectedForSpellcast)
        {
            return new CombatTileSpeechFormatter(this, context, selectedForSpellcast: selectedForSpellcast).DescribeTile(tile);
        }

        private IDetails BuildTileDetails(Vector2Int point)
        {
            PathNode[] path = GetPathTo(point);
            if (!PathfinderExtensions.GetIsValid(path))
            {
                return null;
            }

            PathNode finalNode = path[path.Length - 1];
            Vector2Int finalPoint = ToVector2Int(finalNode.point);
            bool mothersLove = _facade.Troops.GetRootSpreadersForTile(finalPoint, _facade.Teams.Current.Id)
                .Any(troop => troop.HasBacteria((BacteriaTypes)1419));
            bool mothersHate = _facade.Troops.GetRootSpreadersForTile(finalPoint, _facade.Teams.GetOtherTeamId(_facade.Teams.Current.Id))
                .Any(troop => troop.IsHatingMother());

            return new BattleTileDetails(_inputManager)
            {
                MovementLeft = _facade.Troops.Current.MovesLeft,
                TravelCost = finalNode.flooredTravelCost,
                MothersLove = mothersLove,
                MothersHate = mothersHate,
                Point = finalNode.point
            };
        }

        private CombatInspectContext BeginStackInspect(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return null;
            }

            PathNode[] path = GetPathTo(troop.Position);
            _gridManager?.SetInspectedTroop(troop);
            SetNativeCursorTile(troop.Position, path);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTroop);
            _gridManager?.SetState(BattleGridManager.State.InspectTroop);
            _pathManager?.SetState(BattlePathManager.State.InspectTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectTroop);
            SynchronizeNativeHoverForPreview(troop.Position, GetTile(troop.Position), path);

            CombatInspectContext context = CombatInspectContext.ForStack(troop.Position);
            BuildStackRanges(troop, context);
            context.TooltipDetails = _tooltipUtility != null ? _tooltipUtility.GetInspectTroopDetails(troop) : null;
            return context;
        }

        private CombatInspectContext BeginPathInspect(Vector2Int point)
        {
            PathNode[] path = GetPathTo(point);
            SetNativeCursorTile(point, path);
            SetNativeCurrentTroopState();
            _attackPreviewHandler?.Hide();
            CombatInspectContext context = CombatInspectContext.ForPath(point, ConvertPath(path));
            context.TooltipDetails = BuildTileDetails(point);
            return context;
        }

        private CombatInspectContext BeginEntityInspect(IMapEntity entity)
        {
            if (entity == null)
            {
                return null;
            }

            PathNode[] path = GetPathToEntity(entity);
            SetNativeCursorTile(entity.Position, path);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTile);
            _gridManager?.SetState(BattleGridManager.State.InspectEntity);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectEntity);
            _pathManager?.SetState(BattlePathManager.State.CurrentTroop);
            SynchronizeNativeHoverForPreview(entity.Position);

            CombatInspectContext context = PathfinderExtensions.IsReachable(path, GetCurrentMovesLeft(), true)
                ? CombatInspectContext.ForEntityPath(entity.Position, ConvertPath(path))
                : CombatInspectContext.ForEntityOnly(entity.Position);
            context.TooltipDetails = BuildEntityDetails(entity);
            return context;
        }
    }
}
