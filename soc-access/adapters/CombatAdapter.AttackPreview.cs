using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Spells;
using SongsOfConquest.Client.UI;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // THE GAME'S OWN ATTACK PREVIEW, READ BACK, moved out of CombatAdapter.cs unchanged. The game
    // draws what an attack would do over the target - the damage, the kills, and a sentence it
    // passes to AddAdditionalText and then keeps nowhere a reader can reach, which is why a hook
    // captures that one here. Reading it back is the only way the board node can say what the mouse
    // player can see.

    /// <summary>One attack preview the game is drawing, as the facts on it. The damage and kills
    /// are the game's own text, null where the game hides that part; the sentence is the one it
    /// adds under them. The troop is the stack the preview is drawn over, null when the mod did not
    /// see it placed. <see cref="UI.CombatAttackPreviewText"/> does the wording.</summary>
    public sealed class CombatAttackPreviewFacts
    {
        public CombatAttackPreviewFacts(string damage, string kills, string additional, bool targetIsEntity, TroopRef troop, bool isOnTargetTroop)
        {
            Damage = damage;
            Kills = kills;
            Additional = additional;
            TargetIsEntity = targetIsEntity;
            Troop = troop;
            IsOnTargetTroop = isOnTargetTroop;
        }

        public string Damage { get; private set; }
        public string Kills { get; private set; }
        public string Additional { get; private set; }
        /// <summary>The tile aimed at holds a map entity, not a troop.</summary>
        public bool TargetIsEntity { get; private set; }
        public TroopRef Troop { get; private set; }
        /// <summary>The preview is on the troop standing on the tile aimed at.</summary>
        public bool IsOnTargetTroop { get; private set; }

        /// <summary>A spell's preview on a map entity, which the mod knows the entity of. The name is
        /// null for the entity on the tile aimed at, which the tile has just named.</summary>
        public static CombatAttackPreviewFacts ForEntity(string damage, string kills, string entityName, Vector2Int entityPoint, bool isOnTarget)
        {
            CombatAttackPreviewFacts facts = new CombatAttackPreviewFacts(damage, kills, null, isOnTarget, null, false);
            facts.IsEntity = true;
            facts.EntityName = entityName;
            facts.EntityPoint = entityPoint;
            facts.IsOnTargetEntity = isOnTarget;
            return facts;
        }

        /// <summary>The preview is on a map entity the mod knows (<see cref="ForEntity"/>).</summary>
        public bool IsEntity { get; private set; }
        public string EntityName { get; private set; }
        public Vector2Int EntityPoint { get; private set; }
        /// <summary>The preview is on the map entity on the tile aimed at.</summary>
        public bool IsOnTargetEntity { get; private set; }
    }

    public sealed partial class CombatAdapter
    {
        // The extra sentence the game passes to AddAdditionalText and keeps nowhere readable, held
        // per preview because the hook is the only place it exists. Static, so it is dropped in
        // Reset from SocAccessMod.Stop: a preview the game destroyed would otherwise be held here
        // for the life of the process, across every hot reload.
        private static readonly Dictionary<BattleAttackPreview, string> AttackPreviewAdditionalTexts =
            new Dictionary<BattleAttackPreview, string>();

        [HookWritable]
        public static void CaptureAttackPreviewAdditionalText(BattleAttackPreview preview, string text)
        {
            if (preview == null)
            {
                return;
            }

            text = SpokenLines.Clean(text);
            if (string.IsNullOrWhiteSpace(text))
            {
                AttackPreviewAdditionalTexts.Remove(preview);
                return;
            }

            AttackPreviewAdditionalTexts[preview] = text;
        }

        // The troop each preview is drawn over, by id. The game hands the troop to
        // AnimateContainerAtTroop to place the preview and keeps no reference to it, so this hook is
        // the only place the pairing exists; without it an area's numbers could not be told apart.
        // A preview missing here (drawn before a hot reload) is read without a name, never a wrong
        // one. An owner-approved exception to "no patch for UI state", 2026-09-29.
        private static readonly Dictionary<BattleAttackPreview, int> AttackPreviewTroopIds =
            new Dictionary<BattleAttackPreview, int>();

        [HookWritable]
        public static void CaptureAttackPreviewTroop(BattleAttackPreview preview, IBattleTroopState troop)
        {
            if (preview == null)
            {
                return;
            }

            if (troop == null)
            {
                AttackPreviewTroopIds.Remove(preview);
                return;
            }

            AttackPreviewTroopIds[preview] = troop.Id;
        }

        /// <summary>The game hides every preview before its pool hands it out again, so a hidden
        /// preview lets go of both captures and an entity preview never carries a troop's.</summary>
        [HookWritable]
        public static void ClearAttackPreviewCaptures(BattleAttackPreview preview)
        {
            if (preview != null)
            {
                AttackPreviewAdditionalTexts.Remove(preview);
                AttackPreviewTroopIds.Remove(preview);
            }
        }

        /// <summary>The teardown <c>SocAccessMod.Stop</c> calls: let go of every attack preview the
        /// captures are keyed on, and of the battle holding the game's hover, so the next load
        /// starts holding nothing.</summary>
        public static void Reset()
        {
            AttackPreviewAdditionalTexts.Clear();
            AttackPreviewTroopIds.Clear();
            _hoverOwner = null;
        }

        // The captured lines, and everything they were true of. Read time is what triggers the
        // capture, so without this the hover sync and the game's own preview pass would run once per
        // frame under a still cursor: the tile and whether an inspection pinned it come from the
        // caller, and who stands there, how hurt they are, how many statuses they carry, what is
        // being aimed, the turn, where the acting troop stands and whether the game is waiting on a
        // command are read from the game. The waiting is in the key because the game's own preview
        // refuses to draw through it (BattleAttackPreview.Show); the statuses are because a wielder's
        // spell changes the damage without taking any health, and the acting troop's position is
        // because the preview is worked out FROM it - its range and its high ground - and a troop with
        // movement left repositions without the turn moving on.
        private IList<CombatAttackPreviewFacts> _previews;
        private bool _previewRead;
        private Vector2Int _previewPoint;
        private bool _previewPinned;
        private CombatTargetingMode _previewTargeting;
        private int _previewTroopId = -1;
        private int _previewHealthLost;
        private int _previewStatusCount;
        private int _previewTurn;
        private Vector2Int _previewCurrentTroopPoint;
        private bool _previewWaiting;
        // The spell being aimed and the game's damage preview setting: a spell cancelled and another
        // aimed at the same tile, or the setting turned off, reads anew.
        private ISpellDefinition _previewSpell;
        private SpellTier _previewSpellTier;
        private bool _previewDamagePreview;
        private ICommandWaiter _commandWaiter;
        private bool _commandWaiterProbed;

        /// <summary>
        /// WHAT AN ATTACK ON THIS TILE WOULD DO, in the game's own numbers and its own sentences -
        /// the damage, the kills, and the reason it would not land.
        ///
        /// Captured when the node is READ rather than when it is built: the game composes the
        /// preview for the tile its hover is on, so the hover is put on the tile and the game's own
        /// preview pass run right here, under the same guards <see cref="FocusTile"/> syncs under.
        /// Nothing about the answer then depends on which order the frame ran in.
        ///
        /// While an inspection is pinned the preview is the pinned tile's alone, as the tooltip is:
        /// the cursor walking the inspected ranges reads no preview.
        /// </summary>
        public IList<CombatAttackPreviewFacts> ReadAttackPreviews(CombatInspectContext context, Vector2Int focusedTile)
        {
            if (context != null && focusedTile != context.PinnedTile)
            {
                return null;
            }

            Vector2Int point = context != null ? context.PinnedTile : focusedTile;
            bool pinned = context != null;
            CombatTargetingMode targeting = GetTargetingMode();
            int troopId;
            int healthLost;
            int statusCount;
            GetTileTroopState(point, out troopId, out healthLost, out statusCount);
            int turn = GetCurrentTurn();
            Vector2Int currentTroopPoint = GetCurrentTroopPosition();
            bool waiting = IsCommandWaiting();
            ISpellDefinition spell;
            SpellTier spellTier;
            GetAimedSpell(out spell, out spellTier);
            bool damagePreview = IsDamagePreviewEnabled();
            if (_previewRead
                && spell == _previewSpell
                && spellTier == _previewSpellTier
                && damagePreview == _previewDamagePreview
                && point == _previewPoint
                && pinned == _previewPinned
                && targeting == _previewTargeting
                && troopId == _previewTroopId
                && healthLost == _previewHealthLost
                && statusCount == _previewStatusCount
                && turn == _previewTurn
                && currentTroopPoint == _previewCurrentTroopPoint
                && waiting == _previewWaiting)
            {
                return _previews;
            }

            _previewPoint = point;
            _previewPinned = pinned;
            _previewTargeting = targeting;
            _previewTroopId = troopId;
            _previewHealthLost = healthLost;
            _previewStatusCount = statusCount;
            _previewTurn = turn;
            _previewCurrentTroopPoint = currentTroopPoint;
            _previewWaiting = waiting;
            _previewSpell = spell;
            _previewSpellTier = spellTier;
            _previewDamagePreview = damagePreview;
            _previewRead = true;
            _previews = CapturePreviewFor(point);
            return _previews;
        }

        private IList<CombatAttackPreviewFacts> CapturePreviewFor(Vector2Int point)
        {
            CombatTile tile = GetTile(point);
            if (tile == null)
            {
                return null;
            }

            // A spell's preview is asked of the game for the tile, empty or not: an area spell aimed
            // at an empty hex previews every stack it would hit.
            if (GetTargetingMode() == CombatTargetingMode.Spell)
            {
                return CaptureSpellPreviews(tile, point);
            }

            // An area ability may be aimed at an empty hex, and the game draws a preview on every
            // stack it would hit; anything else on an empty hex has no preview to read.
            bool aimingAbility = GetTargetingMode() == CombatTargetingMode.Ability;
            if (tile.Troop == null && tile.Entity == null && !aimingAbility)
            {
                return null;
            }

            if (GetTargetingMode() == CombatTargetingMode.None && !IsAnySpellCastingStateActive())
            {
                SynchronizeNativeHoverForPreview(point, tile, GetPathTo(point));
            }
            else if (aimingAbility && _humanBattleController != null && _humanBattleController.CurrentHoverTile == point)
            {
                // The aim already drew these (FocusTargetTile); drawn again so every preview passes
                // the troop hook in this load, a hot reload having emptied it.
                UpdateNativeAttackPreviews();
            }

            return CaptureAttackPreviews(tile);
        }

        /// <summary>Whether the game is waiting on a command to play out, which is one of the two
        /// states its own preview refuses to draw through. The waiter is a plain instance binding on
        /// the battle's container (BattleSceneInstaller), resolved once and the miss remembered.
        /// </summary>
        private bool IsCommandWaiting()
        {
            if (!_commandWaiterProbed)
            {
                _commandWaiterProbed = true;
                _commandWaiter = Reflect.Resolve<ICommandWaiter>(_container);
            }

            return _commandWaiter != null && _commandWaiter.IsWaiting;
        }

        /// <summary>Every preview the game is drawing, in the order it drew them, as the facts on
        /// it: its numbers and sentence as the game wrote them (null where the game hides them), the
        /// troop the hook saw it placed over, and whether that is the troop on the tile.</summary>
        private List<CombatAttackPreviewFacts> CaptureAttackPreviews(CombatTile tile)
        {
            bool targetIsEntity = tile.Troop == null && tile.Entity != null;
            List<CombatAttackPreviewFacts> facts = new List<CombatAttackPreviewFacts>();
            foreach (BattleAttackPreview preview in GetActiveAttackPreviews())
            {
                // Handed out by the pool but not drawn: the game's damage preview setting is off,
                // or it is waiting on a command. Its text is whatever it last showed.
                if (!IsPreviewShown(preview))
                {
                    continue;
                }

                string damage = GetPreviewText(preview, _attackPreviewDamageTextField);
                string kills = GetPreviewText(preview, _attackPreviewKillsTextField);
                string additional = GetCapturedAdditionalText(preview);
                if (string.IsNullOrWhiteSpace(additional))
                {
                    additional = GetPreviewText(preview, _attackPreviewAdditionalTextField);
                }

                IBattleTroopState troop = GetAttackPreviewTroop(preview);
                facts.Add(new CombatAttackPreviewFacts(
                    IsPreviewContainerVisible(preview, _attackPreviewDamageContainerField) ? damage : null,
                    IsPreviewContainerVisible(preview, _attackPreviewKillsContainerField) ? kills : null,
                    additional,
                    targetIsEntity,
                    troop != null ? CreateTroopRef(troop) : null,
                    troop != null && tile.Troop != null && troop.Id == tile.Troop.Id));
            }

            return facts;
        }

        /// <summary>The troop the troop hook saw this preview placed over, as it stands now.</summary>
        private IBattleTroopState GetAttackPreviewTroop(BattleAttackPreview preview)
        {
            int troopId;
            if (preview == null || _facade == null || !AttackPreviewTroopIds.TryGetValue(preview, out troopId))
            {
                return null;
            }

            try
            {
                return _facade.Troops.Get(troopId);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter could not read an attack preview's troop: " + exception.Message);
                return null;
            }
        }

        private List<BattleAttackPreview> GetActiveAttackPreviews()
        {
            List<BattleAttackPreview> previews = new List<BattleAttackPreview>();
            if (_attackPreviewHandler == null || _attackPreviewPoolField == null)
            {
                return previews;
            }

            try
            {
                object pool = _attackPreviewPoolField.GetValue(_attackPreviewHandler);
                if (pool == null)
                {
                    return previews;
                }

                MethodInfo getActive = AccessTools.Method(pool.GetType(), "GetActive");
                IEnumerable active = getActive != null ? getActive.Invoke(pool, null) as IEnumerable : null;
                if (active == null)
                {
                    return previews;
                }

                foreach (object item in active)
                {
                    BattleAttackPreview preview = item as BattleAttackPreview;
                    if (preview != null)
                    {
                        previews.Add(preview);
                    }
                }
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to capture attack preview text: " + exception.Message);
            }

            return previews;
        }

        /// <summary>Whether the game is showing this preview: every show places it through
        /// AnimateContainer, which activates its container, and Hide fades the container out and
        /// deactivates it.</summary>
        private bool IsPreviewShown(BattleAttackPreview preview)
        {
            CanvasGroup container = preview != null && _attackPreviewContainerField != null
                ? _attackPreviewContainerField.GetValue(preview) as CanvasGroup
                : null;
            return container != null && container.gameObject.activeSelf;
        }

        private bool IsPreviewContainerVisible(BattleAttackPreview preview, FieldInfo field)
        {
            GameObject container = preview != null && field != null ? field.GetValue(preview) as GameObject : null;
            return container != null && container.activeSelf;
        }

        private string GetPreviewText(BattleAttackPreview preview, FieldInfo field)
        {
            UITextMesh text = preview != null && field != null ? field.GetValue(preview) as UITextMesh : null;
            return text != null ? TrimSentence(SpokenLines.Clean(UITextMeshTextUtility.GetEffectiveText(text))) : string.Empty;
        }

        private static string GetCapturedAdditionalText(BattleAttackPreview preview)
        {
            string text;
            return preview != null && AttackPreviewAdditionalTexts.TryGetValue(preview, out text)
                ? TrimSentence(text)
                : string.Empty;
        }

        private static string TrimSentence(string text)
        {
            text = text != null ? text.Trim() : string.Empty;
            while (text.EndsWith(".", StringComparison.Ordinal))
            {
                text = text.Substring(0, text.Length - 1).TrimEnd();
            }

            return text;
        }

        private void UpdateNativeAttackPreviews()
        {
            if (_mouseKeyboardInputModule == null || _updateAttackPreviewsMethod == null)
            {
                return;
            }

            try
            {
                _updateAttackPreviewsMethod.Invoke(_mouseKeyboardInputModule, null);
            }
            catch (Exception exception)
            {
                SocAccessMod.Instance?.LogWarning("CombatAdapter failed to update native attack previews: " + exception.Message);
            }
        }
    }
}
