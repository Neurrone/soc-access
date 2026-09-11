using System;
using SongsOfConquest;
using SongsOfConquest.Common;
using SongsOfConquest.Common.Bacterias;
using SongsOfConquest.Common.Battle;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Spells;
using SongsOfConquestAccess.Events.Combat;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    // WHAT THE NARRATION IS TOLD ABOUT, moved out of CombatAdapter.cs unchanged. A combat event
    // is published long after the thing it is about may have changed or gone, so nothing is handed
    // a live game object: each of these reads the game once and answers with a ref - the id, the
    // side, the game's own name - that the events layer can say at any time.

    public sealed partial class CombatAdapter
    {
        public TroopRef CreateTroopRef(IBattleTroopState troop)
        {
            return CreateTroopRef(troop, troop != null ? troop.Stats.Size : 0, troop != null ? troop.Position : Vector2Int.zero);
        }

        public TroopRef CreateTroopRef(IBattleTroopState troop, int sizeOverride)
        {
            return CreateTroopRef(troop, sizeOverride, troop != null ? troop.Position : Vector2Int.zero);
        }

        public TroopRef CreateTroopRef(IBattleTroopState troop, int sizeOverride, Vector2Int positionOverride)
        {
            if (troop == null)
            {
                return new TroopRef(-1, -1, GetLocalTeamId(), ModText.Get(ModStrings.Combat.UnknownTroop), sizeOverride, positionOverride);
            }

            string name = SpokenLines.Clean(_facade.Troops.GetName(troop.Id, sizeOverride));
            return new TroopRef(troop.Id, troop.TeamId, GetLocalTeamId(), name, sizeOverride, positionOverride);
        }

        public EntityRef CreateEntityRef(IMapEntity entity)
        {
            if (entity == null)
            {
                return new EntityRef(-1, -1, ModText.Get(ModStrings.Combat.UnknownEntity), Vector2Int.zero);
            }

            string name = GetMapEntityName(entity);
            return new EntityRef(entity.Id, entity.BlueprintId, name, entity.Position);
        }

        public CommanderRef CreateCommanderRef(int commanderId)
        {
            try
            {
                ICommanderState commander = _facade != null && _facade.Commanders != null ? _facade.Commanders.Get(commanderId) : null;
                string name = _facade != null && _facade.Commanders != null ? _facade.Commanders.GetShortName(commanderId) : string.Empty;
                return new CommanderRef(commanderId, commander != null ? commander.TeamId : -1, GetLocalTeamId(), name);
            }
            catch (Exception exception)
            {
                _faults.Report("CreateCommanderRef", exception);
                return new CommanderRef(commanderId, -1, GetLocalTeamId(), ModText.Get(ModStrings.Combat.Wielder));
            }
        }

        public SpellRef CreateSpellRef(SpellTypes spellType, int tier)
        {
            string name = LocalizeSpellName(spellType);
            return new SpellRef(spellType, name, tier);
        }

        public AbilityRef CreateAbilityRef(TroopAbilityType abilityType)
        {
            string name = LocalizeAbilityName(abilityType);
            return new AbilityRef(abilityType, name);
        }

        public ModifierChange CreateModifierChange(BacteriaModifier modifier)
        {
            if (modifier == null)
            {
                return null;
            }

            string nameKey = ModifierLocalizationKey(
                modifier.Type,
                modifier.ApplicationType,
                modifier.AmountToAdd,
                out bool formatAmount,
                out int displayAmountMultiplier);
            return new ModifierChange(
                modifier.Type,
                modifier.ApplicationType,
                modifier.AmountToAdd,
                LocalizeText(nameKey + "/Description"),
                formatAmount,
                displayAmountMultiplier,
                LocalizeText(nameKey));
        }

        /// <summary>The game's own key for a modifier, which names it and, with "/Description"
        /// after it, describes it (<c>BacteriaModifierExtensions.GetModifierLocalizedNameKey</c>).
        /// Blessed by a negative amount is the game's Cursed, which it counts the other way up.
        /// </summary>
        private static string ModifierLocalizationKey(
            BacteriaModifierType modifierType,
            BacteriaModifierApplicationType applicationType,
            int amount,
            out bool formatAmount,
            out int displayAmountMultiplier)
        {
            formatAmount = modifierType != BacteriaModifierType.TroopIgnoreZoneOfControl;
            displayAmountMultiplier = 1;

            if (modifierType == BacteriaModifierType.TroopBlessed
                && amount < 0
                && applicationType != BacteriaModifierApplicationType.Percentage
                && !BacteriaModifierExtensions.IsPercentageBased(modifierType))
            {
                displayAmountMultiplier = -1;
                return "Modifiers/Cursed";
            }

            return "Modifiers/" + modifierType.ToString().Replace("Troop", string.Empty);
        }

        public BacteriaRef CreateBacteriaRef(BacteriaReference bacteriaReference)
        {
            if (bacteriaReference == null)
            {
                return null;
            }

            string name = LocalizeText(BacteriaReferenceUtility.GetLocalizationNameKey(bacteriaReference.BacteriaType));
            return new BacteriaRef(bacteriaReference.Id, bacteriaReference.BacteriaType, name);
        }

        public BacteriaRef CreateBacteriaRef(BacteriaTypes bacteriaType)
        {
            string name = LocalizeText(BacteriaReferenceUtility.GetLocalizationNameKey(bacteriaType));
            return new BacteriaRef(-1, bacteriaType, name);
        }

        private string LocalizeSpellName(SpellTypes spellType)
        {
            try
            {
                ISpellDefinition definition = _spellsLookup != null ? _spellsLookup.GetSpellDefinition(spellType) : null;
                string name = definition != null ? GameText.Get(_localization, definition.NameKey, string.Empty) : string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch (Exception exception)
            {
                _faults.Report("LocalizeSpellName", exception);
            }

            return LocalizeText("Spells/" + spellType);
        }

        private string LocalizeAbilityName(TroopAbilityType abilityType)
        {
            try
            {
                ITroopAbilityDefinition definition = _abilityUtility != null ? _abilityUtility.GetAbilityDefinition(abilityType) : null;
                string name = definition != null ? GameText.Get(_localization, definition.NameKey, string.Empty) : string.Empty;
                if (!string.IsNullOrWhiteSpace(name))
                {
                    return name;
                }
            }
            catch (Exception exception)
            {
                _faults.Report("LocalizeAbilityName", exception);
            }

            return LocalizeText("TroopAbilities/" + abilityType);
        }
    }
}
