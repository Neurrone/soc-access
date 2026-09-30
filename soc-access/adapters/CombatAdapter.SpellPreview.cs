using System;
using System.Collections.Generic;
using SongsOfConquest;
using SongsOfConquest.Common;
using SongsOfConquest.Client.Battle.Controller;
using SongsOfConquest.Client.Settings;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Battle.Facade;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // THE GAME'S SPELL DAMAGE PREVIEW, asked of the game rather than read back. The preview a spell
    // draws (BattleSpellPreviewHandler) keeps neither its troop nor anything the mod could match it
    // to, but the controller builds it from two public game calls - the targets the spell would hit
    // from the hovered tile (ICommonSpellHandler.GetTargetsForSpell) and each one's numbers
    // (IClientBattleSpellFacade.GetSpellPreview) - so the mod makes the same two calls for the tile
    // being read. The filtering around them is HumanBattleSpellController.HandleCastingUpdate's and
    // BattleSpellPreviewHandler.Show's, kept in step with them here: no preview with the game's
    // damage preview setting off, while a teleport is aimed, or for a spell aimed at friendlies;
    // none on a dead or magic-immune troop, on a map entity without health, or where the damage is
    // nothing. Chain Lightning's jumps are not previewed by the game and are not read here.
    public sealed partial class CombatAdapter
    {
        private ICommonSpellHandler _spellHandler;
        private bool _spellHandlerProbed;
        private IClientSettings _clientSettings;
        private bool _clientSettingsProbed;

        /// <summary>Whether the game's own "damage preview" gameplay setting is on. Nothing the game
        /// computes as a damage number is drawn with it off. True when the setting cannot be read, so
        /// a missing binding costs nothing but the setting.</summary>
        private bool IsDamagePreviewEnabled()
        {
            if (!_clientSettingsProbed)
            {
                _clientSettingsProbed = true;
                _clientSettings = Reflect.Resolve<IClientSettings>(_container);
            }

            return _clientSettings == null || _clientSettings.UseDamagePreview;
        }

        private ICommonSpellHandler SpellHandler()
        {
            if (!_spellHandlerProbed)
            {
                _spellHandlerProbed = true;
                _spellHandler = Reflect.Resolve<ICommonSpellHandler>(_container);
            }

            return _spellHandler;
        }

        /// <summary>The spell being aimed and its tier, or nulls when no spell is. Part of the
        /// preview cache's key: a spell cancelled and another aimed at the same tile reads anew.
        /// </summary>
        private void GetAimedSpell(out ISpellDefinition spell, out SpellTier tier)
        {
            spell = null;
            tier = null;
            if (_battleSpellController == null || GetTargetingMode() != CombatTargetingMode.Spell)
            {
                return;
            }

            spell = _battleSpellController.CurrentSpell;
            tier = _battleSpellController.CurrentSpellTier;
        }

        /// <summary>What the spell being aimed would do to everything it hits from
        /// <paramref name="point"/>, as the game's own preview would draw it there: one fact per
        /// target, in the game's target order.</summary>
        private List<CombatAttackPreviewFacts> CaptureSpellPreviews(CombatTile tile, Vector2Int point)
        {
            List<CombatAttackPreviewFacts> facts = new List<CombatAttackPreviewFacts>();
            ISpellDefinition spell;
            SpellTier tier;
            GetAimedSpell(out spell, out tier);
            ICommonSpellHandler spellHandler = SpellHandler();
            if (spell == null
                || tier == null
                || spellHandler == null
                || _facade == null
                || _battleSpellController.CurrentState == HumanBattleSpellController.State.CastingTeleportSpell
                || tier.Target == SpellTargetType.Friendly
                || !IsDamagePreviewEnabled())
            {
                return facts;
            }

            try
            {
                bool targetIsEntity = tile.Troop == null && tile.Entity != null;
                foreach (SpellTargetDefinition target in spellHandler.GetTargetsForSpell((SpellTypes)spell.Id, tier, point))
                {
                    foreach (IBattleTroopState troop in _facade.Troops.All)
                    {
                        if (troop.Position != target.Position || !troop.GetIsAlive() || troop.IsImmuneToMagic())
                        {
                            continue;
                        }

                        SpellPreview preview = _facade.Spell.GetSpellPreview(spell, _facade.Commanders.Current, troop);
                        if (preview.Damage == 0)
                        {
                            continue;
                        }

                        facts.Add(new CombatAttackPreviewFacts(
                            preview.Damage.ToString(),
                            preview.Kills.ToString(),
                            SpellPreviewAdditionalText(troop, preview),
                            targetIsEntity,
                            CreateTroopRef(troop),
                            tile.Troop != null && troop.Id == tile.Troop.Id));
                    }

                    foreach (IMapEntity entity in _facade.MapEntities.All)
                    {
                        if (entity.Position != target.Position || !entity.HasComponent<IHealthComponent>())
                        {
                            continue;
                        }

                        SpellPreview preview = _facade.Spell.GetSpellPreview(spell, _facade.Commanders.Current, entity);
                        if (preview.Damage == 0)
                        {
                            continue;
                        }

                        bool onTarget = entity.Position == point;
                        // The game draws a map entity's kills as 1 or 0: whether it falls. None is
                        // passed for 0, so an entity that stands is not read as destroyed.
                        facts.Add(CombatAttackPreviewFacts.ForEntity(
                            preview.Damage.ToString(),
                            preview.Kills > 0 ? preview.Kills.ToString() : null,
                            onTarget ? null : GetEntityFacts(entity).Name,
                            entity.Position,
                            onTarget));
                    }
                }
            }
            catch (Exception exception)
            {
                _faults.Report("CaptureSpellPreviews", exception);
            }

            return facts;
        }

        /// <summary>The lines the game writes under a troop's spell preview: its spell damage
        /// resistance, then what Mother's Embrace takes off (BattleSpellPreviewHandler.Show).</summary>
        private string SpellPreviewAdditionalText(IBattleTroopState troop, SpellPreview preview)
        {
            List<string> lines = new List<string>(2);
            int resistance = troop.Stats.SpellDamageResistancePercent.GetValue();
            if (resistance > 0)
            {
                lines.Add(SpokenLines.Clean(GameText.Get(
                    _localization,
                    "Battle/InspectTroop/SpellPreview/Resistance",
                    string.Empty,
                    "-" + resistance + "%")));
            }

            if (preview.MothersLoveDamageReduction > 0)
            {
                int total = preview.Damage + preview.MothersLoveDamageReduction;
                int percent = Mathf.CeilToInt((float)preview.MothersLoveDamageReduction / total * 100f);
                lines.Add(SpokenLines.Clean(GameText.Get(
                    _localization,
                    "Battle/InspectTroop/AttackPreview/MothersLoveReduction",
                    string.Empty,
                    "-" + percent + "%")));
            }

            lines.RemoveAll(string.IsNullOrWhiteSpace);
            return lines.Count > 0 ? string.Join("\n", lines.ToArray()) : null;
        }
    }
}
