using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using SongsOfConquest.Client;
using SongsOfConquest.Client.Battle.View;
using SongsOfConquest.Client.UI;
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

        [HookWritable]
        public static void ClearAttackPreviewAdditionalText(BattleAttackPreview preview)
        {
            if (preview != null)
            {
                AttackPreviewAdditionalTexts.Remove(preview);
            }
        }

        /// <summary>The teardown <c>SocAccessMod.Stop</c> calls: let go of every attack preview the
        /// captures are keyed on, and of the battle holding the game's hover, so the next load
        /// starts holding nothing.</summary>
        public static void Reset()
        {
            AttackPreviewAdditionalTexts.Clear();
            _hoverOwner = null;
        }

        // The captured lines, and everything they were true of. Read time is what triggers the
        // capture, so without this the hover sync and the game's own preview pass would run once per
        // frame under a still cursor: the tile and whether an inspection pinned it come from the
        // caller, and who stands there, how hurt they are, what is being aimed, the turn and whether
        // the game is waiting on a command are read from the game. The waiting is in the key because
        // the game's own preview refuses to draw through it (BattleAttackPreview.Show).
        private IList<string> _previewLines;
        private bool _previewRead;
        private Vector2Int _previewPoint;
        private bool _previewPinned;
        private CombatTargetingMode _previewTargeting;
        private int _previewTroopId = -1;
        private int _previewHealthLost;
        private int _previewTurn;
        private bool _previewWaiting;
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
        public IList<string> ReadAttackPreviewLines(CombatInspectContext context, Vector2Int focusedTile)
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
            GetTileTroopState(point, out troopId, out healthLost);
            int turn = GetCurrentTurn();
            bool waiting = IsCommandWaiting();
            if (_previewRead
                && point == _previewPoint
                && pinned == _previewPinned
                && targeting == _previewTargeting
                && troopId == _previewTroopId
                && healthLost == _previewHealthLost
                && turn == _previewTurn
                && waiting == _previewWaiting)
            {
                return _previewLines;
            }

            _previewPoint = point;
            _previewPinned = pinned;
            _previewTargeting = targeting;
            _previewTroopId = troopId;
            _previewHealthLost = healthLost;
            _previewTurn = turn;
            _previewWaiting = waiting;
            _previewRead = true;
            // Split where the game drew a line: an additional sentence it wrote over two lines is
            // two lines to read, exactly as a tooltip's are.
            _previewLines = SpokenLines.Of(CapturePreviewFor(point));
            return _previewLines;
        }

        private IList<string> CapturePreviewFor(Vector2Int point)
        {
            CombatTile tile = GetTile(point);
            if (tile == null || (tile.Troop == null && tile.Entity == null))
            {
                return null;
            }

            if (GetTargetingMode() == CombatTargetingMode.None && !IsAnySpellCastingStateActive())
            {
                SynchronizeNativeHoverForPreview(point, tile, GetPathTo(point));
            }

            return CaptureAttackPreviewLines(tile.Troop == null && tile.Entity != null);
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

        private List<string> CaptureAttackPreviewLines(bool targetIsEntity)
        {
            List<string> lines = new List<string>();
            List<BattleAttackPreview> previews = GetActiveAttackPreviews();
            for (int i = 0; i < previews.Count; i++)
            {
                BattleAttackPreview preview = previews[i];
                string damage = GetPreviewText(preview, _attackPreviewDamageTextField);
                string kills = GetPreviewText(preview, _attackPreviewKillsTextField);
                string additional = GetCapturedAdditionalText(preview);
                if (string.IsNullOrWhiteSpace(additional))
                {
                    additional = GetPreviewText(preview, _attackPreviewAdditionalTextField);
                }
                bool hasDamage = IsPreviewContainerVisible(preview, _attackPreviewDamageContainerField)
                    && !string.IsNullOrWhiteSpace(damage);
                bool hasKills = IsPreviewContainerVisible(preview, _attackPreviewKillsContainerField)
                    && !string.IsNullOrWhiteSpace(kills);

                List<string> parts = new List<string>();
                string prefix = previews.Count > 1
                    ? (i == 0 ? ModText.Get(ModStrings.Spatial.PrimaryPrefix) : ModText.Get(ModStrings.Spatial.ExtraTargetPrefix))
                    : string.Empty;
                if (hasDamage)
                {
                    parts.Add(ModText.Get(ModStrings.Spatial.DamagePreview, prefix, damage));
                }

                if (hasKills)
                {
                    parts.Add(targetIsEntity ? FormatEntityDestruction(kills) : ModText.Get(ModStrings.Spatial.Kills, kills));
                }

                if (parts.Count > 0)
                {
                    lines.Add(string.Join(", ", parts.ToArray()) + ".");
                }

                if (!string.IsNullOrWhiteSpace(additional))
                {
                    lines.Add(additional + ".");
                }
            }

            return lines;
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

        private static string FormatEntityDestruction(string killsText)
        {
            int min;
            int max;
            if (TryParseRange(killsText, out min, out max))
            {
            return min <= 0 && max > 0
                ? ModText.Get(ModStrings.Spatial.MayDestroy)
                : ModText.Get(ModStrings.Spatial.Destroys);
            }

            return ModText.Get(ModStrings.Spatial.Destroys);
        }

        private static bool TryParseRange(string text, out int min, out int max)
        {
            min = 0;
            max = 0;
            MatchCollection matches = Regex.Matches(text ?? string.Empty, "\\d+");
            if (matches.Count == 0)
            {
                return false;
            }

            min = int.Parse(matches[0].Value);
            max = matches.Count > 1 ? int.Parse(matches[1].Value) : min;
            return true;
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
