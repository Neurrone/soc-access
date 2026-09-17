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
            // The cursor has left the board or the screen: the mouse gets its hover back, and there
            // is no inspection left to re-assert.
            ClearHoverPin();
            ReleaseHoverOwnership();
        }

        public Tooltip GetInspectTooltip(CombatInspectContext context, Vector2Int focusedTile)
        {
            if (context != null && context.TooltipDetails != null && focusedTile == context.PinnedTile)
            {
                return CreateDetailsTooltip(context.TooltipDetails, context.PinnedTile);
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
                return CreateDetailsTooltip(_tooltipUtility.GetInspectTroopDetails(tile.Troop), focusedTile);
            }

            if (tile.Entity != null)
            {
                return CreateDetailsTooltip(BuildEntityDetails(tile.Entity), focusedTile);
            }

            if (tile.IsReachable)
            {
                return CreateDetailsTooltip(BuildTileDetails(focusedTile), focusedTile);
            }

            return null;
        }

        /// <summary>The tile's dossier: the game's own details block, and nothing the mod composed.
        /// The damage preview used to be prepended here and is now the board node's own section
        /// (<see cref="ReadAttackPreviewLines"/>), which is what lets it be spoken while this - a
        /// long tooltip - is only reviewed.</summary>
        private Tooltip CreateDetailsTooltip(IDetails details, Vector2Int tile)
        {
            if (details == null)
            {
                return null;
            }

            DetailsTextUtility captured = DetailsTextUtility.Capture(details, _localization);
            List<string> textLines = new List<string>(captured.TextLines);
            TileInstruction secondary = TakeCombatTooltipInstruction(captured.InstructionRows, textLines);
            return new Tooltip(
                () => textLines,
                CreateScreenPointTooltipMetadata(details, tile),
                TileInstruction.None,
                secondary,
                () => NativeTooltipUtility.IsLong(details));
        }

        /// <summary>What the game's own buff and nerf indicators over the stack on a tile SAY - the
        /// modifiers drawn under each header the readout names - line by line as the game broke them.
        ///
        /// Reviewable and never spoken: the names are in the readout already, and what they do is a
        /// stat block, which belongs where the dossier is and after it. Read when the lines are read,
        /// so nothing here costs a frame; in inspect mode it is the pinned tile's, as the dossier is.
        /// </summary>
        public IList<string> ReadTroopEffectDetailLines(CombatInspectContext context, Vector2Int focusedTile)
        {
            if (context != null && focusedTile != context.PinnedTile)
            {
                return null;
            }

            CombatTile tile = GetTile(context != null ? context.PinnedTile : focusedTile);
            return tile != null && tile.Troop != null && Hud != null
                ? Hud.GetTroopEffectDetailLines(tile.Troop.Id)
                : null;
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

        /// <summary>Where an inspection left the game: the hover, the four managers and the damage
        /// preview, all on the pinned tile. Called when the inspection begins, and again by
        /// <see cref="ReassertHoverPin"/> when the mouse has taken the hover since.</summary>
        private void PinNativeStackHover(IBattleTroopState troop, PathNode[] path)
        {
            _gridManager?.SetInspectedTroop(troop);
            SetNativeCursorTile(troop.Position, path);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTroop);
            _gridManager?.SetState(BattleGridManager.State.InspectTroop);
            _pathManager?.SetState(BattlePathManager.State.InspectTroop);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectTroop);
            SynchronizeNativeHoverForPreview(troop.Position, GetTile(troop.Position), path);
            PinHoverOn(troop.Position);
            TakeHoverOwnership();
        }

        private void PinNativeEntityHover(IMapEntity entity, PathNode[] path)
        {
            SetNativeCursorTile(entity.Position, path);
            _cursorManager?.SetState(BattleCursorManager.State.InspectTile);
            _gridManager?.SetState(BattleGridManager.State.InspectEntity);
            _highlightManager?.SetState(BattleHighlightManager.State.InspectEntity);
            // InspectEntity, as the game's own entity hover leaves it: the path to the entity was
            // drawn when the cursor landed on it. CurrentTroop here redrew it and, with the hover
            // sync's own SetCurrentTile re-entering it, repeated the path manager's "Blocked"
            // notification twice over.
            _pathManager?.SetState(BattlePathManager.State.InspectEntity);
            SynchronizeNativeHoverForPreview(entity.Position);
            PinHoverOn(entity.Position);
            TakeHoverOwnership();
        }

        private void PinNativeTileHover(Vector2Int point, PathNode[] path)
        {
            SetNativeCursorTile(point, path);
            SetNativeCurrentTroopState();
            _attackPreviewHandler?.Hide();
            PinHoverOn(point);
            TakeHoverOwnership();
        }

        /// <summary>Put the game back on the tile the inspection pinned it to, for a board key
        /// pressed after the mouse took the hover back. Nothing at all while the keyboard still owns
        /// the hover, which is every key of an undisturbed inspection.</summary>
        public void ReassertHoverPin()
        {
            if (!_hoverPinned || ReferenceEquals(_hoverOwner, this))
            {
                return;
            }

            Vector2Int point = _hoverPinTile;
            CombatTile tile = GetTile(point);
            if (tile != null && tile.Troop != null)
            {
                PinNativeStackHover(tile.Troop, GetPathTo(point));
            }
            else if (tile != null && tile.Entity != null)
            {
                PinNativeEntityHover(tile.Entity, GetPathToEntity(tile.Entity));
            }
            else
            {
                PinNativeTileHover(point, GetPathTo(point));
            }
        }

        private CombatInspectContext BeginStackInspect(IBattleTroopState troop)
        {
            if (troop == null)
            {
                return null;
            }

            PathNode[] path = GetPathTo(troop.Position);
            PinNativeStackHover(troop, path);

            CombatInspectContext context = CombatInspectContext.ForStack(troop.Position);
            BuildStackRanges(troop, context);
            context.TooltipDetails = _tooltipUtility != null ? _tooltipUtility.GetInspectTroopDetails(troop) : null;
            return context;
        }

        private CombatInspectContext BeginPathInspect(Vector2Int point)
        {
            PathNode[] path = GetPathTo(point);
            PinNativeTileHover(point, path);
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
            PinNativeEntityHover(entity, path);

            CombatInspectContext context = PathfinderExtensions.IsReachable(path, GetCurrentMovesLeft(), true)
                ? CombatInspectContext.ForEntityPath(entity.Position, ConvertPath(path))
                : CombatInspectContext.ForEntityOnly(entity.Position);
            context.TooltipDetails = BuildEntityDetails(entity);
            return context;
        }
    }
}
