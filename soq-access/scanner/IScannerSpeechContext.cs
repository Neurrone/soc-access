using SongsOfConquestAccess.Speech;

namespace SongsOfConquestAccess.Scanner
{
    internal interface IScannerSpeechContext
    {
        SpeechRequest ToSpeechRequest();
    }
}
