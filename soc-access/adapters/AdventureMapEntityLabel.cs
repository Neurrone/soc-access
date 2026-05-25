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
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    internal static class AdventureMapEntityLabel
    {
        public static string GetMapEntityName(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity)
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
                    return customName;
                }
            }

            string preVisitName;
            if (TryGetPreVisitLabel(facade, selectionHandler, localization, entity, out preVisitName))
            {
                return preVisitName;
            }

            string localizedName = Localize(localization, entity.NameKey);
            if (!string.IsNullOrWhiteSpace(localizedName))
            {
                return localizedName;
            }

            if (!string.IsNullOrWhiteSpace(entity.Name))
            {
                return entity.Name;
            }

            return entity.NameKey;
        }

        public static bool TryGetPreVisitLabel(
            IClientAdventureFacade facade,
            ISelectionHandler selectionHandler,
            ILocalizationHandler localization,
            IMapEntity entity,
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
                    scouting.DetailLevel,
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
                SocAccessPlugin.Instance?.LogWarning("AdventureMapEntityLabel failed to read map entity pre-visit label: " + exception.Message);
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
            catch
            {
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
            catch
            {
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
                string line = SpeechTextSanitizer.Normalize(lines[i]);
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
            string normalizedLine = SpeechTextSanitizer.Normalize(capturedLine).Trim();
            if (artifacts == null || string.IsNullOrWhiteSpace(normalizedLine))
            {
                return false;
            }

            for (int i = 0; i < artifacts.Length; i++)
            {
                string name = SpeechTextSanitizer.Normalize(Localize(localization, artifacts[i].NameKey)).Trim();
                if (string.Equals(normalizedLine, name, StringComparison.OrdinalIgnoreCase))
                {
                    artifact = artifacts[i];
                    return true;
                }
            }

            return false;
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
            catch
            {
                return string.Empty;
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
