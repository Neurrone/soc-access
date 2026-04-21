using SongsOfConquestAccess.Adapters;

namespace SongsOfConquestAccess.Screens
{
    internal sealed class QuestionDialogRuntimeScreenProbe : IRuntimeScreenProbe
    {
        private readonly QuestionDialogProbe _probe = new QuestionDialogProbe();

        public Screen TryGetActiveScreen()
        {
            QuestionDialogAdapter adapter = _probe.FindActiveQuestionDialog();
            if (adapter == null)
            {
                return null;
            }

            return new QuestionDialogScreen(adapter.SourceKey, adapter);
        }
    }
}
