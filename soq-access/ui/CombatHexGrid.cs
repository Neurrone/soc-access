using System.Collections.Generic;
using SongsOfConquestAccess.Adapters;
using SongsOfConquestAccess.Input;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.UI
{
    internal sealed class CombatHexGrid : Widget
    {
        private readonly CombatAdapter _adapter;
        private CombatSnapshot _snapshot;
        private Vector2Int _cursor;
        private CombatInspectContext _inspectContext;
        private bool _componentWarningSpoken;

        public CombatHexGrid(CombatAdapter adapter)
            : base("combat-hex-grid")
        {
            _adapter = adapter;
            RefreshSnapshot();
            _cursor = _adapter != null ? _adapter.GetInitialTile() : Vector2Int.zero;
        }

        public void AttachSpellCastBegin()
        {
            _adapter?.AttachSpellCastBegin(HandleSpellCastBegin);
        }

        public void DetachSpellCastBegin()
        {
            _adapter?.DetachSpellCastBegin();
        }

        public override bool AnnounceName
        {
            get { return true; }
        }

        public override string GetRole()
        {
            return string.Empty;
        }

        public override string GetLabel()
        {
            CombatTile tile = GetFocusedTile();
            string label = _adapter != null ? _adapter.DescribeTile(tile, GetEffectiveInspectContext()) : "Combat grid";
            if (_adapter != null && _adapter.IsSpellTargetingActive() && _adapter.IsSpellTargetSelected(_cursor))
            {
                return "selected, " + label;
            }

            return label;
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
                || actionKey == AccessibilityActions.CombatInspect.Key
                || actionKey == AccessibilityActions.CombatNextRelevantTile.Key
                || actionKey == AccessibilityActions.CombatPreviousRelevantTile.Key
                || (_adapter != null && _adapter.IsSpellTargetingActive() && actionKey == AccessibilityActions.Activate.Key)
                || actionKey == AccessibilityActions.MapSecondaryAction.Key
                || ((_inspectContext != null || (_adapter != null && _adapter.IsSpellTargetingActive())) && actionKey == AccessibilityActions.Cancel.Key);
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

            if (action.Key == AccessibilityActions.HexGridWest.Key)
            {
                return Move(-1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridEast.Key)
            {
                return Move(1, 0);
            }

            if (action.Key == AccessibilityActions.HexGridNorthWest.Key)
            {
                return MoveDiagonal(north: true, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridNorthEast.Key)
            {
                return MoveDiagonal(north: true, east: true);
            }

            if (action.Key == AccessibilityActions.HexGridSouthWest.Key)
            {
                return MoveDiagonal(north: false, east: false);
            }

            if (action.Key == AccessibilityActions.HexGridSouthEast.Key)
            {
                return MoveDiagonal(north: false, east: true);
            }

            if (action.Key == AccessibilityActions.CombatInspect.Key)
            {
                if (_adapter != null && (_adapter.IsSpellTargetingActive() || _adapter.HasVisibleSpellTargetInstruction()))
                {
                    return true;
                }

                return EnterInspect();
            }

            if (action.Key == AccessibilityActions.CombatNextRelevantTile.Key)
            {
                return MoveOrdered(1);
            }

            if (action.Key == AccessibilityActions.CombatPreviousRelevantTile.Key)
            {
                return MoveOrdered(-1);
            }

            if (action.Key == AccessibilityActions.MapSecondaryAction.Key)
            {
                _adapter?.HandleSecondaryAction(_cursor);
                UIManager.SetFocusedWidget(this);
                return true;
            }

            if (action.Key == AccessibilityActions.Activate.Key && _adapter != null && _adapter.IsSpellTargetingActive())
            {
                bool handled = _adapter.ConfirmSpellTarget(_cursor);
                UIManager.SetFocusedWidget(this);
                return handled;
            }

            if (action.Key == AccessibilityActions.Cancel.Key)
            {
                if (_adapter != null && _adapter.CancelSpellTargeting())
                {
                    UIManager.SetFocusedWidget(this);
                    return true;
                }

                return ExitInspect();
            }

            return false;
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

        private bool Move(int xDelta, int yDelta)
        {
            return SetCursor(new Vector2Int(_cursor.x + xDelta, _cursor.y + yDelta));
        }

        private bool MoveDiagonal(bool north, bool east)
        {
            int yDelta = north ? 1 : -1;
            int xDelta;
            if ((_cursor.y & 1) == 0)
            {
                xDelta = east ? 0 : -1;
            }
            else
            {
                xDelta = east ? 1 : 0;
            }

            return Move(xDelta, yDelta);
        }

        private bool EnterInspect()
        {
            if (_adapter != null && (_adapter.IsSpellTargetingActive() || _adapter.HasVisibleSpellTargetInstruction()))
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
            SpeechPipeline.Output(new SpeechRequest("Exited inspect mode", interrupt: false));
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
                && (_adapter == null || !_adapter.IsSpellTargetingActive())
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

        private void FocusCurrentTile(bool updateNativeFocus)
        {
            RefreshSnapshot();
            if (_adapter != null && _adapter.IsSpellTargetingActive())
            {
                if (_inspectContext != null)
                {
                    ExitInspect();
                    return;
                }

                _adapter.FocusSpellTargetTile(_cursor);
            }
            else if (updateNativeFocus)
            {
                _adapter?.FocusTile(_cursor);
            }

            _adapter?.SetFocusedTileOverlay(_cursor);
            UIManager.SetFocusedWidget(this);
        }

        private void HandleSpellCastBegin()
        {
            if (_inspectContext != null)
            {
                ExitInspect();
            }
        }

        private void MaybeSpeakDisconnectedWarning()
        {
            if (_componentWarningSpoken || _inspectContext == null || _inspectContext.Mode != CombatInspectMode.Stack)
            {
                return;
            }

            if (_inspectContext.CountConnectedComponents() > 1)
            {
                SpeechPipeline.Output(new SpeechRequest("Some shown tiles are separated. Use W and Shift W to reach all range tiles.", interrupt: false));
                _componentWarningSpoken = true;
            }
        }

        private static void SpeakInspectStarted(CombatInspectContext context)
        {
            string target = context != null ? context.TargetLabel : null;
            if (string.IsNullOrWhiteSpace(target))
            {
                target = "target";
            }

            SpeechPipeline.Output(new SpeechRequest("Inspecting " + target, interrupt: false));
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
            return _adapter != null && _adapter.IsSpellTargetingActive() ? null : _inspectContext;
        }

        private static int Mod(int value, int modulus)
        {
            int result = value % modulus;
            return result < 0 ? result + modulus : result;
        }

    }
}
