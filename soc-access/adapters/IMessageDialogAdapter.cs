using System;
using System.Collections.Generic;
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

    public interface IMessageDialogAdapter : IPresent
    {
        object SourceKey { get; }

        string Title { get; }

        /// <summary>The whole body as one line, the paragraphs joined by a space.</summary>
        string Body { get; }

        /// <summary>The body's paragraphs, one line each, as the game broke them.</summary>
        IList<string> BodyLines { get; }

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

        /// <summary>The layer this source pushes onto the game's own selection-layer stack while it
        /// is showing, or null where it pushes none. The stack is a LIFO the whole game shares
        /// (<c>UISelectionLayerStack</c>, one instance off the project container), so its top is the
        /// dialog raised LAST, which is the one drawn over the others - and the one whose buttons a
        /// press must reach. The random-event menu pushes nothing and answers null.</summary>
        IUISelectionLayer SelectionLayer { get; }

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
