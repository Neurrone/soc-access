using System;
using System.Collections.Generic;
using SongsOfConquestAccess.Speech;

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

        public bool IncludePath { get; set; }

        public bool Wrapped { get; set; }

        public SpeechRequest ToSpeechRequest(
            Func<ScannerResult, IReadOnlyList<ScannerDirectionStep>, int, int, IScannerSpeechContext> speechContextProvider)
        {
            if (Status == ScannerCommandStatus.NoResults)
            {
                return new SpeechRequest("No scanner results", interrupt: false);
            }

            if (speechContextProvider == null || Result == null)
            {
                return new SpeechRequest("No scanner results", interrupt: false);
            }

            IScannerSpeechContext context = speechContextProvider(Result, Directions, ResultIndex, ResultCount);
            SpeechRequest request = context != null
                ? context.ToSpeechRequest()
                : new SpeechRequest("No scanner results", interrupt: false);

            if (IncludePath && !string.IsNullOrWhiteSpace(CategoryLabel) && !string.IsNullOrWhiteSpace(SubcategoryLabel))
            {
                return new SpeechRequest(CategoryLabel + ", " + SubcategoryLabel + ". " + request.Text, request.Interrupt);
            }

            return request;
        }
    }
}
