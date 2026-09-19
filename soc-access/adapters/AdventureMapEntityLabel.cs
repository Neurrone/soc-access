using System;
using System.Collections.Generic;
using SongsOfConquest.Client.Gamestate;
using SongsOfConquest.Client.Gamestate.Facade;
using SongsOfConquest.Common.Artifacts;
using SongsOfConquest.Common.Details;
using SongsOfConquest.Common.Entities;
using SongsOfConquest.Common.Entities.Adventure;
using SongsOfConquest.Common.Gamestate;
using SongsOfConquest.Common.Gamestate.Facade;
using SongsOfConquest.Common.Localization;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using SongsOfConquestAccess.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public static class AdventureMapEntityLabel
    {
        public static string GetMapEntityName(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity)
        {
            return GetMapEntityName(facade, selectionHandler, localization, entity, null);
        }

        /// <param name="scoutedAt">The scouting detail level to name the entity at, or null for the
        /// one the game answers for its tile now.</param>
        private static string GetMapEntityName(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity,
            ScoutingDetailLevel? scoutedAt)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string customNameKey = string.Empty;
            if (entity.TryGetCustomNameKey(out customNameKey))
            {
                string customName = Localize(localization, customNameKey);
                if (!string.IsNullOrWhiteSpace(customName))
                {
                    return AddEssenceVariant(localization, entity, customName);
                }
            }

            string preVisitName;
            if (TryGetPreVisitLabel(facade, selectionHandler, localization, entity, scoutedAt, out preVisitName))
            {
                return AddEssenceVariant(localization, entity, preVisitName);
            }

            string localizedName = Localize(localization, entity.NameKey);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return AddEssenceVariant(localization, entity, localizedName);
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return AddEssenceVariant(localization, entity, entity.Name);
            }

            return AddEssenceVariant(localization, entity, entity.NameKey);
        }

        /// <summary>
        /// Everything <see cref="GetMapEntityName"/> reads for the name EXCEPT the language: the
        /// keys it can name the entity from, the essence variant it appends and the scouting detail
        /// the game answers for the entity's tile, which is what turns "battle loot" into the
        /// artifacts themselves. Two reads that agree here name the entity the same thing in
        /// whatever language is current, so a caller that remembers a name can tell a real change
        /// from a translated one. The scouting PROVIDER is deliberately left out: it names whichever
        /// friendly unit happens to be nearest and changes with nearly every step, while what the
        /// entity is called does not turn on it.
        /// </summary>
        public static string GetMapEntityNameSignature(IClientAdventureFacade facade, IMapEntity entity)
        {
            if (entity == null)
            {
                return string.Empty;
            }

            string customNameKey;
            if (!entity.TryGetCustomNameKey(out customNameKey))
            {
                customNameKey = string.Empty;
            }

            EssenceType essence;
            int essenceVariant = TryGetSelectedEssenceVariant(entity, out essence) ? (int)essence : -1;
            return string.Concat(
                customNameKey ?? string.Empty,
                "|",
                entity.NameKey ?? string.Empty,
                "|",
                entity.Name ?? string.Empty,
                "|",
                (int)GetScouting(facade, entity).DetailLevel,
                "|",
                essenceVariant);
        }

        /// <summary>
        /// Whether an entity read under <paramref name="previousSignature"/> is still CALLED the same
        /// under <paramref name="signature"/>. A signature says what the name is read from, and one
        /// of those things is the scouting detail level, which the game buckets from how far the
        /// nearest friendly unit stands: it changes as a wielder walks past, and for most entities
        /// the name does not turn on it at all (it is what turns "battle loot" into the artifacts
        /// themselves, and little else). So where the level is the ONLY thing that moved, the name
        /// is read at the level it was read at before and at the level it is read at now, and the
        /// two are compared. Anything else moving is a different name by definition.
        /// </summary>
        public static bool NamesTheSame(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity,
            string previousSignature,
            string signature)
        {
            string previousRest;
            string rest;
            int previousLevel;
            int level;
            if (!TrySplitScoutingLevel(previousSignature, out previousRest, out previousLevel)
                || !TrySplitScoutingLevel(signature, out rest, out level)
                || !string.Equals(previousRest, rest, StringComparison.Ordinal))
            {
                return false;
            }

            if (previousLevel == level)
            {
                return true;
            }

            string before = GetMapEntityName(
                facade, selectionHandler, localization, entity, (ScoutingDetailLevel)previousLevel);
            string now = GetMapEntityName(facade, selectionHandler, localization, entity, null);
            return string.Equals(before, now, StringComparison.Ordinal);
        }

        /// <summary>A signature with its scouting level taken out: the level is the last field but
        /// one (<see cref="GetMapEntityNameSignature"/>), and the two fields after the names are
        /// numbers, so they are found from the end whatever the names hold.</summary>
        private static bool TrySplitScoutingLevel(string signature, out string rest, out int level)
        {
            rest = string.Empty;
            level = 0;
            int essenceAt = signature != null ? signature.LastIndexOf('|') : -1;
            int levelAt = essenceAt > 0 ? signature.LastIndexOf('|', essenceAt - 1) : -1;
            if (levelAt < 0
                || !int.TryParse(
                    signature.Substring(levelAt + 1, essenceAt - levelAt - 1),
                    System.Globalization.NumberStyles.Integer,
                    System.Globalization.CultureInfo.InvariantCulture,
                    out level))
            {
                return false;
            }

            rest = signature.Substring(0, levelAt) + signature.Substring(essenceAt);
            return true;
        }

        public static bool TryGetPreVisitLabel(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity,
            out string label)
        {
            return TryGetPreVisitLabel(facade, selectionHandler, localization, entity, null, out label);
        }

        private static bool TryGetPreVisitLabel(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity,
            ScoutingDetailLevel? scoutedAt,
            out string label)
        {
            label = string.Empty;
            if (entity == null || localization == null)
            {
                return false;
            }

            try
            {
                ICommanderState selectedCommander = selectionHandler != null ? selectionHandler.SelectedCommander : null;
                ScoutingInfo scouting = GetScouting(facade, entity);
                IDetails details = entity.GetPreVisitDetails(
                    selectedCommander != null ? selectedCommander.Id : -1,
                    false,
                    scoutedAt ?? scouting.DetailLevel,
                    scouting.ProviderKey,
                    selectedCommander != null && selectedCommander.IsAlive);

                label = CaptureLabel(details, localization);
                if (!string.IsNullOrWhiteSpace(label))
                {
                    return true;
                }

                MapEntityPreVisitDetails preVisitDetails = details as MapEntityPreVisitDetails;
                label = preVisitDetails != null ? Localize(localization, preVisitDetails.NameKey) : string.Empty;
                return !string.IsNullOrWhiteSpace(label);
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a map entity's pre-visit label", exception);
                return false;
            }
        }

        /// <summary>
        /// Reads the action the game names in its own tooltip for interacting with this
        /// entity, such as claim, visit or pick up. Returns false when the game names no
        /// action for it.
        /// </summary>
        public static bool TryGetInteractionType(
            IClientAdventureFacade facade,
            int interactorId,
            IMapEntity entity,
            out AdventureInteractionType interactionType)
        {
            interactionType = AdventureInteractionType.None;
            if (entity == null)
            {
                return false;
            }

            try
            {
                ScoutingInfo scouting = GetScouting(facade, entity);
                MapEntityPreVisitDetails details = entity.GetPreVisitDetails(
                    interactorId,
                    false,
                    scouting.DetailLevel,
                    scouting.ProviderKey,
                    true) as MapEntityPreVisitDetails;

                interactionType = details != null ? details.InteractionType : AdventureInteractionType.None;
                return interactionType != AdventureInteractionType.None;
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a map entity's interaction type", exception);
                return false;
            }
        }

        private static ScoutingInfo GetScouting(IClientAdventureFacade facade, IMapEntity entity)
        {
            if (facade == null || facade.Teams == null || entity == null)
            {
                return new ScoutingInfo(ScoutingDetailLevel.VeryFar, null);
            }

            Vector2Int scoutingPosition = entity.Position;
            try
            {
                ILocationComponent location;
                Vector2Int centerPoint;
                if (entity.TryGetComponent<ILocationComponent>(out location)
                    && location.TryCalculateCenterPoint(out centerPoint))
                {
                    scoutingPosition = centerPoint;
                }
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a map entity's centre point", exception);
            }

            ITeamState localTeam = facade.Teams.LocalTeamInControl;
            if (localTeam == null)
            {
                return new ScoutingInfo(ScoutingDetailLevel.VeryFar, null);
            }

            try
            {
                var scouting = facade.CalculateScoutingDetailLevel(scoutingPosition, localTeam);
                return new ScoutingInfo(scouting.Item1, scouting.Item2);
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a tile's scouting detail level", exception);
                return new ScoutingInfo(ScoutingDetailLevel.VeryFar, null);
            }
        }

        private static string CaptureLabel(IDetails details, ILocalizationHandler localization)
        {
            IReadOnlyList<string> lines = DetailsTextUtility.Capture(details, localization).TextLines;
            if (lines == null)
            {
                return string.Empty;
            }

            string artifactLabel;
            if (TryFormatCapturedArtifactLabel(details as ArtifactPreVisitDetails, localization, lines, out artifactLabel))
            {
                return artifactLabel;
            }

            for (int i = 0; i < lines.Count; i++)
            {
                string line = SpokenLines.Clean(lines[i]);
                if (!string.IsNullOrWhiteSpace(line))
                {
                    return line;
                }
            }

            return string.Empty;
        }

        private static bool TryFormatCapturedArtifactLabel(
            ArtifactPreVisitDetails details,
            ILocalizationHandler localization,
            IReadOnlyList<string> capturedLines,
            out string label)
        {
            label = string.Empty;
            if (details == null || details.Artifacts == null || localization == null || capturedLines == null)
            {
                return false;
            }

            List<string> names = new List<string>();
            for (int i = 0; i < capturedLines.Count; i++)
            {
                ArtifactDetails artifact;
                if (!TryGetArtifactForCapturedLine(details.Artifacts, localization, capturedLines[i], out artifact))
                {
                    if (names.Count > 0)
                    {
                        break;
                    }

                    continue;
                }

                string name = Localize(localization, artifact.NameKey);
                string formatted = ArtifactSpeechFormatter.FormatName(localization, name, artifact.PowerLevelColor);
                if (!string.IsNullOrWhiteSpace(formatted))
                {
                    names.Add(formatted);
                }
            }

            label = ModText.JoinList(localization, names);
            return !string.IsNullOrWhiteSpace(label);
        }

        private static bool TryGetArtifactForCapturedLine(
            ArtifactDetails[] artifacts,
            ILocalizationHandler localization,
            string capturedLine,
            out ArtifactDetails artifact)
        {
            artifact = default(ArtifactDetails);
            string normalizedLine = SpokenLines.Clean(capturedLine).Trim();
            if (artifacts == null || string.IsNullOrWhiteSpace(normalizedLine))
            {
                return false;
            }

            for (int i = 0; i < artifacts.Length; i++)
            {
                string name = SpokenLines.Clean(Localize(localization, artifacts[i].NameKey)).Trim();
                if (string.Equals(normalizedLine, name, StringComparison.OrdinalIgnoreCase))
                {
                    artifact = artifacts[i];
                    return true;
                }
            }

            return false;
        }

        /// <summary>The game's own name for a commander, empty where the game cannot answer.</summary>
        public static string GetCommanderName(IClientAdventureFacade facade, ICommanderState commander)
        {
            if (commander == null || facade == null || facade.Commanders == null)
            {
                return string.Empty;
            }

            try
            {
                return facade.Commanders.GetName(commander.Id);
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a commander's name", exception);
                return string.Empty;
            }
        }

        private static string Localize(ILocalizationHandler localization, string key)
        {
            if (string.IsNullOrWhiteSpace(key) || localization == null)
            {
                return string.Empty;
            }

            try
            {
                return localization.GetText(key);
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a localized string", exception);
                return string.Empty;
            }
        }

        private static string AddEssenceVariant(
            ILocalizationHandler localization,
            IMapEntity entity,
            string baseName)
        {
            if (string.IsNullOrWhiteSpace(baseName) || entity == null)
            {
                return baseName ?? string.Empty;
            }

            EssenceType essence;
            if (!TryGetSelectedEssenceVariant(entity, out essence))
            {
                return baseName;
            }

            string essenceName = EssenceText.Name(localization, essence);
            return string.IsNullOrWhiteSpace(essenceName)
                ? baseName
                : ModText.Get(localization, ModStrings.Common.EssenceVariant, baseName, essenceName);
        }

        private static bool TryGetSelectedEssenceVariant(IMapEntity entity, out EssenceType essence)
        {
            essence = default(EssenceType);
            if (entity == null)
            {
                return false;
            }

            try
            {
                ILevelComponent level;
                if (!entity.TryGetComponent<ILevelComponent>(out level)
                    || level == null
                    || !level.IsEssenceOptions
                    || level.Level <= 1)
                {
                    return false;
                }

                essence = level.GetDefinition(level.Level).AssociatedEssence;
                return true;
            }
            catch (Exception exception)
            {
                LogOnce.Warn("AdventureMapEntityLabel: reading a map entity's essence variant", exception);
                return false;
            }
        }

        private struct ScoutingInfo
        {
            public ScoutingInfo(ScoutingDetailLevel detailLevel, string providerKey)
            {
                DetailLevel = detailLevel;
                ProviderKey = providerKey;
            }

            public ScoutingDetailLevel DetailLevel;

            public string ProviderKey;
        }
    }
}
