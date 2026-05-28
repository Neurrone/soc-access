using System;
using SongsOfConquest.Client.UI;

namespace SongsOfConquestAccess.Adapters
{
    internal enum DialogAction
    {
        Body = 0,
        Positive = 1,
        Negative = 2
    }

    internal interface IMessageDialogAdapter
    {
        object SourceKey { get; }

        string Title { get; }

        string Body { get; }

        string PositiveLabel { get; }

        string NegativeLabel { get; }

        bool HasPositiveAction { get; }

        bool HasNegativeAction { get; }

        bool IsPositiveActionEnabled { get; }

        bool IsNegativeActionEnabled { get; }

        bool IsPresent();

        void SyncNativeSelection(DialogAction action);

        bool ActivateAction(DialogAction action);
    }

    internal interface IInputDialogAdapter
    {
        bool HasInputField { get; }

        IUITextMeshInputField InputField { get; }

        void AttachInputSubmit(Action<IUITextMeshInputField, string> handler);

        void DetachInputSubmit(Action<IUITextMeshInputField, string> handler);
    }
}
