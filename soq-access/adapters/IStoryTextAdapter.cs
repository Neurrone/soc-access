namespace SongsOfConquestAccess.Adapters
{
    internal interface IStoryTextAdapter
    {
        object SourceKey { get; }

        string Title { get; }

        string Body { get; }

        bool IsPresent();

        bool AdvanceNow();
    }
}
