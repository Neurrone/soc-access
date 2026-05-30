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

        public bool Wrapped { get; set; }

        public string NoResultsText { get; set; }

        public SpeechRequest ToSpeechRequest(
            Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, IScannerSpeechContext> speechContextProvider)
        {
            if (Status == ScannerCommandStatus.NoResults)
            {
                return BuildNoResults();
            }

            if (speechContextProvider == null || Result == null)
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
