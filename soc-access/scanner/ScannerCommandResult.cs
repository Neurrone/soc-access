using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Localization;
using SongsOfConquestAccess.Speech;
using UnityEngine;

namespace SongsOfConquestAccess.Scanner
{
    internal enum ScannerCommandStatus
    {
        Result,
        NoResults
    }

    internal sealed class ScannerCommandResult
    {
        public ScannerCommandResult(ScannerCommandStatus status)
        {
            Status = status;
        }

        public ScannerCommandStatus Status { get; private set; }

        public ScannerResult Result { get; set; }

        public string CategoryLabel { get; set; }

        public string SubcategoryLabel { get; set; }

        public int ResultIndex { get; set; }

        public int ResultCount { get; set; }

        public IReadOnlyList<ScannerDirectionStep> Directions { get; set; }

        public bool HasOrigin { get; set; }

        public Vector2Int Origin { get; set; }

        public bool IncludePath { get; set; }

        /// <summary>
        /// Narrows the announcement to how far away the current result is and
        /// which way it lies. Used by the dedicated readout key, which exists so
        /// the player can re-check a bearing without sitting through the whole
        /// result again.
        /// </summary>
        public bool DistanceAndDirectionOnly { get; set; }

        public bool Wrapped { get; set; }

        public string NoResultsText { get; set; }

        public SpeechRequest ToSpeechRequest(
            Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, IScannerSpeechContext> speechContextProvider)
        {
            if (Status == ScannerCommandStatus.NoResults)
            {
                return BuildNoResults();
            }

            if (Result == null)
            {
                return BuildNoResults();
            }

            if (DistanceAndDirectionOnly)
            {
                return BuildDistanceAndDirection();
            }

            if (speechContextProvider == null)
            {
                return BuildNoResults();
            }

            IScannerSpeechContext context = speechContextProvider(Result, Directions, ResultIndex, ResultCount);
            SpeechRequest request = context != null
                ? context.ToSpeechRequest()
                : BuildNoResults();

            if (IncludePath && !string.IsNullOrWhiteSpace(CategoryLabel) && !string.IsNullOrWhiteSpace(SubcategoryLabel))
            {
                return new SpeechRequest(
                    ModText.Get(ModStrings.UI.ScannerPath, CategoryLabel, SubcategoryLabel, request.Text),
                    request.Interrupt);
            }

            return request;
        }

        private SpeechRequest BuildDistanceAndDirection()
        {
            return new SpeechRequest(
                FormatDistanceAndDirection(ModSettings.ScannerUsesLongDirections),
                interrupt: false);
        }

        /// <summary>
        /// The distance is the sum of the direction runs rather than a separate
        /// geometry, so the number spoken always matches the steps spoken after
        /// it, on square and hex grids alike.
        /// </summary>
        internal string FormatDistanceAndDirection(bool useLongDirections)
        {
            string directions = ScannerSpeechUtility.FormatDirections(Directions, useLongDirections);
            if (Directions == null || Directions.Count == 0)
            {
                return directions;
            }

            int tiles = 0;
            for (int i = 0; i < Directions.Count; i++)
            {
                tiles += Directions[i].Count;
            }

            return ModText.Get(
                ModStrings.Scanner.DistanceAndDirection,
                ModText.Plural(ModStrings.Common.TileCount, tiles, tiles),
                directions);
        }

        private SpeechRequest BuildNoResults()
        {
            return new SpeechRequest(
                string.IsNullOrWhiteSpace(NoResultsText)
                    ? ModText.Get(ModStrings.UI.NoScannerResults)
                    : NoResultsText,
                interrupt: false);
        }
    }
}
