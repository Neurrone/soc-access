namespace SongsOfConquestAccess.Adapters
{
    internal enum DialogAction
    {
        Body = 0,
        Positive = 1,
        Negative = 2
    }

    internal interface IQuestionDialogAdapter
    {
        object SourceKey { get; }

        string Title { get; }

        string Body { get; }

        string PositiveLabel { get; }

        string NegativeLabel { get; }

        bool HasPositiveAction { get; }

        bool HasNegativeAction { get; }

        bool IsPresent();

        void SyncNativeSelection(DialogAction action);

        bool ActivateAction(DialogAction action);
    }
}
