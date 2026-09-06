using System;
using SongsOfConquest.Client.UI;
using UnityEngine;

namespace SongsOfConquestAccess.Adapters
{
    public enum DialogAction
    {
        Body = 0,
        Positive = 1,
        Negative = 2
    }

    public interface IMessageDialogAdapter
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

        /// <summary>Whether the game itself registers keyboard input that closes this source - the
        /// dialogs differ, and this is a fact about each native source rather than about dialogs.
        /// </summary>
        bool GameHandlesEscape { get; }

        bool IsPresent();

        /// <summary>The component the game draws this action's button with, or null where the source
        /// has no such button. Where a button IS on the screen is a game fact; what the reading order
        /// makes of it is not.</summary>
        Component ButtonOf(DialogAction action);

        void SyncNativeSelection(DialogAction action);

        bool ActivateAction(DialogAction action);
    }

    public interface IInputDialogAdapter
    {
        bool HasInputField { get; }

        IUITextMeshInputField InputField { get; }

        void AttachInputSubmit(Action<IUITextMeshInputField, string> handler);

        void DetachInputSubmit(Action<IUITextMeshInputField, string> handler);
    }
}
