using System;
using SongsOfConquest.Client.Battle.HUD;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // SPELL AND ABILITY TARGETING STATE, moved out of CombatAdapter.cs unchanged: the game's own
    // targeting signals, the mode the battle is in, which tiles the spell has already taken, and
    // the confirm and cancel paths that go through the native controller.

    public sealed partial class CombatAdapter
    {
        /// <summary>Listen for the game asking for a spell target. The two facts it asks with - the
        /// spell's name and the instruction - are handed to the screen, which words and speaks
        /// them.</summary>
        public void AttachSpellTargetingNarration(Action<string, string> handler)
        {
            if (_battleHudSignals == null || handler == null || _targetInstructionHandler != null)
            {
                return;
            }

            _spellTargetInstructionHandler = handler;
            _targetInstructionHandler = HandleTargetInstruction;
            _spellTargetingEndHandler = HandleSpellTargetingEnd;
            _battleHudSignals.OnRequestTargetInstruction =
                (Action<ISpellDefinition, string>)Delegate.Combine(_battleHudSignals.OnRequestTargetInstruction, _targetInstructionHandler);
            _battleHudSignals.OnControllerCancelCast =
                (Action)Delegate.Combine(_battleHudSignals.OnControllerCancelCast, _spellTargetingEndHandler);
            _battleHudSignals.OnSpellbookCancelCast =
                (Action)Delegate.Combine(_battleHudSignals.OnSpellbookCancelCast, _spellTargetingEndHandler);
            _battleHudSignals.OnSpellEffectComplete =
                (Action)Delegate.Combine(_battleHudSignals.OnSpellEffectComplete, _spellTargetingEndHandler);
        }

        public void DetachSpellTargetingNarration()
        {
            if (_battleHudSignals == null || _targetInstructionHandler == null)
            {
                return;
            }

            _battleHudSignals.OnRequestTargetInstruction =
                (Action<ISpellDefinition, string>)Delegate.Remove(_battleHudSignals.OnRequestTargetInstruction, _targetInstructionHandler);
            if (_spellTargetingEndHandler != null)
            {
                _battleHudSignals.OnControllerCancelCast =
                    (Action)Delegate.Remove(_battleHudSignals.OnControllerCancelCast, _spellTargetingEndHandler);
                _battleHudSignals.OnSpellbookCancelCast =
                    (Action)Delegate.Remove(_battleHudSignals.OnSpellbookCancelCast, _spellTargetingEndHandler);
                _battleHudSignals.OnSpellEffectComplete =
                    (Action)Delegate.Remove(_battleHudSignals.OnSpellEffectComplete, _spellTargetingEndHandler);
            }
            _targetInstructionHandler = null;
            _spellTargetingEndHandler = null;
            _spellTargetInstructionHandler = null;
        }

        public void AttachSpellCastBegin(Action handler)
        {
            if (_battleHudSignals == null || handler == null || _beginCastHandler != null)
            {
                return;
            }

            _beginCastHandler = _ => handler();
            _battleHudSignals.OnBeginCast =
                (Action<ISpellDefinition>)Delegate.Combine(_battleHudSignals.OnBeginCast, _beginCastHandler);
        }

        public void DetachSpellCastBegin()
        {
            if (_battleHudSignals == null || _beginCastHandler == null)
            {
                return;
            }

            _battleHudSignals.OnBeginCast =
                (Action<ISpellDefinition>)Delegate.Remove(_battleHudSignals.OnBeginCast, _beginCastHandler);
            _beginCastHandler = null;
        }

        /// <summary>The screen's answer to "a new turn has begun, put the cursor on the troop". The
        /// narration asks THIS BATTLE'S adapter rather than reaching for whatever screen happens to
        /// be on top, and the screen decides whether the cursor is its to move.</summary>
        public void AttachActingTroopFocus(Action<int> handler)
        {
            _actingTroopFocusHandler = handler;
        }

        public void RequestActingTroopFocus(int troopId)
        {
            _actingTroopFocusHandler?.Invoke(troopId);
        }

        public void AttachAbilityTargetingBegin(Action<TroopAbilityTargeting> handler)
        {
            if (_battleHudSignals == null || handler == null || _beginAbilityTargetingHandler != null)
            {
                return;
            }

            _beginAbilityTargetingHandler = handler;
            _battleHudSignals.OnBeginAbilityTargeting =
                (Action<TroopAbilityTargeting>)Delegate.Combine(
                    _battleHudSignals.OnBeginAbilityTargeting, _beginAbilityTargetingHandler);
        }

        public void DetachAbilityTargetingBegin()
        {
            if (_battleHudSignals == null || _beginAbilityTargetingHandler == null)
            {
                return;
            }

            _battleHudSignals.OnBeginAbilityTargeting =
                (Action<TroopAbilityTargeting>)Delegate.Remove(
                    _battleHudSignals.OnBeginAbilityTargeting, _beginAbilityTargetingHandler);
            _beginAbilityTargetingHandler = null;
        }

        public void AttachAbilityTargetingEnd(Action<bool> handler)
        {
            if (_battleHudSignals == null || handler == null || _endAbilityTargetingHandler != null)
            {
                return;
            }

            _endAbilityTargetingHandler = handler;
            _battleHudSignals.OnEndAbilityTargeting =
                (Action<bool>)Delegate.Combine(_battleHudSignals.OnEndAbilityTargeting, _endAbilityTargetingHandler);
        }

        public void DetachAbilityTargetingEnd()
        {
            if (_battleHudSignals == null || _endAbilityTargetingHandler == null)
            {
                return;
            }

            _battleHudSignals.OnEndAbilityTargeting =
                (Action<bool>)Delegate.Remove(_battleHudSignals.OnEndAbilityTargeting, _endAbilityTargetingHandler);
            _endAbilityTargetingHandler = null;
        }

        /// <summary>The acting troop's ability, as the game names it.</summary>
        public string GetCurrentAbilityName()
        {
            IBattleTroopState current = GetCurrentTroop();
            ITroopAbilityDefinition ability = current != null && _abilityUtility != null
                ? _abilityUtility.GetAbilityDefinition(current)
                : null;
            return ability != null ? SpokenLines.Clean(GameText.Get(_localization, ability.NameKey, string.Empty)) : string.Empty;
        }

        /// <summary>The game's own instruction for what an ability wants aimed at.</summary>
        public string GetAbilityTargetInstruction(TroopAbilityTargeting targeting)
        {
            return SpokenLines.Clean(GameText.Get(_localization, "Battle/AbilityTargeting/" + targeting, string.Empty));
        }

        public CombatTargetingMode GetTargetingMode()
        {
            if (_battleSpellController != null)
            {
                HumanBattleSpellController.State state = _battleSpellController.CurrentState;
                if (state == HumanBattleSpellController.State.CastingBacteriaSpell
                    || state == HumanBattleSpellController.State.CastingTeleportSpell
                    || state == HumanBattleSpellController.State.CastingSummonSpell)
                {
                    return CombatTargetingMode.Spell;
                }
            }

            if (_humanBattleController != null
                && _humanBattleController.StateMachine != null
                && _humanBattleController.StateMachine.CurrentStateType == HumanBattleController.State.ChoosingAbilityTarget)
            {
                return CombatTargetingMode.Ability;
            }

            return CombatTargetingMode.None;
        }

        private bool IsAnySpellCastingStateActive()
        {
            if (GetTargetingMode() == CombatTargetingMode.Spell)
            {
                return true;
            }

            return _humanBattleController != null
                && _humanBattleController.StateMachine != null
                && _humanBattleController.StateMachine.CurrentStateType == HumanBattleController.State.CastingSpell;
        }

        public bool IsSpellTargetSelected(Vector2Int point)
        {
            return CountSpellTargetSelections(point) > 0;
        }

        private int CountSpellTargetSelections(Vector2Int point)
        {
            if (_battleSpellController == null || _battleSpellController.SelectedTargets == null)
            {
                return 0;
            }

            int count = 0;
            for (int i = 0; i < _battleSpellController.SelectedTargets.Count; i++)
            {
                if (_battleSpellController.SelectedTargets[i] == point)
                {
                    count++;
                }
            }

            return count;
        }

        public void FocusTargetTile(Vector2Int point)
        {
            CombatTargetingMode mode = GetTargetingMode();
            if (mode == CombatTargetingMode.None || !IsValidTile(point))
            {
                return;
            }

            SynchronizeNativeHoverForInput(point);
            if (mode == CombatTargetingMode.Spell)
            {
                _battleSpellController?.SetCurrentTile(point);
            }
            else if (mode == CombatTargetingMode.Ability)
            {
                UpdateNativeAttackPreviews();
            }
        }

        public CombatSpellTargetSelection ConfirmSpellTarget(Vector2Int point)
        {
            if (GetTargetingMode() != CombatTargetingMode.Spell || !IsValidTile(point))
            {
                return CombatSpellTargetSelection.None;
            }

            int previousSelectionCount = CountSpellTargetSelections(point);
            FocusTargetTile(point);
            if (_spellPrimaryClickMethod == null || _mouseKeyboardSpellInputModule == null)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter cannot confirm spell target because native HandlePrimaryClick was not found");
                return CombatSpellTargetSelection.None;
            }

            try
            {
                _spellPrimaryClickMethod.Invoke(_mouseKeyboardSpellInputModule, null);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to invoke native spell primary click: " + exception.Message);
                return CombatSpellTargetSelection.None;
            }

            return CombatSpellTargetSelection.Confirmed(
                previousSelectionCount,
                CountSpellTargetSelections(point),
                GetTargetingMode() == CombatTargetingMode.Spell);
        }

        public bool ConfirmAbilityTarget(Vector2Int point)
        {
            if (GetTargetingMode() != CombatTargetingMode.Ability || !IsValidTile(point))
            {
                return false;
            }

            ScreenInputOverride screenInputOverride;
            if (!TryBeginScreenInputOverride(point, out screenInputOverride))
            {
                return false;
            }

            try
            {
                FocusTargetTile(point);
                return InvokeNativeClick(_primaryClickMethod, "primary ability target");
            }
            finally
            {
                screenInputOverride.Restore();
            }
        }

        public bool CancelSpellTargeting()
        {
            if (GetTargetingMode() != CombatTargetingMode.Spell || _battleSpellController == null)
            {
                return false;
            }

            _battleSpellController.HandleSpellCastCancelled();
            return true;
        }

        public bool CancelAbilityTargeting()
        {
            if (GetTargetingMode() != CombatTargetingMode.Ability || _battleHudSignals == null)
            {
                return false;
            }

            IBattleTroopState current = GetCurrentTroop();
            if (current == null || (_abilityUtility != null && !_abilityUtility.CanBeAborted(current)))
            {
                return false;
            }

            _battleHudSignals.OnEndAbilityTargeting?.Invoke(false);
            return true;
        }

        private void HandleTargetInstruction(ISpellDefinition spell, string instruction)
        {
            string spellName = spell != null ? SpokenLines.Clean(GameText.Get(_localization, spell.NameKey, string.Empty)) : string.Empty;
            _spellTargetInstructionHandler?.Invoke(spellName, SpokenLines.Clean(instruction));
        }

        private void HandleSpellTargetingEnd()
        {
            Hud?.ClearSpellTargetInstructionText();
        }
    }
}
