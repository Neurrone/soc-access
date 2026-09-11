using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
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
    // captures that one here. Reading it back is the only way the tooltip can say what the mouse
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
        /// captures are keyed on, so the next load starts holding nothing.</summary>
        public static void Reset()
        {
            AttackPreviewAdditionalTexts.Clear();
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
