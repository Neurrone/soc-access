using System.Collections.Generic;

namespace SongsOfConquestAccess.Adapters
{
    public interface IStoryTextAdapter : IPresent
    {
        object SourceKey { get; }

        string Title { get; }

        /// <summary>The whole body as one line, the paragraphs joined by a space.</summary>
        string Body { get; }

        /// <summary>The body's paragraphs, one line each, as the game broke them.</summary>
        IList<string> BodyLines { get; }


        bool AdvanceNow();
    }
}
